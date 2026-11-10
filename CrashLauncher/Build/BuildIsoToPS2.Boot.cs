using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;

namespace CrashLauncher;

public static partial class BuildIsoToPS2
{
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
}
