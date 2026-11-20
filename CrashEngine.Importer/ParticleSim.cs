using System.Numerics;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using CrashEngine.Core;

namespace CrashEngine.Importer;

// Amedo 2026-09-26
public static class ParticleNames
{
    public static string Clean(char[] name)
    {
        var s = new string(name);
        int z = s.IndexOf('\0');
        if (z >= 0) s = s[..z];
        return s.Trim();
    }
}

// Amedo 2026-09-26
public sealed class ParticleDef
{
    public const int SampleCount = 64;
    public const float AngleUnit = MathF.PI * 2f / 65536f;
    public const float AspectFix = 512f / 448f;
    public const float FocalX = 7727.6f;
    public const float FocalY = 8654.9f;
    public const float DeadSample = 62.9f;

    public struct Sample
    {
        public Vector2 A, D, E;
        public Vector4 Color;
    }

    public readonly TwinParticleSystem Source;
    public readonly string Name;
    public readonly int SpawnClass, Blend, Page, Rate, Phase, OnFrames, OnRand, OffFrames, OffRand, RepeatCount, MaxAlive, RingSize;
    public readonly float EmitMinDist, EmitMaxDist, DrawMaxDist, BaseSpeedY, GravityHalf, Life, SampleRate, RepeatDelay, RadiusRamp;
    public readonly int SphereCount; // Amedo
    public readonly long[] SphereRadius; // Amedo
    public readonly float TowardCamera; // Amedo
    public readonly Vector3 Bounds; // Amedo
    public readonly Vector3 VelRange, PosRange, StartScale, StartBase, VelScale, VelBase; // Amedo
    public readonly Vector4 UvRect;
    public readonly Vector4 Swatch;
    public readonly Sample[] Samples = new Sample[SampleCount];

    public ParticleDef(TwinParticleSystem s)
    {
        Source = s;
        Name = ParticleNames.Clean(s.Name);
        SpawnClass = s.UnkByte2;
        Blend = s.UnkByte4;
        Page = s.UnkInt;
        Rate = (short)s.UnkUShort1;
        Phase = (short)s.UnkUShort3;
        OnFrames = (short)s.UnkUShort4;
        OnRand = (short)s.UnkUShort5;
        OffFrames = (short)s.UnkUShort6;
        OffRand = (short)s.UnkUShort7;
        EmitMinDist = s.UnkFloat2;
        EmitMaxDist = s.UnkFloat3;
        DrawMaxDist = s.UnkFloat4;
        BaseSpeedY = s.UnkFloat7;
        VelRange = new Vector3(s.UnkVec1.X, s.UnkVec1.Y, s.UnkVec1.Z);
        PosRange = new Vector3(s.UnkVec2.X, s.UnkVec2.Y, s.UnkVec2.Z);
        StartScale = new Vector3(s.UnkFloat8, s.UnkFloat9, s.UnkFloat10); // Amedo
        StartBase = new Vector3(s.UnkFloat11, s.UnkFloat12, s.UnkFloat13);
        VelScale = new Vector3(s.UnkFloat14, s.UnkFloat15, s.UnkFloat16);
        VelBase = new Vector3(s.UnkFloat17, s.UnkFloat18, s.UnkFloat19);
        GravityHalf = s.UnkFloat20;
        Life = s.UnkFloat21;
        SampleRate = Life > 0f ? 64f / Life : 0f;
        RepeatCount = Math.Max(0, (int)s.UnkShorts[1]);
        RepeatDelay = s.UnkFloat38;
        RadiusRamp = s.UnkFloat40;
        TowardCamera = s.UnkFloat37; // Amedo
        Bounds = new Vector3(s.UnkVec3.X, s.UnkVec3.Y, s.UnkVec3.Z); // Amedo
        SphereCount = (sbyte)s.UnkByte8; // Amedo
        SphereRadius = s.UnkLongs7 ?? Array.Empty<long>(); // Amedo
        UvRect = new Vector4(Texel(s.UnkFloat33), Texel(s.UnkFloat34), Texel(s.UnkFloat35), Texel(s.UnkFloat36));
        MaxAlive = ComputeMaxAlive(s);
        RingSize = ((MaxAlive + 31) >> 5) > 32 ? 1024 : MaxAlive;
        Bake(s);

        var sum = Vector3.Zero;
        var plain = Vector3.Zero;
        float weight = 0f;
        foreach (var smp in Samples)
        {
            var rgb = new Vector3(smp.Color.X, smp.Color.Y, smp.Color.Z);
            sum += rgb * smp.Color.W;
            plain += rgb;
            weight += smp.Color.W;
        }
        var avg = weight > 0f ? sum / weight : plain / SampleCount;
        Swatch = new Vector4(Math.Clamp(avg.X, 0f, 1f), Math.Clamp(avg.Y, 0f, 1f), Math.Clamp(avg.Z, 0f, 1f), 1f);
    }

