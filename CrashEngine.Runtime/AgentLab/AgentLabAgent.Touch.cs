using System.Numerics;
using CrashEngine.Runtime.Character;
using CrashEngine.Runtime.Physics;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private static readonly int[] ControllerTypes = [12, 14, 13, 15, 16, 17, 18, 19, 20];

    public int ControllerType { get; private set; } = -1;
    private uint _controllerNode; // Amedo
    public uint ComponentMask => _controllerNode // Amedo
        | (HasMotionComponent ? 1u : 0u)
        | (ControllerType >= 0 ? 2u : 0u)
        | (Component5 is not null ? 0x20u : 0u)
        | (_hasLinkList ? 0x40u : 0u)
        | (HasInputNode_17B130 ? 0x800u : 0u);

    // Amedo
    private bool HasInputNode_17B130 => ControllerType == 12 && (Instance.Source.ParamList3 is { Count: > 0 } p3 ? p3[0] : 6u) != 6u;
    public uint Motion10 { get; set; }
    public int LastHitType { get; private set; }
    public double LastHitTime { get; private set; } = double.NegativeInfinity;

    private PS2AnyOGI? _hullOgi;
    private List<ConvexHull>? _localHulls;
    private CharacterBody? _collisionBody;
    private ConvexHull? _ownHull_4; // Amedo

    private void InitController()
    {
        int type = GameObject is { } go ? (int)go.Type : -1;
        if (type < 0 || type >= ControllerTypes.Length) return;
        ControllerType = ControllerTypes[type];
        _controllerNode = 1u << ControllerType; // Amedo
        if (ControllerType == 19) // Amedo
        {
            Motion10 = 3 | 0xFE000;
            LastHitType = 0;
            LastHitTime = double.NegativeInfinity;
            GrapleInit_140EB0();
            if (!Rendered) ApplyRendered();
            return;
        }
        uint sf = Instance.Source.StateFlags;
        AgentFlags = (sf & 0x10) != 0 ? AgentFlags | 0x4000 : AgentFlags & ~0x4000u; // Amedo 2026-09-29
        AgentFlags = (sf & 2) != 0 ? AgentFlags | 0x10 : AgentFlags & ~0x10u;
        AgentFlags = (sf & 4) != 0 ? AgentFlags | 0x400 : AgentFlags & ~0x400u;
        AgentFlags = (sf & 8) != 0 ? AgentFlags | 0x1000 : AgentFlags & ~0x1000u;
        Motion10 = 3 | 0xFE000;
        LastHitType = 0;
        LastHitTime = double.NegativeInfinity;
        AgentFlags = (sf & 0x100) != 0 ? AgentFlags | 0x8 : AgentFlags & ~0x8u;
        Motion10 = (Motion10 & ~0x100u) | ((sf & 0x200) != 0 ? 0x100u : 0u);
        Motion10 = (Motion10 & ~0x200u) | ((sf & 0x8000) != 0 ? 0x200u : 0u);
        Motion10 = (Motion10 & ~0x400u) | ((sf & 0x10000) != 0 ? 0x400u : 0u);
        Motion10 = (Motion10 & ~0x1000u) | ((sf & 0x20000) != 0 ? 0x1000u : 0u);
        if (ControllerType == 13) AgentFlags |= 0x8000; // Amedo 2026-09-28
        if (ControllerType == 15) CreatureApplyState_13D688(); // Amedo
        if (!Rendered) ApplyRendered();
    }

    public CharacterBody? Body()
    {
        var ogi = Ogi;
        if (_ownHull_4 is null && (ogi is null || OgiHulls.Count(ogi) == 0)) return null; // Amedo
        if (_ownHull_4 is null && !ReferenceEquals(ogi, _hullOgi)) // Amedo
        {
            _hullOgi    = ogi;
            _localHulls = OgiHulls.Of(ogi!).ToList(); // Amedo
        }
        _collisionBody ??= new CharacterBody(this);
        _collisionBody.StateFlags = Instance.Source.StateFlags;
        _collisionBody.Collidable = (AgentFlags & 0x10) != 0; // Amedo 2026-09-28
        _collisionBody.Sphere = _collisionBody.Collidable && Component5 is { IsBox: false } sphere // Amedo 2026-09-30
            ? new Vector4(sphere.Position, sphere.Radius) : null;
        _collisionBody.Parts.Clear();
        var m = GameMatrix;
        if (_ownHull_4 is { } own) // Amedo
        {
            _collisionBody.Parts.Add((own, m));
            return _collisionBody;
        }
        for (int i = 0; i < _localHulls!.Count; i++)
        {
            var world = m;
            int joint = OgiHulls.Joint(ogi!, i); // Amedo
            if (joint != 0xFF && JointsOf(ogi).TryGetValue(joint, out var jw)) world = jw * m; // Amedo 2026-09-30
            _collisionBody.Parts.Add((_localHulls[i], world));
        }
        return _collisionBody;
    }

    // Amedo
    private void SetCollisionBoxSize_221108(float sx, float sy, float sz)
    {
        var ext = new Vector3(sx * 0.5f, sy * 0.5f, sz * 0.5f);
        _ownHull_4 = ConvexHull.Box(-ext, ext);
        SetBoxOverride_1F0168((-ext, ext));
    }

    // Amedo 2026-09-29
    internal object? CollisionModel => Ogi;

    // Amedo 2026-09-29
    internal Matrix4x4 PartMatrix_1F0538(int part)
    {
        var m = GameMatrix;
        if (_ownHull_4 is not null) return m; // Amedo
        if (Ogi is { } ogi && part >= 0 && part < OgiHulls.Count(ogi)) // Amedo
        {
            int joint = OgiHulls.Joint(ogi, part);
            if (joint != 0xFF && JointsOf(ogi).TryGetValue(joint, out var jw)) return jw * m; // Amedo 2026-09-30
        }
        return m;
    }

    public void HandleMessage(int type, AgentLabAgent? sender)
    {
        if (ControllerType < 0) return;
        if (World.AkuTouchBreak) type = GoldenTouch_116F38(type, sender); // Amedo 2026-10-04
        bool shielded = OwnCharacter is { } shieldOwner ? (shieldOwner.MotionWord & 0x400) != 0 : (Motion10 & 0x400) != 0; // Amedo 2026-10-06
        if ((AgentFlags & 0x8) != 0 && !shielded)
        {
            int slot = type switch
            {
                4 => 5,
                5 => 4,
                6 or 10 => 6,
                7 or 9 or 11 => 7,
                8 or 12 => 8,
                13 or 14 => 10,
                _ => 3,
            };
            bool affected = Affected(type);
            Trace($"message type {type} -> slot {slot}{(affected ? "" : " (not affected)")}");
            if (affected)
            {
                LastHitType = type;
                LastHitTime = World.Now; // Amedo 2026-10-01
                TriggerEventSlot(slot, sender);
            }
        }
        if ((Motion10 & 0x100) == 0) return;
        if (!shielded) // Amedo 2026-10-06
        {
            LastHitType = type;
            LastHitTime = World.Now; // Amedo 2026-10-01
            if (type == 3 && sender?.OwnCharacter is { MotionState: 3 }) // Amedo 2026-09-29
                HarmSender(sender);
        }
        else HarmSender(sender);
    }

    // Amedo 2026-09-28
    private void HarmSender(AgentLabAgent? sender)
    {
        if (sender is null) return; // Amedo
        Trace(ReferenceEquals(sender, World.Player) ? "harms the player (0x13E258)" : $"harms {sender.Entity.Name} (0x13E258)");
        sender.ReceiveDamage(0x400, 1, 0f, this, true); // Amedo
    }

    private bool Affected(int type) => type switch
    {
        3 => ((Motion10 >> 13) & 1) != 0,
        4 => ((Motion10 >> 19) & 1) != 0,
        5 => ((Motion10 >> 15) & 1) != 0,
        6 => ((Motion10 >> 16) & 1) != 0,
        7 or 9 => ((Motion10 >> 18) & 1) != 0,
        8 => ((Motion10 >> 17) & 1) != 0,
        _ => true,
    };

    // Amedo 2026-10-04
    private int GoldenTouch_116F38(int type, AgentLabAgent? sender)
    {
        if (sender is null || !ReferenceEquals(sender, World.Player) || World.PlayerCharacter is not { } c) return type;
        if (!c.Invincible || c.HitPoints < 3 || Instance.Source.ObjectId is 0x0001 or 0x0166) return type;
        return type < 4 ? 6 : type;
    }

    private bool HitWithin(float window) => !double.IsNegativeInfinity(LastHitTime) && (World.NowTicks - TicksOf(LastHitTime)) * TickTime <= window; // Amedo

    // Amedo 2026-09-28
    private void SetHitPoints(int value)
    {
        if (OwnCharacter is not { } c) // Amedo 2026-09-29
        {
            if (ControllerType == 15) CreatureHitPoints = value & 0xFF; // Amedo 2026-09-28
            else World.Unimplemented("command", 529, "SetHitPoints on a non-creature");
            return;
        }
        c.HitPoints = value;
        if (0 < value) c.State70 &= ~CharacterController.DeadFlag;
    }
}
