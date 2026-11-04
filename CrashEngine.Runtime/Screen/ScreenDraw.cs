using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Renderer;
using ImGuiNET;

namespace CrashEngine.Runtime.Screen;

// Amedo 2026-09-30
public sealed class ScreenDraw : IDisposable
{
    private readonly List<Texture2D> _atlas = new();
    private GameFont? _atlasFont;
    private readonly Dictionary<GameFont.IFallback, (int Version, List<Texture2D> Pages)> _dynamic = new(ReferenceEqualityComparer.Instance); // Amedo 2026-10-01
    private readonly Dictionary<object, Cached> _layouts = new(ReferenceEqualityComparer.Instance);

    private sealed class Cached
    {
        public string Text = "";
        public GameFont.IFallback? Fallback;
        public Vector2 Position, Scale;
        public uint Flags;
        public readonly List<GameFont.Quad> Quads = new();
    }

    private readonly Oleg.OverlayQueue _overlay = new(); // Amedo
    private GameScreen? _fallbackScreen;
    private List<ScreenItems.Item> _current = new(), _previous = new(); // Amedo
    private readonly List<ScreenItems.Item> _shown = new();
    private long _drawnStep = long.MinValue;
    private float _drawnAspect;
    private bool _havePrevious;
    private int _currentQueued, _previousQueued; // Amedo
    private const float MaxStepMove = 0.2f; // Amedo

    // Amedo
    public List<ScreenItems.Item> Prepare(GameScreen screen, float aspect, long logicStep = -1, float alpha = 1f)
    {
        if (screen.Oleg is not { } oleg) return _shown;
        if (!ReferenceEquals(_fallbackScreen, screen))
        {
            _fallbackScreen = screen;
            _overlay.FallbackFor = screen.FallbackFor;
        }
        if (logicStep < 0 || logicStep != _drawnStep || aspect != _drawnAspect)
        {
            _havePrevious = logicStep >= 0 && logicStep == _drawnStep + 1 && aspect == _drawnAspect;
            (_previous, _current) = (_current, _previous);
            _overlay.Begin(aspect);
            if (oleg.Game_3095C8 is { } game) game.Draw_177908(_overlay); // Amedo
            else oleg.Draw_17A0B8(_overlay);
            _overlay.DrawOverlay_19B120();
            _current.Clear();
            _current.AddRange(_overlay.Items);
            _previousQueued = _currentQueued; // Amedo
            _currentQueued = _overlay.QueuedCount;
            _drawnStep = logicStep;
            _drawnAspect = aspect;
        }
        return Interpolated(alpha);
    }