    private static float Texel(float v) => (((int)(v * 16f)) & 0x3FFF) / 16f;

    private static float Lo(long p) => BitConverter.Int32BitsToSingle((int)p);
    private static float Hi(long p) => BitConverter.Int32BitsToSingle((int)(p >> 32));

    public static float Curve(long[] c, float x)
    {
        for (int k = 0; k < 7 && k + 1 < c.Length; k++)
        {
            float t0 = Lo(c[k]), t1 = Lo(c[k + 1]);
            if (t0 <= x && x <= t1)
            {
                float v0 = Hi(c[k]);
                float dx = x - t0;
                if (dx == 0f) return v0;
                return v0 + dx / (t1 - t0) * (Hi(c[k + 1]) - v0);
            }
        }
        return 0f;
    }

    public static Vector3 ColorCurve(TwinVec4[] c, float x)
    {
        for (int k = 0; k < 7 && k + 1 < c.Length; k++)
        {
            float t0 = c[k].X, t1 = c[k + 1].X;
            if (t0 <= x && x <= t1)
            {
                var v0 = new Vector3(c[k].Y, c[k].Z, c[k].W);
                float dx = x - t0;
                if (dx == 0f) return v0;
                var v1 = new Vector3(c[k + 1].Y, c[k + 1].Z, c[k + 1].W);
                return v0 + dx / (t1 - t0) * (v1 - v0);
            }
        }
        return Vector3.Zero;
    }

    private static float LowByte(float v) => (int)v & 0xFF;

    private void Bake(TwinParticleSystem s)
    {
        for (int i = 0; i < SampleCount; i++)
        {
            float t = i / 64f;
            float w = Curve(s.UnkLongs2, t);
            float h = Curve(s.UnkLongs3, t);
            float rot = (int)Curve(s.UnkLongs4, t) * AngleUnit;
            float sR = MathF.Sin(rot), cR = MathF.Cos(rot);
            float wx = 2f * s.UnkFloat24 * MathF.Sin((int)(s.UnkFloat23 * t * 65536f) * AngleUnit);
            float wy = 2f * s.UnkFloat26 * MathF.Sin((int)(s.UnkFloat25 * t * 65536f) * AngleUnit);

            ref var smp = ref Samples[i];
            smp.A = new Vector2(AspectFix * (-w * cR - h * sR + wx), w * sR - h * cR + wy);
            smp.D = new Vector2(AspectFix * (2f * h * sR), 2f * h * cR);
            smp.E = new Vector2(AspectFix * (w * cR + h * sR + wx), -w * sR + h * cR + wy);

            var rgb = ColorCurve(s.UnkVecs, t);
            float alpha = Curve(s.UnkLongs1, t);
            smp.Color = new Vector4(LowByte(rgb.X) / 255f, LowByte(rgb.Y) / 255f, LowByte(rgb.Z) / 255f, LowByte(alpha) / 128f);
        }
    }

    private static int ComputeMaxAlive(TwinParticleSystem s)
    {
        int n = (int)(s.UnkFloat21 * 60f);
        int rate = (short)s.UnkUShort1;
        int u4 = (short)s.UnkUShort4, u5 = (short)s.UnkUShort5, u6 = (short)s.UnkUShort6;
        int window = u6 != 0 ? u4 + u5 : n;
        int count = 0, k = 0, wait = 1;
        for (int i = n; i > 0; i--)
        {
            if (window != 0)
            {
                if (rate >= 0) count += rate;
                else if (k % -rate == 0) count++;
                window--;
                if (window == 0) wait = u6;
                k++;
            }
            else
            {
                wait--;
                if (wait == 0) { window = u4 + u5; k = 0; }
            }
        }
        int max = (short)count > 0 ? (short)count : 1;
        max = (short)(max * (s.UnkShorts[1] + 1));
        return Math.Max(max, 1);
    }
}

