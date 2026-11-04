using ImGuiNET;

namespace CrashLauncher.CodeEditor;

// Amedo 2026-11-04: keyboard: typing, moving, selecting, clipboard, indent, undo/redo
public sealed partial class CodeEditor
{
    private void HandleKeyboard(float lineH, float viewH)
    {
        var io = ImGui.GetIO();
        bool ctrl = io.KeyCtrl, shift = io.KeyShift, alt = io.KeyAlt;

        if (ctrl && !alt)
        {
            if (Pressed(ImGuiKey.A)) { _anchor = new Pos(0, 0); _caret = new Pos(_lines.Count - 1, _lines[^1].Length); return; }
            if (Pressed(ImGuiKey.C) || Pressed(ImGuiKey.Insert)) { Copy(); return; }
            if (Pressed(ImGuiKey.X)) { if (SelectedText.Length > 0) { ImGui.SetClipboardText(SelectedText); Replace(""); } return; }
            if (Pressed(ImGuiKey.V)) { Paste(); return; }
            if (Pressed(ImGuiKey.Z)) { if (shift) Redo(); else Undo(); return; }
            if (Pressed(ImGuiKey.Y)) { Redo(); return; }
        }
        if (shift && !ctrl && Pressed(ImGuiKey.Insert)) { Paste(); return; }

        // typed characters (AltGr arrives as Ctrl+Alt, so letters typed with it still count)
        for (int i = 0; i < io.InputQueueCharacters.Size; i++)
        {
            char c = (char)io.InputQueueCharacters[i];
            if (c < 32 || c == 127 || (ctrl && !alt)) continue;
            Replace(c.ToString(), typing: true);
        }

        if (Pressed(ImGuiKey.Enter) || Pressed(ImGuiKey.KeypadEnter)) { NewLine(); return; }
        if (Pressed(ImGuiKey.Tab)) { if (shift) Outdent(); else Indent(); return; }
        if (Pressed(ImGuiKey.Backspace))
        {
            if (SelectedText.Length > 0) Replace("");
            else if (_caret.Col > 0) DeleteRange(ctrl ? WordLeft(_caret) : BackspaceTarget(), _caret, typing: !ctrl);
            else if (_caret.Line > 0) DeleteRange(new Pos(_caret.Line - 1, _lines[_caret.Line - 1].Length), _caret, typing: false);
            return;
        }
        if (Pressed(ImGuiKey.Delete))
        {
            if (shift && !ctrl && SelectedText.Length > 0) { ImGui.SetClipboardText(SelectedText); Replace(""); }
            else if (SelectedText.Length > 0) Replace("");
            else if (_caret.Col < _lines[_caret.Line].Length) DeleteRange(_caret, ctrl ? WordRight(_caret) : new Pos(_caret.Line, _caret.Col + 1), typing: !ctrl);
            else if (_caret.Line < _lines.Count - 1) DeleteRange(_caret, new Pos(_caret.Line + 1, 0), typing: false);
            return;
        }
        if (Pressed(ImGuiKey.Escape)) { _anchor = _caret; return; }
        HandleNavigation(lineH, viewH);
    }

    // moving the caret (also allowed when read-only)
    private void HandleNavigation(float lineH, float viewH)
    {
        var io = ImGui.GetIO();
        bool ctrl = io.KeyCtrl, shift = io.KeyShift;
        if (ReadOnly && ctrl && Pressed(ImGuiKey.C)) { Copy(); return; }
        if (ReadOnly && ctrl && Pressed(ImGuiKey.A)) { _anchor = new Pos(0, 0); _caret = new Pos(_lines.Count - 1, _lines[^1].Length); return; }
        Pos to = _caret;
        bool moved = true, keepX = false;
        var (selA, selB) = Ordered();
        bool hasSel = selA != selB;
        int page = Math.Max(1, (int)(viewH / lineH) - 1);

        if (Pressed(ImGuiKey.LeftArrow)) to = !shift && hasSel && !ctrl ? selA : ctrl ? WordLeft(_caret) : Left(_caret);
        else if (Pressed(ImGuiKey.RightArrow)) to = !shift && hasSel && !ctrl ? selB : ctrl ? WordRight(_caret) : Right(_caret);
        else if (Pressed(ImGuiKey.UpArrow)) { to = Vertical(-1); keepX = true; }
        else if (Pressed(ImGuiKey.DownArrow)) { to = Vertical(1); keepX = true; }
        else if (Pressed(ImGuiKey.PageUp)) { to = Vertical(-page); keepX = true; }
        else if (Pressed(ImGuiKey.PageDown)) { to = Vertical(page); keepX = true; }
        else if (Pressed(ImGuiKey.Home)) to = ctrl ? new Pos(0, 0) : SmartHome(_caret);
        else if (Pressed(ImGuiKey.End)) to = ctrl ? new Pos(_lines.Count - 1, _lines[^1].Length) : new Pos(_caret.Line, _lines[_caret.Line].Length);
        else moved = false;
        if (!moved) return;
        _caret = to;
        if (!shift) _anchor = to;
        if (!keepX) _wantX = -1f;
        _scrollToCaret = true;
    }

    private static bool Pressed(ImGuiKey key) => ImGui.IsKeyPressed(key, true);

