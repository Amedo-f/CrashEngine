using CrashEngine.Runtime.Game;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabWorld
{
    private const uint StateFlagHasState = 0x40, StateFlagKept = 0x80;
    private const uint StateCapacity = 0x100;

    private readonly HashSet<(BaseTwinSection, uint)> _keptStates = new();
    private readonly HashSet<(BaseTwinSection, uint)> _lifeStates = new();

    public int KeptStateCount => _keptStates.Count;
    public int LifeStateCount => _lifeStates.Count;

    private HashSet<(BaseTwinSection, uint)>? StatesOf(AgentLabAgent agent, out (BaseTwinSection, uint) key)
    {
        key = agent.StateSlot_16(); // Amedo
        uint flags = agent.Instance.Source.StateFlags;
        if ((flags & StateFlagHasState) == 0 || key.Item2 >= StateCapacity) return null;
        return (flags & StateFlagKept) != 0 ? _keptStates : _lifeStates;
    }

    public void SetZoneState(AgentLabAgent agent, bool on)
    {
        if (StatesOf(agent, out var key) is not { } set) return;
        bool changed = on ? set.Add(key) : set.Remove(key);
        if (changed) Log?.Invoke($"[state {Now:0.00}s] {agent.Entity.Name}: {(on ? "set" : "cleared")} ({(ReferenceEquals(set, _keptStates) ? "kept" : "this life")})");
    }

    public void ToggleZoneState(AgentLabAgent agent)
    {
        if (StatesOf(agent, out var key) is not { } set) return;
        SetZoneState(agent, !set.Contains(key));
    }

    public bool IsZoneStateSet(AgentLabAgent agent) => StatesOf(agent, out var key) is { } set && set.Contains(key);

    // Amedo
    private bool IsSectionOf(WorldChunk chunk, BaseTwinSection section)
    {
        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
            if (ReferenceEquals(chunk.Rm2.GetItem<BaseTwinSection>((uint)lid)?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION), section)) return true;
        return false;
    }

    // Amedo
    internal void SaveChunkStates_2686B0(SaveStream stream)
    {
        stream.WriteCount_DC(Chunks.Count);
        Span<byte> bits = stackalloc byte[0x20];
        foreach (var chunk in Chunks)
        {
            stream.WriteString_204320(chunk.Name);
            stream.WriteBool_104(true);
            bits.Clear();
            foreach (var (section, id) in _keptStates)
                if (IsSectionOf(chunk, section)) bits[(int)(id >> 3)] |= (byte)(1 << (int)(id & 7));
            stream.WriteBytes_24(bits);
        }
    }

    // Amedo
    internal void RestoreChunkStates_263B38(SaveStream stream)
    {
        _keptStates.Clear();
        int count = stream.ReadCount_8C();
        Span<byte> bits = stackalloc byte[0x20];
        for (int i = 0; i < count; i++)
        {
            string name = stream.ReadString_204260();
            if (!stream.ReadBool_B4()) continue;
            stream.ReadBytes_14(bits);
            var chunk = Chunks.Find(c => c.Name == name);
            if (chunk is null) continue;
            for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                if (chunk.Rm2.GetItem<BaseTwinSection>((uint)lid)?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION) is not { } section) continue;
                for (uint id = 0; id < StateCapacity; id++)
                    if ((bits[(int)(id >> 3)] & (1 << (int)(id & 7))) != 0) _keptStates.Add((section, id));
            }
        }
    }
}
