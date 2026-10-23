using System.Numerics;

namespace CrashEngine.Runtime.Screen;

// Amedo 2026-10-01
public sealed class ScreenItems
{
    public enum ItemKind { Rect, Glyph, Sprite, Triangle }

    public struct Item
    {
        public ItemKind Kind;
        public int Page;
        public GameFont.IFallback? Fallback;
        public GameFont.Page? Image; // Amedo
        public Vector2 P0, P1, P2, P3;
        public Vector2 Uv0, Uv1, Uv2, Uv3;
        public Vector4 Colour;
        public Vector4 Colour1, Colour2; // Amedo
    }

    private sealed class Cached
    {
        public string Text = "";
        public GameFont.IFallback? Fallback;
        public Vector2 Position, Scale;
        public uint Flags;
        public readonly List<GameFont.Quad> Quads = new();
    }

    private readonly Dictionary<object, Cached> _layouts = new(ReferenceEqualityComparer.Instance);

    // Amedo
    public List<GameFont.Quad> Layout(object owner, GameFont font, GameFont.IFallback? fallback, string text, Vector2 position, Vector2 scale, uint flags)
    {
        if (!_layouts.TryGetValue(owner, out var cached)) _layouts[owner] = cached = new Cached();
        if (!string.Equals(cached.Text, text, StringComparison.Ordinal) || !ReferenceEquals(cached.Fallback, fallback) ||
            cached.Position != position || cached.Scale != scale || cached.Flags != flags)
        {
            cached.Text = text;
            cached.Fallback = fallback;
            cached.Position = position;
            cached.Scale = scale;
            cached.Flags = flags;
            cached.Quads.Clear();
            font.Layout(text, position, scale, flags, cached.Quads, fallback);
        }
        return cached.Quads;
    }

    public void Clear() => _layouts.Clear();
}
