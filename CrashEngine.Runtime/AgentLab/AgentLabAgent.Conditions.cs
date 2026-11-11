using System.Numerics;
using CrashEngine.Runtime.Physics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-26
public sealed partial class AgentLabAgent
{
    private float Evaluate(TwinBehaviourCondition c, Level ctx)
    {
        switch (c.ConditionIndex)
        {
            case 0: return 1f;
            case 1: return (AgentFlags & 0x10) != 0 ? 1f : 0f;
            case 2: ctx.Else = _body; return 0f;
            case 3: return World.Rng.GetRandFloat_18C0B8(); // Amedo
            case 5: return (World.NowTicks - TicksOf(ctx.EnterTime)) * TickTime; // Amedo
            case 7: return Anim?.Current is null || Anim.RemainingSeconds <= 0f ? 1f : 0f;
            case 8: return _endReached ? 1f : 0f;
            case 13: return _route is not null ? 1f : 0f;
            case 14: return _keys.Count > 0 ? 1f : 0f;
            case 20: return InsideNode(RouteNode(_routeIndex)) ? 1f : 0f;
            case 21: return InsideNode(EdgeEndNode()) ? 1f : 0f;
            case 9: return MeToInitPosSqrDist_2518B8(); // Amedo
            case 10: return MeToFocusSqrDist();
            case 29: return HeightAboveFocus_226B30(); // Amedo
            case 45: return FocusObject is not null || FocusIsCamera || _hasFocusPosition ? 1f : 0f; // Amedo
            case 48: return (AgentFlags & 0x40) != 0 ? 1f : 0f; // Amedo
            case 55: return AttachedAtExit(c.Parameter);
            case 65: return _busy ? 1f : 0f;
            case 118: return LinkCount;
            case 73: return _hasLinkList ? 1f : 0f; // Amedo 2026-10-05
            case 47: return Var(0) == c.Parameter ? 1f : 0f;
            case 50: return FreshMessage(c.CheckInterval) ? 1f : 0f; // Amedo 2026-09-28
            case 51: return Message == c.Parameter && FreshMessage(c.CheckInterval) ? 1f : 0f; // Amedo 2026-09-28
            case 56: return _hasFocusObject ? 1f : 0f;
            case 57: return _hasFocusPosition ? 1f : 0f;
            case 58: return Anim?.Current is null ? 0f : Anim.RemainingSeconds;
            case 67: return ((Instance.Source.StateFlags >> c.Parameter) & 1) != 0 ? 1f : 0f;
            case 91: return Counters.GetValueOrDefault(c.Parameter);
            case 109: return AtLastLink_229C98 ? 1f : 0f; // Amedo 2026-10-01
            case 92: return Counters.GetValueOrDefault(c.Parameter) == c.ReturnCheck ? c.ReturnCheck + c.ReturnCheck : 0f;
            case 152: return ReferenceEquals(Chunk, World.Player?.Chunk) ? 1f : 0f; // Amedo 2026-09-29
            case 160: return NavDistance_225450(); // Amedo 2026-10-05
            case 111: return SensorValue_22B140(); // Amedo 2026-10-05
            case 86: return Impact_229220; // Amedo 2026-10-05
            case 159: return World.Music is not { Available: true } speech || speech.IsReady(1) ? 1f : 0f;
            case 628: return PartnerDead_128C00() ? 1f : 0f;
            case 621: return CharacterHasHomeChunk_12C898(); // Amedo
            case 547: return EdgeNeedsJump() ? 1f : 0f;
            case 602: return EdgeHasFlag(0x100) ? 1f : 0f; // Amedo 2026-10-05
            case 619: return PlayerNearerAnotherKey();
            case 517:
                return World.Player is { } p && p != this ? GameMath.DistanceSq(WorldPosition, p.WorldPosition) : 0f;
            case 22: return MeFacingFocus(); // Amedo 2026-09-27
            case 24: return ClearLineOfSightToFocus_225E68(); // Amedo 2026-10-06
            case 40: return HeadLookingAtFocus_227758(c.Parameter); // Amedo 2026-10-06
            case 151: return FocusObject is { Rendered: true } ? 1f : 0f; // Amedo 2026-10-06
            case 127: return FocusObject is { } kindOf && kindOf.Instance.ObjectId != 0xFFFF && kindOf.RootController().ParamInt_2619D8(0) == c.Parameter ? 1f : 0f; // Amedo 2026-10-06
            case 545: return World.PlayerCharacter?.Gun is { } gunCharge ? gunCharge.Charge : -1f; // Amedo
            case 579: return World.PlayerCharacter?.Gun is { State: 6 } ? 1f : 0f; // Amedo
            case 581: return ShotHitMe_126708(); // Amedo
            case 633: return World.PlayerCharacter?.Gun is { } gunSecond ? gunSecond.SecondCount : 0f; // Amedo
            case 634: return World.PlayerCharacter?.Gun is { } gunAmmo ? gunAmmo.Ammo : 0f; // Amedo
            case 580: return World.PlayerCharacter is { Jump: { } jump } && jump.Phase is >= 12 and <= 14 ? 1f : 0f; // Amedo 2026-10-06
            case 46: return FocusObject is { } fa && (fa.Instance.ObjectId & 0x7FFF) == c.Parameter && fa.Instance.ObjectId != 0xFFFF ? 1f : 0f;
            case 52: return ((_keyCurrent + 1) & 0xFF) == c.Parameter ? 1f : 0f;
            case 132: return AtLastKey_22A658 ? 1f : 0f; // Amedo 2026-10-01
            case 137: return _keys.Count; // Amedo 2026-10-30
            case 66: return FocusObject is { _busy: true } ? 1f : 0f;
            case 68:
            case 69: return MeToKeySqrDist(c.Parameter);
            case 88:
            case 123: return Alive(ref _slot118) is { } s118 ? GameMath.DistanceSq(GamePosition, s118.GamePosition) : 1e30f;
            case 77: return Alive(ref _slot118) is not null ? 1f : 0f;
            case 79: return Alive(ref _slot118) is { } s79 && s79.Instance.ObjectId != 0xFFFF && (s79.Instance.ObjectId & 0x7FFF) == c.Parameter ? 1f : 0f; // Amedo 2026-10-05
            case 122: return Alive(ref _slot118) is not { } b118 || b118._busy ? 1f : 0f; // Amedo 2026-09-30
            case 126: return FocusAhead_225988(); // Amedo 2026-09-30
            case 128: return Side_2285C0(FocusTarget(), 1);
            case 129: return Side_2285C0(FocusTarget(), 0);
            case 130: return Side_2285C0(Alive(ref _slot118)?.GamePosition, 1);
            case 78: return Alive(ref _slot11C) is not null ? 1f : 0f; // Amedo 2026-09-30
            case 124: return _attachedTo is not null ? 1f : 0f;
            case 53: return TouchingTerrain(c);
            case 147: return OnGround_228F58(); // Amedo 2026-09-30
            case 61: return IsResting_2290D8(); // Amedo
            case 75: return Cycle_22AC40(1); // Amedo
            case 76: return Cycle_22AC40(2); // Amedo
            case 103: return _route is not null ? _routeIndex & 0xFF : 0f; // Amedo
            case 117: return MessageOnce_22A028(c, ctx); // Amedo
            case 131: return Side_2285C0(Alive(ref _slot118)?.GamePosition, 0); // Amedo
            case 139: return FocusTarget() is { } fromStart ? GameMath.DistanceSq(_initMatrix.Translation, fromStart) : 0f; // Amedo
            case 140: return Alive(ref _slot118) is { } above ? GamePosition.Y - above.GamePosition.Y : 0f; // Amedo
            case 144: return SlotAlongAxisSq_2421D8(0); // Amedo
            case 145: return SlotAlongAxisSq_2421D8(1); // Amedo
            case 146: return SlotAlongAxisSq_2421D8(2); // Amedo
            case 148: return SlotAhead_2257E0(); // Amedo
            case 149: return SlotVisible_226360(c); // Amedo
            case 150: return SlotInViewCone_227C20(c); // Amedo
            case 154: return _keyCurrent == 0xFF || ((_keyCurrent + 1) & 1) == 0 ? 0f : 1f; // Amedo
            case 155: return FocusTarget() is { } fromExit && JointPosition_22DA00(c.Parameter & 0xFF, out var exitAt) ? GameMath.DistanceSq(fromExit, exitAt) : 0f; // Amedo
            case 157: return GameMatrix.M22; // Amedo
            case 158: return (ParamInt_2619D8(0) & 1) != 0 ? 0f : 1f; // Amedo
            case 161: return FocusObject is { } focusOne && ReferenceEquals(focusOne, Alive(ref _slot118)) ? 1f : 0f; // Amedo
            case 550: return EdgeHasFlag(8) ? 1f : 0f; // Amedo
            case 551: return EdgeHasFlag(0x10) ? 1f : 0f; // Amedo
            case 568: return PlayerInView_1276A0(c); // Amedo
            case 573: return PadInput_2B19D0(c.Parameter, 6); // Amedo
            case 574: return PadInput_2B19D0(c.Parameter, 7); // Amedo
            case 575: return PadInput_2B19D0(c.Parameter, 4); // Amedo
            case 616: return World.Player is { } nearPlayer && _hasFocusPosition ? GameMath.DistanceSq(_focusPosition, nearPlayer.GamePosition) : 1e30f; // Amedo
            case 618: return BesidePlayer_125D38(); // Amedo
            case 172: return _timer5C == 0 ? 0f : MathF.Max(0f, (World.NowTicks - _timer5C) * (1f / 9000f)); // Amedo 2026-09-30
            case 153: return RootController().GameObject is { } slots && c.Parameter < slots.BehaviourSlots.Count && slots.BehaviourSlots[c.Parameter] != 0xFFFF ? 1f : 0f; // Amedo 2026-09-30
            case 54: return _physics is { } touched && touched.Has(PhysicsController.TouchedAgent) ? 1f : 0f;
            case 106: return _physics is { TerrainType: not 0 } ? 1f : 0f;
            case 162: return _physics?.Body is not null ? 1f : 0f;
            case 133: return _spawnedBy is not null ? 1f : 0f;
            case 617: return World.PlayerCharacter is { Pushed: not null } ? 1f : 0f; // Amedo 2026-09-30
            case 623: return World.PlayerCharacter is { } weight && (weight.State70 & 0xF00ul) is 0x200 or 0x300 && ReferenceEquals(weight.StandingOn, this) ? 1f : 0f; // Amedo 2026-09-29
            case 630: return InWater ? 1f : 0f;
            case 72: return BoxAboveIsOverlapped() ? 1f : 0f; // Amedo 2026-09-28
            case 589: return DamageKind(0x10000);
            case 590: return DamageKind(0x40000);
            case 591: return DamageKind(0x20);
            case 592: return DamageKind(0x1000000);
            case 636: return DamageKind(0x80000);
            case 524: return CreatureHitPoints;
            case 520: return HitWithin(c.CheckInterval) ? 1f : 0f; // Amedo 2026-09-27
            case 521: return LastHitType is 6 or 10 && HitWithin(c.CheckInterval) ? 1f : 0f; // Amedo 2026-09-27
            case 522: return LastHitType is 7 or 11 && HitWithin(c.CheckInterval) ? 1f : 0f;
            case 523: return LastHitType is 8 or 12 && HitWithin(c.CheckInterval) ? 1f : 0f;
            case 534: return 6 <= LastHitType && HitWithin(c.CheckInterval) ? 1f : 0f;
            case 535: return LastHitType == 4 && HitWithin(c.CheckInterval) ? 1f : 0f;
            case 536: return LastHitType == 3 && HitWithin(c.CheckInterval) ? 1f : 0f;
            case 537: return LastHitType == 5 && HitWithin(c.CheckInterval) ? 1f : 0f;
            case 532: return LastHitType == 9 && HitWithin(c.CheckInterval) ? 1f : 0f; // Amedo 2026-10-05
            case 120: return GamePosition.Y; // Amedo 2026-10-05
            case 168: return LedgeDrop_2405F0(c.Parameter); // Amedo 2026-10-05
            case 37: // Amedo 2026-10-05
            {
                var (bmin, bmax) = TreeBox;
                return World.InCameraFrustum_227380?.Invoke(bmin + (bmax - bmin) * 0.5f) == true ? 1f : 0f;
            }
            case 39: // Amedo
            {
                if (World.CameraObjectMatrix?.Invoke() is not { } cam || World.InCameraFrustum_227380 is not { } inFrustum) return 0f;
                var (bmin, bmax) = TreeBox;
                var center = bmin + (bmax - bmin) * 0.5f;
                if (!inFrustum(center)) return 0f;
                return Chunk.Collision is { } col && col.SegmentCast(cam.Translation, center, 0x80, out _) ? 0f : 1f;
            }
            case 89: return _hasStoredPosition ? GameMath.DistanceSq(GamePosition, _storedPosition) : 1e30f;
            case 82: return _hasStoredPosition ? 1f : 0f; // Amedo 2026-09-28
            case 6: return World.RootLinksLoaded ? 1f : 0f; // Amedo 2026-09-30
            case 12: return World.IsZoneStateSet(this) ? 1f : 0f; // Amedo 2026-09-28
            case 125:
                if (_physics is not { } ev || (ev.State & PhysicsController.StateSurfaceEvent) == 0) return 0f;
                ev.State &= ~PhysicsController.StateSurfaceEvent;
                return 1f;
            case 138: return FocusToSlotSqrDist();
            case 60: return World.GlobalCounters.GetValueOrDefault(c.Parameter) == c.ReturnCheck ? c.ReturnCheck + c.ReturnCheck : 0f;
            case 59: return World.GlobalCounters.GetValueOrDefault(c.Parameter); // Amedo 2026-10-05
            case 87: return _physics is { } floor && (floor.State & PhysicsController.StateFloor) != 0 ? 1f : 0f; // Amedo 2026-10-05
            case 546: return ((RouteNode(_routeIndex)?.Flags ?? 0) & 2) != 0 ? 1f : 0f; // Amedo 2026-10-05
            case 175: return 0f;
            case 639: return (World.Progression >> 21) & 0x1F;
            case 637: return ((World.Progression >> 4) & 0xF) == 5 ? 1f : 0f;
            // Amedo 2026-09-27
            case 512: return World.PlayerCharacter is { } hp ? hp.HitPoints : 0f;
            case 587: return ReferenceEquals(this, World.Player) ? 1f : 0f; // Amedo 2026-09-28
            case 610: return DamageKind(0x80);
            case 611: return DamageKind(0x2);
            case 625: return DamageKind(0x2000000);
            case 626: return DamageKind(0x4);
            case 638: return DamageKind(0x400008);
            case 622: return 1f - PlayerHigh(25);
            case 514: return PlayerHigh(0);
            case 515: return PlayerFlag(Character.CharacterController.Grounded);
            case 557: return PlayerFlag(Character.CharacterController.Moving);
            case 558:
            case 560: return PlayerHigh(10);
            case 559: return PlayerHigh(11);
            case 561: return PlayerFlag(Character.CharacterController.Falling);
            case 562: return World.PlayerCharacter is { Carrying: true } ? 1f : 0f;
            case 563: return PlayerHigh(5);
            case 564: return World.PlayerCharacter is { MotionState: 9 } ? 1f : 0f;
            case 565: return World.PlayerCharacter is { MotionState: 6 or 10 } ? 1f : 0f;
            case 566: return World.PlayerCharacter is { MotionState: 8 or 12 } ? 1f : 0f;
            case 567: return PlayerHigh(1);
            case 570:
            case 571:
            case 572: return 0f;
            case 642: return World.PlayerCharacter is { Dead: true } ? 1f : 0f; // Amedo 2026-09-28
            case 644: return NonPlayerRequester_12C3C8() ? 1f : 0f; // Amedo 2026-10-05
            case 49: return (AgentFlags & 0x80) != 0 ? 1f : 0f; // Amedo 2026-10-05
            case 64: return LostAllAttachments_229E60() ? 1f : 0f; // Amedo
            case 627: return IsVehicleRollerbrawl_128CE8(World.PlayerCharacter) ? 1f : 0f; // Amedo 2026-10-05
            case 578: return OwnCharacter is { OnEdge: true } ? 1f : 0f;
            case 586: return OwnCharacter is { } own && (own.MotionFlagsHigh & (1u << 22)) != 0 ? 1f : 0f;
            case 577: // Amedo 2026-09-29
            case 593: return OwnCharacter?.Vehicle is not null ? 1f : 0f;
            case 594: return IsVehicleRollerbrawl_128CE8() ? 1f : 0f;
            case 596: return OwnCharacter?.Vehicle?.Kind == Character.Vehicles.Vehicle.Humiliskate ? 1f : 0f;
            case 598: return (OwnCharacter ?? World.PlayerCharacter)?.Vehicle?.Kind == Character.Vehicles.Vehicle.Hoverboard ? 1f : 0f;
            case 4: return Rendered ? 1f : 0f; // Amedo
            case 110: return WorldPosition.Y - _initPosition.Y; // Amedo
            case 176: return World.RootLinksQueued ? 1f : 0f; // Amedo
            case 135: return _paths.Count & 0xFF; // Amedo
            case 134: return _pathAlong; // Amedo
            case 167: return SplineGap_225338(); // Amedo
            case 620: return SkateCrouch_12D0B0(); // Amedo
            case 533: return LastHitType is 13 or 14 && HitWithin(c.CheckInterval) ? 1f : 0f; // Amedo
            case 74: return Cycle_22AC40(0); // Amedo
            case 94: return _lookAt is { } la25 && (la25.A0 & 0x42000000) == 0x42000000 ? 1f : 0f; // Amedo
            case 95: return _lookAt is { } la26 && (la26.A0 & 0x44000000) == 0x44000000 ? 1f : 0f; // Amedo
            case 604: return PreviousRouteStepFlag_12B0D0(0x10) ? 1f : 0f; // Amedo
            case 116: return _physics is { } wall && (wall.State & PhysicsController.StateWall) != 0 ? 1f : 0f; // Amedo
            case 108: return _physics?.RideObject_80 is not null ? 1f : 0f; // Amedo
            case 542: return ClearLineOfSightToPlayer_125F08(); // Amedo
            case 540: return FacingPlayer_125998(); // Amedo
            case 23: return FocusFacingMe_225BE8(); // Amedo
            case 163: return FocusObjectAwake_88() is { } counted && counted.Counters.GetValueOrDefault(0) == c.ReturnCheck ? c.ReturnCheck + c.ReturnCheck : 0f; // Amedo
            case 640: return World.PlayerCharacter?.Vehicle?.Value_D0 ?? 0f; // Amedo
            case 643: return ((World.Progression >> 16) & 0x1F) == c.Parameter ? 1f : 0f; // Amedo
            case 588: return DamageKind(0x20000); // Amedo
            case 641: return World.GameMode == 0xE ? 1f : 0f; // Amedo
            case 519: return HasCrateNode && 0 < _crateWumpa ? 1f : 0f; // Amedo
            case 177: return Region.RegionProfile.Current.Region == Region.GameRegion.Pal ? FocusInSameChunk_Pal_2521F8() : 0f; // Amedo 2026-11-11
        }
        World.Unimplemented("condition", c.ConditionIndex, AgentLabWorld.ConditionName(c.ConditionIndex));
        return 0f;
    }

