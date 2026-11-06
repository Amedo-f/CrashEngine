using System.Numerics;
using CrashEngine.Importer;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-28
public sealed class CharacterBones : MeshDecoder.IJointControllers
{
    public const int Spring = 1, Leg = 2, Hips = 3, Nose = 4;

    private const float Far = 1e30f;

    public sealed class Record
    {
        public int Mode;
        public Vector3 Tip, Velocity, Smoothed;
        public readonly int[] Angles = new int[3];
        public Vector3 Normal = Vector3.UnitY;
        public float Step;
        public int Source = -1;
        public int Joint;
        public float HalfLife, HalfLife2, GainA, GainB, LimitA, LimitB;
        public int AxisA, AxisB, SlotA, SlotB;
        public bool Started;
        public readonly int[] PrevAngles = new int[3]; // Amedo 2026-09-30
        public Vector3 FrameTip;
        public bool HasFrameTip;
    }

    public const int SpringStepTicks = 150; // Amedo 2026-09-30
    private const int MaxSpringSteps = 10;
    private int _springTicks;

    private readonly CharacterController _c;

    public readonly List<Record> Records = new();
    public float LegLeft = 1f, LegRight = 1f, PitchLeft, PitchRight;
    public bool LegsOn, NoseOn;
    public float NoseScale = 1f, NoseVelocity;
    public int Kind { get; private set; } = -1;

    public CharacterBones(CharacterController c) => _c = c;

    public readonly CharacterLook Look = new(); // Amedo 2026-09-29

    // Amedo 2026-09-29
    public void Transfer_15F500(Matrix4x4 obj)
    {
        foreach (var r in Records)
        {
            r.Tip = Vector3.Transform(r.Tip, obj);
            r.Velocity = Vector3.TransformNormal(r.Velocity, obj);
            r.Smoothed = Vector3.TransformNormal(r.Smoothed, obj);
            r.Normal = Vector3.TransformNormal(r.Normal, obj);
            r.FrameTip = Vector3.Transform(r.FrameTip, obj); // Amedo 2026-09-30
        }
    }

    // Amedo 2026-10-05
    public void SpinEnd_15F860(Matrix4x4 node)
    {
        var right = Vector3.TransformNormal(Vector3.UnitX, node);
        foreach (var r in Records)
        {
            r.Velocity = right;
            r.Smoothed = Vector3.Zero;
        }
    }

    public void Warped_15F398()
    {
        foreach (var r in Records)
        {
            r.Velocity = Vector3.Zero;
            r.Smoothed = Vector3.Zero;
            r.HasFrameTip = false; // Amedo 2026-09-30
        }
    }

    public void Setup(int kind)
    {
        Kind = kind;
        Look.Kind = kind; // Amedo 2026-09-29
        Look.Reset_147410();
        Records.Clear();
        _springTicks = 0; // Amedo 2026-09-30
        LegsOn = NoseOn = false;
        switch (kind)
        {
            case 0:
                AddSpring(0.05f, 0.1f, -165f, -70f, 3.3f, 2.1f, 1.6f, 1, 0x14, 2, 0, 0, 2);
                AddSpring(0.05f, 0.1f, -165f, -70f, 3.3f, 2.1f, 1.6f, 0, 0x15, 2, 0, 0, 2);
                AddSpring(0.05f, 0.1f, -23f, 25f, 3.2f, 1.1f, 1.1f, 9, 0x1A, 1, 2, 0, 1);
                AddSpring(0.05f, 0.1f, -23f, -25f, 3.2f, 1.1f, 1.1f, 10, 0x1B, 1, 2, 0, 1);
                AddLegs();
                Records.Add(new Record { Mode = Hips, Step = 1.3f, Joint = 5 });
                Records.Add(new Record { Mode = Nose, Joint = 0x1C });
                NoseOn = LegsOn = true;
                break;
            case 1:
                AddLegs();
                AddSpring(0.05f, 0.1f, -23f, 45f, 5.2f, 2f, 1.1f, 9, 0x1A, 1, 2, 0, 1);
                AddSpring(0.05f, 0.1f, -23f, -45f, 5.2f, 2f, 1.1f, 10, 0x1B, 1, 2, 0, 1);
                LegsOn = true;
                break;
            case 2:
            case 3:
                LegsOn = true;
                AddLegs();
                break;
        }
        NoseScale = 1f;
        NoseVelocity = 0f;
        LegRight = LegLeft = 1f;
        PitchRight = PitchLeft = 0f;
    }

    // Amedo 2026-10-04
    public void SetNose_15F388(float scale)
    {
        NoseScale = scale;
        NoseVelocity = 0f;
    }

    // Amedo 2026-09-30
    public void Squash_15F350(float scale)
    {
        float current = NoseScale;
        if (!(scale < current)) return;
        NoseScale = scale;
        NoseVelocity = (scale - current) * 20f;
    }

