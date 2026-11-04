using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Runtime.AgentLab;
using CrashEngine.Runtime.Character;
using CrashEngine.Runtime.Physics;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.CompilerServices;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using TwinBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinBoundingBoxBuilder;
using ITwinObject = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject;

namespace CrashEngine.Runtime.Debug;

// Amedo 2026-10-03
public sealed class ObjectCollisionOverlay : Component
{
    public static readonly Vector4 Solid = new(0.2f, 1f, 0.35f, 1f);
    public static readonly Vector4 TouchOnly = new(1f, 0.85f, 0.15f, 1f);
    public static readonly Vector4 Ignored = new(0.62f, 0.62f, 0.66f, 1f);
    public static readonly Vector4 CharacterBox = new(0.25f, 0.8f, 1f, 1f);
    public static readonly Vector4 MovementSphere = new(1f, 0.5f, 0.1f, 1f);
    private const float FaceAlpha = 0.18f, XRayAlpha = 0.45f;
    private const uint CrashMask = 0x5B010, CrashExclude = 1, CrashAny = 0x18;
    private static readonly int[] ControllerTypes = [12, 14, 13, 15, 16, 17, 18, 19, 20];
    private static readonly ConditionalWeakTable<TwinBoundingBoxBuilder, ConvexHull> Hulls = new();
    private static readonly ConditionalWeakTable<PS2AnyOGI, Dictionary<int, Matrix4x4>> RestJoints = new();
    private static readonly Dictionary<int, ConvexHull> KindBoxes = new();

    private readonly Func<AgentLabWorld?> _world;
    private readonly Entity _chunkRoot;
    private readonly Dictionary<ConvexHull, (GpuMesh Faces, GpuMesh Edges)> _meshes = new(ReferenceEqualityComparer.Instance);
    private (GpuMesh Faces, GpuMesh Edges)? _sphere;
    private GpuMesh? _ring;
    private Vector3? _eye;
    private readonly Material _faceMat = new() { Unlit = true, Culling = Material.CullMode.Both, AlphaBlend = true, DepthWrite = false, FogEnabled = false };
    private readonly Material _edgeMat = new() { Unlit = true, Culling = Material.CullMode.Both, FogEnabled = false };
    private readonly Material _xrayMat = new() { Unlit = true, Culling = Material.CullMode.Both, AlphaBlend = true, DepthWrite = false, IgnoreDepthTest = true, FogEnabled = false };
    private readonly List<Entity> _instances = new();
    private readonly HashSet<AgentLabAgent> _playerLinks = new(ReferenceEqualityComparer.Instance);

    public ObjectCollisionOverlay(Entity chunkRoot, Func<AgentLabWorld?> world)
    {
        _world = world;
        _chunkRoot = chunkRoot;
    }

    public override bool IsTranslucent => true;

    public override void OnRender(GL gl)
    {
        if (RenderPipeline.Instance is not { } pipeline) return;
        var sh = pipeline.Shader;
        _eye = pipeline.Camera is { } cam && Matrix4x4.Invert(cam.View, out var inv) ? inv.Translation : null;
        if (_world() is { } world) RenderPlay(gl, sh, world);
        else RenderEdit(gl, sh);
    }

    private void RenderPlay(GL gl, TwinShaderProgram sh, AgentLabWorld world)
    {
        var player = world.Player;
        _playerLinks.Clear();
        if (player is not null)
            foreach (var l in player.Links) if (l is not null) _playerLinks.Add(l);
        foreach (var agent in world.Agents)
        {
            if (agent.Destroyed || !agent.Entity.Active) continue;
            var parent = agent.Entity.Parent?.Transform.World ?? Matrix4x4.Identity;
            if (agent.Body() is { } body)
            {
                bool crashIgnores = ReferenceEquals(agent, player) || _playerLinks.Contains(agent) ||
                                    (agent.AgentFlags & CrashExclude) != 0 || (agent.ComponentMask & CrashMask) == 0 || (agent.AgentFlags & CrashAny) == 0;
                var color = crashIgnores ? Ignored : body.Collidable ? Solid : TouchOnly;
                if (body.Sphere is { } s) DrawSphere(gl, sh, new Vector3(s.X, s.Y, s.Z), s.W, parent, color);
                else foreach (var (hull, part) in body.Parts) Draw(gl, sh, hull, part * parent, color);
            }
            if (agent.Physics is { Body: null } && 0f < agent.LogicalRadius)
                DrawSphere(gl, sh, agent.SphereCenter, agent.LogicalRadius, parent, MovementSphere);
            if (agent.OwnCharacter is { } c)
            {
                var hull = (c.Crouch.LowShape && (c.MotionFlagsHigh & 1u) != 0) || c.InAirState ? c.LowHull : c.StandHull;
                Draw(gl, sh, hull, Matrix4x4.CreateTranslation(agent.GamePosition) * parent, CharacterBox);
            }
        }
        foreach (var d in world.DynamicScenery) // Amedo 2026-10-06
        {
            var parent = d.Entity.Parent?.Transform.World ?? Matrix4x4.Identity;
            foreach (var (hull, part) in d.Body.Parts) Draw(gl, sh, hull, part * parent, d.Body.Collidable ? Solid : Ignored);
        }
    }

