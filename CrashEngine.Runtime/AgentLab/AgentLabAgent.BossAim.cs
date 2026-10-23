using System.Numerics;
using CrashEngine.Importer;
using CrashEngine.Runtime.Camera;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    internal sealed class BossAim_115D88 : MeshDecoder.IJointControllers
    {
        internal sealed class Weapon
        {
            public AgentLabAgent? Owner;
            public bool Returning;
            public bool Locked = true;
            public bool Restart;
            public bool NeedsRest = true;
            public float Scale = 1f;
            public float Speed = 0.1f;
            public int Bone = 0xFF;
            public Matrix4x4 Current = Matrix4x4.Identity;
            public Matrix4x4 InverseRest = Matrix4x4.Identity;
            public Quaternion ShownFrom = Quaternion.Identity, ShownTo = Quaternion.Identity;
            public int ShownFrame = int.MinValue;
            public float AimDot = float.NaN;
        }

        private const float Eps = 2.4999998e-09f;
        private const float QuatEps = 1e-10f;
        private const float NearTolerance = 0.01f;
        private const float RateScale = 20f;

        private readonly AgentLabAgent _agent;
        public readonly Weapon[] Weapons = [new(), new(), new()];
        public AgentLabAgent? Owner;
        public AgentLabAgent? TargetObject;
        public Vector3 TargetPosition;
        public int TargetMode;
        private readonly List<int> _bones = new();
        private AnimatedObject? _anim;

        public BossAim_115D88(AgentLabAgent agent) => _agent = agent;

        public void Setup_116B54(uint bones)
        {
            Weapons[0].Bone = (int)(bones & 0xFF);
            Weapons[1].Bone = (int)((bones >> 8) & 0xFF);
            Weapons[2].Bone = (int)((bones >> 16) & 0xFF);
            foreach (var w in Weapons) w.Owner = _agent;
            Owner = _agent;
            if (_agent.Anim is { } anim) Attach_1218D8(anim);
            else _agent.World.Unimplemented("command", 641, "final boss weapons on an object without animation (original reads a null component 3)");
        }

        public void Release_116BAC(uint mask)
        {
            for (int i = 0; i < 3; i++)
                if ((mask & (1u << i)) != 0) Weapons[i].Returning = !Weapons[i].Locked;
        }

        public void Activate_116C04(uint mask)
        {
            for (int i = 0; i < 3; i++)
            {
                if ((mask & (1u << i)) == 0) continue;
                var w = Weapons[i];
                bool was = w.Locked;
                w.Locked = false;
                w.Restart = was;
                w.Returning = false;
            }
        }

        public void SetRates_116CD0(uint mask, float scale, float speed)
        {
            for (int i = 0; i < 3; i++)
            {
                if ((mask & (1u << i)) == 0) continue;
                Weapons[i].Scale = scale;
                Weapons[i].Speed = speed;
            }
        }

        public void Attach_1218D8(AnimatedObject anim)
        {
            foreach (var w in Weapons) w.Restart = true;
            foreach (var w in Weapons)
                if (!_bones.Contains(w.Bone)) _bones.Add(w.Bone);
            _anim = anim;
            if (anim.Controllers is null) anim.Controllers = this;
            else if (!ReferenceEquals(anim.Controllers, this))
                _agent.World.Unimplemented("animation", 0x298378, "final boss weapons share a model with another joint controller");
        }

        public void Detach_121960(AnimatedObject anim)
        {
            foreach (var w in Weapons) _bones.Remove(w.Bone);
            if (_bones.Count == 0 && ReferenceEquals(anim.Controllers, this)) anim.Controllers = null;
        }

        public void Reset_121880()
        {
            if (Owner?.Anim is { } anim) Detach_121960(anim);
        }

        private Weapon? WeaponOn(int reactId)
        {
            foreach (var w in Weapons)
                if (w.Bone == reactId) return w;
            return null;
        }

        public void Rotate(int reactId, ref Quaternion rot, bool hasRotation) { }

        public bool Transform(int reactId, ref Matrix4x4 model)
        {
            if (!_bones.Contains(reactId) || WeaponOn(reactId) is not { } w || Owner is not { } owner) return false;
            var node = owner.GameMatrix;
            var world = model * node;
            var inv = CamMath.RigidInverse_18E808(node);
            Vector3 target;
            if (TargetMode == 1) target = TargetPosition;
            else if (TargetMode == 2 && TargetObject is { Destroyed: false } t) target = t.GamePosition;
            else return false;
            var d = new Vector4(target.X - world.M41, target.Y - world.M42, target.Z - world.M43, 1f);
            float k = CamMath.InvLengthGuarded_11C030(Eps, d);
            var look = Along_185A40(new Vector3(d.X * k, d.Y * k, d.Z * k));
            var m = world;
            if (AnimatedObject.InLogicCompose)
            {
                Blend_116448(w, ref m, look, model);
                var z = new Vector3(m.M31, m.M32, m.M33);
                w.AimDot = Vector3.Dot(z / MathF.Max(z.Length(), 1e-20f), new Vector3(look.M31, look.M32, look.M33));
            }
            else Shown_116448(w, ref m, look, model);
            m *= inv;
            m.M41 = model.M41; m.M42 = model.M42; m.M43 = model.M43; m.M44 = model.M44;
            model = m;
            return true;
        }

        private static Quaternion Unit(Matrix4x4 m)
        {
            var q = CamMath.MatrixToQuat_186900(m);
            float k = CamMath.QuatInvLength_11C0B8(q, 0f, QuatEps);
            return new Quaternion(q.X * k, q.Y * k, q.Z * k, q.W * k);
        }

        private void Blend_116448(Weapon w, ref Matrix4x4 m, Matrix4x4 look, Matrix4x4 model)
        {
            int frame = _agent.World.Frame;
            if (w.Locked) return;
            if (w.NeedsRest)
            {
                w.InverseRest = CamMath.RigidInverse_18E808(model);
                w.NeedsRest = false;
            }
            if (w.Speed == 0f)
            {
                m = w.Current;
                w.ShownFrom = w.ShownTo = Unit(w.Current);
                w.ShownFrame = frame;
                return;
            }
            if (w.Speed == 1f)
            {
                m = look;
                w.Current = m;
                w.ShownFrom = w.ShownTo = Unit(w.Current);
                w.ShownFrame = frame;
                return;
            }
            if (w.Restart)
            {
                w.Current = m;
                w.Restart = false;
            }
            var delta = w.InverseRest * model;
            var from = Unit(w.Current);
            Quaternion to;
            if (w.Returning)
            {
                to = Unit(m);
                if (CamMath.QuatNear_1861A0(to, from, NearTolerance))
                {
                    w.Returning = false;
                    w.Locked = true;
                }
            }
            else to = Unit(look);
            float dt = (float)_agent.World.Dt;
            var q = CamMath.Slerp_187100(w.Speed * RateScale * dt, from, to);
            w.Current = CamMath.WithRotation_18E620(w.Current, q);
            w.ShownFrom = from;
            w.ShownTo = q;
            w.ShownFrame = frame;
            m = w.Current;
            if (w.Bone == 0) m = delta * m;
            if (w.Scale != 1f) m = CamMath.ScaleColumns_183DE0(m, new Vector3(w.Scale));
        }

        private void Shown_116448(Weapon w, ref Matrix4x4 m, Matrix4x4 look, Matrix4x4 model)
        {
            if (w.Locked || w.NeedsRest) return;
            if (w.Speed == 0f)
            {
                m = w.Current;
                return;
            }
            if (w.Speed == 1f)
            {
                m = look;
                return;
            }
            var q = w.ShownTo;
            if (w.ShownFrame == _agent.World.Frame && SimulationClock.Active)
                q = CamMath.Slerp_187100(SimulationClock.Alpha, w.ShownFrom, w.ShownTo);
            m = CamMath.WithRotation_18E620(w.Current, q);
            if (w.Bone == 0) m = w.InverseRest * model * m;
            if (w.Scale != 1f) m = CamMath.ScaleColumns_183DE0(m, new Vector3(w.Scale));
        }
    }

    private BossAim_115D88? _bossAim;
    internal BossAim_115D88? BossAimComponent => _bossAim;

    private void FinalBossCommand_116B08(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        if (_bossAim is not { } aim)
        {
            World.Unimplemented("command", 641, "final boss command without the type 0 component (original writes through [A+0x114])");
            return;
        }
        uint mask = Arg(2);
        switch (Arg(1))
        {
            case 0:
                aim.Setup_116B54(Arg(5));
                Trace($"final boss weapons on joints {aim.Weapons[0].Bone}/{aim.Weapons[1].Bone}/{aim.Weapons[2].Bone}");
                return;
            case 1: aim.Release_116BAC(mask); return;
            case 2: aim.Activate_116C04(mask); return;
            case 3:
            {
                int code = (int)(Arg(0) & 0xFF);
                if (CodeObject(code) is { } obj)
                {
                    aim.TargetObject = obj;
                    aim.TargetMode = 2;
                    Trace($"final boss weapons aim at {obj.Entity.Name}");
                }
                else if (CodePosition(code, out var pos))
                {
                    aim.TargetMode = 1;
                    aim.TargetPosition = pos;
                    Trace($"final boss weapons aim at {pos}");
                }
                return;
            }
            case 4: aim.SetRates_116CD0(mask, BitConverter.UInt32BitsToSingle(Arg(3)), BitConverter.UInt32BitsToSingle(Arg(4))); return;
        }
    }

    // Amedo
    public string? BossAimDescription => _bossAim is not { } b ? null : string.Join(" | ", b.Weapons.Select(w => FormattableString.Invariant(
        $"j{w.Bone} {(w.Locked ? "anim" : w.Returning ? "back" : "aim")} s{w.Speed:0.###} x{w.Scale:0.##} dot {w.AimDot:0.000}"))) + FormattableString.Invariant($" | mode {b.TargetMode} {b.TargetObject?.Entity.Name.Split("_act_").Last() ?? "-"}");
}
