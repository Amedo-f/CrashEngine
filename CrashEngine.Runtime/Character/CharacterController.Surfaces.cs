using System.Numerics;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-28
public sealed partial class CharacterController
{
    private const int WaterSurfaceId = 12;

    // Amedo 2026-10-04
    public float GoldenSeconds = 8f;
    public Action? InvincibilityEnded;
    public Func<CharacterController, bool>? GoldenCheck_2C8260; // Amedo

    public bool CutsceneMode { get; set; } // Amedo
    public int WetSurface = -1;
    public int GroundSurfaceId = -1; // Amedo 2026-09-30
    public Vector3 WaterPoint;
    public Action<ushort, Vector3>? SpawnParticle;
    public Action<Vector3, Vector3, Vector3, int, int, int, int>? AddDecal; // Amedo 2026-09-30
    public Action<ushort, Vector3, float>? PlaySound;
    private int _lastSplash = int.MinValue / 2;

    private void SurfaceHazards()
    {
        if (CurrentSurface() is { } ground && ground.Deadly) SurfaceHit(ground);
        foreach (var r in Collision.Solids)
        {
            CollisionWorld.Surface s;
            if ((r.Flags & CharacterCollision.Static) != 0)
            {
                if ((r.Flags & 0x70) == 0) continue;
                s = _world.SurfaceAt(r.Triangle.Surface);
            }
            else
            {
                if ((r.Flags & 0x74) == 0) continue;
                s = _world.SurfaceAt(r.Owner is { } owner ? HullSurfaceOf?.Invoke(owner, r.Part) ?? 0 : 0); // Amedo
            }
            if (s.Deadly) SurfaceHit(s);
        }
    }

    private void NonSolidContacts()
    {
        var p = Position + new Vector3(0f, CenterOffset, 0f);
        bool wet = false;
        foreach (var r in Collision.Others)
        {
            if (!CharacterCollision.Contains(r, p)) continue;
            if ((r.Flags & CharacterCollision.Static) == 0)
            {
                if (Touch is not null && r.Owner is { } owner) SendTouch(owner, 0f); // Amedo
                continue;
            }
            var s = _world.SurfaceAt(r.Triangle.Surface);
            wet = true;
            TouchWater(r.Triangle, s, p);
            if (s.Deadly) SurfaceHit(s);
        }
        if (!wet) WetSurface = -1;
    }

    private void TouchWater(in CollisionWorld.Triangle tri, CollisionWorld.Surface s, Vector3 p)
    {
        if (s.Id != WaterSurfaceId) return;
        CollisionWorld.Plane(tri.A, tri.B, tri.C, out var n, out float w);
        WaterPoint = p - n * (Vector3.Dot(n, p) + w);
        if (WetSurface != WaterSurfaceId) WaterEnter(s);
        else WaterStay(s);
        WetSurface = WaterSurfaceId;
    }

    private void WaterEnter(CollisionWorld.Surface s)
    {
        var v = Velocity;
        var at = new Vector3(WaterPoint.X + v.X * 0.12f, WaterPoint.Y, WaterPoint.Z + v.Z * 0.12f);
        bool splash = 0.5f < (Ticks - _lastSplash) * (1f / 9000f);
        Log?.Invoke($"[player] water enter at ({at.X:0.##}, {at.Y:0.##}, {at.Z:0.##}) vel.y {v.Y:0.##}{(splash ? " splash" : "")}");
        if (!splash) return;
        SurfaceParticles(s, at);
        SurfaceSound(s, at);
        _lastSplash = Ticks;
    }

    private void WaterStay(CollisionWorld.Surface s)
    {
        var v = Velocity;
        float intensity = MathF.Min(0.5f, (v.X * v.X + v.Y * v.Y + v.Z * v.Z) * 0.001f + 0.01f);
        var at = new Vector3(WaterPoint.X + v.X * 0.14f, WaterPoint.Y, WaterPoint.Z + v.Z * 0.14f);
        Ripple(at, intensity);
    }

    private void SurfaceParticles(CollisionWorld.Surface s, Vector3 at)
    {
        ushort pid = s.ParticleAt(0);
        var dir = new Vector3(0.9f, 0f, 0f);
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3f);
        for (int i = 0; i < 6; i++)
        {
            RippleAt_1AE158(at); // Amedo 2026-09-30
            if (pid != 0xFFFF) SpawnParticle?.Invoke(pid, new Vector3(at.X + dir.X, at.Y + dir.Y - 0.2f, at.Z + dir.Z));
            dir = Vector3.Transform(dir, turn);
        }
    }

    private void SurfaceSound(CollisionWorld.Surface s, Vector3 at)
    {
        ushort id = s.SoundAt(0, out float volume);
        if (id != 0xFFFF) PlaySound?.Invoke(id, at, volume);
    }

    // Amedo 2026-09-30
    private void Ripple(Vector3 at, float chance)
    {
        if (Rng.GetRandFloat_18C0B8() <= chance) RippleAt_1AE158(at); // Amedo
    }

    // Amedo 2026-09-30
    private void RippleAt_1AE158(Vector3 at)
    {
        float x = Rng.RandomSigned_18C0F0() * (0.4f * 1f); // Amedo
        float y = Rng.RandomSigned_18C0F0() * (0.4f * 0f);
        float z = Rng.RandomSigned_18C0F0() * (0.4f * 1f);
        AddDecal?.Invoke(at + new Vector3(x, y + 0.09f, z), Vector3.UnitY, Vector3.UnitZ, 0, 0, 0, 0);
    }

    private void SurfaceHit(CollisionWorld.Surface s)
    {
        if (HitPoints != 0) Log?.Invoke($"[player] deadly surface {s.Id} (kind 0x{s.HitKind:X}, damage {s.HitAmount})");
        Damage(s.HitSource, s.HitKind, s.HitAmount, this);
    }
}
