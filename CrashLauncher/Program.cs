using CrashEngine.Core;
using CrashEngine.Importer;
using CrashLauncher;
using System.Runtime.InteropServices;

string SCRIPT_OUT = Path.Combine(AppContext.BaseDirectory, "Scripts");

Engine.CustomStyleSetup = EditorTheme.Apply; // Amedo
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
                ushort[] rangesManaged = { 0x0020, 0x00FF, 0x0600, 0x06FF, 0x0750, 0x077F, 0x2010, 0x2027, 0x2030, 0x2044, 0xFB50, 0xFDFF, 0xFE70, 0xFEFF, 0 }; // Amedo 2026-10-01
                var rangesPtr = (ushort*)Marshal.AllocHGlobal(rangesManaged.Length * sizeof(ushort));
                for (int i = 0; i < rangesManaged.Length; i++) rangesPtr[i] = rangesManaged[i];
                atlas.AddFontFromFileTTF(sysFont, 16f, default, (IntPtr)rangesPtr);
                // Amedo 2026-11-04: fixed-width font for the code editor (Consolas), with the Arabic letters taken from the UI font
                var mono = Path.Combine(sysFontsDir, "consola.ttf");
                if (File.Exists(mono))
                {
                    ushort[] latin = { 0x0020, 0x00FF, 0x2010, 0x2027, 0 };
                    var latinPtr = (ushort*)Marshal.AllocHGlobal(latin.Length * sizeof(ushort));
                    for (int i = 0; i < latin.Length; i++) latinPtr[i] = latin[i];
                    var monoFont = atlas.AddFontFromFileTTF(mono, 15f, default, (IntPtr)latinPtr);
                    ushort[] arabic = { 0x0600, 0x06FF, 0x0750, 0x077F, 0xFB50, 0xFDFF, 0xFE70, 0xFEFF, 0 };
                    var arabicPtr = (ushort*)Marshal.AllocHGlobal(arabic.Length * sizeof(ushort));
                    for (int i = 0; i < arabic.Length; i++) arabicPtr[i] = arabic[i];
                    var merge = ImGuiNET.ImGuiNative.ImFontConfig_ImFontConfig();
                    merge->MergeMode = 1;
                    atlas.AddFontFromFileTTF(sysFont, 15f, merge, (IntPtr)arabicPtr);
                    CrashLauncher.CodeEditor.CodeEditor.MonoFont = monoFont;
                }
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
};

GraphicsSettings.UseFile(null); // Amedo
CrashEngine.Renderer.SceneTarget.ReuseHeldFrames = true;
var engine = new Engine("CrashEngine", 1280, 720);

// Amedo 2026-09-22
try
{
    var iconDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons");
    var icons = new List<(int, int, byte[])>();
    foreach (var sz in new[] { 16, 32, 48 })
    {
        var rgba = CrashEngine.Renderer.Texture2D.LoadRgba(Path.Combine(iconDir, $"app-icon-{sz}.png"), out int w, out int h);
        if (rgba != null) icons.Add((w, h, rgba));
    }
    if (icons.Count > 0) engine.SetWindowIcon(icons.ToArray());
}
catch { }

JitWarmup.Start(typeof(CrashEngine.Runtime.SceneSimulation).Assembly, typeof(MeshDecoder).Assembly, // Amedo 2026-10-01
    typeof(CrashEngine.Renderer.RenderPipeline).Assembly, typeof(Engine).Assembly, typeof(Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2).Assembly); // Amedo

engine.ActiveScene = new ProjectSetupScene(SCRIPT_OUT);
engine.Run();
