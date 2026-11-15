using CrashEngine.Core;
using CrashEngine.Importer;
using CrashLauncher;
using System.Runtime.InteropServices;

string SCRIPT_OUT = Path.Combine(AppContext.BaseDirectory, "Scripts");

if (args.Length == 0 && ProjectCreator.StartHub()) return; // Amedo 2026-11-12

Engine.CustomStyleSetup = EditorTheme.Apply; // Amedo
Engine.CustomFontSetup = atlas =>
{
    try
    {
        var sysFont = EditorFonts.AddUiFont(atlas); // Amedo 2026-11-12
        if (sysFont is not null)
        {
            unsafe
            {
                // Amedo 2026-11-04
                if (EditorFonts.MonoFontPath() is { } mono) // Amedo 2026-11-14
                {
                    var monoFont = atlas.AddFontFromFileTTF(mono, 15f, default, EditorFonts.Ranges([0x0020, 0x00FF, 0x2010, 0x2027, 0]));
                    var merge = ImGuiNET.ImGuiNative.ImFontConfig_ImFontConfig();
                    merge->MergeMode = 1;
                    atlas.AddFontFromFileTTF(sysFont, 15f, merge, EditorFonts.Ranges([0x0600, 0x06FF, 0x0750, 0x077F, 0xFB50, 0xFDFF, 0xFE70, 0xFEFF, 0]));
                    CrashLauncher.CodeEditor.CodeEditor.MonoFont = monoFont;
                }
            }
            SplashScene.AddFonts(atlas, sysFont); // Amedo 2026-11-12
            Console.WriteLine($"Font setup: loaded {Path.GetFileName(sysFont)} with extended glyph range (em-dash etc. now render correctly).");
        }
        else
        {
            Console.WriteLine("Font setup: no system TTF found (segoeui/arial/DejaVuSans/NotoSans) — falling back to ImGui's ASCII-only default font (em-dashes etc. will show as '?').");
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

int openArg = Array.FindIndex(args, a => a.Equals("--open", StringComparison.OrdinalIgnoreCase)); // Amedo 2026-11-12
int createArg = Array.FindIndex(args, a => a.Equals("--create", StringComparison.OrdinalIgnoreCase)); // Amedo 2026-11-12
string? openError = openArg >= 0 && openArg + 1 < args.Length ? ProjectCreator.Open(args[openArg + 1]) : "";
if (createArg >= 0 && createArg + 3 < args.Length) // Amedo 2026-11-12
{
    openError = ProjectCreator.Validate(args[createArg + 3], args[createArg + 2], args[createArg + 1], out var created);
    if (openError is null) engine.ActiveScene = new SplashScene(created, SCRIPT_OUT);
}
int resumeArg = Array.FindIndex(args, a => a.Equals("--resume", StringComparison.OrdinalIgnoreCase)); // Amedo 2026-11-12
if (resumeArg >= 0 && resumeArg + 1 < args.Length)
{
    if (CrashProject.Open(args[resumeArg + 1]) is { } unfinished) { engine.ActiveScene = new SplashScene(unfinished, SCRIPT_OUT); openError = null; }
    else openError = $"Not a project: {args[resumeArg + 1]}";
}
if (openError is not null)
{
    if (openError.Length > 0) Console.WriteLine($"--open: {openError}");
    engine.ActiveScene = new ProjectSetupScene(SCRIPT_OUT);
}
engine.Run();
