using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private bool _building;
    private float _buildProgress;
    private readonly object _buildLogLock = new();
    private readonly List<string> _buildLog = new();

    private string _isoOutputPath = "";
    // Amedo 2026-09-20
    private bool _isoPathLoaded;
    private string IsoOutputPathFile =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, ".iso_output_path.txt");

    private void EnsureIsoOutputPathLoaded()
    {
        if (_isoPathLoaded) return;
        _isoPathLoaded = true;
        try { if (File.Exists(IsoOutputPathFile)) _isoOutputPath = File.ReadAllText(IsoOutputPathFile).Trim(); }
        catch { }
    }

    private void SaveIsoOutputPath()
    {
        try { File.WriteAllText(IsoOutputPathFile, _isoOutputPath ?? ""); } catch { }
    }

    private void ChooseIsoOutputPath(Action? then)
    {
        var current = _isoOutputPath.Trim();
        ShowSaveFileDialog("Choose the ISO file to build (overwritten every build)", "PS2 ISO\0*.iso\0All Files\0*.*\0\0",
            current, path =>
            {
                if (path is null) return;
                if (!Path.HasExtension(path)) path += ".iso";
                _isoOutputPath = path;
                SaveIsoOutputPath();
                then?.Invoke();
            });
    }

    private void BuildIso()
    {
        if (_building) return;
        EnsureIsoOutputPathLoaded();
        if (string.IsNullOrWhiteSpace(_isoOutputPath))
        {
            _browser.Log("Build ISO: choose the ISO file name first (kept for this project until you change it).");
            ChooseIsoOutputPath(BuildIso);
            return;
        }
        StopSimulation(); // Amedo 2026-09-26
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null) { _browser.Log("Build ISO: no loaded chunk has a ChunkSource (unexpected)."); return; }

        ChunkExporter.SyncEntityGraph(chunkRoot);
        SyncCollisionBoxes(chunkRoot);
        SyncCollisionCylinders(chunkRoot);
        SyncCollisionPlanes(chunkRoot);
        SyncGeneratedMeshCollision(chunkRoot);
        SyncTriggerMarkers(chunkRoot);
        SyncCameraMarkers(chunkRoot);
        SyncPositionMarkers(chunkRoot);
        SyncAiPositionMarkers(chunkRoot);
        SyncSceneryLightMarkers(chunkRoot); // Amedo 2026-09-19
        SyncInstanceScale(chunkRoot);
        SyncWorldLighting(chunkRoot);
        SaveSoundGainBaselines(chunkRoot);

        _building = true;
        _buildProgress = 0f;
        lock (_buildLogLock) _buildLog.Clear();

        void BLog(string msg)
        {
            lock (_buildLogLock)
            {
                _buildLog.Add(msg);
                if (_buildLog.Count > 300) _buildLog.RemoveAt(0);
            }
        }

        var workDir        = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build");
        var savedChunksDir = SavedChunksDir;
        var outPathOverride = _isoOutputPath.Trim();
        BLog($"Build ISO: starting on disc '{_extractedRoot}' — merges every chunk saved this session, not just the one open now.");

        Func<string, bool>? excludePath = null;
        List<string>? keepLevelPaths = null;
        List<string>? excludedCutscenes = null;
        uint? doubleJumpMask = null; // Amedo 2026-09-25
        int? akuAkuSeconds = null; // Amedo 2026-09-25
        bool akuAkuTouchBreak = false; // Amedo 2026-09-25
        uint? akuAkuMusicId = null; // Amedo 2026-09-25
        int? akuAkuMusicVolume = null; // Amedo 2026-09-25
        bool akuAkuGlowFix = false; // Amedo 2026-09-25
        const string startingLevel = @"Levels\Earth\Hub\beach";
        var buildLevel = CrashLauncher.BuildIsoToPS2.LevelPathOf(chunkRoot.Get<ChunkSource>()!.Rm2Path);
        var projectRoot = Path.GetDirectoryName(_scriptOut) ?? _extractedRoot;
        // Amedo 2026-09-20
        if (CrashProject.Open(projectRoot) is { } proj)
        {
            if (proj.ExcludedCutscenes.Count > 0)
                excludedCutscenes = proj.ExcludedCutscenes;
            doubleJumpMask = proj.DoubleJumpMask; // Amedo 2026-09-25
            akuAkuSeconds = proj.AkuAkuInvincibilitySeconds; // Amedo 2026-09-25
            akuAkuTouchBreak = proj.AkuAkuTouchBreak; // Amedo 2026-09-25
            akuAkuMusicId = proj.AkuAkuGoldenMusicId; // Amedo 2026-09-25
            akuAkuMusicVolume = proj.AkuAkuGoldenMusicVolume; // Amedo 2026-09-25
            akuAkuGlowFix = ReadClassicAkuAku() ?? proj.ClassicAkuAku; // Amedo 2026-09-25

            if (proj.IsNewGame)
            {
                var claimed = new HashSet<string>(proj.ClaimedLevels.Select(p => p.Replace('/', '\\').TrimStart('\\')),
                                                   StringComparer.OrdinalIgnoreCase);
                excludePath = normalizedPath =>
                {
                    if (!normalizedPath.StartsWith(@"Levels\", StringComparison.OrdinalIgnoreCase)) return false;
                    var ext = Path.GetExtension(normalizedPath);
                    if (!ext.Equals(".rm2", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".sm2", StringComparison.OrdinalIgnoreCase)) return false;
                    var basePath = normalizedPath[..^ext.Length];
                    if (basePath.Equals(startingLevel, StringComparison.OrdinalIgnoreCase)) return false;
                    if (basePath.Equals(buildLevel, StringComparison.OrdinalIgnoreCase)) return false;
                    return !claimed.Contains(basePath);
                };
                keepLevelPaths = new List<string>(claimed) { startingLevel, buildLevel };
                BLog($"Build ISO: New Game mode ON — excluding every original level NOT in ClaimedLevels ({proj.ClaimedLevels.Count} claimed).");
            }
        }

        Task.Run(() =>
        {
            try
            {
                var isoPath = CrashLauncher.BuildIsoToPS2.Build(
                    chunkRoot, workDir, _extractedRoot, savedChunksDir,
                    BLog, p => _buildProgress = p, outPathOverride, excludePath, keepLevelPaths, excludedCutscenes, doubleJumpMask, akuAkuSeconds, akuAkuTouchBreak, akuAkuMusicId, akuAkuMusicVolume, akuAkuGlowFix); // Amedo 2026-09-25
                BLog($"Build ISO: done. Open in PCSX2: {isoPath}");
                _browser.Log($"Build ISO: done. Open in PCSX2: {isoPath}");
            }
            catch (Exception ex)
            {
                BLog($"Build ISO failed: {ex.Message}");
                _browser.Log($"Build ISO failed: {ex.Message}");
            }
            finally { _building = false; }
        });
    }

    private void DrawBuildProgress()
    {
        if (!_building) return;

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(560f, 280f), ImGuiCond.Always);
        ImGui.Begin("Building ISO...",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);

        ImGui.ProgressBar(_buildProgress, new Vector2(-1f, 22f), $"{_buildProgress * 100f:F0}%");
        ImGui.Separator();
        ImGui.BeginChild("##buildlog", new Vector2(-1f, -1f), ImGuiChildFlags.None);
        lock (_buildLogLock)
        {
            foreach (var line in _buildLog) ImGui.TextUnformatted(line);
        }
        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) ImGui.SetScrollHereY(1f);
        ImGui.EndChild();

        ImGui.End();
    }
}
