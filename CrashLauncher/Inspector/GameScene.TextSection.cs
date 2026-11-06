using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Runtime.Screen;
using ImGuiNET;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace CrashLauncher;

// Amedo 2026-10-01
public sealed partial class GameScene
{
    private const int DisplayBottomTextCommand = 603, DisplayBottomTextInstanceCommand = 657, ActorSubtypeEqualsCondition = 47;

    private readonly ScreenDraw _textPreviewDraw = new();
    private GameScreen? _textOriginal;
    private GameScreen? _textPreview;
    private string? _textPreviewLanguage;
    private TextStyles? _textStyles;
    private (string Key, TtfGlyphs? Glyphs) _textPreviewGlyphs;
    private readonly Dictionary<int, object> _textPreviewSlots = new();
    private readonly Dictionary<(string? Language, int Line), string> _textEdits = new();

    private string DiscCrash6 => Path.Combine(_extractedRoot, "Crash6");

    private static bool TextTyping => ImGui.GetIO().WantTextInput || RtlTextBox.Active || CodeEditor.CodeEditor.Active; // Amedo 2026-11-04

    private List<int> TextLinesOf(InstanceData inst, PS2AnyObject? obj, BaseTwinSection? localBehaviours, BaseTwinSection? globalBehaviours, out bool textMaster)
    {
        textMaster = false;
        var lines = new List<int>();
        if (obj is null) return lines;
        var graphs = new List<PS2BehaviourGraph>();
        foreach (uint id in obj.RefBehaviours)
        {
            if (localBehaviours?.ContainsItem(id) == true && localBehaviours.GetItem<ITwinItem>(id) is PS2BehaviourGraph lg) graphs.Add(lg);
            else if (globalBehaviours?.ContainsItem(id) == true && globalBehaviours.GetItem<ITwinItem>(id) is PS2BehaviourGraph gg) graphs.Add(gg);
        }
        static IEnumerable<PS2BehaviourCommand> Commands(PS2BehaviourStateBody body) => body.Commands.OfType<PS2BehaviourCommand>();
        foreach (var g in graphs)
            foreach (var state in g.ScriptStates)
                foreach (var body in state.Bodies.OfType<PS2BehaviourStateBody>())
                    if (Commands(body).Any(c => c.CommandIndex == DisplayBottomTextInstanceCommand)) textMaster = true;

        if (textMaster && inst.Source.ParamList3.Count > 0)
        {
            uint v = inst.Source.ParamList3[0];
            lines.Add(GameScreen.InstanceLine(v));
            foreach (var g in graphs)
                foreach (var state in g.ScriptStates)
                    foreach (var body in state.Bodies.OfType<PS2BehaviourStateBody>())
                    {
                        if (body.Condition is not { ConditionIndex: ActorSubtypeEqualsCondition } cond || cond.Parameter != v) continue;
                        var reached = Commands(body).ToList();
                        if (body.HasStateJump && body.JumpToState >= 0 && body.JumpToState < g.ScriptStates.Count)
                            foreach (var next in g.ScriptStates[body.JumpToState].Bodies.OfType<PS2BehaviourStateBody>())
                                reached.AddRange(Commands(next));
                        foreach (var c in reached)
                            if (c.CommandIndex == DisplayBottomTextCommand && c.Arguments.Count > 0) lines.Add((int)c.Arguments[0]);
                    }
        }
        else
        {
            foreach (var g in graphs)
                foreach (var state in g.ScriptStates)
                    foreach (var body in state.Bodies.OfType<PS2BehaviourStateBody>())
                        foreach (var c in Commands(body))
                            if (c.CommandIndex == DisplayBottomTextCommand && c.Arguments.Count > 0) lines.Add((int)c.Arguments[0]);
        }
        return lines.Distinct().ToList();
    }

    private GameScreen OriginalTexts() => _textOriginal ??= GameScreen.Load(DiscCrash6);

