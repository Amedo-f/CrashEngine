namespace CrashLauncher;

internal static class DiscBackup
{
    public static string BuildDir(string scriptOut, string discContentPath) =>
        Path.Combine(Path.GetDirectoryName(scriptOut) ?? discContentPath, "Build");

    public static string Dir(string buildDir, string discContentPath) =>
        Path.Combine(buildDir, "original_backup_" + Path.GetFileName(discContentPath.TrimEnd('\\', '/')));

    public static string DirFor(string scriptOut, string discContentPath) =>
        Dir(BuildDir(scriptOut, discContentPath), discContentPath);

    public static string BdPath(string scriptOut, string discContentPath) =>
        Path.Combine(DirFor(scriptOut, discContentPath), "Crash.BD");

    public static string PristineSource(string scriptOut, string discContentPath)
    {
        string bd = BdPath(scriptOut, discContentPath);
        return File.Exists(bd) ? bd : discContentPath;
    }
}
