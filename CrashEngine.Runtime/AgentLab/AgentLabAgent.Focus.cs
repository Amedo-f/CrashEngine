using System.Numerics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private const int SourceNone = 0xFF;
    private const int RefPlayer = 0xF2;
    private const int RefStored = 0xF6;
    private const int RefSlot11C = 0xF7;
    private const int RefSlot118 = 0xF8;
    private const int RefRouteNode = 0xFA;
    private const int RefFocusObject = 0xFB;
    private const int RefFocusPosition = 0xFC;
    private const int RefNextKey = 0xFD;
    private const int RefCurrentKey = 0xFE;

    private AgentLabAgent? _focusObject;
    private Vector3 _focusPosition;
    private bool _hasFocusObject;
    private bool _focusCamera; // Amedo
    private bool _hasFocusPosition;
    private Vector3 _storedPosition;
    private bool _hasStoredPosition;
    private AgentLabAgent? _slot118;
    private AgentLabAgent? _slot11C;
    private AgentLabAgent? _messageSource;
    private AgentLabAgent? _controller;

    private readonly List<Vector3> _keys = new();
    private int _keyCurrent;
    private int _keyFirst;
    private int _keyLast;
    private bool _endReached;

    public Vector3 GamePosition => LocalMatrix.Translation;

    public AgentLabAgent? FocusObject
    {
        get
        {
            if (!_hasFocusObject || _focusObject is null) return null;
            if (!_focusObject.Destroyed) return _focusObject;
            _hasFocusObject = _hasFocusPosition = false;
            _focusObject = null;
            return null;
        }
    }

    public Vector3? FocusPosition => _hasFocusPosition ? _focusPosition : null;
    public Vector3? StoredPosition => _hasStoredPosition ? _storedPosition : null;
    public IReadOnlyList<Vector3> Keys => _keys;
    public int CurrentKey => _keyCurrent;

    // Amedo
    private bool FocusIsCamera => _hasFocusObject && _focusCamera && _focusObject is null;

    // Amedo
    private Vector3? FocusObjectPosition => FocusIsCamera ? World.CameraObjectPosition?.Invoke() : FocusObject?.GamePosition;

    // Amedo
    private void FocusOnCamera_120068()
    {
        bool has = World.CameraObjectPosition?.Invoke() is not null;
        _focusObject = null;
        _focusCamera = has;
        if (has) _hasFocusObject = true;
        _hasFocusPosition = false;
    }

    private void FocusOn(AgentLabAgent target)
    {
        _focusCamera      = false; // Amedo
        _focusObject      = target;
        _hasFocusObject   = true;
        _hasFocusPosition = false;
    }

    private void FocusAt(Vector3 position)
    {
        _focusPosition    = position;
        _hasFocusPosition = true;
        _hasFocusObject   = false;
    }

    // Amedo
    private void FocusBesidePlayer_213970(float distance)
    {
        if (World.Player is not { } player) return;
        var me = GamePosition;
        var p = player.GamePosition;
        var pm = player.GameMatrix;
        var n = new Vector3(me.X - p.X, 0f, me.Z - p.Z);
        n *= GameMath.InverseLength(n);
        float side = pm.M11 * n.X + pm.M12 * n.Y + pm.M13 * n.Z;
        var c = new Vector3(n.Z, 0f, -n.X) * (side < 0f ? -distance : distance);
        FocusAt(c + me);
    }

    private void Store(Vector3 position)
    {
        _storedPosition    = position;
        _hasStoredPosition = true;
    }

    internal void ForgetReleased()
    {
        if (_messageSource is { Released: true }) _messageSource = null;
        if (_slot118 is { Released: true }) _slot118 = null;
        if (_slot11C is { Released: true }) _slot11C = null;
        if (_controller is { Released: true }) _controller = null;
        if (Requester is { Released: true }) Requester = null;
        if (_focusObject is { Released: true })
        {
            _focusObject = null;
            _hasFocusObject = _hasFocusPosition = false;
        }
    }

    private static AgentLabAgent? Alive(ref AgentLabAgent? slot)
    {
        if (slot is { Destroyed: true }) slot = null;
        return slot;
    }

    private AgentLabAgent? Participant(int index) => index < Participants.Count ? Participants[index] : null;

    private void LoadKeys()
    {
        _keys.Clear();
        var sec = World.LayoutItems(Instance.Section, Constants.LAYOUT_POSITIONS_SECTION);
        if (sec is not null)
            foreach (var id in Instance.Source.Positions)
                if (sec.ContainsItem(id) && sec.GetItem<PS2AnyPosition>(id) is { } p)
                    _keys.Add(new Vector3(p.Position.X, p.Position.Y, p.Position.Z));
        _keyCurrent    = 0;
        _keyFirst      = 0;
        _keyLast       = Math.Max(0, _keys.Count - 1);
        _endReached    = false;
    }

    private void NextKey()
    {
        _keyCurrent++;
        if (_keyLast >= _keyCurrent) return;
        _endReached = true;
        _keyCurrent    = _keyFirst;
    }

    private Vector3? Key(int index) => index >= 0 && index < _keys.Count ? _keys[index] : null;

    // Amedo 2026-10-01
    private bool AtLastKey_22A658
    {
        get
        {
            var owner = _controller ?? this;
            return owner._keyCurrent != 0xFF && owner._keyCurrent >= owner._keys.Count - 1;
        }
    }

    private int RandomBelow(int n) => World.Rng.GetRandom_18C018(n); // Amedo

    private void PrevKey()
    {
        _keyCurrent = (_keyCurrent - 1) & 0xFF;
        if (_keyCurrent != 0xFF) return;
        _endReached = true;
        _keyCurrent = _keyLast;
    }

    private void SetKey(uint w)
    {
        var owner = _controller ?? this;
        int count = owner._keys.Count;
        int key = (int)(w & 0xFF), lo = (int)((w >> 8) & 0xFF), hi = (int)((w >> 16) & 0xFF);
        int pick;
        if (key != SourceNone) pick = key;
        else if ((w & 0x08000000) != 0 && !(lo == 0xFF && hi == 0xFF))
        {
            if (lo == 0xFF) pick = RandomBelow(hi);
            else
            {
                int top = count - 1 < hi ? count - 1 : hi;
                pick = 0;
                if (count >= 2)
                    for (int tries = 0; tries < 64; tries++)
                    {
                        pick = lo + RandomBelow(top - lo + 1);
                        if ((w & 0x02000000) == 0 || pick != owner._keyCurrent) break;
                    }
            }
        }
        else if ((w & 0x04000000) != 0)
        {
            if (lo == 0xFF) lo = 0;
            var here = GamePosition;
            float best = 1e8f;
            pick = 0;
            for (int i = lo; i < count && i < hi; i++)
            {
                var kd = here - owner._keys[i]; // Amedo
                float d = kd.X * kd.X + kd.Y * kd.Y + kd.Z * kd.Z;
                if (d < best) { best = d; pick = i; }
            }
        }
        else if ((w & 0x10000000) != 0)
        {
            owner._endReached = false;
            owner._keyCurrent = (count - 1) & 0xFF;
            return;
        }
        else return;
        owner._endReached = pick == count;
        owner._keyCurrent = (pick < count ? pick : pick - count) & 0xFF;
    }

    private Vector3 SourcePosition(int participant, int key, int space, Vector3? offset, Vector3 unchanged = default)
    {
        Vector3 pos = Vector3.Zero;
        AgentLabAgent? target = null;
        bool found = false;

        if (participant != SourceNone)
        {
            target = participant == RefFocusObject ? FocusObject : Participant(participant);
            if (target is null && participant != RefFocusObject) return unchanged;
            if (participant == RefFocusObject && FocusIsCamera && FocusObjectPosition is { } camera) { pos = camera; found = true; }
        }
        else if (key != SourceNone)
        {
            switch (key)
            {
                case RefPlayer: target = World.Player; break;
                case 0xF4: World.Unimplemented("focus source", key, "[ctx+0x20] object"); break;
                case RefStored: if (_hasStoredPosition) { pos = _storedPosition; found = true; } break;
                case RefSlot11C: target = Alive(ref _slot11C); break;
                case RefSlot118: target = Alive(ref _slot118); break;
                case RefRouteNode: if (RouteNode(_routeIndex) is { } rn) { pos = rn.Position; found = true; } break;
                case RefFocusObject:
                    if (FocusIsCamera && FocusObjectPosition is { } cam) { pos = cam; found = true; }
                    else target = FocusObject;
                    break;
                case RefFocusPosition: if (FocusPosition is { } fp) { pos = fp; found = true; } break;
                case RefNextKey:
                    NextKey();
                    if (Key(_keyCurrent) is { } nk) { pos = nk; found = true; }
                    break;
                case RefCurrentKey: if (Key(_keyCurrent) is { } ck) { pos = ck; found = true; } break;
                default: if (key < 0xF2 && (_controller ?? this).Key(key) is { } k) { pos = k; found = true; } break; // Amedo 2026-09-28
            }
        }

        if (target is not null)
        {
            pos   = target.GamePosition;
            found = true;
        }

        if (!found)
            switch (space)
            {
                case 1: pos = _initMatrix.Translation; break;
                case 2: pos = GamePosition; break;
                case 7: if (_storedSpace is { } ss) pos = ss.Translation; break; // Amedo 2026-09-27
                case 4: // Amedo
                    _slotAC ??= target;
                    if (Alive(ref _slotAC) is { } tracked) pos = tracked.GamePosition;
                    break;
                case 5: if (_physics?.RideObject_80 is { } ride) pos = SupportMatrix(ride).Translation; break; // Amedo
            }

        if (offset is { } o)
            switch (space)
            {
                case 0: pos += o; break;
                case 1: pos += Vector3.TransformNormal(o, _initMatrix); break;
                case 2: pos += Vector3.TransformNormal(o, LocalMatrix); break;
                case 3: if (target is not null) pos += Vector3.TransformNormal(o, target.LocalMatrix); break;
                case 7: if (_storedSpace is { } so) pos += Vector3.TransformNormal(o, so); break;
                case 4: // Amedo
                    _slotAC ??= target;
                    if (Alive(ref _slotAC) is { } facing)
                    {
                        var d = facing.GamePosition - GamePosition;
                        pos += Vector3.TransformNormal(o, Along_185A40(d * GameMath.InverseLength(d)));
                    }
                    break;
                case 5: if (_physics?.RideObject_80 is { } rideOn) pos += Vector3.TransformNormal(o, SupportMatrix(rideOn)); break; // Amedo
            }
        return pos;
    }

    private Matrix4x4? _storedSpace; // Amedo 2026-09-27
    private AgentLabAgent? _slotAC; // Amedo
    private Quaternion _storedSpaceQuat = Quaternion.Identity; // Amedo

    private void StoreSpace(uint w)
    {
        int participant = (int)(w & 0xFF), key = (int)((w >> 8) & 0xFF);
        var from = key == SourceNone ? participant == SourceNone ? this : Participant(participant)
                 : key == RefFocusObject ? FocusObject : null;
        if (from is not null) StorePlace_23D168(from); // Amedo
    }

    // Amedo
    private void StorePlace_23D168(AgentLabAgent from)
    {
        _storedSpace = from.LocalMatrix;
        _storedSpaceQuat = from.LocalRotation;
    }

    // Amedo
    internal bool JointPosition_22DA00(int exit, out Vector3 position)
    {
        position = default;
        if (exit >= 0x3F || !HasExitNode_2952A0(exit)) return false; // Amedo
        position = ExitAnchor_293B70(exit).Translation;
        return true;
    }

    // Amedo
    private bool ExitPointPlace_23C900(int exit, out Vector3 position, out Vector3 direction)
    {
        position = direction = default;
        if (exit >= 0x3F || !HasExitNode_2952A0(exit)) return false; // Amedo
        var m = ExitAnchor_293B70(exit);
        position = m.Translation;
        direction = new Vector3(m.M31, m.M32, m.M33);
        return true;
    }

    private void SetFocusPosition(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        ulong flags = Arg(5) | ((ulong)Arg(6) << 32);
        int key = (int)((flags >> 8) & 0xFF);
        var offset = new Vector3(F(1), F(2), F(3));
        Vector3 pos = Vector3.Zero;

        if (key is RefRouteNode or 0xEF)
        {
            pos = RouteFocus(key == RefRouteNode, a);
            if ((flags & 0x200000) != 0) pos += offset;
        }
        else
        {
            if (F(11) != 0f)
            {
                if (_sensors110 is not null) pos = GamePosition + _sensorDir170 * F(11); // Amedo 2026-10-05
            }
            else if (F(12) != 0f)
            {
                pos = GamePosition;
                if (World.Player is { } player)
                {
                    var away = pos - player.GamePosition;
                    float lenSq = away.X * away.X + away.Y * away.Y + away.Z * away.Z; // Amedo
                    if (lenSq > 2.4999998e-09f) pos += away * (F(12) / MathF.Sqrt(lenSq));
                }
            }
            else if ((Arg(16) & 0xFF) == SourceNone)
                pos = SourcePosition((int)(flags & 0xFF), key, (int)((flags >> 16) & 0xF), (flags & 0x200000) != 0 ? offset : null);
            else
            {
                var target = (flags & 0x100000000) != 0 ? FocusObjectAwake_88() : this; // Amedo
                if (target is not null && target.JointPosition_22DA00((int)(Arg(16) & 0xFF), out var joint)) pos = joint;
            }

            if ((flags & 0x400000) != 0) pos = World.Rng.JitterVector_23C848(pos, F(7), F(8), 1f, 1f); // Amedo
        }

        if ((flags & 0x1C000000) != 0)
        {
            bool initial = (flags & 0x20000000) != 0;
            var frame = initial ? _initMatrix : LocalMatrix;
            var origin = frame.Translation;
            var delta = pos - origin;
            pos = origin;
            if ((flags & 0x04000000) != 0) { var ax = new Vector3(frame.M11, frame.M12, frame.M13); pos += ax * Dot3(delta, ax); } // Amedo
            if ((flags & 0x08000000) != 0) { var ay = new Vector3(frame.M21, frame.M22, frame.M23); pos += ay * Dot3(delta, ay); }
            if ((flags & 0x10000000) != 0) { var az = new Vector3(frame.M31, frame.M32, frame.M33); pos += az * Dot3(delta, az); }
            if ((flags & 0xA0000000) == 0xA0000000) pos = GamePosition + (pos - origin);
        }

        if ((flags & 0x200000000) != 0)
        {
            var m = LocalMatrix;
            var here = m.Translation;
            var gap = pos - here; // Amedo
            pos = here + new Vector3(m.M31, m.M32, m.M33) * MathF.Sqrt(gap.X * gap.X + gap.Y * gap.Y + gap.Z * gap.Z);
        }

        Trace($"SetFocusPosition source 0x{key:X2} ({FocusObject?.Entity.Name ?? "no focus"}) -> {pos} into {(flags >> 23) & 7}"); // Amedo 2026-10-05
        switch ((int)((flags >> 23) & 7))
        {
            case 0: FocusAt(pos); break;
            case 3: Store(pos); break;
        }
    }

    private void AddNoiseToFocus(PS2BehaviourCommand cmd)
    {
        var a = cmd.Arguments;
        if (a.Count < 5) return;
        float radius = FloatValueOf(a[1]);
        float F(int i) => BitConverter.UInt32BitsToSingle(a[i]);
        switch (a[0] & 7)
        {
            case 0:
                if (FocusPosition is { } fp) FocusAt(World.Rng.JitterVector_23C848(fp, radius, F(3), F(2), F(4))); // Amedo
                break;
            case 3:
                if (_hasStoredPosition) Store(World.Rng.JitterVector_23C848(_storedPosition, radius, F(3), F(2), F(4))); // Amedo
                break;
        }
    }

    // Amedo
    private const float FocusEps = 2.4999998e-09f;

    private Vector3 FlatDirectionToFocus(Vector3 focus)
    {
        var pos = GamePosition;
        var d = new Vector4(focus.X - pos.X, 0f, focus.Z - pos.Z, 1f);
        return new Vector3(d.X, d.Y, d.Z) * GameMath.InvLength_11C030(FocusEps, d);
    }

    private void SetFocusPositionOffset_2150C0(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint flags = Arg(7);
        var p = _hasFocusPosition ? _focusPosition : Vector3.Zero;
        if ((flags & 1) != 0) p.X = F(0);
        if ((flags & 2) != 0) p.Y = F(1);
        if ((flags & 4) != 0) p.Z = F(2);
        float dist = F(6);
        if (0f < dist)
        {
            var n = FlatDirectionToFocus(p);
            var pos = GamePosition;
            p = new Vector3(pos.X, p.Y, pos.Z) + n * dist;
            if ((flags & 8) != 0) p += Vector3.Cross(Vector3.UnitY, n) * -F(3);
            if ((flags & 0x10) != 0) p.Y += F(4);
            if ((flags & 0x20) != 0) p += n * -F(5);
        }
        else
        {
            if ((flags & 8) != 0) p.X += F(3);
            if ((flags & 0x10) != 0) p.Y += F(4);
            if ((flags & 0x20) != 0) p.Z += F(5);
        }
        FocusAt(p);
    }

    private void SetFocusPositionAtAngle_2154D8(uint distWord, uint angleWord)
    {
        var p = _hasFocusPosition ? _focusPosition : Vector3.Zero;
        var n = FlatDirectionToFocus(p);
        int angle = GameMath.FromValue_11BFA0(BitConverter.UInt32BitsToSingle(angleWord), 1);
        var r = GameMath.RotateAxis_18DE00(new Vector4(n, 1f), new Vector4(0f, 1f, 0f, 1f), angle, true);
        Store(p + new Vector3(r.X, r.Y, r.Z) * BitConverter.UInt32BitsToSingle(distWord));
    }

    private Vector3 WaterPoint140 => OwnCharacter?.WaterPoint ?? _waterPoint; // Amedo 2026-09-28

    private void PositionWarp(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint w = Arg(0);
        bool face = (w & 0x400000) != 0;
        var offset = (w & 0x200000) != 0 ? new Vector3(F(1), F(2), F(3)) : (Vector3?)null;
        var m = LocalMatrix;
        var pos = face ? m.Translation + new Vector3(m.M31, m.M32, m.M33) : m.Translation;
        if ((w & 0x1000000) != 0)
        {
            var at = WaterPoint140; // Amedo 2026-09-28
            if (MathF.Abs(at.X) <= 5e-5f && MathF.Abs(at.Y) <= 5e-5f && MathF.Abs(at.Z) <= 5e-5f)
                pos = m.Translation + new Vector3(0f, 1.5f, 0f);
            else
            {
                pos = at;
                if (offset is { } o) pos += Vector3.TransformNormal(o, m);
            }
            Trace($"PositionWarp from A+0x140 {at} -> {pos}");
        }
        else pos = SourcePosition((int)(w & 0xFF), (int)((w >> 8) & 0xFF), (int)((w >> 16) & 0xF), offset, pos);

        var before = LocalMatrix; // Amedo
        if (!face) { SetLocalPosition(pos); KeepWarp(pos); } // Amedo 2026-09-29
        else if ((w & 0x800000) != 0) SteerBodyTowards_22C330(pos, 1f, true, 0f);
        else SteerTowards_22CD68(pos, 1f, 0f, 90f);
        if (Tracing) Trace($"PositionWarp w 0x{w:X8} to {pos}, forward {new Vector3(before.M31, before.M32, before.M33)} -> {new Vector3(LocalMatrix.M31, LocalMatrix.M32, LocalMatrix.M33)}"); // Amedo
    }

    // Amedo 2026-09-29
    private void WarpAgent_216B70(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint w = Arg(0);
        int targetCode = (int)(w >> 24), sourceCode = (int)(Arg(1) & 0xFF);
        bool face = (w & 0x400000) != 0;
        var target = targetCode == 0xFF ? this : targetCode < 0xDE ? Participant(targetCode) : CodeObject(targetCode);
        if (target is null) return;
        var m = LocalMatrix;
        var pos = face ? m.Translation + new Vector3(m.M31, m.M32, m.M33) : m.Translation;
        if (sourceCode == 0xFF)
        {
            var offset = (w & 0x200000) != 0 ? new Vector3(F(5), F(6), F(7)) : (Vector3?)null;
            pos = SourcePosition((int)(w & 0xFF), (int)((w >> 8) & 0xFF), (int)((w >> 16) & 0xF), offset, pos);
        }
        else if ((sourceCode < 0xDE ? Participant(sourceCode) : CodeObject(sourceCode)) is { } from) pos = from.LocalMatrix.Translation;
        else if (CodePosition(sourceCode, out var at)) pos = at;
        Trace($"WarpAgent {target.Entity.Name} {(face ? "faces" : "to")} {pos}");
        if (!face) { target.SetLocalPosition(pos); target.KeepWarp(pos); } // Amedo 2026-09-29
        else if ((w & 0x800000) != 0) target.SteerBodyTowards_22C330(pos, 1f, true, 0f);
        else target.SteerTowards_22CD68(pos, 1f, 0f, 90f);
        if (target.OwnCharacter is { } pc) pc.Warped_13FAE0(); // Amedo 2026-09-29
    }

    // Amedo
    private void StrafeTowardsTarget_218278(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint w = Arg(0);
        var target = Vector3.Zero;
        switch ((int)(w & 0xFF))
        {
            case RefSlot118:
                if (Alive(ref _slot118) is { } slot) target = slot.GamePosition;
                break;
            case RefStored:
                if (_hasStoredPosition) target = _storedPosition;
                break;
            case RefFocusObject:
                if (FocusObjectPosition is { } focused) target = focused;
                else if (_hasFocusPosition) target = _focusPosition;
                break;
            case RefFocusPosition:
                if (_hasFocusPosition) target = _focusPosition;
                break;
        }
        float dt = StepDt;
        var me = LocalMatrix.Translation;
        var d = target - me;
        Vector3 axis, origin = Vector3.Zero;
        switch ((int)((w >> 8) & 0xF))
        {
            case 1:
                axis = new Vector3(_initMatrix.M11, _initMatrix.M12, _initMatrix.M13);
                origin = _initMatrix.Translation;
                break;
            case 2:
                var m = LocalMatrix;
                axis = new Vector3(m.M11, m.M12, m.M13);
                break;
            default:
                axis = d;
                break;
        }
        var p = axis * (d.X * axis.X + d.Y * axis.Y + d.Z * axis.Z);
        if (!(0.1f < MathF.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z))) return;
        var pos = me + p * GameMath.InverseLength(p) * F(1) * dt;
        float limit = F(2);
        if (!(limit <= 0f))
        {
            var o = pos - origin;
            if (!(MathF.Abs(axis.X * o.X + axis.Y * o.Y + axis.Z * o.Z) <= limit)) return;
        }
        if (pos != me) SetLocalPosition(pos);
        if ((w & 0x1000) != 0) SteerTowards_22CD68(target, 1f, 0f, 90f);
        if (_wobble is { } wobble) wobble.RestPose = pos;
    }

    private static Quaternion RotationOf(Matrix4x4 m) // Amedo 2026-09-27
    {
        m.Translation = Vector3.Zero;
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
    }

    private void RotationWarp(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        RotateWarp(Arg(0), new Quaternion(F(1), F(2), F(3), F(4))); // Amedo
    }

    // Amedo
    private void RotateAgent_217A38(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        int code = (int)(Arg(1) & 0xFF);
        var target = code == 0xFF ? this : code < 0xDE ? Participant(code) : CodeObject(code);
        if (target is null) return;
        target.RotateWarp(Arg(0), new Quaternion(F(5), F(6), F(7), F(8)));
    }

    private void RotateWarp(uint w, Quaternion turn) // Amedo
    {
        bool relative = (w & 0x200000) != 0;
        Quaternion? q = ((w >> 16) & 0xF) switch
        {
            0 => turn,
            1 => relative ? RotationOf(_initMatrix) * turn : RotationOf(_initMatrix),
            2 => relative ? LocalRotation * turn : null,
            7 => _storedSpace is { } s ? relative ? RotationOf(s) * turn : RotationOf(s) : null,
            _ => null,
        };
        if (q is { } r && r != LocalRotation) SetLocalRotation(r);
        if ((w & 0x400000) == 0) return;
        var m = LocalMatrix;
        SetFacing(Unit(new Vector3(m.M31, m.M32, m.M33)));
    }

    private void SetKeyNearestPlayer(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        if (World.Player is not { } player || _keys.Count == 0) return;
        float range = BitConverter.UInt32BitsToSingle(Arg(0));
        if (BitConverter.UInt32BitsToSingle(Arg(1)) != 0f) World.Unimplemented("command", 524, "SetKeyNearestPlayer velocity lead");
        uint w = Arg(2);
        bool includeCurrent = (w & 0xFF) != 0;
        int first = (int)((w >> 8) & 0xFF), last = (int)((w >> 16) & 0xFF);
        var target = player.GamePosition;
        int best = -1;
        float bestSq = 1e30f;
        for (int i = 0; i < _keys.Count; i++)
        {
            if (i < first || i > last || (!includeCurrent && i == _keyCurrent)) continue;
            float dSq = GameMath.DistanceSq(_keys[i], target);
            if (range != 0f)
            {
                if (dSq >= range) continue;
                dSq = GameMath.DistanceSq(_keys[i], GamePosition);
            }
            if (dSq >= bestSq) continue;
            bestSq = dSq;
            best   = i;
        }
        if (best < 0) return;
        _keyCurrent = best;
        _endReached = false;
    }

    private AgentLabAgent? AgentRef(int sel)
    {
        switch (sel)
        {
            case 0xDE:
                World.Unimplemented("agent ref", sel, "body relation");
                return null;
            case 0xDF: return _spawnedBy;
            case RefSlot11C: return Alive(ref _slot11C);
            case RefSlot118: return Alive(ref _slot118);
            case RefFocusObject: return FocusObject;
            default: return Participant(sel);
        }
    }

    private void AssignAgent(AgentLabAgent? target, uint mode)
    {
        if (target is null) return;
        switch (mode & 7)
        {
            case 0: FocusOn(target); break;
            case 1: _slot118 = target; break;
            case 2: _slot11C = target; break;
            case 3: Store(target.GamePosition); break;
        }
    }

    private void AssignFromLinks(uint w) // Amedo 2026-09-27
    {
        AgentLabAgent? target;
        if ((w & 0x100) != 0)
        {
            var owner = (w & 0x400) != 0 ? FocusObject : (w & 0x800) != 0 ? Alive(ref _slot118) : _controller ?? this;
            if (owner is null) return;
            int index;
            if ((w & 0x200) != 0) index = owner._linkCursor; // Amedo
            else if ((w & 0x8000) != 0) index = (owner.LinkCount - 1) & 0xFF;
            else if ((index = (int)(w & 0xFF)) >= owner.LinkCount) return;
            target = owner.LinkAt(index);
        }
        else
        {
            int code = (int)(w & 0xFF);
            target = code < 0xDE ? Participant(code) : CodeObject(code);
        }
        if (target is null) return;
        switch ((w >> 12) & 7)
        {
            case 0: FocusOn(target); break;
            case 1: _slot118 = target; break;
            case 3: Store(target.GamePosition); break;
        }
    }

    private void SetController(uint arg0, bool set) // Amedo 2026-09-27
    {
        int code = (int)(arg0 & 0xFF);
        if (code == SourceNone)
        {
            if (!set && (arg0 & 0x100) != 0) _controller = null;
            return;
        }
        if (CodeObject(code) is { } target) target._controller = set ? this : null;
    }

    private void SetFocusToKey(uint w)
    {
        var owner = _controller ?? this; // Amedo 2026-09-28
        if ((w & 0x1000) != 0) owner = FocusObject ?? owner;
        else if ((w & 0x2000) != 0) owner = Alive(ref _slot118) ?? owner;

        int key = (int)(w & 0xFF);
        if (key == RefNextKey) owner.NextKey();
        if ((key is RefNextKey or RefCurrentKey ? owner.Key(owner._keyCurrent) : owner.Key(key)) is not { } pos) return;

        switch ((w >> 9) & 7)
        {
            case 0: FocusAt(pos); break;
            case 3: Store(pos); break;
        }
    }

    private void RequestFocus(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        ulong sel = Arg(7) | ((ulong)Arg(8) << 32);
        ulong opt = Arg(9) | ((ulong)Arg(10) << 32);
        int participant = (sel & 0xF000000000) == 0xF000000000 ? SourceNone : (int)((sel >> 36) & 0xF);
        var offset = new Vector3(BitConverter.UInt32BitsToSingle(Arg(1)), BitConverter.UInt32BitsToSingle(Arg(2)), BitConverter.UInt32BitsToSingle(Arg(3)));
        var center = SourcePosition(participant, (int)((Arg(8) >> 8) & 0xFF), (int)((sel >> 32) & 0xF),
                                    (sel & (1UL << 52)) != 0 ? offset : null);
        float radius = BitConverter.UInt32BitsToSingle(Arg(11));

        int mode = (int)(opt & 3);
        AgentLabAgent? current = mode switch
        {
            0 => FocusObject,
            1 => Alive(ref _slot118),
            2 => Alive(ref _slot11C),
            _ => null,
        };

        int count = (int)((Arg(8) >> 16) & 7);
        int heldCount = (int)((opt >> 3) & 3); // Amedo
        bool nearest = (Arg(8) & 0x80000) != 0; // Amedo 2026-10-05

        // Amedo 2026-09-30
        uint require = (opt & 4) != 0 ? 0u : 8u, exclude = 1u;
        switch ((int)((sel >> 55) & 0xF))
        {
            case 1: case 7: require |= 0x80; break;
            case 2: case 6: case 8: exclude = 0x81; break;
            case 3: case 5: require |= 0x40; break;
            case 4: exclude = 0x41; break;
        }
        switch ((int)((sel >> 59) & 3))
        {
            case 1: require |= 0x100; break;
            case 2: exclude |= 0x100; break;
        }
        if ((opt & 0x40) != 0) require |= 0x400;
        uint components = (uint)(opt >> 32);

        AgentLabAgent? best = null;
        float bestSq = 1e30f;
        bool picked = false;
        var matches = (Arg(8) & 0x400000) != 0 && heldCount == 0 ? new List<AgentLabAgent>() : null; // Amedo 2026-10-05
        foreach (var o in World.QuerySphere(center, radius, components, exclude, this, true, require, true, 0x80)) // Amedo
        {
            if (heldCount != 0 && count != 0) // Amedo
            {
                if (o is AgentLabAgent held && MatchesObject(a, held.Instance.ObjectId, heldCount, 7)
                    && held.AttachedTo is { } holder && MatchesObject(a, holder.Instance.ObjectId, count))
                {
                    best = holder;
                    break;
                }
                continue;
            }
            if (picked) break;
            if (o is not AgentLabAgent other) continue;
            float dSq = GameMath.DistanceSq(center, other.GamePosition);
            if (!nearest) // Amedo 2026-10-05
            {
                if (count > 0 && !MatchesObject(a, other.Instance.ObjectId, count)) continue;
                if (matches is not null && count > 0) { if (matches.Count < 0x80) matches.Add(other); continue; } // Amedo
                best = other;
                picked = true;
                continue;
            }
            if ((opt & 0x20) == 0 && other == current) continue;
            if (count > 0 && !MatchesObject(a, other.Instance.ObjectId, count)) continue;
            if (dSq >= bestSq) continue;
            best = other;
            bestSq = dSq;
        }
        if (matches is { Count: > 0 }) best = matches[World.Rng.GetRandom_18C018(matches.Count)]; // Amedo
        Trace($"RequestFocus require 0x{require:X} exclude 0x{exclude:X} -> {best?.Entity.Name ?? "none"}"); // Amedo 2026-09-30

        if (best is null)
        {
            switch (mode)
            {
                case 0: _hasFocusObject = _hasFocusPosition = false; break;
                case 1: _slot118 = null; break;
                case 2: _slot11C = null; break;
            }
            return;
        }
        switch (mode)
        {
            case 0: FocusOn(best); break;
            case 1: _slot118 = best; break;
            case 2: _slot11C = best; break;
        }
        if ((Arg(8) & 0x40000000) != 0) _slot11C = best;
        if ((Arg(8) & 0x20000000) != 0) best._busy = true; // Amedo 2026-09-30
    }

    private static bool MatchesObject(List<uint> a, ushort objectId, int count, int first = 5) // Amedo
    {
        for (int i = 0; i < count; i++)
        {
            int word = first + i / 2;
            if (word >= a.Count) return false;
            if ((ushort)(a[word] >> (16 * (i & 1))) == objectId) return true;
        }
        return false;
    }

    private void CopyTarget(uint w)
    {
        int src = (int)(w & 0xFF), dst = (int)((w >> 8) & 0xFF), who = (int)((w >> 16) & 0xFF);
        var from = who == 0xF0 ? this : CodeObject(who);
        if (from is null) return;

        var obj = from.CodeObject(src);
        Vector3 pos = default;
        bool found = obj is not null;
        if (obj is not null) pos = obj.GamePosition;
        else found = from.CodePosition(src, out pos);
        if (!found) return;
        if (!SetCodeObject(dst, obj)) SetCodePosition(dst, pos);
    }

    private AgentLabAgent? CodeObject(int code)
    {
        switch (code)
        {
            case 0xF0: return this;
            case RefPlayer: return World.Player;
            case RefSlot11C: return _slot11C;
            case RefSlot118: return _slot118;
            case RefFocusObject: return FocusObject;
            case 0xDF: return _spawnedBy;
            case 0xF5:
                World.Unimplemented("agent code", code, "object");
                return null;
        }
        return null;
    }

    private bool CodePosition(int code, out Vector3 pos)
    {
        pos = default;
        switch (code)
        {
            case RefStored:
                pos = _storedPosition;
                return _hasStoredPosition;
            case RefSlot11C:
                if (Alive(ref _slot11C) is null || _slot118 is not { } bugged) return false;
                pos = bugged.GamePosition;
                return true;
            case RefSlot118:
                if (Alive(ref _slot118) is not { } s) return false;
                pos = s.GamePosition;
                return true;
            case RefFocusObject:
                if (FocusObject is not { } f) return false;
                pos = f.GamePosition;
                return true;
            case RefFocusPosition:
                pos = _focusPosition;
                return _hasFocusPosition;
            case 0xF5:
                World.Unimplemented("agent code", code, "position");
                return false;
        }
        return false;
    }

    private bool SetCodeObject(int code, AgentLabAgent? obj)
    {
        switch (code)
        {
            case RefFocusObject:
                _focusObject = obj;
                if (obj is not null) _hasFocusObject = true;
                _hasFocusPosition = false;
                return true;
            case RefSlot118: _slot118 = obj; return true;
            case RefSlot11C: _slot11C = obj; return true;
            case 0xF5:
                World.Unimplemented("agent code", code, "set object");
                return false;
        }
        return false;
    }

    private void SetCodePosition(int code, Vector3 pos)
    {
        switch (code)
        {
            case RefFocusPosition: FocusAt(pos); break;
            case RefStored: Store(pos); break;
            case 0xF5:
            case RefSlot11C:
            case RefSlot118:
            case RefFocusObject:
                World.Unimplemented("agent code", code, "move object");
                break;
        }
    }

    private float MeToFocusSqrDist()
    {
        Vector3 focus;
        if (_hasFocusObject)
        {
            if (FocusObjectPosition is not { } f) return 0f; // Amedo
            focus = f;
        }
        else if (FocusPosition is { } p) focus = p;
        else return 0f;
        return GameMath.DistanceSq(GamePosition, focus);
    }

    // Amedo
    private float HeightAboveFocus_226B30()
    {
        Vector3 focus;
        if (_hasFocusObject)
        {
            if (FocusObjectPosition is not { } f) return 0f;
            focus = f;
        }
        else if (FocusPosition is { } p) focus = p;
        else return 0f;
        return GamePosition.Y - focus.Y;
    }

    // Amedo
    private void ClearAssignment_23DF90(int mode)
    {
        switch (mode)
        {
            case 0: _hasFocusObject = _hasFocusPosition = false; break;
            case 1: _slot118 = null; break;
            case 2: _slot11C = null; break;
            case 3: _hasStoredPosition = false; break;
        }
    }

    // Amedo 2026-10-05
    private void CopyFromMessenger_2144F0(uint arg0)
    {
        int mode = (int)(arg0 & 7);
        if (_messageSource is not { } other)
        {
            ClearAssignment_23DF90(mode); // Amedo
            return;
        }
        switch (mode)
        {
            case 0:
                _focusObject = other._focusObject;
                _focusCamera = other._focusCamera; // Amedo
                _focusPosition = other._focusPosition;
                _hasFocusObject = other._hasFocusObject;
                _hasFocusPosition = !other._hasFocusObject && other._hasFocusPosition;
                break;
            case 1: _slot118 = Alive(ref other._slot118); break;
            case 2: _slot11C = other.CachedTemplate(); break;
            case 3: if (other._hasStoredPosition) Store(other._storedPosition); break;
        }
    }

    // Amedo 2026-10-05
    private void SetFocusCounter_212AA8(uint arg0, uint from, uint valueWord)
    {
        if (!_hasFocusObject || _focusObject is null) return;
        if (_focusObject.Destroyed)
        {
            _focusObject = null;
            _hasFocusObject = _hasFocusPosition = false;
            return;
        }
        int value = from == 0xFF ? ValueOf(valueWord) : Counters.GetValueOrDefault((int)from);
        _focusObject.Counters[(int)(arg0 & 0xFF)] = value & 0xFF;
    }

    // Amedo
    private void ModifyFocusCounter_212870(uint arg0, uint valueWord)
    {
        if (!_hasFocusObject || _focusObject is null) return;
        if (_focusObject.Destroyed)
        {
            _focusObject = null;
            _hasFocusObject = _hasFocusPosition = false;
            return;
        }
        int index = (int)(arg0 & 0xFF);
        int value = _focusObject.Counters.GetValueOrDefault(index) + RootController().IntVariable(valueWord);
        _focusObject.Counters[index] = value >= 0x100 ? 0xFF : value < 0 ? 0 : value;
    }
}
