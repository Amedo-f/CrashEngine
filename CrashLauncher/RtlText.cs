using System.Numerics;
using System.Text;
using CrashEngine.Runtime.Screen;
using ImGuiNET;

namespace CrashLauncher;

// Amedo 2026-10-01
public static class RtlText
{
    private static readonly Dictionary<char, (char Isolated, char Final, char Initial, char Medial)> Forms = Build();

    private static Dictionary<char, (char, char, char, char)> Build()
    {
        var d = new Dictionary<char, (char, char, char, char)>();
        void Dual(char c, int first) => d[c] = ((char)first, (char)(first + 1), (char)(first + 2), (char)(first + 3));
        void Right(char c, int first) => d[c] = ((char)first, (char)(first + 1), '\0', '\0');
        d['ء'] = ('ﺀ', '\0', '\0', '\0');
        Right('آ', 0xFE81); Right('أ', 0xFE83); Right('ؤ', 0xFE85); Right('إ', 0xFE87);
        Dual('ئ', 0xFE89); Right('ا', 0xFE8D); Dual('ب', 0xFE8F); Right('ة', 0xFE93);
        Dual('ت', 0xFE95); Dual('ث', 0xFE99); Dual('ج', 0xFE9D); Dual('ح', 0xFEA1);
        Dual('خ', 0xFEA5); Right('د', 0xFEA9); Right('ذ', 0xFEAB); Right('ر', 0xFEAD);
        Right('ز', 0xFEAF); Dual('س', 0xFEB1); Dual('ش', 0xFEB5); Dual('ص', 0xFEB9);
        Dual('ض', 0xFEBD); Dual('ط', 0xFEC1); Dual('ظ', 0xFEC5); Dual('ع', 0xFEC9);
        Dual('غ', 0xFECD); Dual('ف', 0xFED1); Dual('ق', 0xFED5); Dual('ك', 0xFED9);
        Dual('ل', 0xFEDD); Dual('م', 0xFEE1); Dual('ن', 0xFEE5); Dual('ه', 0xFEE9);
        Right('و', 0xFEED); d['ى'] = ('ﻯ', 'ﻰ', 'ﯨ', 'ﯩ'); Dual('ي', 0xFEF1);
        Dual('پ', 0xFB56); Dual('چ', 0xFB7A); Right('ژ', 0xFB8A); Dual('ک', 0xFB8E);
        Dual('گ', 0xFB92); Dual('ی', 0xFBFC);
        return d;
    }

    private static bool Transparent(char c) => c is >= 'ً' and <= 'ٟ' or 'ٰ';

    private static bool JoinsForward(char c) => c == 'ـ' || (Forms.TryGetValue(c, out var f) && f.Initial != '\0');

    private static bool JoinsBack(char c) => c == 'ـ' || (Forms.TryGetValue(c, out var f) && f.Final != '\0');

    private static char LamAlef(char alef, bool final) => alef switch
    {
        'آ' => final ? 'ﻶ' : 'ﻵ',
        'أ' => final ? 'ﻸ' : 'ﻷ',
        'إ' => final ? 'ﻺ' : 'ﻹ',
        'ا' => final ? 'ﻼ' : 'ﻻ',
        _ => '\0',
    };

    public static bool HasRtl(string s)
    {
        foreach (char c in s) if (Bidi.IsRtl(c)) return true;
        return false;
    }

    public static string Visual(string logical) => Visual(logical, null);

