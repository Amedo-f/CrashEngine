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
        const string original = "Original (disc)";
        if (ImGui.BeginCombo("Game texts##langcombo", proj.Language ?? original))
        {
            if (ImGui.Selectable(original, proj.Language is null)) { proj.Language = null; proj.Save(); }
            foreach (var n in names)
                if (ImGui.Selectable(n, string.Equals(proj.Language, n, StringComparison.OrdinalIgnoreCase))) { proj.Language = n; proj.Save(); }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("The texts the game shows (hints at the bottom of the screen, later menus and credits).\n" +
                         "Each language is a folder in <project>\\Language with AgentLab.txt (hints), Code.txt (menus)\n" +
                         "and Credits.txt: one text per non-empty line, in the SAME order as the original (UTF-8).\n" +
                         "Letters the game's own font doesn't have (Arabic, Cyrillic, Japanese...) are drawn from the\n" +
                         "folder's .ttf/.otf font (or the shipped Fonts\\Lalezar-Regular.ttf) in the game's style.\n" +
                         "A missing file falls back to the original text. Used by Play Scene and Build to PC.");

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
}