    // Amedo 2026-10-06
    private void RenderEditDynamicScenery(GL gl, TwinShaderProgram sh)
    {
        var root = _chunkRoot.Children.FirstOrDefault(c => c.Name == "Scenery")?.Children.FirstOrDefault(c => c.Name == "DynamicScenery");
        if (root is null || !root.Active) return;
        foreach (var e in root.Children)
        {
            if (!e.Active || e.Get<DynamicSceneryAnimator>()?.Model is not { } model) continue;
            var m = e.Transform.World;
            foreach (var builder in model.BoundingBoxBuilders) Draw(gl, sh, Hulls.GetValue(builder, ConvexHull.From), m, Solid);
        }
    }

    private void RenderEdit(GL gl, TwinShaderProgram sh)
    {
        var src = _chunkRoot.Get<ChunkSource>();
        RenderEditDynamicScenery(gl, sh); // Amedo 2026-10-06
        _instances.Clear();
        Collect(_chunkRoot, _instances);
        foreach (var e in _instances)
        {
            if (!e.Active || e.Get<InstanceData>() is not { } data) continue;
            var obj = src?.MeshTables is { } t ? MeshDecoder.GetRawObject(t, data.ObjectId) : null;
            var anim = e.Get<AnimatedObject>();
            var ogi = anim?.ModelGroup?.Ogi ?? anim?.CurrentGroup?.Ogi ?? anim?.DefaultGroup?.Ogi;
            if (ogi is null && src?.MeshTables is { } tables && MeshDecoder.ResolveDefaultOgiId(tables, data.ObjectId) is { } ogiId)
                ogi = MeshDecoder.GetRawOgi(tables, ogiId);
            var worldM = e.Transform.World;
            if (ogi is not null) // Amedo
            {
                var joints = anim?.ShownJointWorlds ?? anim?.JointWorlds ?? RestJoints.GetValue(ogi, MeshDecoder.ComputeJointWorlds);
                var color = data.ObjectId == 0 ? Ignored : EditColor(obj, data.StateFlags);
                for (int i = 0; i < OgiHulls.Count(ogi); i++) // Amedo
                {
                    int joint = OgiHulls.Joint(ogi, i);
                    var m = joint != 0xFF && joints.TryGetValue(joint, out var jw) ? jw * worldM : worldM;
                    Draw(gl, sh, OgiHulls.Of(ogi)[i], m, color);
                }
            }
            if (obj is { Type: ITwinObject.ObjectType.Character })
            {
                var parent = e.Parent?.Transform.World ?? Matrix4x4.Identity;
                int kind = Kind(data, obj);
                if (!KindBoxes.TryGetValue(kind, out var box))
                {
                    var (h, r, _, _) = CharacterController.BoxSize_13C758(kind);
                    KindBoxes[kind] = box = CharacterController.Box(h, r);
                }
                Draw(gl, sh, box, Matrix4x4.CreateTranslation(e.Transform.Local.Translation) * parent, CharacterBox);
            }
        }
    }