// Amedo 2026-09-26
public sealed class ParticleWorld
{
    public static float Dt { get; set; } = 1f / 60f;
    public static bool EmitFar { get; set; } = true; // Amedo 2026-11-20
    private const float MaxCatchUp = 0.25f;

    public float Clock;
    public int Frame;
    public CrashEngine.Core.GameRandom Rng = CrashEngine.Core.GameRandom.Shared; // Amedo
    public readonly List<ParticleEmitterSim> Emitters = new();
    public Func<bool>? AnchorDrawn; // Amedo
    public Func<Vector3, Vector3, bool>? BoxInView; // Amedo
    public readonly DecalWorld Decals = new(); // Amedo 2026-09-30
    public readonly List<SkidMarkRing> SkidMarks = new(); // Amedo
    public Matrix4x4 SkidToAnchor = Matrix4x4.Identity; // Amedo

    private uint _floatSeed = 12345u;
    private int _intSeed = 1;
    private float _acc;
    public bool LogicDriven; // Amedo
    public float RenderCarry; // Amedo

    public float RenderTime => Clock + (LogicDriven ? RenderCarry : _acc); // Amedo

    // Amedo
    public void EndFrame_100D78(Vector3? camera, bool distanceGating)
    {
        Tick(camera, distanceGating);
        Decals.Step_1AD158();
    }

    public float Rand01()
    {
        _floatSeed = _floatSeed * 1664525u + 1013904223u;
        return BitConverter.Int32BitsToSingle((int)(0x3F800000u | (_floatSeed & 0x7FFFFFu))) - 1f;
    }

    public int RandInt()
    {
        int s = _intSeed ^ 0x075BD924;
        int k = s / 127773;
        s = 16807 * (s - k * 127773) - 2836 * k;
        if (s < 0) s += 0x7FFFFFFF;
        _intSeed = s ^ 0x075BD924;
        return _intSeed;
    }

    public void Advance(float realDt, Vector3? camera, bool distanceGating)
    {
        _acc += Math.Clamp(realDt, 0f, MaxCatchUp);
        while (_acc >= Dt)
        {
            _acc -= Dt;
            Tick(camera, distanceGating);
            Decals.Step_1AD158(); // Amedo 2026-09-30
        }
    }

    public void Tick(Vector3? camera, bool distanceGating)
    {
        Clock += Dt;
        Frame++;
        CollisionSpheres_3A0B10.Clear(); // Amedo
        foreach (var em in Emitters)
            if (em.Enabled) em.UpdateCollisionSpheres_1B6B40(this); // Amedo
        foreach (var em in Emitters)
        {
            if (!em.Enabled) continue;
            float dist = camera is { } c && SceneDrawn_1E8(em) ? GameMath.Distance(c, em.Origin) : 1e7f; // Amedo
            em.Process(this, dist, distanceGating);
        }
        foreach (var em in Emitters)
            if (em.Enabled) em.ProcessBounces(this);
        if (_ghosts.Count > 0) // Amedo 2026-11-11
        {
            Emitters.AddRange(_ghosts);
            _ghosts.Clear();
        }
        Emitters.RemoveAll(em => em.GhostOf is { } of && (!em.AnyAlive(Clock) || !Emitters.Contains(of))); // Amedo 2026-11-11
    }

    // Amedo 2026-11-11
    private readonly List<ParticleEmitterSim> _ghosts = new();

    internal void AddGhost(ParticleEmitterSim ghost) => _ghosts.Add(ghost);

    public void Reset()
    {
        Clock = 0f;
        Frame = 0;
        _acc = 0f;
        _ghosts.Clear(); // Amedo 2026-11-11
        Emitters.RemoveAll(em => em.GhostOf is not null); // Amedo 2026-11-11
        CollisionSpheres_3A0B10.Clear(); // Amedo
        foreach (var em in Emitters) em.Restart(this);
    }

    // Amedo
    public bool SceneDrawn_1E8(ParticleEmitterSim em) => (em.SceneDrawn ?? AnchorDrawn)?.Invoke() != false;

    public readonly List<ParticleEmitterSim> CollisionSpheres_3A0B10 = new(); // Amedo

