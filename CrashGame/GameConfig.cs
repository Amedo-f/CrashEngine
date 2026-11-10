using System.Globalization;
using System.Text.Json;

namespace CrashGame;

// Amedo 2026-09-30
sealed class GameConfig
{
    public string Project { get; set; } = "";
    public string Level { get; set; } = @"Levels\Earth\Hub\beach";
    public bool Windowed { get; set; }
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public float Volume { get; set; } = 1f;

    public bool Hidden;
    public double QuitAfter;
    public double ShotAt;
    public bool RecordInput; // Amedo
    public string Disc = "";
    public string SavedChunks = "";
    public string? Tson; // Amedo 2026-10-04
    public string ProjectFolder = "";
    public string? HdTextureDirectory; // Amedo 2026-10-02

    // Amedo
    public GameConfig ForLevel(string level)
    {
        var copy = (GameConfig)MemberwiseClone();
        copy.Level = level.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? level[..^4] : level;
        return copy;
    }

    public static GameConfig Load(string[] args)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "game.json");
        var cfg = File.Exists(file)
            ? JsonSerializer.Deserialize<GameConfig>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new GameConfig()
            : new GameConfig();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i].ToLowerInvariant())
            {
                case "--project": cfg.Project = Next(); break;
                case "--level": cfg.Level = Next(); break;
                case "--windowed": cfg.Windowed = true; break;
                case "--size":
                    var wh = Next().Split('x');
                    cfg.Width = int.Parse(wh[0], CultureInfo.InvariantCulture);
                    cfg.Height = int.Parse(wh[1], CultureInfo.InvariantCulture);
                    break;
                case "--hidden": cfg.Hidden = true; cfg.Windowed = true; break;
                case "--quit-after": cfg.QuitAfter = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--volume": cfg.Volume = float.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--shot": cfg.ShotAt = double.Parse(Next(), CultureInfo.InvariantCulture); break;
#if DEBUG
                case "--record-input": cfg.RecordInput = true; break; // Amedo
#endif
            }
        }
        cfg.Resolve();
        return cfg;
    }

    private void Resolve()
    {
        if (string.IsNullOrWhiteSpace(Project)) throw new InvalidOperationException("No project: set \"project\" in game.json or pass --project <folder or .tson>");
        string? tson = Project.EndsWith(".tson", StringComparison.OrdinalIgnoreCase) ? Project
            : Directory.Exists(Project) ? Directory.GetFiles(Project, "*.tson").FirstOrDefault() : null;
        string folder = tson is not null ? Path.GetDirectoryName(tson)! : Project;
        bool newGame = false;
        string? disc = null;
        if (tson is not null)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(tson));
            var root = doc.RootElement;
            if (root.TryGetProperty("DiscContentPathPS2", out var d) && d.ValueKind == JsonValueKind.String) disc = d.GetString();
            if (root.TryGetProperty("IsNewGame", out var n) && n.ValueKind is JsonValueKind.True or JsonValueKind.False) newGame = n.GetBoolean();
        }
        Tson = tson; // Amedo 2026-10-04
        ProjectFolder = folder;
        Disc = disc is { Length: > 0 } && Directory.Exists(disc) ? disc : Path.Combine(folder, "disc", "ps2");
        SavedChunks = Path.Combine(folder, newGame ? "SavedChunks_NewGame" : "SavedChunks");
        var hd = Path.Combine(folder, newGame ? "HD_NewGame" : "HD", "Textures"); // Amedo 2026-10-02
        if (Directory.Exists(hd)) HdTextureDirectory = hd;
        if (Level.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)) Level = Level[..^4];
    }
}
