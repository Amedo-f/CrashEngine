using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Screen;

// Amedo 2026-09-30
public sealed partial class GameScreen // Amedo 2026-10-01
{
    public const int TicksPerSecond = 9000;
    public const string FontPath = @"Startup\Fonts\Crash.psf";
    public const string PalFontPath = @"Startup\Fonts\Crash_Euro.psf"; // Amedo
    public static string FontPathFor(string discLanguage) => Region.RegionProfile.For(discLanguage).FontPath; // Amedo 2026-11-11
    public static System.Numerics.Vector2 FontSizeFor(string discLanguage) => Region.RegionProfile.For(discLanguage).FontSize; // Amedo 2026-11-11
    public const string FrontendPath = @"Startup\Frontend.bin"; // Amedo

    public GameFont? Font { get; }
    public GameTexts? AgentLabTexts { get; }
    public GameTexts? CodeTexts { get; }

    public Oleg.Oleg? Oleg { get; set; } // Amedo
    public IReadOnlyList<object?>? FrontendSounds { get; private init; } // Amedo
    public string DiscLanguage { get; private init; } = PlaySettings.AmericanLanguage; // Amedo
    public int IconFileSize { get; private init; } // Amedo
    private string? _packagePath; // Amedo

    private GameScreen(GameFont? font, GameTexts? agentLab, GameTexts? code)
    {
        Font = font;
        AgentLabTexts = agentLab;
        CodeTexts = code;
    }

    public const string DefaultFallbackFont = "Lalezar-Regular.ttf"; // Amedo 2026-10-01

    public string? LanguageDirectory { get; private init; }