    // Amedo 2026-09-27

    private int _timer5C; // Amedo 2026-09-30

    private float PlayerFlag(int bit) => World.PlayerCharacter is { } p && (p.MotionFlags & bit) != 0 ? 1f : 0f; // Amedo 2026-09-27

    // Amedo 2026-09-28
    private float DamageKind(uint mask) => (LastDamageKind & mask) != 0 ? 1f : 0f; // Amedo 2026-09-28

    // Amedo 2026-09-28
    private const float TickTime = 1f / 9000f; // Amedo

    private static int TicksOf(double seconds) => (int)Math.Round(seconds * 9000.0); // Amedo

    // Amedo
    private bool FreshMessage(float window)
    {
        if (double.IsNegativeInfinity(MessageTime)) return false;
        int sent = TicksOf(MessageTime);
        return !(World.NowTicks < sent) && (World.NowTicks - sent) * TickTime <= window;
    }

    private float PlayerHigh(int bit) => World.PlayerCharacter is { } p && (p.MotionFlagsHigh & (1u << bit)) != 0 ? 1f : 0f; // Amedo 2026-09-27

    private float MeFacingFocus() // Amedo 2026-09-27
    {
        Vector3 target;
        if (_hasFocusObject)
        {
            if (FocusObjectPosition is not { } f) return 0f; // Amedo
            target = f;
        }
        else if (FocusPosition is { } fp) target = fp;
        else return 0f;
        var here = GamePosition;
        var d = new Vector3(target.X - here.X, (target.Y + 0.8f) - here.Y, target.Z - here.Z);
        float lenSq = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
        d *= lenSq > 2.4999998e-09f ? 1f / MathF.Sqrt(lenSq) : 0f;
        var m = LocalMatrix;
        return m.M31 * d.X + m.M32 * d.Y + m.M33 * d.Z;
    }

