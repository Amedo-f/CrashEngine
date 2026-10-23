using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrashEngine.Core;

// Amedo
public enum TextureFiltering { Original, Trilinear, Anisotropic4, Anisotropic8, Anisotropic16 }

// Amedo
public enum ShadowMode { Original, Sun }

// Amedo
public sealed record GraphicsSettings
{
    public static readonly int[] FrameLimits = { 60, 144, 240, 360, 0 };
    public static readonly int[] RenderHeights = { 0, 720, 900, 1080, 1440, 2160 };
    public static readonly int[] MsaaSamples = { 1, 2, 4, 8 };
    public const int BrightnessMin = 0, BrightnessMax = 10, BrightnessDefault = 5;
    public const int SharpnessMin = 0, SharpnessMax = 10, SharpnessDefault = 0; // Amedo
    public const int SunStrengthMin = 0, SunStrengthMax = 10, SunStrengthDefault = 6; // Amedo
    public const int SunRangeMin = 50, SunRangeMax = 2000, SunRangeDefault = 400; // Amedo
    public const int SunSoftnessMin = 0, SunSoftnessMax = 10, SunSoftnessDefault = 4; // Amedo
    public const int PointLightStrengthMin = 0, PointLightStrengthMax = 10, PointLightStrengthDefault = 5; // Amedo
    public const int ShapeSoftnessMin = 0, ShapeSoftnessMax = 10, ShapeSoftnessDefault = 8; // Amedo

    public bool Fullscreen { get; init; } = true;
    public bool VSync { get; init; } = true;
    public int FrameLimit { get; init; } = 60; // Amedo
    public int RenderHeight { get; init; }
    public int Msaa { get; init; } = 8; // Amedo
    public TextureFiltering Filtering { get; init; } = TextureFiltering.Anisotropic16; // Amedo
    public int Brightness { get; init; } = BrightnessDefault;
    public int Sharpness { get; init; } = SharpnessDefault; // Amedo
    public ShadowMode Shadows { get; init; } = ShadowMode.Original; // Amedo
    public int SunStrength { get; init; } = SunStrengthDefault; // Amedo
    public int SunAzimuth { get; init; } = 135; // Amedo
    public int SunElevation { get; init; } = 50; // Amedo
    public int SunRange { get; init; } = SunRangeDefault; // Amedo
    public int SunSoftness { get; init; } = SunSoftnessDefault; // Amedo
    public bool SunFromLevel { get; init; } = true; // Amedo
    public bool PointLights { get; init; } = true; // Amedo
    public int PointLightStrength { get; init; } = PointLightStrengthDefault; // Amedo
    public int ShapeSoftness { get; init; } = ShapeSoftnessDefault; // Amedo

    [JsonIgnore] public float Gamma => 1f + (Brightness - BrightnessDefault) * 0.1f;
    [JsonIgnore] public float SharpenAmount => Sharpness / (float)SharpnessMax; // Amedo
    [JsonIgnore] public float MipBias => -Sharpness / (float)SharpnessMax; // Amedo
    [JsonIgnore] public float SunShadowAmount => SunStrength / (float)SunStrengthMax; // Amedo
    [JsonIgnore] public float SunPenumbra => SunSoftness / (float)SunSoftnessMax * 0.08f; // Amedo
    [JsonIgnore] public float ShapeSoft => ShapeSoftness / (float)ShapeSoftnessMax; // Amedo
    [JsonIgnore] public float PointLightAmount => PointLightStrength / (float)PointLightStrengthDefault * 0.06f; // Amedo: the game's object-light numbers (clamped there) scaled to a visible pool under a light
    [JsonIgnore] public int Anisotropy => Filtering switch
    {
        TextureFiltering.Anisotropic4 => 4,
        TextureFiltering.Anisotropic8 => 8,
        TextureFiltering.Anisotropic16 => 16,
        _ => 1,
    };

    public static bool DisplayModeAvailable { get; set; }
    public static bool Persist { get; set; } = true;
    public static int Version { get; private set; }
    public static event Action<GraphicsSettings, GraphicsSettings>? Changed;

    private static GraphicsSettings? _current;
    public static GraphicsSettings Current => _current ??= Load();

    private static string? _file; // Amedo
    private static bool _fileless; // Amedo
    public static string FilePath => _file ?? Path.Combine(AppContext.BaseDirectory, "graphics.json"); // Amedo

    // Amedo
    public static void UseFile(string? path)
    {
        if (_current is not null && !_fileless && path is not null && string.Equals(_file, path, StringComparison.OrdinalIgnoreCase)) return;
        _file = path;
        _fileless = path is null;
        var next = Load();
        var old = _current ?? next;
        _current = next;
        Version++;
        if (next != old) Changed?.Invoke(old, next);
    }
    private static string OldFrameModeFile => Path.Combine(AppContext.BaseDirectory, "frame_mode.txt");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static void Set(GraphicsSettings next, bool save = true)
    {
        next = next.Sanitized();
        var old = Current;
        if (next == old) return;
        _current = next;
        Version++;
        Changed?.Invoke(old, next);
        if (save) next.Save();
    }

