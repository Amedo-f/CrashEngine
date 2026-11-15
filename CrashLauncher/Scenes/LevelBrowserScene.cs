using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class LevelBrowserScene : Scene
{
    private readonly string  _extractedRoot;
    private readonly string  _scriptOut;
    private readonly string? _projectPath;

    private Dictionary<string, List<(string Label, string Rm2)>> _groups = new();

    private readonly object _logLock = new();
    private readonly List<string> _log = new();

    private CrashProject? _project;
    private bool _startOpened; // Amedo 2026-11-12

    public int Revision { get; private set; } // Amedo 2026-11-12
    public bool HasProject => _project is not null;
    public bool IsNewGame => _project?.IsNewGame ?? false;

    public LevelBrowserScene(string extractedRoot, string scriptOut, string? projectPath = null)
    {
        _extractedRoot = extractedRoot;
        _scriptOut     = scriptOut;
        _projectPath   = projectPath;
    }

    protected override void Build()
    {
        var pipeEnt  = new Entity("RenderPipeline");
        var pipe     = pipeEnt.Add(new RenderPipeline());
        pipe.FogColor = new System.Numerics.Vector3(0.07f, 0.07f, 0.07f);
        AddRoot(pipeEnt);

        if (_projectPath is not null)
        {
            GraphicsSettings.FrameLimits = CrashEngine.Runtime.Region.RegionProfile.ForDisc(_extractedRoot).FrameLimits; // Amedo 2026-11-11
            GraphicsSettings.UseFile(Path.Combine(_projectPath, "graphics.json")); // Amedo
        }

        ScanLevels();
        Log($"CrashEngine ready  —  {_groups.Values.Sum(g => g.Count)} chunks found");
        Log($"Extracted: {_extractedRoot}");
        if (_project is { IsNewGame: true })
            Log($"New Game mode: ON — {_project.ClaimedLevels.Count} level slot(s) claimed. Build ISO will exclude every other original level.");

        if (_project is not null && !_startOpened) // Amedo 2026-11-12
        {
            _startOpened = true;
            _pendingLoadPath = StartScene;
        }
    }

    public override void OnImGuiRender()
    {
        if (TakePendingOpen() is { } path)
        {
            LoadLevel(path);
            return;
        }

        int sw = Engine.Instance.Width;
        int sh = Engine.Instance.Height;

        float browserW = 320f;
        float logH     = 200f;

        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(new Vector2(browserW, sh - logH));
        ImGui.Begin("Level Browser",
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus);

        // Amedo 2026-09-21
        if (ImGui.Button("< Back to Projects", new Vector2(-1f, 0f)))
        {
            ProjectCreator.Close(_scriptOut); // Amedo 2026-11-12
            ImGui.End();
            return;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Close the current project and return to the project menu (New / Open).\nNothing is lost -- saved edits stay in this project's folder.");
        }
        ImGui.Separator();

        // Amedo 2026-09-21
        if (_projectPath is not null)
        {
            ImGui.BeginDisabled();
            ImGui.Button("All Assets  —  Under development", new Vector2(-1f, 0f));
            ImGui.EndDisabled();
        }

        if (_project is not null)
        {
            string label = _project.IsNewGame ? $"New Game: ON  ({_project.ClaimedLevels.Count} claimed)" : "+ New Game (off)";
            if (ImGui.Button(label, new Vector2(-1f, 0f)))
                SetNewGame(!_project.IsNewGame);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(NewGameHelp);
        }

        if (ImGui.Button("+ New Scene", new Vector2(-1f, 0f)))
            StartNewScene();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(NewSceneHelp);

        if (MovingScene is { } moving)
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), $"Moving {moving} —");
            ImGui.TextWrapped("click a GROUP to move it inside (same filename), or an existing LEVEL to REPLACE that exact path.");
            if (ImGui.Button("Cancel Move", new Vector2(-1f, 0f)))
                CancelMove();
            ImGui.Separator();
        }
        else
        {
            ImGui.TextDisabled("Click a chunk to load it");
            ImGui.Separator();
        }
        if (MoveError is { } moveError)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), moveError);

        DrawScenePopups();

        foreach (var (group, chunks) in VisibleGroups())
        {
            bool picking = MovingScene is not null;
            if (picking) ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.4f, 0.55f, 0.6f));
            bool groupOpen = ImGui.TreeNodeEx(group, ImGuiTreeNodeFlags.None);
            if (picking)
            {
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                    MoveInto(GamePaths.Folder(chunks[0].Rm2));
            }
            else if (_project is not null && ImGui.BeginPopupContextItem($"##ctxgroup_{group}"))
            {
                if (ImGui.MenuItem("Add Scene..."))
                    StartClaim();
                ImGui.EndPopup();
            }
            if (groupOpen)
            {
                foreach (var (label, rm2) in chunks.OrderBy(c => c.Label))
                {
                    if (picking) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.85f, 1f, 1f));
                    bool clicked = ImGui.Selectable($"  {label}");
                    if (picking) ImGui.PopStyleColor();

                    if (picking)
                    {
                        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        if (clicked) MoveOnto(rm2);
                    }
                    else if (clicked)
                        _pendingLoadPath = rm2;

                    if (!picking && ImGui.BeginPopupContextItem($"##ctx_{rm2}"))
                    {
                        if (ImGui.MenuItem("Open"))
                            _pendingLoadPath = rm2;
                        if (ImGui.MenuItem("Move..."))
                            StartMove(rm2);
                        if (ImGui.MenuItem("Rename..."))
                            StartRename(rm2);
                        if (ImGui.MenuItem("Delete"))
                            DeleteSavedScene(rm2);
                        if (IsClaimed(rm2) && ImGui.MenuItem("Unclaim (restore original)"))
                            Unclaim(rm2);
                        ImGui.EndPopup();
                    }
                }
                ImGui.TreePop();
            }
        }
        ImGui.End();

        ImGui.SetNextWindowPos(new Vector2(0, sh - logH));
        ImGui.SetNextWindowSize(new Vector2(sw, logH));
        ImGui.Begin("Output",
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus);

        foreach (var line in GetLog())
            ImGui.TextUnformatted(line);

        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1f);

        ImGui.End();

        ImGui.SetNextWindowPos(new Vector2(sw - 240f, 0));
        ImGui.SetNextWindowSize(new Vector2(240f, 160f));
        ImGui.Begin("Info",
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus);

        ImGui.Text($"FPS: {1f / EngineTime.Delta:F0}");
        ImGui.Separator();
        ImGui.TextWrapped("Controls:\nWASD = move\nShift = run\nCtrl = crouch");
        ImGui.End();
    }

    public void OpenLevel(string rm2Rel) => LoadLevel(rm2Rel);

    private void LoadLevel(string rm2Rel)
    {
        var sm2Rel = Path.ChangeExtension(rm2Rel, ".sm2");
        Log($"Loading {rm2Rel}...");

        var addCrashFrom = _pendingAddCrash is { } add && add.Rm2.Equals(rm2Rel, StringComparison.OrdinalIgnoreCase) ? add.Donor : null; // Amedo 2026-11-12
        _pendingAddCrash = null;
        RememberScene(rm2Rel);

        var gameScene = new GameScene(_extractedRoot, rm2Rel, sm2Rel, _scriptOut, this, addCrashFrom);
        Engine.Instance.ActiveScene = gameScene;
    }

    private const string TemplateLevelPath = @"Levels\Earth\Hub\beach";

    private const string NewSceneTemplatePath = @"Levels\Earth\Hub\totemex";
    private const string CrashDonorLevelPath  = @"Levels\Earth\Hub\beach";

    // Amedo 2026-11-12
    public string StartScene =>
        _project?.LastScene is { } last && IsVisible(last) ? last : TemplateLevelPath + ".rm2";

    private void RememberScene(string rm2Rel)
    {
        if (ReadProject() is not { } project || rm2Rel.Equals(project.LastScene, StringComparison.OrdinalIgnoreCase)) return;
        project.LastScene = rm2Rel;
        SaveProject(project);
    }

    private CrashProject? ReadProject()
    {
        if (_projectPath is not null) _project = CrashProject.Open(_projectPath) ?? _project;
        return _project;
    }

    private void SaveProject(CrashProject project)
    {
        project.Save();
        _project = project;
        Revision++;
    }

    private List<string> GetTemplateLevelList()
    {
        var list = new List<string>();
        try
        {
            using var pkg = PackageReader.Open(PristineArchiveSource);
            foreach (var rec in pkg.GetByExtension(".rm2"))
                list.Add(rec.Path.Replace('/', '\\')[..^4]);
        }
        catch {  }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    public void Log(string msg)
    {
        lock (_logLock)
        {
            _log.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            if (_log.Count > 200) _log.RemoveAt(0);
        }
        Console.WriteLine(msg);
    }

    public IReadOnlyList<string> GetLog() { lock (_logLock) return _log.ToArray(); }

    private void AddToGroups(string rel)
    {
        var parts = rel.Split('\\');
        string group = parts.Length >= 3
            ? string.Join(" / ", parts.Skip(1).Take(parts.Length - 2))
            : parts[0];
        var label = Path.GetFileNameWithoutExtension(rel);

        if (!_groups.TryGetValue(group, out var list))
            _groups[group] = list = new();
        list.Add((label, rel));
    }

    // Amedo 2026-11-12
    private IEnumerable<(string Group, List<(string Label, string Rm2)> Chunks)> VisibleGroups()
    {
        bool newGameMode = _project?.IsNewGame ?? false;
        HashSet<string>? claimedSet = newGameMode
            ? new HashSet<string>(_project!.ClaimedLevels.Select(p => p.Replace('/', '\\')), StringComparer.OrdinalIgnoreCase)
            : null;
        bool IsAlwaysVisible(string rm2) =>
            rm2.StartsWith(@"Levels\Custom\", StringComparison.OrdinalIgnoreCase) ||
            rm2[..^4].Equals(TemplateLevelPath, StringComparison.OrdinalIgnoreCase);

        foreach (var (group, chunksAll) in _groups.OrderBy(g => g.Key))
        {
            var chunks = newGameMode
                ? chunksAll.Where(c => IsAlwaysVisible(c.Rm2) || claimedSet!.Contains(c.Rm2[..^4])).ToList()
                : chunksAll;
            if (newGameMode && chunks.Count == 0) continue;
            yield return (group, chunks);
        }
    }

    public List<string> VisibleScenes() => VisibleGroups().SelectMany(g => g.Chunks.Select(c => c.Rm2)).ToList();

    public bool IsVisible(string rm2) => VisibleScenes().Contains(rm2, StringComparer.OrdinalIgnoreCase);

    private void ScanLevels()
    {
        ReadProject(); // Amedo 2026-11-12
        _groups.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var pkg = PackageReader.Open(PristineArchiveSource);
            foreach (var rec in pkg.GetByExtension(".rm2"))
            {
                var rel = rec.Path.Replace('/', '\\');
                seen.Add(rel);
                AddToGroups(rel);
            }
        }
        catch (Exception ex)
        {
            Log($"Package scan failed: {ex.Message}");
        }

        try
        {
            if (Directory.Exists(SavedChunksDir))
            {
                foreach (var file in Directory.GetFiles(SavedChunksDir, "*.rm2", SearchOption.AllDirectories))
                {
                    var rel = GamePaths.FromDisk(Path.GetRelativePath(SavedChunksDir, file));
                    if (seen.Add(rel)) AddToGroups(rel);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"SavedChunks scan failed: {ex.Message}");
        }
        Revision++;
    }
}
