using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;

namespace CrashEngine.Runtime.Screen;

// Amedo 2026-09-30
public sealed class GameFont
{
    public readonly record struct Glyph(float U, float V, float W, float H, int Page);
    public readonly record struct Quad(float X, float Y, float W, float H, float U, float V0, float UW, float V1, int Page);
    public readonly record struct Page(int Width, int Height, byte[] Rgba)
    {
        public const int BlendMix = 0, BlendAdd = 1; // Amedo
        public int Blend { get; init; } // Amedo
    }
    public readonly record struct Item(float Advance, float DrawX, float DrawW, float Height, float Baseline, float U, float V0, float UW, float V1, int Page);

    // Amedo 2026-10-01
    public interface IFallback
    {
        IReadOnlyList<Page> Pages { get; }
        int Version { get; }
        bool LatinFromAtlas { get; }
        float SpaceAdvance { get; }
        float LetterSpacing { get; }
        bool Shape(string text, int start, int length, bool rightToLeft, List<Item> items);
    }

    public const float GlyphWidth = 0.042f, GlyphHeight = 0.07f;
    public const float PalGlyphWidth = 0.046f, PalGlyphHeight = 0.085f; // Amedo
    public const float AtlasBaseline = 33f * 16f;

    public Vector2 Size_0C { get; init; } = new(GlyphWidth, GlyphHeight); // Amedo
    public int First { get; }
    public Glyph[] Glyphs { get; }
    public Page[] Pages { get; }

    public int PageCount(IFallback? fallback) => Pages.Length + (fallback?.Pages.Count ?? 0);
    public Page PageAt(int index, IFallback? fallback) => index < Pages.Length ? Pages[index] : fallback!.Pages[index - Pages.Length];

    public static bool IsButton(char c) => c is '[' or '\\' or ']' or '^' or '{' or '}' or '<' or '>' or '@';

    private readonly List<(float Width, float Ascent, float Descent, int Start, int Count)> _lines = new();
    private readonly List<Item> _items = new();
    private readonly List<(int Start, int Length, bool Rtl)> _runs = new();
    private readonly List<int> _segments = new();

    public GameFont(PS2PSF psf)
    {
        First = psf.UnkInt;
        Glyphs = psf.UnkVecs.Select(v => new Glyph(v.X, v.Y, v.Z, v.W, (BitConverter.SingleToInt32Bits(v.X) & 3) - 1)).ToArray();
        Pages = psf.FontPages.Select(p => p.Texture is PS2AnyTexture t ? Decode(t) : new Page(1, 1, new byte[4])).ToArray();
    }

    internal static Page Decode(PS2AnyTexture tex) // Amedo 2026-10-01
    {
        tex.CalculateData();
        int w = tex.ImageWidthPower > 0 ? 1 << tex.ImageWidthPower : 1;
        int h = tex.ImageHeightPower > 0 ? 1 << tex.ImageHeightPower : 1;
        var rgba = new byte[w * h * 4];
        for (int i = 0; i < tex.Colors.Count && i < w * h; i++)
        {
            var c = tex.Colors[i];
            rgba[i * 4] = c.R; rgba[i * 4 + 1] = c.G; rgba[i * 4 + 2] = c.B; rgba[i * 4 + 3] = c.A;
        }
        return new Page(w, h, rgba);
    }

    private int Index(int c)
    {
        int i = (c & 0xFF) - First;
        return i < 0 || i >= Glyphs.Length ? 0 : i;
    }

    // Amedo 2026-10-01
    public bool InAtlas(char c)
    {
        int i = c - First;
        if (c == ' ') return true;
        return c < 0x100 && i >= 0 && i < Glyphs.Length && Glyphs[i].Page >= 0;
    }

    public Vector2 BaseScale
    {
        get
        {
            var a = Glyphs[Index('a')];
            return new Vector2(Size_0C.X / a.W, Size_0C.Y / a.H); // Amedo
        }
    }

    private Item AtlasItem(char c)
    {
        var g = Glyphs[Index(c)];
        return new Item(g.W, 0f, g.W, g.H, AtlasBaseline, g.U / 16f, g.V / 16f, g.W / 16f, (g.V - g.H) / 16f, g.Page);
    }

