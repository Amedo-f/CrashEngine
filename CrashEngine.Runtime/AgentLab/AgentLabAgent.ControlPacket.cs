using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-26
public sealed partial class AgentLabAgent
{
    private const int PacketDelay = 10;
    private const int PacketRandRange = 15;

    private bool _packetApplied;
    private bool _packetRunning;
    private double _packetStart;
    private double _packetDeadline = -1;

    private void ApplyPacket(TwinBehaviourControlPacket packet)
    {
        ControlPacket  = packet;
        _packetApplied = true;
    }

    private void EndPacket()
    {
        ControlPacket  = null;
        _packetApplied = false;
        _packetRunning = false;
        _moving        = false;
    }

    // Amedo
    private void PacketEnded_23D8B0()
    {
        if (_otherChannel is { Behaviour: not null, ControlPacket: not null }) return;
        _moveTarget     = System.Numerics.Vector3.Zero;
        _followOffset40 = System.Numerics.Vector3.Zero;
        _rotTarget      = default;
    }

    private void StartPacket()
    {
        var packet = ControlPacket!;
        HoldTrajectory_23B140(); // Amedo
        var startPosition = LocalMatrix.Translation; // Amedo
        var startRotation = LocalRotation; // Amedo
        _packetRunning  = true;
        _packetStart    = World.Now;
        _packetDeadline = -1;
        if (PacketFloat(packet, PacketDelay, out float delay))
        {
            _packetDeadline = _packetStart + delay;
            if (PacketFloat(packet, PacketRandRange, out float range))
                _packetDeadline += World.Rng.GetRandFloat_18C0B8() * range; // Amedo
        }
        if (!ResolvePacketSources(packet))
        {
            Trace("packet ended: target object missing");
            EndPacket();
            return;
        }
        StartMotion(packet);
        if (packet.Rotates) SetTrajectoryRest60(startRotation); // Amedo
        if (packet.Translates) SetTrajectoryRest50(startPosition); // Amedo
        Trace($"packet start: motion {packet.Motion}, deadline {(_packetDeadline < 0 ? "none" : $"{_packetDeadline - _packetStart:0.00}s")}" +
              (_moving ? $", move {(_translationDone ? "-" : $"{_moveStart} -> {_moveTarget}")} turn {(_rotationDone ? "-" : $"{_rotStart} -> {_rotTarget}")} rate {_moveRate:0.###}/s" : ""));
    }

    private TwinBehaviourControlPacket? PacketFinished()
    {
        var packet = ControlPacket!;
        bool timeUp = !packet.Stalls && _packetDeadline >= 0 && World.Now >= _packetDeadline;
        if (!MotionComplete && !timeUp) return null;
        _moving = false;
        Trace("packet finished");
        ControlPacket  = null;
        _packetRunning = false;
        return packet;
    }

    private bool PacketFloat(TwinBehaviourControlPacket packet, int field, out float value)
    {
        value = 0f;
        if (field >= packet.Bytes.Count) return false;
        byte b = packet.Bytes[field];
        if (b == 0xFF) return false;
        if ((b & 0x80) != 0) { value = RootController().FloatVar(b & 0x7F); return true; } // Amedo
        if (b >= packet.Floats.Count) return false;
        value = BitConverter.UInt32BitsToSingle(packet.Floats[b]);
        return true;
    }

    public float[]? FloatParams { get; set; } // Amedo 2026-09-27

    private float FloatVar(int index)
    {
        if (FloatParams is { } runtime) return index < runtime.Length ? runtime[index] : 0f;
        var p = Instance.Source.ParamList2;
        return p is not null && index < p.Count ? p[index] : 0f;
    }
}
