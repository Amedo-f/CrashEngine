using System.Numerics;
using System.Text.Json;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-10-26
public sealed record TouchControl(string Id, string Pad, string Image, string Anchor, float X, float Y, float W, float H, string? Press = null, string? Knob = null, bool Round = false)
{
    public bool IsStick => Pad is "LeftStick" or "RightStick";

    public (Vector2 Center, Vector2 Size) Rect(float screenW, float screenH)
    {
        var size = new Vector2(W, H) * screenH;
        float x = X * screenH, y = Y * screenH;
        float cx = Anchor switch
        {
            "top-right" or "bottom-right" => screenW - x,
            "top-center" or "bottom-center" => screenW * 0.5f + x,
            _ => x,
        };
        float cy = Anchor.StartsWith("bottom") ? screenH - y : y;
        return (new Vector2(cx, cy), size);
    }

    public bool Hit(Vector2 p, float screenW, float screenH)
    {
        var (c, s) = Rect(screenW, screenH);
        if (IsStick || Round) return GameMath.Distance(p, c) <= s.X * 0.5f;
        return MathF.Abs(p.X - c.X) <= s.X * 0.5f && MathF.Abs(p.Y - c.Y) <= s.Y * 0.5f;
    }
}

// Amedo 2026-10-26
public sealed record TouchLayout(int Version, float Opacity, List<TouchControl> Controls)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static TouchLayout Load(string path) =>
        JsonSerializer.Deserialize<TouchLayout>(File.ReadAllText(path), Json) ?? throw new InvalidDataException(path);

    // Amedo 2026-10-27
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
}

