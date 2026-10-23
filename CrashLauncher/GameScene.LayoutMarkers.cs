using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using PS2AnyAIPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPath;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinBoundingBoxBuilder;
using TwinChunkLinkBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinChunkLinkBoundingBoxBuilder;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using TwinTrigger = Twinsanity.TwinsanityInterchange.Common.TwinTrigger;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{

    private Vector3 GizmoPivot()
    {
        if (_selectedSet.Count == 0) return _selected?.Transform.World.Translation ?? Vector3.Zero;
        var sum = Vector3.Zero;
        foreach (var e in _selectedSet) sum += e.Transform.World.Translation;
        return sum / _selectedSet.Count;
    }

    private float GizmoScale()
    {
        if (_camera is null || _selected is null) return 1f;
        float dist = Vector3.Distance(_camera.Transform.Position, GizmoPivot());
        return MathF.Max(dist * 0.055f, 0.5f);
    }

    private int GetGizmoHoverAxis(Vector2 mousePos, int sw, int sh)
    {
        if (_selected is null) return -1;
        var   gPos = GizmoPivot();
        float tipW = TipLen * GizmoScale();
        var tips = new[]
        {
            gPos + new Vector3(tipW, 0,    0),
            gPos + new Vector3(0,    tipW, 0),
            gPos + new Vector3(0,    0,    tipW),
        };
        for (int i = 0; i < 3; i++)
        {
            if (!WorldToScreen(tips[i], sw, sh, out var sp)) continue;
            if (Vector2.Distance(sp, mousePos) < 18f) return i;
        }
        return -1;
    }

    private int GetDuplicateHoverDir(Vector2 mousePos, int sw, int sh)
    {
        float tipW = TipLen * GizmoScale();
        for (int i = 0; i < SixDirs.Length; i++)
        {
            if (!WorldToScreen(_dupGizmoPivot + SixDirs[i] * tipW, sw, sh, out var sp)) continue;
            if (Vector2.Distance(sp, mousePos) < 18f) return i;
        }
        return -1;
    }

    private static Vector3[] RingPoints(Vector3 center, Vector3 axis, float radius, int segments = 48)
    {
        var arbitrary = MathF.Abs(Vector3.Dot(axis, Vector3.UnitY)) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        var u = Vector3.Normalize(Vector3.Cross(axis, arbitrary));
        var v = Vector3.Cross(axis, u);
        var pts = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments * MathF.Tau;
            pts[i] = center + (u * MathF.Cos(t) + v * MathF.Sin(t)) * radius;
        }
        return pts;
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab   = b - a;
        float l2 = ab.LengthSquared();
        if (l2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    private const float ViewRingScale = 1.15f;

    private Vector3 RotateRingAxis(int index) => index switch
    {
        0 => Vector3.UnitX,
        1 => Vector3.UnitY,
        2 => Vector3.UnitZ,
        _ => _camera is not null ? _camera.Forward : Vector3.UnitZ,
    };

    private int GetRotateHoverAxis(Vector2 mousePos, int sw, int sh)
    {
        if (_selected is null) return -1;
        var   gPos   = GizmoPivot();
        float radius = TipLen * GizmoScale();

        int best = -1; float bestDist = 10f;
        for (int a = 0; a < 4; a++)
        {
            var pts = RingPoints(gPos, RotateRingAxis(a), a == 3 ? radius * ViewRingScale : radius);
            Vector2? prev = null;
            for (int i = 0; i <= pts.Length; i++)
            {
                if (!WorldToScreen(pts[i % pts.Length], sw, sh, out var sp)) { prev = null; continue; }
                if (prev is { } pv)
                {
                    float d = DistancePointToSegment(mousePos, pv, sp);
                    if (d < bestDist) { bestDist = d; best = a; }
                }
                prev = sp;
            }
        }
        return best;
    }

    private void UpdateGizmoDrag(Vector2 mousePos, int sw, int sh)
    {
        if (_selected is null || _camera is null) return;
        var delta = mousePos - _gizmoPrevMouse;
        _gizmoPrevMouse = mousePos;
        if (delta.LengthSquared() < 0.01f) return;

        var worldAxis = _gizmoAxis switch
        {
            0 => Vector3.UnitX,
            1 => Vector3.UnitY,
            _ => Vector3.UnitZ,
        };

        var objWorldPos = GizmoPivot();
        if (!WorldToScreen(objWorldPos, sw, sh, out var sObj) ||
            !WorldToScreen(objWorldPos + worldAxis, sw, sh, out var sTip))
            return;

        var   sAxis    = Vector2.Normalize(sTip - sObj);
        float movement = Vector2.Dot(delta, sAxis);
        float dist     = Vector3.Distance(_camera.Transform.Position, objWorldPos);
        float wpx      = _camera.WorldPerPixel(dist, sh); // Amedo 2026-10-04

        var worldDelta = worldAxis * movement * wpx;

        foreach (var ent in _selectedSet)
            ApplyWorldDelta(ent, worldDelta);
    }

    private void UpdateRotateDrag(Vector2 mousePos, int sw, int sh)
    {
        if (_selected is null || _camera is null) return;
        var gPos = GizmoPivot();
        if (!WorldToScreen(gPos, sw, sh, out var sc)) return;

        var prevVec = _gizmoPrevMouse - sc;
        var curVec  = mousePos - sc;
        _gizmoPrevMouse = mousePos;
        if (prevVec.LengthSquared() < 4f || curVec.LengthSquared() < 4f) return;

        float prevAngle = MathF.Atan2(prevVec.Y, prevVec.X);
        float curAngle  = MathF.Atan2(curVec.Y, curVec.X);
        float delta     = curAngle - prevAngle;
        while (delta >  MathF.PI) delta -= MathF.Tau;
        while (delta < -MathF.PI) delta += MathF.Tau;
        delta = -delta;

        var worldAxis = RotateRingAxis(_gizmoAxis);

        float facing = Vector3.Dot(worldAxis, Vector3.Normalize(_camera.Transform.Position - gPos));
        float angle  = facing < 0f ? -delta : delta;

        foreach (var ent in _selectedSet)
            ApplyWorldRotation(ent, gPos, worldAxis, angle);
    }

    private static void ApplyWorldDelta(Entity ent, Vector3 worldDelta)
    {
        var localDelta = worldDelta;
        if (ent.Transform.Parent is not null)
        {
            Matrix4x4.Invert(ent.Transform.Parent.World, out var invParent);
            localDelta = Vector3.TransformNormal(worldDelta, invParent);
        }

        if (ent.Transform.LocalMatrix is { } lm)
        {
            lm.M41 += localDelta.X;
            lm.M42 += localDelta.Y;
            lm.M43 += localDelta.Z;
            ent.Transform.LocalMatrix = lm;
        }
        else
        {
            ent.Transform.Position += localDelta;
        }
    }

    private static void ApplyWorldRotation(Entity ent, Vector3 pivotWorld, Vector3 axisWorld, float angleRad)
    {
        var worldBefore = ent.Transform.World;
        var rot          = Matrix4x4.CreateFromAxisAngle(axisWorld, angleRad);
        var newWorld     = worldBefore
                          * Matrix4x4.CreateTranslation(-pivotWorld)
                          * rot
                          * Matrix4x4.CreateTranslation(pivotWorld);

        var newLocal = newWorld;
        if (ent.Transform.Parent is not null && Matrix4x4.Invert(ent.Transform.Parent.World, out var invParent))
            newLocal = newWorld * invParent;

        if (ent.Transform.LocalMatrix is not null)
        {
            ent.Transform.LocalMatrix = newLocal;
        }
        else if (Matrix4x4.Decompose(newLocal, out var scale, out var rotation, out var translation))
        {
            ent.Transform.Position = translation;
            ent.Transform.Rotation = rotation;
            ent.Transform.Scale    = scale;
        }
        else
        {
            ent.Transform.LocalMatrix = newLocal;
        }
    }

    private static Vector3 MatrixToEulerXYZ(Matrix4x4 m)
    {
        float y = MathF.Asin(Math.Clamp(-m.M13, -1f, 1f));
        float x = MathF.Atan2(m.M23, m.M33);
        float z = MathF.Atan2(m.M12, m.M11);
        return new Vector3(x, y, z);
    }

    private static Quaternion EulerXYZToQuaternion(Vector3 eulerRad) =>
        Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateRotationX(eulerRad.X) * Matrix4x4.CreateRotationY(eulerRad.Y) * Matrix4x4.CreateRotationZ(eulerRad.Z));

    private static bool TryGetWorldAABB(Entity root, out Vector3 min, out Vector3 max)
    {
        var lo  = new Vector3(float.MaxValue);
        var hi  = new Vector3(float.MinValue);
        bool any = false;

        void Collect(Entity e)
        {
            var mr = e.Get<CrashEngine.Importer.MeshRenderer>();
            if (mr?.Mesh?.RaycastPositions is { } pts)
            {
                var world = e.Transform.World;
                foreach (var p in pts)
                {
                    var wp = Vector3.Transform(p, world);
                    lo  = Vector3.Min(lo, wp);
                    hi  = Vector3.Max(hi, wp);
                    any = true;
                }
            }
            foreach (var c in e.Children) Collect(c);
        }
        Collect(root);
        min = lo; max = hi;
        return any;
    }

    private void ExecuteGlue(Entity source, Entity target)
    {
        if (!TryGetWorldAABB(source, out var sMin, out var sMax)) return;
        if (!TryGetWorldAABB(target, out var tMin, out var tMax)) return;

        var sCenter  = (sMin + sMax) * 0.5f;
        var tCenter  = (tMin + tMax) * 0.5f;
        var toTarget = tCenter - sCenter;

        int axis;
        if (_glueAxisLock >= 0) axis = _glueAxisLock;
        else
        {
            var abs = new Vector3(MathF.Abs(toTarget.X), MathF.Abs(toTarget.Y), MathF.Abs(toTarget.Z));
            axis = abs.X >= abs.Y && abs.X >= abs.Z ? 0 : (abs.Y >= abs.Z ? 1 : 2);
        }

        float delta = axis switch
        {
            0 => toTarget.X >= 0 ? tMin.X - sMax.X : tMax.X - sMin.X,
            1 => toTarget.Y >= 0 ? tMin.Y - sMax.Y : tMax.Y - sMin.Y,
            _ => toTarget.Z >= 0 ? tMin.Z - sMax.Z : tMax.Z - sMin.Z,
        };
        var worldDelta = axis switch
        {
            0 => new Vector3(delta, 0f, 0f),
            1 => new Vector3(0f, delta, 0f),
            _ => new Vector3(0f, 0f, delta),
        };

        var before = new TransformSnapshot(source.Transform);
        ApplyWorldDelta(source, worldDelta);
        var after = new TransformSnapshot(source.Transform);
        if (!after.Equals(before))
            _undoStack.Push(new TransformEditAction { Entity = source, Before = before, After = after });
    }

    private Entity? RaycastScene(Vector2 mousePos, int sw, int sh)
    {
        var (orig, dir) = GetMouseRay(mousePos, sw, sh);
        Entity? best  = null;
        float   bestT = float.MaxValue;
        Entity? bestCollision  = null;
        float   bestCollisionT = float.MaxValue;

        foreach (var c in _cubes)
        {
            var p  = c.Ent.Transform.Position;
            var mn = p + new Vector3(-1f, 0f, -1f);
            var mx = p + new Vector3( 1f, 2f,  1f);
            if (RayAABB(orig, dir, mn, mx, out float t) && t < bestT)
            { bestT = t; best = c.Ent; }
        }

        void Visit(Entity e)
        {
            if (!e.Active) return;
            if (e.Has<ArrayPreviewMarker>()) return;
            if ((e.Has<InstanceData>() || e.Has<CrashEngine.Importer.SceneryTile>() || e.Has<CrashEngine.Importer.CollisionMesh>()
                 || e.Has<CrashEngine.Importer.LinkedSceneryLink>()
                 || e.Get<CrashEngine.Importer.MeshRenderer>() is not null
                 || e.Children.Any(c => c.Get<CrashEngine.Importer.MeshRenderer>() is not null)) &&
                TryGetWorldBounds(e, out var center, out var radius) &&
                RaySphere(orig, dir, center, radius, out _))
            {
                bool isCollision = e.Has<CrashEngine.Importer.CollisionMesh>();
                void CheckMesh(Entity m)
                {
                    var mr = m.Get<CrashEngine.Importer.MeshRenderer>();
                    if (mr is not null && RaycastMeshRenderer(mr, orig, dir, out float wt))
                    {
                        if (isCollision) { if (wt < bestCollisionT) { bestCollisionT = wt; bestCollision = e; } }
                        else             { if (wt < bestT)          { bestT          = wt; best          = e; } }
                    }
                    foreach (var mc in m.Children) CheckMesh(mc);
                }
                CheckMesh(e);
            }
            foreach (var child in e.Children) Visit(child);
        }
        foreach (var root in Roots) Visit(root);

        void VisitMarkerRoot(Entity? root)
        {
            if (root is null || !root.Active) return;
            foreach (var m in root.Children)
            {
                if (!m.Active) continue;
                if (!(m.Has<CrashEngine.Importer.TriggerMarker>() || m.Has<CrashEngine.Importer.CameraMarker>() ||
                      m.Has<CrashEngine.Importer.PositionMarker>() || m.Has<CrashEngine.Importer.AiPositionMarker>() ||
                      m.Has<CrashEngine.Importer.ParticleEmitterMarker>() || m.Has<CrashEngine.Importer.SceneryLightMarker>())) // Amedo 2026-09-19
                    continue;
                var p = m.Transform.Position;
                var s = m.Transform.Scale;
                Vector3 mn, mx;
                if (m.Has<CrashEngine.Importer.SceneryLightMarker>())
                {
                    // Amedo 2026-09-19
                    const float R = 1.7f;
                    mn = p + new Vector3(-R, -R, -R);
                    mx = p + new Vector3( R,  R,  R);
                }
                else if (m.Has<CrashEngine.Importer.AiPositionMarker>() && IsAiIconMarker(m))
                {
                    // Amedo
                    if (RayHitsAiIcon(m.Children[0], orig, dir, out float ti) && ti < bestT) { bestT = ti; best = m; }
                    continue;
                }
                else
                {
                    mn = p + new Vector3(-s.X, 0f, -s.Z);
                    mx = p + new Vector3( s.X, 2f * s.Y, s.Z);
                }
                if (RayAABB(orig, dir, mn, mx, out float t) && t < bestT) { bestT = t; best = m; }
            }
        }
        VisitMarkerRoot(_triggersRoot);
        VisitMarkerRoot(_camerasRoot);
        VisitMarkerRoot(_positionsRoot);
        VisitMarkerRoot(_aiPosRoot);
        VisitMarkerRoot(_particleEmittersRoot);
        VisitMarkerRoot(_lightsRoot); // Amedo 2026-09-19

        return bestCollision ?? best;
    }

    private static bool RaycastMeshRenderer(CrashEngine.Importer.MeshRenderer mr, Vector3 worldOrig, Vector3 worldDir, out float worldT)
    {
        worldT = float.MaxValue;
        if (mr.Mesh is null || mr.Entity is null) return false;
        var world = mr.Entity.Transform.World;
        if (!Matrix4x4.Invert(world, out var inv)) return false;

        var localOrig = Vector3.Transform(worldOrig, inv);
        var localDir  = Vector3.TransformNormal(worldDir, inv);
        if (!mr.Mesh.RaycastTriangles(localOrig, localDir, out float localT)) return false;

        var worldHit = Vector3.Transform(localOrig + localDir * localT, world);
        worldT = Vector3.Distance(worldOrig, worldHit);
        return true;
    }

    private static bool TryGetWorldBounds(Entity instanceRoot, out Vector3 center, out float radius)
    {
        var centers = new List<Vector3>();
        var radii   = new List<float>();

        void Collect(Entity e)
        {
            var mr = e.Get<CrashEngine.Importer.MeshRenderer>();
            if (mr?.Material?.LocalCenter is { } lc)
            {
                var world = e.Transform.World;
                centers.Add(Vector3.Transform(lc, world));
                var scale = Vector3.TransformNormal(Vector3.One, world).Length() / MathF.Sqrt(3f);
                radii.Add(mr.Material.BoundingRadius * MathF.Max(scale, 0.01f));
            }
            foreach (var child in e.Children) Collect(child);
        }
        Collect(instanceRoot);

        if (centers.Count == 0)
        {
            center = instanceRoot.Transform.World.Translation;
            radius = 0f;
            return false;
        }

        center = Vector3.Zero;
        foreach (var c in centers) center += c;
        center /= centers.Count;

        radius = 0f;
        for (int i = 0; i < centers.Count; i++)
            radius = MathF.Max(radius, Vector3.Distance(center, centers[i]) + radii[i]);
        radius = MathF.Max(radius, 0.3f);
        return true;
    }

    private static bool RaySphere(Vector3 orig, Vector3 dir, Vector3 center, float radius, out float t)
    {
        t = 0f;
        var oc   = orig - center;
        float b  = Vector3.Dot(oc, dir);
        float c  = Vector3.Dot(oc, oc) - radius * radius;
        float disc = b * b - c;
        if (disc < 0f) return false;
        float sq = MathF.Sqrt(disc);
        float t0 = -b - sq, t1 = -b + sq;
        t = t0 > 0f ? t0 : t1;
        return t > 0f;
    }

    private (Vector3 orig, Vector3 dir) GetMouseRay(Vector2 mp, int sw, int sh)
    {
        if (_camera is null) return (Vector3.Zero, Vector3.UnitZ);
        mp -= ViewOrigin; // Amedo
        float nx = (2f * mp.X / sw) - 1f;
        float ny = 1f - (2f * mp.Y / sh);
        Matrix4x4.Invert(_camera.Projection, out var invP);
        Matrix4x4.Invert(_camera.View,       out var invV);
        var n4 = new Vector4(nx, ny, -1f, 1f);
        var f4 = new Vector4(nx, ny,  1f, 1f);
        var nv = Vector4.Transform(n4, invP); nv /= nv.W;
        var fv = Vector4.Transform(f4, invP); fv /= fv.W;
        var nw = Vector4.Transform(nv, invV);
        var fw = Vector4.Transform(fv, invV);
        var orig = new Vector3(nw.X, nw.Y, nw.Z);
        return (orig, Vector3.Normalize(new Vector3(fw.X, fw.Y, fw.Z) - orig));
    }

    private static bool RayAABB(Vector3 o, Vector3 d,
                                 Vector3 mn, Vector3 mx, out float t)
    {
        t = 0f;
        float tmin = float.MinValue, tmax = float.MaxValue;
        if (!Slab(o.X, d.X, mn.X, mx.X, ref tmin, ref tmax)) return false;
        if (!Slab(o.Y, d.Y, mn.Y, mx.Y, ref tmin, ref tmax)) return false;
        if (!Slab(o.Z, d.Z, mn.Z, mx.Z, ref tmin, ref tmax)) return false;
        t = tmin > 0f ? tmin : tmax;
        return t > 0f;
    }

    private static bool Slab(float o, float d, float mn, float mx,
                              ref float tmin, ref float tmax)
    {
        if (MathF.Abs(d) > 1e-6f)
        {
            float t1 = (mn - o) / d, t2 = (mx - o) / d;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tmin = MathF.Max(tmin, t1);
            tmax = MathF.Min(tmax, t2);
            return tmin <= tmax;
        }
        return o >= mn && o <= mx;
    }

    private void EnsureCubeMesh(GL gl)
    {
        if (_cubeMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float z, Vector3 n, float u, float v) =>
            new() { Position = new(x, y, z), Normal = n, UV = new(u, v), Color = w };
        var U = Vector3.UnitY;  var D = -Vector3.UnitY;
        var F = Vector3.UnitZ;  var B = -Vector3.UnitZ;
        var R = Vector3.UnitX;  var L = -Vector3.UnitX;
        _cubeMesh = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-1,2,-1,U,0,0), V( 1,2,-1,U,1,0), V( 1,2, 1,U,1,1),
            V( 1,2, 1,U,1,1), V(-1,2, 1,U,0,1), V(-1,2,-1,U,0,0),
            V(-1,0,-1,D,0,0), V( 1,0, 1,D,1,1), V( 1,0,-1,D,1,0),
            V( 1,0, 1,D,1,1), V(-1,0,-1,D,0,0), V(-1,0, 1,D,0,1),
            V(-1,0,1,F,0,1), V( 1,2,1,F,1,0), V(-1,2,1,F,0,0),
            V(-1,0,1,F,0,1), V( 1,0,1,F,1,1), V( 1,2,1,F,1,0),
            V(-1,0,-1,B,1,1), V(-1,2,-1,B,1,0), V( 1,2,-1,B,0,0),
            V(-1,0,-1,B,1,1), V( 1,2,-1,B,0,0), V( 1,0,-1,B,0,1),
            V(1,0,-1,R,0,1), V(1,2, 1,R,1,0), V(1,0, 1,R,1,1),
            V(1,0,-1,R,0,1), V(1,2,-1,R,0,0), V(1,2, 1,R,1,0),
            V(-1,0,-1,L,1,1), V(-1,0, 1,L,0,1), V(-1,2, 1,L,0,0),
            V(-1,0,-1,L,1,1), V(-1,2, 1,L,0,0), V(-1,2,-1,L,1,0),
        }, PrimitiveType.Triangles);
    }

    private void EnsureCubeWireMesh(GL gl)
    {
        if (_cubeWireMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float z) =>
            new() { Position = new(x, y, z), Normal = Vector3.UnitY, UV = Vector2.Zero, Color = w };
        void Edge(List<GpuMesh.Vertex> v, (float x, float y, float z) a, (float x, float y, float z) b)
        {
            v.Add(V(a.x, a.y, a.z));
            v.Add(V(b.x, b.y, b.z));
        }
        var bl = new List<GpuMesh.Vertex>();
        Edge(bl, (-1,0,-1), ( 1,0,-1)); Edge(bl, ( 1,0,-1), ( 1,0, 1));
        Edge(bl, ( 1,0, 1), (-1,0, 1)); Edge(bl, (-1,0, 1), (-1,0,-1));
        Edge(bl, (-1,2,-1), ( 1,2,-1)); Edge(bl, ( 1,2,-1), ( 1,2, 1));
        Edge(bl, ( 1,2, 1), (-1,2, 1)); Edge(bl, (-1,2, 1), (-1,2,-1));
        Edge(bl, (-1,0,-1), (-1,2,-1)); Edge(bl, ( 1,0,-1), ( 1,2,-1));
        Edge(bl, ( 1,0, 1), ( 1,2, 1)); Edge(bl, (-1,0, 1), (-1,2, 1));
        _cubeWireMesh = new GpuMesh(gl, bl.ToArray(), PrimitiveType.Lines);
    }

    // Amedo 2026-09-19
    private GpuMesh? _lightIconQuad;
    private void EnsureLightIconQuad(GL gl)
    {
        if (_lightIconQuad is not null) return;
        var col = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float u, float v, Vector3 n) => new() { Position = new(x, y, 0f), Normal = n, UV = new(u, v), Color = col };
        var F = Vector3.UnitZ; var B = -Vector3.UnitZ;
        const float s = 1.6f;
        _lightIconQuad = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-s,-s, 0f,1f, F), V(s,-s, 1f,1f, F), V(s,s, 1f,0f, F),
            V(-s,-s, 0f,1f, F), V(s,s, 1f,0f, F), V(-s,s, 0f,0f, F),
            V(-s,-s, 0f,1f, B), V(s,s, 1f,0f, B), V(s,-s, 1f,1f, B),
            V(-s,-s, 0f,1f, B), V(-s,s, 0f,0f, B), V(s,s, 1f,0f, B),
        }, PrimitiveType.Triangles);
    }

    private void EnsureQuadMesh(GL gl)
    {
        if (_quadMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, Vector3 n) => new() { Position = new(x, y, 0f), Normal = n, UV = Vector2.Zero, Color = w };
        var F = Vector3.UnitZ; var B = -Vector3.UnitZ;
        _quadMesh = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-1,-1,F), V(1,-1,F), V(1,1,F), V(-1,-1,F), V(1,1,F), V(-1,1,F),
            V(-1,-1,B), V(1,1,B), V(1,-1,B), V(-1,-1,B), V(-1,1,B), V(1,1,B),
        }, PrimitiveType.Triangles);
    }

    private void EnsureBoxZoneMesh(GL gl)
    {
        if (_boxZoneMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float z, Vector3 n) => new() { Position = new(x, y, z), Normal = n, UV = Vector2.Zero, Color = w };
        var U = Vector3.UnitY;  var D = -Vector3.UnitY;
        var F = Vector3.UnitZ;  var B = -Vector3.UnitZ;
        var R = Vector3.UnitX;  var L = -Vector3.UnitX;
        _boxZoneMesh = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-1,1,-1,U), V( 1,1,-1,U), V( 1,1, 1,U),
            V( 1,1, 1,U), V(-1,1, 1,U), V(-1,1,-1,U),
            V(-1,-1,-1,D), V( 1,-1, 1,D), V( 1,-1,-1,D),
            V( 1,-1, 1,D), V(-1,-1,-1,D), V(-1,-1, 1,D),
            V(-1,-1,1,F), V( 1,1,1,F), V(-1,1,1,F),
            V(-1,-1,1,F), V( 1,-1,1,F), V( 1,1,1,F),
            V(-1,-1,-1,B), V(-1,1,-1,B), V( 1,1,-1,B),
            V(-1,-1,-1,B), V( 1,1,-1,B), V( 1,-1,-1,B),
            V(1,-1,-1,R), V(1,1, 1,R), V(1,-1, 1,R),
            V(1,-1,-1,R), V(1,1,-1,R), V(1,1, 1,R),
            V(-1,-1,-1,L), V(-1,-1, 1,L), V(-1,1, 1,L),
            V(-1,-1,-1,L), V(-1,1, 1,L), V(-1,1,-1,L),
        }, PrimitiveType.Triangles);
    }

    private void AddLoadZoneBox(Entity wallEntity, LoadWallMarker wall)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null || wallEntity.Transform.LocalMatrix is not { } wallLm) return;

        var right = new Vector3(wallLm.M11, wallLm.M12, wallLm.M13);
        var up    = new Vector3(wallLm.M21, wallLm.M22, wallLm.M23);
        float halfW = right.Length() > 1e-6f ? right.Length() : 1f;
        float halfH = up.Length()    > 1e-6f ? up.Length()    : 1f;
        var wallCenter = new Vector3(wallLm.M41, wallLm.M42, wallLm.M43);
        var normal = right.LengthSquared() > 1e-6f && up.LengthSquared() > 1e-6f
            ? Vector3.Normalize(Vector3.Cross(Vector3.Normalize(right), Vector3.Normalize(up)))
            : Vector3.UnitZ;

        float depth = MathF.Min(halfW, halfH) * 0.5f;
        var center = wallCenter + normal * depth;
        var boxLm = new Matrix4x4(
            halfW, 0f, 0f, 0f,
            0f, halfH, 0f, 0f,
            0f, 0f, depth, 0f,
            center.X, center.Y, center.Z, 1f);

        var newBox = new TwinBoundingBoxBuilder();
        ChunkExporter.RecomputeBoundingBox(newBox, boxLm);
        var linkBox = new TwinChunkLinkBoundingBoxBuilder { Type = 0, BondingBoxBuilder = newBox };
        wall.Source.ChunkLinksCollisionData.Add(linkBox);

        EnsureBoxZoneMesh(Engine.Instance.GL);
        var e = new Entity($"LoadZoneBox_{System.IO.Path.GetFileName(wall.Source.Path)}_{wall.Source.ChunkLinksCollisionData.Count}");
        e.Transform.LocalMatrix = boxLm;
        e.Add(new LoadZoneBoxMarker { Link = wall.Source, Box = linkBox });
        var rdr = e.Add(new DirectCubeRenderer { Mesh = _boxZoneMesh, Color = new Vector4(1f, 0.65f, 0.1f, 0.35f) });
        rdr.Mat.AlphaBlend = true;
        wallEntity.Parent?.AddChild(e);

        // Amedo 2026-09-20
        PushAddWithSelectionRestore(
            new LoadZoneBoxAddAction { Parent = wallEntity.Parent!, Entity = e, Link = wall.Source, Box = linkBox },
            e);

        _browser.Log($"Added a pre-load bounding box for '{wall.Source.Path}' (box {wall.Source.ChunkLinksCollisionData.Count} on this link). " +
                      "Drag it into place (Move gizmo) and use Size X/Y/Z to resize -- click Add Bounding Box again to chain more into an L/U shape. Save Chunk to persist.");
    }

    private static GpuMesh BuildGizmoMesh(GL gl)
    {
        var v = new List<GpuMesh.Vertex>();
        AppendArrow(v, Quaternion.Identity,
                    new Vector4(1f, 0.2f, 0.2f, 1f));
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitZ,  MathF.PI / 2f),
                    new Vector4(0.2f, 1f, 0.2f, 1f));
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2f),
                    new Vector4(0.2f, 0.2f, 1f, 1f));
        return new GpuMesh(gl, v.ToArray(), PrimitiveType.Triangles);
    }

    private static GpuMesh BuildDuplicateGizmoMesh(GL gl)
    {
        var v = new List<GpuMesh.Vertex>();
        var col = new Vector4(1f, 0.65f, 0.15f, 1f);
        AppendArrow(v, Quaternion.Identity,                                          col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitY,  MathF.PI),      col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitZ,  MathF.PI / 2f), col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathF.PI / 2f), col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2f), col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitY,  MathF.PI / 2f), col);
        return new GpuMesh(gl, v.ToArray(), PrimitiveType.Triangles);
    }

    private static void AppendArrow(List<GpuMesh.Vertex> v, Quaternion rot, Vector4 col)
    {
        const float SL = StemLen, SR = 0.06f, HL = HeadLen, HR = 0.18f;
        GpuMesh.Vertex Pt(float x, float y, float z) => new()
        {
            Position = Vector3.Transform(new Vector3(x, y, z), rot),
            Normal   = Vector3.UnitY, UV = Vector2.Zero, Color = col,
        };
        v.Add(Pt(0,-SR, 0)); v.Add(Pt(SL,-SR, 0)); v.Add(Pt(SL, SR, 0));
        v.Add(Pt(0,-SR, 0)); v.Add(Pt(SL, SR, 0)); v.Add(Pt(0,  SR, 0));
        v.Add(Pt(0, 0,-SR)); v.Add(Pt(SL, 0,-SR)); v.Add(Pt(SL, 0, SR));
        v.Add(Pt(0, 0,-SR)); v.Add(Pt(SL, 0, SR)); v.Add(Pt(0,  0, SR));
        float bx = SL, tx = SL + HL;
        v.Add(Pt(bx, HR,  0)); v.Add(Pt(bx,  0, HR)); v.Add(Pt(tx, 0, 0));
        v.Add(Pt(bx,  0, HR)); v.Add(Pt(bx,-HR,  0)); v.Add(Pt(tx, 0, 0));
        v.Add(Pt(bx,-HR,  0)); v.Add(Pt(bx,  0,-HR)); v.Add(Pt(tx, 0, 0));
        v.Add(Pt(bx,  0,-HR)); v.Add(Pt(bx, HR,  0)); v.Add(Pt(tx, 0, 0));
    }

    private static GpuMesh BuildSingleArrowMesh(GL gl, Vector4 col)
    {
        var v = new List<GpuMesh.Vertex>();
        AppendArrow(v, Quaternion.Identity, col);
        return new GpuMesh(gl, v.ToArray(), PrimitiveType.Triangles);
    }


    private static Quaternion RotationBetween(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        to   = Vector3.Normalize(to);
        float d = Vector3.Dot(from, to);
        if (d > 0.9999f) return Quaternion.Identity;
        if (d < -0.9999f)
        {
            var axis = Vector3.Cross(Vector3.UnitY, from);
            if (axis.LengthSquared() < 1e-6f) axis = Vector3.Cross(Vector3.UnitZ, from);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }
        var c = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(c.X, c.Y, c.Z, 1f + d));
    }

    private Entity MakeQuadMarker(Entity parent, string name, TwinChunkLink link, Vector4 color)
    {
        var wall = link.LoadingWall!;
        var p1 = new Vector3(wall.Column1.X, wall.Column1.Y, wall.Column1.Z);
        var p2 = new Vector3(wall.Column2.X, wall.Column2.Y, wall.Column2.Z);
        var p3 = new Vector3(wall.Column3.X, wall.Column3.Y, wall.Column3.Z);
        var p4 = new Vector3(wall.Column4.X, wall.Column4.Y, wall.Column4.Z);

        var center    = (p1 + p2 + p3 + p4) * 0.25f;
        var edgeRight = p2 - p1;
        var edgeUp    = p4 - p1;
        var halfRight = edgeRight.Length() * 0.5f;
        var halfUp    = edgeUp.Length() * 0.5f;
        var right     = halfRight > 1e-6f ? edgeRight / edgeRight.Length() : Vector3.UnitX;
        var up        = halfUp    > 1e-6f ? edgeUp    / edgeUp.Length()    : Vector3.UnitY;
        var normal    = Vector3.Cross(right, up);
        if (normal.LengthSquared() < 1e-6f) normal = Vector3.UnitZ; else normal = Vector3.Normalize(normal);

        var lm = new Matrix4x4(
            right.X * halfRight, right.Y * halfRight, right.Z * halfRight, 0f,
            up.X    * halfUp,    up.Y    * halfUp,    up.Z    * halfUp,    0f,
            normal.X,             normal.Y,            normal.Z,            0f,
            center.X,             center.Y,            center.Z,            1f);

        var e = new Entity(name);
        e.Transform.LocalMatrix = lm;
        e.Add(new LoadWallMarker { Source = link });
        var rdr = e.Add(new DirectCubeRenderer { Mesh = _quadMesh, Color = color });
        rdr.Mat.AlphaBlend = true;
        parent.AddChild(e);
        return e;
    }

    private Entity MakePointMarker(Entity parent, string name, TwinVec4 pos, Vector4 color)
    {
        var e = new Entity(name);
        e.Transform.Position = new Vector3(pos.X, pos.Y, pos.Z);
        e.Transform.Scale    = new Vector3(0.3f, 0.3f, 0.3f);

        var fillEnt = new Entity($"{name}_fill");
        var fillRdr = fillEnt.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = color });
        fillRdr.Mat.AlphaBlend = true;
        fillRdr.Mat.DepthWrite = false;
        fillRdr.Mat.IgnoreDepthTest = true;
        e.AddChild(fillEnt);

        var edgeEnt = new Entity($"{name}_edge");
        var edgeRdr = edgeEnt.Add(new DirectCubeRenderer { Mesh = _cubeWireMesh, Color = new Vector4(color.X, color.Y, color.Z, 1f) });
        edgeRdr.Mat.DepthWrite = false;
        edgeRdr.Mat.IgnoreDepthTest = true;
        e.AddChild(edgeEnt);

        parent.AddChild(e);
        return e;
    }

    // Amedo
    private Entity MakeAiPositionMarker(Entity parent, string name, TwinVec4 pos)
    {
        var color = new Vector4(1f, 0.25f, 0.25f, 0.7f);
        var gl = Engine.Instance.GL;
        var skull = EnsureAiSkullModel(gl);
        Texture2D? tex = null;
        if (skull is not null) tex = new Texture2D(gl, skull.Pixels, (uint)skull.TexWidth, (uint)skull.TexHeight);
        else
        {
            try
            {
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "ai_position_marker.png");
                if (System.IO.File.Exists(iconPath)) tex = Texture2D.FromFile(gl, iconPath);
            }
            catch { }
        }
        if (tex is null || (skull is null && _lightIconQuad is null)) return MakePointMarker(parent, name, pos, color);

        var e = new Entity(name);
        e.Transform.Position = new Vector3(pos.X, pos.Y, pos.Z);
        e.Transform.Scale    = new Vector3(0.3f, 0.3f, 0.3f);

        var iconEnt = new Entity($"{name}_icon");
        float k = skull is null ? AiIconScale : AiSkullWorldHeight / (e.Transform.Scale.Y * skull.Height);
        iconEnt.Transform.Position = skull is null ? new Vector3(0f, AiIconLift, 0f) : Vector3.Zero;
        iconEnt.Transform.Scale    = new Vector3(k, k, k);
        var iconRdr = iconEnt.Add(new DirectCubeRenderer { Mesh = skull?.Mesh ?? _lightIconQuad, Color = Vector4.One });
        iconRdr.Albedo              = tex;
        iconRdr.Mat.AlphaBlend      = skull is null;
        iconRdr.Mat.AlphaTest       = skull is null ? 0.5f : 0f;
        iconRdr.Mat.BillboardRender = skull is null;
        iconRdr.Mat.Culling         = Material.CullMode.Both;
        iconRdr.Mat.AlwaysOnTop     = false;
        e.AddChild(iconEnt);

        var ringEnt = new Entity($"{name}_ring");
        ringEnt.Transform.Scale = new Vector3(AiRingRadius, 1f, AiRingRadius);
        var ringRdr = ringEnt.Add(new DirectCubeRenderer { Mesh = EnsureAiRingMesh(Engine.Instance.GL), Color = new Vector4(0.70f, 0.17f, 0.17f, 1f) });
        ringRdr.Mat.Culling         = Material.CullMode.Both;
        ringRdr.Mat.AlwaysOnTop     = false;
        ringRdr.Mat.IgnoreDepthTest = false;
        e.AddChild(ringEnt);

        parent.AddChild(e);
        return e;
    }
    private const float AiIconScale = 0.75f;
    private const float AiSkullWorldHeight = 1.0f; // Amedo
    private const float AiIconLift = AiIconScale * 1.6f; // Amedo
    private const float AiRingRadius = AiIconScale * 1.6f * 1.4f; // Amedo

    // Amedo
    private sealed record AiSkullModel(GpuMesh Mesh, byte[] Pixels, int TexWidth, int TexHeight, float Height,
                                       Vector3[] Positions, uint[] Indices, Vector3 Min, Vector3 Max);
    private AiSkullModel? _aiSkull;
    private bool _aiSkullTried;
    private static readonly Vector3 AiSkullLight = Vector3.Normalize(new Vector3(-0.35f, 0.85f, 0.6f));
    private AiSkullModel? EnsureAiSkullModel(GL gl)
    {
        if (_aiSkullTried) return _aiSkull;
        _aiSkullTried = true;
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "AiSkull.glb");
        if (!System.IO.File.Exists(path)) return null;
        var parts = CrashEngine.Assets.ModelImporter.ExtractRawSubmeshes(path);
        if (parts is not { Count: > 0 }) return null;
        var verts = new List<GpuMesh.Vertex>();
        var idx = new List<uint>();
        byte[]? px = null; int tw = 0, th = 0;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in parts)
        {
            uint baseIndex = (uint)verts.Count;
            for (int i = 0; i < p.Positions.Length; i++)
            {
                float shade = 0.55f + 0.45f * MathF.Max(0f, Vector3.Dot(p.Normals[i], AiSkullLight));
                verts.Add(new GpuMesh.Vertex { Position = p.Positions[i], Normal = p.Normals[i], UV = p.UVs[i], Color = new Vector4(shade, shade, shade, 1f) });
                minY = MathF.Min(minY, p.Positions[i].Y); maxY = MathF.Max(maxY, p.Positions[i].Y);
            }
            foreach (uint i in p.Indices) idx.Add(baseIndex + i);
            if (px is null && p.DiffusePixelsRGBA is { } d && p.TexWidth > 0 && p.TexHeight > 0) { px = d; tw = p.TexWidth; th = p.TexHeight; }
        }
        if (px is null || maxY <= minY) return null;
        var positions = new Vector3[verts.Count];
        var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
        for (int i = 0; i < verts.Count; i++) { positions[i] = verts[i].Position; lo = Vector3.Min(lo, positions[i]); hi = Vector3.Max(hi, positions[i]); }
        var indices = idx.ToArray();
        return _aiSkull = new AiSkullModel(new GpuMesh(gl, verts.ToArray(), indices), px, tw, th, maxY - minY, positions, indices, lo, hi);
    }

    // Amedo
    private bool RayHitsAiIcon(Entity icon, Vector3 orig, Vector3 dir, out float t)
    {
        t = float.MaxValue;
        if (_aiSkull is not { } skull)
        {
            var c = icon.Transform.World.Translation;
            float r = icon.Transform.World.M22 * 1.6f;
            if (!RayAABB(orig, dir, c - new Vector3(r), c + new Vector3(r), out float tb)) return false;
            t = tb;
            return true;
        }
        if (!Matrix4x4.Invert(icon.Transform.World, out var inv)) return false;
        var o = Vector3.Transform(orig, inv);
        var d = Vector3.TransformNormal(dir, inv);
        if (!RayAABB(o, d, skull.Min, skull.Max, out _)) return false;
        var pos = skull.Positions; var ind = skull.Indices;
        for (int i = 0; i + 2 < ind.Length; i += 3)
        {
            var a = pos[ind[i]];
            var e1 = pos[ind[i + 1]] - a;
            var e2 = pos[ind[i + 2]] - a;
            var pv = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, pv);
            if (MathF.Abs(det) < 1e-12f) continue;
            float inv_det = 1f / det;
            var tv = o - a;
            float u = Vector3.Dot(tv, pv) * inv_det;
            if (u < 0f || u > 1f) continue;
            var qv = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(d, qv) * inv_det;
            if (v < 0f || u + v > 1f) continue;
            float tt = Vector3.Dot(e2, qv) * inv_det;
            if (tt > 0f && tt < t) t = tt;
        }
        return t < float.MaxValue;
    }

    // Amedo
    private GpuMesh? _aiRingMesh;
    private GpuMesh EnsureAiRingMesh(GL gl)
    {
        if (_aiRingMesh is not null) return _aiRingMesh;
        const int Segments = 48;
        const float Inner = 0.84f;
        var verts = new GpuMesh.Vertex[Segments * 6];
        GpuMesh.Vertex V(float x, float z) => new() { Position = new(x, 0f, z), Normal = Vector3.UnitY, Color = Vector4.One };
        for (int i = 0; i < Segments; i++)
        {
            float a0 = i * MathF.Tau / Segments, a1 = (i + 1) * MathF.Tau / Segments;
            var o0 = V(MathF.Cos(a0), MathF.Sin(a0)); var o1 = V(MathF.Cos(a1), MathF.Sin(a1));
            var i0 = V(MathF.Cos(a0) * Inner, MathF.Sin(a0) * Inner); var i1 = V(MathF.Cos(a1) * Inner, MathF.Sin(a1) * Inner);
            int k = i * 6;
            verts[k] = o0; verts[k + 1] = o1; verts[k + 2] = i1;
            verts[k + 3] = o0; verts[k + 4] = i1; verts[k + 5] = i0;
        }
        return _aiRingMesh = new GpuMesh(gl, verts, PrimitiveType.Triangles);
    }

    // Amedo
    private CrashEngine.Runtime.Physics.CollisionWorld? _aiGroundWorld;
    private object? _aiGroundRm2;
    private readonly Dictionary<Entity, Vector3> _aiRingAt = new();
    private void UpdateAiRings(Entity root, ChunkSource src)
    {
        if (!ReferenceEquals(_aiGroundRm2, src.Rm2))
        {
            _aiGroundRm2 = src.Rm2;
            _aiGroundWorld = CrashEngine.Runtime.Physics.CollisionWorld.FromLevel(src.Rm2, src.GlobalRm2);
            _aiRingAt.Clear();
        }
        foreach (var e in root.Children)
        {
            if (!IsAiIconMarker(e) || e.Children.Count < 2) continue;
            var p = e.Transform.Position;
            if (_aiRingAt.TryGetValue(e, out var last) && last == p) continue;
            _aiRingAt[e] = p;
            float drop = 0f;
            if (_aiGroundWorld is { } world)
            {
                var start = p + new Vector3(0f, AiGroundProbeUp, 0f);
                var end = p - new Vector3(0f, AiGroundProbeDown, 0f);
                if (world.SegmentCast(start, end, 0x40, out var hit) || world.SegmentCast(start, end, 0xFFFFFFFFu, out hit))
                    drop = hit.Point.Y + AiRingGroundLift - p.Y;
            }
            e.Children[1].Transform.Position = new Vector3(0f, drop / e.Transform.Scale.Y, 0f);
        }
    }
    private const float AiGroundProbeUp = 0.5f, AiGroundProbeDown = 30f, AiRingGroundLift = 0.03f; // Amedo

    // Amedo
    private static bool IsAiIconMarker(Entity e) => e.Children.Count > 0 && e.Children[0].Name.EndsWith("_icon");

    // Amedo
    private readonly Dictionary<object, Entity> _aiSrcToEnt = new();
    private readonly Dictionary<uint, Entity> _aiIdToEnt = new();
    private readonly List<(Vector3 A, Vector3 B, byte Kind)> _aiSegs = new();
    private readonly List<GpuMesh.Vertex> _aiLineVerts = new();
    private DirectCubeRenderer? _aiLineRdr;
    private int _aiLineHash;
    private void UpdateAiPathLines()
    {
        if (_aiPosRoot is not { Active: true } root) return;
        if (root.Parent?.Get<ChunkSource>() is not { } src) return;
        UpdateAiRings(root, src);
        if (_aiSkull is not null && _camera is not null && Matrix4x4.Invert(_camera.View, out var camWorld))
        {
            var eye = camWorld.Translation;
            foreach (var e in root.Children)
            {
                if (!e.Active || !IsAiIconMarker(e)) continue;
                var icon = e.Children[0];
                var to = eye - icon.Transform.World.Translation;
                icon.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(to.X, to.Z));
            }
        }
        if (_aiLineRdr is null || !ReferenceEquals(_aiLineRdr.Entity.Parent, root))
        {
            var host = new Entity("__AiPathLines");
            _aiLineRdr = host.Add(new DirectCubeRenderer { Color = Vector4.One, OwnsMesh = true });
            _aiLineRdr.Mat.AlwaysOnTop = false;
            _aiLineRdr.Mat.IgnoreDepthTest = false;
            _aiLineRdr.Mat.Culling = Material.CullMode.Both;
            root.AddChild(host);
            _aiLineHash = 0;
        }

        _aiSegs.Clear();
        _aiSrcToEnt.Clear();
        foreach (var e in root.Children)
            if (e.Active && e.Get<CrashEngine.Importer.AiPositionMarker>()?.Source is { } s) _aiSrcToEnt[s] = e;
        var rm2 = src.Rm2;
        for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
            var posSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);
            var pathSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
            if (posSec is null || pathSec is null) continue;
            _aiIdToEnt.Clear();
            for (int i = 0; i < posSec.GetItemsAmount(); i++)
            {
                var item = posSec.GetItem(i);
                if (_aiSrcToEnt.TryGetValue(item, out var ent)) _aiIdToEnt[item.GetID()] = ent;
            }
            for (int i = 0; i < pathSec.GetItemsAmount(); i++)
            {
                if (pathSec.GetItem(i) is not PS2AnyAIPath p || p.Args.Length < 2) continue;
                if (!_aiIdToEnt.TryGetValue(p.Args[0], out var ea) || !_aiIdToEnt.TryGetValue(p.Args[1], out var eb)) continue;
                bool hot = _selectedSet.Contains(ea) || _selectedSet.Contains(eb);
                _aiSegs.Add((ea.Transform.Position, eb.Transform.Position, hot ? (byte)1 : (byte)0));
            }
        }
        foreach (var e in root.Children)
        {
            if (!e.Active || !IsAiIconMarker(e) || e.Children.Count < 2) continue;
            var top = e.Transform.Position;
            var ground = top + new Vector3(0f, e.Children[1].Transform.Position.Y * e.Transform.Scale.Y, 0f);
            if (top.Y - ground.Y > 0.05f) _aiSegs.Add((top, ground, 2));
        }

        var hc = new HashCode();
        foreach (var sg in _aiSegs) { hc.Add(sg.A); hc.Add(sg.B); hc.Add(sg.Kind); }
        int hash = hc.ToHashCode();
        if (hash == _aiLineHash && _aiLineRdr.Mesh is not null) return;
        _aiLineHash = hash;

        _aiLineVerts.Clear();
        foreach (var (a, c, kind) in _aiSegs)
        {
            var col = kind switch { 1 => new Vector4(1f, 0.88f, 0.25f, 1f), 2 => new Vector4(0.55f, 0.12f, 0.12f, 1f), _ => new Vector4(0.82f, 0.2f, 0.2f, 1f) };
            float radius = kind switch { 1 => 0.045f, 2 => 0.012f, _ => 0.03f };
            AppendAiTube(_aiLineVerts, a, c, radius, radius, col);
            if (kind == 2) continue;
            var d = c - a;
            float len = d.Length();
            if (len < 0.6f) continue;
            d /= len;
            var mid = (a + c) * 0.5f;
            AppendAiTube(_aiLineVerts, mid - d * 0.14f, mid + d * 0.14f, radius * 3.2f, 0f, col);
        }
        _aiLineRdr.Mesh?.Dispose();
        _aiLineRdr.Mesh = _aiLineVerts.Count > 0 ? new GpuMesh(Engine.Instance.GL, _aiLineVerts.ToArray(), PrimitiveType.Triangles) : null;
    }

    // Amedo
    private static void AppendAiTube(List<GpuMesh.Vertex> verts, Vector3 a, Vector3 b, float ra, float rb, Vector4 color)
    {
        const int Sides = 6;
        var axis = b - a;
        float len = axis.Length();
        if (len < 0.0001f) return;
        var dir = axis / len;
        var up = MathF.Abs(dir.Y) > 0.99f ? Vector3.UnitX : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(up, dir));
        var trueUp = Vector3.Cross(dir, right);
        GpuMesh.Vertex V(Vector3 p, Vector3 n) => new() { Position = p, Normal = n, Color = color };
        for (int i = 0; i < Sides; i++)
        {
            float t0 = i * MathF.Tau / Sides, t1 = (i + 1) * MathF.Tau / Sides;
            var n0 = right * MathF.Cos(t0) + trueUp * MathF.Sin(t0);
            var n1 = right * MathF.Cos(t1) + trueUp * MathF.Sin(t1);
            verts.Add(V(a + n0 * ra, n0)); verts.Add(V(a + n1 * ra, n1)); verts.Add(V(b + n0 * rb, n0));
            verts.Add(V(a + n1 * ra, n1)); verts.Add(V(b + n1 * rb, n1)); verts.Add(V(b + n0 * rb, n0));
            if (ra > 0f) { verts.Add(V(a, -dir)); verts.Add(V(a + n1 * ra, -dir)); verts.Add(V(a + n0 * ra, -dir)); }
        }
    }

    // Amedo 2026-09-19
    private void BuildSceneryLightMarkers(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery scenery)
    {
        if (_lightsRoot is null) return;
        foreach (var ch in _lightsRoot.Children.ToList()) _lightsRoot.RemoveChild(ch);
        for (int i = 0; i < scenery.PointLights.Count; i++)
            AddOneLightMarker(scenery.PointLights[i], false, $"PointLight_{i}");
        for (int i = 0; i < scenery.NegativeLights.Count; i++)
            AddOneLightMarker(scenery.NegativeLights[i], true, $"NegLight_{i}");
    }

    // Amedo 2026-09-19
    private Entity? AddOneLightMarker(Twinsanity.TwinsanityInterchange.Common.Lights.Light light, bool isNegative, string name)
    {
        if (_lightsRoot is null) return null;
        var ent = new Entity(name);
        ent.Transform.Position = new Vector3(light.Position.X, light.Position.Y, light.Position.Z);
        ent.Transform.Scale = new Vector3(0.5f, 0.5f, 0.5f);
        _lightsRoot.AddChild(ent);
        ent.Add(new CrashEngine.Importer.SceneryLightMarker { Source = light, IsNegative = isNegative });

        var tint = isNegative ? new Vector4(1f, 0.4f, 0.4f, 1f) : new Vector4(1f, 1f, 1f, 1f);

        Texture2D? tex = null;
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "lightbulb.png");
            if (System.IO.File.Exists(iconPath)) tex = Texture2D.FromFile(Engine.Instance.GL, iconPath);
        }
        catch { }

        if (tex is not null)
        {
            var iconRdr = ent.Add(new DirectCubeRenderer { Mesh = _lightIconQuad ?? _quadMesh, Color = tint });
            iconRdr.Albedo              = tex;
            iconRdr.Mat.AlphaBlend      = true;
            iconRdr.Mat.BillboardRender = true;
            iconRdr.Mat.Culling         = Material.CullMode.Both;
            iconRdr.Mat.DepthWrite      = false;
            iconRdr.Mat.IgnoreDepthTest = true;
        }
        else
        {
            var fillRdr = ent.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = new Vector4(tint.X, tint.Y, tint.Z, 0.85f) });
            fillRdr.Mat.AlphaBlend = true; fillRdr.Mat.DepthWrite = false; fillRdr.Mat.IgnoreDepthTest = true;
        }
        return ent;
    }

    // Amedo 2026-09-26
    private void SyncLightMarkerPositionsLive()
    {
        if (_lightsRoot is null) return;
        foreach (var ent in _lightsRoot.Children)
        {
            var light = ent.Get<CrashEngine.Importer.SceneryLightMarker>()?.Source;
            if (light is null) continue;
            var p = ent.Transform.Position;
            var src = light.Position;
            if (src.X == p.X && src.Y == p.Y && src.Z == p.Z) continue;
            light.Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(p.X, p.Y, p.Z, src.W);
        }
    }

    // Amedo 2026-09-26
    private static int EnableAddedSceneryLights(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery scenery)
    {
        int gAmb = scenery.AmbientLights.Count, gDir = scenery.DirectionalLights.Count, gPt = scenery.PointLights.Count;
        int totalNodes = scenery.Sceneries.Count;
        int rescoped = 0;
        void Enable(int gi)
        {
            if (gi < 0 || gi >= 128 || totalNodes == 0) return;
            if (scenery.Sceneries.Any(nd => nd.LightsEnabler[gi])) return;
            foreach (var nd in scenery.Sceneries) nd.LightsEnabler[gi] = true;
            rescoped++;
        }
        for (int p = 0; p < gPt; p++) Enable(gAmb + gDir + p);
        for (int q = 0; q < scenery.NegativeLights.Count; q++) Enable(gAmb + gDir + gPt + q);
        return rescoped;
    }

    // Amedo 2026-09-19
    private void SelectLightMarkerFor(Twinsanity.TwinsanityInterchange.Common.Lights.Light light)
    {
        var ent = _lightsRoot?.Children.FirstOrDefault(c => ReferenceEquals(c.Get<CrashEngine.Importer.SceneryLightMarker>()?.Source, light));
        if (ent is not null) SelectClicked(ent, false);
    }

    // Amedo 2026-09-19
    private void RemoveLightMarkerFor(Twinsanity.TwinsanityInterchange.Common.Lights.Light light)
    {
        var ent = _lightsRoot?.Children.FirstOrDefault(c => ReferenceEquals(c.Get<CrashEngine.Importer.SceneryLightMarker>()?.Source, light));
        if (ent is not null) _lightsRoot!.RemoveChild(ent);
    }

    private void EnsurePositionMarkersExist(Entity chunkRoot, PS2AnyTwinsanityRM2 rm2, IEnumerable<ushort> ids)
    {
        if (_positionsRoot is null) return;
        var existing = new HashSet<uint>(
            AllEntities(chunkRoot).Where(x => x.Has<CrashEngine.Importer.PositionMarker>())
                .Select(x => x.Get<CrashEngine.Importer.PositionMarker>()!.Source.GetID()));

        foreach (var id in ids.Distinct())
        {
            if (existing.Contains(id)) continue;
            for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
                var posSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_POSITIONS_SECTION);
                if (posSec is null) continue;
                bool found = false;
                for (int i = 0; i < posSec.GetItemsAmount(); i++)
                {
                    if (posSec.GetItem(i) is not PS2AnyPosition p || p.GetID() != id) continue;
                    var posEnt = MakePointMarker(_positionsRoot, $"Position_{p.GetID():X4}", p.Position,
                                    new Vector4(1f, 0.9f, 0.15f, 0.7f));
                    posEnt.Add(new CrashEngine.Importer.PositionMarker { Source = p });
                    found = true;
                    break;
                }
                if (found) break;
            }
        }
    }

    private void EnsureAiPositionMarkersExist(Entity chunkRoot, PS2AnyTwinsanityRM2 rm2, IEnumerable<ushort> ids)
    {
        if (_aiPosRoot is null) return;
        var existing = new HashSet<uint>(
            AllEntities(chunkRoot).Where(x => x.Has<CrashEngine.Importer.AiPositionMarker>())
                .Select(x => x.Get<CrashEngine.Importer.AiPositionMarker>()!.Source.GetID()));

        foreach (var id in ids.Distinct())
        {
            if (existing.Contains(id)) continue;
            for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
                var aiPosSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);
                if (aiPosSec is null) continue;
                bool found = false;
                for (int i = 0; i < aiPosSec.GetItemsAmount(); i++)
                {
                    if (aiPosSec.GetItem(i) is not PS2AnyAIPosition ap || ap.GetID() != id) continue;
                    var aiPosEnt = MakeAiPositionMarker(_aiPosRoot, $"AiPosition_{ap.GetID():X4}", ap.Position); // Amedo
                    aiPosEnt.Add(new CrashEngine.Importer.AiPositionMarker { Source = ap });
                    found = true;
                    break;
                }
                if (found) break;
            }
        }
    }

    public Entity? AddAiPositionAt(Vector3 pos)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2 = chunkRoot?.Get<ChunkSource>()?.Rm2;
        if (chunkRoot is null || rm2 is null || _aiPosRoot is null) { _browser.Log("Add AI Position: no level loaded."); return null; }

        BaseTwinSection? aiSec = null;
        for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION && aiSec is null; lid++)
            aiSec = rm2.GetItem<BaseTwinSection>((uint)lid)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);
        if (aiSec is null) { _browser.Log("Add AI Position: this level has no AI-positions section."); return null; }

        uint id = 0; var used = new HashSet<uint>();
        for (int i = 0; i < aiSec.GetItemsAmount(); i++) if (aiSec.GetItem(i) is PS2AnyAIPosition p) used.Add(p.GetID());
        while (used.Contains(id)) id++;

        var ap = new PS2AnyAIPosition { Position = new TwinVec4(pos.X, pos.Y, pos.Z, 2.44f), UnkShort = 0 };
        ap.SetID(id);
        aiSec.AddItem(ap);

        var ent = MakeAiPositionMarker(_aiPosRoot, $"AiPosition_{id:X4}", ap.Position); // Amedo
        ent.Add(new CrashEngine.Importer.AiPositionMarker { Source = ap });
        _aiPosRoot.Active = true; _showAiPositions = true;

        _selectedSet.Clear(); _selectedSet.Add(ent); _selected = ent; _revealSelectionInTree = true;
        _browser.Log($"Added AI Position 0x{id:X4} at ({pos.X:0.#},{pos.Y:0.#},{pos.Z:0.#}). Ctrl+D to copy, drag to move, Delete to remove. Save Chunk to keep.");
        return ent;
    }

    public void AddAiPathBetweenSelected()
    {
        var nodes = _selectedSet
            .Select(x => (Ent: x, Marker: x.Get<CrashEngine.Importer.AiPositionMarker>()))
            .Where(t => t.Marker is not null).ToList();
        if (nodes.Count < 2)
        { _browser.Log($"Add AI Path: select at least TWO AI positions (Ctrl+click). You have {nodes.Count} selected."); return; }

        var rm2 = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2;
        if (rm2 is null) { _browser.Log("Add AI Path: no level loaded."); return; }

        BaseTwinSection? pathSec = null;
        for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION && pathSec is null; lid++)
            pathSec = rm2.GetItem<BaseTwinSection>((uint)lid)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
        if (pathSec is null) { _browser.Log("Add AI Path: this level has no AI-paths section."); return; }

        Vector3 Pos(Entity e) => e.Transform.World.Translation;
        var tour = new List<(Entity Ent, CrashEngine.Importer.AiPositionMarker? Marker)> { nodes[0] };
        var remaining = nodes.Skip(1).ToList();
        while (remaining.Count > 0)
        {
            var last = Pos(tour[^1].Ent);
            int bi = 0; float bd = float.MaxValue;
            for (int i = 0; i < remaining.Count; i++)
            { var d = Vector3.DistanceSquared(last, Pos(remaining[i].Ent)); if (d < bd) { bd = d; bi = i; } }
            tour.Add(remaining[bi]); remaining.RemoveAt(bi);
        }

        var existing = new HashSet<(ushort, ushort)>();
        uint nextId = 0;
        for (int i = 0; i < pathSec.GetItemsAmount(); i++)
            if (pathSec.GetItem(i) is PS2AnyAIPath p)
            { existing.Add((p.Args[0], p.Args[1])); existing.Add((p.Args[1], p.Args[0])); nextId = Math.Max(nextId, p.GetID() + 1); }

        int edges = tour.Count == 2 ? 1 : tour.Count;
        int made = 0;
        for (int i = 0; i < edges; i++)
        {
            ushort fromId = (ushort)tour[i].Marker!.Source.GetID();
            ushort toId   = (ushort)tour[(i + 1) % tour.Count].Marker!.Source.GetID();
            if (fromId == toId || existing.Contains((fromId, toId))) continue;
            var path = new PS2AnyAIPath { Args = new ushort[] { fromId, toId, 0, 0, 0 } };
            path.SetID(nextId++);
            pathSec.AddItem(path);
            existing.Add((fromId, toId)); existing.Add((toId, fromId));
            made++;
        }
        _browser.Log($"Added {made} AI path(s) linking {tour.Count} selected AI position(s) into a connected {(tour.Count > 2 ? "loop" : "pair")}. Save Chunk to keep.");
    }

    private void BuildLayoutMarkers(Entity chunkRoot)
    {
        var rm2 = chunkRoot.Get<ChunkSource>()?.Rm2;
        if (rm2 is null) return;

        var triggersRoot  = new Entity("Triggers")    { Active = false };
        var camerasRoot   = new Entity("Cameras")     { Active = false };
        var positionsRoot = new Entity("Positions")   { Active = false };
        var aiPosRoot     = new Entity("AiPositions") { Active = false };
        var loadScenesRoot = new Entity("LoadScenes") { Active = false };
        var emittersRoot   = new Entity("ParticleEmitters") { Active = false };
        var lightsRoot     = new Entity("Lights") { Active = _showLights }; // Amedo 2026-09-19
        var effectsRoot    = new Entity("ParticleEffects") { Active = _showParticleEffects }; // Amedo 2026-09-26
        var effects        = effectsRoot.Add(new ParticleEffectsRenderer { DistanceGating = _particleDistanceGating }); // Amedo 2026-09-26
        chunkRoot.AddChild(triggersRoot);
        chunkRoot.AddChild(camerasRoot);
        chunkRoot.AddChild(positionsRoot);
        chunkRoot.AddChild(aiPosRoot);
        chunkRoot.AddChild(loadScenesRoot);
        chunkRoot.AddChild(emittersRoot);
        chunkRoot.AddChild(lightsRoot);
        chunkRoot.AddChild(effectsRoot); // Amedo 2026-09-26
        AddObjectCollisionRoot(chunkRoot); // Amedo 2026-10-03
        AddGameCameraOverlay(chunkRoot); // Amedo 2026-10-06
        _triggersRoot = triggersRoot; _camerasRoot = camerasRoot;
        _positionsRoot = positionsRoot; _aiPosRoot = aiPosRoot;
        _loadScenesRoot = loadScenesRoot; _particleEmittersRoot = emittersRoot;
        _lightsRoot = lightsRoot;
        _particleEffectsRoot = effectsRoot; _particleEffects = effects; // Amedo 2026-09-26
        EnsureFxTexturePagesLoaded(); // Amedo 2026-09-26
        effects.Pages = _fxTexturePages; // Amedo 2026-09-26
        effects.Decals = _fxDecals; // Amedo 2026-09-30

        Entity MakeBoxMarker(Entity parent, string name, TwinVec4 pos, TwinVec4 scale, Vector4 color, Texture2D? icon = null, TwinVec4? rotation = null)
        {
            var e = new Entity(name);
            e.Transform.Position = new Vector3(pos.X, pos.Y, pos.Z);
            var sc = new Vector3(MathF.Max(scale.X, 0.05f), MathF.Max(scale.Y, 0.05f), MathF.Max(scale.Z, 0.05f));
            e.Transform.Scale = sc;
            if (rotation is not null)
            {
                TwinTrigger.DecodeAxisAngle(rotation.Value, out var axis, out var angleRad); // Amedo
                if (angleRad != 0f)
                    e.Transform.Rotation = Quaternion.CreateFromAxisAngle(new Vector3(axis.X, axis.Y, axis.Z), angleRad);
            }

            var fillEnt = new Entity($"{name}_fill");
            var fillRdr = fillEnt.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = color });
            fillRdr.Mat.AlphaBlend = true;
            fillRdr.Mat.DepthWrite = false;
            fillRdr.Mat.IgnoreDepthTest = true;
            e.AddChild(fillEnt);

            var edgeEnt = new Entity($"{name}_edge");
            var edgeRdr = edgeEnt.Add(new DirectCubeRenderer { Mesh = _cubeWireMesh, Color = new Vector4(color.X, color.Y, color.Z, 1f) });
            edgeRdr.Mat.DepthWrite = false;
            edgeRdr.Mat.IgnoreDepthTest = true;
            e.AddChild(edgeEnt);

            if (icon is not null)
            {
                var iconEnt = new Entity($"{name}_icon");
                iconEnt.Transform.Position = new Vector3(pos.X, pos.Y + sc.Y, pos.Z);
                const float iconWorldSize = 0.9f;
                iconEnt.Transform.Scale = new Vector3(iconWorldSize, iconWorldSize, iconWorldSize);
                var iconRdr = iconEnt.Add(new DirectCubeRenderer { Mesh = _quadMesh, Color = Vector4.One });
                iconRdr.Mat.Albedo          = icon;
                iconRdr.Mat.AlphaBlend      = true;
                iconRdr.Mat.BillboardRender = true;
                iconRdr.Mat.Culling         = Material.CullMode.Both;
                iconRdr.Mat.DepthWrite      = false;
                iconRdr.Mat.IgnoreDepthTest = true;
                parent.AddChild(iconEnt);
            }

            parent.AddChild(e);
            return e;
        }

        int triggers = 0, cameras = 0, positions = 0, aiPositions = 0;
        for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
            if (layout is null) continue;

            var trigSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
            if (trigSec is not null)
                for (int i = 0; i < trigSec.GetItemsAmount(); i++)
                    if (trigSec.GetItem(i) is PS2AnyTrigger t)
                    {
                        var trigEnt = MakeBoxMarker(triggersRoot, $"Trigger_{t.GetID():X4}", t.Trigger.Position, t.Trigger.Scale,
                                      new Vector4(0.2f, 0.85f, 0.3f, 0.35f), rotation: t.Trigger.Rotation);
                        trigEnt.Add(new CrashEngine.Importer.TriggerMarker { Source = t });
                        triggers++;
                    }

            var camSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_CAMERAS_SECTION);
            if (camSec is not null)
                for (int i = 0; i < camSec.GetItemsAmount(); i++)
                    if (camSec.GetItem(i) is PS2AnyCamera c)
                    {
                        var camEnt = MakeBoxMarker(camerasRoot, $"Camera_{c.GetID():X4}", c.CamTrigger.Position, c.CamTrigger.Scale,
                                      new Vector4(0.15f, 0.4f, 0.9f, 0.35f), rotation: c.CamTrigger.Rotation);
                        camEnt.Add(new CrashEngine.Importer.CameraMarker { Source = c });
                        cameras++;
                    }

            var posSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_POSITIONS_SECTION);
            if (posSec is not null)
                for (int i = 0; i < posSec.GetItemsAmount(); i++)
                    if (posSec.GetItem(i) is PS2AnyPosition p)
                    {
                        var posEnt = MakePointMarker(positionsRoot, $"Position_{p.GetID():X4}", p.Position,
                                        new Vector4(1f, 0.9f, 0.15f, 0.7f));
                        posEnt.Add(new CrashEngine.Importer.PositionMarker { Source = p });
                        positions++;
                    }

            var aiPosSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);
            if (aiPosSec is not null)
                for (int i = 0; i < aiPosSec.GetItemsAmount(); i++)
                    if (aiPosSec.GetItem(i) is PS2AnyAIPosition ap)
                    {
                        var aiPosEnt = MakeAiPositionMarker(aiPosRoot, $"AiPosition_{ap.GetID():X4}", ap.Position); // Amedo
                        aiPosEnt.Add(new CrashEngine.Importer.AiPositionMarker { Source = ap });
                        aiPositions++;
                    }
        }

        // Amedo 2026-09-19
        var sceneryForLights = chunkRoot.Get<ChunkSource>()?.Sm2
            ?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);
        if (sceneryForLights is not null) BuildSceneryLightMarkers(sceneryForLights);

        int loadScenes = 0;
        foreach (var linkEnt in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.LinkedSceneryLink>()))
        {
            var link = linkEnt.Get<CrashEngine.Importer.LinkedSceneryLink>()!.Source;
            if (link.LoadingWall is null) continue;
            MakeQuadMarker(loadScenesRoot, $"LoadScene_{System.IO.Path.GetFileName(link.Path)}",
                link, new Vector4(1f, 0.4f, 0.1f, 0.6f));
            loadScenes++;
        }

        int emitters = 0;
        var particleData = rm2.GetItem<PS2AnyParticleData>((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
        if (particleData is not null)
        {
            foreach (var em in particleData.ParticleEmitters)
            {
                var name = ParticleNames.Clean(em.Name); // Amedo 2026-09-26
                var e = new Entity($"ParticleEmitter_{(string.IsNullOrWhiteSpace(name) ? "unnamed" : name)}");
                e.Transform.Position = new Vector3(em.Position.X, em.Position.Y, em.Position.Z);
                e.Transform.Scale    = new Vector3(0.4f, 0.4f, 0.4f);
                var rdr = e.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = new Vector4(0.6f, 0.85f, 1f, 0.7f) });
                rdr.Mat.AlphaBlend = true;
                e.Add(new CrashEngine.Importer.ParticleEmitterMarker { Source = em });
                emittersRoot.AddChild(e);
                AddParticleEffect(effects, e, em, chunkRoot.Get<ChunkSource>()); // Amedo 2026-09-26
                emitters++;
            }
        }

        _browser.Log($"Layout markers: {triggers} triggers, {cameras} cameras, {positions} positions, " +
                      $"{aiPositions} AI positions, {loadScenes} load scenes, {emitters} particle emitters " +
                      "(all hidden by default, see Scene panel toggles).");
    }

    private Entity? _triggersRoot, _camerasRoot, _positionsRoot, _aiPosRoot, _loadScenesRoot, _particleEmittersRoot;
    private Entity? _lightsRoot; // Amedo 2026-09-19
    private bool _showTriggers, _showCameras, _showPositions, _showAiPositions, _showLoadScenes, _showParticleEmitters;
    private bool _showLights = true; // Amedo 2026-09-19
    private Entity? _particleEffectsRoot; // Amedo 2026-09-26
    private ParticleEffectsRenderer? _particleEffects; // Amedo 2026-09-26
    private bool _showParticleEffects = true, _particleDistanceGating = true; // Amedo 2026-09-26
}
