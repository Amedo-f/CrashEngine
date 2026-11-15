namespace CrashEngine.Platform;

// Amedo 2026-11-15
public static class SystemFonts
{
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly string[] UnixDirectories =
    {
        "/usr/share/fonts", "/usr/local/share/fonts",
        Path.Combine(Home, ".local", "share", "fonts"),
        Path.Combine(Home, ".fonts"),
    };

    public static string? Find(params string[] names)
    {
        var windowsFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var name in names)
        {
            if (windowsFonts.Length > 0 && Path.Combine(windowsFonts, name) is var direct && File.Exists(direct)) return direct;
            if (OperatingSystem.IsWindows()) continue;
            foreach (var dir in UnixDirectories)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    if (Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories).FirstOrDefault() is { } found) return found;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
        return null;
    }
}
