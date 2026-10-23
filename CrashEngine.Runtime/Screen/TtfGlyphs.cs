using System.Numerics;
using HarfBuzzSharp;
using StbTrueTypeSharp;
using HbBuffer = HarfBuzzSharp.Buffer;

namespace CrashEngine.Runtime.Screen;

// Amedo 2026-10-01
public sealed unsafe class TtfGlyphs : GameFont.IFallback, IDisposable
{
    public const int Oversample = 2, PageSize = 1024;
    public const float Baseline = 33f, CapHeight = 29f;

    private readonly StbTrueType.stbtt_fontinfo _stb;
    private readonly Blob _blob;
    private readonly Face _face;
    private readonly Font _font;
    private readonly HbBuffer _buffer = new();
    private readonly TextStyle _style;
    private readonly float _pixelScale;
    private readonly List<GameFont.Page> _pages = new();
    private readonly Dictionary<(string Text, bool Rtl), GameFont.Item> _cache = new();
    private int _shelfX, _shelfY, _shelfHeight;

    public string Name { get; }
    public IReadOnlyList<GameFont.Page> Pages => _pages;
    public int Version { get; private set; }
    public bool LatinFromAtlas => _style.GameFontForLatin;
    public float SpaceAdvance { get; }
    public float LetterSpacing => _style.LetterSpacing * 16f;

    private TtfGlyphs(string path, TextStyle style)
    {
        Name = Path.GetFileName(path);
        _style = style.Clone();
        _stb = StbTrueType.CreateFont(File.ReadAllBytes(path), 0) ?? throw new InvalidDataException("not a TrueType font");
        _blob = Blob.FromFile(path);
        _face = new Face(_blob, 0);
        _font = new Font(_face);
        _font.SetScale(_face.UnitsPerEm, _face.UnitsPerEm);
        int reference = StbTrueType.stbtt_FindGlyphIndex(_stb, 0x0627);
        if (reference == 0) reference = StbTrueType.stbtt_FindGlyphIndex(_stb, 'H');
        int x0, y0, x1, y1;
        float top = 0f;
        if (reference != 0 && StbTrueType.stbtt_GetGlyphBox(_stb, reference, &x0, &y0, &x1, &y1) != 0) top = y1;
        if (top <= 0f)
        {
            int ascent, descent, gap;
            StbTrueType.stbtt_GetFontVMetrics(_stb, &ascent, &descent, &gap);
            top = ascent * 0.7f;
        }
        _pixelScale = CapHeight * Math.Clamp(_style.Size, 0.1f, 8f) / top * Oversample;
        int space = StbTrueType.stbtt_FindGlyphIndex(_stb, ' ');
        int advance = 0, bearing = 0;
        if (space != 0) StbTrueType.stbtt_GetGlyphHMetrics(_stb, space, &advance, &bearing);
        SpaceAdvance = (space != 0 ? advance : top * 0.3f) * _pixelScale / Oversample * 16f * _style.WordSpacing;
    }

    public static TtfGlyphs? Load(string path, TextStyle? style, Action<string>? log)
    {
        try
        {
            return new TtfGlyphs(path, style ?? new TextStyle());
        }
        catch (Exception ex)
        {
            log?.Invoke($"[Screen] font {path} failed: {ex.Message}");
            return null;
        }
    }

    public bool Shape(string text, int start, int length, bool rightToLeft, List<GameFont.Item> items)
    {
        var key = (text.Substring(start, length), rightToLeft);
        if (!_cache.TryGetValue(key, out var item))
        {
            if (Build(key.Item1, rightToLeft) is not { } built) return false;
            _cache[key] = item = built;
        }
        items.Add(item);
        return true;
    }