    private GameScreen PreviewScreen(string? languageDirectory)
    {
        if (_textPreview is not null && _textPreviewLanguage == languageDirectory) return _textPreview;
        _textPreview?.Dispose();
        _textPreview = GameScreen.Load(DiscCrash6, null, languageDirectory);
        _textPreviewLanguage = languageDirectory;
        _textStyles = TextStyles.Load(languageDirectory);
        _textEdits.Clear();
        return _textPreview;
    }

    private GameFont.IFallback? PreviewGlyphs(string? languageDirectory, TextStyle style)
    {
        var key = (languageDirectory ?? "") + "|" + style.Key;
        if (_textPreviewGlyphs.Key == key) return _textPreviewGlyphs.Glyphs;
        if (_textPreviewGlyphs.Glyphs is { } old)
        {
            _textPreviewDraw.Forget(old);
            old.Dispose();
        }
        var path = GameScreen.ResolveFont(languageDirectory, style.Font);
        _textPreviewGlyphs = (key, path is null ? null : TtfGlyphs.Load(path, style, _browser.Log));
        return _textPreviewGlyphs.Glyphs;
    }

    private void DrawTextSection(Entity e, InstanceData inst, PS2AnyObject? obj, BaseTwinSection? localBehaviours, BaseTwinSection? globalBehaviours)
    {
        var lines = TextLinesOf(inst, obj, localBehaviours, globalBehaviours, out bool textMaster);
        if (lines.Count == 0) return;
        if (!ImGui.CollapsingHeader("Text##objtext", ImGuiTreeNodeFlags.DefaultOpen)) return;
        var proj = GetGameProject();
        var language = LanguageDirectory(proj?.Language);
        var original = OriginalTexts().AgentLabTexts;
        var screen = PreviewScreen(language);
        var texts = screen.AgentLabTexts;
        var styles = _textStyles ??= new TextStyles();
        var chunk = SourceOf(e) is { Rm2Path: { } rm2 } ? Path.ChangeExtension(rm2, null) : "";
        string objectKey = TextStyles.ObjectKey(chunk, inst.Source.GetID());

        ImGui.TextDisabled($"Language: {proj?.Language ?? "Original (disc)"}  (Game Settings -> Language)");
        if (language is null)
        {
            ImGui.TextWrapped("The original texts come from the disc and are read-only. Make an editable copy to change the words or the style:");
            if (ImGui.Button("Make an editable English copy##textcopy") && proj is not null)
            {
                try
                {
                    GameScreen.ExportOriginalTexts(DiscCrash6, Path.Combine(LanguagesRoot, "English"));
                    proj.Language = "English";
                    proj.Save();
                    _browser.Log($"Text: {Path.Combine(LanguagesRoot, "English")} created from the disc and selected.");
                }
                catch (Exception ex)
                {
                    _browser.Log($"Text: could not create the English copy: {ex.Message}");
                }
            }
        }

        bool ownStyle = styles.Objects.ContainsKey(objectKey);
        var style = styles.For(objectKey);
        foreach (int line in lines)
        {
            ImGui.PushID(line);
            ImGui.Separator();
            ImGui.Text($"Line {line}");
            ImGui.PushTextWrapPos(0f);
            ImGui.TextDisabled($"Original: {original?[line] ?? ""}");
            ImGui.PopTextWrapPos();
            string shown = texts?[line] ?? "";
            if (language is not null)
            {
                var editKey = (language, line);
                if (!_textEdits.TryGetValue(editKey, out var buffer)) buffer = shown.Replace('~', '\n');
                if (RtlTextBox.Draw("##textedit", ref buffer, new Vector2(-1f, ImGui.GetTextLineHeight() * 3.2f), out bool committed))
                    _textEdits[editKey] = buffer;
                if (committed && _textEdits.TryGetValue(editKey, out var edited))
                {
                    try
                    {
                        GameTexts.SetLine(Path.Combine(language, GameTexts.AgentLabFile), line, edited);
                        _textEdits.Remove(editKey);
                        _textPreviewLanguage = null;
                        _browser.Log($"Text: line {line} saved to {Path.Combine(language, GameTexts.AgentLabFile)}.");
                    }
                    catch (Exception ex)
                    {
                        _browser.Log($"Text: line {line} not saved: {ex.Message}");
                    }
                }
                if (_textEdits.TryGetValue(editKey, out var pending)) shown = pending.Replace("\r\n", "~").Replace('\n', '~');
            }
            if (screen.Font is { } font)
            {
                if (!_textPreviewSlots.TryGetValue(line, out var slot)) _textPreviewSlots[line] = slot = new object();
                var min = ImGui.GetCursorScreenPos();
                var size = new Vector2(MathF.Max(ImGui.GetContentRegionAvail().X, 60f), 58f);
                ImGui.Dummy(size);
                _textPreviewDraw.DrawPreview(font, PreviewGlyphs(language, style), shown, ImGui.GetWindowDrawList(), min, min + size, slot);
            }
            ImGui.PopID();
        }

        if (textMaster && inst.Source.ParamList3.Count > 0 && texts is not null)
        {
            ImGui.Separator();
            int current = GameScreen.InstanceLine(inst.Source.ParamList3[0]);
            static string Short(string s) => RtlText.Visual(s.Length > 42 ? s[..42] + "..." : s);
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.BeginCombo("##textline", $"Show line {current}: {Short(texts[current])}"))
            {
                for (int n = 30; n < texts.Count; n++)
                {
                    if (n is > 40 and < 52) continue;
                    if (ImGui.Selectable($"{n}: {Short(texts[n])}##line{n}", n == current))
                        inst.Source.ParamList3[0] = (uint)(n < 41 ? n - 30 : n - 41);
                }
                ImGui.EndCombo();
            }
            if (language is not null && ImGui.Button("New text for this object##textnew"))
            {
                try
                {
                    int index = texts.Count;
                    foreach (var dir in Directory.GetDirectories(LanguagesRoot))
                        GameTexts.SetLine(Path.Combine(dir, GameTexts.AgentLabFile), index, "new text");
                    inst.Source.ParamList3[0] = (uint)(index - 41);
                    _textPreviewLanguage = null;
                    _browser.Log($"Text: line {index} added to every language and linked to this object (Save Chunk to keep the link). PC only: the PS2 disc text has no such line.");
                }
                catch (Exception ex)
                {
                    _browser.Log($"Text: could not add a line: {ex.Message}");
                }
            }
        }

