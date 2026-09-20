using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;

namespace CrashEngine.Importer;

public sealed class PackageReader : IDisposable
{
    private FileStream?   _bdStream;
    private BinaryReader? _bdReader;
    private readonly List<BHRecord> _records = new();

    private readonly object _bdLock = new();

    private readonly string? _root;

    public IReadOnlyList<BHRecord> Records => _records;

    public string? ShadowDir { get; set; }

    public static PackageReader Open(string source)
    {
        if (File.Exists(source) &&
            Path.GetExtension(source).Equals(".bd", StringComparison.OrdinalIgnoreCase))
        {
            var bh = Path.ChangeExtension(source, ".BH");
            if (!File.Exists(bh)) throw new FileNotFoundException($"BH header not found: {bh}");
            return new PackageReader(source, bh);
        }

        if (Directory.Exists(source))
        {
            var crash6 = Path.Combine(source, "Crash6");
            if (Directory.Exists(crash6))
            {
                var bds = Directory.GetFiles(crash6, "*.BD", SearchOption.TopDirectoryOnly);
                if (bds.Length > 0)
                {
                    var bh = Path.ChangeExtension(bds[0], ".BH");
                    if (File.Exists(bh))
                    {
                        Console.WriteLine($"[PackageReader] Disc mode: {bds[0]}");
                        return new PackageReader(bds[0], bh);
                    }
                }
            }
            return new PackageReader(source);
        }

        throw new FileNotFoundException($"Package source not found: {source}");
    }

    public PackageReader(string bdPath, string bhPath)
    {
        using var bhStream = new FileStream(bhPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var bhReader = new BinaryReader(bhStream);
        bhReader.ReadInt32();
        while (bhStream.Position < bhStream.Length)
        {
            var r = new BHRecord();
            r.Read(bhReader, 0);
            _records.Add(r);
        }
        _bdStream = new FileStream(bdPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _bdReader = new BinaryReader(_bdStream);
    }

    public PackageReader(string extractedRoot)
    {
        _root = extractedRoot;
        foreach (var file in Directory.EnumerateFiles(extractedRoot, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (ext.StartsWith(".back", StringComparison.OrdinalIgnoreCase)) continue;

            var rel = Path.GetRelativePath(extractedRoot, file)
                          .Replace('/', '\\');
            var r = new BHRecord { Path = rel, Offset = 0, Length = (int)new FileInfo(file).Length };
            _records.Add(r);
        }
        Console.WriteLine($"[PackageReader] Folder mode: {_root}  ({_records.Count} files)");
    }

    public IEnumerable<BHRecord> GetByExtension(string ext) =>
        _records.Where(r => r.Path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public MemoryStream OpenRecord(BHRecord record)
    {
        if (_root is not null)
        {
            var full = Path.Combine(_root, record.Path);
            return new MemoryStream(File.ReadAllBytes(full));
        }
        lock (_bdLock)
        {
            _bdStream!.Position = record.Offset;
            var data = _bdReader!.ReadBytes(record.Length);
            return new MemoryStream(data);
        }
    }

    public MemoryStream? OpenByPath(string path)
    {
        if (ShadowDir is not null)
        {
            var shadowFile = Path.Combine(ShadowDir, path);
            if (File.Exists(shadowFile))
                return new MemoryStream(File.ReadAllBytes(shadowFile));
        }

        var r = _records.FirstOrDefault(x =>
            x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        return r is null ? null : OpenRecord(r);
    }

    public void Dispose()
    {
        _bdReader?.Dispose();
        _bdStream?.Dispose();
    }
}
