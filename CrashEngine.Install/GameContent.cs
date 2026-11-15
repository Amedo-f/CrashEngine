using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrashEngine.Install;

// Amedo 2026-11-12
public static class GameContent
{
    private const string ActiveFile = "active.json";

    public static string? ActiveProject
    {
        get
        {
            try
            {
                var file = Path.Combine(GameDiscs.Root, ActiveFile);
                if (!File.Exists(file)) return null;
                var project = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))?.GetValueOrDefault("Project");
                return project is not null && Directory.Exists(project) ? project : null;
            }
            catch (Exception) { return null; }
        }
        private set => File.WriteAllText(Path.Combine(GameDiscs.Root, ActiveFile), JsonSerializer.Serialize(new Dictionary<string, string?> { ["Project"] = value }));
    }

    public static void Apply(GameDiscs.Disc disc, FileStream? iso, string? modName, FileStream? mod, Action<string, float> progress)
    {
        if (!GameDiscs.Installed(disc))
        {
            if (iso is null) throw new InvalidOperationException($"The {disc.Region} disc isn't prepared on this phone yet.");
            GameDiscs.Unpack(iso, disc, p => progress($"Preparing Crash Twinsanity ({disc.Region})...", p));
        }
        ActiveProject = mod is null || modName is null ? OriginalProject(disc) : UnpackMod(disc, modName, mod, progress);
        Console.WriteLine($"[setup] active: {ActiveProject}");
    }

    public static void WriteProject(string folder, string? discPath)
    {
        Directory.CreateDirectory(folder);
        var json = new JsonObject { ["Name"] = "Crash Twinsanity", ["Completed"] = true, ["IsNewGame"] = false };
        if (discPath is not null) json["DiscContentPathPS2"] = discPath;
        File.WriteAllText(Path.Combine(folder, "Crash Twinsanity.tson"), json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void PointModAtDisc(string folder, string? discPath)
    {
        foreach (var tson in Directory.GetFiles(folder, "*.tson"))
        {
            var json = JsonNode.Parse(File.ReadAllText(tson))!.AsObject();
            if (discPath is null) json.Remove("DiscContentPathPS2");
            else json["DiscContentPathPS2"] = discPath;
            File.WriteAllText(tson, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string OriginalProject(GameDiscs.Disc disc)
    {
        var folder = Path.Combine(GameDiscs.Root, "games", "original-" + disc.Id.ToLowerInvariant());
        if (!File.Exists(Path.Combine(folder, "Crash Twinsanity.tson"))) WriteProject(folder, GameDiscs.Folder(disc));
        return folder;
    }

    private static string UnpackMod(GameDiscs.Disc disc, string name, FileStream mod, Action<string, float> progress)
    {
        var folder = Path.Combine(GameDiscs.Root, "mods", string.Concat(name.Split(Path.GetInvalidFileNameChars())));
        Mods.Extract(mod, folder, p => progress($"Installing {name}...", p));
        PointModAtDisc(folder, GameDiscs.Folder(disc));
        return folder;
    }
}
