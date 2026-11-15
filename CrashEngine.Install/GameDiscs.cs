using System.Security.Cryptography;
using System.Text;
using CrashEngine.Importer;

namespace CrashEngine.Install;

// Amedo 2026-11-12
public static class GameDiscs
{
    public sealed record Disc(string Id, string Region, string Exe, string ExeSha256, string HeaderSha256);

    public static readonly Disc[] Known =
    [
        new("US", "USA", "SLUS_209.09",
            "8d114879fb704d328394771aec6276136bdb4ecf4c5f8de06139b7b42974038f",
            "27fe74b6f1253954dc9c2bede794a8528df0b035852ce4e8213d8d08ad42f6a7"),
        new("EU", "Europe", "SLES_525.68",
            "7a609bb3ac32c4ef42129088902c4cf8aa05cf9cb62845e121764e32d2cedc1d",
            "5e3c94140c2e6ca02156fde0fb553514af6006d6653e7361d4fe7d43acefd280"),
    ];

    private const string ReadyMark = ".ready";

    public static string Root { get; set; } = "";

    public static string Folder(Disc disc) => Path.Combine(Root, "discs", disc.Id);

    public static bool Installed(Disc disc) => IsReady(Folder(disc));

    public static bool IsReady(string folder) => File.Exists(Path.Combine(folder, ReadyMark));

    public static (Disc? Disc, string Message) Identify(string path) // Amedo 2026-11-12
    {
        try
        {
            using var iso = File.OpenRead(path);
            return Identify(iso);
        }
        catch (Exception ex) { return (null, ex.Message); }
    }

    public static (Disc? Disc, string Message) Identify(FileStream iso)
    {
        byte[]? cnf;
        try { cnf = Ps2IsoReader.ReadFile(iso, "SYSTEM.CNF"); }
        catch (Exception ex)
        {
            Console.WriteLine($"[launcher] reading the chosen file ({iso.Length} bytes, seek {iso.CanSeek}) failed: {ex}");
            cnf = null;
        }
        if (cnf is null) return (null, "This file is not a PlayStation 2 disc image.");
        string boot = Encoding.ASCII.GetString(cnf);
        var disc = Known.FirstOrDefault(d => boot.Contains(d.Exe, StringComparison.OrdinalIgnoreCase));
        if (disc is null) return (null, "This is not a Crash Twinsanity disc.");
        string exe = Sha256(Ps2IsoReader.ReadFile(iso, disc.Exe)), header = Sha256(Ps2IsoReader.ReadFile(iso, "CRASH6/CRASH.BH"));
        Console.WriteLine($"[launcher] disc {disc.Id}: {disc.Exe} {exe}, CRASH.BH {header}");
        if (exe != disc.ExeSha256 || header != disc.HeaderSha256)
            return (null, $"This Crash Twinsanity ({disc.Region}) disc is modified.\nPlease use an original, unmodified disc.");
        return (disc, $"Crash Twinsanity ({disc.Region})");
    }

    public static void Unpack(FileStream iso, Disc disc, Action<float> progress) => UnpackTo(iso, disc, Folder(disc), progress);

    public static void UnpackTo(FileStream iso, Disc disc, string folder, Action<float> progress)
    {
        var partial = folder + ".partial";
        if (Directory.Exists(partial)) Directory.Delete(partial, true);
        int files = Ps2IsoReader.Extract(iso, partial, (_, p) => progress(p));
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(folder))!);
        Directory.Move(partial, folder);
        File.WriteAllText(Path.Combine(folder, ReadyMark), disc.ExeSha256);
        Console.WriteLine($"[launcher] disc {disc.Id}: unpacked {files} files to {folder}");
    }

    private static string Sha256(byte[]? bytes) => bytes is null ? "" : Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