    // Amedo
    public void Draw(GameScreen screen, ImDrawListPtr list, Vector2 origin, Vector2 size, long logicStep = -1, float alpha = 1f, Vector2? screenOffset = null, bool bars = false)
    {
        if (size.X <= 0f || size.Y <= 0f || screen.Oleg is null) return;
        if (screenOffset is { } offset) // Amedo
        {
            var (inner, innerSize) = ScreenFit.Rect(origin, size, offset);
            if (bars) Bars(list, origin, size, inner, innerSize);
            origin = inner;
            size = innerSize;
        }
        var items = Prepare(screen, size.X / size.Y * 0.75f, logicStep, alpha); // Amedo
        float styleAlpha = ImGui.GetStyle().Alpha;
        if (styleAlpha != _coloursAlpha)
        {
            _colours.Clear();
            _haveLastColour = false;
            _coloursAlpha = styleAlpha;
        }
        Vector2 white = default;
        bool haveWhite = false;
        bool add = false; // Amedo
        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(items);
        for (int index = 0; index < span.Length; index++)
        {
            ref readonly var item = ref span[index];
            bool itemAdd = item.Kind == ScreenItems.ItemKind.Sprite && item.Image is { Blend: GameFont.Page.BlendAdd }; // Amedo
            if (itemAdd != add)
            {
                if (itemAdd) ImGuiBlend.Add(list);
                else ImGuiBlend.Mix(list);
                add = itemAdd;
            }
            Vector2 P(Vector2 p) => origin + p * size;
            if (item.Kind == ScreenItems.ItemKind.Triangle) // Amedo
            {
                if (!haveWhite)
                {
                    white = ImGui.GetFontTexUvWhitePixel();
                    haveWhite = true;
                }
                int run = 1;
                while (index + run < span.Length && span[index + run].Kind == ScreenItems.ItemKind.Triangle) run++;
                index = WriteTriangles(list, span, index, run, origin, size, white) - 1;
                continue;
            }
            uint tint = Colour(item.Colour);
            switch (item.Kind)
            {
                case ScreenItems.ItemKind.Rect:
                    list.AddQuadFilled(P(item.P0), P(item.P1), P(item.P2), P(item.P3), tint); // Amedo
                    break;
                case ScreenItems.ItemKind.Glyph:
                    if (screen.Font is { } font && GlyphPage(font, item.Fallback, item.Page) is { } page)
                        list.AddImage((nint)page.GlId, P(item.P0), P(item.P2), item.Uv0, item.Uv2, tint);
                    break;
                case ScreenItems.ItemKind.Sprite:
                    if (item.Image is { } image) // Amedo
                    {
                        var texture = ImageTexture(image);
                        if (WriteQuadsByVertex) AddImageQuadByVertex(list, (nint)texture.GlId, P(item.P0), P(item.P1), P(item.P2), P(item.P3), item.Uv0, item.Uv1, item.Uv2, item.Uv3, tint); // Amedo 2026-10-26
                        else list.AddImageQuad((nint)texture.GlId, P(item.P0), P(item.P1), P(item.P2), P(item.P3), item.Uv0, item.Uv1, item.Uv2, item.Uv3, tint);
                    }
                    break;
            }
        }
        if (add) ImGuiBlend.Mix(list); // Amedo
    }

    private readonly Dictionary<Vector4, uint> _colours = new();
    private float _coloursAlpha = -1f;
    private Vector4 _lastColour;
    private bool _haveLastColour;
    private uint _lastColourU32;

    private uint Colour(in Vector4 c)
    {
        if (_haveLastColour && c.Equals(_lastColour)) return _lastColourU32;
        if (!_colours.TryGetValue(c, out uint u))
        {
            if (_colours.Count >= 4096) _colours.Clear();
            u = ImGui.GetColorU32(c);
            _colours[c] = u;
        }
        _lastColour = c;
        _haveLastColour = true;
        _lastColourU32 = u;
        return u;
    }

    private unsafe int WriteTriangles(ImDrawListPtr list, ReadOnlySpan<ScreenItems.Item> items, int first, int count, Vector2 origin, Vector2 size, Vector2 white)
    {
        int end = first + count;
        if (list._VtxCurrentIdx + (uint)(count * 3) >= 1u << 16)
        {
            for (int i = first; i < end; i++)
            {
                ref readonly var t = ref items[i];
                list.PrimReserve(3, 3);
                list.PrimVtx(origin + t.P0 * size, white, Colour(t.Colour));
                list.PrimVtx(origin + t.P1 * size, white, Colour(t.Colour1));
                list.PrimVtx(origin + t.P2 * size, white, Colour(t.Colour2));
            }
            return end;
        }
        list.PrimReserve(count * 3, count * 3);
        var native = list.NativePtr;
        ImDrawVert* vtx = native->_VtxWritePtr;
        ushort* idx = native->_IdxWritePtr;
        uint next = native->_VtxCurrentIdx;
        for (int i = first; i < end; i++)
        {
            ref readonly var t = ref items[i];
            *idx++ = (ushort)next++;
            *vtx++ = new ImDrawVert { pos = origin + t.P0 * size, uv = white, col = Colour(t.Colour) };
            *idx++ = (ushort)next++;
            *vtx++ = new ImDrawVert { pos = origin + t.P1 * size, uv = white, col = Colour(t.Colour1) };
            *idx++ = (ushort)next++;
            *vtx++ = new ImDrawVert { pos = origin + t.P2 * size, uv = white, col = Colour(t.Colour2) };
        }
        native->_VtxWritePtr = vtx;
        native->_IdxWritePtr = idx;
        native->_VtxCurrentIdx = next;
        return end;
    }

    public static bool WriteQuadsByVertex { get; set; } // Amedo 2026-10-26