    // Amedo 2026-09-30
    private Vector3? FocusTarget() => _hasFocusObject ? FocusObjectPosition : FocusPosition; // Amedo

    // Amedo 2026-10-06
    private float ClearLineOfSightToFocus_225E68()
    {
        if (FocusTarget() is not { } target) return 0f;
        var lift = new Vector3(0f, 0.8f, 0f);
        if (Chunk.Collision is not { } world) return 1f;
        return world.SegmentCast(GamePosition + lift, target + lift, 0x80, out _) ? 0f : 1f;
    }

    // Amedo
    private float Cycle_22AC40(int axis)
    {
        if (_wobble is not { } w) return 0f;
        return w.Phase[axis] * BitConverter.Int32BitsToSingle(0x3BB40000) * BitConverter.Int32BitsToSingle(0x3B360B61);
    }

    // Amedo
    private bool PreviousRouteStepFlag_12B0D0(ushort bit)
    {
        if (_route is null) return false;
        int step = (_routeIndex - 1) & 0xFF;
        if (step == 0xFF) step = 0;
        return RouteNode(step) is { } node && (node.Flags & bit) != 0;
    }

    // Amedo
    private AgentLabAgent? FocusObjectAwake_88()
    {
        if (!_hasFocusObject || _focusObject is null) return null;
        if (_focusObject.Destroyed || _focusObject.Asleep)
        {
            _focusObject = null;
            _hasFocusObject = _hasFocusPosition = false;
            return null;
        }
        return _focusObject;
    }

