using CrashEngine.Core;
using ImGuiNET;
using Silk.NET.Input;
using System.Numerics;
using H = CrashLauncher.CollisionShapeHandles;

namespace CrashLauncher;

// Amedo 2026-11-19
public sealed partial class GameScene
{
    private const float CollisionHandlePickPixels = 9f, CollisionEdgePickPixels = 5f, CollisionMinSize = 0.05f;
    private const float CollisionHandleRadius = 4.5f, CollisionHandleHoverRadius = 6.5f;
    private const double CollisionOutlineRefreshSeconds = 0.5;
    private const int CollisionHandleShapeLimit = 8, CollisionOutlineEdgeLimit = 20000, CylinderAllSidesUpTo = 16;
    private const uint CollisionOutlineColor = 0xFF8BF491, CollisionHandleBorder = 0xFF285A2A, CollisionHandleHot = 0xFFFFFFFF;

    private bool _collisionOutlinesDirty = true;
    private double _collisionOutlinesAt = double.NegativeInfinity;
    private readonly List<Entity> _collisionOutlined = new();
    private readonly Dictionary<GeneratedCollisionMarker, (int A, int B)[]> _generatedEdges = new(ReferenceEqualityComparer.Instance);
    private CollisionHandleDrag? _collisionDrag;

    private readonly record struct CollisionHandle(Entity Entity, int Face, Vector3 LocalPoint);

    private sealed class CollisionHandleDrag
    {
        public required Entity Entity;
        public required int Face;
        public required CollisionShapeState Before;
        public Vector2 PrevMouse;
    }

    private sealed class CollisionShapeState
    {
        public Vector3[]? Corners;
        public float A, B;
        public TransformSnapshot Transform;
    }

    private sealed class CollisionShapeEditAction : IEditAction
    {
        public required GameScene Scene;
        public required Entity Entity;
        public required CollisionShapeState Before, After;

        public void Undo() => Scene.ApplyCollisionShapeState(Entity, Before);
        public void Redo() => Scene.ApplyCollisionShapeState(Entity, After);
        public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
    }

    private void RefreshCollisionOutlines()
    {
        double now = EngineTime.Total;
        if (!_collisionOutlinesDirty && now - _collisionOutlinesAt < CollisionOutlineRefreshSeconds) return;
        _collisionOutlinesDirty = false;
        _collisionOutlinesAt = now;
        _collisionOutlined.Clear();
        var seen = new HashSet<Entity>();
        foreach (var sel in _selectionOrder)
            foreach (var e in AllEntities(sel))
                if (e.Get<CollisionShapeMarker>() is not null && seen.Add(e)) _collisionOutlined.Add(e);
    }

    private bool CollisionOverlayActive => _workspace == Workspace.Scene && !PlayFullView && _camera is not null;

    private void DrawCollisionOutlines()
    {
        if (!CollisionOverlayActive) return;
        RefreshCollisionOutlines();
        if (_collisionOutlined.Count == 0) return;
        int w = ViewW, h = ViewH;
        var draw = ImGui.GetForegroundDrawList();
        draw.PushClipRect(ViewOrigin, ViewOrigin + new Vector2(w, h));
        int budget = CollisionOutlineEdgeLimit;
        foreach (var e in _collisionOutlined)
            if (e.Parent is not null)
                foreach (var (a, b) in CollisionShapeEdges(e, ref budget))
                    if (WorldToScreen(a, w, h, out var sa) && WorldToScreen(b, w, h, out var sb)) draw.AddLine(sa, sb, CollisionOutlineColor, 1.5f);
        (Entity? Entity, int Face) hot = _collisionDrag is { } drag ? (drag.Entity, drag.Face)
            : HoveredCollisionHandle(Input.MousePosition, w, h) is { } hover ? (hover.Entity, hover.Face) : (null, -1);
        foreach (var handle in VisibleCollisionHandles())
        {
            if (!WorldToScreen(Vector3.Transform(handle.LocalPoint, handle.Entity.Transform.World), w, h, out var sp)) continue;
            bool isHot = ReferenceEquals(handle.Entity, hot.Entity) && handle.Face == hot.Face;
            draw.AddCircleFilled(sp, isHot ? CollisionHandleHoverRadius : CollisionHandleRadius, isHot ? CollisionHandleHot : CollisionOutlineColor);
            draw.AddCircle(sp, isHot ? CollisionHandleHoverRadius : CollisionHandleRadius, CollisionHandleBorder);
        }
        draw.PopClipRect();
    }