    private static Vector4 EditColor(PS2AnyObject? obj, uint stateFlags)
    {
        int type = obj is null ? -1 : (int)obj.Type;
        int controller = type >= 0 && type < ControllerTypes.Length ? ControllerTypes[type] : -1;
        bool inMask = controller >= 0 && (CrashMask & (1u << controller)) != 0;
        bool solid = (stateFlags & 2) != 0, touch = (stateFlags & 0x100) != 0;
        if (!inMask || (!solid && !touch)) return Ignored;
        return solid ? Solid : TouchOnly;
    }

    private static int Kind(InstanceData data, PS2AnyObject obj)
    {
        var p3 = data.Source.ParamList3;
        if (p3 is { Count: > 0 }) return (int)p3[0];
        return obj.InstIntegers is { Count: > 0 } d ? (int)d[0] : 0;
    }

    private static void Collect(Entity e, List<Entity> into)
    {
        foreach (var c in e.Children)
        {
            if (c.Get<InstanceData>() is not null) into.Add(c);
            else Collect(c, into);
        }
    }

    private void Draw(GL gl, TwinShaderProgram sh, ConvexHull hull, Matrix4x4 model, Vector4 color)
    {
        if (hull.Points.Length == 0) return;
        if (!_meshes.TryGetValue(hull, out var mesh)) _meshes[hull] = mesh = Build(gl, hull);
        DrawMesh(gl, sh, mesh, model, color);
    }