    public static string Visual(string logical, List<int>? sources)
    {
        if (!HasRtl(logical))
        {
            if (sources is not null) { sources.Clear(); for (int i = 0; i < logical.Length; i++) sources.Add(i); }
            return logical;
        }
        var shaped = new StringBuilder(logical.Length);
        var shapedSources = new List<int>(logical.Length);
        for (int i = 0; i < logical.Length; i++)
        {
            char c = logical[i];
            if (!Forms.TryGetValue(c, out var forms))
            {
                shaped.Append(c);
                shapedSources.Add(i);
                continue;
            }
            int p = i - 1;
            while (p >= 0 && Transparent(logical[p])) p--;
            int n = i + 1;
            while (n < logical.Length && Transparent(logical[n])) n++;
            bool back = p >= 0 && JoinsForward(logical[p]) && JoinsBack(c);
            if (c == 'ل' && n < logical.Length && LamAlef(logical[n], back) is var ligature && ligature != '\0')
            {
                shaped.Append(ligature);
                shapedSources.Add(i);
                for (int k = i + 1; k < n; k++) { shaped.Append(logical[k]); shapedSources.Add(k); }
                i = n;
                continue;
            }
            bool forward = n < logical.Length && JoinsForward(c) && JoinsBack(logical[n]);
            char form = back && forward && forms.Medial != '\0' ? forms.Medial
                : back && forms.Final != '\0' ? forms.Final
                : forward && forms.Initial != '\0' ? forms.Initial
                : forms.Isolated;
            shaped.Append(form);
            shapedSources.Add(i);
        }

        var text = shaped.ToString();
        var runs = new List<(int Start, int Length, bool Rtl)>();
        bool paragraphRtl = Bidi.Runs(text, 0, text.Length, runs);
        var visual = new StringBuilder(text.Length);
        sources?.Clear();
        for (int r = 0; r < runs.Count; r++)
        {
            var (start, length, rtl) = runs[paragraphRtl ? runs.Count - 1 - r : r];
            if (!rtl)
            {
                for (int k = start; k < start + length; k++) { visual.Append(text[k]); sources?.Add(shapedSources[k]); }
                continue;
            }
            int end = start + length;
            while (end > start)
            {
                int clusterStart = end - 1;
                while (clusterStart > start && Transparent(text[clusterStart])) clusterStart--;
                for (int k = clusterStart; k < end; k++)
                {
                    char c = text[k];
                    visual.Append(c == '(' ? ')' : c == ')' ? '(' : c);
                    sources?.Add(shapedSources[k]);
                }
                end = clusterStart;
            }
        }
        return visual.ToString();
    }

    public static void TextWrappedRight(string logical)
    {
        if (!HasRtl(logical)) { ImGui.TextWrapped(logical); return; }
        var visual = Visual(logical);
        float width = ImGui.CalcTextSize(visual).X, avail = ImGui.GetContentRegionAvail().X;
        if (width < avail) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - width);
        ImGui.TextUnformatted(visual);
    }
}

// Amedo 2026-10-01
public static class RtlTextBox
{
    private sealed class State
    {
        public int Caret;
        public string Start = "";
    }

    private static uint _active;
    private static int _activeFrame;
    private static readonly Dictionary<uint, State> States = new();
    private static readonly List<int> Sources = new();

    public static bool Active => _active != 0 && ImGui.GetFrameCount() - _activeFrame <= 1;

    public static bool Draw(string label, ref string text, Vector2 size, out bool committed)
    {
        committed = false;
        uint id = ImGui.GetID(label);
        if (!States.TryGetValue(id, out var state)) States[id] = state = new State { Caret = text.Length };
        var style = ImGui.GetStyle();
        var min = ImGui.GetCursorScreenPos();
        if (size.X <= 0f) size.X = MathF.Max(ImGui.GetContentRegionAvail().X, 40f);
        var max = min + size;
        ImGui.InvisibleButton(label, size);
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        bool changed = false;
        bool active = _active == id;
        if (clicked && !active)
        {
            _active = id;
            active = true;
            state.Start = text;
        }
        else if (active && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !hovered || ImGui.IsKeyPressed(ImGuiKey.Escape)))
        {
            _active = 0;
            active = false;
            committed = text != state.Start;
        }

        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(min, max, ImGui.GetColorU32(active ? ImGuiCol.FrameBgActive : hovered ? ImGuiCol.FrameBgHovered : ImGuiCol.FrameBg), style.FrameRounding);
        float lineHeight = ImGui.GetTextLineHeight();
        var lines = text.Split('\n');