    // Amedo
    public ParticleEmitterSim? TestCollisionSpheres_1B7738(Vector3 point, float radius, float yScale)
    {
        float invY = 1f / yScale;
        foreach (var em in CollisionSpheres_3A0B10)
            if (em.SphereHit_1B7738(point, radius, invY)) return em;
        return null;
    }
}

// Amedo 2026-09-26
public sealed class ParticleEmitterSim
{
    public struct Particle
    {
        public Vector3 Pos, Vel;
        public Vector3 Start; // Amedo
        public float Birth, Rate;
        public bool Used;
    }

    private struct Bounce
    {
        public int Index;
        public int DueFrame;
        public float HitAge;
    }

    public readonly ParticleDef Def;
    public Vector3 Origin;
    public Matrix4x4 EmitRotation = Matrix4x4.Identity;
    public Matrix4x4 RenderRotation = Matrix4x4.Identity;
    public int Phase;
    public float Ground;
    public float Restitution = 0.9f;
    public int BurstCycles;
    public bool Enabled = true;
    public object? Tag;
    public bool WorldSpace; // Amedo 2026-09-27
    public bool IgnoreDistance; // Amedo 2026-09-27
    public Func<bool>? SceneDrawn; // Amedo
    public Vector3? RenderOffset; // Amedo
    public float Scale = 1f; // Amedo

    private struct CollisionSphere
    {
        public Vector3 Dir;
        public float Age;
    }

    private CollisionSphere[] _spheres = Array.Empty<CollisionSphere>(); // Amedo
    private int _sphereCursor1D0; // Amedo
    private short _sphereTimer1D2 = 1; // Amedo

    public Vector3 DrawOrigin => WorldSpace ? Vector3.Zero : Origin; // Amedo 2026-09-27

    public bool Finished { get; private set; }
    public Particle[] Ring => _ring;

    private Particle[] _ring = Array.Empty<Particle>();
    private int _ringIdx;
    private bool _emitting, _stopped;
    private int _onLeft, _nextFrame, _cyclesLeft;
    private short _spin144, _spin146;
    private float _createdClock;
    private readonly List<Bounce> _bounces = new();

    public ParticleEmitterSim(ParticleDef def, ParticleWorld world, int phase, int burstCycles = 0)
    {
        Def = def;
        Phase = phase;
        BurstCycles = burstCycles;
        Restart(world);
    }

    public static Matrix4x4 EmitterRotation(short aZ, short aY, short aX) =>
        Matrix4x4.CreateRotationZ(aZ * ParticleDef.AngleUnit) *
        Matrix4x4.CreateRotationY(aY * ParticleDef.AngleUnit) *
        Matrix4x4.CreateRotationX(aX * ParticleDef.AngleUnit);

    public static Matrix4x4 EmitterRenderRotation(short aZ, short aY) =>
        Matrix4x4.CreateRotationZ(aZ * ParticleDef.AngleUnit) *
        Matrix4x4.CreateRotationY(aY * ParticleDef.AngleUnit);

    public void ApplyEmitter(TwinParticleEmitter em)
    {
        EmitRotation = EmitterRotation(em.UnkShort3, em.UnkShort4, em.UnkShort5);
        RenderRotation = EmitterRenderRotation(em.UnkShort1, em.UnkShort2);
        Ground = em.UnkFloat2;
        Restitution = em.UnkFloat3;
    }

    public void Restart(ParticleWorld world)
    {
        Clear();
        Finished = false;
        _stopped = false;
        _released = false; // Amedo 2026-09-29
        _createdClock = world.Clock;
        _spheres = 0 < Def.SphereCount ? new CollisionSphere[Def.SphereCount] : Array.Empty<CollisionSphere>(); // Amedo
        for (int i = 0; i < _spheres.Length; i++) _spheres[i].Age = -1f;
        _sphereCursor1D0 = 0;
        _sphereTimer1D2 = 1;
        _onLeft = Def.OnFrames;
        _cyclesLeft = BurstCycles;
        _emitting = BurstCycles > 0;
        _spin144 = _spin146 = 0;
        int wait = 0;
        int period = Def.OnFrames + Def.OffFrames;
        if ((Def.OnRand | Def.OffRand) == 0) // Amedo
        {
            if (Def.SpawnClass == 7)
            {
                _spin144 = (short)(int)(Phase * Def.VelRange.Y);
                _spin146 = (short)(int)(Phase * Def.VelRange.Z);
            }
            if (BurstCycles == 0 && period > 0)
            {
                int v = world.Frame % period - Phase;
                wait = v == 0 ? 0 : period - v;
            }
        }
        _nextFrame = world.Frame + wait;
    }

