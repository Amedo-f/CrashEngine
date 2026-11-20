using CrashEngine.Platform;

namespace CrashEngine.Core;

// Amedo 2026-11-14
public static class GamePaths
{
    public const char Separator = '\\';

    public static string Folder(string gamePath)
    {
        int cut = gamePath.LastIndexOf(Separator);
        return cut < 0 ? "" : gamePath[..cut];
    }

    public static string FileName(string gamePath) => gamePath[(gamePath.LastIndexOf(Separator) + 1)..];

    public static string FileNameWithoutExtension(string gamePath)
    {
        var name = FileName(gamePath);
        int dot = name.LastIndexOf('.');
        return dot < 0 ? name : name[..dot];
    }

    public static string Combine(string folder, string name) => folder.Length == 0 ? name : folder + Separator + name;

    public static string FromDisk(string diskRelative) => diskRelative.Replace(Path.DirectorySeparatorChar, Separator);

    public static string ToDisk(string gamePath) => gamePath.Replace(Separator, Path.DirectorySeparatorChar);

    public static string OnDisk(string root, string gamePath) => Path.Combine(root, ToDisk(gamePath));

    // Amedo 2026-11-20
    public static string FindOnDisk(string root, params string[] parts) => PlatformFiles.FindOnDisk(root, parts);
}