        if (active)
        {
            _activeFrame = ImGui.GetFrameCount();
            ImGui.SetNextFrameWantCaptureKeyboard(true);
            var io = ImGui.GetIO();
            bool ctrl = io.KeyCtrl;
            for (int i = 0; i < io.InputQueueCharacters.Size; i++)
            {
                char c = (char)io.InputQueueCharacters[i];
                if (c < 32 || ctrl) continue;
                text = text.Insert(state.Caret, c.ToString());
                state.Caret++;
                changed = true;
            }
            LineBounds(text, state.Caret, out int lineStart, out int lineEnd);
            bool rtlLine = RtlText.HasRtl(text[lineStart..lineEnd]);
            if (ImGui.IsKeyPressed(ImGuiKey.Backspace) && state.Caret > 0) { text = text.Remove(state.Caret - 1, 1); state.Caret--; changed = true; }
            if (ImGui.IsKeyPressed(ImGuiKey.Delete) && state.Caret < text.Length) { text = text.Remove(state.Caret, 1); changed = true; }
            if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter)) { text = text.Insert(state.Caret, "\n"); state.Caret++; changed = true; }
            if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow)) state.Caret = Math.Clamp(state.Caret + (rtlLine ? 1 : -1), 0, text.Length);
            if (ImGui.IsKeyPressed(ImGuiKey.RightArrow)) state.Caret = Math.Clamp(state.Caret + (rtlLine ? -1 : 1), 0, text.Length);
            if (ImGui.IsKeyPressed(ImGuiKey.Home)) state.Caret = lineStart;
            if (ImGui.IsKeyPressed(ImGuiKey.End)) state.Caret = lineEnd;
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.V) && ImGui.GetClipboardText() is { Length: > 0 } paste)
            {
                paste = paste.Replace("\r\n", "\n").Replace('\r', '\n');
                text = text.Insert(state.Caret, paste);
                state.Caret += paste.Length;
                changed = true;
            }
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.C)) ImGui.SetClipboardText(text);
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.X)) { ImGui.SetClipboardText(text); text = ""; state.Caret = 0; changed = true; }
            if (changed) lines = text.Split('\n');
        }
        state.Caret = Math.Clamp(state.Caret, 0, text.Length);

        list.PushClipRect(min, max, true);
        float y = min.Y + style.FramePadding.Y;
        int offset = 0;
        uint colour = ImGui.GetColorU32(ImGuiCol.Text);
        foreach (var line in lines)
        {
            var visual = RtlText.Visual(line, Sources);
            float width = ImGui.CalcTextSize(visual).X;
            bool rtl = RtlText.HasRtl(line);
            float x = rtl ? max.X - style.FramePadding.X - width : min.X + style.FramePadding.X;
            list.AddText(new Vector2(x, y), colour, visual);

            if (active && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hovered && ImGui.GetMousePos().Y >= y && ImGui.GetMousePos().Y < y + lineHeight)
                state.Caret = offset + CaretFromX(line, visual, x, ImGui.GetMousePos().X);
            if (active && state.Caret >= offset && state.Caret <= offset + line.Length)
            {
                float cx = CaretX(line, visual, x, state.Caret - offset, rtl);
                if ((int)(ImGui.GetTime() * 2.0) % 2 == 0) list.AddLine(new Vector2(cx, y), new Vector2(cx, y + lineHeight), colour, 1.5f);
            }
            offset += line.Length + 1;
            y += lineHeight;
        }
        list.PopClipRect();
        return changed;
    }

    private static void LineBounds(string text, int caret, out int start, out int end)
    {
        start = caret > 0 ? text.LastIndexOf('\n', caret - 1) + 1 : 0;
        end = text.IndexOf('\n', caret);
        if (end < 0) end = text.Length;
    }

    private static float PrefixWidth(string visual, int count) => count <= 0 ? 0f : ImGui.CalcTextSize(visual[..Math.Min(count, visual.Length)]).X;

    private static float CaretX(string line, string visual, float x, int caret, bool rtlLine)
    {
        if (visual.Length == 0) return x;
        for (int k = 0; k < visual.Length; k++)
        {
            int source = Sources[k];
            bool rtl = Bidi.IsRtl(visual[k]);
            if (source == caret) return x + PrefixWidth(visual, rtl ? k + 1 : k);
        }
        return caret >= line.Length ? (rtlLine ? x : x + PrefixWidth(visual, visual.Length)) : x;
    }

    private static int CaretFromX(string line, string visual, float x, float mouseX)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int k = 0; k <= visual.Length; k++)
        {
            float d = MathF.Abs(x + PrefixWidth(visual, k) - mouseX);
            if (d >= bestDistance) continue;
            bestDistance = d;
            if (k < visual.Length) best = Bidi.IsRtl(visual[k]) ? Sources[k] + 1 : Sources[k];
            else best = Bidi.IsRtl(visual[k - 1]) ? Sources[k - 1] : Sources[k - 1] + 1;
        }
        return Math.Clamp(best, 0, line.Length);
    }
}
