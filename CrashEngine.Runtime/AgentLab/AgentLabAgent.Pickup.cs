using System.Numerics;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    public PickupAgent? Pickup { get; private set; }

    internal void TraceText(string text) => Trace(text);

    internal void ClearCollidable() => AgentFlags &= ~0x10u;

    private bool CreateCustomAgent()
    {
        if (CreateProjectileAgent()) return true; // Amedo 2026-10-06
        if (GameObject is not { Type: ITwinObject.ObjectType.Pickup } go || go.UnkTypeValue is < 0x10 or >= 0x12) return false;
        Pickup = new PickupAgent(this, World.NextPickupPhase());
        Pickup.SetSettingsIndex(Var(0));
        Pickup.Activate(World.NowTicks);
        Trace($"pickup custom agent (settings {Var(0)})");
        return true;
    }

    public void CollectBy(AgentLabAgent collector)
    {
        if (Pickup is { } p) p.CollectedBy(collector, World.NowTicks);
        else TriggerEventSlot(1, null);
    }

    private bool RequestPickup(int slot, AgentLabAgent? sender)
    {
        if (GameObject is null || slot >= GameObject.BehaviourSlots.Count) return false;
        ushort id = GameObject.BehaviourSlots[slot];
        return id != 0xFFFF && Pickup!.Request(id, sender, World.NowTicks);
    }

    private void CreateCrateContents(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        ushort first = (ushort)(Arg(0) & 0xFFFF), second = (ushort)(Arg(0) >> 16);
        bool hasFirst = first != 0xFFFF, hasSecond = second != 0xFFFF;
        var origin = GamePosition;
        if (hasSecond && !hasFirst)
        {
            Spawn((ushort)(second & 0x7FFF), null, origin + new Vector3(0f, 0.5f, 0f), Matrix4x4.Identity, null);
            return;
        }
        if (!hasFirst) return;
        int lo = (int)(Arg(1) & 0xF), hi = (int)((Arg(1) >> 4) & 0xF);
        int count = World.Rng.GetRandRange_18C060(lo, hi - lo); // Amedo
        float delay = 0f;
        for (int k = 0; k < count; k++)
        {
            var pos = origin;
            delay += 0.05f;
            pos.Y += 0.5f;
            float rx = World.Rng.GetRandFloat__18C130(0.8f); // Amedo
            float rz = World.Rng.GetRandFloat__18C130(0.8f);
            var offset = new Vector3(rx, -0.15f, rz);
            pos += offset;
            var fruit = Spawn((ushort)(first & 0x7FFF), null, pos, Matrix4x4.Identity, null);
            if (fruit is null) continue;
            if (fruit.Pickup is not { } pickup)
            {
                fruit.LaunchPhysics_22ECB0(offset * 2f, -1f); // Amedo 2026-10-05
                continue;
            }
            pickup.Launch(offset * 2f, -1f);
            pickup.Delay(Character.CharacterController.Cvt(delay * 9000f));
        }
        Trace($"crate contents: {count} x object 0x{first & 0x7FFF:X4}");
    }
}
