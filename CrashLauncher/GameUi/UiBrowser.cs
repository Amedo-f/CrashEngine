using CrashEngine.Core;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private string _uiCategory = "Loading Screens";
    private string _uiLanguage = "English";
    private readonly Dictionary<string, List<Texture2D>> _uiTextureCache = new();
    private readonly Dictionary<string, List<string>>    _uiLangFileCache = new();

    private void DrawUiBrowser()
    {
        DrawUiZoomOverlay();
        if (!_showUiBrowser) return;

        ImGui.SetNextWindowSize(new Vector2(720f, 520f), ImGuiCond.FirstUseEver);
        if (!_windowChrome.Begin("UI Browser##uibrowser", ref _showUiBrowser)) return;

        ImGui.TextDisabled("Real assets decoded straight from the disc — same pieces the game");
        ImGui.TextDisabled("itself shows. No on-screen layout data exists in these files (see");
        ImGui.TextDisabled("tooltip below), so this is the individual art, not a live mockup.");
        ImGui.Separator();

        ImGui.BeginChild("##uiCatList", new Vector2(170f, -1f), ImGuiChildFlags.Border);
        string[] categories = { "Boot Sequence", "Legal / Copyright", "Loading Screens", "Level Titles", "Icons", "Decal", "Fonts", "FMV / Cutscenes", "Effects" };
        foreach (var cat in categories)
            if (ImGui.Selectable(cat, _uiCategory == cat))
                _uiCategory = cat;
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##uiContent", Vector2.Zero, ImGuiChildFlags.Border);
        switch (_uiCategory)
        {
            case "Boot Sequence":
                DrawUiBootSequence();
                break;
            case "Legal / Copyright":
                DrawUiLegal();
                break;
            case "Loading Screens":
                DrawUiPsmGroup(new[]
                {
                    @"Language\Loading\Loading1.psm",
                    @"Language\Loading\Loading2.psm",
                    @"Language\Loading\Loading3.psm",
                }, kind: UiAssetKind.SplitFrames, flipV: true);
                break;
            case "Level Titles":
                DrawUiLevelTitles();
                break;
            case "Icons":
                DrawUiPsmGroup(new[] { @"Startup\Icons.psm" }, flipV: true);
                break;
            case "Decal":
                DrawUiPtc(@"Startup\Decal.ptc", flipV: true);
                break;
            case "Fonts":
                DrawUiPsfGroup(new[] { @"Startup\Fonts\Arial.psf", System.IO.File.Exists(System.IO.Path.Combine(_extractedRoot, CrashEngine.Runtime.PlaySettings.PalExe)) ? CrashEngine.Runtime.Screen.GameScreen.PalFontPath : CrashEngine.Runtime.Screen.GameScreen.FontPath }); // Amedo
                break;
            case "FMV / Cutscenes":
                DrawUiFmvList();
                break;
            case "Effects":
                DrawEffectsBrowser();
                break;
        }
        ImGui.EndChild();

        ImGui.End();
    }

    private void DrawUiBootSequence()
    {
        ImGui.TextWrapped("Real boot order, reconstructed from what's actually on disc:");
        ImGui.Separator();

        DrawUiBootPatch();
        ImGui.Separator();
        DrawUiLegalBlackout();
        ImGui.Separator();

        ImGui.Text("1. Legal / Copyright screen");
        DrawUiPsmGroup(GetLegalPsmPaths(), kind: UiAssetKind.SplitFrames, flip180: true); // Amedo 2026-09-24

        ImGui.Spacing();
        ImGui.Text("2. Vivendi Universal splash (video)");
        DrawUiVideoPlaceholder(@"FMV\VIVENDI.PSS");

        ImGui.Spacing();
        ImGui.Text("3. Traveller's Tales logo (video)");
        DrawUiVideoPlaceholder(@"FMV\TTIDENT.PSS");

        ImGui.Spacing();
        ImGui.Text("4. Loading screen");
        DrawUiPsmGroup(new[] { @"Language\Loading\Loading1.psm" }, kind: UiAssetKind.SplitFrames);
    }

    private void DrawUiVideoPlaceholder(string relativePath)
    {
        var full = GamePaths.OnDisk(_extractedRoot, relativePath);
        if (System.IO.File.Exists(full))
        {
            var info = new System.IO.FileInfo(full);
            ImGui.TextDisabled($"  {relativePath}  ({info.Length / 1024.0 / 1024.0:F1} MB) — real .PSS video file on disc.");
            if (ImGui.Button($"Play (external player)##bootplay_{relativePath}"))
                PlayFmvExternal(full);
        }
        else
        {
            ImGui.TextDisabled($"  {relativePath} — not found at this disc path.");
        }
    }
}
