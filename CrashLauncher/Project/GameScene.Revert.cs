using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-19
public sealed partial class GameScene
{
    private sealed record RevertItem(string Label, string Path, bool IsFolder);

    private static readonly Vector4 RevertButtonColor = new(0.6f, 0.15f, 0.15f, 1f);

    private bool _openRevertLevelPopup, _openResetAllConfirmPopup;
    private List<RevertItem> _revertItems = new();
    private bool _resetAllHd = true, _resetAllGameSettings = true;

    private bool LevelOpen => !string.IsNullOrWhiteSpace(GamePaths.FileNameWithoutExtension(_rm2 ?? "")) && Roots.Any(r => r.Has<ChunkSource>());

    private List<RevertItem> LevelRevertItems()
    {
        var items = new List<RevertItem>();
        if (!LevelOpen) return items;
        AddRevertFile(items, "Saved scene", GamePaths.OnDisk(SavedChunksDir, _rm2));
        AddRevertFile(items, "Saved scene", GamePaths.OnDisk(SavedChunksDir, _sm2));
        AddRevertFolder(items, "Behaviour code (C#)", BehaviourCodeDir(global: false));
        AddRevertFile(items, "Modern look links", ModernLinksPath);
        AddRevertFile(items, "Lighting and sky", GetWorldLightingMetaPath());
        AddRevertFile(items, "Collision shapes", GetCollisionMarkersPath());
        if (Directory.Exists(SoundGainBaselineDir))
        {
            string scope = _rm2.Replace('\\', '_').Replace('/', '_').Replace(':', '_') + "__";
            foreach (var f in Directory.GetFiles(SoundGainBaselineDir, scope + "*")) AddRevertFile(items, "Sound volume baseline", f);
        }
        return items;
    }

    private List<RevertItem> AllRevertItems(bool hd)
    {
        var items = new List<RevertItem>();
        AddRevertFolder(items, "Saved scenes (every level + shared)", SavedChunksDir);
        AddRevertFolder(items, "Behaviour code (C#, every level + Default)", Path.Combine(BehaviourCodeRoot, "Behaviours"));
        if (Directory.Exists(ModernDir))
            foreach (var f in Directory.GetFiles(ModernDir, "*.json")) AddRevertFile(items, "Modern look links", f);
        AddRevertFolder(items, "Lighting and sky", ModeDir("WorldLighting"));
        AddRevertFolder(items, "Collision shapes", ModeDir("CollisionMarkers"));
        if (hd) AddRevertFolder(items, "HD textures", ModeDir("HD"));
        return items;
    }

    private static void AddRevertFile(List<RevertItem> items, string label, string path)
    {
        if (File.Exists(path)) items.Add(new(label, path, false));
    }

