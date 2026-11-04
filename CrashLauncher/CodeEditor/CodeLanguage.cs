namespace CrashLauncher.CodeEditor;

// Amedo 2026-11-04: what each piece of a line is, so the editor can colour it
public enum TokenKind : byte
{
    Text,
    Keyword,
    State,
    Property,
    Enum,
    Command,
    AnimationCommand,
    SoundCommand,
    ParticleCommand,
    Condition,
    Number,
    String,
    Comment,
    Punctuation,
    Directive,
}

public readonly record struct Token(int Start, int Length, TokenKind Kind);

public interface ICodeLanguage
{
    // one line -> its pieces (gaps are plain text); lines are coloured on their own, no state between lines
    void Tokenize(string line, List<Token> into);
}
