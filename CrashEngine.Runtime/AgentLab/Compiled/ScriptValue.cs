namespace CrashEngine.Runtime.AgentLab.Compiled;

// Amedo 2026-11-18
public readonly struct ScriptValue
{
    public readonly uint Word;

    private ScriptValue(uint word) => Word = word;

    public static ScriptValue Int(int value) => new(unchecked((uint)(value << 3)));
    public static ScriptValue Float(float value) => new((BitConverter.SingleToUInt32Bits(value) & ~7u) | 4u);
    public static ScriptValue Var(uint index) => new((index << 3) | 1u);
    public static ScriptValue FVar(uint index) => new((index << 3) | 5u);
    public static ScriptValue Raw(uint word) => new(word);

    public static implicit operator ScriptValue(int value) => Int(value);
    public static implicit operator ScriptValue(float value) => Float(value);
}
