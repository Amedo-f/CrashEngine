using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrashEngine.Runtime.Screen;

// Amedo 2026-10-01
public sealed class TextStyle
{
    public string? Font { get; set; }
    public bool GameFontForLatin { get; set; } = true;
    public float Size { get; set; } = 1f;
    public float LetterSpacing { get; set; }
    public float WordSpacing { get; set; } = 1f;
    public string FillTop { get; set; } = "#FEAC00";
    public string FillBottom { get; set; } = "#FE0400";
    public float GradientStart { get; set; }
    public float GradientEnd { get; set; } = 1f;
    public bool Outline { get; set; } = true;
    public string OutlineColour { get; set; } = "#E8E8EA";
    public float OutlineWidth { get; set; } = 1.5f;
    public bool Shadow { get; set; } = true;
    public string ShadowColour { get; set; } = "#000000";
    public float ShadowOpacity { get; set; } = 0.9f;
    public float ShadowX { get; set; } = 2f;
    public float ShadowY { get; set; } = 2.5f;
    public float ShadowWidth { get; set; } = 1.5f;
    public float ShadowSoftness { get; set; } = 0.5f;

    public TextStyle Clone() => (TextStyle)MemberwiseClone();

    [JsonIgnore]
    public string Key => JsonSerializer.Serialize(this);

    public static Vector3 Colour(string? hex)
    {
        if (hex is null) return Vector3.One;
        var s = hex.TrimStart('#');
        if (s.Length != 6 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return Vector3.One;
        return new Vector3((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF) / 255f;
    }

    public static string Hex(Vector3 c)
    {
        static int B(float f) => Math.Clamp((int)MathF.Round(f * 255f), 0, 255);
        return $"#{B(c.X):X2}{B(c.Y):X2}{B(c.Z):X2}";
    }
}

// Amedo 2026-10-01
public sealed class TextStyles
{
    public const string FileName = "style.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public TextStyle Default { get; set; } = new();
    public Dictionary<string, TextStyle> Objects { get; set; } = new();

    public TextStyle For(string? objectKey) =>
        objectKey is not null && Objects.TryGetValue(objectKey, out var own) ? own : Default;

    public static string ObjectKey(string chunk, uint instanceId) => $"{Streaming.StreamLink.Normalize(chunk)}#{instanceId}";

    public static TextStyles Load(string? directory, Action<string>? log = null)
    {
        var file = directory is null ? null : Path.Combine(directory, FileName);
        if (file is null || !File.Exists(file)) return new TextStyles();
        try
        {
            return JsonSerializer.Deserialize<TextStyles>(File.ReadAllText(file)) ?? new TextStyles();
        }
        catch (Exception ex)
        {
            log?.Invoke($"[Screen] {file} unreadable ({ex.Message}); using the game style");
            return new TextStyles();
        }
    }

    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, FileName), JsonSerializer.Serialize(this, Options));
    }
}
