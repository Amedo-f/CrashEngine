using System.Numerics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-26
public sealed partial class AgentLabAgent
{
    private const int PacketSelector = 0;
    private const int PacketKeyIndex = 1;
    private const int PacketMoveSpeed = 2;
    private const int PacketTurnSpeed = 3;
    private const int PacketRawPosX = 4;
    private const int PacketPitch = 7;
    private const int PacketDecDist = 19;
    private const int PacketAcDist = 20;
    private const int PacketSyncUnit = 21;

    private Vector3? _srcKey;
    private Vector3? _srcRoute;
    private AgentLabAgent? _srcTarget;
    private Vector3? _srcSpecial;

    private Matrix4x4 _initMatrix = Matrix4x4.Identity;
    private bool _moving;
    private bool _translationDone;
    private bool _rotationDone;
    private bool _smooth;
    private Vector3 _moveStart;
    private Vector3 _moveTarget;
    private double _moveRate;
    private Quaternion _rotStart;
    private Quaternion _rotTarget;
    private bool _turns; // Amedo
    private Quaternion _rotTargetStale; // Amedo

    private bool MotionComplete => _moving && _translationDone && _rotationDone;

    private Matrix4x4 LocalMatrix => Entity.Transform.LocalMatrix ?? Matrix4x4.CreateTranslation(Entity.Transform.Position);

    // Amedo 2026-09-27
    public Matrix4x4 GameMatrix
    {
        get => LocalMatrix;
        set
        {
            Entity.Transform.LocalMatrix = value;
            MarkMoved_197048(); // Amedo
        }
    }

    private void SetLocalPosition(Vector3 position)
    {
        if (Entity.Transform.LocalMatrix is { } m)
        {
            m.Translation = position;
            Entity.Transform.LocalMatrix = m;
        }
        else Entity.Transform.Position = position;
        MarkMoved_197048(); // Amedo
    }

    private Quaternion LocalRotation
    {
        get
        {
            var m = LocalMatrix;
            m.Translation = Vector3.Zero;
            return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
        }
    }