    private void AddSpring(float halfLife, float halfLife2, float gainA, float gainB, float reserved, float limitA, float limitB,
                           int source, int joint, int slotA, int slotB, int axisA, int axisB) => Records.Add(new Record
    {
        Mode = Spring, Source = source, Joint = joint, HalfLife = halfLife, HalfLife2 = halfLife2, GainA = gainA, GainB = gainB, LimitA = limitA, LimitB = limitB, SlotA = slotA, SlotB = slotB, AxisA = axisA, AxisB = axisB,
    });

    private void AddLegs()
    {
        foreach (int j in new[] { 6, 9, 7, 10, 8, 11 }) Records.Add(new Record { Mode = Leg, Joint = j });
    }

    private bool HasLook => _c.Limbs.Anim is not null && Kind is >= 0 and not 4; // Amedo

    // Amedo
    public void LookFrame_130958(float dt)
    {
        if (!HasLook) return;
        bool crouched = (_c.MotionFlagsHigh & 1) != 0;
        bool look = _c.Vehicle is not { AllowsLook: false };
        Look.Input_130958(look ? _c.LookStickX : 0f, look ? _c.LookStickY : 0f, _c.MotionState, crouched, _c.LookTarget?.Invoke());
        var game = _c.GameMatrix;
        Look.Update_1476A8(dt, _c.CutsceneMode, crouched, Look.HasTarget ? _c.Limbs.ExitMatrix(1, game) : null, new Vector3(game.M31, game.M32, game.M33));
    }

    // Amedo
    public void LookRelax_147B18(float dt)
    {
        if (HasLook) Look.Death_147B18(dt);
    }

    public void Joints_14B6D0(float dt)
    {
        if (_c.Limbs.Anim is not null)
        {
            StepSprings(dt); // Amedo 2026-09-30
            foreach (var rec in Records)
                if (rec.Mode != Spring) UpdateRecord(dt, rec);
        }
        if (LegsOn) LegProbes(dt);
        if (!NoseOn) return;
        float x = NoseScale, v = NoseVelocity;
        float nx = x + v * dt;
        NoseScale = nx;
        NoseVelocity = v + ((x - 1f) * -300f + v * -9f) * dt;
        if (1.3f < nx) NoseScale = 1.3f;
        else if (nx < 0.5f) NoseScale = 0.5f;
    }

    private static float Decay(float halfLife, float dt) => MathF.Exp(dt * -0.6931472f / halfLife);

    private static int Slot(int i) => i is 1 or 2 ? i : 0;

    private static float Axis(Vector4 v, int i) => i switch { 1 => v.Y, 2 => v.Z, 3 => v.W, _ => v.X };

    // Amedo
    public void HoldSprings()
    {
        foreach (var rec in Records)
            if (rec.Mode == Spring) Array.Copy(rec.Angles, rec.PrevAngles, 3);
    }

    // Amedo 2026-09-30
    private void StepSprings(float dt)
    {
        int frameTicks = (int)MathF.Round(dt * GameMath.SecToTick);
        if (frameTicks < 0) frameTicks = 0;
        _springTicks += frameTicks;
        int steps = _springTicks / SpringStepTicks;
        _springTicks -= steps * SpringStepTicks;
        if (steps > MaxSpringSteps) steps = MaxSpringSteps;
        var game = _c.GameMatrix;
        foreach (var rec in Records)
        {
            if (rec.Mode != Spring) continue;
            Matrix4x4 src;
            if (rec.Source == -1) src = game;
            else if (_c.Limbs.ExitMatrix(rec.Source, game) is { } m) src = m;
            else continue;
            var now = src.Translation;
            for (int i = 0; i < steps; i++)
            {
                int before = _springTicks + (steps - 1 - i) * SpringStepTicks;
                var p = before <= 0 || !rec.HasFrameTip || frameTicks <= 0
                    ? now
                    : Vector3.Lerp(now, rec.FrameTip, (float)before / frameTicks);
                Array.Copy(rec.Angles, rec.PrevAngles, 3);
                StepSpring(rec, src, p, SpringStepTicks * GameMath.TickToSec);
            }
            rec.FrameTip = now;
            rec.HasFrameTip = true;
        }
    }