    public void Clear()
    {
        _ring = Array.Empty<Particle>();
        _ringIdx = 0;
        _bounces.Clear();
    }

    // Amedo 2026-09-29
    public void Release_1B47E0()
    {
        _stopped = true;
        _emitting = false;
        Finished = true;
        _released = true;
    }

    private bool _released; // Amedo 2026-09-29

    // Amedo 2026-11-11
    internal ParticleEmitterSim? GhostOf { get; private set; }

    private ParticleEmitterSim DetachLive_1B43B4(ParticleWorld world)
    {
        var ghost = new ParticleEmitterSim(Def, world, Phase)
        {
            Origin = Origin, EmitRotation = EmitRotation, RenderRotation = RenderRotation, Ground = Ground, Restitution = Restitution,
            WorldSpace = WorldSpace, IgnoreDistance = IgnoreDistance, SceneDrawn = SceneDrawn, RenderOffset = RenderOffset,
            Scale = Scale, Enabled = Enabled,
        };
        ghost._ring = _ring;
        ghost._ringIdx = _ringIdx;
        ghost._bounces.AddRange(_bounces);
        ghost._spheres = Array.Empty<CollisionSphere>();
        ghost.GhostOf = this;
        ghost.Release_1B47E0();
        return ghost;
    }

    public bool AnyAlive(float now)
    {
        foreach (ref readonly var p in _ring.AsSpan())
            if (p.Used && (now - p.Birth) * p.Rate <= ParticleDef.DeadSample) return true;
        return false;
    }

    internal void Process(ParticleWorld world, float dist, bool distanceGating)
    {
        if (_released || world.Frame < _nextFrame) return; // Amedo 2026-09-29

        float maxDist = IgnoreDistance || ParticleWorld.EmitFar ? 0f : Def.EmitMaxDist; // Amedo 2026-11-20
        if (_emitting)
        {
            if (dist < Def.EmitMinDist) _emitting = false;
            else if (maxDist > 0f && maxDist < dist) _emitting = false;
        }
        if (!distanceGating) _emitting = true;
        if (_stopped) _emitting = false;

        if (_emitting)
        {
            if (Def.SampleRate > 0f)
            {
                if (_ring.Length != Def.RingSize)
                {
                    _ring = new Particle[Def.RingSize];
                    _ringIdx = 0;
                    _bounces.Clear();
                }
                if (Def.Rate > 0)
                    for (int i = 0; i < Def.Rate; i++) SpawnOne(world);
                else if (Def.Rate < 0 && world.Frame % -Def.Rate == 0)
                    SpawnOne(world);
            }
        }
        else if (!_stopped)
        {
            if (Def.EmitMinDist <= dist && (maxDist == 0f || dist <= maxDist))
                _emitting = true;
            else if (_ring.Length != 0)
            {
                if (AnyAlive(world.Clock)) world.AddGhost(DetachLive_1B43B4(world)); // Amedo 2026-11-11
                Clear();
            }
        }

        _onLeft--;
        int wait;
        if (_emitting) wait = 1;
        else if (Def.OffFrames == 0 || Def.OffRand != 0) wait = world.RandInt() % 8 + 50;
        else wait = 1;
        if (_onLeft == 0)
        {
            wait += Def.OffFrames;
            if (Def.OffRand != 0) wait += world.RandInt() % Def.OffRand + 1;
            _onLeft = Def.OnFrames;
            if (Def.OnRand != 0) _onLeft += world.RandInt() % Def.OnRand + 1;
            if (_cyclesLeft != 0 && --_cyclesLeft == 0)
            {
                Release_1B47E0(); // Amedo
                wait = 33;
            }
        }
        _nextFrame = world.Frame + wait;
    }