    private void SetLocalRotation(Quaternion rotation)
    {
        var m = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation));
        m.Translation = LocalMatrix.Translation;
        Entity.Transform.LocalMatrix = m;
        MarkMoved_197048(); // Amedo
    }

    // Amedo
    private void MoveInstance_23C280(Vector3 position)
    {
        if (!_springRest_88)
        {
            SetLocalPosition(position);
            return;
        }
        if (_storedSpace is not { } place || ControlPacket is { Space: TwinBehaviourControlPacket.SpaceType.STORED_SPACE }) return;
        place.Translation = position;
        _storedSpace = place;
    }

    // Amedo
    private void TurnInstance_23C468(Quaternion rotation)
    {
        if (!_springRest_88)
        {
            SetLocalRotation(rotation);
            return;
        }
        if (_storedSpace is not { } place || ControlPacket is { Space: TwinBehaviourControlPacket.SpaceType.STORED_SPACE }) return;
        var m = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation));
        m.Translation = place.Translation;
        _storedSpace = m;
        _storedSpaceQuat = rotation;
    }

    private void StartMotion(TwinBehaviourControlPacket packet)
    {
        _moving  = false;
        _chasing = false;
        _projectile = false;
        _springing = false; // Amedo
        _sqrTolerance = PacketFloat(packet, PacketRandRange, out float tolerance) ? tolerance : 0f;
        var motion = packet.Motion;
        if (motion == TwinBehaviourControlPacket.MotionType.NO_MOTION) return;
        bool chase = motion is TwinBehaviourControlPacket.MotionType.GROUND_CHASE or TwinBehaviourControlPacket.MotionType.AIR_CHASE
                            or TwinBehaviourControlPacket.MotionType.UNKNOWN_11 or TwinBehaviourControlPacket.MotionType.UNKNOWN_12
                            or TwinBehaviourControlPacket.MotionType.UNKNOWN_13; // Amedo
        bool projectile = motion == TwinBehaviourControlPacket.MotionType.PROJECTILE; // Amedo 2026-09-27
        bool spring = motion == TwinBehaviourControlPacket.MotionType.SPRING; // Amedo
        if (motion != TwinBehaviourControlPacket.MotionType.LINEAR_INTERP && !chase && !projectile && !spring)
        {
            World.Unimplemented("motion", (int)motion, motion.ToString());
            return;
        }
        if (!chase) HoldMotion(); // Amedo 2026-09-29

        _translationDone = !packet.Translates;
        _rotationDone    = !packet.Rotates;
        _turns           = false; // Amedo
        _smooth          = packet.AccelerationFunction == TwinBehaviourControlPacket.AccelFunction.SMOOTH_CURVE;
        _accelerated     = false; // Amedo
        _moveStart       = LocalMatrix.Translation;
        bool hasTarget   = PacketTarget(packet, out var target);
        if (packet.TracksDestination) _slotAC = _srcTarget; // Amedo
        if (_srcKey is not null && motion == TwinBehaviourControlPacket.MotionType.LINEAR_INTERP) KeyDirection_20F020(); // Amedo

        if (projectile)
        {
            if (!packet.Translates || !hasTarget) return;
            _moveTarget = target;
            StartProjectile(packet);
            if (packet.Rotates) StartRotation_20D420(packet, target, false); // Amedo
            _projectileTurn = _turns; // Amedo
            _moving = true;
            return;
        }

        if (spring) // Amedo
        {
            if (!packet.Translates || !hasTarget) return;
            if (target == _moveStart) _translationDone = true;
            else StartSpring_20CDD4(packet, target);
            if (packet.Rotates) StartRotation_20D420(packet, target, false); // Amedo
            _moving = true;
            return;
        }

        if (chase)
        {
            if (!packet.Translates || !hasTarget)
            {
                if (!packet.Translates) World.Unimplemented("motion", (int)motion, $"{motion} without translation");
                return;
            }
            _moveTarget = target;
            if (_moveTarget == _moveStart) _translationDone = true;
            else StartChase(packet);
            if (packet.Rotates) StartRotation_20D420(packet, target, false); // Amedo
            _moving = true;
            return;
        }

        if (packet.Translates)
        {
            if (!hasTarget) return;
            _moveTarget = target;
            if (_moveTarget == _moveStart) _translationDone = true;
            else _moveRate = LinearRate(packet, Distance_20D144(_moveStart, _moveTarget)); // Amedo
        }

        if (packet.Rotates) StartRotation_20D420(packet, hasTarget ? target : null, !packet.Translates); // Amedo
        _moving = true;
    }

    // Amedo
    private void StartRotation_20D420(TwinBehaviourControlPacket packet, Vector3? facing, bool rate)
    {
        _rotStart = LocalRotation;
        _rotTargetStale = _rotTarget;
        _turns = RotationTarget(packet, facing, out _rotTarget);
        if (!_turns)
        {
            _rotationDone = true;
            return;
        }
        if (rate) _moveRate = TurnRate(packet);
        if (RotationReached_20D958(_rotStart, _rotTarget)) _rotationDone = true;
    }

    // Amedo
    private static bool RotationReached_20D958(Quaternion a, Quaternion b) =>
        MathF.Abs(a.X - b.X) <= 5e-5f && MathF.Abs(a.Y - b.Y) <= 5e-5f && MathF.Abs(a.Z - b.Z) <= 5e-5f && MathF.Abs(a.W - b.W) <= 5e-5f;

    private bool RotationTarget(TwinBehaviourControlPacket packet, Vector3? facing, out Quaternion target)
    {
        target = Quaternion.Identity;
        if (PacketHas(packet, PacketDamping))
            World.Unimplemented("motion rotation", (int)packet.Space, "rotation yaw snap (field 17, 0x1874B8)");
        if (!packet.YawFaces)
        {
            if (PacketHas(packet, PacketRotationSource))
            {
                World.Unimplemented("motion rotation", (int)packet.Space, "rotation start/source override (field 22, ctx+0x27)");
                return false;
            }
            bool turnsBy = PacketAngles(packet, true, out var euler); // Amedo
            return SpaceRotation(packet, turnsBy, euler, out target);
        }
        if (facing is not { } f) return false;
        bool facingTurns = PacketAngles(packet, false, out var faceEuler); // Amedo

        var delta = f - _moveStart;
        if (Tracing) Trace($"face target {f} from {_moveStart} delta {delta}"); // Amedo
        if (1e-6f < delta.X * delta.X + delta.Z * delta.Z) // Amedo
            target = GameMath.RotationY_18D620(GameMath.Yaw_18C830(new Vector4(delta, 1f)));
        if (facingTurns) target = GameMath.Mul_18D558(target, faceEuler); // Amedo
        return true;
    }

    private const int PacketRotationSource = 22;

    // Amedo
    private Quaternion _keyDirD0 = Quaternion.Identity;

    // Amedo
    private void KeyDirection_20F020()
    {
        int count = _keys.Count;
        if (count == 0 || _keyCurrent < 0 || _keyCurrent >= count) return;
        int next = _keyCurrent + 1;
        if (next >= count) next -= count;
        var d = _keys[next] - _keys[_keyCurrent];
        _keyDirD0 = d.X == 0f && d.Z == 0f ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.UnitY, AngleUnits(MathF.Atan2(d.X, d.Z)));
    }

    private static float AngleUnits(float radians) => (int)(radians * 10430.378f) * 9.58738e-5f;

    // Amedo
    private bool PacketAngles(TwinBehaviourControlPacket packet, bool variables, out Quaternion euler)
    {
        var holder = variables ? RootController() : null;
        bool missing = false;
        Span<int> angle = stackalloc int[3];
        for (int i = 0; i < 3; i++)
        {
            int field = PacketPitch + i;
            byte b = field < packet.Bytes.Count ? packet.Bytes[field] : (byte)0xFF;
            if (b == 0xFF)
            {
                missing = true;
                angle[i] = GameMath.FromValue_11BFA0(0f, 0);
            }
            else if (holder is not null && (b & 0x80) != 0) angle[i] = holder.AngleVariable_261778(b & 0x7F);
            else angle[i] = GameMath.FromValue_11BFA0(b < packet.Floats.Count ? BitConverter.UInt32BitsToSingle(packet.Floats[b]) : 0f, 0);
        }
        euler = GameMath.Euler_186780(angle[0], angle[1], angle[2]);
        return missing;
    }

    private bool SpaceRotation(TwinBehaviourControlPacket packet, bool angles, Quaternion euler, out Quaternion target)
    {
        target = Quaternion.Identity;
        Quaternion? from = null;
        switch (packet.Space)
        {
            case TwinBehaviourControlPacket.SpaceType.WORLD_SPACE:
            case TwinBehaviourControlPacket.SpaceType.TARGET_SPACE:
                if (_srcKey is not null || (_srcRoute is not null && packet.Space == TwinBehaviourControlPacket.SpaceType.WORLD_SPACE))
                    from = _keyDirD0; // Amedo
                else if (_srcTarget is { } t) from = t.LocalRotation;
                break;
            case TwinBehaviourControlPacket.SpaceType.INITIAL_SPACE: from = RotationOf(_initMatrix); break;
            case TwinBehaviourControlPacket.SpaceType.CURRENT_SPACE: from = LocalRotation; break;
            case TwinBehaviourControlPacket.SpaceType.STORED_SPACE: if (_storedSpace is { } s) from = RotationOf(s); break;
            case TwinBehaviourControlPacket.SpaceType.PARENT_SPACE: // Amedo
            case TwinBehaviourControlPacket.SpaceType.CURRENT_POS:
                target = _rotTargetStale;
                return true;
            default:
                World.Unimplemented("motion rotation", (int)packet.Space, $"{packet.Space} rotation target (unchanged in the game)");
                return false;
        }
        target = from is { } b ? angles ? GameMath.Mul_18D558(b, euler) : b : angles ? euler : Quaternion.Identity; // Amedo
        return true;
    }

    private bool ResolvePacketSources(TwinBehaviourControlPacket packet)
    {
        _srcKey = _srcRoute = _srcSpecial = null;
        _srcTarget = null;

        int selector = PacketInt(packet, PacketSelector) is { } sel ? (int)sel : SourceNone;
        if (PacketHas(packet, PacketSyncUnit))
            World.Unimplemented("motion target", (int)packet.Space, "SyncUnit");

        if (selector != SourceNone)
        {
            if (selector == RefFocusObject && FocusIsCamera) return (_srcSpecial = FocusObjectPosition) is not null; // Amedo
            _srcTarget = selector == RefFocusObject ? FocusObject : Participant(selector);
            return selector != RefFocusObject || _srcTarget is not null;
        }

        if (PacketInt(packet, PacketKeyIndex) is not { } k || k == SourceNone) return true;
        int key = (int)k;
        switch (key)
        {
            case RefPlayer: _srcTarget = World.Player; return true;
            case 0xF5: World.Unimplemented("motion target", key, "[[A+0x10C]+0xB0] object"); return false;
            case RefStored: if (_hasStoredPosition) _srcSpecial = _storedPosition; return true;
            case RefSlot11C: return (_srcTarget = Alive(ref _slot11C)) is not null;
            case RefSlot118: return (_srcTarget = Alive(ref _slot118)) is not null;
            case 0xF9:
                if (_route is null) return true;
                PreviousRouteStep_20EEB0(); // Amedo
                _srcRoute = RouteNode(_routeIndex)?.Position;
                return true;
            case RefRouteNode: _srcRoute = RouteNode(_routeIndex)?.Position; return true;
            case RefFocusObject: // Amedo
                if (FocusIsCamera) return (_srcSpecial = FocusObjectPosition) is not null;
                return (_srcTarget = FocusObject) is not null;
            case RefFocusPosition: _srcSpecial = FocusPosition; return true;
            case RefNextKey:
                NextKey();
                _srcKey = Key(_keyCurrent);
                return true;
            case RefCurrentKey: _srcKey = Key(_keyCurrent); return true;
        }

        _endReached = key == _keys.Count;
        int index = key < _keys.Count ? key : key - _keys.Count;
        _keyCurrent = index;
        _srcKey = Key(index);
        return true;
    }

    private double TurnRate(TwinBehaviourControlPacket packet)
    {
        PacketFloat(packet, PacketTurnSpeed, out float turnSpeed);
        float speed = AngleUnits(turnSpeed); // Amedo 2026-10-06
        byte speedByte = PacketTurnSpeed < packet.Bytes.Count ? packet.Bytes[PacketTurnSpeed] : (byte)0xFF; // Amedo
        if (speedByte != 0xFF && (speedByte & 0x80) != 0) speed = RootController().AngleVariable_261778(speedByte & 0x7F) * 9.58738e-5f; // Amedo
        if (Tracing) Trace($"turn rate: field {PacketTurnSpeed} byte 0x{speedByte:X2} speed {speed}"); // Amedo
        int angle = AngleUnits_186F38(_rotStart, _rotTarget);
        bool acc = PacketFloat(packet, PacketAcDist, out float accel);
        bool dec = PacketFloat(packet, PacketDecDist, out float decel);
        if (acc || dec)
        {
            float curve = accel + ((angle * 9.58738e-5f / speed - accel * 0.5f) - decel * 0.5f) + decel; // Amedo 2026-10-06
            if (curve < 0f) curve = 0f;
            _accelerated = true; // Amedo
            _accSpan = (accel, decel);
            _accTotal = curve;
            _accSpeed = speed;
            _accDistance = angle * 9.58738e-5f;
            return 5e-5f < MathF.Abs(curve) ? 1.0 / curve : 10000.0;
        }
        float duration = GameMath.Trunc(angle / speed) * 9.58738e-5f; // Amedo 2026-10-06
        return 5e-5f < MathF.Abs(duration) ? 1.0 / duration : 1e30;
    }

    // Amedo 2026-10-06
    private static int AngleUnits_186F38(Quaternion a, Quaternion b)
    {
        float la = GameMath.Length(a), lb = GameMath.Length(b);
        if (0f < la) a *= 1f / la;
        if (0f < lb) b *= 1f / lb;
        float dot = MathF.Abs(Quaternion.Dot(a, b));
        float s = 1f - dot;
        if (s < 0f) s = 0f;
        float acos = (((dot * -0.0187293f + 0.074261f) * dot - 0.2121144f) * dot + 1.5707288f) * MathF.Sqrt(s);
        return GameMath.Trunc(acos * 10430.378f);
    }

    private double LinearRate(TwinBehaviourControlPacket packet, float distance)
    {
        PacketFloat(packet, PacketMoveSpeed, out float speed);
        bool acc = PacketFloat(packet, PacketAcDist, out float accel);
        bool dec = PacketFloat(packet, PacketDecDist, out float decel);
        float duration = distance / speed;
        if (acc || dec)
        {
            duration = accel + (duration - accel * 0.5f - decel * 0.5f) + decel;
            _accelerated = true; // Amedo
            _accSpan = (accel, decel);
            _accTotal = duration;
            _accSpeed = speed;
            _accDistance = distance;
        }
        return MathF.Abs(duration) <= 5e-5f ? 1e30 : 1.0 / duration;
    }

    // Amedo
    private static float Distance_20D144(Vector3 a, Vector3 b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private bool _accelerated;
    private (float Acc, float Dec) _accSpan;
    private float _accTotal, _accSpeed, _accDistance;

    private float AcceleratedFraction_232FD8(float tau)
    {
        float total = _accTotal, v = _accSpeed;
        var packet = ControlPacket; // Amedo
        bool hasAcc = packet is not null && PacketFloat(packet, PacketAcDist, out _accSpan.Acc);
        bool hasDec = packet is not null && PacketFloat(packet, PacketDecDist, out _accSpan.Dec);
        float a = Clamp_18C2A0(_accSpan.Acc, 0f, total);
        float d = Clamp_18C2A0(_accSpan.Dec, 0f, total);
        float tail = total - d;
        float x;
        if (hasAcc && tau < a) x = v / a * tau * tau * 0.5f;
        else if (hasDec && tail < tau)
        {
            float start = hasAcc ? v / a * a * a * 0.5f : 0f;
            float left = d - (tau - tail);
            x = start + v * (total - a - d) + v / d * d * d * 0.5f - v / d * left * left * 0.5f;
        }
        else if (hasAcc) x = v / a * a * a * 0.5f + v * (tau - a);
        else x = v * tau;
        return MathF.Abs(_accDistance) <= 5e-5f ? 1e6f : x / _accDistance;
    }

    private bool PacketTarget(TwinBehaviourControlPacket packet, out Vector3 target)
    {
        target = default;
        bool hasRaw = RawPos(packet, out var raw);
        switch (packet.Space)
        {
            case TwinBehaviourControlPacket.SpaceType.WORLD_SPACE:
            {
                var source = _srcKey ?? _srcRoute ?? _srcTarget?.GamePosition ?? _srcSpecial;
                target = source is { } s ? s + (hasRaw ? raw : Vector3.Zero) : hasRaw ? raw : Vector3.Zero;
                return true;
            }
            case TwinBehaviourControlPacket.SpaceType.INITIAL_SPACE:
                target = _initMatrix.Translation + (hasRaw ? Vector3.TransformNormal(raw, _initMatrix) : Vector3.Zero);
                return true;
            case TwinBehaviourControlPacket.SpaceType.CURRENT_SPACE:
            case TwinBehaviourControlPacket.SpaceType.STORED_SPACE: // Amedo 2026-09-27
            {
                var m = LocalMatrix;
                target = m.Translation + (hasRaw ? Vector3.TransformNormal(raw, m) : Vector3.Zero);
                return true;
            }
            case TwinBehaviourControlPacket.SpaceType.TARGET_SPACE:
                if (_srcKey is not null || _srcRoute is not null || _srcTarget is null) // Amedo
                {
                    target = _moveTarget;
                    return true;
                }
                var tm = _srcTarget.LocalMatrix;
                target = tm.Translation + (hasRaw ? Vector3.TransformNormal(raw, tm) : Vector3.Zero);
                return true;
            case TwinBehaviourControlPacket.SpaceType.PARENT_SPACE: // Amedo
            {
                if (_srcTarget is not { } chased) return false;
                target = chased.GamePosition;
                if (hasRaw)
                {
                    var d = chased.GamePosition - GamePosition;
                    target += Vector3.TransformNormal(raw, Along_185A40(d * GameMath.InverseLength(d)));
                }
                return true;
            }
            case TwinBehaviourControlPacket.SpaceType.CURRENT_POS: // Amedo 2026-09-27
                if (PathTarget(hasRaw, raw, out target)) return true;
                World.Unimplemented("motion target", (int)packet.Space, "CURRENT_POS without a path");
                return false;
        }
        World.Unimplemented("motion target", (int)packet.Space, packet.Space.ToString());
        return false;
    }

    private static uint? PacketInt(TwinBehaviourControlPacket packet, int field)
    {
        if (field >= packet.Bytes.Count) return null;
        byte b = packet.Bytes[field];
        return b != 0xFF && b < packet.Floats.Count ? packet.Floats[b] : null;
    }

    private bool RawPos(TwinBehaviourControlPacket packet, out Vector3 raw)
    {
        bool x = PacketFloat(packet, PacketRawPosX, out float rx);
        bool y = PacketFloat(packet, PacketRawPosX + 1, out float ry);
        bool z = PacketFloat(packet, PacketRawPosX + 2, out float rz);
        raw = new Vector3(rx, ry, rz);
        return x || y || z;
    }

    private static bool PacketHas(TwinBehaviourControlPacket packet, int field) =>
        field < packet.Bytes.Count && packet.Bytes[field] != 0xFF;

    private Vector3 _followOffset40; // Amedo

    // Amedo
    private Vector3 FollowOffset_20A9A0(TwinBehaviourControlPacket packet, Vector3 followed, Vector3 destination)
    {
        if (PacketFloat(packet, 0x12, out float chance) && World.Rng.GetRandFloat_18C0B8() < chance && PacketFloat(packet, 0x13, out float spread)) // Amedo
        {
            var d = followed - GamePosition;
            d *= GameMath.InverseLength(d);
            _followOffset40 = new Vector3(d.Z, 0f, -d.X) * RandomSpread(spread);
            if (Tracing) Trace($"follow target: new side offset {_followOffset40} (chance {chance}, spread {spread})");
        }
        return destination + _followOffset40;
    }

    // Amedo
    private void FollowTarget_20A9A0(TwinBehaviourControlPacket packet)
    {
        if (packet.Space == TwinBehaviourControlPacket.SpaceType.CURRENT_POS) // Amedo 2026-09-27
        {
            var pathDirection = _pathDirection; // Amedo
            if (PacketTarget(packet, out var ahead)) _moveTarget = FollowOffset_20A9A0(packet, pathDirection, ahead);
            return;
        }
        if (Alive(ref _slotAC) is not { } followed) return;
        _srcKey = _srcRoute = _srcSpecial = null;
        _srcTarget = followed;
        if (PacketTarget(packet, out var moved)) _moveTarget = FollowOffset_20A9A0(packet, followed.GamePosition, moved);
    }

    // Amedo
    internal void MotionStop_20E910()
    {
        _velocity = Vector3.Zero;
        _chaseCurrentSpeed = 0f;
    }

    // Amedo
    private void StopNodeMotion_23DF48()
    {
        MotionStop_20E910();
        _physics?.Body?.Stop_291398();
    }

    private void UpdateMotion()
    {
        HoldTrajectory_23B140(); // Amedo
        if (!_moving) return; // Amedo
        if (ControlPacket is { } follows && (follows.TracksDestination || follows.Space is TwinBehaviourControlPacket.SpaceType.INITIAL_POS
                                                                         or TwinBehaviourControlPacket.SpaceType.CURRENT_POS))
            FollowTarget_20A9A0(follows); // Amedo
        if (ControlPacket!.Motion == TwinBehaviourControlPacket.MotionType.LINEAR_INTERP) // Amedo
        {
            HoldMotion(); // Amedo 2026-09-29
            StepInterpolation_2326A0();
        }
        else
        {
            if (ControlPacket.Rotates && !_rotationDone && StepRotation_232D70()) SetTrajectoryRest60(_rotTarget); // Amedo
            bool moved = _translationDone; // Amedo
            if (_springing) // Amedo
            {
                if (!_translationDone) StepSpring_232260(StepDt);
            }
            else if (_chasing)
            {
                if (!_translationDone) UpdateChase(StepDt);
            }
            else if (_projectile)
            {
                HoldMotion(); // Amedo 2026-09-29
                if (!_translationDone) UpdateProjectile(StepDt);
            }
            if (!moved && _translationDone) // Amedo
            {
                _rotationDone = true;
                SetTrajectoryRest50(_moveTarget);
            }
        }
        if (ControlPacket is { OrientsPredicts: true }) FaceMotion_20DD38(); // Amedo
    }

    // Amedo
    private bool StepRotation_232D70()
    {
        if (_accelerated) return StepAcceleratedRotation_232ED8();
        float t = (float)((World.Now - _packetStart) * _moveRate);
        if (1f <= t || t < -1f)
        {
            if (_turns) TurnInstance_23C468(_rotTarget);
            _rotationDone = true;
            return true;
        }
        if (_smooth) t = 3f * t * t - 2f * t * t * t;
        if (_turns) TurnInstance_23C468(GameMath.Slerp_187100(t, _rotStart, _rotTarget));
        return false;
    }

    // Amedo
    private bool StepAcceleratedRotation_232ED8()
    {
        float elapsed = (float)(World.Now - _packetStart);
        float t = (float)(elapsed * _moveRate);
        if (1f <= t || t < -1f)
        {
            if (_turns) TurnInstance_23C468(_rotTarget);
            _rotationDone = true;
            return true;
        }
        if (_turns) TurnInstance_23C468(GameMath.Slerp_187100(AcceleratedFraction_232FD8(elapsed), _rotStart, _rotTarget));
        return false;
    }

    // Amedo
    private void StepInterpolation_2326A0()
    {
        bool moves = ControlPacket!.Translates;
        double t = (World.Now - _packetStart) * _moveRate;
        if (t >= 1.0 || t <= -1.0)
        {
            if (moves)
            {
                MoveInstance_23C280(_moveTarget);
                _translationDone = true;
                SetTrajectoryRest50(_moveTarget); // Amedo
            }
            _rotationDone = true;
            if (ControlPacket.Rotates)
            {
                if (_turns) TurnInstance_23C468(_rotTarget);
                SetTrajectoryRest60(_rotTarget); // Amedo
            }
            return;
        }
        float s = (float)t;
        if (_accelerated) s = AcceleratedFraction_232FD8((float)(World.Now - _packetStart));
        else if (_smooth) s = 3f * s * s - 2f * s * s * s;
        if (moves)
        {
            var step = _moveStart + (_moveTarget - _moveStart) * s;
            SmoothVelocity_20E828(s);
            if (_sqrTolerance != 0f && GameMath.DistanceSq(step, _moveTarget) < _sqrTolerance) // Amedo 2026-09-28
            {
                _translationDone = true;
                _rotationDone    = true;
                return;
            }
            MoveInstance_23C280(step);
        }
        if (_turns) TurnInstance_23C468(GameMath.Slerp_187100(s, _rotStart, _rotTarget)); // Amedo
    }

    // Amedo
    private void SmoothVelocity_20E828(float s)
    {
        var d = _moveTarget - _moveStart;
        float inv = GameMath.InverseLength(d);
        d = new Vector3(d.X * inv, d.Y * inv, d.Z * inv);
        float k = (s - s * s) * 6f;
        _velocity = new Vector3(d.X * k, d.Y * k, d.Z * k);
    }

    // Amedo
    private void FaceMotion_20DD38()
    {
        float inv = GameMath.InverseLength(_velocity);
        var d = new Vector3(_velocity.X * inv, _velocity.Y * inv, _velocity.Z * inv);
        if (!(1e-6f < d.X * d.X + d.Z * d.Z)) return;
        var q = GameMath.MatrixToQuat_186900(LookAlong_185920(d, Vector3.UnitY));
        var before = LocalRotation;
        if (q != before) SetLocalRotation(q);
        if (Tracing) Trace($"face motion: along {d} -> {q}");
    }

    // Amedo
    internal static Matrix4x4 LookAlong_185920(Vector3 d, Vector3 up)
    {
        var r = AxisFrame_185C08(up, 1);
        var local = new Vector3(d.X * r.M11 + d.Y * r.M12 + d.Z * r.M13,
                                d.X * r.M21 + d.Y * r.M22 + d.Z * r.M23,
                                d.X * r.M31 + d.Y * r.M32 + d.Z * r.M33);
        var m = Along_185A40(local) * r;
        m.M14 = m.M24 = m.M34 = 0f;
        m.M41 = m.M42 = m.M43 = 0f;
        m.M44 = 1f;
        return m;
    }

    // Amedo
    internal static Matrix4x4 AxisFrame_185C08(Vector3 axis, int index)
    {
        ReadOnlySpan<int> next = [1, 2, 0];
        Span<Vector3> rows = stackalloc Vector3[3];
        rows[index] = axis;
        int i = next[index];
        var a = GameMath.PerpendicularOf_18DA40(axis);
        float ia = GameMath.InverseLength(a);
        a = new Vector3(a.X * ia, a.Y * ia, a.Z * ia);
        rows[i] = a;
        var b = new Vector3(axis.Y * a.Z - axis.Z * a.Y, axis.Z * a.X - axis.X * a.Z, axis.X * a.Y - axis.Y * a.X);
        float ib = GameMath.InverseLength(b);
        rows[next[i]] = new Vector3(b.X * ib, b.Y * ib, b.Z * ib);
        return new Matrix4x4(rows[0].X, rows[0].Y, rows[0].Z, 0f,
                             rows[1].X, rows[1].Y, rows[1].Z, 0f,
                             rows[2].X, rows[2].Y, rows[2].Z, 0f,
                             0f, 0f, 0f, 1f);
    }
}
