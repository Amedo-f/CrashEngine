
namespace CrashLauncher.CodeEditor;

// Amedo 2026-11-04: colours of the code editor (dark, close to what code editors usually use)
public sealed class CodePalette
{
    public uint Background = Rgb(30, 30, 30);
    public uint Gutter = Rgb(30, 30, 30);
    public uint LineNumber = Rgb(110, 118, 129);
    public uint LineNumberCurrent = Rgb(200, 200, 200);
    public uint CurrentLine = Rgba(255, 255, 255, 14);
    public uint Selection = Rgba(38, 79, 120, 255);
    public uint Caret = Rgb(220, 220, 220);
    public uint ErrorLine = Rgba(160, 30, 30, 110);

    private readonly uint[] _kinds = new uint[Enum.GetValues<TokenKind>().Length];

    public CodePalette()
    {
        Set(TokenKind.Text, Rgb(212, 212, 212));
        Set(TokenKind.Keyword, Rgb(86, 156, 214));
        Set(TokenKind.State, Rgb(78, 201, 176));
        Set(TokenKind.Property, Rgb(156, 220, 254));
        Set(TokenKind.Enum, Rgb(184, 215, 163));
        Set(TokenKind.Command, Rgb(220, 220, 170));
        Set(TokenKind.AnimationCommand, Rgb(64, 156, 255));   // blue (user's choice)
        Set(TokenKind.SoundCommand, Rgb(190, 190, 196));      // light grey (user's choice)
        Set(TokenKind.ParticleCommand, Rgb(197, 134, 255));   // purple (user's choice)
        Set(TokenKind.Condition, Rgb(79, 193, 255));
        Set(TokenKind.Number, Rgb(181, 206, 168));
        Set(TokenKind.String, Rgb(206, 145, 120));
        Set(TokenKind.Comment, Rgb(106, 153, 85));
        Set(TokenKind.Punctuation, Rgb(150, 150, 150));
        Set(TokenKind.Directive, Rgb(128, 128, 128));
    }

    public uint this[TokenKind kind] => _kinds[(int)kind];
    public void Set(TokenKind kind, uint colour) => _kinds[(int)kind] = colour;

    private static uint Rgb(int r, int g, int b) => Rgba(r, g, b, 255);
    private static uint Rgba(int r, int g, int b, int a) => (uint)(r | g << 8 | b << 16 | a << 24);
}