    // Amedo 2026-10-26
    private static void AddImageQuadByVertex(ImDrawListPtr list, nint texture, in Vector2 p0, in Vector2 p1, in Vector2 p2, in Vector2 p3, in Vector2 uv0, in Vector2 uv1, in Vector2 uv2, in Vector2 uv3, uint colour)
    {
        list.PushTextureID(texture);
        list.PrimReserve(6, 4);
        ushort first = (ushort)list._VtxCurrentIdx;
        list.PrimWriteIdx(first);
        list.PrimWriteIdx((ushort)(first + 1));
        list.PrimWriteIdx((ushort)(first + 2));
        list.PrimWriteIdx(first);
        list.PrimWriteIdx((ushort)(first + 2));
        list.PrimWriteIdx((ushort)(first + 3));
        list.PrimWriteVtx(p0, uv0, colour);
        list.PrimWriteVtx(p1, uv1, colour);
        list.PrimWriteVtx(p2, uv2, colour);
        list.PrimWriteVtx(p3, uv3, colour);
        list.PopTextureID();
    }

    private Texture2D? _movie; // Amedo
    private int _movieVersion = -1;

    // Amedo
    public static void ClearFrame_19F1C8(ImDrawListPtr list, Vector2 origin, Vector2 size) => list.AddRectFilled(origin, origin + size, 0xFF000000u);

    // Amedo
    public void DrawMovie_2AEB30(ImDrawListPtr list, Vector2 origin, Vector2 size, Vector2 screenOffset, byte[] rgba, int width, int height, int version, bool wide, Vector2 frame)
    {
        if (size.X <= 0f || size.Y <= 0f || width <= 0 || height <= 0) return;
        if (_movie is null || _movie.Width != (uint)width || _movie.Height != (uint)height)
        {
            _movie?.Dispose();
            _movie = new Texture2D(Engine.Instance.GL, rgba, (uint)width, (uint)height);
            _movie.ClampToEdge(); // Amedo
            _movieVersion = version;
            _scaledVersion = -1;
        }
        else if (version != _movieVersion)
        {
            _movie.UpdateData(rgba, (uint)width, (uint)height);
            _movieVersion = version;
        }
        var (inner, innerSize) = ScreenFit.Rect(origin, size, screenOffset);
        var (min, extent) = Video.MoviePlayer.Rect_2AEB30(Oleg.OverlayQueue.Widescreen_30A744, wide, frame);
        var p0 = inner + min * innerSize;
        var shown = extent * innerSize;
        var pixels = shown * ImGui.GetIO().DisplayFramebufferScale; // Amedo
        int tw = (int)MathF.Round(pixels.X), th = (int)MathF.Round(pixels.Y);
        uint texture = _movie.GlId;
        if (tw > width || th > height)
        {
            _scaler ??= new MovieScaler(Engine.Instance.GL);
            if (_scaledVersion != _movieVersion || _scaledWidth != tw || _scaledHeight != th || _scaler.Output == 0)
            {
                _scaler.Scale(_movie.GlId, width, height, tw, th);
                _scaledVersion = _movieVersion;
                _scaledWidth = tw;
                _scaledHeight = th;
            }
            if (_scaler.Output != 0) texture = _scaler.Output;
        }
        list.AddImage((nint)texture, p0, p0 + shown);
    }

    private MovieScaler? _scaler; // Amedo
    private int _scaledVersion = -1, _scaledWidth, _scaledHeight;

    // Amedo
    private static void Bars(ImDrawListPtr list, Vector2 origin, Vector2 size, Vector2 inner, Vector2 innerSize)
    {
        const uint black = 0xFF000000u;
        var end = origin + size;
        var innerEnd = inner + innerSize;
        if (origin.Y < inner.Y) list.AddRectFilled(origin, new Vector2(end.X, inner.Y), black);
        if (innerEnd.Y < end.Y) list.AddRectFilled(new Vector2(origin.X, innerEnd.Y), end, black);
        if (origin.X < inner.X) list.AddRectFilled(new Vector2(origin.X, inner.Y), new Vector2(inner.X, innerEnd.Y), black);
        if (innerEnd.X < end.X) list.AddRectFilled(new Vector2(innerEnd.X, inner.Y), new Vector2(end.X, innerEnd.Y), black);
    }

