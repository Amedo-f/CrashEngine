using CrashEngine.Core;
using CrashEngine.Importer;
using CrashLauncher;
using ImGuiNET;
using System.Runtime.InteropServices;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

string SCRIPT_OUT = Path.Combine(AppContext.BaseDirectory, "Scripts");

const bool CUSTOM_FONT_ENABLED = false;
Engine.CustomFontSetup = atlas =>
{
    try
    {
        var sysFontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var sysFont = Path.Combine(sysFontsDir, "segoeui.ttf");
        if (!File.Exists(sysFont)) sysFont = Path.Combine(sysFontsDir, "arial.ttf");
        if (File.Exists(sysFont))
        {
            unsafe
            {
                ushort[] rangesManaged = { 0x0020, 0x00FF, 0x2010, 0x2027, 0x2030, 0x2044, 0 };
                var rangesPtr = (ushort*)Marshal.AllocHGlobal(rangesManaged.Length * sizeof(ushort));
                for (int i = 0; i < rangesManaged.Length; i++) rangesPtr[i] = rangesManaged[i];
                atlas.AddFontFromFileTTF(sysFont, 16f, default, (IntPtr)rangesPtr);
            }
            Console.WriteLine($"Font setup: loaded {Path.GetFileName(sysFont)} with extended glyph range (em-dash etc. now render correctly).");
        }
        else
        {
            Console.WriteLine("Font setup: no system TTF found (segoeui.ttf/arial.ttf) — falling back to ImGui's ASCII-only default font (em-dashes etc. will show as '?').");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Font setup: failed to load extended-glyph font ({ex.Message}) — falling back to default.");
    }

    if (!CUSTOM_FONT_ENABLED) return;
    try
    {
        string? discPath = null;
        foreach (var recent in CrashProject.GetRecents())
        {
            var proj = CrashProject.Open(recent);
            if (proj?.DiscContentPathPS2 is { } dp && Directory.Exists(dp)) { discPath = dp; break; }
        }
        if (discPath is null) { Console.WriteLine("Custom font: no project with disc content found yet, using default font."); return; }

        using var pkg = PackageReader.Open(discPath);
        using var stream = pkg.OpenByPath(@"Startup\Fonts\Arial.psf");
        if (stream is null) { Console.WriteLine("Custom font: Arial.psf not found on this disc."); return; }
        using var reader = new BinaryReader(stream);
        var psf = new PS2PSF();
        psf.Read(reader, (int)stream.Length);

        var data = MeshDecoder.GetPsfRawGlyphs(psf);
        if (data is null || data.Value.Pages.Count == 0 || data.Value.Glyphs.Count == 0)
        { Console.WriteLine("Custom font: Arial.psf didn't decode into usable glyph data."); return; }
        var (pw, _, pageRgba) = data.Value.Pages[0];

        var baseFont = atlas.AddFontDefault();
        var placed = new List<(int RectId, MeshDecoder.PsfGlyph Glyph)>();
        foreach (var g in data.Value.Glyphs)
            placed.Add((atlas.AddCustomRectFontGlyph(baseFont, (ushort)g.Codepoint, g.W, g.H, g.W), g));

        atlas.Build();

        atlas.GetTexDataAsRGBA32(out IntPtr atlasPixels, out int atlasW, out int atlasH, out _);
        unsafe
        {
            var dst = (byte*)atlasPixels;
            long dstLen = (long)atlasW * atlasH * 4;
            foreach (var (rectId, g) in placed)
            {
                var rect = atlas.GetCustomRectByIndex(rectId);
                for (int y = 0; y < g.H; y++)
                {
                    int srcRowStart = ((g.Y + y) * pw + g.X) * 4;
                    int dstRowStart = ((rect.Y + y) * atlasW + rect.X) * 4;
                    int rowBytes = g.W * 4;
                    if (srcRowStart < 0 || srcRowStart + rowBytes > pageRgba.Length) continue;
                    if (dstRowStart < 0 || dstRowStart + rowBytes > dstLen) continue;
                    for (int b = 0; b < rowBytes; b++)
                        dst[dstRowStart + b] = pageRgba[srcRowStart + b];
                }
            }
        }
        Console.WriteLine($"Custom font: baked {placed.Count} real Crash Twinsanity glyph(s) from Arial.psf into the ImGui font atlas.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Custom font setup failed, using default font: {ex.Message}");
    }
};

var engine = new Engine("CrashEngine", 1280, 720);
engine.ActiveScene = new ProjectSetupScene(SCRIPT_OUT);
engine.Run();
