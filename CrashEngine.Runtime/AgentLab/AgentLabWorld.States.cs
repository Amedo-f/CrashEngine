using System.Runtime.CompilerServices;
using CrashEngine.Runtime.Game;
using CrashEngine.Runtime.Streaming;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabWorld
{
    private const uint StateFlagHasState = 0x40, StateFlagKept = 0x80;
    private const uint StateCapacity = 0x100;

    private readonly HashSet<(string Chunk, uint Id)> _keptStates = new();
    private readonly HashSet<(string Chunk, uint Id)> _lifeStates = new();
    private readonly ConditionalWeakTable<BaseTwinSection, string> _stateChunks = new();

    private HashSet<(string Chunk, uint Id)>? StatesOf(AgentLabAgent agent, out (string Chunk, uint Id) key)
    {
        var (section, id) = agent.StateSlot_16(); // Amedo
        key = (_stateChunks.TryGetValue(section, out var chunk) ? chunk : StreamLink.Normalize(agent.Chunk.Name), id);
        uint flags = agent.Instance.Source.StateFlags;
        if ((flags & StateFlagHasState) == 0 || id >= StateCapacity) return null;
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
    private void RememberStateChunk(WorldChunk chunk)
    {
        string name = StreamLink.Normalize(chunk.Name);
        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
            if (chunk.Rm2.GetItem<BaseTwinSection>((uint)lid)?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION) is { } section)
                _stateChunks.AddOrUpdate(section, name);
    }

    // Amedo
    internal void SaveChunkStates_2686B0(SaveStream stream)
    {
        var names = Chunks.Select(c => StreamLink.Normalize(c.Name)).Concat(_keptStates.Select(s => s.Chunk)).Distinct().ToList();
        stream.WriteCount_DC(names.Count);
        Span<byte> bits = stackalloc byte[0x20];
        foreach (var name in names)
        {
            stream.WriteString_204320(name);
            stream.WriteBool_104(true);
            bits.Clear();
            foreach (var (chunk, id) in _keptStates)
                if (chunk == name) bits[(int)(id >> 3)] |= (byte)(1 << (int)(id & 7));
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
            string name = StreamLink.Normalize(stream.ReadString_204260());
            if (!stream.ReadBool_B4()) continue;
            stream.ReadBytes_14(bits);
            for (uint id = 0; id < StateCapacity; id++)
                if ((bits[(int)(id >> 3)] & (1 << (int)(id & 7))) != 0) _keptStates.Add((name, id));
        }
    }
}