    private static void AddRevertFolder(List<RevertItem> items, string label, string path)
    {
        if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any()) items.Add(new(label, path, true));
    }

    private (string Folder, int Moved, int Failed) MoveToDiscarded(IReadOnlyList<RevertItem> items, string what)
    {
        string root = ProjectRoot();
        string folder = Path.Combine(root, "Discarded", $"{what}_{DateTime.Now:yyyyMMdd_HHmmss}");
        _behaviourWatcher?.Dispose();
        _behaviourWatcher = null;
        int moved = 0, failed = 0;
        foreach (var item in items)
        {
            string rel = Path.GetRelativePath(root, item.Path);
            string target = rel.StartsWith("..") || Path.IsPathRooted(rel) ? Path.Combine(folder, Path.GetFileName(item.Path)) : Path.Combine(folder, rel);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (item.IsFolder) Directory.Move(item.Path, target);
                else File.Move(item.Path, target);
                moved++;
            }
            catch (Exception ex)
            {
                failed++;
                _browser.Log($"Revert: could not move {rel} ({ex.Message}). Close any program using it and try again.");
            }
        }
        _behaviourCodeTimes = null;
        _behaviourCodeRm2 = _behaviourCodeGlobalRm2 = null;
        _scriptTextCache.Clear();
        return (folder, moved, failed);
    }

    private void RevertLevelFromDisc()
    {
        if (!LevelOpen) { _browser.Log("Reload Level from Disc: no level is open."); return; }
        var (folder, moved, failed) = MoveToDiscarded(_revertItems, GamePaths.FileNameWithoutExtension(_rm2));
        _browser.Log(moved == 0 && failed == 0
            ? "Reload Level from Disc: this level had no saved edits."
            : $"Reload Level from Disc: {moved} item(s) moved to {Path.GetRelativePath(ProjectRoot(), folder)} (move them back to undo)" + (failed > 0 ? $", {failed} could not be moved." : "."));
        ReloadLevel(fromPristineDisc: true);
    }

    private void ResetAllLevelsFromDisc()
    {
        var (folder, moved, failed) = MoveToDiscarded(AllRevertItems(_resetAllHd), "AllLevels");
        _browser.Log($"Reset All Levels From Disc: {moved} item(s) moved to {Path.GetRelativePath(ProjectRoot(), folder)} (move them back to undo)" + (failed > 0 ? $", {failed} could not be moved." : "."));

        // Amedo 2026-09-20
        try
        {
            var backupDir = DiscBackup.DirFor(_scriptOut, _extractedRoot);
            var backupBd  = Path.Combine(backupDir, "Crash.BD");
            var backupBh  = Path.Combine(backupDir, "Crash.BH");
            var liveBd    = Path.Combine(_extractedRoot, "Crash6", "Crash.BD");
            var liveBh    = Path.Combine(_extractedRoot, "Crash6", "Crash.BH");
            if (File.Exists(backupBd) && File.Exists(backupBh) && File.Exists(liveBd))
            {
                File.Copy(backupBd, liveBd, overwrite: true);
                File.Copy(backupBh, liveBh, overwrite: true);
                _browser.Log("Reset All Levels From Disc: restored the disc archive (Crash.BD/BH) from the pristine " +
                              "original backup -- every level now reads truly original data (including changes Build ISO had baked in).");
            }
        }
        catch (Exception ex)
        {
            _browser.Log("Reset All Levels From Disc: WARNING -- could not restore disc archive from backup: " + ex.Message);
        }

        if (_resetAllGameSettings && GetGameProject() is { } proj) ResetGameSettings(proj);

        if (Roots.Any(r => r.Has<ChunkSource>())) ReloadLevel(fromPristineDisc: true);
        else _browser.Log("Reset All Levels From Disc: done. Open any level to see the original data.");
    }

    private void ResetGameSettings(CrashProject proj)
    {
        proj.DoubleJumpMask = null;
        proj.AkuAkuInvincibilitySeconds = null;
        proj.AkuAkuTouchBreak = false;
        proj.AkuAkuGoldenMusicId = null;
        proj.AkuAkuGoldenMusicVolume = null;
        proj.ClassicAkuAku = false;
        proj.ExcludedCutscenes.Clear();
        proj.CratesReceiveShadows = false;
        GameShadowDefs.CratesReceiveShadows = false;
        proj.Save();
        _browser.Log("Reset All Levels From Disc: Game Settings are back to the original game.");
    }

    private void DrawRevertLevelPopup()
    {
        if (_openRevertLevelPopup)
        {
            _revertItems = LevelRevertItems();
            ImGui.OpenPopup("Reload Level from Disc?");
            _openRevertLevelPopup = false;
        }
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        if (!ImGui.BeginPopupModal("Reload Level from Disc?", ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextUnformatted($"{GamePaths.FileNameWithoutExtension(_rm2)} goes back to the original disc: objects, positions, characters,");
        ImGui.TextUnformatted("scripts, collision, lighting and modern looks.");
        ImGui.Spacing();
        if (_revertItems.Count > 0)
        {
            ImGui.TextUnformatted("Its edits move to the project's Discarded folder (move them back to undo):");
            foreach (var item in _revertItems) ImGui.BulletText($"{item.Label}: {Path.GetRelativePath(ProjectRoot(), item.Path)}");
        }
        else ImGui.TextDisabled("This level has no saved edits: it just reloads.");
        ImGui.TextDisabled("Edits shared by every level stay (Startup\\Default.rm2, Default scripts, HD textures, Game Settings).");
        ImGui.TextDisabled("Reset ALL Levels From Disc takes those back too.");
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, RevertButtonColor);
        if (ImGui.Button("Reload from Disc", new Vector2(200f, 0f)))
        {
            RevertLevelFromDisc();
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120f, 0f))) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void DrawResetAllConfirmPopup()
    {
        if (_openResetAllConfirmPopup)
        {
            ImGui.OpenPopup("Reset All Levels From Disc?");
            _openResetAllConfirmPopup = false;
        }
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        if (!ImGui.BeginPopupModal("Reset All Levels From Disc?", ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextUnformatted("EVERY level of this project goes back to the original disc, exactly as the game ships.");
        ImGui.Spacing();
        var items = AllRevertItems(_resetAllHd);
        if (items.Count > 0)
        {
            ImGui.TextUnformatted("These move to the project's Discarded folder (move them back to undo):");
            foreach (var item in items) ImGui.BulletText($"{item.Label}: {Path.GetRelativePath(ProjectRoot(), item.Path)}");
        }
        else ImGui.TextDisabled("No saved edits in this project.");
        ImGui.Checkbox("HD textures too##resetallhd", ref _resetAllHd);
        ImGui.Checkbox("Game Settings too (Aku Aku, double jump, skipped cutscenes, crate shadows)##resetallgs", ref _resetAllGameSettings);
        ImGui.TextDisabled("Kept: the project itself, its disc, My Assets, the models in Modern, and graphics settings.");
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, RevertButtonColor);
        if (ImGui.Button("Reset All Levels", new Vector2(200f, 0f)))
        {
            ResetAllLevelsFromDisc();
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120f, 0f))) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }
}
