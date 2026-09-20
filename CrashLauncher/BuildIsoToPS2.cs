using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public static class BuildIsoToPS2
{
    public static string Build(Entity chunkRoot, string workDir, string discContentPath,
                               string savedChunksDir, Action<string> log, Action<float>? onProgress = null,
                               string? outputPathOverride = null, Func<string, bool>? excludePath = null,
                               IReadOnlyCollection<string>? keepLevelPaths = null,
                               IReadOnlyCollection<string>? excludedCutscenes = null)
    {
        var fmvPath     = Path.Combine(discContentPath, "FMV");
        var fmvTempPath = Path.Combine(workDir, "FMV_excluded_temp");
        if (Directory.Exists(fmvTempPath))
        {
            foreach (var f in Directory.GetFiles(fmvTempPath, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(fmvTempPath, f);
                var dest = Path.Combine(fmvPath, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Move(f, dest, overwrite: true);
            }
            Directory.Delete(fmvTempPath, recursive: true);
            log("Restored FMV\\ file(s) left over from a previous build that didn't finish cleanly.");
        }
        var source = chunkRoot.Get<ChunkSource>()
            ?? throw new InvalidOperationException("Chunk root has no ChunkSource — was it built by ChunkImporter.LoadChunk?");

        string isoPath;
        if (!string.IsNullOrEmpty(outputPathOverride))
        {
            isoPath = outputPathOverride;
            Directory.CreateDirectory(Path.GetDirectoryName(isoPath)!);
        }
        else
        {
            var isoDir = Path.Combine(workDir, "image");
            Directory.CreateDirectory(isoDir);
            isoPath = Path.Combine(isoDir, $"{Path.GetFileNameWithoutExtension(source.Rm2Path)}_test.iso");
        }
        if (File.Exists(isoPath))
        {
            try
            {
                using var probe = File.Open(isoPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                throw new InvalidOperationException(
                    $"The ISO file is currently open somewhere else (probably still loaded in an emulator) — " +
                    $"close it there and try building again: {isoPath}");
            }
        }

        var stagingDir = Path.Combine(workDir, "archives");
        if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
        Directory.CreateDirectory(stagingDir);

        if (Directory.Exists(savedChunksDir))
        {
            foreach (var file in Directory.EnumerateFiles(savedChunksDir, "*.*", SearchOption.AllDirectories))
            {
                var rel  = Path.GetRelativePath(savedChunksDir, file);
                var dest = Path.Combine(stagingDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }
            log($"Staged saved chunks from {savedChunksDir}");
        }

        var rm2Full = Path.Combine(stagingDir, source.Rm2Path);
        Directory.CreateDirectory(Path.GetDirectoryName(rm2Full)!);
        using (var fs = File.Create(rm2Full)) { using var bw = new BinaryWriter(fs); source.Rm2.Write(bw); }
        log($"Staged {source.Rm2Path}");

        if (source.Sm2 is not null && source.Sm2Path is not null)
        {
            var sm2Full = Path.Combine(stagingDir, source.Sm2Path);
            Directory.CreateDirectory(Path.GetDirectoryName(sm2Full)!);
            using (var fs = File.Create(sm2Full)) { using var bw = new BinaryWriter(fs); source.Sm2.Write(bw); }
            log($"Staged {source.Sm2Path}");
        }

        if (source.GlobalRm2 is not null)
        {
            var globalFull = Path.Combine(stagingDir, @"Startup\Default.rm2");
            Directory.CreateDirectory(Path.GetDirectoryName(globalFull)!);
            using (var fs = File.Create(globalFull)) { using var bw = new BinaryWriter(fs); source.GlobalRm2.Write(bw); }
            log(@"Staged Startup\Default.rm2 (global shared objects, live state)");
        }

        var originalBhPath = Path.Combine(discContentPath, "Crash6", "Crash.BH");
        var originalBdPath = Path.Combine(discContentPath, "Crash6", "Crash.BD");
        var discKey         = Path.GetFileName(discContentPath.TrimEnd('\\', '/'));
        var backupDir       = Path.Combine(workDir, "original_backup_" + discKey);
        var backupBhPath    = Path.Combine(backupDir, "Crash.BH");
        var backupBdPath    = Path.Combine(backupDir, "Crash.BD");
        if (!File.Exists(backupBhPath) && File.Exists(originalBhPath))
        {
            Directory.CreateDirectory(backupDir);
            File.Copy(originalBhPath, backupBhPath);
            File.Copy(originalBdPath, backupBdPath);
            log("Backed up original Crash.BD/BH (first build only, kept for every future build's fallback).");
        }

        if (keepLevelPaths is { Count: > 0 })
            NeuterDanglingLinksForNewGame(stagingDir, backupBdPath, backupBhPath, keepLevelPaths, log);

        log("Packing archive (Crash.BD/BH)...");
        var tempBdPath = originalBdPath + ".tmp";
        var tempBhPath = originalBhPath + ".tmp";
        var bd = new PS2BD("", tempBhPath);
        bd.BuildRecordsWithFallback(stagingDir, backupBhPath, backupBdPath, excludePath);
        using (var bdFile = new FileStream(tempBdPath, FileMode.Create, FileAccess.Write))
        using (var bdWriter = new BinaryWriter(bdFile))
            bd.Write(bdWriter);
        File.Move(tempBdPath, originalBdPath, overwrite: true);
        File.Move(tempBhPath, originalBhPath, overwrite: true);
        log("Archive updated.");

        if (excludedCutscenes is { Count: > 0 } && Directory.Exists(fmvPath))
        {
            var wanted = new HashSet<string>(excludedCutscenes, StringComparer.OrdinalIgnoreCase);
            var moved = 0;
            foreach (var f in Directory.GetFiles(fmvPath, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(fmvPath, f);
                if (!wanted.Contains(rel)) continue;
                var dest = Path.Combine(fmvTempPath, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Move(f, dest, overwrite: true);
                moved++;
            }
            if (moved > 0)
                log($"New Game mode: excluding {moved} cutscene(s) per the project's own per-file selection — moved out for this build only.");
        }
        try
        {
            log("Building ISO image (native packer — this can take a bit)...");
            onProgress?.Invoke(0f);
            var progress = Ps2ImageMakerNative.StartPacking(discContentPath, isoPath);
            while (!progress.Finished)
            {
                Thread.Sleep(500);
                progress = Ps2ImageMakerNative.PollProgress();
                onProgress?.Invoke(progress.ProgressPercentage);
                if (progress.NewState) log($"  {progress.ProgressS}  {progress.ProgressPercentage * 100f:F0}%");
            }
        }
        finally
        {
            if (Directory.Exists(fmvTempPath))
            {
                foreach (var f in Directory.GetFiles(fmvTempPath, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(fmvTempPath, f);
                    var dest = Path.Combine(fmvPath, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Move(f, dest, overwrite: true);
                }
                Directory.Delete(fmvTempPath, recursive: true);
                log("Excluded cutscene(s) restored to FMV\\.");
            }
        }
        onProgress?.Invoke(1f);
        log($"ISO built: {isoPath}");
        return isoPath;
    }

    internal static bool IsStoryCutscene(string fileName)
    {
        if (fileName.Equals("COMPLETE.PSS", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.Length < 4) return false;
        var c0 = char.ToUpperInvariant(fileName[0]);
        return (c0 == 'H' || c0 == 'B') && char.IsDigit(fileName[1]) && char.IsDigit(fileName[2]) && fileName[3] == '_';
    }

    private static void NeuterDanglingLinksForNewGame(string stagingDir, string backupBdPath, string backupBhPath,
                                                        IReadOnlyCollection<string> keepLevelPaths, Action<string> log)
    {
        var keepSet = new HashSet<string>(keepLevelPaths, StringComparer.OrdinalIgnoreCase);
        int levelsChecked = 0, linksNeutered = 0;

        foreach (var levelBasePath in keepLevelPaths)
        {
            var sm2Rel = levelBasePath + ".sm2";
            var stagedPath = Path.Combine(stagingDir, sm2Rel);

            byte[] sm2Bytes;
            if (File.Exists(stagedPath))
            {
                sm2Bytes = File.ReadAllBytes(stagedPath);
            }
            else if (File.Exists(backupBdPath) && File.Exists(backupBhPath))
            {
                using var pkg = PackageReader.Open(backupBdPath);
                using var stream = pkg.OpenByPath(sm2Rel);
                if (stream is null) continue;
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                sm2Bytes = ms.ToArray();
            }
            else continue;

            PS2AnyTwinsanitySM2 sm2;
            using (var ms = new MemoryStream(sm2Bytes))
            using (var reader = new BinaryReader(ms))
            {
                sm2 = new PS2AnyTwinsanitySM2();
                sm2.Read(reader, sm2Bytes.Length);
            }
            levelsChecked++;

            var linkItem = sm2.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
            if (linkItem is null || linkItem.LinksList.Count == 0) continue;

            bool changed = false;
            foreach (var link in linkItem.LinksList)
            {
                var targetPath = link.Path.Replace('/', '\\').TrimStart('\\');
                if (keepSet.Contains(targetPath)) continue;
                if (!link.IsLoadWallActive && !link.IsRendered && !link.KeepLoaded) continue;

                link.IsLoadWallActive = false;
                link.IsRendered       = false;
                link.KeepLoaded       = false;
                changed = true;
                linksNeutered++;
            }

            if (!changed) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
            using var fs = File.Create(stagedPath);
            using var writer = new BinaryWriter(fs);
            sm2.Write(writer);
        }

        log($"New Game: checked {levelsChecked} kept level(s), neutered {linksNeutered} dangling link(s) pointing at excluded levels.");
    }
}
