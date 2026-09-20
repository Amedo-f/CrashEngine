using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Linq;
using System.Numerics;

namespace CrashLauncher;

public sealed class ArrayPreviewMarker : Component { }

public sealed partial class GameScene
{
    private int     _arrayCount        = 2;
    private int     _arrayDirIndex     = 0;
    private float   _arrayGap          = 0f;
    private Vector3 _arrayRotationDeg  = Vector3.Zero;
    private bool    _arrayModeActive;
    private readonly List<(Entity Parent, Entity Ghost)> _arrayPreviewGhosts = new();

    private static readonly string[] ArrayDirLabels = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };

    private static bool IsArrayableEntity(Entity e) =>
        e.Has<SceneryTile>() || e.Has<InstanceData>();

    private void DrawArraySection(Entity e)
    {
        if (!IsArrayableEntity(e))
        {
            if (_arrayModeActive) { _arrayModeActive = false; ClearArrayPreview(); }
            return;
        }

        ImGui.Separator();
        if (!ImGui.CollapsingHeader("Array##array"))
        {
            if (_arrayModeActive) { _arrayModeActive = false; ClearArrayPreview(); }
            return;
        }

        int arrayableCount = _selectedSet.Count(IsArrayableEntity);

        if (!_arrayModeActive)
        {
            if (ImGui.Button("Add Array##addarray", new Vector2(-1f, 0f)))
            {
                _arrayCount = 2;
                _arrayGap = 0f;
                _arrayModeActive = true;
                RebuildArrayPreview();
            }
            if (ImGui.IsItemHovered())
                MaybeTooltip("Blender-style array — shows a LIVE preview you can tweak before\n" +
                             "committing anything real. Each step sits flush against the last,\n" +
                             "automatically sized from the object's own real geometry.");
            return;
        }

        ImGui.TextUnformatted("Count (total, including the original):");
        ImGui.SetNextItemWidth(-1f);
        ImGui.DragInt("##arrayCount", ref _arrayCount, 1, 2, 200);
        if (_arrayCount < 2) _arrayCount = 2;

        ImGui.Spacing();
        ImGui.TextUnformatted("Direction (spacing auto-sized to the object, flush every step):");
        for (int i = 0; i < ArrayDirLabels.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            bool selected = _arrayDirIndex == i;
            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.25f, 0.5f, 0.9f, 1f));
            if (ImGui.Button($"{ArrayDirLabels[i]}##arraydir{i}", new Vector2(48f, 0f)))
                _arrayDirIndex = i;
            if (selected) ImGui.PopStyleColor();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Gap (extra spacing on top of flush, per step):");
        ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat("##arrayGap", ref _arrayGap, 0.05f, 0f, 1000f);
        if (_arrayGap < 0f) _arrayGap = 0f;

        ImGui.Spacing();
        ImGui.TextUnformatted("Rotation (degrees, per step):");
        ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat3("##arrayRotation", ref _arrayRotationDeg, 1f);

        RebuildArrayPreview();

        ImGui.Spacing();
        if (ImGui.Button($"Apply Array ({arrayableCount} object(s) × {_arrayCount})##applyarray", new Vector2(-1f, 0f)))
        {
            ApplyArray();
            _arrayModeActive = false;
            ClearArrayPreview();
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Bakes real duplicates now (fresh graphics/instance ids, same proven\n" +
                         "clone pipeline Ctrl+D uses) -- each step sits flush against the last\n" +
                         "along the picked direction, plus Rotation×N tilt.\n" +
                         "Not a live/re-editable modifier -- the PS2 formats have no such concept.\n" +
                         "Undoable like any other duplicate. No collision auto-generated.");
        ImGui.SameLine();
        if (ImGui.Button("Cancel##cancelarray", new Vector2(-1f, 0f)))
        {
            _arrayModeActive = false;
            ClearArrayPreview();
        }
    }

    private void ApplyArray()
    {
        if (_arrayCount < 2) return;
        DuplicateSelected(SixDirs[_arrayDirIndex], repeatCount: _arrayCount - 1,
            arrayStepRotationDeg: _arrayRotationDeg, arrayStepGap: _arrayGap);
    }

    private void ClearArrayPreview()
    {
        foreach (var (parent, ghost) in _arrayPreviewGhosts)
            parent.RemoveChild(ghost);
        _arrayPreviewGhosts.Clear();
    }

    private void RebuildArrayPreview()
    {
        ClearArrayPreview();
        if (!_arrayModeActive || _arrayCount < 2) return;

        var dir = SixDirs[_arrayDirIndex];

        foreach (var original in _selectedSet.Where(IsArrayableEntity))
        {
            var parent = original.Parent;
            if (parent is null) continue;

            var baseLocal = original.Transform.LocalMatrix ?? original.Transform.Local;
            var basePos   = baseLocal.Translation;

            float extent = 1.5f;
            if (TryGetWorldAABB(original, out var aMin, out var aMax))
            {
                extent = MathF.Abs(dir.X) > 0.5f ? (aMax.X - aMin.X)
                        : MathF.Abs(dir.Y) > 0.5f ? (aMax.Y - aMin.Y)
                        : (aMax.Z - aMin.Z);
                extent = MathF.Max(extent, 0.01f);
            }
            extent += _arrayGap;

            for (int step = 1; step < _arrayCount; step++)
            {
                var (ex, ey, ez) = DecomposeXyzEulerDegrees(baseLocal);
                var rot = _arrayRotationDeg * step;
                float nrx = (ex + rot.X) * MathF.PI / 180f;
                float nry = (ey + rot.Y) * MathF.PI / 180f;
                float nrz = (ez + rot.Z) * MathF.PI / 180f;
                var newRot = Matrix4x4.CreateRotationX(nrx) * Matrix4x4.CreateRotationY(nry) * Matrix4x4.CreateRotationZ(nrz);
                var newPos = basePos + dir * extent * step;

                newRot.Translation = newPos;
                var ghost = CloneVisualOnly(original, $"{original.Name}_arraypreview{step}");
                ghost.Add(new ArrayPreviewMarker());
                ghost.Transform.LocalMatrix = newRot;
                parent.AddChild(ghost);
                _arrayPreviewGhosts.Add((parent, ghost));
            }
        }
    }

    private static Entity CloneVisualOnly(Entity source, string name)
    {
        var clone = new Entity(name);
        void Copy(Entity src, Entity dst)
        {
            foreach (var c in src.Children)
            {
                var childClone = new Entity(c.Name);
                if (c.Get<CrashEngine.Importer.MeshRenderer>() is { } mr)
                    childClone.Add(new CrashEngine.Importer.MeshRenderer { Mesh = mr.Mesh, Material = mr.Material });
                childClone.Transform.LocalMatrix = c.Transform.LocalMatrix;
                childClone.Transform.Position    = c.Transform.Position;
                childClone.Transform.Rotation    = c.Transform.Rotation;
                childClone.Transform.Scale       = c.Transform.Scale;
                dst.AddChild(childClone);
                Copy(c, childClone);
            }
        }
        Copy(source, clone);
        return clone;
    }
}