    private GameFont.Item? Build(string text, bool rightToLeft)
    {
        _buffer.ClearContents();
        _buffer.AddUtf16(text);
        _buffer.GuessSegmentProperties();
        _buffer.Direction = rightToLeft ? Direction.RightToLeft : Direction.LeftToRight;
        _font.Shape(_buffer);
        var infos = _buffer.GlyphInfos;
        var positions = _buffer.GlyphPositions;
        if (infos.Length == 0) return null;

        float spacing = rightToLeft ? 0f : _style.LetterSpacing * Oversample / _pixelScale;
        float pen = 0f;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        var boxes = new (int Glyph, int X, int Y, int W, int H)[infos.Length];
        for (int i = 0; i < infos.Length; i++)
        {
            int glyph = (int)infos[i].Codepoint;
            float gx = (pen + positions[i].XOffset) * _pixelScale, gy = -positions[i].YOffset * _pixelScale;
            int bx0, by0, bx1, by1;
            StbTrueType.stbtt_GetGlyphBitmapBox(_stb, glyph, _pixelScale, _pixelScale, &bx0, &by0, &bx1, &by1);
            int ox = (int)MathF.Round(gx) + bx0, oy = (int)MathF.Round(gy) + by0;
            boxes[i] = (glyph, ox, oy, bx1 - bx0, by1 - by0);
            if (bx1 > bx0 && by1 > by0)
            {
                minX = Math.Min(minX, ox); minY = Math.Min(minY, oy);
                maxX = Math.Max(maxX, ox + bx1 - bx0); maxY = Math.Max(maxY, oy + by1 - by0);
            }
            pen += positions[i].XAdvance + (i + 1 < infos.Length ? spacing : 0f);
        }
        float advance = pen * _pixelScale;
        if (minX > maxX) { minX = 0; maxX = 1; minY = -1; maxY = 0; }

        float reach = MathF.Max(_style.Outline ? _style.OutlineWidth : 0f,
            _style.Shadow ? _style.ShadowWidth + MathF.Max(MathF.Abs(_style.ShadowX), MathF.Abs(_style.ShadowY)) + _style.ShadowSoftness : 0f);
        int pad = (int)MathF.Ceiling((reach + 1.5f) * Oversample);
        int baseline = Math.Max((int)(Baseline * Math.Clamp(_style.Size, 0.1f, 8f) * Oversample), -minY + pad);
        int w = maxX - minX + pad * 2, h = baseline + maxY + pad;
        var coverage = new float[w * h];
        var bitmap = Array.Empty<byte>();
        foreach (var (glyph, x, y, gw, gh) in boxes)
        {
            if (gw <= 0 || gh <= 0) continue;
            if (bitmap.Length < gw * gh) bitmap = new byte[gw * gh];
            fixed (byte* p = bitmap) StbTrueType.stbtt_MakeGlyphBitmap(_stb, p, gw, gh, gw, _pixelScale, _pixelScale, glyph);
            int dx = x - minX + pad, dy = y + baseline;
            for (int r = 0; r < gh; r++)
                for (int c = 0; c < gw; c++)
                {
                    int tx = dx + c, ty = dy + r;
                    if (tx < 0 || ty < 0 || tx >= w || ty >= h) continue;
                    ref float v = ref coverage[ty * w + tx];
                    v = MathF.Max(v, bitmap[r * gw + c] / 255f);
                }
        }

        var rgba = Style(coverage, w, h, baseline);
        var (page, px, py) = Place(w, h);
        var dst = _pages[page].Rgba;
        int copy = Math.Min(w, PageSize - px);
        for (int r = 0; r < h && py + r < PageSize; r++)
            Array.Copy(rgba, r * w * 4, dst, ((py + r) * PageSize + px) * 4, copy * 4);
        Version++;

        const float units = 16f / Oversample;
        return new GameFont.Item(advance * units, (minX - pad) * units, w * units, h * units, baseline * units, px, py, w, py + h, page);
    }

