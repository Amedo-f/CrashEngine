using System.Numerics;
using CrashEngine.Runtime.Character.Vehicles;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
internal sealed class SkateController_121FD0 : ISkateSounds
{
    public SkateController_121FD0(AgentLabAgent owner)
    {
        Owner = owner;
        Array.Fill(TrailIds, (ushort)0xFFFF);
        Array.Fill(SoundIds, (ushort)0xFFFF);
    }

    public AgentLabAgent Owner { get; }
    public readonly ushort[] TrailIds = new ushort[8];
    public readonly ushort[] SoundIds = new ushort[16];
    public int TrailCount, SoundCount;
    public bool InCutscene, Grinding, WasGrinding;
    public int SurfaceIndex = -1;
    public CollisionWorld.Surface Surface;

    public void Surface_1199D8(int index, CollisionWorld.Surface surface, Vector3 velocity) => Owner.SkateSurface_1199D8(this, index, surface, velocity);
    public void Land_119C70(float impact) => Owner.SkateLand_119C70(this, impact);
    public void Lean_122148(float lean) => Owner.SkateLean_122148(this, lean);
}

// Amedo
public sealed partial class AgentLabAgent
{
    private SkateController_121FD0? _skate;
    private ushort _soundProxy7C = 0xFFFF;
    private float _trailLevel64 = 1f;

    internal ISkateSounds? SkateSounds => _skate;

    internal void SetTrailLevel_64(float level)
    {
        if (_trails is not null) _trailLevel64 = level;
    }

    private void SkateStart_1220F8(SkateController_121FD0 k)
    {
        k.Grinding = false;
        k.WasGrinding = false;
    }

    private void SkateFrame_1197D8(SkateController_121FD0 k)
    {
        k.InCutscene = World.GameMode is AgentLabWorld.ModeCutscene or AgentLabWorld.ModeCutsceneAlt;
        k.WasGrinding = k.Grinding;
        if (!k.InCutscene && World.PlayerCharacter?.Vehicle is Humiliskate board)
            k.Grinding = k.SurfaceIndex >= 0 && board.Grinding;
        if (k.SurfaceIndex >= 0 || _channel158 < 0) return;
        StopSound_23D4D8();
        k.Grinding = false;
        k.WasGrinding = false;
    }

    internal void SkateSurface_1199D8(SkateController_121FD0 k, int index, CollisionWorld.Surface surface, Vector3 v)
    {
        int old = k.SurfaceIndex;
        k.SurfaceIndex = index;
        k.Surface = surface;
        if (index != old || k.Grinding && !k.WasGrinding) StopSound_23D4D8();
        int slot = k.Grinding ? 3 : 0xFFFF;
        if (k.SurfaceIndex < 0 && !k.Grinding) return;
        if (!k.Grinding) slot = k.Surface.Id switch { 8 => 2, 12 or 17 => 1, _ => 0 };
        float speed = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        float volume = speed * 0.01f + 0.1f;
        float pitch = speed * 0.06f + 0.4f;
        if (k.Grinding)
        {
            pitch -= 0.4f;
            volume += 0.75f;
        }
        pitch = Clamp_18C2A0(pitch, 0.5f, 3f);
        volume = Clamp_18C2A0(volume, 0.05f, 1.5f);
        ushort id = k.SoundIds[slot];
        if (id == 0xFFFF || World.Sounds is not { } sounds) return;
        if (_channel158 < 0)
        {
            _channel158 = sounds.PlayTracked(id, SoundPosition, volume, pitch, $"{Entity.Name} skate slot {slot} (0x1199D8)", 0, persistent: true);
            return;
        }
        sounds.SetTracked_1E51C0(_channel158, null, pitch);
        sounds.SetTracked_1E51C0(_channel158, volume, null);
    }

    internal void SkateLand_119C70(SkateController_121FD0 k, float impact)
    {
        int slot = k.SurfaceIndex < 0 ? 0xFFFF : k.Surface.Id switch { 7 or 9 => 8, 8 => 9, _ => 7 };
        float pitch = Clamp_18C2A0(impact * 0.05f + 1f, 0.5f, 2f);
        float volume = Clamp_18C2A0(impact * 0.045f + 0.2f, 0.2f, 1.3f);
        SkateOneShot(k, slot, volume, pitch, "0x119C70");
    }

    internal void SkateLean_122148(SkateController_121FD0 k, float lean)
    {
        int slot = k.SurfaceIndex < 0 ? 0xFFFF : k.Surface.Id switch { 7 or 9 => 5, 8 => 6, _ => 4 };
        float a = MathF.Abs(lean);
        float pitch = Clamp_18C2A0(a * 0.75f + 0.8f, 0.5f, 2f);
        float volume = Clamp_18C2A0(a * 0.25f + 0.3f, 0.2f, 2f);
        SkateOneShot(k, slot, volume, pitch, "0x122148");
    }