    private void Copy()
    {
        var (a, b) = Ordered();
        // nothing selected: copy the whole line, like most code editors
        ImGui.SetClipboardText(a != b ? TextBetween(a, b).Replace("\n", _newline) : _lines[_caret.Line] + _newline);
    }

    private void Paste()
    {
        if (ImGui.GetClipboardText() is not { Length: > 0 } text) return;
        Replace(text.Replace("\r\n", "\n").Replace('\r', '\n'));
    }

    private void NewLine()
    {
        string line = _lines[_caret.Line];
        int lead = 0;
        while (lead < line.Length && line[lead] == ' ') lead++;
        lead = Math.Min(lead, _caret.Col);
        string before = line[..Math.Min(_caret.Col, line.Length)].TrimEnd();
        int indent = lead + (before.EndsWith('{') ? TabSize : 0);
        Replace("\n" + new string(' ', indent));
    }

    private void Indent()
    {
        var (a, b) = Ordered();
        if (a.Line == b.Line)
        {
            int col = ColumnOf(_lines[_caret.Line], _caret.Col);
            Replace(new string(' ', TabSize - col % TabSize), typing: false);
            return;
        }
        ShiftLines(a.Line, b.Col == 0 && b.Line > a.Line ? b.Line - 1 : b.Line, +1);
    }

    private void Outdent()
    {
        var (a, b) = Ordered();
        ShiftLines(a.Line, b.Col == 0 && b.Line > a.Line ? b.Line - 1 : b.Line, -1);
    }

    // indent / outdent whole lines; each line is one recorded change, all undone together is fine one by one
    private void ShiftLines(int from, int to, int dir)
    {
        var caret = _caret; var anchor = _anchor;
        int caretShift = 0, anchorShift = 0;
        for (int l = from; l <= to; l++)
        {
            if (dir > 0)
            {
                if (_lines[l].Length == 0) continue;
                _caret = _anchor = new Pos(l, 0);
                Replace(new string(' ', TabSize));
                if (l == caret.Line) caretShift = TabSize;
                if (l == anchor.Line) anchorShift = TabSize;
            }
            else
            {
                int n = 0;
                while (n < TabSize && n < _lines[l].Length && _lines[l][n] == ' ') n++;
                if (n == 0) continue;
                DeleteRange(new Pos(l, 0), new Pos(l, n), typing: false);
                if (l == caret.Line) caretShift = -Math.Min(n, caret.Col);
                if (l == anchor.Line) anchorShift = -Math.Min(n, anchor.Col);
            }
        }
        _caret = new Pos(caret.Line, Math.Max(0, caret.Col + caretShift));
        _anchor = new Pos(anchor.Line, Math.Max(0, anchor.Col + anchorShift));
        Clamp();
    }

    private int ColumnOf(string line, int index)
    {
        int col = 0;
        for (int i = 0; i < index && i < line.Length; i++) col = line[i] == '\t' ? (col / TabSize + 1) * TabSize : col + 1;
        return col;
    }

    // backspace inside the leading spaces removes back to the previous indent stop
    private Pos BackspaceTarget()
    {
        string line = _lines[_caret.Line];
        bool onlySpaces = true;
        for (int i = 0; i < _caret.Col; i++) if (line[i] != ' ') { onlySpaces = false; break; }
        if (!onlySpaces || _caret.Col == 0) return new Pos(_caret.Line, _caret.Col - 1);
        int stop = (_caret.Col - 1) / TabSize * TabSize;
        return new Pos(_caret.Line, stop);
    }

    private Pos Left(Pos p) => p.Col > 0 ? new Pos(p.Line, p.Col - 1) : p.Line > 0 ? new Pos(p.Line - 1, _lines[p.Line - 1].Length) : p;
    private Pos Right(Pos p) => p.Col < _lines[p.Line].Length ? new Pos(p.Line, p.Col + 1) : p.Line < _lines.Count - 1 ? new Pos(p.Line + 1, 0) : p;

    private Pos WordLeft(Pos p)
    {
        if (p.Col == 0) return Left(p);
        string line = _lines[p.Line];
        int c = p.Col;
        while (c > 0 && char.IsWhiteSpace(line[c - 1])) c--;
        if (c > 0 && IsWordChar(line[c - 1])) while (c > 0 && IsWordChar(line[c - 1])) c--;
        else if (c > 0) c--;
        return new Pos(p.Line, c);
    }

    private Pos WordRight(Pos p)
    {
        string line = _lines[p.Line];
        if (p.Col >= line.Length) return Right(p);
        int c = p.Col;
        if (IsWordChar(line[c])) while (c < line.Length && IsWordChar(line[c])) c++;
        else if (!char.IsWhiteSpace(line[c])) c++;
        while (c < line.Length && char.IsWhiteSpace(line[c])) c++;
        return new Pos(p.Line, c);
    }

    private Pos SmartHome(Pos p)
    {
        string line = _lines[p.Line];
        int first = 0;
        while (first < line.Length && char.IsWhiteSpace(line[first])) first++;
        return new Pos(p.Line, p.Col == first ? 0 : first);
    }

    private Pos Vertical(int lines)
    {
        var font = ImGui.GetFont();
        if (_wantX < 0f) _wantX = ColumnX(_lines[_caret.Line], _caret.Col, font);
        int l = Math.Clamp(_caret.Line + lines, 0, _lines.Count - 1);
        return new Pos(l, ColumnAt(_lines[l], _wantX, font));
    }
}
