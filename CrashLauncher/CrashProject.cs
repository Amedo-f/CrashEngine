using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace CrashLauncher;

public sealed class CrashProject
{
    public string    Name                { get; set; } = "";
    public string    Path                { get; set; } = "";
    public string?   DiscContentPathPS2  { get; set; }
    public string?   DiscContentPathXbox { get; set; }
    public DateTime  LastModified        { get; set; } = DateTime.Now;
    public string    Version             { get; set; } = "1.0.0";

    public bool      Completed           { get; set; } = false;

    public bool         IsNewGame     { get; set; } = false;
    public List<string> ClaimedLevels { get; set; } = new();

    public List<string> ExcludedCutscenes { get; set; } = new();

    [JsonIgnore]
    public string ProjectPath => System.IO.Path.Combine(Path, Name);

    public static bool ValidateDiscPS2(string discPath) =>
        Directory.Exists(discPath) &&
        Directory.GetFiles(discPath)
                 .Select(f => System.IO.Path.GetFileName(f).ToLowerInvariant())
                 .Contains("system.cnf");

    public void CreateProjectStructure()
    {
        Directory.CreateDirectory(ProjectPath);
        Directory.CreateDirectory(System.IO.Path.Combine(ProjectPath, "assets"));
        Directory.CreateDirectory(System.IO.Path.Combine(ProjectPath, "disc"));
    }

    public void CopyDiscContents(Action<float>? progress = null)
    {
        if (string.IsNullOrEmpty(DiscContentPathPS2)) return;
        var source = DiscContentPathPS2;
        var target = System.IO.Path.Combine(ProjectPath, "disc", "ps2");

        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(System.IO.Path.Combine(target,
                System.IO.Path.GetRelativePath(source, dir)));

        var files = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories);
        int done = 0;
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount)) };
        Parallel.ForEach(files, options, file =>
        {
            var dest = System.IO.Path.Combine(target, System.IO.Path.GetRelativePath(source, file));
            var srcInfo = new FileInfo(file);
            var destInfo = new FileInfo(dest);
            if (!destInfo.Exists || destInfo.Length != srcInfo.Length)
                File.Copy(file, dest, true);
            int n = Interlocked.Increment(ref done);
            progress?.Invoke(n / (float)files.Length);
        });

        DiscContentPathPS2 = target;
    }

    public void Save()
    {
        LastModified = DateTime.Now;
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(System.IO.Path.Combine(ProjectPath, Name + ".tson"), json);
    }

    public static CrashProject? Open(string folderOrTson)
    {
        string? tson = null;
        if (File.Exists(folderOrTson) &&
            folderOrTson.EndsWith(".tson", StringComparison.OrdinalIgnoreCase))
            tson = folderOrTson;
        else if (Directory.Exists(folderOrTson))
            tson = Directory.GetFiles(folderOrTson, "*.tson").FirstOrDefault();

        if (tson is null) return null;
        try { return JsonSerializer.Deserialize<CrashProject>(File.ReadAllText(tson)); }
        catch { return null; }
    }

    private static string RecentsFile =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "recent_projects.txt");

    public static List<string> GetRecents() =>
        File.Exists(RecentsFile)
            ? File.ReadAllLines(RecentsFile).Where(Directory.Exists).Distinct().Take(10).ToList()
            : new List<string>();

    public static void AddRecent(string projectPath)
    {
        var list = GetRecents();
        list.Remove(projectPath);
        list.Insert(0, projectPath);
        File.WriteAllLines(RecentsFile, list.Take(10));
    }


    public static List<CrashProject> FindIncompleteProjects()
    {
        var result = new List<CrashProject>();
        var root = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrashEngineProjects");
        if (!Directory.Exists(root)) return result;

        foreach (var dir in Directory.GetDirectories(root))
        {
            var tson = Directory.GetFiles(dir, "*.tson").FirstOrDefault();
            if (tson is null) continue;
            try
            {
                var p = JsonSerializer.Deserialize<CrashProject>(File.ReadAllText(tson));
                if (p is not null && !p.Completed) result.Add(p);
            }
            catch {  }
        }
        return result;
    }

    public static void DeleteProject(string projectPath)
    {
        if (Directory.Exists(projectPath)) Directory.Delete(projectPath, recursive: true);
    }
}