    private void DrawSphere(GL gl, TwinShaderProgram sh, Vector3 center, float radius, Matrix4x4 parent, Vector4 color)
    {
        if (!(0f < radius)) return;
        _sphere ??= BuildSphere(gl);
        DrawMesh(gl, sh, _sphere.Value, Matrix4x4.CreateScale(radius) * Matrix4x4.CreateTranslation(center) * parent, color);
        if (_eye is not { } eye) return;
        var wc = Vector3.Transform(center, parent);
        float wr = radius * GameMath.Length(new Vector3(parent.M11, parent.M12, parent.M13));
        var toEye = eye - wc;
        float d = GameMath.Length(toEye);
        if (d <= wr) return;
        var f = toEye / d;
        var right = GameMath.Normalize(Vector3.Cross(MathF.Abs(f.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX, f));
        var up = Vector3.Cross(f, right);
        var basis = new Matrix4x4(right.X, right.Y, right.Z, 0f, up.X, up.Y, up.Z, 0f, f.X, f.Y, f.Z, 0f, 0f, 0f, 0f, 1f);
        float k = wr / d;
        _ring ??= BuildRing(gl);
        sh.Set("StartModel", Matrix4x4.CreateScale(wr * MathF.Sqrt(1f - k * k)) * basis * Matrix4x4.CreateTranslation(wc + f * (wr * k)));
        _edgeMat.BaseColor = color;
        _edgeMat.Apply(gl, sh);
        _ring.Draw(gl);
        _edgeMat.Restore(gl, sh);
        _xrayMat.BaseColor = new Vector4(color.X, color.Y, color.Z, XRayAlpha);
        _xrayMat.Apply(gl, sh);
        _ring.Draw(gl);
        _xrayMat.Restore(gl, sh);
    }

    private static GpuMesh BuildRing(GL gl)
    {
        const int n = 48;
        var v = new GpuMesh.Vertex[n * 2];
        for (int k = 0; k < n; k++)
        {
            float a0 = 2f * MathF.PI * k / n, a1 = 2f * MathF.PI * (k + 1) / n;
            v[2 * k] = V(new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0f));
            v[2 * k + 1] = V(new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0f));
        }
        return new GpuMesh(gl, v, PrimitiveType.Lines);
    }

    private void DrawMesh(GL gl, TwinShaderProgram sh, (GpuMesh Faces, GpuMesh Edges) mesh, Matrix4x4 model, Vector4 color)
    {
        sh.Set("StartModel", model);
        _faceMat.BaseColor = new Vector4(color.X, color.Y, color.Z, FaceAlpha);
        _faceMat.Apply(gl, sh);
        mesh.Faces.Draw(gl);
        _faceMat.Restore(gl, sh);
        _edgeMat.BaseColor = color;
        _edgeMat.Apply(gl, sh);
        mesh.Edges.Draw(gl);
        _edgeMat.Restore(gl, sh);
    }

    private static GpuMesh.Vertex V(Vector3 p) => new() { Position = p, Normal = Vector3.UnitY, Color = Vector4.One };

    private static (GpuMesh Faces, GpuMesh Edges) BuildSphere(GL gl)
    {
        const int rings = 8, segments = 16, circle = 32;
        var faces = new List<GpuMesh.Vertex>();
        Vector3 P(int ring, int seg)
        {
            float t = MathF.PI * ring / rings, a = 2f * MathF.PI * seg / segments;
            return new Vector3(MathF.Sin(t) * MathF.Cos(a), MathF.Cos(t), MathF.Sin(t) * MathF.Sin(a));
        }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                faces.Add(V(P(r, s))); faces.Add(V(P(r + 1, s))); faces.Add(V(P(r + 1, s + 1)));
                faces.Add(V(P(r, s))); faces.Add(V(P(r + 1, s + 1))); faces.Add(V(P(r, s + 1)));
            }
        var edges = new List<GpuMesh.Vertex>();
        for (int k = 0; k < circle; k++)
        {
            float a0 = 2f * MathF.PI * k / circle, a1 = 2f * MathF.PI * (k + 1) / circle;
            var (c0, s0, c1, s1) = (MathF.Cos(a0), MathF.Sin(a0), MathF.Cos(a1), MathF.Sin(a1));
            edges.Add(V(new Vector3(c0, s0, 0f))); edges.Add(V(new Vector3(c1, s1, 0f)));
            edges.Add(V(new Vector3(c0, 0f, s0))); edges.Add(V(new Vector3(c1, 0f, s1)));
            edges.Add(V(new Vector3(0f, c0, s0))); edges.Add(V(new Vector3(0f, c1, s1)));
        }
        return (new GpuMesh(gl, faces.ToArray(), PrimitiveType.Triangles), new GpuMesh(gl, edges.ToArray(), PrimitiveType.Lines));
    }

    internal static (GpuMesh Faces, GpuMesh Edges) Build(GL gl, ConvexHull hull) // Amedo 2026-10-06
    {
        var faces = new List<GpuMesh.Vertex>();
        var edges = new List<GpuMesh.Vertex>();
        var seen = new HashSet<(int, int)>();
        void Edge(int a, int b)
        {
            if (a == b || !seen.Add(a < b ? (a, b) : (b, a))) return;
            edges.Add(V(hull.Points[a]));
            edges.Add(V(hull.Points[b]));
        }
        void Polygon(IReadOnlyList<int> idx)
        {
            for (int k = 1; k + 1 < idx.Count; k++)
            {
                faces.Add(V(hull.Points[idx[0]]));
                faces.Add(V(hull.Points[idx[k]]));
                faces.Add(V(hull.Points[idx[k + 1]]));
            }
            for (int k = 0; k < idx.Count; k++) Edge(idx[k], idx[(k + 1) % idx.Count]);
        }
        if (hull.FaceStart.Length > 0)
        {
            var idx = new List<int>();
            foreach (int start in hull.FaceStart)
            {
                idx.Clear();
                int count = start < hull.FaceVerts.Length ? hull.FaceVerts[start] : 0;
                for (int j = 0; j < count && start + 1 + j < hull.FaceVerts.Length; j++)
                    if (hull.FaceVerts[start + 1 + j] < hull.Points.Length) idx.Add(hull.FaceVerts[start + 1 + j]);
                if (idx.Count >= 3) Polygon(idx);
            }
        }
        else if (hull.Points.Length == 8)
        {
            int[][] quads = [[0, 1, 2, 3], [4, 7, 6, 5], [0, 4, 5, 1], [1, 5, 6, 2], [2, 6, 7, 3], [3, 7, 4, 0]];
            foreach (var q in quads) Polygon(q);
        }
        foreach (var (a, b) in hull.Edges)
            if (a < hull.Points.Length && b < hull.Points.Length) Edge(a, b);
        return (new GpuMesh(gl, faces.ToArray(), PrimitiveType.Triangles), new GpuMesh(gl, edges.ToArray(), PrimitiveType.Lines));
    }

    public override void OnDestroy()
    {
        foreach (var (f, e) in _meshes.Values) { f.Dispose(); e.Dispose(); }
        _meshes.Clear();
        if (_sphere is { } s) { s.Faces.Dispose(); s.Edges.Dispose(); }
        _sphere = null;
        _ring?.Dispose();
        _ring = null;
    }
}
