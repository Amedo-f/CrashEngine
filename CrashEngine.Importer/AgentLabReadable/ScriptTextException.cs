namespace CrashEngine.Importer.AgentLabReadable;

// Amedo 2026-11-18
public sealed class ScriptTextException(int line, string reason, int column = 0) : FormatException($"line {line}: {reason}")
{
    public int Line { get; } = line;
    public int Column { get; } = column;
    public string Reason { get; } = reason;
}
