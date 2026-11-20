using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-20
[SupportedOSPlatform("linux")]
internal static class DesktopEntry
{
    public static void Create(string file, string target, string workingDirectory, string name, string? icon)
    {
        var lines = new List<string>
        {
            "[Desktop Entry]",
            "Type=Application",
            $"Name={name}",
            $"Exec=\"{target}\"",
            $"Path={workingDirectory}",
            "Terminal=false",
        };
        if (icon is not null) lines.Add($"Icon={icon}");
        File.WriteAllText(file, string.Join("\n", lines) + "\n");
        File.SetUnixFileMode(file, File.GetUnixFileMode(file) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
}
