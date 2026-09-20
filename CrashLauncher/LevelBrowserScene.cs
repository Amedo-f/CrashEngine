using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinBoundingBoxBuilder;
using TwinChunkLinkBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinChunkLinkBoundingBoxBuilder;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed class LevelBrowserScene : Scene
{
    private readonly string  _extractedRoot;
    private readonly string  _scriptOut;
    private readonly string? _projectPath;

    private Dictionary<string, List<(string Label, string Rm2)>> _groups = new();

    private readonly object _logLock = new();
    private readonly List<string> _log = new();
    private bool _autoScroll = true;

    private string _newSceneName = "";
    private string? _createError;
    private string _createTemplatePath = NewSceneTemplatePath;
    private string _createTemplateFilter = "";
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

        if (_projectPath is not null &&
            ImGui.Button("All Assets", new Vector2(-1f, 0f)))
        {
            Engine.Instance.ActiveScene = new AssetBrowserScene(_projectPath, this);
            ImGui.End();
            return;
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

    private bool TryCreateBlankScene(string rawName, string templatePath, bool blank, out string? rm2Rel, out string? error)
    {
        var name = SanitizeSceneName(rawName);
        if (name.Length == 0) { rm2Rel = null; error = "Enter a name (letters/digits/underscore)."; return false; }
        var sceneDir = Path.Combine(SavedChunksDir, "Levels", "Custom", name);
        if (Directory.Exists(sceneDir)) { rm2Rel = null; error = $"A scene named \"{name}\" already exists."; return false; }
        return WriteBlankScene(templatePath, $@"Levels\Custom\{name}\{name}", blank, out rm2Rel, out error);
    }

    private bool TryClaimLevelSlot(string targetBasePath, string templatePath, out string? error)
    {
        if (_projectPath is null) { error = "No project loaded -- can't record a New Game claim without one."; return false; }
        if (!WriteBlankScene(templatePath, targetBasePath, blank: false, out _, out error)) return false;

        var project = CrashProject.Open(_projectPath);
        if (project is null) { error = "Scene written, but couldn't load this project's .tson to record the claim."; return false; }
        if (!project.ClaimedLevels.Contains(targetBasePath, StringComparer.OrdinalIgnoreCase))
            project.ClaimedLevels.Add(targetBasePath);
        project.Save();
        error = null;
        return true;
    }

    private bool TryUnclaimLevelSlot(string basePath, out string? error)
    {
        if (_project is null) { error = "No project loaded."; return false; }
        if (!File.Exists(BackupBdPath))
        { error = "No backup archive found -- New Game's own Build ISO never actually ran yet, so there's nothing baked-in to restore from (try plain \"Delete\" instead)."; return false; }

        var savedChunksDir = SavedChunksDir;
        var rm2Path = Path.Combine(savedChunksDir, basePath + ".rm2");
        var sm2Path = Path.Combine(savedChunksDir, basePath + ".sm2");

        try
        {
            using var pkg = PackageReader.Open(BackupBdPath);
            Directory.CreateDirectory(Path.GetDirectoryName(rm2Path)!);

            using (var rm2Stream = pkg.OpenByPath(basePath + ".rm2"))
            {
                if (rm2Stream is null) { error = $"'{basePath}.rm2' not found even in the backup archive (unexpected)."; return false; }
                using var fs = File.Create(rm2Path);
                rm2Stream.CopyTo(fs);
            }
            using (var sm2Stream = pkg.OpenByPath(basePath + ".sm2"))
            {
                if (sm2Stream is not null)
                {
                    using var fs = File.Create(sm2Path);
                    sm2Stream.CopyTo(fs);
                }
            }
        }
        catch (Exception ex)
        {
            error = $"Could not restore original content from backup: {ex.Message}";
            return false;
        }

        _project.ClaimedLevels.RemoveAll(p => p.Equals(basePath, StringComparison.OrdinalIgnoreCase));
        _project.Save();
        error = null;
        return true;
    }

    private bool WriteBlankScene(string templatePath, string destBasePath, bool blank, out string? rm2Rel, out string? error)
    {
        rm2Rel = null;
        var savedChunksDir = SavedChunksDir;

        try
        {
            PS2AnyTwinsanityRM2 rm2;
            PS2AnyTwinsanitySM2 sm2;
            var sceneName = Path.GetFileName(destBasePath);

            _ = blank;
            {
                using var pkg = PackageReader.Open(PristineArchiveSource);
                pkg.ShadowDir = savedChunksDir;

                PS2AnyTwinsanityRM2 sourceRm2;
                using (var stream = pkg.OpenByPath($"{templatePath}.rm2"))
                {
                    if (stream is null) { error = $"Template level not found: {templatePath}.rm2"; return false; }
                    sourceRm2 = new PS2AnyTwinsanityRM2();
                    using var reader = new BinaryReader(stream);
                    sourceRm2.Read(reader, (int)stream.Length);
                }
                PS2AnyTwinsanitySM2 sourceSm2;
                using (var stream = pkg.OpenByPath($"{templatePath}.sm2"))
                {
                    if (stream is null) { error = $"Template level not found: {templatePath}.sm2"; return false; }
                    sourceSm2 = new PS2AnyTwinsanitySM2();
                    using var reader = new BinaryReader(stream);
                    sourceSm2.Read(reader, (int)stream.Length);
                }
                (rm2, sm2) = BlankSceneTemplate.CreateCleanTemplate(sourceRm2, sourceSm2, sceneName);
            }


            var destDir = Path.Combine(savedChunksDir, Path.GetDirectoryName(destBasePath) ?? "");
            Directory.CreateDirectory(destDir);

            var rm2Path = Path.Combine(savedChunksDir, destBasePath + ".rm2");
            var sm2Path = Path.Combine(savedChunksDir, destBasePath + ".sm2");
            WriteChunkAtomic(rm2Path, rm2.Write);
            WriteChunkAtomic(sm2Path, sm2.Write);

            rm2Rel = destBasePath + ".rm2";
            Log(blank
                ? $"Wrote a genuinely empty scene (no template) -> {rm2Rel}"
                : $"Wrote blank scene (template: {templatePath}) -> {rm2Rel}");
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to write scene: {ex.Message}";
            return false;
        }
    }

    private static void WriteChunkAtomic(string path, Action<BinaryWriter> writeAction)
    {
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        using (var writer = new BinaryWriter(fs))
            writeAction(writer);
        File.Move(tmp, path, overwrite: true);
    }

    private void DeleteSavedScene(string rm2Rel)
    {
        var savedChunksDir = SavedChunksDir;
        var rm2Path = Path.Combine(savedChunksDir, rm2Rel);
        var sm2Path = Path.ChangeExtension(rm2Path, ".sm2");

        bool deletedAnything = false;
        try
        {
            if (File.Exists(rm2Path)) { File.Delete(rm2Path); deletedAnything = true; }
            if (File.Exists(sm2Path)) { File.Delete(sm2Path); deletedAnything = true; }

            var markersRel = rm2Rel.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? rm2Rel[..^4] : rm2Rel;
            var markersPath = Path.Combine(CollisionMarkersDir, markersRel + ".json");
            if (File.Exists(markersPath)) { File.Delete(markersPath); deletedAnything = true; }

            var markersDir = Path.GetDirectoryName(markersPath);
            if (markersDir is not null && Directory.Exists(markersDir) && Directory.GetFiles(markersDir).Length == 0
                && Directory.GetDirectories(markersDir).Length == 0)
                Directory.Delete(markersDir);

            var dir = Path.GetDirectoryName(rm2Path);
            if (dir is not null && Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0
                && Directory.GetDirectories(dir).Length == 0)
                Directory.Delete(dir);
        }
        catch (Exception ex)
        {
            Log($"Delete failed for {rm2Rel}: {ex.Message}");
            return;
        }

        Log(deletedAnything
            ? $"Deleted saved copy of {rm2Rel}."
            : $"{rm2Rel} has no saved copy — nothing to delete.");
        ScanLevels();
    }

    private bool MoveSavedScene(string sourceRm2Rel, string newRelNoExt, out string? error)
    {
        if (newRelNoExt.Length == 0) { error = "Enter a destination path."; return false; }

        var savedChunksDir = SavedChunksDir;
        var srcRm2 = Path.Combine(savedChunksDir, sourceRm2Rel);
        var srcSm2 = Path.ChangeExtension(srcRm2, ".sm2");

        var destRm2 = Path.Combine(savedChunksDir, newRelNoExt + ".rm2");
        var destSm2 = Path.Combine(savedChunksDir, newRelNoExt + ".sm2");
        if (Path.GetFullPath(destRm2).Equals(Path.GetFullPath(srcRm2), StringComparison.OrdinalIgnoreCase))
        { error = "Source and destination are the same path."; return false; }

        try
        {
            bool hadDirectCopy = File.Exists(srcRm2);

            byte[] rm2Bytes;
            if (hadDirectCopy)
            {
                rm2Bytes = File.ReadAllBytes(srcRm2);
            }
            else
            {
                using var pkg = PackageReader.Open(PristineArchiveSource);
                pkg.ShadowDir = savedChunksDir;
                using var rm2Stream = pkg.OpenByPath(sourceRm2Rel);
                if (rm2Stream is null) { error = $"{sourceRm2Rel} not found (neither saved nor in the archive)."; return false; }
                using var ms = new MemoryStream();
                rm2Stream.CopyTo(ms);
                rm2Bytes = ms.ToArray();
            }

            Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2? sm2 = null;
            if (hadDirectCopy)
            {
                if (File.Exists(srcSm2))
                {
                    sm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
                    using var fs = File.OpenRead(srcSm2);
                    using var reader = new BinaryReader(fs);
                    sm2.Read(reader, (int)fs.Length);
                }
            }
            else
            {
                using var pkg = PackageReader.Open(PristineArchiveSource);
                pkg.ShadowDir = savedChunksDir;
                var sourceSm2Rel = Path.ChangeExtension(sourceRm2Rel, ".sm2");
                using var sm2Stream = pkg.OpenByPath(sourceSm2Rel);
                if (sm2Stream is not null)
                {
                    sm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
                    using var reader = new BinaryReader(sm2Stream);
                    sm2.Read(reader, (int)sm2Stream.Length);
                }
            }

            if (sm2 is not null)
            {
                var scenery = sm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>(
                    (uint)Twinsanity.TwinsanityInterchange.Enumerations.Constants.SCENERY_SECENERY_ITEM);
                if (scenery is not null) scenery.Name = newRelNoExt.Replace('\\', '/').ToLowerInvariant().Replace('/', '\\');
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destRm2)!);
            WriteChunkAtomic(destRm2, w => w.Write(rm2Bytes));
            if (sm2 is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destSm2)!);
                WriteChunkAtomic(destSm2, sm2.Write);
            }

            if (hadDirectCopy)
            {
                File.Delete(srcRm2);
                if (File.Exists(srcSm2)) File.Delete(srcSm2);

                var srcMarkers = Path.Combine(CollisionMarkersDir,
                    (sourceRm2Rel.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? sourceRm2Rel[..^4] : sourceRm2Rel) + ".json");
                if (File.Exists(srcMarkers))
                {
                    var destMarkers = Path.Combine(CollisionMarkersDir, newRelNoExt + ".json");
                    Directory.CreateDirectory(Path.GetDirectoryName(destMarkers)!);
                    File.Copy(srcMarkers, destMarkers, overwrite: true);
                    File.Delete(srcMarkers);
                    var oldMarkersDir = Path.GetDirectoryName(srcMarkers);
                    if (oldMarkersDir is not null && Directory.Exists(oldMarkersDir) && Directory.GetFiles(oldMarkersDir).Length == 0
                        && Directory.GetDirectories(oldMarkersDir).Length == 0)
                        Directory.Delete(oldMarkersDir);
                }

                var srcDir = Path.GetDirectoryName(srcRm2);
                if (srcDir is not null && Directory.Exists(srcDir) && Directory.GetFiles(srcDir).Length == 0
                    && Directory.GetDirectories(srcDir).Length == 0)
                    Directory.Delete(srcDir);
            }
        }
        catch (Exception ex)
        {
            error = $"Move failed: {ex.Message}";
            return false;
        }

        Log($"Moved {sourceRm2Rel} -> {newRelNoExt}.rm2/.sm2");
        error = null;
        ScanLevels();
        return true;
    }

    private bool RenameSavedScene(string sourceRm2Rel, string rawNewName, out string? error)
    {
        var newName = SanitizeSceneName(rawNewName);
        if (newName.Length == 0) { error = "Enter a name (letters/digits/underscore)."; return false; }

        var dir = Path.GetDirectoryName(sourceRm2Rel) ?? "";
        var destRel = Path.Combine(dir, newName);
        return MoveSavedScene(sourceRm2Rel, destRel, out error);
    }

    private static string SanitizeSceneName(string raw)
    {
        var trimmed = raw.Trim();
        var chars = trimmed.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
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

    private string SavedChunksDir =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "SavedChunks");

    private string CollisionMarkersDir =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "CollisionMarkers");

    private string BackupBdPath =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build",
                     "original_backup_" + Path.GetFileName(_extractedRoot.TrimEnd('\\', '/')), "Crash.BD");
    private string BackupBhPath => Path.ChangeExtension(BackupBdPath, ".BH");

    private string PristineArchiveSource => File.Exists(BackupBdPath) ? BackupBdPath : _extractedRoot;

    private bool TryRestoreOriginalArchive(out string? error)
    {
        if (!File.Exists(BackupBdPath) || !File.Exists(BackupBhPath))
        { error = null; return true; }
        var liveBdPath = Path.Combine(_extractedRoot, "Crash6", "Crash.BD");
        var liveBhPath = Path.Combine(_extractedRoot, "Crash6", "Crash.BH");
        try
        {
            File.Copy(BackupBdPath, liveBdPath, overwrite: true);
            File.Copy(BackupBhPath, liveBhPath, overwrite: true);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Could not restore the original archive (is it open in PCSX2 or being built right now?): {ex.Message}";
            return false;
        }
    }

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