    public void Layout(string text, Vector2 position, Vector2 scale, uint flags, List<Quad> quads, IFallback? fallback = null)
    {
        var s = BaseScale * scale;
        var space = Glyphs[Index(' ')];
        float total = 0f;
        _lines.Clear();
        _items.Clear();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '~')
            {
                if (_lines.Count > 0)
                {
                    var last = _lines[^1];
                    _lines[^1] = (last.Width, last.Ascent, last.Descent + space.H * s.Y, last.Start, last.Count);
                }
                total += space.H * s.Y;
                i++;
                continue;
            }
            int end = text.IndexOf('~', i);
            if (end < 0) end = text.Length;
            int start = _items.Count;
            AddLine(text, i, end - i, fallback);
            float width = 0f, ascent = AtlasBaseline, descent = space.H - AtlasBaseline;
            for (int k = start; k < _items.Count; k++)
            {
                width += _items[k].Advance;
                ascent = MathF.Max(ascent, _items[k].Baseline);
                descent = MathF.Max(descent, _items[k].Height - _items[k].Baseline);
            }
            total += (ascent + descent) * s.Y;
            _lines.Add((width * s.X, ascent * s.Y, descent * s.Y, start, _items.Count - start));
            i = end < text.Length ? end + 1 : end;
        }

        float y = position.Y;
        if ((flags & 1) == 0) y -= (flags & 4) == 0 ? total * 0.5f : total;
        foreach (var (lineWidth, ascent, descent, start, count) in _lines)
        {
            float x = position.X;
            if ((flags & 0x10) == 0) x -= (flags & 0x40) == 0 ? lineWidth * 0.5f : lineWidth;
            for (int k = start; k < start + count; k++)
            {
                var it = _items[k];
                if (it.Page >= 0)
                    quads.Add(new Quad(x + it.DrawX * s.X, y + ascent - it.Baseline * s.Y, it.DrawW * s.X, it.Height * s.Y, it.U, it.V0, it.UW, it.V1, it.Page));
                x += it.Advance * s.X;
            }
            y += ascent + descent;
        }
    }

    // Amedo 2026-10-01
    private void AddLine(string text, int start, int length, IFallback? fallback)
    {
        bool latinFromAtlas = fallback is null || fallback.LatinFromAtlas;
        bool UseAtlas(char c) => latinFromAtlas ? InAtlas(c) : IsButton(c);
        float spacing = fallback?.LetterSpacing ?? 0f;
        bool plain = true;
        for (int k = start; k < start + length && plain; k++) plain = UseAtlas(text[k]);
        if (plain || fallback is null)
        {
            for (int k = start; k < start + length; k++) AddAtlas(text[k], spacing);
            return;
        }
        bool paragraphRtl = Bidi.Runs(text, start, length, _runs);
        for (int r = 0; r < _runs.Count; r++)
        {
            var (runStart, runLength, rtl) = _runs[paragraphRtl ? _runs.Count - 1 - r : r];
            _segments.Clear();
            for (int k = runStart; k < runStart + runLength;)
            {
                bool atlas = UseAtlas(text[k]);
                int e = k + 1;
                while (e < runStart + runLength && UseAtlas(text[e]) == atlas && (atlas || (text[e] == ' ') == (text[k] == ' '))) e++;
                _segments.Add(k);
                _segments.Add(e - k);
                k = e;
            }
            int segs = _segments.Count / 2;
            for (int q = 0; q < segs; q++)
            {
                int si = rtl ? segs - 1 - q : q;
                int segStart = _segments[si * 2], segLength = _segments[si * 2 + 1];
                if (UseAtlas(text[segStart]))
                {
                    for (int k = 0; k < segLength; k++)
                    {
                        char c = text[rtl ? segStart + segLength - 1 - k : segStart + k];
                        if (rtl) c = c == '(' ? ')' : c == ')' ? '(' : c;
                        AddAtlas(c, spacing);
                    }
                }
                else if (text[segStart] == ' ')
                {
                    for (int k = 0; k < segLength; k++)
                        _items.Add(new Item(fallback.SpaceAdvance, 0f, 0f, 0f, AtlasBaseline, 0f, 0f, 0f, 0f, -1));
                }
                else
                {
                    int first = _items.Count;
                    if (fallback.Shape(text, segStart, segLength, rtl, _items))
                    {
                        for (int k = first; k < _items.Count; k++)
                            if (_items[k].Page >= 0) _items[k] = _items[k] with { Page = _items[k].Page + Pages.Length };
                    }
                    else
                    {
                        for (int k = 0; k < segLength; k++) AddAtlas(' ', spacing);
                    }
                }
            }
        }
    }

    // Amedo 2026-10-01
    private void AddAtlas(char c, float spacing)
    {
        var item = AtlasItem(c);
        _items.Add(spacing == 0f ? item : item with { Advance = item.Advance + spacing });
    }
}
