namespace CrashLauncher.CodeEditor;

// Amedo 2026-11-18
public sealed class CSharpLanguage : ICodeLanguage
{
    public static readonly CSharpLanguage Instance = new();

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "using", "static", "namespace", "public", "private", "protected", "internal", "sealed", "abstract", "class", "struct",
        "override", "virtual", "readonly", "const", "new", "null", "true", "false", "return", "this", "base", "if", "else",
        "for", "foreach", "in", "while", "switch", "case", "default", "break", "var", "string", "int", "uint", "float", "byte",
        "short", "ushort", "bool", "object", "void", "get", "set", "init",
    };

    public void Tokenize(string line, List<Token> into)
    {
        int i = 0, n = line.Length;
        string previous = "";
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
            if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(line[i + 1])) ||
                (c == '-' && i + 1 < n && char.IsDigit(line[i + 1]) && !EndsValue(line, i)))
            {
                int j = i + 1;
                if (c == '0' && j < n && line[j] is 'x' or 'X') { j++; while (j < n && (Uri.IsHexDigit(line[j]) || line[j] == '_')) j++; }
                else while (j < n && (char.IsDigit(line[j]) || line[j] is '.' or 'e' or 'E' || (line[j] is '-' or '+' && line[j - 1] is 'e' or 'E'))) j++;
                while (j < n && line[j] is 'u' or 'U' or 'f' or 'F' or 'l' or 'L') j++;
                into.Add(new Token(i, j - i, TokenKind.Number));
                i = j;
                previous = "";
                continue;
            }
            if (char.IsLetter(c) || c == '_' || c == '@')
            {
                int j = i + 1;
                while (j < n && (char.IsLetterOrDigit(line[j]) || line[j] == '_')) j++;
                string word = line[(c == '@' ? i + 1 : i)..j];
                int k = j;
                while (k < n && line[k] == ' ') k++;
                TokenKind kind;
                if (c != '@' && Keywords.Contains(word)) kind = TokenKind.Keyword;
                else if (previous == "ConditionId") kind = TokenKind.Condition;
                else if (previous.Length > 0) kind = TokenKind.Enum;
                else if (word == "If" && k < n && line[k] == '(') kind = TokenKind.Keyword;
                else if (k < n && line[k] == '(') kind = AgentLabLanguage.CommandKind(word);
                else if (k < n && line[k] == ':' && (k + 1 >= n || line[k + 1] != ':')) kind = TokenKind.Property;
                else kind = TokenKind.Text;
                into.Add(new Token(i, j - i, kind));
                previous = k < n && line[k] == '.' && word is "ConditionId" or "CommandId" or "SpaceType" or "MotionType" or "ContinuousRotateType"
                                                           or "AccelFunction" or "MessageMode" or "Toggle" or "Designator" or "AssignTarget"
                                                           or "AttachSource" or "ScriptValue" or "PacketNumber" or "PacketInteger"
                    ? word : "";
                i = j;
                continue;
            }
            into.Add(new Token(i, 1, TokenKind.Punctuation));
            if (c != '.') previous = "";
            i++;
        }
    }

    private static bool EndsValue(string line, int at)
    {
        for (int i = at - 1; i >= 0; i--)
        {
            if (line[i] == ' ') continue;
            return char.IsLetterOrDigit(line[i]) || line[i] is ')' or ']' or '_';
        }
        return false;
    }
}
