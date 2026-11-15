using CrashEngine.Core;
using CrashEngineHub;
using CrashLauncher;

// Amedo 2026-11-12
Engine.CustomStyleSetup = HubTheme.Apply; // Amedo 2026-11-14
Engine.CustomFontSetup = atlas =>
{
    if (EditorFonts.AddUiFont(atlas) is not { } font) return; // Amedo 2026-11-14
    HubUi.TitleFont = atlas.AddFontFromFileTTF(font, 28f, EditorFonts.SharpConfig(), EditorFonts.Ranges(EditorFonts.UiRanges));
};

var engine = new Engine("CrashEngine Hub", 1100, 680);
try
{
    var icons = CrashEngine.Renderer.Texture2D.LoadWindowIcons(Path.Combine(AppContext.BaseDirectory, "Assets", "Icons"), "hub-icon"); // Amedo 2026-11-15
    if (icons.Length > 0) engine.SetWindowIcon(icons);
}
catch (Exception ex)
{
    Console.WriteLine($"[hub] icon: {ex.Message}");
}

engine.ActiveScene = new HubScene();
engine.Run();