    // Amedo
    internal void UpdateCollisionSpheres_1B6B40(ParticleWorld world)
    {
        if (_spheres.Length == 0) return;
        world.CollisionSpheres_3A0B10.Add(this);
        for (int j = 0; j < _spheres.Length; j++)
        {
            ref var s = ref _spheres[j];
            if (s.Age == -1f) continue;
            s.Age += ParticleWorld.Dt;
            if (Def.Life < s.Age) s.Age = -1f;
        }
        _sphereTimer1D2--;
        if (world.Frame < _nextFrame || !_emitting || 0 < _sphereTimer1D2) return;
        ref var b = ref _spheres[_sphereCursor1D0];
        b.Age = 0f;
        b.Dir = Vector3.TransformNormal(new Vector3(0f, Def.BaseSpeedY, 0f), EmitRotation);
        if (++_sphereCursor1D0 >= _spheres.Length) _sphereCursor1D0 = 0;
        _sphereTimer1D2 = (short)(int)(Def.Life * 60f / _spheres.Length);
    }

    // Amedo
    internal bool SphereHit_1B7738(Vector3 point, float radius, float invY)
    {
        var origin = RenderOffset ?? Origin;
        for (int j = 0; j < _spheres.Length; j++)
        {
            float t = _spheres[j].Age;
            if (t == -1f) continue;
            var pos = Vector3.TransformNormal(_spheres[j].Dir, RenderRotation) * t;
            pos.Y += Def.GravityHalf * t * t;
            pos += origin;
            float r = ParticleDef.Curve(Def.SphereRadius, t / Def.Life);
            if (!(0f < r)) continue;
            float reach = r + radius;
            var d = point - pos;
            if (invY != 1f) d.Y *= reach / (r + radius * invY);
            if (GameMath.LengthSquared(d) < reach * reach) return true;
        }
        return false;
    }

    private int NextSlot()
    {
        if (_ringIdx >= _ring.Length) _ringIdx = 0;
        return _ringIdx++;
    }

    private static Vector3 RotZ(Vector3 v, int angle)
    {
        float a = angle * ParticleDef.AngleUnit, s = MathF.Sin(a), c = MathF.Cos(a);
        return new Vector3(v.X * c - v.Y * s, v.X * s + v.Y * c, v.Z);
    }

    private static Vector3 RotY(Vector3 v, int angle)
    {
        float a = angle * ParticleDef.AngleUnit, s = MathF.Sin(a), c = MathF.Cos(a);
        return new Vector3(v.X * c + v.Z * s, v.Y, v.Z * c - v.X * s);
    }

    // Amedo
    private const int LineAngle = 0x36B0, LineOutAngle = 0x7530;
    private static readonly float LineAlongScale = BitConverter.Int32BitsToSingle(0x2F99999A);
    private static readonly float LineAlongReach = BitConverter.Int32BitsToSingle(0x3E99999A);
    private static readonly float LineOutScale = BitConverter.Int32BitsToSingle(0x30400000);
    private static readonly float LifeLonger = BitConverter.Int32BitsToSingle(0x3036DB6E);

    // Amedo
    private static float RandomRange(CrashEngine.Core.GameRandom rng, float scale, float baseValue) =>
        CrashEngine.Core.GameRandom.CvtSW(rng.GetRand_2C8230()) * scale + baseValue;

    // Amedo
    private static float RandomLifeScale_1B3190(CrashEngine.Core.GameRandom rng, float life)
    {
        float longer = life * CrashEngine.Core.GameRandom.CvtSW(rng.GetRand_2C8230());
        longer *= LifeLonger;
        return 64f / (life + longer);
    }

    // Amedo
    private static int AsinAngle(float x) =>
        CrashEngine.Core.GameMath.Trunc(CrashEngine.Core.GameMath.Asin16_18B7F8(x) * CrashEngine.Core.GameMath.UnitToRad * CrashEngine.Core.GameMath.RadToUnit);

    // Amedo
    private static float Sym(ParticleWorld world, float range)
    {
        float u = world.Rand01();
        return (u + u) * range - range;
    }

