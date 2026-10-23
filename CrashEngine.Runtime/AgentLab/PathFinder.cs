using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed class NavRequest
{
    public uint Flags;
    public byte Byte4;
    public float Radius8, ValueC, Value10, Value14, Value18;
    public ushort StartPos, GoalPos, StartNav, GoalNav;
}

// Amedo
public sealed class NavRoute
{
    public const int Size = 0x100;
    public readonly ushort[] Nav = new ushort[Size];
    public readonly ushort[] Pos = new ushort[Size];
    public byte Count;
}

// Amedo
public sealed class GamePathFinder
{
    private struct Entry
    {
        public NavNode? Node;
        public ushort Nav, Pos;
        public float G, F;
    }

    public const int MaxNavs = 0x80;
    private const int Entries = 0xFF;
    private readonly NavGraph?[] _navs = new NavGraph?[MaxNavs];
    private readonly Entry[] _entries = new Entry[Entries];
    private byte _reverse_11F4;
    private NavRequest? _request_1234;

    public Vector3 Start_1200, Dest_1210, Mid_1220;
    public float Radius2_1230;
    public int Stamp { get; private set; }

    public NavGraph? NavAt(int index) => index >= 0 && index < MaxNavs ? _navs[index] : null;

    public NavNode? PositionOf(int nav, int pos) => NavAt(nav) is { } n && pos >= 0 && pos < n.Nodes.Count ? n.Nodes[pos] : null;

    public NavNode? PositionAt_253588(NavRoute route, int index) => PositionOf(route.Nav[index & 0xFF], route.Pos[index & 0xFF]);

    public NavPath? PathTo_2535D0(NavRoute route, int index)
    {
        index &= 0xFF;
        if (index == 0) return null;
        return PositionAt_253588(route, index)?.PathTo_23C710(route.Nav[index - 1], route.Pos[index - 1]);
    }

    public void Occupy_253660(NavRoute route)
    {
        for (int i = 0; i < route.Count; i++)
            if (PositionAt_253588(route, i) is { } node && node.Occupancy < 0xFF) node.Occupancy++;
    }

    public void Leave_253718(NavRoute route)
    {
        for (int i = 0; i < route.Count; i++)
            if (PositionAt_253588(route, i) is { Occupancy: not 0 } node) node.Occupancy--;
    }

    public void Link_22BB58(NavGraph nav)
    {
        int index = Array.IndexOf(_navs, null);
        if (index < 0) return;
        nav.Index = index;
        _navs[index] = nav;
        NewSearch_252080();
        foreach (var path in nav.AiPaths)
        {
            path.NavA = path.NavB = (ushort)index;
            if (path.A < nav.Nodes.Count) nav.Nodes[path.A].Paths.Add(path);
            if (path.B < nav.Nodes.Count) nav.Nodes[path.B].Paths.Add(path);
        }
    }

    public void Unlink(NavGraph nav)
    {
        if (nav.Index >= 0 && nav.Index < MaxNavs && ReferenceEquals(_navs[nav.Index], nav)) _navs[nav.Index] = null;
        nav.Index = -1;
    }

    private void NewSearch_252080()
    {
        Stamp++;
        if (Stamp != 0x8000) return;
        foreach (var nav in _navs)
            if (nav is not null)
                foreach (var node in nav.Nodes) node.Stamp = 0;
        Stamp = 1;
    }

    private static float Estimate_17C258(NavNode a, NavNode b)
    {
        float x = a.Position.X - b.Position.X, y = a.Position.Y - b.Position.Y, z = a.Position.Z - b.Position.Z;
        return x * x + y * y + z * z;
    }

    private static float DistanceSq(Vector3 a, Vector3 b)
    {
        float x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
        return x * x + y * y + z * z;
    }

    private float StepCost_178470(NavPath path, NavNode from, NavNode to)
    {
        if ((to.Flags & 1) != 0 || (from.Flags & 1) != 0) return -1f;
        uint r = _request_1234!.Flags;
        if ((r & 4) != 0) return DistanceSq(from.Position, to.Position);
        uint p = path.Flags;
        if ((r & 0x200000) != 0 && (p & 0x40) != 0) return -1f;
        if ((r & 0x1000000) != 0 && (p & 0x1E0) == 0) return -1f;
        if ((r & 0x20000) != 0)
        {
            if ((p & 4) != 0) return -1f;
        }
        else
        {
            if ((r & 0x80000) != 0 && (p & 8) != 0) return -1f;
            if ((r & 0x40000) != 0 && (p & 0x10) != 0) return -1f;
        }
        if ((r & 0x800000) != 0 && (p & 0x100) != 0) return -1f;
        if ((r & 0x400000) != 0 && (p & 0x80) != 0) return -1f;
        if ((r & 0x100000) != 0 && (p & 0x20) != 0) return -1f;
        float cost = DistanceSq(from.Position, to.Position);
        if ((r & 8) != 0 && !(MathF.Abs(Radius2_1230) <= 5e-05f))
            cost += (1f - DistanceSq(to.Position, Mid_1220) / Radius2_1230) * 100f;
        if ((r & 0x20) != 0) cost += to.Occupancy * 100f;
        if ((r & 0x10) != 0 && !(MathF.Abs(Radius2_1230) <= 5e-05f))
            cost += DistanceSq(to.Position, Mid_1220) * 100f / Radius2_1230;
        return cost;
    }

