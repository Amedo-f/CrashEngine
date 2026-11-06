using System.Numerics;
using CrashEngine.Importer;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabAgent
{
    // Amedo 2026-10-02
    internal sealed class LookAt_236570 : MeshDecoder.IJointControllers, MeshDecoder.IJointBasis
    {
        public const uint WholeBody = 0x3000000;
        private const ulong Limited = 0x1000000, YawLow = 0x2000000, YawHigh = 0x4000000, PitchLow = 0x8000000, PitchHigh = 0x10000000;
        private const ulong Returning = 0x20000000, Attached = 0x40000000;
        private static readonly float Eps = BitConverter.Int32BitsToSingle(0x312BCC76);

        private readonly AgentLabAgent _owner;
        public List<uint> Config = new();
        public float Pitch, Yaw, PitchSpeed, YawSpeed, PitchMax, PitchMin, YawMax;
        public int PitchUnits, YawUnits;
        private int _prevPitchUnits, _prevYawUnits, _unitsFrame = int.MinValue;
        public Matrix4x4 Basis = Matrix4x4.Identity;
        public ulong A0 = 0xFFFFFF;
        public float Rate, Damping = 1f;
        public AgentLabAgent? Target, Default, Last;
        public float Weight, DefaultWeight;
        public ulong D0;
        private Quaternion _before;

        public LookAt_236570(AgentLabAgent owner) => _owner = owner;

        private uint Arg(int i) => i < Config.Count ? Config[i] : 0u;
        public bool Whole => (Arg(15) & WholeBody) != 0;
        public int Joint => (int)(A0 & 0xFF);
        public int Joint2 => (int)((A0 >> 8) & 0xFF);
        public int Ref => (int)((A0 >> 16) & 0xFF);
        public bool LooksAway => ((D0 >> 5) & 1) != 0;
        public bool Active => ((D0 >> 1) & 1) != 0;

        public void Setup_236B68(List<uint> config)
        {
            Config = config;
            _owner.World.Rng.RandomFromFloat_18C1C0(2f, 8f); // Amedo
            if (!Whole)
            {
                uint c = Arg(15);
                A0 = (A0 & ~0xFFFFFFUL) | (c & 0xFFFFFF);
                Rate = BitConverter.UInt32BitsToSingle(Arg(2));
                A0 &= ~Returning;
                PitchMax = (int)Arg(4) * GameMath.UnitToRad;
                YawMax = (int)Arg(5) * GameMath.UnitToRad;
                PitchMin = -((int)Arg(3) * GameMath.UnitToRad);
                Damping = BitConverter.UInt32BitsToSingle(Arg(16));
            }
            D0 = (D0 & ~4UL) | 8;
            D0 = (D0 & ~0x10UL) | (_owner.HasCharacterController ? 0x10UL : 0UL);
            Enable_237530();
        }

        public void Enable_237530()
        {
            if (!Whole && (A0 & Attached) == 0)
            {
                if ((D0 & 0x10) == 0)
                {
                    _owner.AttachLookJoints(this, true);
                    A0 |= Attached;
                }
                A0 &= ~Returning;
            }
            D0 = (D0 & ~0x24UL) | 2;
        }

        public void Disable_23F640()
        {
            D0 |= 0x28;
            Return_2372E0(true);
        }

        public void Return_2372E0(bool thenDetach)
        {
            D0 = (D0 & ~4UL) | (thenDetach ? 4UL : 0UL);
            Target = null;
            if (!Whole) A0 = (A0 & ~0x80000000UL) | Returning;
        }

        public void Detach_23F5A8()
        {
            if ((A0 & Attached) == 0) return;
            _owner.AttachLookJoints(this, false);
            A0 &= ~Attached;
        }

        // Amedo
        public void HearNoise_237380(float loudness, AgentLabAgent source)
        {
            if (((Arg(15) >> 26) & 1) != 0 || (D0 & 4) != 0 || loudness < Weight) return;
            Target = source;
            Last = Target;
            Weight = loudness;
            if ((Arg(15) & WholeBody) == 0) A0 &= ~Returning;
            ulong old = D0;
            D0 = (old & ~4UL) | 8;
            if ((old & 2) == 0 || (A0 & Attached) == 0) Enable_237530();
        }

        public void SetTarget_237620(float weight, AgentLabAgent target, bool persistent)
        {
            D0 &= ~4UL;
            if (!Whole) A0 &= ~Returning;
            if (persistent)
            {
                Default = target;
                DefaultWeight = weight;
            }
            Target = target;
            Last = target;
            Weight = weight;
            D0 |= 8;
            Enable_237530();
        }

        public void Update_236CB8()
        {
            var agent = _owner;
            _prevPitchUnits = PitchUnits;
            _prevYawUnits = YawUnits;
            _unitsFrame = agent.World.Frame;
            uint gate = Arg(17);
            if (gate < 0xFF && 0x640 - gate < (agent.Metric & 0xFFFFFF)) return;
            bool whole = Whole;
            bool proceed = (D0 & 2) != 0 && ((D0 & 0x10) != 0 || whole || (A0 & Attached) != 0);
            if (!proceed)
            {
                if (whole) return;
                if (Alive(ref Last) is not { } last || (last.AgentFlags & 0x400) == 0) return;
                return;
            }
            Weight *= 0.97f;
            var tgt = Alive(ref Target);
            var def = Alive(ref Default);
            if (def is not null && Weight < DefaultWeight)
            {
                if (!ReferenceEquals(Target, def)) Target = def;
                Weight = DefaultWeight;
            }
            if (whole || (D0 & 0x10) != 0 || (A0 & Returning) == 0)
            {
                if (tgt is not null && 0.05f <= Weight && (tgt.AgentFlags & 0x400) != 0)
                {
                    ulong old = D0;
                    D0 |= 8;
                    if ((old & 0x10) != 0) return;
                    var pos = tgt.LookPoint_262DD8();
                    if (whole)
                    {
                        float t = 1f - MathF.Pow(1f - MathF.Min(BitConverter.UInt32BitsToSingle(Arg(2)), 1f), (float)agent.World.Dt * 60f);
                        if (((Arg(15) >> 25) & 1) != 0) agent.SteerBodyTowards_22C330(pos, t, true, 0f);
                        else agent.SteerTowards_22CD68(pos, t, 0f, 90f);
                        return;
                    }
                    if ((A0 & Attached) != 0) Head_236658(pos);
                    return;
                }
                if (def is not null)
                {
                    Target = def;
                    Last = def;
                    Weight = DefaultWeight;
                    D0 |= 8;
                    return;
                }
                if ((D0 & 8) == 0) return;
                Return_2372E0(false);
                D0 &= ~8UL;
                return;
            }
            if (!(MathF.Abs(Pitch) < 0.005f && MathF.Abs(Yaw) < 0.005f))
            {
                if ((A0 & Attached) != 0) Head_236658(default);
                return;
            }
            Weight = 0f;
            Detach_23F5A8();
            D0 &= ~8UL;
            A0 &= ~Returning;
            if ((D0 & 4) != 0) D0 &= ~0xEUL;
        }

        private void Head_236658(Vector3 pos)
        {
            A0 &= ~0x1F000000UL;
            float dt = (float)_owner.World.Dt;
            if ((A0 & Returning) == 0)
            {
                var d = new Vector4(pos.X - Basis.M41, pos.Y - Basis.M42, pos.Z - Basis.M43, 1f);
                float k1 = Rate * 2000f;
                float k2 = Rate * Damping * 166f;
                float inv = GameMath.InvLength_11C030(Eps, d);
                float dx = d.X * inv, dy = d.Y * inv, dz = d.Z * inv;
                PitchSpeed += ((Basis.M21 * dx + Basis.M22 * dy + Basis.M23 * dz) * -k1 - PitchSpeed * k2) * dt;
                YawSpeed += ((Basis.M11 * dx + Basis.M12 * dy + Basis.M13 * dz) * k1 - YawSpeed * k2) * dt;
            }
            else
            {
                float k1 = Rate * 1000f;
                float k2 = Rate * Damping * 250f;
                PitchSpeed += (-Pitch * k1 - PitchSpeed * k2) * dt;
                YawSpeed += (-Yaw * k1 - YawSpeed * k2) * dt;
            }
            Pitch += PitchSpeed * dt;
            Yaw += YawSpeed * dt;
            if (Pitch < PitchMin)
            {
                Pitch = PitchMin;
                A0 |= PitchLow | Limited;
            }
            else if (PitchMax < Pitch)
            {
                Pitch = PitchMax;
                A0 |= PitchHigh | Limited;
            }
            if (Yaw < -YawMax)
            {
                Yaw = -YawMax;
                A0 |= YawLow | Limited;
            }
            else if (YawMax < Yaw)
            {
                Yaw = YawMax;
                A0 |= YawHigh | Limited;
            }
            PitchUnits = GameMath.Trunc(Pitch * GameMath.RadToUnit);
            YawUnits = GameMath.Trunc(Yaw * GameMath.RadToUnit);
        }

        private bool Mine(int reactId) => (A0 & Attached) != 0 && (reactId == Joint || (Joint2 != 0xFF && reactId == Joint2));

        public void Rotate(int reactId, ref Quaternion rot, bool hasRotation)
        {
            if (!Mine(reactId)) return;
            _before = rot;
            int pitch = PitchUnits, yaw = YawUnits;
            if (!AnimatedObject.InLogicCompose && _unitsFrame == _owner.World.Frame && SimulationClock.Active)
            {
                float a = SimulationClock.Alpha;
                pitch = (int)MathF.Round(_prevPitchUnits + (PitchUnits - _prevPitchUnits) * a);
                yaw = (int)MathF.Round(_prevYawUnits + (YawUnits - _prevYawUnits) * a);
            }
            var q = GameMath.EulerXY_1862A8(pitch, yaw);
            rot = hasRotation ? Quaternion.Multiply(q, rot) : q;
            if (AnimatedObject.InLogicCompose && Ref != 0xFF) Basis = _owner.ExitAnchor_293B70(Ref); // Amedo
        }

        public bool Transform(int reactId, ref Matrix4x4 model) => false;

        public bool WantsPreWorld(int reactId, out Quaternion rotBefore)
        {
            rotBefore = _before;
            return AnimatedObject.InLogicCompose && Ref == 0xFF && Mine(reactId);
        }

        public void PreWorld(int reactId, in Matrix4x4 world) => Basis = world * _owner.GameMatrix;
    }

    private LookAt_236570? _lookAt;

    // Amedo 2026-10-02
    public string? LookAtDescription => _lookAt is not { } l ? null : FormattableString.Invariant(
        $"{(l.Whole ? "body" : (l.D0 & 0x10) != 0 ? "char" : "joint")} j{l.Joint}/{l.Ref} d0 0x{l.D0:X} a0 0x{l.A0 >> 24:X} p {l.Pitch * 57.29578f:0.0} y {l.Yaw * 57.29578f:0.0} w {l.Weight:0.00} tgt {l.Target?.Entity.Name.Split("_act_").Last() ?? "-"} def {l.Default?.Entity.Name.Split("_act_").Last() ?? "-"}") +
        (OwnCharacter is { } oc ? FormattableString.Invariant($" | B {(oc.Bones.Look.HasTarget ? "target" : "-")} a {oc.Bones.Look.AngleA * 57.29578f:0.0} b {oc.Bones.Look.AngleB * 57.29578f:0.0} facing {oc.Bones.Look.TargetFacing:0.00} exits {Ogi?.ExitPoints.Count}") : "");

    internal bool HasCharacterController => OwnCharacter is not null || IsCharacterObject;

    // Amedo 2026-10-02
    internal void AttachLookJoints(LookAt_236570 look, bool attach)
    {
        if (Anim is not { } anim) return;
        if (attach) { if (anim.Controllers is null) anim.Controllers = look; }
        else if (ReferenceEquals(anim.Controllers, look)) anim.Controllers = null;
    }

    private void SetupLookAt_23DC08(List<uint> a)
    {
        DeleteLookAt_23DAE0();
        _lookAt = new LookAt_236570(this);
        _lookAt.Setup_236B68(a);
    }

    private void DeleteLookAt_23DAE0()
    {
        _lookAt?.Detach_23F5A8();
        _lookAt = null;
    }

    private void SetLookTarget_21C930(uint arg0, uint arg1)
    {
        if (_lookAt is null) return;
        AgentLabAgent? target = null;
        if ((arg0 & 0xFF) == 0xFF)
        {
            if ((arg0 & 0x400) != 0) target = FocusObject;
            else if ((arg0 & 0x100) != 0) target = World.Player;
            else if ((arg0 & 0x200) != 0) target = Alive(ref _slot11C);
        }
        else target = Participant((int)(arg0 & 0xFF));
        if (target is null) return;
        _lookAt.SetTarget_237620(BitConverter.UInt32BitsToSingle(arg1), target, ((arg0 >> 11) & 1) != 0);
        Trace($"look at {target.Entity.Name} (weight {_lookAt.Weight:0.##}, rate {BitConverter.UInt32BitsToSingle(_lookAt.Config.Count > 2 ? _lookAt.Config[2] : 0):0.###})");
    }

    private void UpdateLookAt() => _lookAt?.Update_236CB8();

    // Amedo 2026-10-02
    internal Vector3 LookPoint_262DD8()
    {
        if (HasCharacterController && JointPosition_22DA00(1, out var head)) return head; // Amedo
        var (min, max) = WorldBox();
        return (max - min) * 0.5f + min;
    }

    internal Vector3? CharacterLookTarget_130958()
    {
        if (_lookAt is not { } look || look.LooksAway || !look.Active) return null;
        if (Alive(ref look.Target) is not { } t) return null;
        return t.LookPoint_262DD8(); // Amedo
    }
}
