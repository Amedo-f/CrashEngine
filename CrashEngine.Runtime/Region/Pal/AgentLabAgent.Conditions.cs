namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-11-11
public sealed partial class AgentLabAgent
{
    private float FocusInSameChunk_Pal_2521F8() =>
        _hasFocusObject && FocusObject is { } same && ReferenceEquals(same.Chunk, Chunk) ? 1f : 0f;
}
