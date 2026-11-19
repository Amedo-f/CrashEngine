using System.Numerics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-30
public sealed partial class AgentLabAgent
{
    private sealed class ChannelState
    {
        public List<Level> Levels = new();
        public int ActiveDepth;
        public PS2BehaviourGraph? Behaviour;
        public TwinBehaviourControlPacket? ControlPacket;
        public int Priority = -1;
        public bool Restartable;
        public object? StartedBy;
        public int StartedPriority;
        public List<AgentLabAgent?> Participants = new();
        public AgentLabAgent? Requester;
        public (PS2BehaviourGraph Graph, TwinBehaviourStarter? Starter, AgentLabAgent? Originator, AgentLabAgent? Requester)? Pending;
        public bool PendingDefault;
        public int Body = -1; // Amedo 2026-11-18
        public int LockDepth = 0xFF;
        public int Timer5C;
        public bool PacketApplied, PacketRunning;
        public double PacketStart, PacketDeadline = -1;
        public Vector3? SrcKey, SrcRoute, SrcSpecial;
        public AgentLabAgent? SrcTarget;
        public bool Moving, TranslationDone, RotationDone, Smooth;
        public Vector3 MoveStart, MoveTarget;
        public double MoveRate;
        public Quaternion RotStart, RotTarget;
        public bool Turns; // Amedo
        public bool Chasing, ChaseAir, ChaseClimb; // Amedo
        public float ChaseSpeed, ChaseCurrentSpeed, HomePower = 1f, Banking, Damping, MaxBank = 80f, SqrTolerance;
        public bool Springing, SpringNoOvershoot; // Amedo
        public float SpringK, SpringDamping;
        public Vector3 SpringForce;
    }

    private ChannelState? _otherChannel;
    private int _channel;

    public int Channel => _channel;

    private void Exchange(ChannelState s)
    {
        (Levels, s.Levels) = (s.Levels, Levels);
        (ActiveDepth, s.ActiveDepth) = (s.ActiveDepth, ActiveDepth);
        (Behaviour, s.Behaviour) = (s.Behaviour, Behaviour);
        (ControlPacket, s.ControlPacket) = (s.ControlPacket, ControlPacket);
        (Priority, s.Priority) = (s.Priority, Priority);
        (Restartable, s.Restartable) = (s.Restartable, Restartable);
        (_startedBy, s.StartedBy) = (s.StartedBy, _startedBy);
        (_startedPriority, s.StartedPriority) = (s.StartedPriority, _startedPriority);
        (Participants, s.Participants) = (s.Participants, Participants);
        (Requester, s.Requester) = (s.Requester, Requester);
        (_pendingBehaviour, s.Pending) = (s.Pending, _pendingBehaviour);
        (_pendingDefault, s.PendingDefault) = (s.PendingDefault, _pendingDefault);
        (_body, s.Body) = (s.Body, _body);
        (_lockDepth, s.LockDepth) = (s.LockDepth, _lockDepth);
        (_timer5C, s.Timer5C) = (s.Timer5C, _timer5C);
        (_packetApplied, s.PacketApplied) = (s.PacketApplied, _packetApplied);
        (_packetRunning, s.PacketRunning) = (s.PacketRunning, _packetRunning);
        (_packetStart, s.PacketStart) = (s.PacketStart, _packetStart);
        (_packetDeadline, s.PacketDeadline) = (s.PacketDeadline, _packetDeadline);
        (_srcKey, s.SrcKey) = (s.SrcKey, _srcKey);
        (_srcRoute, s.SrcRoute) = (s.SrcRoute, _srcRoute);
        (_srcTarget, s.SrcTarget) = (s.SrcTarget, _srcTarget);
        (_srcSpecial, s.SrcSpecial) = (s.SrcSpecial, _srcSpecial);
        (_moving, s.Moving) = (s.Moving, _moving);
        (_translationDone, s.TranslationDone) = (s.TranslationDone, _translationDone);
        (_rotationDone, s.RotationDone) = (s.RotationDone, _rotationDone);
        (_smooth, s.Smooth) = (s.Smooth, _smooth);
        (_moveStart, s.MoveStart) = (s.MoveStart, _moveStart);
        (_moveTarget, s.MoveTarget) = (s.MoveTarget, _moveTarget);
        (_moveRate, s.MoveRate) = (s.MoveRate, _moveRate);
        (_rotStart, s.RotStart) = (s.RotStart, _rotStart);
        (_rotTarget, s.RotTarget) = (s.RotTarget, _rotTarget);
        (_turns, s.Turns) = (s.Turns, _turns); // Amedo
        (_chasing, s.Chasing) = (s.Chasing, _chasing);
        (_chaseAir, s.ChaseAir) = (s.ChaseAir, _chaseAir);
        (_chaseClimb, s.ChaseClimb) = (s.ChaseClimb, _chaseClimb); // Amedo
        (_chaseSpeed, s.ChaseSpeed) = (s.ChaseSpeed, _chaseSpeed);
        (_chaseCurrentSpeed, s.ChaseCurrentSpeed) = (s.ChaseCurrentSpeed, _chaseCurrentSpeed);
        (_homePower, s.HomePower) = (s.HomePower, _homePower);
        (_banking, s.Banking) = (s.Banking, _banking);
        (_damping, s.Damping) = (s.Damping, _damping);
        (_maxBank, s.MaxBank) = (s.MaxBank, _maxBank);
        (_sqrTolerance, s.SqrTolerance) = (s.SqrTolerance, _sqrTolerance);
        (_springing, s.Springing) = (s.Springing, _springing); // Amedo
        (_springNoOvershoot, s.SpringNoOvershoot) = (s.SpringNoOvershoot, _springNoOvershoot);
        (_springK, s.SpringK) = (s.SpringK, _springK);
        (_springDamping, s.SpringDamping) = (s.SpringDamping, _springDamping);
        (_springForce, s.SpringForce) = (s.SpringForce, _springForce);
        _channel ^= 1;
    }

    // Amedo
    internal void StopRunners_23E498()
    {
        var entered = EnterChannel(0); // Amedo
        Stop_20C4A8();
        _pendingBehaviour = null;
        _pendingDefault = false;
        LeaveChannel(entered);
        if (_otherChannel is { } other) // Amedo
        {
            var second = EnterChannel(1);
            Stop_20C4A8();
            LeaveChannel(second);
        }
        _otherChannel = null;
        _channel = 0; // Amedo
    }

    private ChannelState? EnterChannel(int channel)
    {
        if (channel == _channel) return null;
        var other = _otherChannel ??= new ChannelState();
        Exchange(other);
        return other;
    }

    private void LeaveChannel(ChannelState? entered)
    {
        if (entered is not null) Exchange(entered);
    }

    private void StepChannel1()
    {
        if (_channel != 0 || _otherChannel is not { } c || (c.Behaviour is null && c.Pending is null)) return;
        Exchange(c);
        try { RunContext(); }
        finally { Exchange(c); }
    }
}