    // Amedo
    private float FocusFacingMe_225BE8()
    {
        Matrix4x4 m;
        if (FocusIsCamera && World.CameraObjectMatrix?.Invoke() is { } camera) m = camera;
        else if (FocusObjectAwake_88() is { } focus) m = focus.LocalMatrix;
        else return 0f;
        var d = GamePosition + new Vector3(0f, 0.8f, 0f) - m.Translation;
        d *= GameMath.InverseLength(d);
        return m.M31 * d.X + m.M32 * d.Y + m.M33 * d.Z;
    }

    // Amedo
    private float FacingPlayer_125998()
    {
        if (World.Player is not { } player) return 0f;
        var m = LocalMatrix;
        var d = player.GamePosition + new Vector3(0f, 0.5f, 0f) - GamePosition;
        d *= GameMath.InverseLength(d);
        return m.M31 * d.X + m.M32 * d.Y + m.M33 * d.Z;
    }

    // Amedo
    private float ClearLineOfSightToPlayer_125F08()
    {
        if (World.Player is not { } player) return 0f;
        var lift = new Vector3(0f, 0.5f, 0f);
        if (Chunk.Collision is not { } world) return 1f;
        return world.SegmentCast(GamePosition + lift, player.GamePosition + lift, 0x80, out _) ? 0f : 1f;
    }

