using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private const int MaxTrails = 8;
    private const ulong TrailOffset = 1ul << 37;
    private const ulong TrailFollow = 1ul << 53;

    private sealed class Trail
    {
        public ulong Flags;
        public uint Filter = 0xFFFFFFFF;
        public ushort System;
        public ushort Sound = 0xFFFF;
        public ushort Spawn = 0xFFFF;
        public byte Attach;
        public float Interval, Jitter, RateScale;
        public Vector3 Offset;
        public int Handle = -1;
        public int Timer;
        public bool Primed;
        public bool AnimFired; // Amedo 2026-09-30
        public uint[] Words = []; // Amedo 2026-09-28
    }

    private List<Trail>? _trails;
    private const ushort SandStepSystem = 0xF4; // Amedo 2026-09-30

    private void DoParticle(List<uint> a)
    {
        if (World.Particles is not { } fx) return;
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint w0 = Arg(0), w1 = Arg(1);
        var vec = new Vector3(F(5), F(6), F(7));
        int sys = (int)(w0 & 0xFFFF);
        if ((w1 & 0x3FC00000) != 0x3FC00000)
        {
            vec = Key((int)((w1 >> 22) & 0xFF)) ?? Vector3.Zero;
            w0 &= ~0x800000u;
            w1 |= 0x20u;
        }
        if ((w1 & 0x3FC000) != 0x3FC000) World.Unimplemented("particle", 10, "actor particle override (vt+0x7C, A+0x130/0x134)");
        int count = (int)((w0 >> 16) & 0x7F);
        if ((w0 & 0x01800000) != 0)
        {
            int h = fx.Spawn(sys, count, true, Vector3.Zero);
            if (h < 0) return;
            if (ParticleScale != 1f) fx.SetScale(h, ParticleScale); // Amedo
            var m = EmitterMatrix((w1 & 1) != 0, (int)((w1 >> 1) & 0xF), (w1 & 0x20) != 0 ? vec : null, ParticleFrame_223298((int)((w0 >> 25) & 0x3F)));
            fx.SetEmitMatrix(h, RootMatrix(m)); // Amedo 2026-09-28
            Trace($"particle {sys} x{count} at {m.Translation}");
            return;
        }
        if ((w1 & 0x3FC0) != 0x3FC0)
        {
            int code = (int)((w1 >> 6) & 0xFF);
            var target = code < 0xDE ? Participant(code) : CodeObject(code);
            if (target is not null) vec = target.GamePosition;
            else World.Unimplemented("particle", 10, "position by code (vt+0x12C)");
        }
        var pos = (w1 & 0x20) != 0 ? vec : Vector3.Zero;
        int spawned = fx.Spawn(sys, count, true, RootPoint(pos)); // Amedo 2026-09-28
        if (spawned >= 0 && ParticleScale != 1f) fx.SetScale(spawned, ParticleScale); // Amedo
        Trace($"particle {sys} x{count} at {pos}");
    }

    // Amedo
    private float ParticleScale => GameObject is { } obj ? World.ObjectScale(obj) : 1f;

    // Amedo
    private Matrix4x4 ParticleFrame_223298(int exit) => exit == 0x3F || !HasExitNode_2952A0(exit) ? LocalMatrix : ExitAnchor_293B70(exit);

    private static Matrix4x4 EmitterMatrix(bool keepRotation, int axis, Vector3? offset, Matrix4x4 m)
    {
        var a0 = new Vector3(m.M11, m.M12, m.M13);
        var a1 = new Vector3(m.M21, m.M22, m.M23);
        var a2 = new Vector3(m.M31, m.M32, m.M33);
        Vector3 r0 = keepRotation ? a0 : Vector3.UnitX, r1 = keepRotation ? a1 : Vector3.UnitY, r2 = keepRotation ? a2 : Vector3.UnitZ;
        var t = m.Translation;
        if (offset is { } o) t += a0 * o.X + a1 * o.Y + a2 * o.Z;
        (r0, r1, r2) = axis switch
        {
            1 => (r0, -r1, r2),
            2 => (r0, r2, -r1),
            3 => (-r0, -r2, r1),
            4 => (-r1, -r0, r2),
            5 => (r1, r0, r2),
            _ => (r0, r1, r2),
        };
        return new Matrix4x4(r0.X, r0.Y, r0.Z, 0f, r1.X, r1.Y, r1.Z, 0f, r2.X, r2.Y, r2.Z, 0f, t.X, t.Y, t.Z, 1f);
    }

    private void AddTrail(List<uint> a) => AddTrailRecord(a, 1);

    internal void RunPackCommand(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommand cmd)
    {
        switch (cmd.CommandIndex)
        {
            case 541: AddTrail(cmd.Arguments); break;
            case 542: ClearTrails(); break;
            case 544: DoParticle(cmd.Arguments); break;
            case 545: DoSound(cmd.Arguments); break; // Amedo 2026-09-28
            case 546: CreateDamage(cmd.Arguments); break;
        }
    }

    internal void AddTrailRecord(IReadOnlyList<uint> words, int start)
    {
        uint W(int i) => start + i < words.Count ? words[start + i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(W(i));
        TrailList(); // Amedo
        if (_trails.Count >= MaxTrails) return;
        _trails.Add(new Trail
        {
            Flags     = W(0) | ((ulong)W(1) << 32),
            Filter    = W(2),
            System    = (ushort)(W(3) & 0xFFFF),
            Sound     = (ushort)(W(3) >> 16),
            Spawn     = (ushort)(W(8) & 0xFFFF),
            Attach    = (byte)((W(8) >> 16) & 0xFF),
            Interval  = F(10),
            Jitter    = F(11),
            RateScale = F(23),
            Offset    = new Vector3(F(24), F(25), F(26)),
            Words     = Enumerable.Range(0, 28).Select(W).ToArray(), // Amedo 2026-09-28
        });
        Trace($"trail added (system 0x{W(3) & 0xFFFF:X}, type {W(0) & 0xF}) [{string.Join(" ", Enumerable.Range(0, 28).Select(i => W(i).ToString("X")))}]"); // Amedo 2026-09-30
    }

    // Amedo 2026-10-04
    internal int UpdateEmitter_23E8F8(ushort system, int exit, AgentLabAgent on, int handle)
    {
        if (World.Particles is not { } fx) return -1;
        if (system == 0xFFFF) return StopEmitter_23EB00(handle);
        if (handle < 0) handle = fx.Spawn(system, 0, true, on.RootPoint(on.GamePosition));
        if (handle < 0) return -1;
        fx.SetEmitMatrix(handle, on.RootMatrix(EmitterMatrix(false, 0, null, on.ParticleFrame_223298(exit))));
        return handle;
    }

    // Amedo 2026-10-04
    internal int StopEmitter_23EB00(int handle)
    {
        if (handle >= 0) World.Particles?.Stop(handle);
        return -1;
    }

    // Amedo 2026-11-16
    internal void UpdateParticleTrails_237A18()
    {
        if (_trails is null || World.Particles is not { } fx) return;
        foreach (var t in _trails)
        {
            int handle = t.Handle;
            if (t.System != 0xFFFF)
            {
                if (handle < 0) handle = StartTrail_23EA48(fx, t);
            }
            else handle = StopEmitter_23EB00(handle);
            if (handle >= 0)
            {
                var m = RootMatrix(TrailEmitMatrix(t));
                if ((t.Flags & TrailFollow) != 0) fx.SetRenderMatrix(handle, m);
                else fx.SetEmitMatrix(handle, m);
            }
            t.Handle = handle;
        }
    }

    // Amedo 2026-11-16
    private int StartTrail_23EA48(RuntimeParticles fx, Trail t) =>
        fx.Spawn(t.System, 0, (t.Flags & TrailFollow) == 0, RootPoint(GamePosition));

    internal bool HasTrails => _trails is { Count: > 0 }; // Amedo 2026-11-21

    internal void AddDefaultTrail(ushort system)
    {
        TrailList(); // Amedo
        if (_trails.Count >= MaxTrails) return;
        _trails.Add(new Trail { Flags = 0x4010ul | ((ulong)0x3FC0 << 32), System = system, Attach = 63 });
    }

    // Amedo
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_trails))]
    private List<Trail> TrailList()
    {
        if (_trails is null)
        {
            _trails = new List<Trail>();
            _trailLevel64 = 1f;
        }
        return _trails;
    }

    internal void ClearTrails()
    {
        if (_trails is null) return;
        foreach (var t in _trails)
            if (t.Handle >= 0) World.Particles?.Stop(t.Handle);
        _trails = null;
    }

    // Amedo 2026-09-30
    private uint _keep88;

    private void Keep_20FE50(uint flags) =>
        _keep88 = (_keep88 & ~0x401F00u) | ((flags & 1) != 0 ? 0x100u : 0u) | ((flags & 2) != 0 ? 0x200u : 0u) | ((flags & 8) != 0 ? 0x800u : 0u) // Amedo
                | ((flags & 4) != 0 ? 0x400u : 0u) | ((flags & 0x10) != 0 ? 0x1000u : 0u) | ((flags & 0x20) != 0 ? 0x400000u : 0u);

    private void ClearTrailCategory_23FD38(int category)
    {
        if (_trails is null) return;
        for (int i = 0; i < _trails.Count;) // Amedo
        {
            if ((int)((_trails[i].Flags >> 50) & 7) != category) { i++; continue; }
            RemoveTrailSlot_23FDD8(i);
        }
    }

    // Amedo
    private void RemoveTrailSlot_23FDD8(int slot)
    {
        var trails = _trails!;
        if (trails[slot].Handle >= 0) World.Particles?.Stop(trails[slot].Handle);
        int last = trails.Count - 1;
        trails[slot] = trails[last];
        trails.RemoveAt(last);
    }

    // Amedo 2026-09-30
    private Vector3 RootDirection(Vector3 d) => ReferenceEquals(Chunk, World.RootChunk) ? d : Vector3.TransformNormal(d, Chunk.ToRoot);

    private void Footprint_21A788(Matrix4x4 foot, Vector3? offset)
    {
        if (World.Particles is not { } fx) return;
        var node = LocalMatrix;
        var pos = foot.Translation;
        if (offset is { } o)
            pos += new Vector3(node.M11, node.M12, node.M13) * o.X + new Vector3(node.M21, node.M22, node.M23) * o.Y + new Vector3(node.M31, node.M32, node.M33) * o.Z;
        pos.Y = node.M42 + 0.09f;
        var forward = new Vector3(node.M31, node.M32, node.M33);
        var dir = new Vector3(foot.M31, foot.M32, foot.M33);
        if (GameMath.Dot(dir, forward) < 0.7f) dir = forward;
        dir.Y = 0f;
        dir *= GameMath.InverseLength(dir);
        if (Tracing) Trace($"footprint at {pos} dir {dir}"); // Amedo 2026-09-30
        fx.AddDecal_1ADC18(RootPoint(pos), RootDirection(Vector3.UnitY), RootDirection(dir), 1, 0, 1, 1);
    }

    private void Ripple_1AE158(Matrix4x4 m) =>
        World.Particles?.AddDecal_1ADC18(RootPoint(m.Translation + new Vector3(0f, 0.09f, 0f)), RootDirection(new Vector3(m.M21, m.M22, m.M23)),
                                          RootDirection(new Vector3(m.M31, m.M32, m.M33)), 0, 0, 0, 0);

    private void WaterRipples_235900(Trail t, Matrix4x4 m)
    {
        int count = (int)((t.Flags >> 54) & 0xF);
        if (count == 0)
        {
            Ripple_1AE158(m);
            return;
        }
        float spread = BitConverter.UInt32BitsToSingle(t.Words[23]);
        for (int i = 0; i < count; i++)
        {
            float x = RandomSigned() * spread; // Amedo
            RandomSigned();
            float z = RandomSigned() * spread;
            var r = m;
            r.Translation += new Vector3(x, 0f, z);
            Ripple_1AE158(r);
        }
    }

    private void DoubleFootprint_137988()
    {
        var own = Instance.Source.ParamList1;
        uint v = own is { Count: > 0 } ? own[0] : 0u;
        float x = v == 0 ? 0.085f : v == 1 ? 0.06f : 0f;
        var offset = v <= 1 ? new Vector3(x, 0f, 0.12f) : Vector3.Zero;
        if (!HasExitNode_2952A0(7) || !HasExitNode_2952A0(6)) return; // Amedo
        Footprint_21A788(ExitAnchor_293B70(6), offset);
        Footprint_21A788(ExitAnchor_293B70(7), offset with { X = -offset.X });
    }

    // Amedo 2026-09-30
    private void Cleanup_23D820()
    {
        if ((_keep88 & 0x100) == 0) ClearTrails();
        else ClearTrailCategory_23FD38(1);
    }

    // Amedo 2026-09-28
    private Matrix4x4 TrailEmitMatrix(Trail t) =>
        EmitterMatrix(((t.Flags >> 18) & 1) != 0, (int)((t.Flags >> 19) & 0xF), (t.Flags & TrailOffset) != 0 ? t.Offset : null,
                      ParticleFrame_223298(t.Attach));

    private bool TimedTrail(Trail t)
    {
        float interval = t.Interval;
        if ((t.Flags & (1ul << 26)) != 0) // Amedo 2026-10-01
        {
            float speed = GameMath.Length(_velocity);
            if (speed < 0.0001f) return false;
            interval = 1f / (speed * t.RateScale);
        }
        int now = World.NowTicks;
        if (t.Primed && !(interval < (now - t.Timer) * (1f / 9000f))) return false;
        bool emit = t.Primed || t.Jitter == 0f;
        t.Primed = true;
        t.Timer = now - (int)(World.Rng.RandomBelowFloat_18C180(t.Jitter) * 9000f); // Amedo
        return emit;
    }

    // Amedo 2026-09-30
    private bool AnimationTrail_235A90(Trail t)
    {
        if (Anim is null) return false;
        if (!(Anim.RemainingSeconds < BitConverter.UInt32BitsToSingle(t.Words[21])))
        {
            t.AnimFired = false;
            return false;
        }
        if (t.AnimFired) return false;
        t.AnimFired = true;
        return true;
    }

    // Amedo 2026-09-30
    private int GroundSurface130 => OwnCharacter is { } c ? c.GroundSurfaceId : _groundSurface;
    private int WetSurface134 => OwnCharacter is { } c ? c.WetSurface : _wetSurface;

    // Amedo 2026-09-30
    private bool EffectSurface(out CollisionWorld.Surface surface, out bool wet)
    {
        int id = WetSurface134;
        wet = id != -1;
        if (!wet) id = GroundSurface130;
        surface = default;
        if (id == -1 || (OwnCharacter?.CollisionWorld ?? Chunk.Collision) is not { } world) return false;
        surface = world.SurfaceAt(id);
        return true;
    }

    private void UpdateTrails()
    {
        if (_trails is null || (World.Particles is null && World.Sounds is null)) return; // Amedo 2026-09-28
        var fx = World.Particles;
        foreach (var t in _trails)
        {
            int type = (int)(t.Flags & 0xF);
            bool emit = false;
            if (t.Filter == 0xFFFFFFFF || t.Filter == (uint)GroundSurface130) // Amedo 2026-09-30
                switch (type)
                {
                    case 0: emit = true; break;
                    case 1: emit = TimedTrail(t); break;
                    case 2: case 6: break;
                    case 7: emit = AnimationTrail_235A90(t); break; // Amedo 2026-09-30
                    case 8: emit = BitConverter.UInt32BitsToSingle(t.Words.Length > 22 ? t.Words[22] : 0u) < _trailLevel64; break; // Amedo
                    default: World.Unimplemented("trail", type, $"motion trail trigger type {type} (A+0x128)"); break;
                }
            // Amedo 2026-09-30
            int select = (int)((t.Flags >> 38) & 0xFF);
            bool surfaced = false, wet = false;
            CollisionWorld.Surface surface = default;
            if (select != 0xFF) surfaced = EffectSurface(out surface, out wet);
            ushort system = surfaced ? surface.ParticleAt(select) : t.System;
            var m = Matrix4x4.Identity;
            bool placed = false, onWater = false;
            if (system != 0xFFFF && type == 7)
            {
                if (emit)
                {
                    placed = true;
                    m = ParticleFrame_223298(t.Attach);
                    if (wet)
                    {
                        m = TrailEmitMatrix(t);
                        onWater = true;
                        m.Translation = WaterPoint140;
                        WaterRipples_235900(t, m); // Amedo 2026-09-30
                    }
                    else if (surfaced && surface.Id is 10 or 15) Footprint_21A788(m, (t.Flags & TrailOffset) != 0 ? t.Offset : null); // Amedo 2026-09-30
                    if (fx is not null && fx.Spawn(system, 1, true, Vector3.Zero) is var h and >= 0) fx.SetEmitMatrix(h, RootMatrix(m));
                }
                else if (t.Handle >= 0 && fx is not null)
                {
                    fx.Stop(t.Handle);
                    t.Handle = -1;
                }
            }
            else if (system == 0xFFFF && surfaced && type == 7 && emit && surface.Id is 10 or 15)
            {
                placed = true;
                m = ParticleFrame_223298(t.Attach);
                Footprint_21A788(m, (t.Flags & TrailOffset) != 0 ? t.Offset : null); // Amedo 2026-09-30
                if (surface.Id == 10) fx?.Spawn(SandStepSystem, 1, true, RootPoint(m.Translation));
            }
            if (emit)
            {
                ushort id = 0xFFFF;
                float volume = (t.Flags & (1ul << 34)) != 0 ? BitConverter.UInt32BitsToSingle(t.Words[18]) : -1f;
                if (surfaced)
                {
                    if ((t.Flags & (1ul << 58)) == 0 && select < 6) id = surface.SoundAt(select, out volume);
                    if (type == 7 && !onWater)
                    {
                        placed = true;
                        m = TrailEmitMatrix(t);
                        if (wet)
                        {
                            m.Translation = WaterPoint140;
                            WaterRipples_235900(t, m); // Amedo 2026-09-30
                        }
                    }
                }
                else if (t.Sound != 0xFFFF) id = TrailSlotSound(t.Flags, t.Words);
                if (!placed && (id != 0xFFFF || (t.Flags & (1ul << 32)) != 0))
                {
                    placed = true;
                    m = TrailEmitMatrix(t);
                }
                if (Tracing && type == 7) Trace($"step trail: surface {(surfaced ? surface.Id : -1)}{(wet ? " (water)" : "")} particle 0x{system:X} sound 0x{id:X} vol {volume:0.##} at {m.Translation}"); // Amedo 2026-09-30
                if (id != 0xFFFF) TrailSound(id, volume, t.Flags, t.Words, m.Translation);
                if ((t.Flags & (1ul << 32)) != 0) ShakeCamera(t.Words[13], t.Words[14], t.Words[15], t.Words[17], RootPoint(m.Translation)); // Amedo 2026-09-28
                if (t.Spawn != 0xFFFF) Deliver(t.Spawn, this); // Amedo
            }
            if (fx is null || system == 0xFFFF || type == 7) continue; // Amedo 2026-09-30
            bool follow = (t.Flags & TrailFollow) != 0;
            if (emit)
            {
                if (t.Handle < 0) t.Handle = fx.Spawn(system, 0, !follow, RootPoint(GamePosition)); // Amedo 2026-09-30
            }
            else if (t.Handle >= 0)
            {
                fx.Stop(t.Handle);
                t.Handle = -1;
            }
            if (t.Handle < 0) continue;
            Matrix4x4 rm; // Amedo 2026-09-30
            int source = (int)((t.Flags >> 10) & 0xF);
            Vector3? offset = (t.Flags & TrailOffset) != 0 ? t.Offset : null;
            if (source == 0)
                rm = TrailEmitMatrix(t); // Amedo 2026-09-28
            else if (source == 1) // Amedo 2026-11-09
            {
                rm = Matrix4x4.CreateFromQuaternion(RotationOf(_initMatrix));
                rm.Translation = _initMatrix.Translation;
                if (offset is { } o) rm.Translation += Vector3.TransformNormal(o, rm);
            }
            else if (source == 2)
            {
                rm = LocalMatrix;
                if (offset is { } o) rm.Translation += Vector3.TransformNormal(o, rm);
            }
            else
            {
                World.Unimplemented("trail", source, "trail matrix source (actor frame A+0x40)");
                rm = LocalMatrix;
            }
            if (follow) fx.SetRenderMatrix(t.Handle, RootMatrix(rm)); // Amedo 2026-09-28
            else fx.SetEmitMatrix(t.Handle, RootMatrix(rm));
        }
    }
}
