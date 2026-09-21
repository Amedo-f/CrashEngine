using System.Text;

namespace CrashEngine.Importer;

// Amedo 2026-09-21
// Minimal, dependency-free ISO9660 reader for PS2 game discs. Handles both 2048-byte
// (Mode 1, the common case) and raw 2352-byte sectors (Mode 1 + Mode 2/Form 1), streams
// large files, and reproduces the disc's file tree on disk. Notes live in the docs.
public static class Ps2IsoReader
{
    private const int UserData = 2048;

    private sealed class Layout
    {
        public int RawSectorSize;   // 2048 or 2352
        public int UserDataOffset;  // 0, 16 or 24
    }

    private sealed record DirEntry(string Name, uint Lba, uint Size, bool IsDir);

    /// <summary>True if the file looks like a readable ISO9660 disc image.</summary>
    public static bool IsIso(string isoPath)
    {
        try
        {
            using var fs = File.OpenRead(isoPath);
            return DetectLayout(fs) is not null;
        }
        catch { return false; }
    }

    /// <summary>
    /// Extracts every file/folder from the ISO into <paramref name="destDir"/>, preserving the
    /// directory tree. progress(relativePath, 0..1) is called per file. Returns file count.
    /// </summary>
    public static int Extract(string isoPath, string destDir, Action<string, float>? progress = null)
    {
        using var fs = File.OpenRead(isoPath);
        var layout = DetectLayout(fs)
            ?? throw new InvalidOperationException("Not a recognizable ISO9660 disc image (no CD001 volume descriptor).");

        // Root directory record lives at byte 156 of the Primary Volume Descriptor (LBA 16).
        var pvd = ReadSector(fs, layout, 16);
        uint rootLba  = ReadU32Le(pvd, 156 + 2);
        uint rootSize = ReadU32Le(pvd, 156 + 10);

        // First pass: enumerate all files (for progress), then extract.
        var files = new List<(string Rel, uint Lba, uint Size)>();
        var dirs  = new List<string>();
        Walk(fs, layout, rootLba, rootSize, "", files, dirs);

        foreach (var d in dirs)
            Directory.CreateDirectory(Path.Combine(destDir, d));
        Directory.CreateDirectory(destDir);

        int done = 0;
        foreach (var (rel, lba, size) in files)
        {
            var outPath = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            ExtractFile(fs, layout, lba, size, outPath);
            done++;
            progress?.Invoke(rel, files.Count == 0 ? 1f : (float)done / files.Count);
        }
        return done;
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
                if (recLen == 0) break; // rest of this sector is padding

                uint extLba  = ReadU32Le(sector, pos + 2);
                uint extSize = ReadU32Le(sector, pos + 10);
                byte flags   = sector[pos + 25];
                byte nameLen = sector[pos + 32];

                // 0x00 = ".", 0x01 = ".." — skip both
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

    private static void ExtractFile(FileStream fs, Layout layout, uint lba, uint size, string outPath)
    {
        using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        var buf = new byte[UserData];
        uint remaining = size;
        uint sector = lba;
        while (remaining > 0)
        {
            long pos = (long)sector * layout.RawSectorSize + layout.UserDataOffset;
            fs.Position = pos;
            int want = (int)Math.Min(remaining, (uint)UserData);
            fs.ReadExactly(buf, 0, want);
            outFs.Write(buf, 0, want);
            remaining -= (uint)want;
            sector++;
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
        // Volume descriptors start at LBA 16. A valid one begins with a type byte then "CD001".
        Span<byte> id = stackalloc byte[6];
        (int raw, int off)[] candidates =
        {
            (2048, 0),   // Mode 1 / 2048-byte user sectors (most PS2 ISOs)
            (2352, 16),  // raw Mode 1 (12 sync + 4 header)
            (2352, 24),  // raw Mode 2 / Form 1 (+ 8-byte subheader)
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
        // Strip the ISO9660 ";1" version and any trailing dot on extension-less names.
        int semi = raw.IndexOf(';');
        if (semi >= 0) raw = raw[..semi];
        if (raw.EndsWith('.')) raw = raw[..^1];
        return raw;
    }

    private static uint ReadU32Le(byte[] b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
}
