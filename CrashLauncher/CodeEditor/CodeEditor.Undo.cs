
namespace CrashLauncher.CodeEditor;

// Amedo 2026-11-04: every change is an insert or a delete at a position; undo plays it backwards. Typing letters one
// after the other (and deleting one after the other) joins into one step.
public sealed partial class CodeEditor
{
    private sealed class Edit
    {
        public bool Insert;              // true = text was put in at At, false = text was taken out at At
        public Pos At;
        public string Text = "";
        public Pos CaretBefore, AnchorBefore, CaretAfter;
        public bool Typing;              // a single typed character (may join the next one)
    }

    private readonly List<Edit> _undo = new();
    private readonly List<Edit> _redo = new();

    // ---- the only two places the text changes ----

    private Pos InsertRaw(Pos at, string text)
    {
        var parts = text.Split('\n');
        string line = _lines[at.Line];
        string head = line[..at.Col], tail = line[at.Col..];
        if (parts.Length == 1)
        {
            _lines[at.Line] = head + parts[0] + tail;
            Touch();
            return new Pos(at.Line, at.Col + parts[0].Length);
        }
        _lines[at.Line] = head + parts[0];
        var add = new List<string>(parts.Length - 1);
        for (int i = 1; i < parts.Length - 1; i++) add.Add(parts[i]);
        add.Add(parts[^1] + tail);
        _lines.InsertRange(at.Line + 1, add);
        Touch();
        return new Pos(at.Line + parts.Length - 1, parts[^1].Length);
    }

    private string DeleteRaw(Pos a, Pos b)
    {
        if (a == b) return "";
        string removed = TextBetween(a, b);
        string head = _lines[a.Line][..a.Col], tail = _lines[b.Line][b.Col..];
        _lines[a.Line] = head + tail;
        if (b.Line > a.Line) _lines.RemoveRange(a.Line + 1, b.Line - a.Line);
        Touch();
        return removed;
    }

    private void Touch()
    {
        _textCache = null;
        Changed = true;
        ErrorLine = -1;
    }

    // ---- recorded changes ----

    private void Replace(string text, bool typing = false)
    {
        var (a, b) = Ordered();
        var caretBefore = _caret; var anchorBefore = _anchor;
        if (a != b)
        {
            string removed = DeleteRaw(a, b);
            Push(new Edit { Insert = false, At = a, Text = removed, CaretBefore = caretBefore, AnchorBefore = anchorBefore, CaretAfter = a });
            typing = false;
        }
        if (text.Length > 0)
        {
            var end = InsertRaw(a, text);
            var last = _undo.Count > 0 ? _undo[^1] : null;
            if (typing && last is { Insert: true, Typing: true } && last.CaretAfter == a && !IsWordBreak(text, last.Text))
            {
                last.Text += text;
                last.CaretAfter = end;
                _redo.Clear();
            }
            else Push(new Edit { Insert = true, At = a, Text = text, CaretBefore = caretBefore, AnchorBefore = anchorBefore, CaretAfter = end, Typing = typing });
            _caret = _anchor = end;
        }
        else _caret = _anchor = a;
        _scrollToCaret = true;
        _wantX = -1f;
    }

    // a space after a word starts a new undo step, like most editors
    private static bool IsWordBreak(string next, string sofar) => next == " " && sofar.Length > 0 && sofar[^1] != ' ';

    private void DeleteRange(Pos a, Pos b, bool typing)
    {
        if (a == b) return;
        if (b < a) (a, b) = (b, a);
        var caretBefore = _caret; var anchorBefore = _anchor;
        string removed = DeleteRaw(a, b);
        var last = _undo.Count > 0 ? _undo[^1] : null;
        if (typing && removed.Length == 1 && last is { Insert: false, Typing: true })
        {
            if (b == last.At) { last.Text = removed + last.Text; last.At = a; last.CaretAfter = a; _redo.Clear(); }        // backspace again
            else if (a == last.At) { last.Text += removed; _redo.Clear(); }                                                 // delete again
            else Push(new Edit { Insert = false, At = a, Text = removed, CaretBefore = caretBefore, AnchorBefore = anchorBefore, CaretAfter = a, Typing = true });
        }
        else Push(new Edit { Insert = false, At = a, Text = removed, CaretBefore = caretBefore, AnchorBefore = anchorBefore, CaretAfter = a, Typing = typing && removed.Length == 1 });
        _caret = _anchor = a;
        _scrollToCaret = true;
        _wantX = -1f;
    }

    private void Push(Edit e)
    {
        _undo.Add(e);
        if (_undo.Count > 2000) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var e = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        if (e.Insert) DeleteRaw(e.At, EndOf(e.At, e.Text));
        else InsertRaw(e.At, e.Text);
        // an edit made of a delete then an insert (typing over a selection) undoes both
        if (e.Insert && _undo.Count > 0 && _undo[^1] is { Insert: false } prev && prev.CaretAfter == e.At && e.CaretBefore == prev.CaretBefore && !e.Typing)
        {
            _undo.RemoveAt(_undo.Count - 1);
            InsertRaw(prev.At, prev.Text);
            _redo.Add(prev);
        }
        _redo.Add(e);
        _caret = e.CaretBefore; _anchor = e.AnchorBefore;
        Clamp();
        _scrollToCaret = true;
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var e = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        if (e.Insert) InsertRaw(e.At, e.Text);
        else DeleteRaw(e.At, EndOf(e.At, e.Text));
        _undo.Add(e);
        if (!e.Insert && _redo.Count > 0 && _redo[^1] is { Insert: true } next && next.At == e.At && next.CaretBefore == e.CaretBefore)
        {
            _redo.RemoveAt(_redo.Count - 1);
            InsertRaw(next.At, next.Text);
            _undo.Add(next);
            e = next;
        }
        _caret = _anchor = e.CaretAfter;
        Clamp();
        _scrollToCaret = true;
    }

    private static Pos EndOf(Pos at, string text)
    {
        int nl = text.LastIndexOf('\n');
        if (nl < 0) return new Pos(at.Line, at.Col + text.Length);
        int lines = 0;
        foreach (char c in text) if (c == '\n') lines++;
        return new Pos(at.Line + lines, text.Length - nl - 1);
    }

    private void Clamp()
    {
        Pos C(Pos p)
        {
            int l = Math.Clamp(p.Line, 0, _lines.Count - 1);
            return new Pos(l, Math.Clamp(p.Col, 0, _lines[l].Length));
        }
        _caret = C(_caret);
        _anchor = C(_anchor);
    }
}