    public static GameScreen Load(string? discDirectory, Action<string>? log = null, string? languageDirectory = null, string discLanguage = PlaySettings.AmericanLanguage,
                                  PS2AnyTwinsanityRM2? globalRm2 = null) // Amedo
    {
        GameFont? font = null;
        GameFont.Page?[] icons = new GameFont.Page?[IconSlots]; // Amedo 2026-10-01
        object?[]? frontend = null; // Amedo
        GameFont.Page? particles = ReadParticlePage(globalRm2, 0); // Amedo
        int iconSize = 0; // Amedo
        GameTexts? agentLab = GameTexts.FromLanguage(languageDirectory, GameTexts.AgentLabFile), code = GameTexts.FromLanguage(languageDirectory, GameTexts.CodeFile); // Amedo 2026-10-01
        var bd = discDirectory is null ? null : Path.Combine(discDirectory, "CRASH.BD");
        if (bd is not null && File.Exists(bd))
        {
            try
            {
                using var pkg = PackageReader.Open(bd);
                if (pkg.OpenByPath(FontPathFor(discLanguage)) is { } fs) // Amedo
                {
                    var psf = new PS2PSF();
                    psf.Read(new BinaryReader(fs), (int)fs.Length);
                    font = new GameFont(psf) { Size_0C = FontSizeFor(discLanguage) }; // Amedo
                }
                if (pkg.OpenByPath(IconsPath) is { } ip) ReadIcons(ip, icons); // Amedo 2026-10-01
                if (pkg.OpenByPath(FrontendPath) is { } fe) frontend = ReadFrontend(fe); // Amedo
                if (pkg.OpenByPath(Game.SaveManager.IconPath) is { } ico) iconSize = (int)ico.Length; // Amedo
                if (agentLab is null && (pkg.OpenByPath(GameTexts.AgentLabPathFor(discLanguage)) ?? pkg.OpenByPath(GameTexts.AgentLabPath)) is { } a) agentLab = new GameTexts(a.ToArray()); // Amedo
                if (code is null && (pkg.OpenByPath(GameTexts.CodePathFor(discLanguage)) ?? pkg.OpenByPath(GameTexts.CodePath)) is { } c) code = new GameTexts(c.ToArray()); // Amedo
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Screen] font/texts load failed: {ex.Message}");
            }
        }
        var styles = TextStyles.Load(languageDirectory, log); // Amedo 2026-10-01
        log?.Invoke($"[Screen] font {(font is null ? "missing" : $"{font.Glyphs.Length} glyphs, {font.Pages.Length} pages")}, " +
                    $"fallback {Path.GetFileName(ResolveFont(languageDirectory, styles.Default.Font)) ?? "none"}, styles {1 + styles.Objects.Count}, " +
                    $"language {languageDirectory ?? "disc"}, texts {agentLab?.Count ?? 0} script / {code?.Count ?? 0} code, " +
                    $"icons {icons.Count(i => i is not null)}, frontend sounds {frontend?.Length ?? 0}, particle page {(particles is { } page ? $"{page.Width}x{page.Height}" : "missing")}"); // Amedo
        return new GameScreen(font, agentLab, code) { LanguageDirectory = languageDirectory, Styles = styles, Log = log, Icons = icons, FrontendSounds = frontend, DiscLanguage = discLanguage, _packagePath = bd, IconFileSize = iconSize, ParticlePage_0 = particles };
    }

    // Amedo
    public IReadOnlyList<GameFont.Page?>? ReadPicture(string path)
    {
        if (_packagePath is null || !File.Exists(_packagePath)) return null;
        try
        {
            using var pkg = PackageReader.Open(_packagePath);
            if (pkg.OpenByPath(path) is not { } stream) return null;
            var psm = new PS2PSM();
            psm.Read(new BinaryReader(stream), (int)stream.Length);
            var pages = new GameFont.Page?[psm.PTCs.Count];
            for (int i = 0; i < pages.Length; i++)
                if (psm.PTCs[i].Texture is Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture tex) pages[i] = GameFont.Decode(tex);
            return pages;
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[Screen] picture {path} failed: {ex.Message}");
            return null;
        }
    }

    // Amedo
    public string? ReadCredits()
    {
        if (LanguageDirectory is not null && Path.Combine(LanguageDirectory, GameTexts.CreditsFile) is var own && File.Exists(own))
            return File.ReadAllText(own, System.Text.Encoding.UTF8).TrimStart('\uFEFF');
        if (_packagePath is null || !File.Exists(_packagePath)) return null;
        try
        {
            using var pkg = PackageReader.Open(_packagePath);
            var stream = pkg.OpenByPath(GameTexts.CreditsPathFor(DiscLanguage)) ?? pkg.OpenByPath(GameTexts.CreditsPath);
            return stream is null ? null : System.Text.Encoding.Latin1.GetString(stream.ToArray());
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[Screen] credits text failed: {ex.Message}");
            return null;
        }
    }

    // Amedo
    private static object?[] ReadFrontend(Stream stream)
    {
        var bank = new PS2Frontend();
        bank.Read(new BinaryReader(stream), (int)stream.Length);
        var sounds = new object?[bank.GetItemsAmount()];
        for (int i = 0; i < sounds.Length; i++) sounds[i] = bank.GetItem(i);
        return sounds;
    }

    // Amedo 2026-10-01
    public TextStyles Styles { get; private init; } = new();
    private Action<string>? Log { get; init; }
    private readonly Dictionary<string, TtfGlyphs?> _fallbacks = new();
    private readonly Dictionary<string, TtfGlyphs?> _byObject = new();

    // Amedo 2026-10-01
    public GameFont.IFallback? FallbackFor(string? objectKey)
    {
        if (_byObject.TryGetValue(objectKey ?? "", out var known)) return known;
        var style = Styles.For(objectKey);
        var key = style.Key;
        if (!_fallbacks.TryGetValue(key, out var cached))
        {
            var path = ResolveFont(LanguageDirectory, style.Font);
            _fallbacks[key] = cached = path is null ? null : TtfGlyphs.Load(path, style, Log);
        }
        return _byObject[objectKey ?? ""] = cached;
    }

    // Amedo 2026-10-01
    public static IEnumerable<string> FontFiles(string? languageDirectory)
    {
        static IEnumerable<string> In(string? dir) => dir is not null && Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.ttf").Concat(Directory.GetFiles(dir, "*.otf")).OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            : Enumerable.Empty<string>();
        return In(languageDirectory).Concat(In(Path.Combine(AppContext.BaseDirectory, "Fonts")));
    }

    // Amedo 2026-10-01
    public static string? ResolveFont(string? languageDirectory, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) &&
            FontFiles(languageDirectory).FirstOrDefault(f => Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase)) is { } chosen)
            return chosen;
        return FallbackFontPath(languageDirectory);
    }

    // Amedo 2026-10-01
    public static string? FallbackFontPath(string? languageDirectory)
    {
        if (languageDirectory is not null && Directory.Exists(languageDirectory) &&
            Directory.GetFiles(languageDirectory, "*.ttf").Concat(Directory.GetFiles(languageDirectory, "*.otf")).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault() is { } own)
            return own;
        var shipped = Path.Combine(AppContext.BaseDirectory, "Fonts", DefaultFallbackFont);
        return File.Exists(shipped) ? shipped : null;
    }

    // Amedo 2026-10-01
    public void Dispose()
    {
        foreach (var f in _fallbacks.Values) f?.Dispose();
        _fallbacks.Clear();
    }

    // Amedo 2026-10-01
    public static int ExportOriginalTexts(string discDirectory, string languageDirectory, string? discLanguage = null) // Amedo
    {
        using var pkg = PackageReader.Open(Path.Combine(discDirectory, "CRASH.BD"));
        Directory.CreateDirectory(languageDirectory);
        int written = 0;
        string lang = discLanguage ?? PlaySettings.DetectDiscLanguage(discDirectory); // Amedo
        foreach (var (source, file) in new[] { (GameTexts.AgentLabPathFor(lang), GameTexts.AgentLabFile), (GameTexts.CodePathFor(lang), GameTexts.CodeFile), (GameTexts.CreditsPathFor(lang), GameTexts.CreditsFile) }) // Amedo
        {
            var target = Path.Combine(languageDirectory, file);
            if (File.Exists(target) || pkg.OpenByPath(source) is not { } ms) continue;
            File.WriteAllText(target, System.Text.Encoding.Latin1.GetString(ms.ToArray()), new System.Text.UTF8Encoding(false));
            written++;
        }
        return written;
    }

    public static int Ticks(float seconds) => GameMath.Trunc(seconds * TicksPerSecond);

    public static int InstanceLine(uint subtype) => (int)(subtype < 11 ? subtype + 0x1E : subtype + 0x29); // Amedo 2026-10-01
}