    private void StepSpring(Record rec, Matrix4x4 src, Vector3 p, float dt)
    {
        if (!rec.Started)
        {
            rec.Started = true;
            rec.Tip = p;
        }
        var delta = p - rec.Tip;
        rec.Tip = p;
        float k1 = Decay(rec.HalfLife, dt), k2 = Decay(rec.HalfLife2, dt);
        rec.Velocity = rec.Velocity * k1 + delta * (1f - k1);
        rec.Smoothed = rec.Smoothed * k2 + rec.Velocity * (1f - k2);
        var diff = rec.Velocity - rec.Smoothed;
        var local = GameMath.Rotate3_18E4B8(GameMath.RigidInverse_18E808(src), new Vector4(diff, 1f));
        if (_c.Link is { } link) // Amedo
            local = GameMath.Rotate3_18E4B8(GameMath.RotY_18BBC0(GameMath.FromValue_11BFA0(-(link.Scale40 * 0.25f * 3f), 0)), local);
        float a = -Axis(local, rec.AxisA) * rec.GainA;
        float b = Axis(local, rec.AxisB) * rec.GainB;
        if (rec.LimitA < a) a = rec.LimitA;
        if (a < -rec.LimitA) a = -rec.LimitA;
        if (rec.LimitB < b) b = rec.LimitB;
        if (b < -rec.LimitB) b = -rec.LimitB;
        int sa = Slot(rec.SlotA);
        rec.Angles[sa] = GameMath.Scale_15DE90(k1, rec.Angles[sa]) + GameMath.Trunc((1f - k1) * a * GameMath.RadToUnit);
        int sb = Slot(rec.SlotB);
        rec.Angles[sb] = GameMath.Scale_15DE90(k1, rec.Angles[sb]) + GameMath.Trunc((1f - k1) * b * GameMath.RadToUnit);
        rec.Angles[Slot(3 - rec.SlotB - rec.SlotA)] = 0;
    }

