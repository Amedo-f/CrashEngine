namespace CrashEngine.Runtime.AgentLab.Compiled;

// Amedo 2026-11-18
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BehaviourIdAttribute(uint id) : Attribute
{
    public uint Id { get; } = id;
}