    // Amedo 2026-10-06
    private float HeadLookingAtFocus_227758(int exit)
    {
        if (FocusTarget() is not { } target || !ExitPointPlace_23C900(exit, out var position, out var direction)) return 0f; // Amedo
        var d = target + new Vector3(0f, 0.8f, 0f) - position;
        d *= GameMath.InverseLength(d);
        return direction.X * d.X + direction.Y * d.Y + direction.Z * d.Z;
    }

    // Amedo 2026-09-30
    private float FocusAhead_225988()
    {
        if (FocusTarget() is not { } target) return 0f;
        var d = target - GamePosition;
        float lenSq = GameMath.LengthSquared(d);
        float inv = lenSq > 2.4999998e-09f ? 1f / MathF.Sqrt(lenSq) : 0f;
        var m = LocalMatrix;
        return m.M31 * d.X * inv + m.M33 * d.Z * inv;
    }

    // Amedo 2026-09-30
    private float Side_2285C0(Vector3? target, int mode)
    {
        if (target is not { } t) return 0f;
        var d = t - GamePosition;
        float lenSq = GameMath.LengthSquared(d);
        float inv = lenSq > 2.4999998e-09f ? 1f / MathF.Sqrt(lenSq) : 0f;
        var m = LocalMatrix;
        float r = (m.M11 * d.X + m.M12 * d.Y + m.M13 * d.Z) * inv;
        if (mode == 1) return r < 0f ? 0f : MathF.Abs(r);
        if (mode == 0) return r <= 0f ? MathF.Abs(r) : 0f;
        return MathF.Abs(r);
    }

