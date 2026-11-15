using System.Text.Json;

namespace CrashEngineHub;

// Amedo 2026-11-12
public sealed class PlayState
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrashEngine");
    public static string ModsFolder => Path.Combine(Root, "mods");
    private static string StateFile => Path.Combine(Root, "hub_play.json");

    public string Iso { get; set; } = "";
    public string? DiscId { get; set; }
    public string InstallFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Games");
    public string ShortcutFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public string? Language { get; set; }
    public string? Applied { get; set; }

    public static PlayState Load()
    {
        try { return File.Exists(StateFile) ? JsonSerializer.Deserialize<PlayState>(File.ReadAllText(StateFile)) ?? new PlayState() : new PlayState(); }
        catch (JsonException) { return new PlayState(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
