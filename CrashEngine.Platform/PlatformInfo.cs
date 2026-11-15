namespace CrashEngine.Platform;

// Amedo 2026-11-15
public static class PlatformInfo
{
    public static string Rid => OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";

    public static string ExeFile(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    public static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
}
