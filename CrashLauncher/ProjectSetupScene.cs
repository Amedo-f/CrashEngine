using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed class ProjectSetupScene : Scene
{
    private readonly string _scriptOut;

    private string _name     = "New project";
    private string _projPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrashEngineProjects");
    private string _discPath = "";

    private string _status   = "";
    private bool   _creating = false;
    private List<string> _recents = new();

    private List<CrashProject> _incomplete = new();

    private readonly object _logLock = new();
    private readonly List<string> _unpackLog = new();
    private volatile float  _progress;
    private string? _pendingDiscPath;
    private string? _pendingProjectPath;

    // Amedo 2026-09-20
    private System.Threading.CancellationTokenSource? _cts;
    private System.Threading.ManualResetEventSlim? _pauseGate;
    private volatile bool _paused;

    private readonly ImGuiFileBrowser _fileBrowser = new();

    public ProjectSetupScene(string scriptOut) => _scriptOut = scriptOut;

    protected override void Build()
    {
        var pipeEnt = new Entity("RenderPipeline");
        var pipe    = pipeEnt.Add(new RenderPipeline());
        pipe.FogColor = new Vector3(0.09f, 0.09f, 0.11f);
        AddRoot(pipeEnt);

        _recents = CrashProject.GetRecents();
        _incomplete = CrashProject.FindIncompleteProjects();
    }

    public override void OnImGuiRender()
    {
        _fileBrowser.Draw();

        if (_pendingDiscPath is not null)
        {
            var disc = _pendingDiscPath;
            var proj = _pendingProjectPath;
            _pendingDiscPath = null;
            _pendingProjectPath = null;
            var scriptOut = proj is not null ? Path.Combine(proj, "Scripts") : _scriptOut;
            Engine.Instance.ActiveScene = new LevelBrowserScene(disc, scriptOut, proj);
            return;
        }

        int sw = Engine.Instance.Width, sh = Engine.Instance.Height;

        if (_incomplete.Count > 0 && !_creating)
        {
            var proj = _incomplete[0];
            float iw = 560f, ih = 140f;
            ImGui.SetNextWindowPos(new Vector2((sw - iw) / 2f, (sh - ih) / 2f));
            ImGui.SetNextWindowSize(new Vector2(iw, ih));
            ImGui.Begin("Incomplete project found",
                ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
            ImGui.TextWrapped($"\"{proj.Name}\" at \"{proj.ProjectPath}\" was never fully " +
                               "created — the app was likely closed while it was still copying " +
                               "or unpacking. Continue creating it, or delete it and start over?");
            ImGui.Spacing();
            if (ImGui.Button("Continue", new Vector2(160f, 30f)))
            {
                _incomplete.RemoveAt(0);
                RunCreateOrResume(proj);
            }
            ImGui.SameLine();
            if (ImGui.Button("Delete", new Vector2(160f, 30f)))
            {
                CrashProject.DeleteProject(proj.ProjectPath);
                _incomplete.RemoveAt(0);
            }
            ImGui.End();
            return;
        }

        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(new Vector2(sw, sh));
        ImGui.Begin("Create Project...",
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

        float labelW = 170f;

        void Row(string label, ref string value, string id, bool browse = false,
                 string? browseFilter = null, bool pickFolderOfFile = false, Action<string>? onBrowsed = null)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.SetCursorPosX(labelW - ImGui.CalcTextSize(label).X);
            ImGui.TextUnformatted(label);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(browse ? -60f : -10f);
            ImGui.InputText($"##{id}", ref value, 512);
            if (browse)
            {
                ImGui.SameLine();
                if (ImGui.Button($"...##{id}", new Vector2(-10f, 0f)))
                {
                    ShowOpenFileDialog($"Select {label}", browseFilter ?? "All Files\0*.*\0\0", picked =>
                    {
                        if (picked is null) return;
                        onBrowsed?.Invoke(pickFolderOfFile ? Path.GetDirectoryName(picked)! : picked);
                    });
                }
            }
        }

        Row("Project name: ", ref _name, "name");
        Row("Project path: ", ref _projPath, "ppath");
        Row("PS2 Disc content path: ", ref _discPath, "dpath", browse: true,
            browseFilter: "PS2 disc root (SYSTEM.CNF)\0system.cnf\0All Files\0*.*\0\0",
            pickFolderOfFile: true, onBrowsed: v => _discPath = v);

        ImGui.TextDisabled("Disc contents are always copied into the project's own folder —");
        ImGui.TextDisabled("each project keeps its own pristine, isolated copy of the source disc.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_creating) ImGui.BeginDisabled();
        if (ImGui.Button("Create", new Vector2(-10f, 32f)))
            CreateProject();
        if (_creating) ImGui.EndDisabled();

        if (_status.Length > 0)
        {
            var col = _status.StartsWith("ERROR")
                ? new Vector4(1f, 0.35f, 0.35f, 1f)
                : new Vector4(0.6f, 0.9f, 0.6f, 1f);
            ImGui.TextColored(col, _status);
        }

        if (_creating)
        {
            ImGui.ProgressBar(_progress, new Vector2(-10f, 22f), $"{_progress * 100f:F1}%");

            // Amedo 2026-09-20
            if (!_paused)
            {
                if (ImGui.Button("Pause", new Vector2(120f, 26f))) { _paused = true; _pauseGate?.Reset(); }
            }
            else
            {
                if (ImGui.Button("Resume", new Vector2(120f, 26f))) { _paused = false; _pauseGate?.Set(); }
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120f, 26f))) { _cts?.Cancel(); _pauseGate?.Set(); }
            if (_paused)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), "Paused");
            }

            ImGui.BeginChild("##unpacklog", new Vector2(-10f, 140f), ImGuiChildFlags.None);
            lock (_logLock)
            {
                int start = Math.Max(0, _unpackLog.Count - 200);
                for (int i = start; i < _unpackLog.Count; i++)
                    ImGui.TextUnformatted(_unpackLog[i]);
            }
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                ImGui.SetScrollHereY(1f);
            ImGui.EndChild();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("Open existing project");

        if (ImGui.Button("Open Project (.tson)...", new Vector2(220f, 0f)))
        {
            ShowOpenFileDialog("Open Project", "TT Lab / CrashEngine Project\0*.tson\0All Files\0*.*\0\0",
                picked => { if (picked is not null) OpenProject(picked); });
        }

        foreach (var recent in _recents)
        {
            if (ImGui.Selectable($"  {recent}"))
                OpenProject(recent);
        }

        ImGui.End();
    }

    private void CreateProject()
    {
        if (_name.Trim().Length == 0 || _projPath.Trim().Length == 0)
        { _status = "ERROR: Project name and path are required!"; return; }

        if (!CrashProject.ValidateDiscPS2(_discPath))
        { _status = "ERROR: Improper PS2 disc content provided!"; return; }

        var project = new CrashProject
        {
            Name = _name.Trim(),
            Path = _projPath.Trim(),
            DiscContentPathPS2 = _discPath.Trim(),
        };

        if (Directory.Exists(project.ProjectPath) &&
            Directory.GetFiles(project.ProjectPath, "*.tson").Length > 0)
        {
            _status = $"ERROR: A project already exists at \"{project.ProjectPath}\" — " +
                      "pick a different project name or path, or use \"Open existing project\" below.";
            return;
        }

        RunCreateOrResume(project);
    }

    private void RunCreateOrResume(CrashProject project)
    {
        _creating = true;
        _progress = 0f;
        lock (_logLock) _unpackLog.Clear();
        _status = "Creating project...";
        var started = DateTime.Now;

        // Amedo 2026-09-20
        _paused = false;
        _cts = new System.Threading.CancellationTokenSource();
        _pauseGate = new System.Threading.ManualResetEventSlim(true);
        var ct = _cts.Token;
        var gate = _pauseGate;

        void WLog(string msg) { lock (_logLock) _unpackLog.Add(msg); Console.WriteLine(msg); }

        Task.Run(() =>
        {
            try
            {
                project.CreateProjectStructure();

                project.Save();

                WLog("Copying disc contents to project...");
                project.CopyDiscContents(p => _progress = p * 0.5f, ct, gate);

                WLog("Reading game archives...");
                using (var pkg = PackageReader.Open(project.DiscContentPathPS2!))
                {
                    if (pkg.Records.Count == 0)
                    { _status = "ERROR: No game archives found in Crash6/!"; return; }
                    WLog($"{pkg.Records.Count} records in archive");

                    var manifestPath = Path.Combine(project.ProjectPath, ".unpack_manifest.txt");
                    var doneSet = File.Exists(manifestPath)
                        ? new HashSet<string>(File.ReadAllLines(manifestPath))
                        : new HashSet<string>();
                    var manifestLock = new object();
                    void OnRecordDone(string path) { lock (manifestLock) File.AppendAllText(manifestPath, path + "\n"); }

                    WLog("Unpacking PS2 assets...");
                    var result = AssetUnpacker.Unpack(pkg, project.ProjectPath, WLog,
                        p => _progress = 0.5f + p * 0.5f, doneSet, OnRecordDone, ct, gate);
                    WLog($"Unpacked {result.Files} files, {result.Chunks} chunks, " +
                         $"{result.Assets} assets, {result.Textures} textures " +
                         $"({result.Skipped} already done, {result.Errors} warnings)");

                    try { File.Delete(manifestPath); } catch {  }
                }

                WLog("Serializing project...");
                project.Completed = true;
                project.Save();
                CrashProject.AddRecent(project.ProjectPath);
                WLog($"Project created in {DateTime.Now - started:mm\\:ss}");

                _status = $"Project created in {DateTime.Now - started:mm\\:ss}";
                _pendingProjectPath = project.ProjectPath;
                _pendingDiscPath = project.DiscContentPathPS2;
            }
            catch (OperationCanceledException)
            {
                WLog("Cancelled by user.");
                _status = "Extraction cancelled — progress is saved; resume it later from \"Incomplete project found\".";
            }
            catch (Exception ex)
            {
                _status = $"ERROR: {ex.Message}";
            }
            finally
            {
                _creating = false;
                _paused = false;
                _incomplete = CrashProject.FindIncompleteProjects();
            }
        });
    }

    private void OpenProject(string folderOrTson)
    {
        var project = CrashProject.Open(folderOrTson);
        if (project is null || string.IsNullOrEmpty(project.DiscContentPathPS2))
        { _status = "ERROR: Not a valid project (missing .tson or disc path)"; return; }

        if (!Directory.Exists(project.DiscContentPathPS2))
        { _status = $"ERROR: Disc content path no longer exists: {project.DiscContentPathPS2}"; return; }

        CrashProject.AddRecent(project.ProjectPath);
        var scriptOut = Path.Combine(project.ProjectPath, "Scripts");
        Engine.Instance.ActiveScene =
            new LevelBrowserScene(project.DiscContentPathPS2, scriptOut, project.ProjectPath);
    }

    private void ShowOpenFileDialog(string title, string filter, Action<string?> callback) =>
        _fileBrowser.OpenFile(title, filter, callback);
}
