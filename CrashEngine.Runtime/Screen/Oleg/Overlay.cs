using System.Numerics;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public interface IShape2D
{
    void Draw(OverlayQueue overlay, Matrix3x2? matrix, uint? colour);
}

// Amedo
public sealed class OverlayQueue
{
    public const int LayerCount = 6;

    private struct QueuedShape
    {
        public Matrix3x2? Matrix;
        public IShape2D Shape;
        public uint? Colour;
    }

    public const uint PlainColour_2E6FE4 = 0x80C0C0C0u;

    private struct QueuedText
    {
        public Vector2 Position, Scale;
        public uint Colour, Flags;
        public string Text;
        public object Owner;
        public GameFont.IFallback? Fallback;
    }

    private readonly List<QueuedShape>[] _layers_38 = Enumerable.Range(0, LayerCount).Select(_ => new List<QueuedShape>()).ToArray();
    private readonly List<(GameFont Font, List<QueuedText> Texts)> _texts_18 = new();

    public uint Colour_10 = 0x80C0C0C0u;
    public GameFont? Font_28;
    public Vector2 Scale_2C = Vector2.One;
    public uint Flags_34 = 0x22;
    public string? TextStyle;
    public Func<string?, GameFont.IFallback?>? FallbackFor;
    public object? TextOwner;
    public static float ScreenAspect = 1f;
    public static bool Widescreen_30A744 = true; // Amedo

    public readonly List<ScreenItems.Item> Items = new();
    private readonly List<ScreenItems.Item> _immediate = new(); // Amedo
    private bool _flushing; // Amedo
    private readonly ScreenItems _layouts = new();

    // Amedo
    public void Begin(float aspect)
    {
        ScreenAspect = aspect;
        Items.Clear();
        _immediate.Clear(); // Amedo
        foreach (var l in _layers_38) l.Clear();
        foreach (var (_, texts) in _texts_18) texts.Clear();
    }

    // Amedo
    public static Vector2 FitPlaceToScreen_19FFA0(Vector2 anchor, Vector2 p) => new((p.X - anchor.X) / ScreenAspect + anchor.X, p.Y);

    // Amedo
    public static Vector2 FitSizeToScreen_1A0030(Vector2 size) => new(size.X / ScreenAspect, size.Y);

    // Amedo
    public void QueuePlacedShape_1A0510(Matrix3x2 matrix, IShape2D? shape, int layer)
    {
        if (shape is null) return;
        _layers_38[Math.Clamp(layer, 0, LayerCount - 1)].Add(new QueuedShape { Matrix = matrix, Shape = shape, Colour = Colour_10 == PlainColour_2E6FE4 ? null : Colour_10 });
    }

    // Amedo
    public void QueueShape_1A0450(IShape2D? shape, int layer)
    {
        if (shape is null) return;
        _layers_38[Math.Clamp(layer, 0, LayerCount - 1)].Add(new QueuedShape { Matrix = null, Shape = shape, Colour = Colour_10 == PlainColour_2E6FE4 ? null : Colour_10 });
    }

    // Amedo
    public void QueueText_1A03A0(string? text, Vector2 position)
    {
        if (string.IsNullOrEmpty(text) || Font_28 is null) return;
        var list = TextQueueGet_1AC268(Font_28);
        list.Add(new QueuedText { Text = text, Position = position, Scale = Scale_2C, Colour = Colour_10, Flags = Flags_34, Owner = TextOwner ?? text, Fallback = FallbackFor?.Invoke(TextStyle) });
    }

    // Amedo
    private List<QueuedText> TextQueueGet_1AC268(GameFont font)
    {
        foreach (var (f, texts) in _texts_18)
            if (ReferenceEquals(f, font)) return texts;
        var created = new List<QueuedText>();
        _texts_18.Add((font, created));
        return created;
    }

    // Amedo
    public void DrawOverlay_19B120()
    {
        _flushing = true;
        foreach (var layer in _layers_38)
            foreach (var s in layer) s.Shape.Draw(this, s.Matrix, s.Colour);
        foreach (var (font, texts) in _texts_18)
            foreach (var t in texts) DrawFontText_19D138(font, t);
        _flushing = false;
        QueuedCount = Items.Count; // Amedo
        Items.AddRange(_immediate);
        _immediate.Clear();
    }

    public int QueuedCount { get; private set; } // Amedo

    private List<ScreenItems.Item> Target => _flushing ? Items : _immediate; // Amedo

    private void DrawFontText_19D138(GameFont font, QueuedText t)
    {
        var colour = Vector4.Clamp(Colour.ToVector(t.Colour), Vector4.Zero, Vector4.One);
        if (colour.W <= 0f) return;
        foreach (var q in _layouts.Layout(t.Owner, font, t.Fallback, t.Text, t.Position, t.Scale, t.Flags))
        {
            var page = font.PageAt(q.Page, t.Fallback);
            float u0 = q.U / page.Width, u1 = (q.U + q.UW) / page.Width, v0 = q.V0 / page.Height, v1 = q.V1 / page.Height;
            Items.Add(new ScreenItems.Item
            {
                Kind = ScreenItems.ItemKind.Glyph, Page = q.Page, Fallback = t.Fallback, Colour = colour,
                P0 = new(q.X, q.Y), P1 = new(q.X + q.W, q.Y), P2 = new(q.X + q.W, q.Y + q.H), P3 = new(q.X, q.Y + q.H),
                Uv0 = new(u0, v0), Uv1 = new(u1, v0), Uv2 = new(u1, v1), Uv3 = new(u0, v1),
            });
        }
    }

    // Amedo
    public void AddTriangle(Vector2 p0, Vector2 p1, Vector2 p2, uint c0, uint c1, uint c2)
    {
        var a = Vector4.Clamp(Colour.ToVector(c0), Vector4.Zero, Vector4.One);
        var b = Vector4.Clamp(Colour.ToVector(c1), Vector4.Zero, Vector4.One);
        var c = Vector4.Clamp(Colour.ToVector(c2), Vector4.Zero, Vector4.One);
        if (a.W <= 0f && b.W <= 0f && c.W <= 0f) return;
        Target.Add(new ScreenItems.Item { Kind = ScreenItems.ItemKind.Triangle, P0 = p0, P1 = p1, P2 = p2, P3 = p2, Colour = a, Colour1 = b, Colour2 = c });
    }

    // Amedo
    public void AddQuad(GameFont.Page? image, in Vector2 p0, in Vector2 p1, in Vector2 p2, in Vector2 p3, in Vector2 uv0, in Vector2 uv1, in Vector2 uv2, in Vector2 uv3, uint colour)
    {
        var c = Vector4.Clamp(Colour.ToVector(colour), Vector4.Zero, Vector4.One);
        if (c.W <= 0f) return;
        Target.Add(new ScreenItems.Item
        {
            Kind = image is null ? ScreenItems.ItemKind.Rect : ScreenItems.ItemKind.Sprite, Image = image, Colour = c,
            P0 = p0, P1 = p1, P2 = p2, P3 = p3, Uv0 = uv0, Uv1 = uv1, Uv2 = uv2, Uv3 = uv3,
        });
    }

    public void ClearLayouts() => _layouts.Clear();
}