    // Amedo
    private List<ScreenItems.Item> Interpolated(float alpha)
    {
        if (!_havePrevious || alpha >= 1f) return _current;
        _shown.Clear();
        bool queued = SameLayout(0, _previousQueued, 0, _currentQueued);
        bool immediate = SameLayout(_previousQueued, _previous.Count, _currentQueued, _current.Count);
        System.Runtime.InteropServices.CollectionsMarshal.SetCount(_shown, _current.Count);
        var current = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_current);
        var previous = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_previous);
        var shown = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_shown);
        for (int i = 0; i < current.Length; i++)
        {
            ref readonly var c = ref current[i];
            ref var o = ref shown[i];
            o = c;
            bool tail = i >= _currentQueued;
            int j = tail ? i - _currentQueued + _previousQueued : i;
            if (!(tail ? immediate : queued) || !Close(previous[j], c)) continue;
            ref readonly var p = ref previous[j];
            o.P0 = Vector2.Lerp(p.P0, c.P0, alpha);
            o.P1 = Vector2.Lerp(p.P1, c.P1, alpha);
            o.P2 = Vector2.Lerp(p.P2, c.P2, alpha);
            o.P3 = Vector2.Lerp(p.P3, c.P3, alpha);
            o.Colour = Vector4.Lerp(p.Colour, c.Colour, alpha);
            o.Colour1 = Vector4.Lerp(p.Colour1, c.Colour1, alpha);
            o.Colour2 = Vector4.Lerp(p.Colour2, c.Colour2, alpha);
        }
        return _shown;
    }

    // Amedo
    private bool SameLayout(int prevStart, int prevEnd, int curStart, int curEnd)
    {
        if (prevEnd - prevStart != curEnd - curStart) return false;
        for (int k = 0; k < curEnd - curStart; k++)
            if (!Matches(_previous[prevStart + k], _current[curStart + k])) return false;
        return true;
    }

    // Amedo
    private static bool Close(in ScreenItems.Item a, in ScreenItems.Item b) =>
        GameMath.DistanceSq(a.P0, b.P0) <= MaxStepMove * MaxStepMove && GameMath.DistanceSq(a.P1, b.P1) <= MaxStepMove * MaxStepMove &&
        GameMath.DistanceSq(a.P2, b.P2) <= MaxStepMove * MaxStepMove && GameMath.DistanceSq(a.P3, b.P3) <= MaxStepMove * MaxStepMove;

    // Amedo
    private static bool Matches(in ScreenItems.Item a, in ScreenItems.Item b) =>
        a.Kind == b.Kind && a.Page == b.Page && ReferenceEquals(a.Fallback, b.Fallback) && a.Image == b.Image &&
        a.Uv0 == b.Uv0 && a.Uv1 == b.Uv1 && a.Uv2 == b.Uv2 && a.Uv3 == b.Uv3;

    private readonly Dictionary<GameFont.Page, Texture2D> _images = new(); // Amedo

    // Amedo
    private Texture2D ImageTexture(GameFont.Page page)
    {
        if (!_images.TryGetValue(page, out var texture))
        {
            _images[page] = texture = new Texture2D(Engine.Instance.GL, page.Rgba, (uint)page.Width, (uint)page.Height);
            texture.ClampToEdge(); // Amedo
        }
        return texture;
    }

    // Amedo 2026-10-01
    private Texture2D? GlyphPage(GameFont font, GameFont.IFallback? fallback, int page)
    {
        var atlas = AtlasPages(font);
        if (page < atlas.Count) return atlas[page];
        var dynamic = DynamicPages(fallback);
        return dynamic is not null && page - atlas.Count < dynamic.Count ? dynamic[page - atlas.Count] : null;
    }

    // Amedo 2026-10-01
    private List<GameFont.Quad> LayoutFor(object owner, GameFont font, GameFont.IFallback? fallback, string text, Vector2 position, Vector2 scale, uint flags)
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

    // Amedo 2026-10-01
    private void DrawQuads(GameFont font, GameFont.IFallback? fallback, List<GameFont.Quad> quads, ImDrawListPtr list, Vector2 origin, Vector2 size, uint tint)
    {
        var atlas = AtlasPages(font);
        var dynamic = DynamicPages(fallback);
        foreach (var q in quads)
        {
            Texture2D? texture = q.Page < atlas.Count ? atlas[q.Page] : dynamic is not null && q.Page - atlas.Count < dynamic.Count ? dynamic[q.Page - atlas.Count] : null;
            if (texture is null) continue;
            var page = font.PageAt(q.Page, fallback);
            var p0 = origin + new Vector2(q.X, q.Y) * size;
            var p1 = p0 + new Vector2(q.W, q.H) * size;
            list.AddImage((nint)texture.GlId, p0, p1,
                new Vector2(q.U / page.Width, q.V0 / page.Height),
                new Vector2((q.U + q.UW) / page.Width, q.V1 / page.Height), tint);
        }
    }

    // Amedo 2026-10-01
    public void DrawPreview(GameFont font, GameFont.IFallback? fallback, string text, ImDrawListPtr list, Vector2 min, Vector2 max, object owner)
    {
        const float screenAspect = 16f / 9f;
        float k = screenAspect * 0.75f;
        list.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.2f, 0.32f, 0.18f, 1f)));
        list.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.5f)));
        if (text.Length == 0) return;
        var quads = LayoutFor(owner, font, fallback, text, new Vector2(0.5f, 0.5f), new Vector2(0.5f / k, 0.5f), 0x22);
        if (quads.Count == 0) return;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var q in quads)
        {
            x0 = MathF.Min(x0, q.X); y0 = MathF.Min(y0, q.Y);
            x1 = MathF.Max(x1, q.X + q.W); y1 = MathF.Max(y1, q.Y + q.H);
        }
        var rect = max - min;
        float height = MathF.Min(rect.Y * 0.9f / MathF.Max(y1 - y0, 1e-4f), rect.X * 0.96f / MathF.Max((x1 - x0) * screenAspect, 1e-4f));
        var size = new Vector2(height * screenAspect, height);
        var origin = min + rect * 0.5f - new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f) * size;
        list.PushClipRect(min, max, true);
        DrawQuads(font, fallback, quads, list, origin, size, 0xFFFFFFFF);
        list.PopClipRect();
    }

    // Amedo 2026-10-01
    private List<Texture2D> AtlasPages(GameFont font)
    {
        if (ReferenceEquals(_atlasFont, font)) return _atlas;
        foreach (var t in _atlas) t.Dispose();
        _atlas.Clear();
        _atlasFont = font;
        foreach (var p in font.Pages) _atlas.Add(new Texture2D(Engine.Instance.GL, p.Rgba, (uint)p.Width, (uint)p.Height));
        return _atlas;
    }

    // Amedo 2026-10-01
    private List<Texture2D>? DynamicPages(GameFont.IFallback? fallback)
    {
        if (fallback is null) return null;
        if (_dynamic.TryGetValue(fallback, out var known) && known.Version == fallback.Version) return known.Pages;
        if (known.Pages is not null) foreach (var t in known.Pages) t.Dispose();
        var pages = fallback.Pages.Select(p => new Texture2D(Engine.Instance.GL, p.Rgba, (uint)p.Width, (uint)p.Height)).ToList();
        _dynamic[fallback] = (fallback.Version, pages);
        return pages;
    }

    // Amedo 2026-10-01
    public void Forget(GameFont.IFallback fallback)
    {
        if (!_dynamic.Remove(fallback, out var known)) return;
        foreach (var t in known.Pages) t.Dispose();
    }

    public void Dispose()
    {
        _movie?.Dispose(); // Amedo
        _movie = null;
        _scaler?.Dispose(); // Amedo
        _scaler = null;
        _scaledVersion = -1;
        foreach (var t in _atlas) t.Dispose();
        _atlas.Clear();
        _atlasFont = null;
        foreach (var (_, pages) in _dynamic.Values) foreach (var t in pages) t.Dispose();
        _dynamic.Clear();
        _layouts.Clear();
        _overlay.ClearLayouts(); // Amedo
        _fallbackScreen = null;
        foreach (var t in _images.Values) t.Dispose(); // Amedo
        _images.Clear();
    }
}