    private void UpdateRecord(float dt, Record rec)
    {
        if (rec.Mode == Hips)
        {
            if ((_c.MotionFlagsHigh & ((1u << 9) | 1u)) == 0)
            {
                rec.Normal = Vector3.UnitY;
                return;
            }
            var target = SurfaceNormal(out var n) ? n : Vector3.UnitY;
            var d = target - rec.Normal;
            float max = rec.Step * dt;
            if (max < MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z))
            {
                float inv = GameMath.InverseLength(d);
                d = new Vector3(d.X * inv * max, d.Y * inv * max, d.Z * inv * max);
            }
            rec.Normal += d;
        }
    }

    private bool SurfaceNormal(out Vector3 normal)
    {
        normal = Vector3.Zero;
        float count = 0f;
        var m = _c.GameMatrix;
        var pos = m.Translation;
        var right = new Vector3(m.M11, m.M12, m.M13);
        var up = new Vector3(m.M21, m.M22, m.M23);
        var fwd = new Vector3(m.M31, m.M32, m.M33);
        for (int i = 0; i < 4; i++)
        {
            var start = pos + right * ((i & 1) != 0 ? 0.3f : -0.3f) + fwd * (i / 2 != 0 ? 0.3f : -0.3f);
            var end = start + up * -0.3f;
            if (!CharacterCollision.TriangleListRayCast_2928E8(_c.Triangles2B0, start, end, out _, out _, out var tri)) continue; // Amedo
            CharacterCollision.TrianglePlane(tri.A, tri.B, tri.C, out var n, out _);
            if (0f < n.Y) n = -n;
            count += 1f;
            normal += n;
        }
        if (count == 0f) return false;
        float k = 1f / count;
        normal = new Vector3(-(normal.X * k), -(normal.Y * k), -(normal.Z * k));
        return true;
    }

    private void LegProbes(float dt)
    {
        var m = _c.GameMatrix;
        var pos = m.Translation;
        var right = new Vector3(m.M11, m.M12, m.M13);
        var fwd = new Vector3(m.M31, m.M32, m.M33);
        var l = pos + right * -0.23f;
        var r = pos + right * 0.23f;
        float hl = Probe(l), hr = Probe(r);
        float fl = hl == Far ? 1f : 1f - Math.Clamp(hl, -0.3f, 0.3f) * 0.8333333f;
        float fr = hr == Far ? 1f : 1f - Math.Clamp(hr, -0.3f, 0.3f) * 0.8333333f;
        float k = dt * 5f;
        if (1f < k) k = 1f;
        LegRight = (1f - k) * LegRight + k * fr;
        LegLeft = (1f - k) * LegLeft + k * fl;
        float hlf = Probe(pos + right * -0.23f + fwd * 0.15f);
        float hrf = Probe(pos + right * 0.23f + fwd * 0.15f);
        float k2 = dt * 14f;
        if (1f < k2) k2 = 1f;
        float pl = hl == Far || hlf == Far ? 0f : Math.Clamp((hl - hlf) * 6.4f, -0.25f, 0.25f);
        PitchLeft = (1f - k2) * PitchLeft + k2 * pl;
        float pr = hr == Far || hrf == Far ? 0f : Math.Clamp((hr - hrf) * 6.4f, -0.25f, 0.25f);
        PitchRight = (1f - k2) * PitchRight + k2 * pr;
    }

    private float Probe(Vector3 p)
    {
        var from = new Vector3(p.X, p.Y + 0.5f, p.Z);
        var to = new Vector3(p.X, p.Y - 0.5f, p.Z);
        return Segment(from, to, out var hit) ? hit.Y - p.Y : Far;
    }

    private bool Segment(Vector3 from, Vector3 to, out Vector3 hit)
    {
        if (CharacterCollision.TriangleListRayCast_2928E8(_c.Triangles2B0, from, to, out float at, out hit, out _)) // Amedo
        {
            if (0f < at)
            {
                var mid = from * at + to * (1f - at);
                float t = Bodies(from, mid);
                if (0f <= t && t <= 1f) hit = from * (1f - t) + mid * t;
            }
            return true;
        }
        hit = default;
        float tb = Bodies(from, to);
        if (!(0f <= tb && tb <= 1f)) return false;
        hit = from * (1f - tb) + to * tb;
        return true;
    }

    private float Bodies(Vector3 a, Vector3 b) => _c.LimbBodies is { } bodies
        ? Physics.HullQueries.SegmentBodies_1F1740(bodies(Vector3.Min(a, b), Vector3.Max(a, b)), a, b)
        : Far;

    private Record? Find(int reactId)
    {
        foreach (var rec in Records)
            if (rec.Joint == reactId) return rec;
        return null;
    }

    public void Rotate(int reactId, ref Quaternion rot, bool hasRotation)
    {
        if (Look.Rotate_148148(reactId, ref rot, hasRotation)) return; // Amedo 2026-09-29
        if (Find(reactId) is not { Mode: Spring } rec) return;
        float alpha = DisplayAlpha; // Amedo 2026-09-30
        var q = GameMath.Euler_186780(Shown(rec, 0, alpha), Shown(rec, 1, alpha), Shown(rec, 2, alpha));
        rot = hasRotation ? Quaternion.Multiply(q, rot) : q;
    }

    // Amedo 2026-09-30
    private static int Shown(Record rec, int i, float alpha) =>
        alpha <= 0f ? rec.PrevAngles[i] : (int)MathF.Round(rec.PrevAngles[i] + (rec.Angles[i] - rec.PrevAngles[i]) * alpha);

    public int ShownAngle(int reactId, int i) => Find(reactId) is { Mode: Spring } rec ? Shown(rec, i, DisplayAlpha) : 0; // Amedo 2026-09-30

    private float DisplayAlpha => SimulationClock.Active ? SimulationClock.Alpha : _springTicks / (float)SpringStepTicks; // Amedo 2026-09-30

    public bool Transform(int reactId, ref Matrix4x4 model)
    {
        if (Look.Transform_148148(reactId, ref model, _c.GameMatrix)) return true; // Amedo
        if (_c.Link is not null && _c.ArmBone_14C7D8(reactId, ref model)) return true; // Amedo 2026-10-05
        if (Find(reactId) is not { } rec) return false;
        var b = model;
        b.M41 = b.M42 = b.M43 = 0f;
        b.M44 = 1f;
        Matrix4x4 r;
        switch (rec.Mode)
        {
            case Leg:
            {
                var x = Matrix4x4.Identity;
                switch (reactId)
                {
                    case 6: x.M22 = LegLeft; break;
                    case 9: x.M22 = LegRight; break;
                    case 8: x.M22 = 1f / LegLeft; x.M32 = -PitchLeft; break;
                    case 11: x.M22 = 1f / LegRight; x.M32 = -PitchRight; break;
                }
                r = b * x;
                break;
            }
            case Hips:
            {
                var n = rec.Normal;
                var c = new Vector3(-(n.Y * 0f - n.Z * 1f), -(n.Z * 0f - n.X * 0f), -(n.X * 1f - n.Y * 0f));
                if (!(0.001f < MathF.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z)))
                {
                    r = b;
                    break;
                }
                int angle = GameMath.Acos_18B738(0f * n.X + 1f * n.Y + 0f * n.Z);
                var q = GameMath.AxisAngle_18D798(c, angle);
                var node = _c.GameMatrix;
                node.M41 = node.M42 = node.M43 = 0f;
                node.M44 = 1f;
                var qm = GameMath.QuatToMatrix_18E620(q);
                qm.M41 = qm.M42 = qm.M43 = 0f;
                qm.M44 = 1f;
                qm = node * qm;
                qm *= GameMath.RigidInverse_18E808(node);
                r = b * qm;
                break;
            }
            case Nose:
            {
                var x = Matrix4x4.Identity;
                x.M33 = NoseScale;
                r = x * b;
                break;
            }
            default:
                return false;
        }
        r.M41 = model.M41;
        r.M42 = model.M42;
        r.M43 = model.M43;
        r.M44 = model.M44;
        model = r;
        return true;
    }
}
