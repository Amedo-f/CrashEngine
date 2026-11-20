using CrashEngine.Platform.Linux;
using CrashEngine.Platform.Windows;

namespace CrashEngine.Platform;

// Amedo 2026-11-20
public static class PlatformFiles
{
    public static string FindOnDisk(string root, string[] parts)
    {
        if (OperatingSystem.IsLinux()) return LinuxPaths.FindIgnoringCase(root, parts);
        return Path.Combine([root, .. parts]);
    }

    public static void ShowFolder(string folder)
    {
        if (OperatingSystem.IsWindows()) WinExplorer.ShowFolder(folder);
        else if (OperatingSystem.IsLinux()) XdgOpen.ShowFolder(folder);
    }

    public static void CreateShortcut(string folder, string name, string target, string workingDirectory)
    {
        if (OperatingSystem.IsWindows())
            WinShortcut.Create(Path.GetFullPath(Path.Combine(folder, name + ".lnk")), Path.GetFullPath(target), Path.GetFullPath(workingDirectory), name);
        else if (OperatingSystem.IsLinux())
        {
            var icon = Path.Combine(Path.GetFullPath(workingDirectory), "Icons", "game-icon-48.png");
            DesktopEntry.Install(Path.GetFullPath(folder), name, Path.GetFullPath(target), Path.GetFullPath(workingDirectory), File.Exists(icon) ? icon : null);
        }
    }

    public static string DriveRoot(string path)
    {
        if (OperatingSystem.IsLinux()) return LinuxDrives.MountOf(path);
        return Path.GetPathRoot(Path.GetFullPath(path)) ?? "";
    }

    public static IEnumerable<DriveInfo> Drives()
    {
        var drives = OperatingSystem.IsLinux() ? LinuxDrives.UserDrives() : DriveInfo.GetDrives();
        return drives.Where(d => d.IsReady);
    }
}