    private static float[] Dilate(float[] mask, int w, int h, float radius)
    {
        if (radius <= 0f) return (float[])mask.Clone();
        int reach = (int)MathF.Ceiling(radius) + 1;
        var result = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float best = 0f;
                for (int dy = -reach; dy <= reach; dy++)
                {
                    int sy = y + dy;
                    if (sy < 0 || sy >= h) continue;
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int sx = x + dx;
                        if (sx < 0 || sx >= w) continue;
                        float v = mask[sy * w + sx];
                        if (v <= best) continue;
                        float weight = Math.Clamp(radius + 0.5f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                        best = MathF.Max(best, v * weight);
                    }
                }
                result[y * w + x] = best;
            }
        return result;
    }

    private static float[] ShiftBlur(float[] mask, int w, int h, int ox, int oy, int blur)
    {
        var result = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                int n = 0;
                for (int dy = -blur; dy <= blur; dy++)
                    for (int dx = -blur; dx <= blur; dx++)
                    {
                        int sx = x - ox + dx, sy = y - oy + dy;
                        if (sx >= 0 && sy >= 0 && sx < w && sy < h) sum += mask[sy * w + sx];
                        n++;
                    }
                result[y * w + x] = sum / n;
            }
        return result;
    }

    private byte[] Style(float[] fill, int w, int h, int baseline)
    {
        var s = _style;
        var rim = s.Outline ? Dilate(fill, w, h, s.OutlineWidth * Oversample) : null;
        var shadow = s.Shadow
            ? ShiftBlur(Dilate(fill, w, h, s.ShadowWidth * Oversample), w, h,
                (int)MathF.Round(s.ShadowX * Oversample), (int)MathF.Round(s.ShadowY * Oversample), (int)MathF.Round(s.ShadowSoftness * Oversample))
            : null;
        Vector3 top = TextStyle.Colour(s.FillTop), bottom = TextStyle.Colour(s.FillBottom);
        Vector3 rimColour = TextStyle.Colour(s.OutlineColour), shadowColour = TextStyle.Colour(s.ShadowColour);
        float size = Math.Clamp(s.Size, 0.1f, 8f);
        float capSpan = CapHeight * size * Oversample, capTop = baseline - capSpan;
        float g0 = capTop + Math.Clamp(s.GradientStart, 0f, 1f) * capSpan, g1 = capTop + Math.Clamp(s.GradientEnd, 0f, 1f) * capSpan;
        var rgba = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            float t = g1 > g0 ? Math.Clamp((y - g0) / (g1 - g0), 0f, 1f) : y < g0 ? 0f : 1f;
            var fillColour = Vector3.Lerp(top, bottom, t);
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                var c = Vector3.Zero;
                float a = 0f;
                if (shadow is not null) Over(ref c, ref a, shadowColour, shadow[i] * Math.Clamp(s.ShadowOpacity, 0f, 1f));
                if (rim is not null) Over(ref c, ref a, rimColour, rim[i]);
                Over(ref c, ref a, fillColour, fill[i]);
                if (a <= 0f) continue;
                rgba[i * 4] = (byte)(c.X / a * 255f + 0.5f);
                rgba[i * 4 + 1] = (byte)(c.Y / a * 255f + 0.5f);
                rgba[i * 4 + 2] = (byte)(c.Z / a * 255f + 0.5f);
                rgba[i * 4 + 3] = (byte)(a * 255f + 0.5f);
            }
        }
        return rgba;
    }

    private static void Over(ref Vector3 c, ref float a, Vector3 colour, float alpha)
    {
        c = colour * alpha + c * (1f - alpha);
        a = alpha + a * (1f - alpha);
    }

    private (int Page, int X, int Y) Place(int w, int h)
    {
        w = Math.Min(w, PageSize);
        if (_pages.Count == 0 || _shelfX + w > PageSize)
        {
            _shelfY += _shelfHeight;
            _shelfX = 0;
            _shelfHeight = 0;
        }
        if (_pages.Count == 0 || _shelfY + h > PageSize)
        {
            _pages.Add(new GameFont.Page(PageSize, PageSize, new byte[PageSize * PageSize * 4]));
            _shelfX = _shelfY = _shelfHeight = 0;
        }
        var spot = (_pages.Count - 1, _shelfX, _shelfY);
        _shelfX += w + 1;
        _shelfHeight = Math.Max(_shelfHeight, h + 1);
        return spot;
    }

    public void Dispose()
    {
        _buffer.Dispose();
        _font.Dispose();
        _face.Dispose();
        _blob.Dispose();
        _stb.Dispose();
    }
}
