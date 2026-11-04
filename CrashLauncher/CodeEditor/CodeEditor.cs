using System.Numerics;
using System.Text;
using ImGuiNET;

namespace CrashLauncher.CodeEditor;

// Amedo 2026-11-04: our own code text box: coloured lines (ICodeLanguage), line numbers, caret and selection, typing,
// clipboard, undo/redo. Only the visible lines are drawn, so long scripts stay fast. The text comes back exactly as it
// went in (same line endings) until the user changes it.
public sealed partial class CodeEditor
{
    public static ImFontPtr MonoFont;                 // set at font setup (Consolas); the editor falls back to the current font
    private static unsafe bool HasMonoFont => MonoFont.NativePtr != null;
    public static bool Active => _activeId != 0 && ImGui.GetFrameCount() - _activeFrame <= 1;
    private static uint _activeId;
    private static int _activeFrame;

    public ICodeLanguage? Language { get; set; }
    public CodePalette Palette { get; set; } = new();
    public int TabSize { get; set; } = 3;              // the script text indents with 3 spaces
    public bool ReadOnly { get; set; }
    public int ErrorLine { get; set; } = -1;           // 0-based; -1 = none
    public string? ErrorText { get; set; }

    public bool Changed { get; private set; }           // the text changed this frame
    public int CaretLine => _caret.Line;
    public int CaretColumn => _caret.Col;
    public int LineCount => _lines.Count;

    private readonly struct Pos(int line, int col) : IComparable<Pos>
    {
        public int Line { get; } = line;
        public int Col { get; } = col;
        public int CompareTo(Pos o) => Line != o.Line ? Line.CompareTo(o.Line) : Col.CompareTo(o.Col);
        public static bool operator <(Pos a, Pos b) => a.CompareTo(b) < 0;
        public static bool operator >(Pos a, Pos b) => a.CompareTo(b) > 0;
        public static bool operator ==(Pos a, Pos b) => a.CompareTo(b) == 0;
        public static bool operator !=(Pos a, Pos b) => a.CompareTo(b) != 0;
        public override bool Equals(object? o) => o is Pos p && p == this;
        public override int GetHashCode() => Line * 65599 + Col;
    }

    private readonly List<string> _lines = new() { "" };
    private string _newline = "\n";
    private Pos _caret, _anchor;
    private float _wantX = -1f;                         // column kept when moving up/down
    private bool _scrollToCaret;
    private bool _dragging;
    private string? _textCache;
    private readonly List<Token> _tokens = new();

    public string Text
    {
        get => _textCache ??= string.Join(_newline, _lines);
        set => SetText(value);
    }

    public void SetText(string text)
    {
        _newline = text.Contains("\r\n") && !HasLoneLf(text) ? "\r\n" : "\n";
        _lines.Clear();
        _lines.AddRange((_newline == "\r\n" ? text.Replace("\r\n", "\n") : text).Split('\n'));
        _caret = _anchor = new Pos(0, 0);
        _undo.Clear();
        _redo.Clear();
        _textCache = text;
        ErrorLine = -1;
        ErrorText = null;
    }

    private static bool HasLoneLf(string s)
    {
        for (int i = 0; i < s.Length; i++) if (s[i] == '\n' && (i == 0 || s[i - 1] != '\r')) return true;
        return false;
    }

    // ---------------------------------------------------------------- drawing + input

    public void Render(string id, Vector2 size)
    {
        Changed = false;
        bool mono = HasMonoFont;
        if (mono) ImGui.PushFont(MonoFont);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Palette.Background);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        uint myId = ImGui.GetID(id);
        ImGui.BeginChild(id, size, ImGuiChildFlags.Border, ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove);

        var font = ImGui.GetFont();
        float charW = font.GetCharAdvance('M');
        float lineH = MathF.Floor(ImGui.GetFontSize() + 3f);
        float gutterW = (Math.Max(3, _lines.Count.ToString().Length) + 2) * charW;
        float textX0 = gutterW + charW;

        var origin = ImGui.GetCursorScreenPos();         // content top-left, already moved by the scroll
        var winPos = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();
        bool focused = _activeId == myId;
        bool hovered = ImGui.IsWindowHovered();

        // content size (for the scroll bars)
        float widest = 0f;
        foreach (var l in _lines) widest = MathF.Max(widest, l.Length);
        ImGui.Dummy(new Vector2(textX0 + (widest + 4) * charW, _lines.Count * lineH + lineH));