    public void SetNearFocusWeight_251CA8(float weight)
    {
        var d = Dest_1210 - Start_1200;
        d *= Camera.CamMath.InvLengthGuarded_11C030(2.4999998e-09f, new Vector4(d, 0f));
        var side = new Vector3(d.Z, 0f, -d.X);
        float far = MathF.Sqrt(DistanceSq(Start_1200, Dest_1210));
        Mid_1220 += side * (weight * far);
    }

    private NavRoute MakeRoute_252108(NavNode start, ushort nav, ushort pos)
    {
        var route = new NavRoute();
        for (int steps = 1; ; steps++)
        {
            route.Nav[route.Count] = nav;
            route.Pos[route.Count] = pos;
            route.Count++;
            var node = PositionOf(nav, pos);
            if (steps >= 0xFF || node is null) return route;
            if (node.BackPos_1A == 0xFF) return route;
            nav = node.BackNav_18;
            pos = node.BackPos_1A;
            node.BackPos_1A = 0xFF;
            if (ReferenceEquals(node, start)) return route;
        }
    }

    public NavRoute? Search_242868(NavRequest req)
    {
        NewSearch_252080();
        _request_1234 = req;
        var start = PositionOf(req.StartNav, req.StartPos);
        var goal = PositionOf(req.GoalNav, req.GoalPos);
        if (start is null || goal is null) return null;
        int head = 0x7F, tail = 0x7F;
        ref var first = ref _entries[head];
        first.G = 0f;
        first.F = Estimate_17C258(start, goal);
        first.Node = start;
        first.Nav = req.StartNav;
        first.Pos = req.StartPos;
        start.BackPos_1A = 0xFF;
        while (true)
        {
            int at = head;
            var node = _entries[at].Node!;
            if (ReferenceEquals(node, goal)) return MakeRoute_252108(start, _entries[at].Nav, _entries[at].Pos);
            head++;
            ushort fromNav = _entries[at].Nav, fromPos = _entries[at].Pos;
            for (int i = 0; i < node.Paths.Count; i++)
            {
                var path = node.Paths[i];
                var navB = NavAt(path.NavB);
                var navA = NavAt(path.NavA);
                NavNode? next = null;
                ushort nextNav = 0, nextPos = 0;
                if (navB is not null && path.B < navB.Nodes.Count && ReferenceEquals(navB.Nodes[path.B], node))
                {
                    if (navA is not null && path.A < navA.Nodes.Count)
                    {
                        nextNav = path.NavA;
                        nextPos = path.A;
                        _reverse_11F4 = 0;
                        next = navA.Nodes[path.A];
                    }
                }
                else if (navA is not null && path.A < navA.Nodes.Count && ReferenceEquals(navA.Nodes[path.A], node)
                         && navB is not null && path.B < navB.Nodes.Count)
                {
                    nextNav = path.NavB;
                    nextPos = path.B;
                    _reverse_11F4 = 1;
                    next = navB.Nodes[path.B];
                }
                if (next is null || next.Stamp == (Stamp & 0x7FFF)) continue;
                float step = StepCost_178470(path, node, next);
                if (!(0f < step)) continue;
                next.Stamp = Stamp & 0x7FFF;
                if (ReferenceEquals(next, goal))
                {
                    next.BackPos_1A = fromPos;
                    next.BackNav_18 = fromNav;
                    return MakeRoute_252108(start, nextNav, nextPos);
                }
                float g = _entries[at].G + step;
                float h = Estimate_17C258(next, goal);
                int slot;
                if (g + h < _entries[head].F)
                {
                    if (head == 0) return null;
                    head--;
                    slot = head;
                }
                else
                {
                    if (tail == 0xFE) return null;
                    tail++;
                    slot = tail;
                }
                ref var e = ref _entries[slot];
                e.F = g + h;
                e.Pos = nextPos;
                e.Node = next;
                e.G = g;
                e.Nav = nextNav;
                next.BackPos_1A = fromPos;
                next.BackNav_18 = fromNav;
            }
            if (tail < head)
            {
                req.Flags |= 0x400;
                return null;
            }
        }
    }
}
