using CrashEngine.Importer;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabWorld
{
    internal readonly Dictionary<InstanceData, InstancePlaces> Places_13C = new();
    internal readonly Dictionary<InstanceData, InstancePlace> Information_40 = new();
    private readonly List<AgentLabAgent> _global = new();
    private readonly List<AgentLabAgent> _globalStep = new();

    public WorldChunk? UnloadingChunk { get; set; }
    public Action<AgentLabAgent, InstancePlace>? RestartGlobal_197428 { get; set; }
    public Action<AgentLabAgent>? ReleaseGlobal_197570 { get; set; }

    // Amedo
    internal WorldChunk? ChunkNamed_1F0F58(string? name)
    {
        if (name is null) return null;
        foreach (var c in Chunks)
            if (c.Name == name) return c;
        return null;
    }

    // Amedo
    internal bool Unloading_1F0F58(WorldChunk chunk) => ReferenceEquals(chunk, UnloadingChunk);

    // Amedo
    internal void AddGlobal_198EA0(AgentLabAgent agent)
    {
        if (!_global.Contains(agent)) _global.Add(agent);
    }

    // Amedo
    internal void MarkOrderDirty() => _orderDirty = true;

    // Amedo
    private readonly Dictionary<(object Model, int Hull), ushort> _ownHullSurfaces = new();

    // Amedo
    internal ushort OwnHullSurface_1F00A0(object? model, int hull) =>
        model is not null && _ownHullSurfaces.TryGetValue((model, hull), out var s) ? s : (ushort)0;

    // Amedo
    internal void SetOwnHullSurface_1F0730(object? model, int hull, ushort surface)
    {
        if (model is not null) _ownHullSurfaces[(model, hull)] = surface;
    }

    // Amedo
    private void UpdateGlobalInstances_198F90()
    {
        if (_global.Count == 0) return;
        _globalStep.Clear();
        _globalStep.AddRange(_global);
        _global.Clear();
        foreach (var agent in _globalStep)
        {
            if (agent.Destroyed || agent.Released) continue;
            var info = Information_40.GetValueOrDefault(agent.Instance);
            if (info is not null && ChunkNamed_1F0F58(info.Chunk) is not null) RestartGlobal_197428?.Invoke(agent, info);
            else if (info is null || (info.Flags & 1) == 0) ReleaseGlobal_197570?.Invoke(agent);
            else Log?.Invoke($"[AgentLab {Now:0.00}s] {agent.Entity.Name}: global, its place chunk {info.Chunk} is not loaded (left out of every list, 0x1954A8 flag 1)");
        }
        _globalStep.Clear();
    }
}
