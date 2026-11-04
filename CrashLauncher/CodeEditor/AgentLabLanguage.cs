namespace CrashLauncher.CodeEditor;

// Amedo 2026-11-04: colours of the behaviour script text (the editor's raw text and its readable form)
public sealed class AgentLabLanguage : ICodeLanguage
{
    public static readonly AgentLabLanguage Instance = new();

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "Script", "StartState", "Priority", "Body", "Condition", "next_state", "reenter", "packet", "settings", "data",
        "uses_object_slot", "state_unknown", "none_blocking", "skip_first_state_body", "true", "false", "True", "False",
        "behaviour", "state", "if", "execute", "interval", "unknown",
    };

    public void Tokenize(string line, List<Token> into)
    {
        int i = 0, n = line.Length;
        bool afterCondition = false, afterDirective = false;
        while (i < n)
        {
            char c = line[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < n && line[i + 1] == '/') { into.Add(new Token(i, n - i, TokenKind.Comment)); return; }
            if (c == '"')
            {
                int j = i + 1;
                while (j < n && line[j] != '"') j += line[j] == '\\' && j + 1 < n ? 2 : 1;
                j = Math.Min(n, j + 1);
                into.Add(new Token(i, j - i, TokenKind.String));
                i = j;
                continue;
            }
            if (c == '@')
            {
                int j = i + 1;
                while (j < n && char.IsLetterOrDigit(line[j])) j++;
                into.Add(new Token(i, j - i, TokenKind.Directive));
                afterDirective = true;
                i = j;
                continue;
            }
            if (char.IsDigit(c) || (c is '-' or '.' && i + 1 < n && char.IsDigit(line[i + 1])))
            {
                int j = i + 1;
                if (c == '0' && j < n && line[j] is 'x' or 'X') { j++; while (j < n && Uri.IsHexDigit(line[j])) j++; }
                else while (j < n && (char.IsDigit(line[j]) || line[j] is '.' or 'e' or 'E' || (line[j] is '-' or '+' && line[j - 1] is 'e' or 'E'))) j++;
                if (j < n && line[j] is 'u' or 'U' or 'f' or 'F') j++;
                into.Add(new Token(i, j - i, TokenKind.Number));
                i = j;
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                int j = i + 1;
                while (j < n && (char.IsLetterOrDigit(line[j]) || line[j] == '_')) j++;
                string word = line[i..j];
                int k = j;
                while (k < n && line[k] == ' ') k++;
                bool call = k < n && line[k] == '(';
                TokenKind kind;
                if (afterCondition) { kind = TokenKind.Condition; afterCondition = false; }
                else if (word == "Condition") { kind = TokenKind.Keyword; afterCondition = true; }
                else if (word.StartsWith("State_", StringComparison.Ordinal) || word.StartsWith("ControlPacket_", StringComparison.Ordinal)) kind = TokenKind.State;
                else if (Keywords.Contains(word)) kind = TokenKind.Keyword;
                else if (call && (afterDirective || IsStatementStart(line, i))) kind = CommandKind(word);
                else if (IsAllCaps(word)) kind = TokenKind.Enum;
                else if (k < n && line[k] == '=') kind = TokenKind.Property;
                else kind = TokenKind.Text;
                afterDirective = false;
                into.Add(new Token(i, j - i, kind));
                i = j;
                continue;
            }
            into.Add(new Token(i, 1, TokenKind.Punctuation));
            i++;
        }
    }

    // Amedo: the three kinds the user wants to tell apart at a glance (each will open its own preview)
    public static TokenKind CommandKind(string name)
    {
        if (name.Contains("Anim", StringComparison.Ordinal)) return TokenKind.AnimationCommand;
        if (name.Contains("Sound", StringComparison.Ordinal) || name.Contains("Music", StringComparison.Ordinal)) return TokenKind.SoundCommand;
        if (name.Contains("Particle", StringComparison.Ordinal) || name.Contains("Trail", StringComparison.Ordinal)) return TokenKind.ParticleCommand;
        return TokenKind.Command;
    }

    private static bool IsStatementStart(string line, int at)
    {
        for (int i = 0; i < at; i++) if (!char.IsWhiteSpace(line[i])) return false;
        return true;
    }

    private static bool IsAllCaps(string w)
    {
        bool letter = false;
        foreach (char c in w)
        {
            if (char.IsLower(c)) return false;
            if (char.IsLetter(c)) letter = true;
        }
        return letter && w.Length > 1;
    }
}