    private float FocusToSlotSqrDist() // Amedo 2026-09-27
    {
        Vector3? focus = _hasFocusObject ? FocusObjectPosition : FocusPosition; // Amedo
        var slot = Alive(ref _slot118);
        return focus is { } f && slot is not null ? GameMath.DistanceSq(f, slot.GamePosition) : 0f;
    }

    private float MeToKeySqrDist(int parameter)
    {
        var owner = _controller ?? this;
        Vector3? key = parameter is 0 or 0xFF ? owner.Key(owner._keyCurrent) : owner.Key(parameter - 1);
        return key is { } k ? GameMath.DistanceSq(GamePosition, k) : 0f;
    }

    private int Var(int index)
    {
        var p = Instance.Source.ParamList3;
        return p is not null && index < p.Count ? (int)p[index] : 0;
    }

    // Amedo 2026-10-05
    private bool NonPlayerRequester_12C3C8()
    {
        var requester = _channel == 0 ? Requester : _otherChannel?.Requester;
        if (requester is null || ReferenceEquals(requester, World.Player)) return false;
        if ((requester.ComponentMask & (1u << 12)) == 0) return false;
        return ((World.Progression >> 4) & 0xF) == AgentLabWorld.PlayerModeSingle;
    }

    // Amedo

    // Amedo
    private static float Dot3(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    // Amedo
    private float IsResting_2290D8()
    {
        if (_physics is { Body: { } body })
            return body.Velocity.X * body.Velocity.X + body.Velocity.Y * body.Velocity.Y + body.Velocity.Z * body.Velocity.Z < 0.05f
                && body.Spin.X * body.Spin.X + body.Spin.Y * body.Spin.Y + body.Spin.Z * body.Spin.Z < 0.05f ? 1f : 0f;
        if (_contactResponse is not null) return 1f;
        return _velocity.X * _velocity.X + _velocity.Y * _velocity.Y + _velocity.Z * _velocity.Z < 0.001f ? 1f : 0f;
    }

    // Amedo
    // Amedo
    private float MeToInitPosSqrDist_2518B8()
    {
        var d = GamePosition - _initMatrix.Translation;
        return d.X * d.X + d.Y * d.Y + d.Z * d.Z;
    }

    private static float MessageOnce_22A028(TwinBehaviourCondition c, Level ctx)
    {
        if (ctx.Value_1A != c.Parameter) return 0f;
        ctx.Value_1A = 0xFFFF;
        return 1f;
    }

    // Amedo
    private float SlotAlongAxisSq_2421D8(int row)
    {
        var m = GameMatrix;
        var me = GamePosition;
        if (Alive(ref _slot118) is not { } slot) return 0f;
        var axis = row == 0 ? new Vector3(m.M11, m.M12, m.M13) : row == 1 ? new Vector3(m.M21, m.M22, m.M23) : new Vector3(m.M31, m.M32, m.M33);
        var d = slot.GamePosition - me;
        float along = Dot3(axis, d);
        var at = me + axis * along;
        return GameMath.DistanceSq(me, at);
    }

    // Amedo
    private float SlotAhead_2257E0()
    {
        if (Alive(ref _slot118) is not { } slot) return 0f;
        var s = slot.GamePosition;
        var me = GamePosition;
        var d = new Vector3(s.X - me.X, s.Y + 0.8f - me.Y, s.Z - me.Z);
        d *= GameMath.InverseLength(d);
        var m = GameMatrix;
        return m.M31 * d.X + m.M32 * d.Y + m.M33 * d.Z;
    }

    // Amedo
    private float SlotVisible_226360(TwinBehaviourCondition c)
    {
        if (Alive(ref _slot118) is not { } slot) return 0f;
        var from = slot.GamePosition;
        var (bmin, bmax) = TreeBox;
        var mid = new Vector3((bmin.X + bmax.X) * 0.5f, (bmin.Y + bmax.Y) * 0.5f, (bmin.Z + bmax.Z) * 0.5f);
        var move = mid - from;
        bool hit = LineOfSight_280958(from, ref move, 0x80, PairMask_309D78, Parent28, out var other); // Amedo
        return !hit || ReferenceEquals(other, slot) ? c.ReturnCheck + c.ReturnCheck : 0f;
    }

    // Amedo
    private float SlotInViewCone_227C20(TwinBehaviourCondition c)
    {
        if (Alive(ref _slot118) is not { } slot) return 0f;
        var target = slot.GamePosition;
        if (!ExitPointPlace_23C900(c.Parameter & 0xFF, out var pos, out var dir)) return 0f;
        target.Y += 0.8f;
        var n = target - pos;
        n *= GameMath.InverseLength(n);
        if (!(c.ReturnCheck < Dot3(dir, n))) return 0f;
        if (Chunk.Collision is { } world && world.SegmentCast(pos, target, 0x80, out _)) return 0f;
        return c.ReturnCheck + c.ReturnCheck;
    }

    // Amedo
    private float PlayerInView_1276A0(TwinBehaviourCondition c)
    {
        int slotIndex = (int)(World.Progression >> 8) & 0xF;
        if (slotIndex == 6 || World.PlayerSlot_570(slotIndex) is not { } player) return 0f;
        if (!ExitPointPlace_23C900(c.Parameter & 0xFF, out var pos, out var dir)) return 0f;
        var p = player.GamePosition;
        if (!(GameMath.DistanceSq(pos, p) < 1600f) || player.OwnCharacter is not { } pc) return 0f;
        float height = (pc.MotionFlagsHigh & 1u) == 0 ? pc.StandHeight_C0 : pc.LowHeight_C0;
        var move = new Vector3(p.X - pos.X, p.Y + height - pos.Y, p.Z - pos.Z);
        var n = move * GameMath.InverseLength(move);
        if (!(c.ReturnCheck < Dot3(dir, n))) return 0f;
        bool hit = LineOfSight_280958(pos, ref move, 0x80, 0x15B010, Parent28, out var other); // Amedo
        return !hit || ReferenceEquals(other, player) ? c.ReturnCheck + c.ReturnCheck : 0f;
    }

    // Amedo
    private float BesidePlayer_125D38()
    {
        int slotIndex = (int)(World.Progression >> 8) & 0xF;
        if (slotIndex == 6 || World.PlayerSlot_570(slotIndex) is not { } player) return 0f;
        var p = player.GamePosition;
        var pm = player.GameMatrix;
        var me = GamePosition;
        var d = new Vector3(me.X - p.X, me.Y + 0.5f - p.Y, me.Z - p.Z);
        d *= GameMath.InverseLength(d);
        return pm.M11 * d.X + pm.M12 * d.Y + pm.M13 * d.Z;
    }

    // Amedo
    private float PadInput_2B19D0(int pad, int input) => pad == 1 && World.Pad1 is { } p ? p.GetButtonPressure_2B19D0(input) : 0f;
}