        if (language is null) return;
        ImGui.SeparatorText("Style");
        if (ImGui.RadioButton("Language default (all texts)##styledefault", !ownStyle) && ownStyle)
        {
            styles.Objects.Remove(objectKey);
            SaveTextStyles(language, styles);
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("This object only##styleobject", ownStyle) && !ownStyle)
        {
            styles.Objects[objectKey] = styles.Default.Clone();
            SaveTextStyles(language, styles);
        }
        style = styles.For(objectKey);
        if (DrawStyleEditor(language, style)) SaveTextStyles(language, styles);
    }

    private void SaveTextStyles(string language, TextStyles styles)
    {
        try
        {
            styles.Save(language);
        }
        catch (Exception ex)
        {
            _browser.Log($"Text: style not saved: {ex.Message}");
        }
    }

    private bool DrawStyleEditor(string language, TextStyle s)
    {
        bool commit = false;
        void Done()
        {
            if (ImGui.IsItemDeactivatedAfterEdit()) commit = true;
        }

        var fonts = GameScreen.FontFiles(language).Select(Path.GetFileName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var defaultFont = Path.GetFileName(GameScreen.FallbackFontPath(language)) ?? "none";
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##stylefont", $"Font: {s.Font ?? $"default ({defaultFont})"}"))
        {
            if (ImGui.Selectable($"default ({defaultFont})", s.Font is null)) { s.Font = null; commit = true; }
            foreach (var f in fonts)
                if (ImGui.Selectable(f, string.Equals(s.Font, f, StringComparison.OrdinalIgnoreCase))) { s.Font = f; commit = true; }
            ImGui.EndCombo();
        }

        bool latin = s.GameFontForLatin;
        if (ImGui.Checkbox("Game font for English letters, digits and buttons##stylelatin", ref latin)) { s.GameFontForLatin = latin; commit = true; }

        float size = s.Size, letter = s.LetterSpacing, word = s.WordSpacing;
        if (ImGui.SliderFloat("Size##stylesize", ref size, 0.5f, 2.5f, "%.2f")) s.Size = size;
        Done();
        if (ImGui.SliderFloat("Letter spacing##styleletter", ref letter, -3f, 8f, "%.1f")) s.LetterSpacing = letter;
        Done();
        if (ImGui.SliderFloat("Word spacing##styleword", ref word, 0.3f, 3f, "%.2f")) s.WordSpacing = word;
        Done();

        ImGui.SeparatorText("Fill");
        var top = TextStyle.Colour(s.FillTop);
        if (ImGui.ColorEdit3("Top colour##stylefilltop", ref top)) s.FillTop = TextStyle.Hex(top);
        Done();
        var bottom = TextStyle.Colour(s.FillBottom);
        if (ImGui.ColorEdit3("Bottom colour##stylefillbottom", ref bottom)) s.FillBottom = TextStyle.Hex(bottom);
        Done();
        float g0 = s.GradientStart, g1 = s.GradientEnd;
        if (ImGui.DragFloatRange2("Gradient from/to##stylegradient", ref g0, ref g1, 0.01f, 0f, 1f, "%.2f", "%.2f")) { s.GradientStart = g0; s.GradientEnd = g1; }
        Done();

        ImGui.SeparatorText("Outline");
        bool outline = s.Outline;
        if (ImGui.Checkbox("Outline##styleoutline", ref outline)) { s.Outline = outline; commit = true; }
        var outlineColour = TextStyle.Colour(s.OutlineColour);
        if (ImGui.ColorEdit3("Outline colour##styleoutlinecolour", ref outlineColour)) s.OutlineColour = TextStyle.Hex(outlineColour);
        Done();
        float outlineWidth = s.OutlineWidth;
        if (ImGui.SliderFloat("Outline width##styleoutlinewidth", ref outlineWidth, 0f, 5f, "%.1f")) s.OutlineWidth = outlineWidth;
        Done();

        ImGui.SeparatorText("Shadow");
        bool shadow = s.Shadow;
        if (ImGui.Checkbox("Shadow##styleshadow", ref shadow)) { s.Shadow = shadow; commit = true; }
        var shadowColour = TextStyle.Colour(s.ShadowColour);
        if (ImGui.ColorEdit3("Shadow colour##styleshadowcolour", ref shadowColour)) s.ShadowColour = TextStyle.Hex(shadowColour);
        Done();
        float opacity = s.ShadowOpacity, width = s.ShadowWidth, softness = s.ShadowSoftness;
        var offset = new Vector2(s.ShadowX, s.ShadowY);
        if (ImGui.SliderFloat("Shadow opacity##styleshadowopacity", ref opacity, 0f, 1f, "%.2f")) s.ShadowOpacity = opacity;
        Done();
        if (ImGui.SliderFloat2("Shadow offset X/Y##styleshadowoffset", ref offset, -8f, 8f, "%.1f")) { s.ShadowX = offset.X; s.ShadowY = offset.Y; }
        Done();
        if (ImGui.SliderFloat("Shadow width##styleshadowwidth", ref width, 0f, 5f, "%.1f")) s.ShadowWidth = width;
        Done();
        if (ImGui.SliderFloat("Shadow softness##styleshadowsoft", ref softness, 0f, 4f, "%.1f")) s.ShadowSoftness = softness;
        Done();

        if (ImGui.Button("Reset to the game style##stylereset"))
        {
            var fresh = new TextStyle();
            foreach (var p in typeof(TextStyle).GetProperties().Where(p => p.CanWrite)) p.SetValue(s, p.GetValue(fresh));
            commit = true;
        }
        return commit;
    }
}