    private IEnumerable<(Vector3 A, Vector3 B)> CollisionShapeEdges(Entity e, ref int budget)
    {
        var m = e.Transform.World;
        var lines = new List<(Vector3, Vector3)>();
        void Add(Vector3 a, Vector3 b) => lines.Add((Vector3.Transform(a, m), Vector3.Transform(b, m)));
        switch (e.Get<CollisionShapeMarker>())
        {
            case CollisionBoxMarker box:
                foreach (var (a, b) in H.BoxEdges) Add(box.Corners[a], box.Corners[b]);
                break;
            case CollisionCylinderMarker cyl:
            {
                int n = Math.Max(3, cyl.Segments);
                Vector3 Ring(int i, float y) => new(MathF.Cos(MathF.Tau * i / n) * cyl.Radius, y, MathF.Sin(MathF.Tau * i / n) * cyl.Radius);
                for (int i = 0; i < n; i++)
                {
                    Add(Ring(i, 0f), Ring(i + 1, 0f));
                    Add(Ring(i, cyl.Height), Ring(i + 1, cyl.Height));
                    if (n <= CylinderAllSidesUpTo || i % (n / 4) == 0) Add(Ring(i, 0f), Ring(i, cyl.Height));
                }
                break;
            }
            case CollisionPlaneMarker plane:
            {
                float hw = plane.Width * 0.5f, hl = plane.Length * 0.5f;
                Vector3 c0 = new(-hw, 0f, -hl), c1 = new(hw, 0f, -hl), c2 = new(hw, 0f, hl), c3 = new(-hw, 0f, hl);
                Add(c0, c1); Add(c1, c2); Add(c2, c3); Add(c3, c0);
                break;
            }
            case GeneratedCollisionMarker gen:
                foreach (var (a, b) in GeneratedEdges(gen))
                {
                    if (lines.Count >= budget) break;
                    if (a < gen.LocalVertices.Count && b < gen.LocalVertices.Count) Add(gen.LocalVertices[a], gen.LocalVertices[b]);
                }
                break;
        }
        if (lines.Count > budget) lines.RemoveRange(budget, lines.Count - budget);
        budget -= lines.Count;
        return lines;
    }

    private (int A, int B)[] GeneratedEdges(GeneratedCollisionMarker gen)
    {
        if (_generatedEdges.TryGetValue(gen, out var cached)) return cached;
        var set = new HashSet<(int, int)>();
        foreach (var (a, b, c) in gen.LocalTriangles)
        {
            set.Add((Math.Min(a, b), Math.Max(a, b)));
            set.Add((Math.Min(b, c), Math.Max(b, c)));
            set.Add((Math.Min(c, a), Math.Max(c, a)));
        }
        return _generatedEdges[gen] = set.ToArray();
    }

    private static List<CollisionHandle> CollisionHandlesOf(Entity e)
    {
        var list = new List<CollisionHandle>();
        switch (e.Get<CollisionShapeMarker>())
        {
            case CollisionBoxMarker box:
                for (int f = 0; f < H.Faces; f++) list.Add(new(e, f, H.FaceCenter(box.Corners, f)));
                break;
            case CollisionCylinderMarker cyl:
                for (int f = 0; f < H.Faces; f++)
                    list.Add(new(e, f, H.Axis(f) == 1 ? new Vector3(0f, H.Positive(f) ? cyl.Height : 0f, 0f) : H.Outward(f) * cyl.Radius + new Vector3(0f, cyl.Height * 0.5f, 0f)));
                break;
            case CollisionPlaneMarker plane:
                for (int f = 0; f < H.Faces; f++)
                    if (H.Axis(f) != 1) list.Add(new(e, f, H.Outward(f) * (H.Axis(f) == 0 ? plane.Width : plane.Length) * 0.5f));
                break;
        }
        return list;
    }

    private IEnumerable<CollisionHandle> VisibleCollisionHandles()
    {
        if (_collisionOutlined.Count > CollisionHandleShapeLimit) yield break;
        foreach (var e in _collisionOutlined)
            if (e.Parent is not null)
                foreach (var handle in CollisionHandlesOf(e)) yield return handle;
    }

    private CollisionHandle? HoveredCollisionHandle(Vector2 mouse, int w, int h)
    {
        if (!MouseInView(mouse)) return null;
        CollisionHandle? best = null;
        float bestDistance = CollisionHandlePickPixels;
        foreach (var handle in VisibleCollisionHandles())
        {
            if (!WorldToScreen(Vector3.Transform(handle.LocalPoint, handle.Entity.Transform.World), w, h, out var sp)) continue;
            float d = Vector2.Distance(sp, mouse);
            if (d < bestDistance) { bestDistance = d; best = handle; }
        }
        return best;
    }