    private void SpawnOne(ParticleWorld world)
    {
        var d = Def;
        int idx = NextSlot();
        float birth = world.Clock + ParticleWorld.Dt;
        float r() => world.Rand01() * 2f - 1f;
        Vector3 pos, vel;
        float rate = d.SampleRate; // Amedo

        switch (d.SpawnClass)
        {
            case 6:
            {
                int phi = (int)(Sym(world, d.VelRange.Y) + d.PosRange.Y); // Amedo
                int theta = (int)(Sym(world, d.VelRange.Z) + d.PosRange.Z);
                float radius = d.PosRange.X;
                if (d.RadiusRamp != 0f)
                {
                    float e = world.Clock - _createdClock;
                    if (e < d.RadiusRamp) radius = d.PosRange.X * (e / d.RadiusRamp);
                }
                pos = RotY(RotZ(new Vector3(0f, radius, 0f), theta), phi);
                vel = RotY(RotZ(new Vector3(0f, Sym(world, d.VelRange.X) + d.BaseSpeedY, 0f), theta), phi); // Amedo
                break;
            }
            case 7:
            {
                int theta = (int)(_spin146 + d.PosRange.Z);
                int phi = (int)(_spin144 + d.PosRange.Y);
                pos = RotY(RotZ(new Vector3(0f, d.PosRange.X, 0f), theta), phi);
                vel = RotY(RotZ(new Vector3(0f, Sym(world, d.VelRange.X) + d.BaseSpeedY, 0f), theta), phi); // Amedo
                break;
            }
            case 8:
            {
                var p = new Vector3(MathF.Sqrt(world.Rand01()), 0f, 0f);
                int phi = (int)world.Rand01() << 16;
                int theta = (short)AsinAngle(r());
                p = RotY(RotZ(p, theta), phi);
                pos = p * d.PosRange;
                vel = new Vector3(Sym(world, d.VelRange.X), Sym(world, d.VelRange.Y) + d.BaseSpeedY, Sym(world, d.VelRange.Z)); // Amedo
                break;
            }
            case 11:
            {
                int v = (int)(world.Rand01() * 65536f) - 0x8000;
                float s0 = MathF.Sin((int)(d.PosRange.Z - d.VelRange.Z) * ParticleDef.AngleUnit);
                float s1 = MathF.Sin((int)(d.PosRange.Z + d.VelRange.Z) * ParticleDef.AngleUnit);
                int theta = (short)AsinAngle(s0 + world.Rand01() * (s1 - s0));
                int phi = (int)d.PosRange.Y + (int)(v * d.VelRange.Y) / 32768;
                pos = RotY(RotZ(new Vector3(d.PosRange.X, 0f, 0f), theta), phi);
                vel = RotY(RotZ(new Vector3(Sym(world, d.VelRange.X) + d.BaseSpeedY, 0f, 0f), theta), phi); // Amedo
                break;
            }
            case 2: // Amedo
            case 5:
                if (d.SpawnClass == 5) rate = RandomLifeScale_1B3190(world.Rng, d.Life);
                pos = new Vector3(RandomRange(world.Rng, d.StartScale.X, d.StartBase.X), RandomRange(world.Rng, d.StartScale.Y, d.StartBase.Y), RandomRange(world.Rng, d.StartScale.Z, d.StartBase.Z));
                vel = new Vector3(RandomRange(world.Rng, d.VelScale.X, d.VelBase.X), RandomRange(world.Rng, d.VelScale.Y, d.VelBase.Y), RandomRange(world.Rng, d.VelScale.Z, d.VelBase.Z));
                break;
            case 3: // Amedo
            {
                float along = CrashEngine.Core.GameRandom.CvtSW(world.Rng.GetRand_2C8230()) * LineAlongScale - LineAlongReach;
                float sx = along * MathF.Sin(LineAngle * ParticleDef.AngleUnit), sz = along * MathF.Cos(LineAngle * ParticleDef.AngleUnit);
                float spread = CrashEngine.Core.GameRandom.CvtSW(world.Rng.GetRand_2C8230()) * LineOutScale + 1f;
                float vx = sx * 1.5f + spread * MathF.Sin(LineOutAngle * ParticleDef.AngleUnit);
                float vz = sz * 1.5f + spread * MathF.Cos(LineOutAngle * ParticleDef.AngleUnit);
                float sy = RandomRange(world.Rng, d.StartScale.Y, d.StartBase.Y);
                pos = new Vector3(sx, sy, sz);
                vel = new Vector3(vx, RandomRange(world.Rng, d.VelScale.Y, d.VelBase.Y), vz);
                break;
            }
            case 4: // Amedo
            {
                var prev = _ring[idx].Start;
                world.Rng.GetRand_2C8230();
                world.Rng.GetRand_2C8230();
                world.Rng.GetRand_2C8230();
                float sy = RandomRange(world.Rng, d.StartScale.Y, d.StartBase.Y);
                pos = new Vector3(prev.X, sy, prev.Z);
                vel = new Vector3(prev.X + prev.X, RandomRange(world.Rng, d.VelScale.Y, d.VelBase.Y), prev.Z + prev.Z);
                break;
            }
            default:
                pos = new Vector3(Sym(world, d.PosRange.X), Sym(world, d.PosRange.Y), Sym(world, d.PosRange.Z)); // Amedo
                vel = new Vector3(Sym(world, d.VelRange.X), Sym(world, d.VelRange.Y) + d.BaseSpeedY, Sym(world, d.VelRange.Z));
                break;
        }

        var start = pos; // Amedo
        if (Scale != 1f) { pos *= Scale; vel *= Scale; } // Amedo
        pos = Vector3.Transform(pos, EmitRotation);
        vel = Vector3.TransformNormal(vel, EmitRotation);
        if (WorldSpace) pos += Origin; // Amedo 2026-09-27
        _ring[idx] = new Particle { Pos = pos, Vel = vel, Start = start, Birth = birth, Rate = rate, Used = true }; // Amedo

        if (d.SpawnClass == 9) RegisterBounce(world, idx);

        for (int k = 1; k <= d.RepeatCount; k++)
        {
            int copy = NextSlot();
            var p = _ring[idx];
            p.Birth += k * d.RepeatDelay;
            _ring[copy] = p;
        }

        if (d.SpawnClass == 7)
        {
            _spin144 = d.VelRange.Y == 0f ? (short)0 : (short)(_spin144 + (int)d.VelRange.Y);
            _spin146 = d.VelRange.Z == 0f ? (short)0 : (short)(_spin146 + (int)d.VelRange.Z);
        }
    }

