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

    private string _newSceneName = "";
    private string? _createError;
    private string _createTemplatePath = NewSceneTemplatePath;
    private bool _createBlank = false;
    private string? _pendingLoadPath;
    private string? _pendingAddCrashFrom;
    private string? _pendingDeletePath;

    private string? _movingSourcePath;
    private string? _moveError;

    private string? _renamingPath;
    private string  _renameNewName = "";
    private string? _renameError;
    private bool    _wantOpenRenamePopup;

    private CrashProject? _project;
    private bool          _wantOpenClaimPopup;
    private string        _claimFilter = "";
    private string?       _claimError;
    private string         _claimTemplatePath   = TemplateLevelPath;
    private string         _claimTemplateFilter = "";

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

        ScanLevels();
        Log($"CrashEngine ready  —  {_groups.Values.Sum(g => g.Count)} chunks found");
        Log($"Extracted: {_extractedRoot}");

        if (_projectPath is not null)
        {
            GraphicsSettings.FrameLimits = CrashEngine.Runtime.Region.RegionProfile.ForDisc(_extractedRoot).FrameLimits; // Amedo 2026-11-11
            GraphicsSettings.UseFile(Path.Combine(_projectPath, "graphics.json")); // Amedo
            _project = CrashProject.Open(_projectPath);
            if (_project is { IsNewGame: true })
                Log($"New Game mode: ON — {_project.ClaimedLevels.Count} level slot(s) claimed. Build ISO will exclude every other original level.");
        }
    }

    public override void OnImGuiRender()
    {
        if (_pendingDeletePath is not null)
        {
            var path = _pendingDeletePath;
            _pendingDeletePath = null;
            DeleteSavedScene(path);
        }

        if (_pendingLoadPath is not null)
        {
            var path = _pendingLoadPath;
            _pendingLoadPath = null;
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
            Engine.Instance.ActiveScene = new ProjectSetupScene(_scriptOut);
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
            bool isNewGame = _project.IsNewGame;
            string label = isNewGame ? $"New Game: ON  ({_project.ClaimedLevels.Count} claimed)" : "+ New Game (off)";
            if (ImGui.Button(label, new Vector2(-1f, 0f)))
            {
                _project.IsNewGame = !_project.IsNewGame;
                _project.Save();
                if (_project.IsNewGame)
                {
                    ScanLevels(); // Amedo 2026-09-22
                    Log("New Game mode: ON — Build ISO will now exclude every original level not claimed via \"Add Scene...\".");
                    if (_project.ExcludedCutscenes.Count == 0)
                    {
                        var fmvRoot = System.IO.Path.Combine(_extractedRoot, "FMV");
                        if (System.IO.Directory.Exists(fmvRoot))
                        {
                            foreach (var f in System.IO.Directory.EnumerateFiles(fmvRoot, "*.pss", System.IO.SearchOption.AllDirectories))
                            {
                                var rel = System.IO.Path.GetRelativePath(fmvRoot, f);
                                if (BuildIsoToPS2.IsStoryCutscene(System.IO.Path.GetFileName(f)))
                                    _project.ExcludedCutscenes.Add(rel);
                            }
                            _project.Save();
                            Log($"New Game mode: pre-selected {_project.ExcludedCutscenes.Count} story cutscene(s) to exclude — adjust per-file under UI Browser → FMV / Cutscenes.");
                        }
                    }
                }
                else
                {
                    if (TryRestoreOriginalArchive(out var restoreErr))
                    {
                        Log("New Game mode: OFF — live archive restored from backup, every original level is back.");
                        ScanLevels();
                    }
                    else
                    {
                        Log($"New Game mode: OFF, but the live archive restore FAILED — {restoreErr} " +
                             "(the browser may still only show what's currently in the live archive until this is fixed and you reopen the project).");
                    }
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Toggles this whole PROJECT into/out of \"New Game\" mode. When ON,\n" +
                                  "Build ISO drops every original level's own archive record UNLESS\n" +
                                  "you've claimed its slot via \"Add Scene...\" (right-click a group\n" +
                                  "below) -- the real starting level (beach) is never dropped, even\n" +
                                  "unclaimed, since the shipped game boots straight into it.");
        }

        if (ImGui.Button("+ New Scene", new Vector2(-1f, 0f)))
        {
            _newSceneName    = "";
            _createError     = null;
            _createBlank     = false;
            ImGui.OpenPopup("##createScene");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Creates an EMPTY scene under Levels\\Custom\\ -- a clone of the beach\n" +
                              "stripped to just Crash + skydome + lighting + music + sound manager,\n" +
                              "with no terrain/collision/instances. To make it render as a real\n" +
                              "in-game neighbour it still needs claiming onto an existing level slot\n" +
                              "(\"Add Scene...\" on a group below) -- a brand-new archive path is\n" +
                              "outside the shipped game's fixed-size per-level id table.");

        if (ImGui.BeginPopup("##createScene"))
        {
            ImGui.TextUnformatted("New Scene — scene name:");
            ImGui.SetNextItemWidth(220f);
            ImGui.InputText("##newSceneName", ref _newSceneName, 64);

            ImGui.TextColored(new Vector4(0.6f, 0.85f, 1f, 1f),
                "Clones the beach into a TRULY EMPTY canvas: keeps only Crash, the\n" +
                "skydome, world lighting, the DJ music object and the sound manager.\n" +
                "Terrain, collision, particle/visual effects, linked scenery, all other\n" +
                "sounds, and every other instance/enemy, trigger, camera, path and\n" +
                "surface is stripped — you build the scene up yourself from here.");

            if (_createError is not null)
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _createError);

            if (ImGui.Button("Create", new Vector2(100f, 0f)))
            {
                if (TryCreateBlankScene(_newSceneName, _createTemplatePath, _createBlank, out var rm2Rel, out var err))
                {
                    _pendingLoadPath = rm2Rel;
                    _pendingAddCrashFrom = CrashDonorLevelPath;
                    ImGui.CloseCurrentPopup();
                }
                else
                {
                    _createError = err;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(100f, 0f)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        if (_movingSourcePath is not null)
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), $"Moving {_movingSourcePath} —");
            ImGui.TextWrapped("click a GROUP to move it inside (same filename), or an existing LEVEL to REPLACE that exact path.");
            if (ImGui.Button("Cancel Move", new Vector2(-1f, 0f)))
                _movingSourcePath = null;
            ImGui.Separator();
        }
        else
        {
            ImGui.TextDisabled("Click a chunk to load it");
            ImGui.Separator();
        }
        if (_moveError is not null)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _moveError);

        if (_wantOpenRenamePopup) { ImGui.OpenPopup("##renameScene"); _wantOpenRenamePopup = false; }
        if (ImGui.BeginPopup("##renameScene"))
        {
            ImGui.TextUnformatted($"Rename {_renamingPath}");
            ImGui.TextDisabled("New name (letters/digits/underscore, same folder, extension implied):");
            ImGui.SetNextItemWidth(240f);
            ImGui.InputText("##renameNewName", ref _renameNewName, 64);
            if (_renameError is not null)
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _renameError);

            if (ImGui.Button("Rename", new Vector2(100f, 0f)))
            {
                if (RenameSavedScene(_renamingPath!, _renameNewName, out var err))
                    ImGui.CloseCurrentPopup();
                else
                    _renameError = err;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(100f, 0f)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        if (_wantOpenClaimPopup) { ImGui.OpenPopup("##claimScene"); _wantOpenClaimPopup = false; }
        if (ImGui.BeginPopup("##claimScene"))
        {
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.3f, 1f),
                "Pick a level to CLAIM -- its real content is replaced with a blank scene.");

            ImGui.TextDisabled($"1) Template to clone FROM (currently: {_claimTemplatePath}):");
            ImGui.SetNextItemWidth(320f);
            ImGui.InputTextWithHint("##claimTemplateFilter", "Search template levels...", ref _claimTemplateFilter, 128);
            ImGui.BeginChild("##claimTemplateList", new Vector2(320f, 90f), ImGuiChildFlags.Border);
            foreach (var lvl in GetTemplateLevelList())
            {
                if (!string.IsNullOrWhiteSpace(_claimTemplateFilter) &&
                    !lvl.Contains(_claimTemplateFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ImGui.Selectable(lvl, lvl.Equals(_claimTemplatePath, StringComparison.OrdinalIgnoreCase)))
                    _claimTemplatePath = lvl;
            }
            ImGui.EndChild();

            ImGui.Spacing();
            ImGui.TextDisabled("2) Real level slot to CLAIM (gets replaced with the template above):");
            ImGui.SetNextItemWidth(320f);
            ImGui.InputTextWithHint("##claimFilter", "Search levels...", ref _claimFilter, 128);
            if (_claimError is not null)
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _claimError);
            ImGui.BeginChild("##claimList", new Vector2(320f, 160f), ImGuiChildFlags.Border);
            foreach (var lvl in GetTemplateLevelList())
            {
                if (!string.IsNullOrWhiteSpace(_claimFilter) &&
                    !lvl.Contains(_claimFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                bool alreadyClaimed = _project?.ClaimedLevels.Contains(lvl, StringComparer.OrdinalIgnoreCase) ?? false;
                bool isStartingLevel = lvl.Equals(TemplateLevelPath, StringComparison.OrdinalIgnoreCase);
                var label = isStartingLevel ? $"{lvl}  (starting level -- can't claim)"
                          : alreadyClaimed  ? $"{lvl}  (already claimed)"
                          : lvl;
                ImGui.BeginDisabled(isStartingLevel);
                if (ImGui.Selectable(label))
                {
                    if (TryClaimLevelSlot(lvl, _claimTemplatePath, out var err))
                    {
                        Log($"Claimed '{lvl}' for New Game (template: {_claimTemplatePath}). Save Chunk on it not needed -- already written.");
                        ScanLevels();
                        ImGui.CloseCurrentPopup();
                    }
                    else _claimError = err;
                }
                ImGui.EndDisabled();
            }
            ImGui.EndChild();
            if (ImGui.Button("Cancel", new Vector2(100f, 0f)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

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

            bool picking = _movingSourcePath is not null;
            if (picking) ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.4f, 0.55f, 0.6f));
            bool groupOpen = ImGui.TreeNodeEx(group, ImGuiTreeNodeFlags.None);
            if (picking)
            {
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    var groupDir = Path.GetDirectoryName(chunks[0].Rm2) ?? group;
                    var destRel  = Path.Combine(groupDir, Path.GetFileNameWithoutExtension(_movingSourcePath!));
                    if (MoveSavedScene(_movingSourcePath!, destRel, out var err)) _movingSourcePath = null;
                    else _moveError = err;
                }
            }
            else if (_project is not null && ImGui.BeginPopupContextItem($"##ctxgroup_{group}"))
            {
                if (ImGui.MenuItem("Add Scene..."))
                {
                    _wantOpenClaimPopup = true;
                    _claimFilter        = "";
                    _claimError         = null;
                }
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
                        if (clicked)
                        {
                            var destRel = rm2[..^4];
                            if (destRel.Equals(_movingSourcePath![..^4], StringComparison.OrdinalIgnoreCase))
                                _moveError = "Source and destination are the same path.";
                            else if (MoveSavedScene(_movingSourcePath!, destRel, out var err)) _movingSourcePath = null;
                            else _moveError = err;
                        }
                    }
                    else if (clicked)
                        _pendingLoadPath = rm2;

                    if (!picking && ImGui.BeginPopupContextItem($"##ctx_{rm2}"))
                    {
                        if (ImGui.MenuItem("Open"))
                            _pendingLoadPath = rm2;
                        if (ImGui.MenuItem("Move..."))
                        {
                            _movingSourcePath = rm2;
                            _moveError = null;
                        }
                        if (ImGui.MenuItem("Rename..."))
                        {
                            _renamingPath = rm2;
                            _renameNewName = Path.GetFileNameWithoutExtension(rm2);
                            _renameError = null;
                            _wantOpenRenamePopup = true;
                        }
                        if (ImGui.MenuItem("Delete"))
                            _pendingDeletePath = rm2;

                        bool isClaimed = _project?.ClaimedLevels.Contains(rm2[..^4], StringComparer.OrdinalIgnoreCase) ?? false;
                        if (isClaimed && ImGui.MenuItem("Unclaim (restore original)"))
                        {
                            if (TryUnclaimLevelSlot(rm2[..^4], out var unclaimErr))
                            {
                                Log($"Unclaimed '{rm2[..^4]}' -- true original content restored from backup.");
                                ScanLevels();
                            }
                            else
                                Log($"Unclaim failed: {unclaimErr}");
                        }
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
        ImGui.TextWrapped("Controls:\nWASD = move\nShift = run\nCtrl = crouch\nESC = return here");
        ImGui.End();
    }

    public void OpenLevel(string rm2Rel) => LoadLevel(rm2Rel);

    private void LoadLevel(string rm2Rel)
    {
        var sm2Rel = Path.ChangeExtension(rm2Rel, ".sm2");
        Log($"Loading {rm2Rel}...");

        var addCrashFrom = _pendingAddCrashFrom;
        _pendingAddCrashFrom = null;

        var gameScene = new GameScene(_extractedRoot, rm2Rel, sm2Rel, _scriptOut, this, addCrashFrom);
        Engine.Instance.ActiveScene = gameScene;
    }

    private const string TemplateLevelPath = @"Levels\Earth\Hub\beach";

    private const string NewSceneTemplatePath = @"Levels\Earth\Hub\totemex";
    private const string CrashDonorLevelPath  = @"Levels\Earth\Hub\beach";

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

    private void ScanLevels()
    {
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
                    var rel = Path.GetRelativePath(SavedChunksDir, file);
                    if (seen.Add(rel)) AddToGroups(rel);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"SavedChunks scan failed: {ex.Message}");
        }
    }
}
