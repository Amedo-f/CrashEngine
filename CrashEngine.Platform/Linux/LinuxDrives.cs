using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-20
[SupportedOSPlatform("linux")]
internal static class LinuxDrives
{
    private static readonly string[] RemovableRoots = ["/media/", "/mnt/", "/run/media/"];

    public static string MountOf(string path)
    {
        string full = Path.GetFullPath(path).TrimEnd('/') + "/", best = "/";
        foreach (var d in DriveInfo.GetDrives())
        {
            string root = d.RootDirectory.FullName.TrimEnd('/') + "/";
            if (root.Length > best.Length && full.StartsWith(root, StringComparison.Ordinal)) best = root;
        }
        return best.Length > 1 ? best.TrimEnd('/') : best;
    }

    public static IEnumerable<DriveInfo> UserDrives() =>
        DriveInfo.GetDrives().Where(d => d.RootDirectory.FullName == "/" ||
                                         RemovableRoots.Any(r => d.RootDirectory.FullName.StartsWith(r, StringComparison.Ordinal)));
}
