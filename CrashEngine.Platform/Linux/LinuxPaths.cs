using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-20
[SupportedOSPlatform("linux")]
internal static class LinuxPaths
{
    public static string FindIgnoringCase(string root, string[] parts)
    {
        var path = Path.Combine([root, .. parts]);
        if (File.Exists(path) || Directory.Exists(path)) return path;
        var current = root;
        foreach (var part in parts)
        {
            var exact = Path.Combine(current, part);
            if (File.Exists(exact) || Directory.Exists(exact))
            {
                current = exact;
                continue;
            }
            if (!Directory.Exists(current)) return path;
            string? match = null;
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                if (string.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase))
                {
                    match = entry;
                    break;
                }
            if (match is null) return path;
            current = match;
        }
        return current;
    }
}