    public GraphicsSettings Sanitized() => this with
    {
        FrameLimit = Allowed(FrameLimits, FrameLimit, 0),
        RenderHeight = Allowed(RenderHeights, RenderHeight, 0),
        Msaa = Allowed(MsaaSamples, Msaa, 1),
        Filtering = Enum.IsDefined(Filtering) ? Filtering : TextureFiltering.Original,
        Brightness = Math.Clamp(Brightness, BrightnessMin, BrightnessMax),
        Sharpness = Math.Clamp(Sharpness, SharpnessMin, SharpnessMax), // Amedo
        Shadows = Enum.IsDefined(Shadows) ? Shadows : ShadowMode.Original, // Amedo
        SunStrength = Math.Clamp(SunStrength, SunStrengthMin, SunStrengthMax), // Amedo
        SunAzimuth = ((SunAzimuth % 360) + 360) % 360, // Amedo
        SunElevation = Math.Clamp(SunElevation, 5, 90), // Amedo
        SunRange = Math.Clamp(SunRange, SunRangeMin, SunRangeMax), // Amedo
        SunSoftness = Math.Clamp(SunSoftness, SunSoftnessMin, SunSoftnessMax), // Amedo
        PointLightStrength = Math.Clamp(PointLightStrength, PointLightStrengthMin, PointLightStrengthMax), // Amedo
        ShapeSoftness = Math.Clamp(ShapeSoftness, ShapeSoftnessMin, ShapeSoftnessMax), // Amedo
    };

    private static int Allowed(int[] allowed, int value, int fallback) => Array.IndexOf(allowed, value) >= 0 ? value : fallback;

    private static GraphicsSettings Load()
    {
        try
        {
            if (_fileless) return new GraphicsSettings(); // Amedo
            if (Persist && File.Exists(FilePath))
                return (JsonSerializer.Deserialize<GraphicsSettings>(File.ReadAllText(FilePath), Json) ?? new()).Sanitized();
            if (Persist && _file is null && File.Exists(OldFrameModeFile)) // Amedo
                return FromFrameMode(File.ReadAllText(OldFrameModeFile).Trim());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GraphicsSettings] could not read {FilePath}: {ex.Message}");
        }
        return new GraphicsSettings();
    }

    private static GraphicsSettings FromFrameMode(string mode) => mode switch
    {
        "Fps60" => new GraphicsSettings { VSync = false, FrameLimit = 60 },
        "Fps144" => new GraphicsSettings { VSync = false, FrameLimit = 144 },
        "Unlimited" => new GraphicsSettings { VSync = false, FrameLimit = 0 },
        _ => new GraphicsSettings(),
    };

    public void Save()
    {
        if (!Persist || _fileless) return; // Amedo
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json)); }
        catch (Exception ex) { Console.WriteLine($"[GraphicsSettings] could not save {FilePath}: {ex.Message}"); }
    }

    public static int EffectiveHeight(int height, int screenHeight) => height <= 0 || height >= screenHeight ? 0 : height;

    public static int[] HeightsFor(int screenHeight)
    {
        var heights = new List<int>();
        foreach (int h in RenderHeights)
            if (h > 0 && h < screenHeight) heights.Add(h);
        heights.Add(0);
        return heights.ToArray();
    }

    public static (int Width, int Height) RenderSize(int height, int screenWidth, int screenHeight)
    {
        height = EffectiveHeight(height, screenHeight);
        if (height <= 0 || screenWidth <= 0 || screenHeight <= 0) return (screenWidth, screenHeight);
        int width = (int)MathF.Round(height * (float)screenWidth / screenHeight / 2f) * 2;
        return (Math.Max(2, width), height);
    }

    // Amedo
    public static Func<float> GameAspect { get; set; } = () => 16f / 9f;
    public static bool FollowWindow { get; set; } // Amedo

    // Amedo
    public static (int Width, int Height) SceneSize(int height, int windowWidth, int windowHeight, int screenHeight)
    {
        if (windowWidth <= 0 || windowHeight <= 0) return (windowWidth, windowHeight);
        if (FollowWindow) return RenderSize(height, windowWidth, windowHeight);
        int target = EffectiveHeight(height, screenHeight) is > 0 and var h ? h : screenHeight > 0 ? screenHeight : windowHeight;
        float aspect = GameAspect(), windowAspect = (float)windowWidth / windowHeight;
        float inner = windowAspect >= aspect ? windowHeight : windowWidth / aspect;
        float scale = target / inner;
        int w = (int)MathF.Round(windowWidth * scale / 2f) * 2, sh = (int)MathF.Round(windowHeight * scale / 2f) * 2;
        return (Math.Max(2, w), Math.Max(2, sh));
    }

    public static string ResolutionName(int height, int screenWidth, int screenHeight)
    {
        var (w, h) = RenderSize(height, screenWidth, screenHeight);
        bool wide169 = screenHeight > 0 && MathF.Abs((float)screenWidth / screenHeight - 16f / 9f) < 0.02f;
        string tag = !wide169 ? "" : h switch { 720 => " HD", 900 => " HD+", 1080 => " FULL HD", 1440 => " 2K", 2160 => " 4K", _ => "" };
        return $"{w}X{h}{tag}";
    }

    [JsonIgnore] public string FrameRateName => VSync
        ? FrameLimit > 0 ? $"VSync + {FrameLimit} FPS" : "VSync"
        : FrameLimit > 0 ? $"{FrameLimit} FPS" : "Unlimited";
}
