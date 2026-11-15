using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;

namespace CrashLauncher;

public sealed partial class LevelBrowserScene
{
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

        var project = ReadProject(); // Amedo 2026-11-12
        if (project is null) { error = "Scene written, but couldn't load this project's .tson to record the claim."; return false; }
        if (!project.ClaimedLevels.Contains(targetBasePath, StringComparer.OrdinalIgnoreCase))
            project.ClaimedLevels.Add(targetBasePath);
        SaveProject(project);
        error = null;
        return true;
    }

    private bool TryUnclaimLevelSlot(string basePath, out string? error)
    {
        if (ReadProject() is not { } project) { error = "No project loaded."; return false; } // Amedo 2026-11-12
        if (!File.Exists(BackupBdPath))
        { error = "No backup archive found -- New Game's own Build ISO never actually ran yet, so there's nothing baked-in to restore from (try plain \"Delete\" instead)."; return false; }

        var savedChunksDir = SavedChunksDir;
        var rm2Path = GamePaths.OnDisk(savedChunksDir, basePath + ".rm2");
        var sm2Path = GamePaths.OnDisk(savedChunksDir, basePath + ".sm2");

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

        project.ClaimedLevels.RemoveAll(p => p.Equals(basePath, StringComparison.OrdinalIgnoreCase));
        SaveProject(project);
        error = null;
        return true;
    }

    // Amedo 2026-11-12
    public void SetNewGame(bool on)
    {
        if (ReadProject() is not { } project) return;
        project.IsNewGame = on;
        SaveProject(project);
        if (on)
        {
            ScanLevels(); // Amedo 2026-09-22
            Log("New Game mode: ON — Build ISO will now exclude every original level not claimed via \"Add Scene...\".");
            if (project.ExcludedCutscenes.Count == 0)
            {
                var fmvRoot = Path.Combine(_extractedRoot, "FMV");
                if (Directory.Exists(fmvRoot))
                {
                    foreach (var f in Directory.EnumerateFiles(fmvRoot, "*.pss", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(fmvRoot, f);
                        if (BuildIsoToPS2.IsStoryCutscene(Path.GetFileName(f)))
                            project.ExcludedCutscenes.Add(rel);
                    }
                    SaveProject(project);
                    Log($"New Game mode: pre-selected {project.ExcludedCutscenes.Count} story cutscene(s) to exclude — adjust per-file under UI Browser → FMV / Cutscenes.");
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


            var destDir = GamePaths.OnDisk(savedChunksDir, GamePaths.Folder(destBasePath));
            Directory.CreateDirectory(destDir);

            var rm2Path = GamePaths.OnDisk(savedChunksDir, destBasePath + ".rm2");
            var sm2Path = GamePaths.OnDisk(savedChunksDir, destBasePath + ".sm2");
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

    public void DeleteSavedScene(string rm2Rel)
    {
        var savedChunksDir = SavedChunksDir;
        var rm2Path = GamePaths.OnDisk(savedChunksDir, rm2Rel);
        var sm2Path = Path.ChangeExtension(rm2Path, ".sm2");

        bool deletedAnything = false;
        try
        {
            if (File.Exists(rm2Path)) { File.Delete(rm2Path); deletedAnything = true; }
            if (File.Exists(sm2Path)) { File.Delete(sm2Path); deletedAnything = true; }

            var markersRel = rm2Rel.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? rm2Rel[..^4] : rm2Rel;
            var markersPath = GamePaths.OnDisk(CollisionMarkersDir, markersRel + ".json");
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
        var srcRm2 = GamePaths.OnDisk(savedChunksDir, sourceRm2Rel);
        var srcSm2 = Path.ChangeExtension(srcRm2, ".sm2");

        var destRm2 = GamePaths.OnDisk(savedChunksDir, newRelNoExt + ".rm2");
        var destSm2 = GamePaths.OnDisk(savedChunksDir, newRelNoExt + ".sm2");
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

                var srcMarkers = GamePaths.OnDisk(CollisionMarkersDir,
                    (sourceRm2Rel.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? sourceRm2Rel[..^4] : sourceRm2Rel) + ".json");
                if (File.Exists(srcMarkers))
                {
                    var destMarkers = GamePaths.OnDisk(CollisionMarkersDir, newRelNoExt + ".json");
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

        var dir = GamePaths.Folder(sourceRm2Rel);
        var destRel = GamePaths.Combine(dir, newName);
        return MoveSavedScene(sourceRm2Rel, destRel, out error);
    }

    private static string SanitizeSceneName(string raw)
    {
        var trimmed = raw.Trim();
        var chars = trimmed.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }

    // Amedo 2026-09-22
    private string SavedChunksDir =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot,
            _project is { IsNewGame: true } ? "SavedChunks_NewGame" : "SavedChunks");

    private string CollisionMarkersDir =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot,
            _project is { IsNewGame: true } ? "CollisionMarkers_NewGame" : "CollisionMarkers");

    private string BackupBdPath => DiscBackup.BdPath(_scriptOut, _extractedRoot);
    private string BackupBhPath => Path.ChangeExtension(BackupBdPath, ".BH");

    private string PristineArchiveSource => DiscBackup.PristineSource(_scriptOut, _extractedRoot);

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
}
