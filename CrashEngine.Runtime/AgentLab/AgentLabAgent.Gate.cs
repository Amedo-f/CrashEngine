using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private const uint MetricFar = 0xFFFFFF;

    private uint _metric = MetricFar;
    private ushort _extra;
    private bool _wakeOnce;
    private SceneryTree.Node? _treeNode;
    private long _lastStep; // Amedo
    private Vector3 _boxMin;
    private Vector3 _boxMax;
    private bool _hasBox;

    public uint Metric => _metric;
    public ushort ActivityExtra => _extra;
    public bool InTree => _treeNode is not null;
    public (Vector3 Min, Vector3 Max) TreeBox => WorldBox();
    public int Phase { get; internal set; }
    public int RegisterOrder { get; internal set; } // Amedo
    public bool LastGateRan { get; private set; } = true;

    private long _prevStep; // Amedo
    private float StepDt // Amedo
    {
        get
        {
            long now = World.NowTicks;
            long last = _lastStep == now ? _prevStep : _lastStep;
            return last == 0 ? 0f : (now - last) * TickTime;
        }
    }

    internal void MarkStepped() // Amedo 2026-10-01
    {
        long now = World.NowTicks; // Amedo
        if (_lastStep == now) return;
        _prevStep = _lastStep;
        _lastStep = now;
    }

    public float Travel { get; private set; }
    private Vector3? _travelFrom;

    internal void MeasureTravel()
    {
        var p = GamePosition;
        if (_travelFrom is { } from) Travel += GameMath.Distance(from, p);
        _travelFrom = p;
    }

    private void SetActivityRadius(uint r) => _extra = r == 0xFF ? (ushort)0xFFFF : (ushort)(r * r);

    private void LoadBox()
    {
        var ogi = Ogi;
        if (ogi?.BoundingBox is not { Length: >= 2 } bb) return;
        _boxMin = new Vector3(bb[0].X, bb[0].Y, bb[0].Z);
        _boxMax = new Vector3(bb[1].X, bb[1].Y, bb[1].Z);
        _hasBox = true;
    }

    private Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI? Ogi => // Amedo 2026-09-27
        Anim?.ModelGroup?.Ogi ?? Anim?.CurrentGroup?.Ogi ?? Anim?.DefaultGroup?.Ogi ?? (GameObject is { } obj ? World.DefaultOgi(obj) : null); // Amedo 2026-10-01

    private (Vector3 Min, Vector3 Max) WorldBox()
    {
        var m = LocalMatrix;
        if (_ogiBox is null && OwnCharacter is null && Ogi is { } hullOgi && CrashEngine.Runtime.Physics.OgiHulls.Count(hullOgi) > 0) return HullBox_1EB158(hullOgi, m); // Amedo
        if (!_hasBox) return (m.Translation, m.Translation);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? _boxMin.X : _boxMax.X,
                                     (i & 2) == 0 ? _boxMin.Y : _boxMax.Y,
                                     (i & 4) == 0 ? _boxMin.Z : _boxMax.Z);
            var p = Vector3.Transform(corner, m);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (min, max);
    }

    // Amedo 2026-10-06
    private int _hullBoxTick = int.MinValue;
    private Matrix4x4 _hullBoxMatrix;
    private object? _hullBoxOgi;
    private (Vector3 Min, Vector3 Max) _hullBox;

    // Amedo 2026-10-06
    private (Vector3 Min, Vector3 Max) HullBox_1EB158(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI ogi, Matrix4x4 m)
    {
        if (_hullBoxTick == World.NowTicks && ReferenceEquals(_hullBoxOgi, ogi) && _hullBoxMatrix == m) return _hullBox;
        if (!ReferenceEquals(ogi, _hullOgi))
        {
            _hullOgi    = ogi;
            _localHulls = CrashEngine.Runtime.Physics.OgiHulls.Of(ogi).ToList(); // Amedo
        }
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (int i = 0; i < _localHulls!.Count; i++)
        {
            var world = HullMatrix_1EB268(ogi, i, m); // Amedo 2026-11-09
            var (hmin, hmax) = _localHulls[i].Bounds(world);
            min = Vector3.Min(min, hmin);
            max = Vector3.Max(max, hmax);
        }
        _hullBoxTick   = World.NowTicks;
        _hullBoxOgi    = ogi;
        _hullBoxMatrix = m;
        _hullBox       = (min, max);
        return _hullBox;
    }

    // Amedo 2026-11-09
    internal Matrix4x4 HullMatrix_1EB268(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI ogi, int hull, Matrix4x4 m)
    {
        int joint = CrashEngine.Runtime.Physics.OgiHulls.Joint(ogi, hull);
        return joint == 0xFF ? m : CopyMatrixAtIndex_1EADE0(ogi, joint) * m;
    }

    // Amedo 2026-11-09
    private Matrix4x4 CopyMatrixAtIndex_1EADE0(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI ogi, int joint) =>
        _jointsPosed_19DE40 && JointsOf(ogi).TryGetValue(joint, out var jw) ? jw : Matrix4x4.Identity;

    private bool _jointsPosed_19DE40; // Amedo 2026-11-09

    private AgentCell? _cell; // Amedo
    internal AgentCell Cell => _cell ??= new AgentCell(this);
    internal bool InChunk_A0 { get; set; } // Amedo

    // Amedo
    internal void MarkMoved_197048() => World.MarkMoved_197048(Cell);

    // Amedo
    private void CellBox_1EB158()
    {
        var (min, max) = WorldBox();
        Cell.BoxMin_40 = min;
        Cell.BoxMax_50 = max;
    }

    // Amedo
    internal bool CellBoxStale()
    {
        var (min, max) = WorldBox();
        return min != Cell.BoxMin_40 || max != Cell.BoxMax_50;
    }

    // Amedo
    internal void ChunkAddInstance_1F1940()
    {
        InChunk_A0 = true;
        if (!Asleep) SortIntoCells_1EB020();
    }

    // Amedo
    internal void SortIntoCells_1EB020()
    {
        var cell = Cell;
        if (!_hasBox) LoadBox();
        if (!cell.Pinned_2C) CellBox_1EB158();
        if (_treeNode is not null) return;
        if (Chunk.Tree?.Root is { } root) MoveToNode_1F0418(Chunk.Tree.Find(cell.BoxMin_40, cell.BoxMax_50) ?? root);
        AgentLabWorld.CellsSort_1EB020(cell, AgentLabWorld.CellsOf_160(Chunk));
    }

    // Amedo
    private void MoveToNode_1F0418(SceneryTree.Node node)
    {
        _treeNode?.Bodies.Remove(this);
        node.Bodies.Insert(0, this);
        _treeNode = node;
    }

    // Amedo
    internal void QueueDraw_1E9DA0(List<AgentLabAgent> front, List<AgentLabAgent> back)
    {
        if (!Rendered || Asleep || Ogi is null) return;
        if (_modelSolid) back.Add(this);
        else front.Add(this);
    }

    // Amedo
    internal void StepObjectCollision_1F0368()
    {
        if (!InChunk_A0) return;
        var cell = Cell;
        if (!cell.Pinned_2C) CellBox_1EB158();
        if (_treeNode is null && Chunk.Tree?.Root is not null) return;
        if (!cell.Pinned_2C) UpdateCollisionCell_1EAAC0();
    }

    private const float CellEps_1EAAC0 = 4.99999987e-05f; // Amedo

    // Amedo
    private void UpdateCollisionCell_1EAAC0()
    {
        var cell = Cell;
        var root = Chunk.Tree?.Root;
        if ((AgentFlags & 0x80000) != 0)
        {
            if (root is not null) MoveToNode_1F0418(root);
            return;
        }
        if (_treeNode is not null && SceneryTree.Contain(_treeNode, cell.BoxMin_40, cell.BoxMax_50, CellEps_1EAAC0) != 1)
        {
            if (Chunk.Tree!.Find(cell.BoxMin_40, cell.BoxMax_50) is { } node) MoveToNode_1F0418(node);
            return;
        }
        if (cell.Grid_20 is { } grid) AgentLabWorld.CellsSort_1EB020(cell, grid);
    }

    // Amedo 2026-09-28
    internal void LeaveTree()
    {
        _treeNode?.Bodies.Remove(this);
        _treeNode = null;
        AgentLabWorld.CellsLeave(Cell); // Amedo
    }

    private bool _modelSolid; // Amedo

    // Amedo
    private void SetSolid_1A1200(bool solid)
    {
        _modelSolid = solid;
        if (Ogi is not null) SetCollisionSolid_1EFF60(solid);
    }

    private void SetCollisionSolid_1EFF60(bool solid) => AgentFlags = solid ? AgentFlags | 0x80000u : AgentFlags & ~0x80000u; // Amedo

    internal void UpdateMetric(Vector3 viewer)
    {
        if (_modelSolid) // Amedo
        {
            _metric = 0;
            _seenFrame = World.Frame;
            return;
        }
        float dSq = GameMath.DistanceSq(viewer, GamePosition);
        int value = dSq >= 2147483520f ? int.MaxValue : (int)dSq;
        _metric = (uint)value & MetricFar;
        _seenFrame = World.Frame; // Amedo 2026-10-01
    }

    private int _seenFrame = int.MinValue; // Amedo 2026-10-01
    private ushort _animExtra; // Amedo 2026-10-01
    public ushort AnimExtra => _animExtra;
    private bool _animComposed;
    private const uint AnimNear = 625, AnimFar = 9375;
    private const float AnimSkipScale = 0.00031996588f;

    // Amedo 2026-10-01
    internal void AnimGroup_19DE40()
    {
        if (!_destroyed) _jointsPosed_19DE40 = true; // Amedo 2026-11-09
        if (Anim is not { } anim || _destroyed) return;
        anim.LogicDriven = true;
        bool gate = World.GateActive && InTree;
        uint m = gate ? _metric : 0u;
        uint d = _animExtra == 0xFFFF || m <= _animExtra ? 0u : m - _animExtra;
        d = d <= AnimNear ? 0u : d - AnimNear;
        bool far = AnimFar <= d;
        bool compose = Rendered && (!gate || _seenFrame >= World.Frame - 1) && !far;
        if (far)
        {
            _animComposed = false;
            anim.SkipLogicStep();
            return;
        }
        bool full = !_animComposed || d == 0;
        if (!full)
        {
            uint mask = (1u << (GameMath.Trunc(AnimSkipScale * d) + 1)) - 1u;
            full = (((uint)World.Frame + (uint)Phase) & mask) == 0;
        }
        if (!full)
        {
            anim.SkipLogicStep();
            anim.MarkExitPoints_295690(); // Amedo
            return;
        }
        anim.AdvanceTo_294980(World.NowTicks);
        if (compose) anim.Compose_294B18();
        anim.MarkExitPoints_295690(); // Amedo
        _animComposed |= compose;
        if (compose) MarkMoved_197048(); // Amedo
    }

    internal bool ShouldRun(int frame)
    {
        bool always = _wakeOnce || (_keep88 & 0x40) != 0; // Amedo
        _wakeOnce = false;

        uint stale = 0;
        if (_extra != 0xFFFF && _extra < _metric)
        {
            uint over = _metric - _extra;
            stale = over > AgentLabWorld.GateNear ? over - AgentLabWorld.GateNear : 0;
        }

        bool run;
        if (always || !AgentLabWorld.ThrottleFarAgents) run = true; // Amedo 2026-09-30
        else if (stale >= AgentLabWorld.GateSleep) run = false;
        else if (stale == 0) run = true;
        else
        {
            int k = (int)(stale * AgentLabWorld.GateScale) + 1;
            uint mask = (1u << k) - 1;
            run = (((uint)frame + (uint)Phase) & mask) == 0;
        }
        LastGateRan = run;
        return run;
    }
}
