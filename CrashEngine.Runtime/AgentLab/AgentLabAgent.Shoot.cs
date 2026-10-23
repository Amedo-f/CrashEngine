using System.Numerics;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-10-06
public sealed partial class AgentLabAgent
{
    public ProjectileAgent? Projectile { get; private set; }

    internal AgentLabAgent? Slot118 => Alive(ref _slot118);

    internal void SetActivityExtra(ushort extra) => _extra = extra;

    internal void UpdateTrailsNow()
    {
        if (!_destroyed && Entity.Active) UpdateTrails();
    }

    internal void DespawnSelf() => DestroySelf();

    private bool CreateProjectileAgent()
    {
        if (GameObject is not { Type: ITwinObject.ObjectType.Projectile } go || go.UnkTypeValue != 0x12) return false;
        Projectile = new ProjectileAgent(this);
        Projectile.SetSettingsIndex(Var(0));
        Trace($"projectile custom agent (settings {Var(0)})");
        return true;
    }

    private bool RequestProjectile(int slot, AgentLabAgent? sender)
    {
        if (GameObject is null || slot >= GameObject.BehaviourSlots.Count) return false;
        ushort id = GameObject.BehaviourSlots[slot];
        return id != 0xFFFF && Projectile!.Request_10C120(id, sender, World.NowTicks);
    }

    private void Shoot_123F98(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint what = Arg(5), bits = Arg(6);
        int exit = (int)(bits & 0xFF);
        Matrix4x4 frame;
        if (exit == 0xFF) frame = LocalMatrix;
        else frame = HasExitNode_2952A0(exit) ? ExitAnchor_293B70(exit) : Matrix4x4.Identity; // Amedo
        var row0 = new Vector3(frame.M11, frame.M12, frame.M13);
        var row1 = new Vector3(frame.M21, frame.M22, frame.M23);
        var row2 = new Vector3(frame.M31, frame.M32, frame.M33);
        var pos = frame.Translation;
        if ((bits & 0x800) != 0) pos += row0 * F(1) + row1 * F(2) + row2 * F(3);
        var rotation = frame;
        rotation.Translation = Vector3.Zero;
        ushort objectId = (ushort)(what & 0x7FFF), message = (ushort)(what >> 16);
        var shot = Spawn(objectId, null, pos, rotation, null);
        if (shot is null) return;
        var p = shot.Projectile;
        if ((bits & 0x2000) != 0)
        {
            var v = row2 * F(7);
            if (p is not null) p.Launch_11EE18(v);
            else World.Unimplemented("command", 548, "Shoot launch of an agent (vt+0xD4)");
        }
        shot.SetActivityExtra(0xFFFF);
        if (p is not null)
        {
            if ((bits & 0x4000) != 0) p.SetTarget(Slot118);
            p.Shooter = this;
            p.Message = message;
            if ((bits & 0x8000) != 0) p.SetFlag(ProjectileAgent.NoDeflect, true);
            p.SetState_10A778(2, World.NowTicks);
        }
        else if (shot.Pickup is not null) World.Unimplemented("command", 548, "Shoot of a pickup custom agent");
        if (message != 0xFFFF) shot.Deliver(message, this);
        Trace($"Shoot object 0x{objectId:X4} from {(exit == 0xFF ? "body" : $"exit {exit}")} at {pos}");
    }
}
