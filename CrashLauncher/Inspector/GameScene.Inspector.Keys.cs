using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-19
public sealed partial class GameScene
{
    private static readonly Vector4 KeyUseColor = new(1f, 0.6f, 0.2f, 1f);
    private readonly HashSet<object> _unlockedKeyLists = new(ReferenceEqualityComparer.Instance);

    private List<Entity> SelectedPositionPoints() =>
        _selectionOrder.Where(x => x.Parent is not null && x.Get<PositionMarker>() is not null).ToList();

    private Entity? SingleSelectedInstance()
    {
        var owners = _selectionOrder.Where(x => x.Parent is not null && x.Get<InstanceData>() is not null).Take(2).ToList();
        return owners.Count == 1 ? owners[0] : null;
    }

    private void DrawInspectorKeys(Entity e, InstanceData inst)
    {
        var points = PatrolPoints(inst.Source);
        var picked = ReferenceEquals(SingleSelectedInstance(), e) ? SelectedPositionPoints() : new List<Entity>();
        var info = MovementOf(inst);
        if (points.Count == 0 && picked.Count == 0 && !info.UsesKeys) return;
        bool patrolEnemy = info.PatrolBit is not null;
        if (!ImGui.CollapsingHeader($"{(patrolEnemy ? "Patrol points (keys)" : "Keys (Positions)")} ({points.Count})##instkeys", ImGuiTreeNodeFlags.DefaultOpen)) return;

        var uses = KeyUsesOf(inst);
        bool locked = (uses.Count > 0 || !patrolEnemy) && !_unlockedKeyLists.Contains(inst.Source);
        for (int i = 0; i < points.Count; i++)
        {
            var (id, _, marker) = points[i];
            ImGui.TextUnformatted($"[{i}] Position_{id:X4}" + (marker is null ? "  (not in this layout)" : ""));
            if (marker is not null)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"Select##keysel{i}")) SelectClicked(marker, false);
            }
            ImGui.BeginDisabled(locked);
            ImGui.SameLine();
            if (ImGui.SmallButton($"^##keyup{i}")) EditPatrol(e, rec => MovePatrolPoint(rec, inst, i, -1));
            ImGui.SameLine();
            if (ImGui.SmallButton($"v##keydn{i}")) EditPatrol(e, rec => MovePatrolPoint(rec, inst, i, +1));
            ImGui.SameLine();
            bool removed = ImGui.SmallButton($"x##keydel{i}");
            ImGui.EndDisabled();
            var scripts = uses.Where(u => u.Range.Contains(i)).Select(u => u.Script).Distinct().ToList();
            if (scripts.Count > 0) ImGui.TextColored(KeyUseColor, $"    used as key {i} by {string.Join(", ", scripts)}");
            if (removed) { EditPatrol(e, rec => RemovePatrolPoint(rec, inst, i)); break; }
        }

        if (points.Count > 0)
        {
            if (uses.Count > 0) ImGui.TextColored(KeyUseColor, "Its scripts use these keys by number: moving or removing one shifts the numbers.");
            else if (!patrolEnemy) ImGui.TextDisabled("Cutscenes and other objects can read these keys by number.");
            if (uses.Count > 0 || !patrolEnemy)
            {
                bool unlocked = !locked;
                if (ImGui.Checkbox("Unlock move / remove##keyunlock", ref unlocked))
                {
                    if (unlocked) _unlockedKeyLists.Add(inst.Source);
                    else _unlockedKeyLists.Remove(inst.Source);
                }
            }
        }

        if (picked.Count > 0)
        {
            if (ImGui.Button($"Add {picked.Count} selected position(s) to the end##keyadd", new Vector2(-1f, 0f))) AppendKeys(e, inst, picked);
        }
        else ImGui.TextDisabled("To add: Shift+click Position points, then Add (adds at the end, numbers stay).");
    }

    private void DrawAddKeysFromPositions(Entity e)
    {
        if (e.Get<PositionMarker>() is null || SingleSelectedInstance() is not { } owner || owner.Get<InstanceData>() is not { } inst) return;
        var picked = SelectedPositionPoints();
        if (picked.Count == 0) return;
        ImGui.Separator();
        ImGui.TextUnformatted($"Keys of {owner.Name}: {inst.Source.Positions.Count}");
        if (ImGui.Button($"Add {picked.Count} selected position(s) to its end##keyaddpos", new Vector2(-1f, 0f))) AppendKeys(owner, inst, picked);
        ImGui.TextDisabled("In the order you selected them. The existing key numbers stay.");
    }
}
