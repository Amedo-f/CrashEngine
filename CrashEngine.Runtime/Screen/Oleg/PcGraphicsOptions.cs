using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public sealed class PcGraphicsOptions
{
    public const int DisplayItem = 0x40, VSyncItem = 0x41, FrameLimitItem = 0x42, ResolutionItem = 0x43, MsaaItem = 0x44, FilteringItem = 0x45, BrightnessItem = 0x46, SharpnessItem = 0x47, ShadowsItem = 0x48, QualityItem = 0x49; // Amedo

    private static readonly string[] DisplayNames = { "FULLSCREEN", "WINDOWED" };
    private static readonly string[] FilteringNames = { "ORIGINAL", "TRILINEAR", "ANISO 4X", "ANISO 8X", "ANISO 16X" };
    private static readonly string[] ShadowNames = { "ORIGINAL", "SUN" }; // Amedo

    private readonly ChoiceItem? _display;
    private readonly ToggleItem _vsync;
    private readonly ChoiceItem _frameLimit, _resolution, _msaa, _filtering;
    private readonly ChoiceItem _shadows; // Amedo
    private readonly ChoiceItem? _quality; // Amedo 2026-10-27
    private readonly ValueItem _brightness;
    private readonly ValueItem _sharpness; // Amedo
    private int _version = -1;
    private int _namesWidth = -1, _namesHeight = -1;
    private int[] _heights = { 0 };

    public PcGraphicsOptions()
    {
        if (GraphicsSettings.DisplayModeAvailable) _display = Choice("DISPLAY", DisplayItem, DisplayNames);
        if (GraphicsSettings.QualityPresets is { Length: > 0 } presets) _quality = Choice("QUALITY", QualityItem, presets.Select(p => p.Name).ToArray()); // Amedo 2026-10-27
        _vsync = new ToggleItem(0, VSyncItem, 0, 1, true, 0, 1) { Name_00 = "VSYNC" };
        _frameLimit = Choice("FRAME LIMIT", FrameLimitItem, GraphicsSettings.FrameLimits.Select(f => f > 0 ? f.ToString() : "UNLIMITED").ToArray());
        _resolution = Choice("RESOLUTION", ResolutionItem, new[] { "" });
        _msaa = Choice("ANTI-ALIASING", MsaaItem, GraphicsSettings.MsaaSamples.Select(s => s > 1 ? $"MSAA {s}X" : "OFF").ToArray());
        _filtering = Choice("FILTERING", FilteringItem, FilteringNames);
        _brightness = new ValueItem(0, BrightnessItem, GraphicsSettings.BrightnessMin, GraphicsSettings.BrightnessMax, false, GraphicsSettings.BrightnessDefault, 1)
        {
            Name_00 = "BRIGHTNESS",
        };
        _shadows = Choice("SHADOWS", ShadowsItem, ShadowNames); // Amedo
        _sharpness = new ValueItem(0, SharpnessItem, GraphicsSettings.SharpnessMin, GraphicsSettings.SharpnessMax, false, GraphicsSettings.SharpnessDefault, 1) // Amedo
        {
            Name_00 = "SHARPNESS",
        };
    }

    private static ChoiceItem Choice(string name, int id, string[] values) => new(0, id, values.Length, false, 0, 1)
    {
        Name_00 = name,
        Strings_40 = values,
        UseStrings_44 = true,
        Cycles_3D = true,
    };

    public void AddTo(MenuPage page)
    {
        if (_quality is not null) // Amedo 2026-10-27
        {
            page.Add_25B710(_quality);
            page.Add_25B710(_frameLimit);
            return;
        }
        if (_display is not null) page.Add_25B710(_display);
        page.Add_25B710(_vsync);
        page.Add_25B710(_frameLimit);
        page.Add_25B710(_resolution);
        page.Add_25B710(_msaa);
        page.Add_25B710(_filtering);
        page.Add_25B710(_brightness);
        page.Add_25B710(_sharpness); // Amedo
        page.Add_25B710(_shadows); // Amedo
    }

    // Amedo
    private void RefreshResolutionNames()
    {
        int w = Engine.Instance.ScreenWidth, h = Engine.Instance.ScreenHeight; // Amedo
        if (w == _namesWidth && h == _namesHeight) return;
        _namesWidth = w;
        _namesHeight = h;
        _heights = GraphicsSettings.HeightsFor(h);
        var names = new string[_heights.Length];
        for (int i = 0; i < names.Length; i++) names[i] = GraphicsSettings.ResolutionName(_heights[i], w, h);
        _resolution.Strings_40 = names;
        _resolution.Min_34 = 0;
        _resolution.Max_38 = names.Length - 1;
        _version = -1;
    }

    public void Load(int col)
    {
        RefreshResolutionNames();
        var s = GraphicsSettings.Current;
        _display?.SetInt(col, s.Fullscreen ? 0 : 1);
        _vsync.SetFlag_25BD40(col, s.VSync ? 1 : 0);
        _frameLimit.SetInt(col, Math.Max(0, Array.IndexOf(GraphicsSettings.FrameLimits, s.FrameLimit)));
        int height = Array.IndexOf(_heights, GraphicsSettings.EffectiveHeight(s.RenderHeight, _namesHeight));
        _resolution.SetInt(col, height >= 0 ? height : _heights.Length - 1);
        _msaa.SetInt(col, Math.Max(0, Array.IndexOf(GraphicsSettings.MsaaSamples, s.Msaa)));
        _filtering.SetInt(col, (int)s.Filtering);
        _brightness.SetInt(col, s.Brightness);
        _sharpness.SetInt(col, s.Sharpness); // Amedo
        _shadows.SetInt(col, (int)s.Shadows); // Amedo
        _quality?.SetInt(col, s.Quality); // Amedo 2026-10-27
        _version = GraphicsSettings.Version;
    }

    public void Store(int col)
    {
        RefreshResolutionNames();
        if (_version != GraphicsSettings.Version)
        {
            Load(col);
            return;
        }
        var s = GraphicsSettings.Current;
        _vsync.GetFlag_25BD68(col, out bool vsync);
        var next = s with
        {
            Fullscreen = _display is null ? s.Fullscreen : Value(_display, col) == 0,
            VSync = vsync,
            FrameLimit = GraphicsSettings.FrameLimits[Value(_frameLimit, col)],
            RenderHeight = _heights[Math.Clamp(Value(_resolution, col), 0, _heights.Length - 1)],
            Msaa = GraphicsSettings.MsaaSamples[Value(_msaa, col)],
            Filtering = (TextureFiltering)Value(_filtering, col),
            Brightness = Value(_brightness, col),
            Sharpness = Value(_sharpness, col), // Amedo
            Shadows = (ShadowMode)Value(_shadows, col), // Amedo
            Quality = _quality is null ? s.Quality : Value(_quality, col), // Amedo 2026-10-27
        };
        if (next == s) return;
        GraphicsSettings.Set(next);
        Load(col);
    }

    private static int Value(MenuItem item, int col) => item.GetInt(col, out int v) ? v : 0;

    public static void ConfigureDrawer(MenuDrawer drawer)
    {
        drawer.FewItems_00 = 4;
        drawer.FewScale_1C = drawer.ManyScale_28;
        drawer.FewSpacing_24 = drawer.ManySpacing_30;
        drawer.ManyItems_01 = 7;
        drawer.ManyScale_28 = new Vector2(0.5f, 0.58f);
        drawer.ManySpacing_30 = 0.04f;
    }
}
