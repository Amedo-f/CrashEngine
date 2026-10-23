using System.Numerics;
using CrashEngine.Runtime.Physics;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed class LayoutTrigger
{
    public const ulong FirstEnterFlag = 0x10000;
    public const ulong EnterFlag      = 0x20000;
    public const ulong InsideFlag     = 0x40000;
    public const ulong ExitFlag       = 0x80000;
    public const ulong CharacterFlag  = 0x100000;
    public const ulong OccupiedFlag   = 0x200000;

    private static readonly (uint Bit, uint Mask)[] ActivatorMasks =
    [
        (0x1, 0x1000), (0x4, 0x2000), (0x8, 0x8000), (0x2, 0x4000), (0x10, 0x10000),
        (0x20, 0x20000), (0x80, 0x80000), (0x40, 0x40000), (0x100, 0x100000), (0x200, 0x200000),
    ];

    private readonly AgentLabWorld _world;
    private readonly List<AgentLabAgent> _inside = new();
    private AgentLabAgent?[]? _targets;

    public PS2AnyTrigger Source { get; }
    public BaseTwinSection Instances { get; }
    public int Layout { get; }
    public ulong Flags;
    public uint Mask { get; }
    public int Interval { get; }
    public bool AnyActivator { get; }
    public ushort[] Messages { get; } = new ushort[4];
    public ConvexHull Hull { get; }
    public Matrix4x4 Matrix { get; }
    public (Vector3 Min, Vector3 Max) Bounds { get; }
    public int Index { get; init; }
    public int LastCheck;
    public IReadOnlyList<AgentLabAgent> Inside => _inside;
    public Vector4 Force = new(0f, 0f, 0f, 1f); // Amedo 2026-09-29
    public bool ForceOn;

    public WorldChunk? Chunk { get; init; } // Amedo 2026-09-28
    private TriggerCell? _cell; // Amedo
    internal TriggerCell Cell => _cell ??= new TriggerCell(this);

    public LayoutTrigger(AgentLabWorld world, PS2AnyTrigger source, BaseTwinSection instances, int layout)
    {
        _world    = world;
        Source    = source;
        Instances = instances;
        Layout    = layout;
        var t = source.Trigger;
        uint header = t.Header;
        Flags = header & 0xFF;
        if (((header >> 12) & 1) != 0) Flags |= CharacterFlag;
        if (((header >> 11) & 1) != 0) { Flags |= FirstEnterFlag; Messages[0] = source.TriggerMessages[0]; }
        if (((header >> 8) & 1) != 0)  { Flags |= EnterFlag;      Messages[1] = source.TriggerMessages[1]; }
        if (((header >> 9) & 1) != 0)  { Flags |= InsideFlag;     Messages[2] = source.TriggerMessages[2]; }
        if (((header >> 10) & 1) != 0) { Flags |= ExitFlag;       Messages[3] = source.TriggerMessages[3]; }

        uint act = (uint)t.ObjectActivatorMask;
        if (act == 1 && (Flags & ExitFlag) == 0)
        {
            Flags |= CharacterFlag;
            AnyActivator = false;
        }
        else if (act == 0)
        {
            if ((Flags & ExitFlag) == 0) Flags |= CharacterFlag;
            AnyActivator = true;
        }
        else
        {
            foreach (var (bit, mask) in ActivatorMasks)
                if ((act & bit) != 0) Mask |= mask;
        }

        Interval = (int)(t.UnkFloat * 9000f);
        var scale = new Vector3(t.Scale.X, t.Scale.Y, t.Scale.Z);
        Hull   = ConvexHull.Box(-scale, scale);
        Matrix = Matrix4x4.CreateFromQuaternion(new Quaternion(t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Rotation.W))
               * Matrix4x4.CreateTranslation(t.Position.X, t.Position.Y, t.Position.Z);
        Bounds = Hull.Bounds(Matrix);
    }

    // Amedo 2026-10-06
    public static ulong BaseFlags_1F5898(uint header, uint act)
    {
        ulong flags = header & 0xFF;
        if (((header >> 12) & 1) != 0) flags |= CharacterFlag;
        if (((header >> 11) & 1) != 0) flags |= FirstEnterFlag;
        if (((header >> 8) & 1) != 0) flags |= EnterFlag;
        if (((header >> 9) & 1) != 0) flags |= InsideFlag;
        if (((header >> 10) & 1) != 0) flags |= ExitFlag;
        if (act is 0 or 1 && (flags & ExitFlag) == 0) flags |= CharacterFlag;
        return flags;
    }

    public string Name => $"trigger L{Layout}:0x{Source.GetID():X}";

    public bool Overlaps(ConvexHull hull, Matrix4x4 m)
    {
        var (min, max) = hull.Bounds(m);
        return CollisionWorld.Overlap(min, max, Bounds.Min, Bounds.Max) && ConvexHull.Overlaps(hull, m, Hull, Matrix);
    }

    public void Process(List<AgentLabAgent> now)
    {
        now.Sort(static (a, b) => a.Phase.CompareTo(b.Phase));
        int i = 0, j = 0;
        while (i < _inside.Count && j < now.Count)
        {
            var o = _inside[i];
            var n = now[j];
            if (ReferenceEquals(o, n))
            {
                if ((Flags & InsideFlag) != 0) Send(2, n);
                i++;
                j++;
            }
            else if (o.Phase < n.Phase)
            {
                if ((Flags & ExitFlag) != 0) Send(3, o);
                i++;
            }
            else
            {
                Enter(n);
                j++;
            }
        }
        if ((Flags & ExitFlag) != 0)
            for (; i < _inside.Count; i++) Send(3, _inside[i]);
        for (; j < now.Count; j++) Enter(now[j]);
        _inside.Clear();
        _inside.AddRange(now);
        if (now.Count != 0) Flags |= OccupiedFlag;
    }

    public void Reset()
    {
        _inside.Clear();
        Flags &= ~OccupiedFlag;
    }

    private void Enter(AgentLabAgent n)
    {
        if ((Flags & (FirstEnterFlag | OccupiedFlag)) == FirstEnterFlag) Send(0, n);
        else if ((Flags & EnterFlag) != 0) Send(1, n);
        else if ((Flags & InsideFlag) != 0) Send(2, n);
    }

    private void Send(int kind, AgentLabAgent activator)
    {
        ushort message = Messages[kind];
        _targets ??= Source.Trigger.Instances.Select(id => _world.Find(Instances, id)).ToArray();
        if (kind != 2 || _world.TraceTriggers)
            _world.Log?.Invoke($"[{Name}] {(kind switch { 0 => "first enter", 1 => "enter", 2 => "inside", _ => "exit" })} " +
                               $"by {activator.Entity.Name} -> message 0x{message:X} to activator + " +
                               $"[{string.Join(", ", Source.Trigger.Instances.Select((id, k) => $"0x{id:X}{(_targets[k] is null ? "?" : "")}"))}]");
        activator.Deliver(message);
        foreach (var target in _targets) target?.Deliver(message, activator);
    }
}
