using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabWorld
{
    private readonly List<InstanceCell> _moved_323540 = new();
    private readonly int[] _volumeCells = new int[3 * 16 * 16 + 1];
    public int MovedProcessed { get; private set; }

    // Amedo
    internal static InstanceCell?[] CellsOf_160(WorldChunk chunk) => chunk.Cells_160 ??= new InstanceCell?[InstanceCell.CellCount];

    // Amedo
    internal void MarkMoved_197048(InstanceCell cell)
    {
        if (cell.Queued_4) return;
        cell.Queued_4 = true;
        _moved_323540.Add(cell);
    }

    // Amedo
    internal void StepQueuedObjects_1985C0()
    {
        for (int i = 0; i < _moved_323540.Count; i++) StepQueued_197818(_moved_323540[i]);
        MovedProcessed += _moved_323540.Count;
        _moved_323540.Clear();
    }

    // Amedo
    private static void StepQueued_197818(InstanceCell cell)
    {
        if ((cell.Flags_4 & 1) == 0)
        {
            if (cell is AgentCell { Agent: var agent }) agent.StepObjectCollision_1F0368();
            else if (cell is SceneryCell { Scenery: var scenery }) scenery.StepObjectCollision_1F0368();
        }
        cell.Queued_4 = false;
    }

    // Amedo
    internal static void CellsSort_1EB020(InstanceCell cell, InstanceCell?[] grid)
    {
        int index = cell.InstanceCell_1E8C88();
        int old = cell.Index_24;
        if (old == -1) InstanceCell.Insert_1999B0(grid, index, cell);
        else if (old != index)
        {
            InstanceCell.Remove_199938(grid, old, cell);
            InstanceCell.Insert_1999B0(grid, index, cell);
        }
        cell.Index_24 = index;
        cell.Grid_20 = grid;
    }

    // Amedo
    internal static void CellsLeave(InstanceCell cell)
    {
        if (cell.Grid_20 is not { } grid || cell.Index_24 == -1) return;
        InstanceCell.Remove_199938(grid, cell.Index_24, cell);
        cell.Index_24 = -1;
    }

    // Amedo
    internal void MoveToChunk_1ED188(AgentLabAgent agent, WorldChunk from)
    {
        if (!agent.InChunk_A0 || agent.Asleep) return;
        Reregister_198730(agent); // Amedo
        agent.LeaveTree();
        agent.SortIntoCells_1EB020();
    }

    // Amedo
    public string CellStats()
    {
        int inGrid = 0, missing = 0, stale = 0, moved = 0, scenery = 0;
        var staleNames = new List<string>();
        foreach (var a in Agents)
        {
            if (!a.InChunk_A0 || a.Asleep || a.Destroyed) continue;
            var c = a.Cell;
            if (c.Index_24 == -1) { missing++; continue; }
            inGrid++;
            if (c.Queued_4) continue;
            if (a.CellBoxStale()) { stale++; if (staleNames.Count < 12) staleNames.Add(a.Entity.Name.Split("_act_").Last()); }
            if (c.Index_24 != c.InstanceCell_1E8C88()) moved++;
        }
        foreach (var d in DynamicScenery) if (d.Cell.Index_24 != -1) scenery++;
        return $"in grid {inGrid}, missing {missing}, stale box {stale}, off cell {moved}, scenery {scenery}, moved processed {MovedProcessed}, draw list {DrawQueued.Front}+{DrawQueued.Back}" + (staleNames.Count > 0 ? $" (stale: {string.Join(", ", staleNames)})" : "");
    }

    // Amedo
    private static bool SphereTouchesBox(Vector3 c, float r, Vector3 min, Vector3 max)
    {
        if (c.X + r < min.X || max.X < c.X - r) return false;
        if (c.Y + r < min.Y || max.Y < c.Y - r) return false;
        if (c.Z + r < min.Z || max.Z < c.Z - r) return false;
        return true;
    }

    // Amedo
    private static bool Skipped(InstanceCell cell, AgentLabAgent? skip, AgentLabAgent? parent) =>
        cell is AgentCell { Agent: var a } && (ReferenceEquals(a, skip) || ReferenceEquals(a, parent) || a.Destroyed || !a.Entity.Active);

    private static object Owner(InstanceCell cell) => cell switch
    {
        AgentCell a => a.Agent,
        SceneryCell d => d.Scenery,
        _ => ((TriggerCell)cell).Trigger,
    };

    // Amedo
    public IReadOnlyList<object> QuerySphere(Vector3 center, float r, uint mask, uint exclude, AgentLabAgent? skip, bool boxes = true,
                                             uint require = 0, bool requireAll = true, int cap = 0xB4)
    {
        _sphere.Clear();
        if (ChunkOf(skip).Cells_160 is not { } grid) return _sphere.ToList();
        var parent = skip?.Parent28;
        float r2 = r * r;
        int n = InstanceCell.CellsOfVolume_1E8D50(center - new Vector3(r), center + new Vector3(r), _volumeCells);
        for (int i = 0; i < n && _sphere.Count < cap; i++)
            for (var c = grid[_volumeCells[i]]; c is not null && _sphere.Count < cap; c = c.Next_14C)
            {
                if (Skipped(c, skip, parent) || !c.Filter_1EA1E8(mask, exclude, require, requireAll)) continue;
                if (boxes ? !SphereTouchesBox(center, r, c.BoxMin_40, c.BoxMax_50) : !(Vector3.DistanceSquared(center, c.Position_30) < r2)) continue;
                _sphere.Add(Owner(c));
            }
        return _sphere.ToList();
    }

    // Amedo
    public List<AgentLabAgent> QueryCylinder_1E9018(Vector3 center, float r, float halfHeight, uint mask, uint exclude, AgentLabAgent? skip, int cap)
    {
        var hits = new List<AgentLabAgent>();
        if (ChunkOf(skip).Cells_160 is not { } grid) return hits;
        var parent = skip?.Parent28;
        float lo = center.Y - halfHeight, hi = center.Y + halfHeight, r2 = r * r;
        var reach = new Vector3(r, halfHeight, r);
        int n = InstanceCell.CellsOfVolume_1E8D50(center - reach, center + reach, _volumeCells);
        for (int i = 0; i < n && hits.Count < cap; i++)
            for (var c = grid[_volumeCells[i]]; c is not null && hits.Count < cap; c = c.Next_14C)
            {
                if (c is not AgentCell { Agent: var a } || Skipped(c, skip, parent) || !c.Filter_1EA1E8(mask, exclude, 0, true)) continue;
                if (!(lo < c.BoxMax_50.Y) || !(c.BoxMin_40.Y < hi)) continue;
                var p = c.Position_30;
                float dx = center.X - p.X, dz = center.Z - p.Z;
                if (dx * dx + dz * dz < r2) hits.Add(a);
            }
        return hits;
    }

    // Amedo
    public List<AgentLabAgent> QueryBox_1ECFC0(Vector3 qmin, Vector3 qmax, uint mask, uint exclude, uint require, bool requireAll, AgentLabAgent? skip, int cap)
    {
        var hits = new List<AgentLabAgent>();
        if (ChunkOf(skip).Cells_160 is not { } grid) return hits;
        var parent = skip?.Parent28;
        int n = InstanceCell.CellsOfVolume_1E8D50(qmin, qmax, _volumeCells);
        for (int i = 0; i < n && hits.Count < cap; i++)
            for (var c = grid[_volumeCells[i]]; c is not null && hits.Count < cap; c = c.Next_14C)
            {
                if (c is not AgentCell { Agent: var a } || Skipped(c, skip, parent) || !c.Filter_1EA1E8(mask, exclude, require, requireAll)) continue;
                if (!Physics.CollisionWorld.Overlap(qmin, qmax, c.BoxMin_40, c.BoxMax_50)) continue;
                hits.Add(a);
            }
        return hits;
    }

    // Amedo
    public List<object> QueryBoxObjects_1ECFC0(Vector3 qmin, Vector3 qmax, uint mask, uint exclude, uint require, bool requireAll, AgentLabAgent? skip, int cap)
    {
        var hits = new List<object>();
        if (ChunkOf(skip).Cells_160 is not { } grid) return hits;
        var parent = skip?.Parent28;
        int n = InstanceCell.CellsOfVolume_1E8D50(qmin, qmax, _volumeCells);
        for (int i = 0; i < n && hits.Count < cap; i++)
            for (var c = grid[_volumeCells[i]]; c is not null && hits.Count < cap; c = c.Next_14C)
            {
                if (Skipped(c, skip, parent) || !c.Filter_1EA1E8(mask, exclude, require, requireAll)) continue;
                if (Physics.CollisionWorld.Overlap(qmin, qmax, c.BoxMin_40, c.BoxMax_50)) hits.Add(Owner(c));
            }
        return hits;
    }

    // Amedo
    internal List<LayoutTrigger> TriggersIn_1ECFC0(Vector3 qmin, Vector3 qmax, WorldChunk chunk, uint exclude = 1, uint require = 0, bool requireAll = true)
    {
        var hits = new List<LayoutTrigger>();
        if (!_triggersLoaded) LoadTriggers();
        if (chunk.Cells_160 is not { } grid) return hits;
        int n = InstanceCell.CellsOfVolume_1E8D50(qmin, qmax, _volumeCells);
        for (int i = 0; i < n && hits.Count < TriggerListCap; i++)
            for (var c = grid[_volumeCells[i]]; c is not null && hits.Count < TriggerListCap; c = c.Next_14C)
            {
                if (c is not TriggerCell { Trigger: var t } || !c.Filter_1EA1E8(TriggerCell.TriggerComponent_80, exclude, require, requireAll)) continue;
                if (Physics.CollisionWorld.Overlap(qmin, qmax, c.BoxMin_40, c.BoxMax_50)) hits.Add(t);
            }
        return hits;
    }

    // Amedo
    public List<object> QueryKinds_1F16B0(WorldChunk chunk, uint mask, uint exclude, uint require, bool requireAll, int cap)
    {
        var hits = new List<object>();
        if (chunk.Cells_160 is not { } grid) return hits;
        for (int i = 0; i < InstanceCell.CellCount && hits.Count < cap; i++)
            for (var c = grid[i]; c is not null && hits.Count < cap; c = c.Next_14C)
            {
                if (c is AgentCell { Agent: var a } && (a.Destroyed || !a.Entity.Active)) continue;
                if (c.Filter_1EA1E8(mask, exclude, require, requireAll)) hits.Add(Owner(c));
            }
        return hits;
    }

    // Amedo
    public IEnumerable<AgentLabAgent> SegmentCandidates_1E9168(Vector3 from, Vector3 to, WorldChunk chunk)
    {
        if (chunk.Cells_160 is not { } grid) yield break;
        var cells = new int[_volumeCells.Length];
        int n = InstanceCell.CellsOfVolume_1E8D50(Vector3.Min(from, to), Vector3.Max(from, to), cells);
        for (int i = 0; i < n; i++)
            for (var c = grid[cells[i]]; c is not null; c = c.Next_14C)
                if (c is AgentCell { Agent: var a }) yield return a;
    }

    // Amedo
    public IReadOnlyList<object> QueryOverlap(Physics.ConvexHull hull, Matrix4x4 m, uint mask, uint exclude, AgentLabAgent? skip, int max = 0xB4)
    {
        _overlap.Clear();
        if (ChunkOf(skip).Cells_160 is not { } grid) return _overlap;
        var (qmin, qmax) = hull.Bounds(m);
        int n = InstanceCell.CellsOfVolume_1E8D50(qmin, qmax, _volumeCells);
        for (int i = 0; i < n && _overlap.Count < max; i++)
            for (var c = grid[_volumeCells[i]]; c is not null && _overlap.Count < max; c = c.Next_14C)
            {
                if (c is not AgentCell { Agent: var a } || ReferenceEquals(a, skip) || a.Destroyed || !a.Entity.Active || !c.Filter_1EA1E8(mask, exclude, 0, true)) continue;
                if (!Physics.CollisionWorld.Overlap(qmin, qmax, c.BoxMin_40, c.BoxMax_50) || a.Body() is not { } body) continue;
                foreach (var (h, pm) in body.Parts)
                {
                    var (bmin, bmax) = h.Bounds(pm);
                    if (!Physics.CollisionWorld.Overlap(qmin, qmax, bmin, bmax) || !Physics.ConvexHull.Overlaps_200370(hull, m, h, pm)) continue;
                    _overlap.Add(a);
                    break;
                }
            }
        return _overlap;
    }

    // Amedo
    public IReadOnlyList<Character.CharacterBody> QueryBodies(Vector3 min, Vector3 max, uint mask, uint exclude, uint require, AgentLabAgent? skip, bool requireAll = false, bool skipLinks = true)
    {
        _bodies.Clear();
        if (ChunkOf(skip).Cells_160 is not { } grid) return _bodies;
        var linked = skipLinks ? skip?.Links.Where(l => l is not null).ToHashSet() : null;
        int n = InstanceCell.CellsOfVolume_1E8D50(min, max, _volumeCells);
        for (int i = 0; i < n && _bodies.Count < 64; i++)
            for (var c = grid[_volumeCells[i]]; c is not null && _bodies.Count < 64; c = c.Next_14C)
            {
                if (!c.Filter_1EA1E8(mask, exclude, require, requireAll) || !Physics.CollisionWorld.Overlap(min, max, c.BoxMin_40, c.BoxMax_50)) continue;
                Character.CharacterBody? body;
                if (c is AgentCell { Agent: var a })
                {
                    if (ReferenceEquals(a, skip) || a.Destroyed || !a.Entity.Active || linked is not null && linked.Contains(a)) continue;
                    body = a.Body();
                }
                else if (c is SceneryCell { Scenery: var d }) body = d.Body;
                else continue;
                if (body is null) continue;
                foreach (var (hull, pm) in body.Parts)
                {
                    var (bmin, bmax) = hull.Bounds(pm);
                    if (!Physics.CollisionWorld.Overlap(min, max, bmin, bmax)) continue;
                    _bodies.Add(body);
                    break;
                }
            }
        return _bodies;
    }
}