    private bool CollisionOutlineClick(Vector2 mouse, int w, int h)
    {
        if (!CollisionOverlayActive) return false;
        RefreshCollisionOutlines();
        if (_collisionOutlined.Count == 0) return false;
        if (HoveredCollisionHandle(mouse, w, h) is { } handle)
        {
            _collisionDrag = new CollisionHandleDrag { Entity = handle.Entity, Face = handle.Face, Before = CaptureCollisionShapeState(handle.Entity), PrevMouse = mouse };
            return true;
        }
        int budget = CollisionOutlineEdgeLimit;
        foreach (var e in _collisionOutlined)
        {
            if (e.Parent is null) continue;
            foreach (var (a, b) in CollisionShapeEdges(e, ref budget))
            {
                if (!WorldToScreen(a, w, h, out var sa) || !WorldToScreen(b, w, h, out var sb) || DistancePointToSegment(mouse, sa, sb) > CollisionEdgePickPixels) continue;
                SelectClicked(e, Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight));
                _revealSelectionInTree = true;
                return true;
            }
        }
        return false;
    }

    private void UpdateCollisionHandleDrag(bool held, Vector2 mouse, int w, int h)
    {
        if (_collisionDrag is not { } drag) return;
        if (!held || drag.Entity.Parent is null || _camera is null)
        {
            FinishCollisionHandleDrag();
            return;
        }
        var delta = mouse - drag.PrevMouse;
        drag.PrevMouse = mouse;
        if (delta.LengthSquared() < 0.01f) return;
        var world = drag.Entity.Transform.World;
        var handle = CollisionHandlesOf(drag.Entity).FirstOrDefault(x => x.Face == drag.Face);
        if (handle.Entity is null) return;
        var point = Vector3.Transform(handle.LocalPoint, world);
        var dir = Vector3.TransformNormal(H.Outward(drag.Face), world);
        float unit = dir.Length();
        if (unit < 1e-6f) return;
        dir /= unit;
        if (!WorldToScreen(point, w, h, out var sPoint) || !WorldToScreen(point + dir, w, h, out var sTip)) return;
        var sAxis = sTip - sPoint;
        if (sAxis.LengthSquared() < 1e-4f) return;
        float pixels = Vector2.Dot(delta, Vector2.Normalize(sAxis));
        float worldPerPixel = _camera.WorldPerPixel(Vector3.Distance(_camera.Transform.Position, point), h);
        MoveCollisionFace(drag.Entity, drag.Face, pixels * worldPerPixel / unit);
    }

    private void MoveCollisionFace(Entity e, int face, float outward)
    {
        var world = e.Transform.World;
        switch (e.Get<CollisionShapeMarker>())
        {
            case CollisionBoxMarker box:
                H.MoveBoxFace(box.Corners, face, outward, CollisionMinSize);
                RebuildCollisionBoxMesh(e, box);
                break;
            case CollisionCylinderMarker cyl:
                if (H.Axis(face) == 1)
                {
                    float grown = H.Grow(ref cyl.Height, outward, CollisionMinSize);
                    if (!H.Positive(face)) ApplyWorldDelta(e, Vector3.TransformNormal(new Vector3(0f, -grown, 0f), world));
                }
                else H.Grow(ref cyl.Radius, outward, CollisionMinSize);
                RebuildCollisionCylinderMesh(e, cyl);
                break;
            case CollisionPlaneMarker plane:
            {
                float grown = H.Axis(face) == 0 ? H.Grow(ref plane.Width, outward, CollisionMinSize) : H.Grow(ref plane.Length, outward, CollisionMinSize);
                ApplyWorldDelta(e, Vector3.TransformNormal(H.Outward(face) * (grown * 0.5f), world));
                RebuildCollisionPlaneMesh(e, plane);
                break;
            }
        }
    }

    private void FinishCollisionHandleDrag()
    {
        if (_collisionDrag is not { } drag) return;
        _collisionDrag = null;
        if (drag.Entity.Parent is null) return;
        var after = CaptureCollisionShapeState(drag.Entity);
        if (SameCollisionShapeState(drag.Before, after)) return;
        _undoStack.Push(new CollisionShapeEditAction { Scene = this, Entity = drag.Entity, Before = drag.Before, After = after });
        _browser.Log($"{drag.Entity.Name}: resized. Save Chunk to write it into the collision data.");
    }

    private static CollisionShapeState CaptureCollisionShapeState(Entity e)
    {
        var state = new CollisionShapeState { Transform = new TransformSnapshot(e.Transform) };
        switch (e.Get<CollisionShapeMarker>())
        {
            case CollisionBoxMarker box: state.Corners = (Vector3[])box.Corners.Clone(); break;
            case CollisionCylinderMarker cyl: state.A = cyl.Radius; state.B = cyl.Height; break;
            case CollisionPlaneMarker plane: state.A = plane.Width; state.B = plane.Length; break;
        }
        return state;
    }

    private static bool SameCollisionShapeState(CollisionShapeState x, CollisionShapeState y) =>
        x.Transform.Equals(in y.Transform) && x.A == y.A && x.B == y.B &&
        (x.Corners is null ? y.Corners is null : y.Corners is not null && x.Corners.AsSpan().SequenceEqual(y.Corners));

    private void ApplyCollisionShapeState(Entity e, CollisionShapeState state)
    {
        state.Transform.ApplyTo(e.Transform);
        switch (e.Get<CollisionShapeMarker>())
        {
            case CollisionBoxMarker box when state.Corners is not null:
                Array.Copy(state.Corners, box.Corners, 8);
                RebuildCollisionBoxMesh(e, box);
                break;
            case CollisionCylinderMarker cyl:
                cyl.Radius = state.A; cyl.Height = state.B;
                RebuildCollisionCylinderMesh(e, cyl);
                break;
            case CollisionPlaneMarker plane:
                plane.Width = state.A; plane.Length = state.B;
                RebuildCollisionPlaneMesh(e, plane);
                break;
        }
    }
}
