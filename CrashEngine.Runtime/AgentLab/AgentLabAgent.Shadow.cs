using CrashEngine.Importer;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private void ShadowCommand_224210(int command, List<uint> a)
    {
        var data = Instance;
        if (!ShadowComponent(command == 187)) return; // Amedo
        int active = data.RuntimeShadowActive;
        GameShadowDefs.Apply(data.RuntimeShadowSlots!, ref active, command, a, FloatValueOf);
        data.RuntimeShadowActive = active;
        Trace($"shadow command {command}: slot {(a.Count > 0 ? a[0] & 0xFF : 0)}, active {active} ({(data.RuntimeShadowSlots![active] is { } s ? $"shape: {s.Circles.Count} circles, {s.Capsules.Count} capsules, rect {(s.Rect is { } r ? $"type {r.Type} {r.SizeX}x{r.SizeZ}" : "-")}" : "empty")})");
    }

    // Amedo
    private bool ShadowComponent(bool create)
    {
        var data = Instance;
        if (data.RuntimeShadowSlots is not null) return true;
        var local = GameShadowDefs.Object(Chunk.Rm2, data.ObjectId);
        var global = GameShadowDefs.Object(World.GlobalRm2, data.ObjectId);
        var slots = GameShadowDefs.Slots(local, global, Ogi, out int first, out bool component);
        if (!component && !create) return false;
        data.RuntimeShadowSlots = slots;
        data.RuntimeShadowActive = first;
        return true;
    }

    // Amedo
    private void SetShadowSlot_211B08(uint arg0)
    {
        if ((arg0 & 0x200000) == 0 || !ShadowComponent(false)) return;
        int slot = (int)((arg0 >> 22) & 0xF);
        if (slot < Instance.RuntimeShadowSlots!.Length) Instance.RuntimeShadowActive = slot;
    }
}