        // focus: a click inside takes it, a click outside drops it
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (hovered && ImGui.GetMousePos().X < winPos.X + winSize.X - ImGui.GetStyle().ScrollbarSize) { _activeId = myId; focused = true; }
            else if (focused) { _activeId = 0; focused = false; }
        }
        if (focused) _activeFrame = ImGui.GetFrameCount();

        if (focused) HandleMouse(origin, textX0, lineH, hovered);
        if (focused && !ReadOnly) HandleKeyboard(lineH, winSize.Y);
        else if (focused) HandleNavigation(lineH, winSize.Y);

        var draw = ImGui.GetWindowDrawList();
        int first = Math.Max(0, (int)(ImGui.GetScrollY() / lineH));
        int last = Math.Min(_lines.Count - 1, first + (int)(winSize.Y / lineH) + 1);
        var (selA, selB) = Ordered();
        bool hasSel = selA != selB;

        for (int li = first; li <= last; li++)
        {
            float y = origin.Y + li * lineH;
            string line = _lines[li];
            float x0 = origin.X + textX0;
            if (li == ErrorLine) draw.AddRectFilled(new Vector2(winPos.X, y), new Vector2(winPos.X + winSize.X, y + lineH), Palette.ErrorLine);
            else if (focused && li == _caret.Line && !hasSel) draw.AddRectFilled(new Vector2(winPos.X, y), new Vector2(winPos.X + winSize.X, y + lineH), Palette.CurrentLine);
            if (hasSel && li >= selA.Line && li <= selB.Line)
            {
                float sx = li == selA.Line ? x0 + ColumnX(line, selA.Col, font) : x0;
                float ex = li == selB.Line ? x0 + ColumnX(line, selB.Col, font) : x0 + ColumnX(line, line.Length, font) + charW * 0.5f;
                draw.AddRectFilled(new Vector2(sx, y), new Vector2(ex, y + lineH), Palette.Selection);
            }
            DrawLine(draw, font, line, x0, y + 1.5f);
        }

        // caret
        if (focused && (int)(ImGui.GetTime() * 1.6) % 2 == 0)
        {
            float cx = origin.X + textX0 + ColumnX(_lines[_caret.Line], _caret.Col, font);
            float cy = origin.Y + _caret.Line * lineH;
            draw.AddLine(new Vector2(cx, cy), new Vector2(cx, cy + lineH), Palette.Caret, 1.5f);
        }

        // gutter on top (it does not scroll sideways)
        draw.AddRectFilled(new Vector2(winPos.X, winPos.Y), new Vector2(winPos.X + gutterW, winPos.Y + winSize.Y), Palette.Gutter);
        for (int li = first; li <= last; li++)
        {
            string num = (li + 1).ToString();
            float y = origin.Y + li * lineH + 1.5f;
            draw.AddText(new Vector2(winPos.X + gutterW - (num.Length + 1) * charW, y), li == _caret.Line ? Palette.LineNumberCurrent : Palette.LineNumber, num);
        }

        if (ErrorLine >= 0 && ErrorText is { } err && hovered)
        {
            float ey = origin.Y + ErrorLine * lineH;
            var m = ImGui.GetMousePos();
            if (m.Y >= ey && m.Y < ey + lineH) ImGui.SetTooltip(err);
        }

        if (_scrollToCaret) { ScrollToCaret(origin, textX0, lineH, font); _scrollToCaret = false; }
        if (focused)
        {
            ImGui.SetNextFrameWantCaptureKeyboard(true);
            if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
        }

        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        if (mono) ImGui.PopFont();
    }

    private void DrawLine(ImDrawListPtr draw, ImFontPtr font, string line, float x0, float y)
    {
        if (line.Length == 0) return;
        _tokens.Clear();
        Language?.Tokenize(line, _tokens);
        int at = 0;
        foreach (var t in _tokens)
        {
            if (t.Start > at) DrawRun(draw, font, line, at, t.Start, x0, y, Palette[TokenKind.Text]);
            DrawRun(draw, font, line, t.Start, t.Start + t.Length, x0, y, Palette[t.Kind]);
            at = t.Start + t.Length;
        }
        if (at < line.Length) DrawRun(draw, font, line, at, line.Length, x0, y, Palette[TokenKind.Text]);
    }

    private void DrawRun(ImDrawListPtr draw, ImFontPtr font, string line, int from, int to, float x0, float y, uint colour)
    {
        float x = x0 + ColumnX(line, from, font);
        int start = from;
        for (int i = from; i < to; i++)
        {
            if (line[i] != '\t' && line[i] != '\r') continue;
            if (i > start) draw.AddText(new Vector2(x, y), colour, line[start..i]);
            x = x0 + ColumnX(line, i + 1, font);
            start = i + 1;
        }
        if (to > start) draw.AddText(new Vector2(x, y), colour, line[start..to]);
    }

    // x of the caret before character `col`: a tab goes to the next tab stop, other characters by their own advance
    private float ColumnX(string line, int col, ImFontPtr font)
    {
        float charW = font.GetCharAdvance('M');
        float x = 0f;
        col = Math.Min(col, line.Length);
        for (int i = 0; i < col; i++)
        {
            char c = line[i];
            if (c == '\t') x = (MathF.Floor(x / (charW * TabSize)) + 1f) * charW * TabSize;
            else if (c != '\r') x += font.GetCharAdvance(c);
        }
        return x;
    }

    private int ColumnAt(string line, float x, ImFontPtr font)
    {
        for (int i = 0; i < line.Length; i++)
        {
            float a = ColumnX(line, i, font), b = ColumnX(line, i + 1, font);
            if (x < (a + b) * 0.5f) return i;
        }
        return line.Length;
    }

    private void ScrollToCaret(Vector2 origin, float textX0, float lineH, ImFontPtr font)
    {
        float top = _caret.Line * lineH, bottom = top + lineH;
        float viewH = ImGui.GetWindowHeight() - ImGui.GetStyle().ScrollbarSize;
        float sy = ImGui.GetScrollY();
        if (top < sy) ImGui.SetScrollY(top);
        else if (bottom > sy + viewH) ImGui.SetScrollY(bottom - viewH);
        float cx = textX0 + ColumnX(_lines[_caret.Line], _caret.Col, font);
        float viewW = ImGui.GetWindowWidth() - ImGui.GetStyle().ScrollbarSize;
        float sx = ImGui.GetScrollX();
        float charW = font.GetCharAdvance('M');
        if (cx - textX0 < sx) ImGui.SetScrollX(Math.Max(0f, cx - textX0 - charW * 4));
        else if (cx + charW * 2 > sx + viewW) ImGui.SetScrollX(cx + charW * 8 - viewW);
    }

    private void HandleMouse(Vector2 origin, float textX0, float lineH, bool hovered)
    {
        var font = ImGui.GetFont();
        var m = ImGui.GetMousePos();
        Pos At()
        {
            int li = Math.Clamp((int)MathF.Floor((m.Y - origin.Y) / lineH), 0, _lines.Count - 1);
            return new Pos(li, ColumnAt(_lines[li], m.X - origin.X - textX0, font));
        }
        var io = ImGui.GetIO();
        if (hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            var p = At();
            var (a, b) = WordAt(p);
            _anchor = a; _caret = b; _dragging = false; _wantX = -1f;
            return;
        }
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _caret = At();
            if (!io.KeyShift) _anchor = _caret;
            _dragging = true;
            _wantX = -1f;
        }
        else if (_dragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _caret = At();
            if (m.Y < ImGui.GetWindowPos().Y || m.Y > ImGui.GetWindowPos().Y + ImGui.GetWindowHeight()) _scrollToCaret = true;
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _dragging = false;
    }

    private (Pos, Pos) WordAt(Pos p)
    {
        string line = _lines[p.Line];
        if (line.Length == 0) return (p, p);
        int c = Math.Min(p.Col, line.Length - 1);
        bool word = IsWordChar(line[c]);
        int a = c, b = c;
        while (a > 0 && IsWordChar(line[a - 1]) == word && !char.IsWhiteSpace(line[a - 1]) == !char.IsWhiteSpace(line[c])) a--;
        while (b < line.Length && IsWordChar(line[b]) == word && !char.IsWhiteSpace(line[b]) == !char.IsWhiteSpace(line[c])) b++;
        return (new Pos(p.Line, a), new Pos(p.Line, b));
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private (Pos, Pos) Ordered() => _anchor < _caret ? (_anchor, _caret) : (_caret, _anchor);

    public string SelectedText
    {
        get
        {
            var (a, b) = Ordered();
            return a == b ? "" : TextBetween(a, b);
        }
    }

    private string TextBetween(Pos a, Pos b)
    {
        if (a.Line == b.Line) return _lines[a.Line][a.Col..b.Col];
        var sb = new StringBuilder(_lines[a.Line][a.Col..]);
        for (int l = a.Line + 1; l < b.Line; l++) sb.Append('\n').Append(_lines[l]);
        sb.Append('\n').Append(_lines[b.Line][..b.Col]);
        return sb.ToString();
    }
}
