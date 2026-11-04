using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public static partial class BuildIsoToPS2
{
    public static string Build(Entity chunkRoot, string workDir, string discContentPath,
                               string savedChunksDir, Action<string> log, Action<float>? onProgress = null,
                               string? outputPathOverride = null, Func<string, bool>? excludePath = null,
                               IReadOnlyCollection<string>? keepLevelPaths = null,
                               IReadOnlyCollection<string>? excludedCutscenes = null,
                               uint? doubleJumpMask = null, int? akuAkuSeconds = null, bool akuAkuTouchBreak = false,
                               uint? akuAkuMusicId = null, int? akuAkuMusicVolume = null, bool akuAkuGlowFix = false)
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

        if (string.IsNullOrWhiteSpace(outputPathOverride))
            throw new InvalidOperationException("No ISO output file chosen — set it in File > ISO Output Path (or Build ISO asks for it).");
        string isoPath = outputPathOverride;
        Directory.CreateDirectory(Path.GetDirectoryName(isoPath)!);
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
        ApplyCutsceneSkipPatch(discContentPath, excludedCutscenes, log); // Amedo 2026-09-20
        ApplyDoubleJumpPatch(discContentPath, doubleJumpMask, log); // Amedo 2026-09-25
        ApplyAkuAkuInvincibilityPatch(discContentPath, akuAkuSeconds, akuAkuTouchBreak, akuAkuMusicId, akuAkuMusicVolume, akuAkuGlowFix, log); // Amedo 2026-09-25
        ApplyStartingLevel(discContentPath, LevelPathOf(source.Rm2Path), log);

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

    internal readonly record struct BootRegionDef(string ExeName, int StartingChunkOffset, int StartingChunkSize);
    internal static readonly BootRegionDef[] BootRegions =
    {
        new("SLES_525.68", 0x1F6708, 56),
        new("SLUS_209.09", 0x1F63A8, 56),
    };

    internal static string LevelPathOf(string rm2Path) =>
        rm2Path.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? rm2Path[..^4] : rm2Path;

    internal static string? ReadStartingLevel(string exePath, BootRegionDef region)
    {
        try
        {
            using var fs = File.OpenRead(exePath);
            fs.Seek(region.StartingChunkOffset, SeekOrigin.Begin);
            var buf = new byte[region.StartingChunkSize];
            if (fs.Read(buf, 0, buf.Length) != buf.Length) return null;
            int nul = Array.IndexOf(buf, (byte)0);
            return System.Text.Encoding.ASCII.GetString(buf, 0, nul < 0 ? buf.Length : nul);
        }
        catch { return null; }
    }

    internal static void ApplyStartingLevel(string discContentPath, string levelPath, Action<string> log)
    {
        foreach (var region in BootRegions)
        {
            var exePath = Path.Combine(discContentPath, region.ExeName);
            if (!File.Exists(exePath)) continue;
            if (levelPath.Length > region.StartingChunkSize - 1)
            {
                log($"Starting level: '{levelPath}' is {levelPath.Length} chars, max {region.StartingChunkSize - 1} fits in the executable. Not changed.");
                return;
            }
            if (string.Equals(ReadStartingLevel(exePath, region), levelPath, StringComparison.OrdinalIgnoreCase)) return;
            var backupPath = exePath + ".orig_backup";
            if (!File.Exists(backupPath))
            {
                File.Copy(exePath, backupPath);
                log($"Boot Patch: backed up original {region.ExeName} -> {Path.GetFileName(backupPath)}");
            }
            var buf = new byte[region.StartingChunkSize];
            var bytes = System.Text.Encoding.ASCII.GetBytes(levelPath);
            Array.Copy(bytes, buf, bytes.Length);
            using (var fs = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Seek(region.StartingChunkOffset, SeekOrigin.Begin);
                fs.Write(buf, 0, buf.Length);
            }
            log($"Starting level: the game now starts in '{levelPath}' (the level this ISO was built from).");
            return;
        }
        log("Starting level: no known executable (SLUS_209.09 / SLES_525.68) — not changed.");
    }

    internal static bool IsStoryCutscene(string fileName)
    {
        if (fileName.Equals("COMPLETE.PSS", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.Length < 4) return false;
        var c0 = char.ToUpperInvariant(fileName[0]);
        return (c0 == 'H' || c0 == 'B') && char.IsDigit(fileName[1]) && char.IsDigit(fileName[2]) && fileName[3] == '_';
    }

    // Amedo 2026-09-20
    private const int FmvSkipLoadBias   = 0xFF000;

    // Amedo 2026-09-21
    private readonly record struct FmvRegion(
        string ExeName, int TableOffset, int TableCount, int CallOffset, int StubOffset,
        byte[] OrigCall, byte[] StubCall, uint StubJumpWord);

    private static readonly FmvRegion[] FmvRegions =
    {
        new("SLUS_209.09", 0x1E7D60, 21, 0x78588, 0x82C1C,
            new byte[] { 0xD8, 0xCA, 0x05, 0x0C }, new byte[] { 0x07, 0x07, 0x06, 0x0C }, 0x0805CAD8u),
        new("SLES_525.68", 0x1E80F8, 20, 0x78698, 0x833AC,
            new byte[] { 0xBA, 0xCA, 0x05, 0x0C }, new byte[] { 0xEB, 0x08, 0x06, 0x0C }, 0x0805CABAu),
    };

    internal static void ApplyCutsceneSkipPatch(string discContentPath, IReadOnlyCollection<string>? excludedCutscenes, Action<string> log)
    {
        // Amedo 2026-09-21
        FmvRegion? regionOpt = null;
        string exePath = "";
        foreach (var r in FmvRegions)
        {
            var candidate = Path.Combine(discContentPath, r.ExeName);
            if (File.Exists(candidate)) { regionOpt = r; exePath = candidate; break; }
        }
        if (regionOpt is not { } region)
        {
            log("Cutscene-skip: no known EE executable (SLUS_209.09 / SLES_525.68) found — nothing patched.");
            return;
        }

        var bytes = File.ReadAllBytes(exePath);

        uint mask = 0;
        var skipped = new List<string>();
        if (excludedCutscenes is { Count: > 0 })
        {
            var excludedBase = new HashSet<string>(
                excludedCutscenes.Select(p => Path.GetFileNameWithoutExtension(p)),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < region.TableCount; i++)
            {
                int entryOff = region.TableOffset + i * 8;
                if (entryOff + 4 > bytes.Length) break;
                uint namePtr = BitConverter.ToUInt32(bytes, entryOff);
                int nameOff = (int)(namePtr - (uint)FmvSkipLoadBias);
                if (nameOff < 0 || nameOff >= bytes.Length) continue;
                var name = ReadAsciiZ(bytes, nameOff);
                var baseName = Path.GetFileNameWithoutExtension(name.Replace('\\', '/'));
                if (baseName.Length > 0 && excludedBase.Contains(baseName))
                {
                    mask |= 1u << i;
                    skipped.Add(baseName);
                }
            }
        }

        if (mask == 0)
        {
            if (BytesEqualAt(bytes, region.CallOffset, region.StubCall))
            {
                Array.Copy(region.OrigCall, 0, bytes, region.CallOffset, 4);
                File.WriteAllBytes(exePath, bytes);
                log("Cutscene-skip: no excluded cutscenes — in-game cutscene player restored to normal.");
            }
            return;
        }

        bool caveOurs = BytesEqualAt(bytes, region.CallOffset, region.StubCall);
        bool caveFree = true;
        for (int k = 0; k < 44; k++) if (bytes[region.StubOffset + k] != 0) { caveFree = false; break; }
        if (!caveFree && !caveOurs)
        {
            log($"Cutscene-skip: ABORTED — code cave at 0x{region.StubOffset:X} is not free (unexpected). Excluded cutscenes were NOT patched to skip.");
            return;
        }

        uint[] words =
        {
            0x3C010000u | (mask >> 16),
            0x34210000u | (mask & 0xFFFF),
            0x34030001u, 0x00E31804u, 0x00611824u, 0x14600003u, 0x00000000u,
            region.StubJumpWord, 0x00000000u, 0x03E00008u, 0x34020001u,
        };
        for (int i = 0; i < words.Length; i++)
            BitConverter.GetBytes(words[i]).CopyTo(bytes, region.StubOffset + i * 4);
        Array.Copy(region.StubCall, 0, bytes, region.CallOffset, 4);
        File.WriteAllBytes(exePath, bytes);

        log($"Cutscene-skip: patched {skipped.Count} excluded cutscene(s) to skip in-game (no hang): {string.Join(", ", skipped)}.");
    }

    // Amedo 2026-09-25
    public const uint DoubleJumpDefaultMask = CrashEngine.Runtime.PlaySettings.DefaultJumpRules; // Amedo 2026-10-04
    public const uint DoubleJumpSpinBit = 0x10000;
    public const uint SlideJumpBodySlamBit = 0x20000;
    public const uint SlideJumpSpinBit = 0x40000;
    private const int DoubleJumpOffset = 0x49CEC;
    private static readonly uint[] DoubleJumpOriginal = { 0x12420004, 0x0000282D, 0x24020006, 0x56420003, 0x8E220000, 0x24050001, 0x8E220000 };
    private static uint[] DoubleJumpPatched(uint mask) => new[] { 0x34050000u | (mask & 0xFFFF), 0x02452806u, 0x30A50001u, 0x8E220000u, 0u, 0u, 0u };
    private static readonly (int Offset, uint Original, uint Patched)[] SpinDoubleJumpWords =
    {
        (0x49CE0, 0x00431024, 0x34421000),
        (0x4A998, 0x24040001, 0x2404DFFF),
        (0x4A9A0, 0x54400006, 0x54400009),
    };
    private static readonly (int Offset, uint Original, uint Patched)[] SlideJumpBodySlamWords =
    {
        (0x49F08, 0x12230022, 0x00000000),
    };
    private static readonly (int Offset, uint Original, uint Patched)[] SlideJumpSpinWords =
    {
        (0x4FD88, 0x1440000B, 0x00000000),
    };
    private static readonly (int Offset, uint Original, uint Patched)[] SlideDoubleJumpTurnWords = WordBlock(0x500E0,
        new uint[] { 0x8E220000, 0x24030007, 0x3044000F, 0x108300A2, 0x24020009, 0x54820003, 0x8CA200A0, 0x100000A6, 0x24020007,
                     0x504000A4, 0x24020007, 0xC440000C, 0xE620005C, 0x8C420010, 0xAE220060, 0xAFA20000, 0x1000009D, 0x24020007 },
        new uint[] { 0x8E220000, 0x24030009, 0x3044000F, 0x1083000C, 0x8CA200A0, 0x26290050, 0x0122100A, 0x8C460010, 0x8E270060,
                     0x38880007, 0x0008380B, 0x00E63825, 0x11000099, 0xAE270060, 0xC440000C, 0xE620005C, 0x1000009D, 0x24020007 });

    private static (int Offset, uint Original, uint Patched)[] WordBlock(int offset, uint[] original, uint[] patched) =>
        original.Select((o, i) => (offset + i * 4, o, patched[i])).ToArray();

    // Amedo
    private sealed record PatchSet(string Exe, int DoubleJump, (int Offset, uint Original, uint Patched)[] SpinDoubleJump,
        (int Offset, uint Original, uint Patched)[] SlideBodySlam, (int Offset, uint Original, uint Patched)[] SlideSpin,
        (int Offset, uint Original, uint Patched)[] SlideTurn, int AkuDuration, (int Offset, uint Original, uint Patched)[] AkuTouchBreak,
        int AkuMusicId, int AkuMusicVolume, int AkuMusicVolumeRight, (int Offset, uint Original, uint Patched)[] AkuMusic,
        (int Offset, uint Original, uint Patched)[] AkuGlowFix, (int Offset, uint Original, uint Patched)[] AkuCutsceneResume);

    private static PatchSet[]? _patchSets;

    // Amedo
    private static PatchSet[] PatchSets => _patchSets ??=
    [
        new(CrashEngine.Runtime.PlaySettings.NtscExe, DoubleJumpOffset, SpinDoubleJumpWords, SlideJumpBodySlamWords, SlideJumpSpinWords,
            SlideDoubleJumpTurnWords, AkuAkuDurationOffset, AkuAkuTouchBreakWords, AkuAkuMusicIdOffset, AkuAkuMusicVolumeOffset,
            AkuAkuMusicVolumeRightOffset, AkuAkuMusicWords, AkuAkuGlowFixWords, AkuAkuCutsceneResumeWords),
        new(CrashEngine.Runtime.PlaySettings.PalExe, PalDoubleJumpOffset, PalSpinDoubleJumpWords, PalSlideJumpBodySlamWords, PalSlideJumpSpinWords,
            PalSlideDoubleJumpTurnWords, PalAkuAkuDurationOffset, PalAkuAkuTouchBreakWords, PalAkuAkuMusicIdOffset, PalAkuAkuMusicVolumeOffset,
            PalAkuAkuMusicVolumeRightOffset, PalAkuAkuMusicWords, PalAkuAkuGlowFixWords, PalAkuAkuCutsceneResumeWords),
    ];

    // Amedo
    internal static bool HasPatchableExe(string discContentPath) => FindPatchSet(discContentPath, out _) is not null;

    // Amedo
    private static PatchSet? FindPatchSet(string discContentPath, out string exePath)
    {
        foreach (var set in PatchSets)
        {
            exePath = Path.Combine(discContentPath, set.Exe);
            if (File.Exists(exePath)) return set;
        }
        exePath = "";
        return null;
    }

    // Amedo 2026-09-25
    public const int AkuAkuDefaultSeconds = CrashEngine.Runtime.PlaySettings.DefaultGoldenSeconds; // Amedo 2026-10-04
    public const int AkuAkuInfinite = CrashEngine.Runtime.PlaySettings.InfiniteGoldenSeconds;
    public const int AkuAkuMaxSeconds = CrashEngine.Runtime.PlaySettings.MaxGoldenSeconds;
    private const int AkuAkuDurationOffset = 0x40A9C;

    private static uint AkuAkuDurationWord(int seconds) =>
        0x3C010000u | (seconds == AkuAkuInfinite ? 0x4974u : BitConverter.SingleToUInt32Bits(seconds) >> 16);

    private static readonly (int Offset, uint Original, uint Patched)[] AkuAkuTouchBreakWords =
        new[] { (0x3F0F8, 0x8EA30014u, 0x0C045BCEu) }.Concat(WordBlock(0x17F38,
            new uint[] { 0x27BDFFD0, 0xFFB10018, 0xFFB00010, 0xFFBF0020, 0x9083000C, 0x2C620006, 0x1040002C, 0x00A0882D,
                         0x3C02002F, 0x00031880, 0x2442DB50, 0x00621821, 0x8C640000, 0x00800008, 0x00000000, 0x3C05002F,
                         0x0220202D, 0x0C080FE2, 0x24A5DAD8, 0x10000025, 0x8F828090, 0x3C05002F, 0x0220202D, 0x0C080FE2,
                         0x24A5DAE8, 0x1000001F, 0x8F828090, 0x3C05002F, 0x0220202D },
            new uint[] { 0x8EA30014, 0x3C080031, 0x8D08A5A8, 0x11000017, 0x00000000, 0x8D080000, 0x15130014, 0x3C090031,
                         0x8D299580, 0x11200011, 0x00000000, 0x8D2A0010, 0x314A0400, 0x1140000D, 0x8D2A0014, 0x000A5182,
                         0x314A00FF, 0x2D4A0003, 0x15400008, 0x962A0006, 0x240B0001, 0x114B0005, 0x240B0166, 0x114B0003,
                         0x2C6A0004, 0x55400001, 0x24030006, 0x03E00008, 0x0060A02D })).ToArray();

    // Amedo 2026-09-25
    private const int AkuAkuMusicIdOffset = 0x16A908;
    private const uint AkuAkuMusicIdOriginal = 0xDFB00000;
    private const uint AkuAkuMusicRequestFlags = 0x001A0000;
    private const int AkuAkuMusicVolumeOffset = 0x16A90C;
    private const uint AkuAkuMusicVolumeOriginal = 0x3C050031;
    private const int AkuAkuMusicVolumeRightOffset = 0x16A910;
    private const uint AkuAkuMusicVolumeRightOriginal = 0x0200202D;
    public const int AkuAkuMusicDefaultVolume = CrashEngine.Runtime.PlaySettings.DefaultGoldenMusicVolume; // Amedo 2026-10-04

    private static readonly (int Offset, uint Original, uint Patched)[] AkuAkuMusicWords =
        new[] { (0x1B2F0, 0x0C04FE8Cu, 0x0C09A651u), (0x32760, 0x8CC40000u, 0x0809A64Au), (0xDE860, 0x27BDFFC0u, 0x0808FC0Cu) }
        .Concat(WordBlock(0x16A818,
            new uint[] { 0x27BDFFE0, 0xFFB00000, 0xFFB10008, 0x00C0802D, 0xFFBF0010, 0x00A0882D, 0x8C820008, 0x0200202D, 0x0C080FE2,
                         0x0040282D, 0x2E23000A, 0x10600041, 0x3C020030, 0x00111880, 0x244243C0, 0x00621821, 0x8C640000, 0x00800008,
                         0x00000000, 0x3C050031, 0x0200202D, 0x0C081018, 0x24A59DC8, 0x10000036, 0xDFB00000, 0x3C050031, 0x0200202D,
                         0x0C081018, 0x24A59DD0, 0x10000030, 0xDFB00000, 0x3C050031, 0x0200202D, 0x0C081018, 0x24A59DD8, 0x1000002A,
                         0xDFB00000, 0x3C050031, 0x0200202D, 0x0C081018, 0x24A59DE0, 0x10000024, 0xDFB00000, 0x3C050031, 0x0200202D,
                         0x0C081018, 0x24A59DE8, 0x1000001E, 0xDFB00000, 0x3C050031, 0x0200202D, 0x0C081018, 0x24A59DF0, 0x10000018,
                         0xDFB00000, 0x3C050031, 0x0200202D, 0x0C081018, 0x24A59DF8, 0x10000012 },
            new uint[] { 0x27BDFFF0, 0xFFBF0000, 0x0C04FE8C, 0x00000000, 0x3C050031, 0x8CA59AB8, 0x3C080027, 0x8CA40010, 0x10800013,
                         0x950A9908, 0x8C890020, 0x112A000D, 0x3C0B001A, 0x012B4825, 0xAD099918, 0x8C8B0008, 0xAD0B991C, 0x8C8B000C,
                         0xAD0B9920, 0x0C079A34, 0xACA00010, 0x0000202D, 0x3C050027, 0x0C077618, 0x24A59908, 0xDFBF0000, 0x03E00008,
                         0x27BD0010, 0x1000FFF8, 0xAD009918, 0x27BDFFE0, 0xFFBF0000, 0xFFA60008, 0xFFA70010, 0x3C050031, 0x8CA59AB8,
                         0x3C060027, 0x8CA40010, 0x1080000F, 0x94C39908, 0x8C880020, 0x1503000C, 0x94C99918, 0x11200006, 0x00000000,
                         0x0000202D, 0x0C077618, 0x24C59918, 0x10000005, 0x00000000, 0x3C014040, 0x44816000, 0x0C079924, 0x24050001,
                         0xDFBF0000, 0xDFA60008, 0xDFA70010, 0x8CC40000, 0x0804C5D9, 0x27BD0020 }))
        .Concat(WordBlock(0x16A914,
            new uint[] { 0x0C081018, 0x24A59E00, 0x1000000C, 0xDFB00000, 0x3C050031, 0x0200202D, 0x0C081018,
                         0x24A59E08, 0x10000006, 0xDFB00000, 0x3C050031, 0x0200202D, 0x0C081018, 0x24A59E10, 0xDFB00000, 0xDFB10008,
                         0xDFBF0010, 0x03E00008, 0x27BD0020 },
            new uint[] { 0x3E4CCCCD, 0x00000000, 0x3F400000, 0x3F400000, 0x3E4CCCCD, 0x3C030031, 0x8C639580,
                         0x1067FFD7, 0x00000000, 0x8CC40000, 0x0804C5D9, 0x00000000, 0x8C880010, 0x8D080010, 0x31080400, 0x1100FFB1,
                         0x00000000, 0x0804FE8C, 0x00000000 }))
        .Concat(WordBlock(0x140030,
            new uint[] { 0x27BDFFE0, 0xFFB00000, 0xE7B40010, 0x0080802D, 0xFFBF0008, 0x8E020068, 0x00021182, 0x30440007, 0x2C830006,
                         0x10600026, 0x46006506, 0x3C020030, 0x00041880, 0x24421020, 0x00621821, 0x8C640000, 0x00800008, 0x00000000,
                         0x0C062E30, 0x00A0202D, 0x1000001E, 0x4600A302, 0xC4A10000, 0x46800860, 0x3C0138C9, 0x34210FDB, 0x44811000,
                         0xC6000004, 0x46020842, 0x46010002, 0x46000064, 0x44020800, 0x30420001, 0x10400011, 0x4600A306 },
            new uint[] { 0x14800020, 0x3C080027, 0x25099908, 0x10A9001D, 0x250A9918, 0x10AA001B, 0x3C0B0031, 0x8D6B9580, 0x11600018,
                         0x00000000, 0x8D6C0010, 0x318C0400, 0x11800014, 0x3C0D0031, 0x8DAD9AB8, 0x11A00011, 0x00000000, 0x8DAD0010,
                         0x11A0000E, 0x952E0000, 0x8DAF0020, 0x15EE000B, 0x94B80000, 0x130E0009, 0x00000000, 0x68B80007, 0x6CB80000,
                         0xFD580000, 0x68B9000F, 0x6CB90008, 0xFD590008, 0x03E00008, 0x24020001, 0x08077619, 0x27BDFFC0 }))
        .ToArray();

    // Amedo 2026-09-25
    private static readonly (int Offset, uint Original, uint Patched)[] AkuAkuGlowFixWords =
        new[] { (0x18CFC, 0x0C08FA3Eu, 0x0C08FC50u) }.Concat(WordBlock(0x140140,
            new uint[] { 0x27BDFFE0, 0xFFB00000, 0xE7B40010, 0x0080802D, 0xFFBF0008, 0x8E020068, 0x00021242, 0x30440007, 0x2C830006,
                         0x10600026, 0x46006506, 0x3C020030, 0x00041880, 0x24421040, 0x00621821, 0x8C640000, 0x00800008 },
            new uint[] { 0x27BDFFE0, 0xFFBF0000, 0x0C08FA3E, 0x00000000, 0x04400009, 0x8E680000, 0x8D0800A0, 0x00024A40, 0x3C0A0037,
                         0x254A0450, 0x012A4821, 0x11000002, 0x00000000, 0xAD2801E8, 0xDFBF0000, 0x03E00008, 0x27BD0020 })).ToArray();

    // Amedo 2026-09-26
    private static readonly (int Offset, uint Original, uint Patched)[] AkuAkuCutsceneResumeWords =
        new[] { (0x790FC, 0x0C04C622u, 0x0C08FC63u), (0x79260, 0x0C04C622u, 0x0C08FC75u), (0x32860, 0x11220006u, 0x080B2098u) }
        .Concat(WordBlock(0x140184,
            new uint[] { 0x00000000, 0x0C062E30, 0x00A0202D, 0x1000001E, 0x4600A302, 0xC4A10000, 0x46800860, 0x3C0138C9, 0x34210FDB,
                         0x44811000, 0xC6000008, 0x46020842, 0x46010002, 0x46000064, 0x44020800, 0x30420001, 0x10400011, 0x4600A306,
                         0x1000000F, 0x46006307, 0x0C06304C, 0x4600A306, 0x1000000B, 0x46000306, 0xC4A10000, 0x46800860, 0x3C0138C9,
                         0x34210FDB, 0x44810000, 0x10000004, 0x46000B02, 0x44800000, 0x1000000E, 0xDFB00000, 0x8E020068, 0x3C030300,
                         0x3C040200, 0x00431824, 0x14640003, 0x3C020100, 0x10000005, 0x46006005, 0x14620003, 0x46006006, 0x46006005,
                         0x46000007, 0xDFB00000, 0xDFBF0008, 0xC7B40010, 0x03E00008 },
            new uint[] { 0x00000000, 0x00000000, 0x3C0B0024, 0x256BF184, 0xAD600000, 0x8C880070, 0x3108000F, 0x24090001, 0x15090009,
                         0x3C0A0031, 0x8D4A972C, 0x8D4A0024, 0x8C880074, 0x8C890078, 0x01094021, 0x010A4823, 0x5D200001, 0xAD680000,
                         0x0804C622, 0xAD640004, 0x27BDFFF0, 0xFFBF0000, 0x0C04C622, 0xFFA40008, 0xDFA40008, 0x3C0B0024, 0x256BF184,
                         0x8D680000, 0x8D6C0004, 0xAD600000, 0x11000010, 0x3C0A0031, 0x1584000E, 0x8D4A972C, 0x8D4A0024, 0x010A4023,
                         0x59000001, 0x24080001, 0xAC880078, 0x90890070, 0x3129000F, 0x35290010, 0xA0890070, 0x8C890010, 0x8D2A0010,
                         0x354A0400, 0xAD2A0010, 0xDFBF0000, 0x03E00008, 0x27BD0010 }))
        .Concat(WordBlock(0x1C9260,
            new uint[] { 0x27BDFF10, 0x0000502D, 0x7FB100D0, 0x7FB50090, 0x0000882D, 0x7FB70070, 0x00A0A82D, 0xAFA60008, 0x0080B82D,
                         0x7FB000E0, 0x0000302D, 0x7FB200C0, 0x7FB300B0, 0x7FB400A0, 0x7FB60080, 0x7FBE0060, 0xFFBF0050, 0xAFA0000C,
                         0x82A30000, 0x2C62002E, 0x10400012, 0x3C020031, 0x00031880, 0x244282B0 },
            new uint[] { 0x3C0B0024, 0x8D6CF184, 0x1180000F, 0x8D6DF188, 0x15A6000D, 0x8CCF0070, 0x31EF000F, 0x25EFFFFF, 0x11E00009,
                         0x00000000, 0x8D0E0004, 0x018E7023, 0x1DC00005, 0x00000000, 0xAD60F184, 0x8CC70010, 0x0804C5D8, 0x00000000,
                         0x15220003, 0x00000000, 0x03E00008, 0x00000000, 0x0804C61A, 0x00000000 }))
        .ToArray();

    private static bool ApplyAkuAkuMusicPatch(byte[] bytes, PatchSet set, uint? musicId, int? volume, Action<string> log) // Amedo
    {
        uint idCur = BitConverter.ToUInt32(bytes, set.AkuMusicId); // Amedo
        uint volCur = BitConverter.ToUInt32(bytes, set.AkuMusicVolume);
        uint volRightCur = BitConverter.ToUInt32(bytes, set.AkuMusicVolumeRight);
        var cur = set.AkuMusic.Select(w => BitConverter.ToUInt32(bytes, w.Offset)).ToArray();
        bool isOriginal = idCur == AkuAkuMusicIdOriginal && volCur == AkuAkuMusicVolumeOriginal
            && volRightCur == AkuAkuMusicVolumeRightOriginal
            && cur.Zip(set.AkuMusic, (c, w) => c == w.Original).All(x => x);
        bool isOurs = (idCur & 0xFFFF0000) == AkuAkuMusicRequestFlags && cur.Zip(set.AkuMusic, (c, w) => c == w.Patched).All(x => x);
        if (!isOriginal && !isOurs)
        {
            log("Golden Aku Aku music: ABORTED — the executable's code isn't the original or our patch (unexpected). Not applied.");
            return false;
        }
        int pct = Math.Clamp(volume ?? AkuAkuMusicDefaultVolume, 1, 100);
        uint idTarget = musicId is { } id ? AkuAkuMusicRequestFlags | (id & 0xFFFF) : AkuAkuMusicIdOriginal;
        uint volTarget = musicId is null ? AkuAkuMusicVolumeOriginal : BitConverter.SingleToUInt32Bits(pct / 100f);
        uint volRightTarget = musicId is null ? AkuAkuMusicVolumeRightOriginal : volTarget;
        if (musicId is null ? isOriginal : isOurs && idCur == idTarget && volCur == volTarget && volRightCur == volRightTarget) return false;
        foreach (var w in set.AkuMusic) // Amedo
            BitConverter.GetBytes(musicId is null ? w.Original : w.Patched).CopyTo(bytes, w.Offset);
        BitConverter.GetBytes(idTarget).CopyTo(bytes, set.AkuMusicId);
        BitConverter.GetBytes(volTarget).CopyTo(bytes, set.AkuMusicVolume);
        BitConverter.GetBytes(volRightTarget).CopyTo(bytes, set.AkuMusicVolumeRight);
        log(musicId is null
            ? "Golden Aku Aku music: restored the original game (no music during the golden invincibility)."
            : $"Golden Aku Aku music: applied (track id {musicId} at {pct}% volume plays during the golden invincibility, then the level music fades back in).");
        return true;
    }

    internal static void ApplyAkuAkuInvincibilityPatch(string discContentPath, int? seconds, bool touchBreak, uint? musicId, int? musicVolume, bool glowFix, Action<string> log)
    {
        var set = FindPatchSet(discContentPath, out var exePath); // Amedo
        int s = seconds ?? AkuAkuDefaultSeconds;
        if (s != AkuAkuInfinite) s = Math.Clamp(s, 1, AkuAkuMaxSeconds);
        if (set is null)
        {
            if (s != AkuAkuDefaultSeconds || touchBreak || musicId is not null || glowFix) log("Aku Aku invincibility: no known executable (SLUS_209.09 / SLES_525.68) — not applied."); // Amedo
            return;
        }
        var bytes = File.ReadAllBytes(exePath);
        if (bytes.Length < set.AkuDuration + 4 || bytes.Length < set.AkuTouchBreak.Max(w => w.Offset) + 4
            || bytes.Length < set.AkuMusic.Max(w => w.Offset) + 4
            || bytes.Length < set.AkuGlowFix.Max(w => w.Offset) + 4
            || bytes.Length < set.AkuCutsceneResume.Max(w => w.Offset) + 4) return;
        bool changed = false;
        uint cur = BitConverter.ToUInt32(bytes, set.AkuDuration);
        uint target = AkuAkuDurationWord(s);
        if ((cur & 0xFFFF0000) != 0x3C010000)
            log("Aku Aku invincibility: ABORTED — the executable's duration instruction isn't the original or our patch (unexpected). Not applied.");
        else if (cur != target)
        {
            BitConverter.GetBytes(target).CopyTo(bytes, set.AkuDuration); // Amedo
            changed = true;
            log(s == AkuAkuDefaultSeconds ? "Aku Aku invincibility: restored the original 8 seconds."
                : s == AkuAkuInfinite ? "Aku Aku invincibility: applied (infinite)."
                : $"Aku Aku invincibility: applied ({s} seconds).");
        }
        changed |= ApplyWordPatch(bytes, set.AkuTouchBreak, touchBreak, // Amedo
            "Golden Aku Aku touch", "touching crates and enemies during the golden invincibility counts as a spin; wumpa are still collected", log);
        changed |= ApplyAkuAkuMusicPatch(bytes, set, musicId, musicVolume, log); // Amedo
        changed |= ApplyWordPatch(bytes, set.AkuGlowFix, glowFix, // Amedo
            "Golden Aku Aku glow", "the golden glow follows Crash into other levels (with Classic Aku Aku)", log);
        changed |= ApplyWordPatch(bytes, set.AkuCutsceneResume, glowFix, // Amedo
            "Golden Aku Aku after cutscenes", "the golden timer keeps running during in-game cutscenes; the rest continues afterwards, or it ends on time mid-cutscene (with Classic Aku Aku)", log);
        if (changed) File.WriteAllBytes(exePath, bytes);
    }

    internal static void ApplyDoubleJumpPatch(string discContentPath, uint? mask, Action<string> log)
    {
        var set = FindPatchSet(discContentPath, out var exePath); // Amedo
        uint m = mask ?? DoubleJumpDefaultMask;
        if (set is null)
        {
            if (m != DoubleJumpDefaultMask) log("Double jump rules: no known executable (SLUS_209.09 / SLES_525.68) — not applied."); // Amedo
            return;
        }
        var bytes = File.ReadAllBytes(exePath);
        if (bytes.Length < set.DoubleJump + 28 || bytes.Length < set.SlideTurn.Max(w => w.Offset) + 4) return;
        bool changed = ApplyJumpTypeMask(bytes, set.DoubleJump, m & 0xFFFF, log); // Amedo
        changed |= ApplyWordPatch(bytes, set.SlideTurn, (m & 0x20) != 0,
            "Slide jump double jump turning", "the double jump after a slide jump turns Crash like a normal double jump", log);
        changed |= ApplyWordPatch(bytes, set.SpinDoubleJump, (m & DoubleJumpSpinBit) != 0, // Amedo
            "Spin jump double jump", "a double jump is allowed during and after a spin jump", log);
        changed |= ApplyWordPatch(bytes, set.SlideBodySlam, (m & SlideJumpBodySlamBit) != 0, // Amedo
            "Slide jump body slam", "the body slam is allowed during a slide jump", log);
        changed |= ApplyWordPatch(bytes, set.SlideSpin, (m & SlideJumpSpinBit) != 0, // Amedo
            "Slide jump spin", "the spin is allowed during a slide jump", log);
        if (changed) File.WriteAllBytes(exePath, bytes);
    }

    private static bool ApplyJumpTypeMask(byte[] bytes, int offset, uint typeMask, Action<string> log) // Amedo
    {
        var cur = new uint[7];
        for (int i = 0; i < 7; i++) cur[i] = BitConverter.ToUInt32(bytes, offset + i * 4);
        bool isOriginal = cur.SequenceEqual(DoubleJumpOriginal);
        bool isOurs = cur.Skip(1).SequenceEqual(DoubleJumpPatched(0).Skip(1)) && (cur[0] & 0xFFFF0000) == 0x34050000;
        if (!isOriginal && !isOurs)
        {
            log("Double jump rules: ABORTED — the executable's jump code isn't the original or our patch (unexpected). Not applied.");
            return false;
        }
        bool wantOriginal = typeMask == DoubleJumpDefaultMask;
        uint[] target = wantOriginal ? DoubleJumpOriginal : DoubleJumpPatched(typeMask);
        if (cur.SequenceEqual(target)) return false;
        for (int i = 0; i < 7; i++) BitConverter.GetBytes(target[i]).CopyTo(bytes, offset + i * 4); // Amedo
        log(wantOriginal
            ? "Double jump rules: restored the original game rules."
            : $"Double jump rules: applied (jump-type mask 0x{typeMask:X2}).");
        return true;
    }

    private static bool ApplyWordPatch(byte[] bytes, (int Offset, uint Original, uint Patched)[] words, bool allow,
        string name, string appliedInfo, Action<string> log)
    {
        var cur = words.Select(w => BitConverter.ToUInt32(bytes, w.Offset)).ToArray();
        bool isOriginal = cur.Zip(words, (c, w) => c == w.Original).All(x => x);
        bool isOurs = cur.Zip(words, (c, w) => c == w.Patched).All(x => x);
        if (!isOriginal && !isOurs)
        {
            log($"{name}: ABORTED — the executable's code isn't the original or our patch (unexpected). Not applied.");
            return false;
        }
        if (allow ? isOurs : isOriginal) return false;
        foreach (var w in words)
            BitConverter.GetBytes(allow ? w.Patched : w.Original).CopyTo(bytes, w.Offset);
        log(allow ? $"{name}: applied ({appliedInfo})." : $"{name}: restored the original rule.");
        return true;
    }

    private static bool BytesEqualAt(byte[] data, int off, byte[] pattern)
    {
        if (off + pattern.Length > data.Length) return false;
        for (int i = 0; i < pattern.Length; i++) if (data[off + i] != pattern[i]) return false;
        return true;
    }

    private static string ReadAsciiZ(byte[] data, int off)
    {
        int end = off;
        while (end < data.Length && data[end] != 0) end++;
        return System.Text.Encoding.ASCII.GetString(data, off, end - off);
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

            // Amedo 2026-09-22
            int beforeCount = linkItem.LinksList.Count;
            linkItem.LinksList.RemoveAll(link =>
                !keepSet.Contains(link.Path.Replace('/', '\\').TrimStart('\\')));
            int removed = beforeCount - linkItem.LinksList.Count;
            if (removed == 0) continue;
            linksNeutered += removed;

            Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
            using var fs = File.Create(stagedPath);
            using var writer = new BinaryWriter(fs);
            sm2.Write(writer);
        }

        log($"New Game: checked {levelsChecked} kept level(s), removed {linksNeutered} dangling link(s) (upcoming scenes + load walls) pointing at excluded levels.");
    }
}
