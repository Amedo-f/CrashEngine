using System.Diagnostics;
using CrashEngine.Runtime.Screen;
using ImGuiNET;

namespace CrashLauncher;

// Amedo 2026-10-01
public sealed partial class GameScene
{
    private string _newLanguage = "";

    private string LanguagesRoot => Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Language");

    private string? LanguageDirectory(string? name) =>
        string.IsNullOrWhiteSpace(name) || !Directory.Exists(Path.Combine(LanguagesRoot, name)) ? null : Path.Combine(LanguagesRoot, name);

    private void DrawLanguage()
    {
        if (!ImGui.CollapsingHeader("Language##gamelanguage")) return;
        var proj = GetGameProject();
        if (proj is null) { ImGui.TextDisabled("No project loaded."); return; }
        var names = Directory.Exists(LanguagesRoot)
            ? Directory.GetDirectories(LanguagesRoot).Select(Path.GetFileName).OfType<string>().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
        if (CrashEngine.Runtime.PlaySettings.IsPalDisc(_extractedRoot)) DrawDiscLanguage(proj); // Amedo 2026-11-11
        const string original = "Original (disc)";
        if (ImGui.BeginCombo("Game texts##langcombo", proj.Language ?? original))
        {
            if (ImGui.Selectable(original, proj.Language is null)) { proj.Language = null; proj.Save(); }
            foreach (var n in names)
                if (ImGui.Selectable(n, string.Equals(proj.Language, n, StringComparison.OrdinalIgnoreCase))) { proj.Language = n; proj.Save(); }
            ImGui.EndCombo();
        }

        ImGui.SetNextItemWidth(140f);
        ImGui.InputTextWithHint("##newlanguage", "e.g. Arabic", ref _newLanguage, 40);
        ImGui.SameLine();
        if (ImGui.Button("New language from original##langnew") && _newLanguage.Trim() is { Length: > 0 } name &&
            name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
        {
            try
            {
                var dir = Path.Combine(LanguagesRoot, name);
                int written = GameScreen.ExportOriginalTexts(Path.Combine(_extractedRoot, "Crash6"), dir);
                proj.Language = name;
                proj.Save();
                _newLanguage = "";
                _browser.Log($"Language: {dir} ready ({written} original text file(s) copied; existing files kept). Translate line by line, then Play Scene.");
            }
            catch (Exception ex)
            {
                _browser.Log($"Language: could not create it: {ex.Message}");
            }
        }
        if (LanguageDirectory(proj.Language) is { } current)
        {
            ImGui.SameLine();
            if (ImGui.Button("Open folder##langopen")) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{current}\"") { UseShellExecute = true });
            var font = GameScreen.FallbackFontPath(current);
            ImGui.TextDisabled($"Font for missing letters: {(font is null ? "none" : Path.GetFileName(font))}");
        }
        ImGui.TextDisabled("Words and style (font, colours, outline, shadow): select a text object\n(e.g. a UTIL_TEXTMASTER) -> Inspector -> Text.");
    }

    // Amedo 2026-11-11
    private static void DrawDiscLanguage(CrashProject proj)
    {
        string system = $"Device language ({CrashEngine.Runtime.Region.Pal.Language.DiscLanguage(null)})";
        if (!ImGui.BeginCombo("Disc language##disclang", proj.DiscLanguage ?? system)) return;
        if (ImGui.Selectable(system, proj.DiscLanguage is null)) { proj.DiscLanguage = null; proj.Save(); }
        foreach (var l in CrashEngine.Runtime.PlaySettings.PalLanguages)
            if (ImGui.Selectable(l, string.Equals(proj.DiscLanguage, l, StringComparison.OrdinalIgnoreCase))) { proj.DiscLanguage = l; proj.Save(); }
        ImGui.EndCombo();
    }
}