    private void SkateOneShot(SkateController_121FD0 k, int slot, float volume, float pitch, string fn)
    {
        if (slot == 0xFFFF) return;
        ushort id = k.SoundIds[slot];
        if (id == 0xFFFF || World.Sounds is not { } sounds) return;
        sounds.PlayAt(id, RootPoint(GamePosition), volume, pitch, $"{Entity.Name} skate slot {slot} ({fn})", SoundAlive);
    }

    private void SetSkateIds_119F50(List<uint> a)
    {
        if (_skate is not { } k) return;
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        ushort Half(int i) => (ushort)((i & 1) == 0 ? Arg(i >> 1) & 0xFFFF : Arg(i >> 1) >> 16);
        uint flags = Arg(22);
        if ((flags & 0x200) != 0 || k.TrailCount == 0)
        {
            int n = (int)(flags & 0xF);
            for (int i = 0; i < n; i++)
            {
                int idx = k.TrailCount;
                k.TrailCount = (idx + 1) & 0xF;
                if (idx < k.TrailIds.Length) k.TrailIds[idx] = Half(i);
            }
        }
        if ((flags & 0x200) == 0 && k.SoundCount != 0) return;
        if (SoundProxy_23CC18() is not { } proxy) return;
        int m = (int)((flags >> 4) & 0x1F);
        for (int i = 0; i < m; i++)
        {
            ushort id = Half(12 + i);
            if (id == 0xFFFF) continue;
            ushort r = id < proxy.SoundSlots.Count ? proxy.SoundSlots[id] : (ushort)0xFFFF;
            if (r == 0xFFFF) continue;
            int idx = k.SoundCount;
            k.SoundCount = (idx + 1) & 0xF;
            k.SoundIds[idx] = (ushort)(r & 0x7FFF);
        }
    }

    private Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject? SoundProxy_23CC18() =>
        _soundProxy7C == 0xFFFF ? null : World.GameObject(_soundProxy7C);

    private void CharacterSoundProxy_10E048(uint arg0)
    {
        int slot = (arg0 & 0x100) != 0 ? (int)((World.Progression >> 8) & 0xF) : (int)(arg0 & 0xFF);
        if (slot == 6 || World.PlayerSlot_570(slot) is not { } target) return;
        if (RootController().GameObject is not { } mine) return;
        target._soundProxy7C = (ushort)mine.GetID();
    }

    private void SetVehicleHumiliskate_124B68(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        int p0 = (int)(Arg(0) & 0xFF), p1 = (int)((Arg(0) >> 8) & 0xFF);
        var driver = p0 == 6 ? null : World.PlayerSlot_570(p0);
        var rider = p1 == 6 ? null : World.PlayerSlot_570(p1);
        float maxSpeed = FloatValueOf(Arg(1));
        float crouchSpeed = FloatValueOf(Arg(2));
        if (driver?.OwnCharacter is not { } c1 || rider?.OwnCharacter is not { } c2) return;
        World.Log?.Invoke($"[vehicle {World.Now:0.00}s] 555: {driver.Entity.Name} + {rider.Entity.Name} (max {maxSpeed}, crouch {crouchSpeed})");
        c1.EnterVehicle_13B938(Vehicle.Humiliskate, c2, 0);
        uint p = (World.Progression & 0xFFFFFF0Fu) | 0x20u;
        p = (p & 0xFFFFF0FFu) | ((uint)(c1.Bones.Kind & 0xF) << 8);
        World.Progression = (p & 0xFFFF0FFFu) | ((uint)(c2.Bones.Kind & 0xF) << 12);
        if (c1.Vehicle is not Humiliskate board) return;
        board.CrouchSpeed = crouchSpeed;
        board.MaxSpeed = maxSpeed;
    }

    private float SkateCrouch_12D0B0() =>
        World.Player?.OwnCharacter?.Vehicle is Humiliskate board && board.Crouch ? 1f : 0f;

    // Amedo
    private void PushPlayerSkate_11B830(float speed)
    {
        int slot = (int)(World.Progression >> 8) & 0xF;
        if (slot == 6 || World.PlayerSlot_570(slot)?.OwnCharacter?.Vehicle is not Humiliskate { Kind: Vehicle.Humiliskate } board) return;
        board.Push_1615A8(new Vector3(0f, 0f, speed), GameMatrix);
    }
}
