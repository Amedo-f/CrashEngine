using System.Numerics;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private int _channel158 = -1;
    private byte _contactFirst168 = 0xFF, _contactLast169 = 0xFF;
    private float _contactThreshold16C = -1f;
    private bool _noSurfaceSound41;
    private int _contactTicks170;

    public (int First, int Last, float Threshold, bool NoSurfaceSound) ContactSounds => (_contactFirst168, _contactLast169, _contactThreshold16C, _noSurfaceSound41);

    // Amedo
    private void SetContactSounds_224BA8(uint arg0, uint arg1)
    {
        SetContactSounds_23DEC0((byte)(arg0 & 0xFF), (byte)((arg0 >> 8) & 0xFF), BitConverter.UInt32BitsToSingle(arg1));
        if ((arg0 & 0x10000) != 0) _noSurfaceSound41 = true;
    }

    // Amedo
    private void SetContactSounds_23DEC0(byte first, byte last, float threshold)
    {
        _contactThreshold16C = threshold;
        _contactFirst168 = first;
        _contactLast169 = last;
    }

    // Amedo
    internal void StopSound_23D4D8()
    {
        if (_channel158 < 0) return;
        World.Sounds?.Stop_1E50C8(_channel158);
        _channel158 = -1;
    }

    // Amedo
    private void BindBodyContacts_2919E8(RigidBody body)
    {
        body.OnLanded = OnRigidBodyLanded_23D2E0;
        body.OnLandedHard = OnRigidBodyLandedHard_23D3D8;
        body.OnScraped = OnRigidBodyScraped_23D458;
    }

    // Amedo
    private bool OnRigidBodyLanded_23D2E0(CollisionWorld.Surface surface, Vector3 point, Vector3 change)
    {
        bool played = false;
        float hard = change.X * change.X + change.Y * change.Y + change.Z * change.Z;
        if (Tracing) Trace($"rigid body landed on surface {surface.Id} (|dv|² {hard:0.###}, sound 0x{surface.SoundAt(0, out _):X})");
        if (surface.SoundAt(0, out _) != 0xFFFF && 10f < hard)
        {
            PlaySurfaceContact_22FED8(surface, 0, point, change, hard - 10f);
            played = true;
        }
        if (10f < hard) MakeNoise_209BC8(12f, MathF.Min(hard * 3f, 1f), point);
        return played;
    }

    // Amedo
    private bool OnRigidBodyLandedHard_23D3D8(CollisionWorld.Surface surface, Vector3 point, Vector3 velocity)
    {
        float speed = velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z;
        if (surface.SoundAt(0, out _) == 0xFFFF || !(15f < speed)) return false;
        if (Tracing) Trace($"rigid body hit surface {surface.Id} hard (|v|² {speed:0.###})");
        PlaySurfaceContactHard_2303F0(surface, 4, point, velocity, speed - 15f);
        return true;
    }

    // Amedo
    private bool OnRigidBodyScraped_23D458(CollisionWorld.Surface surface, Vector3 point, Vector3 velocity)
    {
        float speed = velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z;
        if (surface.SoundAt(0, out _) == 0xFFFF || !(0.2f < speed)) return false;
        if (Tracing) Trace($"rigid body rolls on surface {surface.Id} (|0.3v|² {speed:0.###})");
        PlaySurfaceContactHard_2303F0(surface, 5, point, velocity, speed - 0.2f);
        return true;
    }

    // Amedo
    private void PlaySurfaceContact_22FED8(CollisionWorld.Surface surface, int kind, Vector3 point, Vector3 velocity, float excess)
    {
        if ((World.NowTicks - _contactTicks170) * (1f / 9000f) < 0.5f) return;
        _contactTicks170 = World.NowTicks;
        ushort id = surface.SoundAt(kind, out float scale);
        ushort pid = surface.ParticleAt(kind);
        bool near = Metric < 0x1FA4;
        if (Tracing) Trace($"surface contact {kind}: sound 0x{id:X}, particle 0x{pid:X}, metric {Metric}{(_noSurfaceSound41 ? ", muted (bit 41)" : "")}");
        if (id != 0xFFFF && !_noSurfaceSound41 && near)
        {
            float root = MathF.Sqrt(excess);
            float volume = root * 0.06f + 0.2f;
            if (0f < scale) volume *= scale;
            if (2.5f < volume) volume = 2.5f;
            float pitch = root * 0.3f + 0.4f;
            if (2f < pitch) pitch = 2f;
            else if (pitch < 0.2f) pitch = 0.2f;
            World.Sounds?.PlayAt(id, RootPoint(point), volume, pitch, $"{Entity.Name} surface contact (0x22FED8)", SoundAlive);
            float shake = MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z) * 0.001f;
            if (0.3f < shake) shake = 0.3f;
            float ax = shake * RandomSpread(0.5f);
            float az = shake * RandomSpread(0.5f);
            World.CameraImpulseAxes?.Invoke(ax, shake, az, 0.02f, RootPoint(point));
        }
        if (near && 50f < excess && ((surface.Mask >> 20) & 1) != 0)
        {
            PlayRideSound_13FB68(excess);
            if (_contactFirst168 != 0xFF) PlayContactSound_230298(excess);
        }
        if (pid != 0xFFFF) ContactParticle(pid, point, velocity);
    }

    // Amedo
    private void PlaySurfaceContactHard_2303F0(CollisionWorld.Surface surface, int kind, Vector3 point, Vector3 velocity, float excess)
    {
        ushort id = surface.SoundAt(kind, out float scale);
        ushort pid = surface.ParticleAt(kind);
        if (Tracing) Trace($"surface contact {kind}: sound 0x{id:X}, particle 0x{pid:X}, metric {Metric}{(_noSurfaceSound41 ? ", muted (bit 41)" : "")}");
        if (id != 0xFFFF && !_noSurfaceSound41 && Metric < 0x1FA4)
        {
            float root = MathF.Sqrt(excess);
            float pitch, volume;
            if (kind == 5)
            {
                pitch = root * 0.055f + 1f - _logicalRadius * 0.1f;
                volume = root * 0.05f + 0.75f;
                if (2.8f < pitch) pitch = 2.8f;
            }
            else
            {
                pitch = root * 0.06f + 0.2f;
                volume = root * 0.4f + 0.3f;
                if (2f < pitch) pitch = 2f;
            }
            if (0f < scale) volume *= scale;
            if (1.5f < volume) volume = 1.5f;
            if (World.Sounds is { } sounds)
            {
                if (_channel158 < 0) _channel158 = sounds.PlayTracked(id, SoundPosition, volume, pitch, $"{Entity.Name} surface contact {kind} (0x2303F0)", 0, persistent: true);
                else
                {
                    sounds.SetTracked_1E51C0(_channel158, null, pitch);
                    sounds.SetTracked_1E51C0(_channel158, volume, null);
                }
            }
        }
        if (pid != 0xFFFF) ContactParticle(pid, point, velocity);
    }

    // Amedo
    private void ContactParticle(ushort pid, Vector3 point, Vector3 velocity)
    {
        if (World.Particles is not { } fx || fx.Spawn(pid, 1, true, Vector3.Zero) is not (var h and >= 0)) return;
        var m = Along_185A40(velocity * InverseLength(velocity));
        m.Translation = point;
        fx.SetEmitMatrix(h, RootMatrix(m));
    }

    // Amedo
    private void PlayContactSound_230298(float excess)
    {
        ushort id = 0xFFFF;
        if (_contactThreshold16C < 0f) id = SoundForSlot((ushort)World.Rng.GetRandRange_18C060(_contactFirst168, _contactLast169 - _contactFirst168 + 1)); // Amedo
        if (id == 0xFFFF || World.Sounds is not { } sounds) return;
        float volume = MathF.Sqrt(excess) * 0.06f + 0.2f;
        float pitch = RandomSpread(0.07f) + 1f;
        sounds.PlayAt(id, RootPoint(GamePosition), volume, pitch, $"{Entity.Name} contact sound (0x230298)", SoundAlive);
    }

    // Amedo
    private void PlayRideSound_13FB68(float excess)
    {
        if (ControllerType != 12 || OwnCharacter?.Vehicle?.Other?.Self is not AgentLabAgent node) return;
        if (node._contactFirst168 != 0) node.PlayContactSound_230298(excess);
    }
}