    private static bool SolveHit(float a, float b, float c0, out float t1, out float t2)
    {
        t1 = t2 = 0f;
        if (a == 0f)
        {
            if (b == 0f) return false;
            t1 = t2 = -c0 / b;
            return true;
        }
        float bb = b * b, ac4 = a * (c0 * 4f);
        if (bb < ac4) return false;
        if (ac4 == bb)
        {
            t1 = t2 = -b / (a + a);
            return true;
        }
        float sq = MathF.Sqrt(bb - ac4);
        t1 = (-b + sq) / (a + a);
        t2 = (-b - sq) / (a + a);
        return true;
    }

    private void RegisterBounce(ParticleWorld world, int idx)
    {
        ref var p = ref _ring[idx];
        if (!SolveHit(Def.GravityHalf, p.Vel.Y, p.Pos.Y - Ground, out float t1, out float t2)) return;
        float t = MathF.Max(t1, t2);
        if (!(0f < t) || !(t < Def.Life)) return;
        _bounces.Add(new Bounce { Index = idx, DueFrame = world.Frame + (int)(t * 60f), HitAge = t });
    }

    internal void ProcessBounces(ParticleWorld world)
    {
        for (int i = _bounces.Count - 1; i >= 0; i--)
        {
            var ev = _bounces[i];
            if (ev.DueFrame > world.Frame) continue;
            _bounces.RemoveAt(i);
            if (ev.Index >= _ring.Length) continue;

            ref var p = ref _ring[ev.Index];
            float a = Def.GravityHalf, th = ev.HitAge;
            float oldVy = p.Vel.Y;
            float newVy = -Restitution * (oldVy + 2f * a * th) - 2f * a * th;
            p.Vel.Y = newVy;
            p.Pos.Y -= newVy * th - oldVy * th;

            if (!SolveHit(a, newVy, p.Pos.Y - Ground, out float t1, out float t2)) continue;
            float tn = MathF.Max(t1, t2);
            if (!(tn < Def.Life)) continue;
            int frames = (int)((tn - th) * 60f);
            if (frames == 0)
            {
                if (t1 != t2)
                {
                    p.Vel.Y = 0f;
                    p.Pos.Y = Ground;
                }
                continue;
            }
            _bounces.Add(new Bounce { Index = ev.Index, DueFrame = world.Frame + frames, HitAge = tn });
        }
    }
}
