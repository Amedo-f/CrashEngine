using System.Text;

namespace CrashEngine.Importer;

// Amedo 2026-09-21
public static class Ps2IsoReader
{
    private const int UserData = 2048;

    private sealed class Layout
    {
        public int RawSectorSize;
        public int UserDataOffset;
    }

    private sealed record DirEntry(string Name, uint Lba, uint Size, bool IsDir);

    public static bool IsIso(string isoPath)
    {
        try
        {
            using var fs = File.OpenRead(isoPath);
            return DetectLayout(fs) is not null;
        }
        catch { return false; }
    }

    public static int Extract(string isoPath, string destDir, Action<string, float>? progress = null)
    {
        using var fs = File.OpenRead(isoPath);
        return Extract(fs, destDir, progress);
    }

    // Amedo 2026-10-27
    public static int Extract(FileStream fs, string destDir, Action<string, float>? progress = null)
    {
        var layout = DetectLayout(fs)
            ?? throw new InvalidOperationException("Not a recognizable ISO9660 disc image (no CD001 volume descriptor).");

        var pvd = ReadSector(fs, layout, 16);
        uint rootLba  = ReadU32Le(pvd, 156 + 2);
        uint rootSize = ReadU32Le(pvd, 156 + 10);

        var files = new List<(string Rel, uint Lba, uint Size)>();
        var dirs  = new List<string>();
        Walk(fs, layout, rootLba, rootSize, "", files, dirs);

        foreach (var d in dirs)
            Directory.CreateDirectory(Path.Combine(destDir, d));
        Directory.CreateDirectory(destDir);

        int done = 0;
        long total = files.Sum(f => (long)f.Size), copied = 0;
        foreach (var (rel, lba, size) in files)
        {
            var outPath = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            long before = copied;
            ExtractFile(fs, layout, lba, size, outPath, bytes => progress?.Invoke(rel, total == 0 ? 1f : (float)(before + bytes) / total));
            copied += size;
            done++;
            progress?.Invoke(rel, total == 0 ? 1f : (float)copied / total);
        }
        return done;
    }

    // Amedo 2026-10-27: one file's bytes (path like "SYSTEM.CNF" or "CRASH6/CRASH.BH"), or null if it isn't on the disc
    public static byte[]? ReadFile(string isoPath, string relPath)
    {
        using var fs = File.OpenRead(isoPath);
        return ReadFile(fs, relPath);
    }

    public static byte[]? ReadFile(FileStream fs, string relPath)
    {
        var layout = DetectLayout(fs);
        if (layout is null) return null;
        var pvd = ReadSector(fs, layout, 16);
        uint lba = ReadU32Le(pvd, 156 + 2), size = ReadU32Le(pvd, 156 + 10);
        var parts = relPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var entry = ReadDir(fs, layout, lba, size).FirstOrDefault(e => string.Equals(e.Name, parts[i], StringComparison.OrdinalIgnoreCase));
            if (entry is null || entry.IsDir != (i < parts.Length - 1)) return null;
            (lba, size) = (entry.Lba, entry.Size);
        }
        using var ms = new MemoryStream((int)size);
        CopyFile(fs, layout, lba, size, ms, null);
        return ms.ToArray();
    }

    private static void Walk(FileStream fs, Layout layout, uint dirLba, uint dirSize, string prefix,
        List<(string, uint, uint)> files, List<string> dirs)
    {
        foreach (var e in ReadDir(fs, layout, dirLba, dirSize))
        {
            var rel = prefix.Length == 0 ? e.Name : prefix + "/" + e.Name;
            if (e.IsDir)
            {
                dirs.Add(rel);
                Walk(fs, layout, e.Lba, e.Size, rel, files, dirs);
            }
            else
            {
                files.Add((rel, e.Lba, e.Size));
            }
        }
    }

    private static List<DirEntry> ReadDir(FileStream fs, Layout layout, uint lba, uint size)
    {
        var entries = new List<DirEntry>();
        int sectorCount = (int)((size + UserData - 1) / UserData);
        for (int s = 0; s < sectorCount; s++)
        {
            var sector = ReadSector(fs, layout, lba + (uint)s);
            int pos = 0;
            while (pos < UserData)
            {
                byte recLen = sector[pos];
                if (recLen == 0) break;

                uint extLba  = ReadU32Le(sector, pos + 2);
                uint extSize = ReadU32Le(sector, pos + 10);
                byte flags   = sector[pos + 25];
                byte nameLen = sector[pos + 32];

                bool special = nameLen == 1 && (sector[pos + 33] == 0x00 || sector[pos + 33] == 0x01);
                if (!special)
                {
                    string name = CleanName(Encoding.ASCII.GetString(sector, pos + 33, nameLen));
                    bool isDir = (flags & 0x02) != 0;
                    if (name.Length > 0) entries.Add(new DirEntry(name, extLba, extSize, isDir));
                }
                pos += recLen;
            }
        }
        return entries;
    }

    private static void ExtractFile(FileStream fs, Layout layout, uint lba, uint size, string outPath, Action<long>? copied = null)
    {
        using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        CopyFile(fs, layout, lba, size, outFs, copied);
    }

    private static void CopyFile(FileStream fs, Layout layout, uint lba, uint size, Stream to, Action<long>? copied)
    {
        int chunk = layout.RawSectorSize == UserData ? 1 << 20 : UserData;
        var buf = new byte[chunk];
        uint remaining = size;
        uint sector = lba;
        long done = 0;
        while (remaining > 0)
        {
            fs.Position = (long)sector * layout.RawSectorSize + layout.UserDataOffset;
            int want = (int)Math.Min(remaining, (uint)chunk);
            fs.ReadExactly(buf, 0, want);
            to.Write(buf, 0, want);
            remaining -= (uint)want;
            sector += (uint)(want + UserData - 1) / UserData;
            done += want;
            if (copied is not null && (done & ((1 << 24) - 1)) < want) copied(done);
        }
    }

    private static byte[] ReadSector(FileStream fs, Layout layout, uint lba)
    {
        var buf = new byte[UserData];
        fs.Position = (long)lba * layout.RawSectorSize + layout.UserDataOffset;
        fs.ReadExactly(buf, 0, UserData);
        return buf;
    }

    private static Layout? DetectLayout(FileStream fs)
    {
        Span<byte> id = stackalloc byte[6];
        (int raw, int off)[] candidates =
        {
            (2048, 0),
            (2352, 16),
            (2352, 24),
        };
        foreach (var (raw, off) in candidates)
        {
            long pos = (long)16 * raw + off;
            if (pos + 6 > fs.Length) continue;
            fs.Position = pos;
            try { fs.ReadExactly(id); } catch { continue; }
            if (id[1] == (byte)'C' && id[2] == (byte)'D' && id[3] == (byte)'0' && id[4] == (byte)'0' && id[5] == (byte)'1')
                return new Layout { RawSectorSize = raw, UserDataOffset = off };
        }
        return null;
    }

    private static string CleanName(string raw)
    {
        int semi = raw.IndexOf(';');
        if (semi >= 0) raw = raw[..semi];
        if (raw.EndsWith('.')) raw = raw[..^1];
        return raw;
    }

    private static uint ReadU32Le(byte[] b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
}
