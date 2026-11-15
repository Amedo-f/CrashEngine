namespace CrashEngine.Install;

// Amedo 2026-11-12
public static class RecentProjects
{
    public const int Max = 30;

    public static string File =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrashEngine", "recent_projects.txt");

    public static void ImportLegacy(string legacyFile)
    {
        if (System.IO.File.Exists(File) || !System.IO.File.Exists(legacyFile)) return;
        Write(System.IO.File.ReadAllLines(legacyFile));
    }

    public static List<string> Get() =>
        System.IO.File.Exists(File)
            ? System.IO.File.ReadAllLines(File).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Take(Max).ToList()
            : new List<string>();

    public static void Add(string projectPath)
    {
        var list = Get();
        list.RemoveAll(p => string.Equals(p, projectPath, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, projectPath);
        Write(list);
    }

    public static void Remove(string projectPath)
    {
        var list = Get();
        if (list.RemoveAll(p => string.Equals(p, projectPath, StringComparison.OrdinalIgnoreCase)) > 0) Write(list);
    }

    private static void Write(IEnumerable<string> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllLines(File, list.Take(Max));
    }
}
