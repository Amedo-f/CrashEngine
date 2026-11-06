using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private NavRoute? _route; // Amedo
    private NavPath? _routePath; // Amedo
    private int _routeIndex = 0xFF;
    private float _logicalRadius;

    private NavNode? RouteNode(int index) => _route is { } r && index >= 0 && index < r.Count ? World.PathFinder.PositionAt_253588(r, index) : null; // Amedo

    private NavNode? EdgeEndNode() => RouteNode(_routeIndex - 1 >= 0 ? _routeIndex - 1 : 0);

    private bool EdgeNeedsJump() => EdgeHasFlag(4);

    // Amedo
    private bool EdgeHasFlag(ushort bit) => _route is not null && !_endReached && _routePath is { } path && (path.Flags & bit) != 0;

    // Amedo
    private void SetRoute_20EB48(NavRoute route)
    {
        if (_route is { } old) World.PathFinder.Leave_253718(old);
        _route = route;
        _routeIndex = (route.Count - 1) & 0xFF;
        _routePath = World.PathFinder.PathTo_2535D0(route, _routeIndex);
        World.PathFinder.Occupy_253660(route);
    }

    // Amedo
    private void ReleaseRoute_20EFC8()
    {
        _routeIndex = 0xFF;
        if (_route is null) return;
        World.PathFinder.Leave_253718(_route);
        _routePath = null;
        _route = null;
    }

    // Amedo
    private void ClearRoute_20EAD8()
    {
        _routeIndex = 0xFF;
        _keyCurrent = 0;
        if (_route is not null) World.PathFinder.Leave_253718(_route);
        _route = null;
        _endReached = false;
        _routePath = null;
    }

    // Amedo
    private void PreviousRouteStep_20EEB0()
    {
        if (_route is null) return;
        _routeIndex = (_routeIndex - 1) & 0xFF;
        if (_routeIndex == 0xFF)
        {
            _routeIndex = 0;
            _endReached = true;
        }
        _routePath = World.PathFinder.PathTo_2535D0(_route, _routeIndex);
    }

    // Amedo
    private void NextRouteStep_20EF40()
    {
        if (_route is null) return;
        if (_endReached)
        {
            _routeIndex = 1;
            _endReached = false;
        }
        else _routeIndex = (_routeIndex + 1) & 0xFF;
        if (_routeIndex == _route.Count) _routeIndex = (_routeIndex - 1) & 0xFF;
        _routePath = World.PathFinder.PathTo_2535D0(_route, _routeIndex);
    }

    // Amedo
    private void RestartRoute_20EF20()
    {
        if (_route is null) return;
        _routePath = null;
        _routeIndex = 0;
    }

    private void GetShortRoute(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        ulong flags = Arg(5) | ((ulong)Arg(6) << 32);

        ReleaseRoute_20EFC8(); // Amedo
        _endReached = false;

        var start = GamePosition;
        if ((flags & 0x100000000) != 0)
        {
            var m = LocalMatrix;
            start += new Vector3(m.M31, m.M32, m.M33) * FloatValueOf(Arg(11));
        }
        var offset = new Vector3(F(1), F(2), F(3));
        var dest = SourcePosition((int)(flags & 0xFF), (int)((flags >> 8) & 0xFF), (int)((flags >> 28) & 0xF),
                                  (flags & 0x2000000) != 0 ? offset : null);

        var nav = Chunk.Nav; // Amedo 2026-09-28
        ushort fromNeed = (ushort)(Arg(13) >> 16), fromSkip = (ushort)(Arg(14) & 0xFFFF); // Amedo
        ushort toNeed = (ushort)(Arg(12) >> 16), toSkip = (ushort)(Arg(13) & 0xFFFF);
        var from = (fromNeed | fromSkip) != 0 ? nav.Nearest_23BC80(start, fromNeed, fromSkip) : nav.Nearest_23BAF8(start);
        var to = (toNeed | toSkip) != 0 ? nav.Nearest_23BC80(dest, toNeed, toSkip) : nav.Nearest_23BAF8(dest);
        if (from is null || to is null) return;
        if (F(15) > 0f && MathF.Sqrt(GameMath.DistanceSq(from.Position, start)) - from.Radius > F(15)) return; // Amedo
        if (F(16) > 0f && MathF.Sqrt(GameMath.DistanceSq(to.Position, dest)) - to.Radius > F(16)) return;

        var req = new NavRequest // Amedo
        {
            StartPos = (ushort)from.Index,
            GoalPos = (ushort)to.Index,
            StartNav = (ushort)nav.Index,
            GoalNav = (ushort)nav.Index,
        };
        if ((flags & 0x4000000) != 0) req.Flags = 4;
        else
        {
            if ((flags & 0x400000) != 0) req.Flags |= 2;
            if ((flags & 0x200000000) == 0) req.Flags |= 0x200000;
            if ((flags & 0x1000000000) == 0) req.Flags |= 0x1000000;
            if ((flags & 0x20000) == 0) req.Flags |= 0x20000 | 0x40000 | 0x80000;
            else
            {
                if ((flags & 0x80000) == 0) req.Flags |= 0x40000;
                if ((flags & 0x40000) == 0) req.Flags |= 0x80000;
            }
            if ((flags & 0x800000000) == 0) req.Flags |= 0x800000;
            if ((flags & 0x400000000) == 0) req.Flags |= 0x400000;
            if ((flags & 0x100000) == 0) req.Flags |= 0x100000;
            if ((flags & 0x1000000) != 0) req.Flags |= 8;
            if ((flags & 0x800000) != 0) { req.Flags |= 0x10; req.Value14 = FloatValueOf(Arg(8)); }
            if ((flags & 0x8000000) != 0) req.Flags |= 0x20;
        }
        var pf = World.PathFinder;
        pf.Start_1200 = start;
        pf.Dest_1210 = dest;
        pf.Mid_1220 = new Vector3((dest.X + start.X) * 0.5f, (dest.Y + start.Y) * 0.5f, (dest.Z + start.Z) * 0.5f);
        pf.Radius2_1230 = GameMath.DistanceSq(start, pf.Mid_1220);
        if ((flags & 0x800000) != 0) pf.SetNearFocusWeight_251CA8(req.Value14);
        if (pf.Search_242868(req) is not { } route) return;
        SetRoute_20EB48(route);
        Trace($"route: {route.Count} nodes (request 0x{req.Flags:X})");
    }

    private bool InsideNode(NavNode? node)
    {
        if (node is null) return false;
        var (min, max) = WorldBox(); // Amedo
        var p = new Vector3((max.X - min.X) * 0.5f + min.X, (max.Y - min.Y) * 0.5f + min.Y, (max.Z - min.Z) * 0.5f + min.Z);
        if (node.Position.Y + 3.7f < p.Y || p.Y < node.Position.Y - 0.8f) return false;
        float dx = p.X - node.Position.X, dz = p.Z - node.Position.Z;
        return dx * dx + dz * dz < node.Radius * node.Radius;
    }

    private Vector3 RouteFocus(bool current, List<uint> a)
    {
        float F(int i) => i < a.Count ? BitConverter.UInt32BitsToSingle(a[i]) : 0f;
        if (_route is null) return Vector3.Zero;
        var node = current ? RouteNode(_routeIndex) : EdgeEndNode();
        if (node is null) return Vector3.Zero;

        var pos = node.Position;
        float usable = node.Radius - _logicalRadius;
        bool clamp = false;
        if (F(9) != 0f && RouteNode(current ? (_routeIndex != 0 ? _routeIndex - 1 : -1) : (_routeIndex >= 2 ? _routeIndex - 2 : -1)) is { } after) // Amedo 2026-10-05
        {
            var bisector = Unit((Unit(after.Position - pos) + Unit(GamePosition - pos)) * 0.5f);
            pos += bisector * (usable * F(9));
            clamp = true;
        }
        if (F(10) != 0f)
        {
            float Rand() => World.Rng.GetRandFloat__18C130(1f); // Amedo
            var dir = Unit(new Vector3(Rand(), 0f, Rand()));
            pos += dir * (usable * F(10));
            clamp = true;
        }
        if (0f < F(13) || 0f < F(14) || 0f < F(15)) // Amedo 2026-10-05
        {
            var toMe = Unit(GamePosition - pos);
            pos += new Vector3(toMe.X, pos.Y, toMe.Z) * (usable * F(15)) + new Vector3(-toMe.Z, 0f, toMe.X) * (usable * F(13)) + new Vector3(0f, 4.5f * F(14), 0f);
        }
        if (clamp)
        {
            float dx = pos.X - node.Position.X, dz = pos.Z - node.Position.Z;
            if (usable * usable < dx * dx + dz * dz)
                pos = node.Position + Unit(pos - node.Position) * usable;
        }
        return pos;
    }

    private void NowMoveForwards(uint valueWord)
    {
        var m = LocalMatrix;
        SetLocalPosition(m.Translation + new Vector3(m.M31, m.M32, m.M33) * (FloatValueOf(valueWord) * (float)World.Dt));
    }
}
