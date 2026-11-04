using CrashEngine.Core;
using CrashEngine.Renderer;
using CrashEngine.Runtime.Physics;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Runtime.Debug;

// Amedo 2026-10-06
public sealed class GameCameraOverlay : Component
{
    public static readonly Vector4 FrustumColor = new(1f, 0.86f, 0.25f, 1f);
    public static readonly Vector4 TargetColor = new(1f, 0.45f, 0.15f, 1f);
    public static readonly Vector4 ZoneColor = new(0.3f, 1f, 0.45f, 1f);
    public static readonly Vector4 OtherZoneColor = new(0.62f, 0.62f, 0.68f, 1f);
    public static readonly Vector4 TrailColor = new(0.35f, 0.78f, 1f, 1f);
    public const float ModelScale = 1.5f;
    private const float XRayAlpha = 0.3f, ZoneFaceAlpha = 0.07f, TargetSize = 0.25f, DefaultLength = 6f, MaxLength = 40f;
    private const int TrailSegments = 400;
    private const float TrailStep = 0.05f, TrailBreak = 3f;
    private static readonly Vector3 Light = GameMath.Normalize(new Vector3(-0.35f, 0.85f, 0.4f));

    private readonly Func<SceneSimulation?> _sim;
    private readonly Func<float> _aspect;
    private readonly string? _modelPath;
    private bool _loadTried;
    private GpuMesh? _model, _frustum, _segment, _cross, _trail;
    private Texture2D? _texture;
    private readonly Dictionary<ConvexHull, (GpuMesh Faces, GpuMesh Edges)> _zoneMeshes = new(ReferenceEqualityComparer.Instance);
    private readonly Vector3[] _trailA = new Vector3[TrailSegments], _trailB = new Vector3[TrailSegments];
    private readonly GpuMesh.Vertex[] _trailVerts = new GpuMesh.Vertex[TrailSegments * 2];
    private int _trailCount, _trailHead;
    private Vector3? _trailLast;
    private bool _trailDirty;
    private SceneSimulation? _trailSim;

    private readonly Material _modelMat = new() { Unlit = true, Culling = Material.CullMode.Both, FogEnabled = false };
    private readonly Material _xrayMat = new() { Unlit = true, Culling = Material.CullMode.Both, AlphaBlend = true, DepthWrite = false, IgnoreDepthTest = true, FogEnabled = false };
    private readonly Material _lineMat = new() { Unlit = true, Culling = Material.CullMode.Both, FogEnabled = false };
    private readonly Material _fadeMat = new() { Unlit = true, Culling = Material.CullMode.Both, AlphaBlend = true, DepthWrite = false, FogEnabled = false };

    public bool Visible { get; set; }
    public string? LoadError { get; private set; }

    public GameCameraOverlay(Func<SceneSimulation?> sim, Func<float> aspect, string? modelPath)
    {
        _sim = sim;
        _aspect = aspect;
        _modelPath = modelPath;
    }

    public override bool IsTranslucent => true;

    public override void OnUpdate()
    {
        var sim = _sim();
        if (!ReferenceEquals(sim, _trailSim))
        {
            _trailSim = sim;
            _trailCount = _trailHead = 0;
            _trailLast = null;
            _trailDirty = true;
        }
        if (sim is null || !sim.GameCameraView(out var eye, out _, out _, out _)) return;
        if (_trailLast is not { } last) { _trailLast = eye; return; }
        float d = GameMath.Distance(last, eye);
        if (d < TrailStep) return;
        _trailLast = eye;
        if (d > TrailBreak) return;
        _trailA[_trailHead] = last;
        _trailB[_trailHead] = eye;
        _trailHead = (_trailHead + 1) % TrailSegments;
        if (_trailCount < TrailSegments) _trailCount++;
        _trailDirty = true;
    }

    public override void OnRender(GL gl)
    {
        if (!Visible || RenderPipeline.Instance is not { } pipeline || _sim() is not { } sim || sim.GameCamera is not { } cam) return;
        if (!sim.GameCameraView(out var eye, out var forward, out var up, out float fov)) return;
        if (!_loadTried) Load(gl);
        var sh = pipeline.Shader;
        var right = GameMath.Normalize(Vector3.Cross(forward, up));
        var u = Vector3.Cross(right, forward);
        var basis = new Matrix4x4(right.X, right.Y, right.Z, 0f, u.X, u.Y, u.Z, 0f, -forward.X, -forward.Y, -forward.Z, 0f, eye.X, eye.Y, eye.Z, 1f);
        var gameToWorld = sim.RootToWorld;

        DrawZones(gl, sh, cam, gameToWorld);
        DrawTrail(gl, sh);

        var focus = sim.World.Player is { } player ? player.Entity.Transform.World.Translation : eye + forward * 5f;
        float depth = GameMath.Dot(focus - eye, forward);
        float length = depth >= 1f ? MathF.Min(depth, MaxLength) : DefaultLength;
        var target = eye + forward * length;
        float ty = MathF.Tan(fov * MathF.PI / 360f);
        _frustum ??= BuildFrustum(gl);
        DrawLines(gl, sh, _frustum, Matrix4x4.CreateScale(ty * _aspect() * length, ty * length, length) * basis, FrustumColor);
        _segment ??= new GpuMesh(gl, [V(Vector3.Zero), V(Vector3.UnitZ)], PrimitiveType.Lines);
        DrawLines(gl, sh, _segment, Segment(eye, target), TargetColor);
        _cross ??= BuildCross(gl);
        DrawLines(gl, sh, _cross, Matrix4x4.CreateScale(TargetSize) * Matrix4x4.CreateTranslation(target), TargetColor);

        if (_model is null) return;
        var model = Matrix4x4.CreateScale(ModelScale) * basis;
        sh.Set("StartModel", model);
        _xrayMat.Albedo = _texture;
        _xrayMat.BaseColor = new Vector4(1f, 1f, 1f, XRayAlpha);
        _xrayMat.Apply(gl, sh);
        _model.Draw(gl);
        _xrayMat.Restore(gl, sh);
        _modelMat.Albedo = _texture;
        _modelMat.Apply(gl, sh);
        _model.Draw(gl);
        _modelMat.Restore(gl, sh);
    }

    private void DrawZones(GL gl, TwinShaderProgram sh, Camera.RuntimeGameCamera cam, Matrix4x4 gameToWorld)
    {
        uint current = cam.CurrentZone;
        var overlapping = cam.OverlappingZones;
        for (int i = 0; i < overlapping.Count; i++)
        {
            uint z = overlapping[i];
            if (z == current || !cam.ZoneShape(z, out var hull, out var m)) continue;
            DrawLines(gl, sh, ZoneMesh(gl, hull).Edges, m * gameToWorld, OtherZoneColor);
        }
        if (current == 0 || !cam.ZoneShape(current, out var ch, out var cm)) return;
        var mesh = ZoneMesh(gl, ch);
        sh.Set("StartModel", cm * gameToWorld);
        _fadeMat.Albedo = null;
        _fadeMat.BaseColor = ZoneColor with { W = ZoneFaceAlpha };
        _fadeMat.Apply(gl, sh);
        mesh.Faces.Draw(gl);
        _fadeMat.Restore(gl, sh);
        DrawLines(gl, sh, mesh.Edges, cm * gameToWorld, ZoneColor);
    }

    private (GpuMesh Faces, GpuMesh Edges) ZoneMesh(GL gl, ConvexHull hull)
    {
        if (!_zoneMeshes.TryGetValue(hull, out var mesh)) _zoneMeshes[hull] = mesh = ObjectCollisionOverlay.Build(gl, hull);
        return mesh;
    }

    private void DrawTrail(GL gl, TwinShaderProgram sh)
    {
        if (_trailCount == 0) return;
        if (_trailDirty)
        {
            _trailDirty = false;
            int start = (_trailHead - _trailCount + TrailSegments) % TrailSegments;
            for (int k = 0; k < TrailSegments; k++)
            {
                if (k >= _trailCount)
                {
                    _trailVerts[2 * k] = default;
                    _trailVerts[2 * k + 1] = default;
                    continue;
                }
                int i = (start + k) % TrailSegments;
                float a0 = (float)k / _trailCount, a1 = (k + 1f) / _trailCount;
                _trailVerts[2 * k] = new GpuMesh.Vertex { Position = _trailA[i], Normal = Vector3.UnitY, Color = TrailColor with { W = a0 } };
                _trailVerts[2 * k + 1] = new GpuMesh.Vertex { Position = _trailB[i], Normal = Vector3.UnitY, Color = TrailColor with { W = a1 } };
            }
            if (_trail is null) _trail = new GpuMesh(gl, _trailVerts, PrimitiveType.Lines);
            else _trail.UpdateVertices(gl, _trailVerts);
        }
        if (_trail is null) return;
        sh.Set("StartModel", Matrix4x4.Identity);
        _fadeMat.Albedo = null;
        _fadeMat.BaseColor = Vector4.One;
        _fadeMat.Apply(gl, sh);
        _trail.Draw(gl);
        _fadeMat.Restore(gl, sh);
    }

    private void DrawLines(GL gl, TwinShaderProgram sh, GpuMesh mesh, Matrix4x4 model, Vector4 color)
    {
        sh.Set("StartModel", model);
        _lineMat.BaseColor = color;
        _lineMat.Apply(gl, sh);
        mesh.Draw(gl);
        _lineMat.Restore(gl, sh);
    }

    private static Matrix4x4 Segment(Vector3 a, Vector3 b)
    {
        var d = b - a;
        float len = GameMath.Length(d);
        if (len < 1e-5f) return Matrix4x4.CreateScale(0f) * Matrix4x4.CreateTranslation(a);
        var z = d / len;
        var x = GameMath.Normalize(Vector3.Cross(MathF.Abs(z.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX, z));
        var y = Vector3.Cross(z, x);
        return new Matrix4x4(x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, d.X, d.Y, d.Z, 0f, a.X, a.Y, a.Z, 1f);
    }

    private void Load(GL gl)
    {
        _loadTried = true;
        if (_modelPath is null || !File.Exists(_modelPath)) { LoadError = $"camera model not found: {_modelPath}"; return; }
        var parts = CrashEngine.Assets.ModelImporter.ExtractRawSubmeshes(_modelPath);
        if (parts is not { Count: > 0 }) { LoadError = $"camera model could not be read: {_modelPath}"; return; }
        var verts = new List<GpuMesh.Vertex>();
        var idx = new List<uint>();
        foreach (var p in parts)
        {
            uint baseIndex = (uint)verts.Count;
            for (int i = 0; i < p.Positions.Length; i++)
            {
                float shade = 0.6f + 0.4f * MathF.Max(0f, GameMath.Dot(p.Normals[i], Light));
                verts.Add(new GpuMesh.Vertex { Position = p.Positions[i], Normal = p.Normals[i], UV = p.UVs[i], Color = new Vector4(shade, shade, shade, 1f) });
            }
            foreach (uint i in p.Indices) idx.Add(baseIndex + i);
            if (_texture is null && p.DiffusePixelsRGBA is { } px && p.TexWidth > 0 && p.TexHeight > 0)
                _texture = new Texture2D(gl, px, (uint)p.TexWidth, (uint)p.TexHeight);
        }
        _model = new GpuMesh(gl, verts.ToArray(), idx.ToArray());
    }

    private static GpuMesh.Vertex V(Vector3 p) => new() { Position = p, Normal = Vector3.UnitY, Color = Vector4.One };

    private static GpuMesh BuildFrustum(GL gl)
    {
        Vector3[] c = [new(-1f, -1f, -1f), new(1f, -1f, -1f), new(1f, 1f, -1f), new(-1f, 1f, -1f)];
        var v = new List<GpuMesh.Vertex>();
        for (int k = 0; k < 4; k++)
        {
            v.Add(V(Vector3.Zero)); v.Add(V(c[k]));
            v.Add(V(c[k])); v.Add(V(c[(k + 1) & 3]));
        }
        Vector3 l = new(-0.35f, 1.1f, -1f), r = new(0.35f, 1.1f, -1f), top = new(0f, 1.45f, -1f);
        v.Add(V(l)); v.Add(V(r)); v.Add(V(r)); v.Add(V(top)); v.Add(V(top)); v.Add(V(l));
        return new GpuMesh(gl, v.ToArray(), PrimitiveType.Lines);
    }

    private static GpuMesh BuildCross(GL gl) => new(gl,
        [V(-Vector3.UnitX), V(Vector3.UnitX), V(-Vector3.UnitY), V(Vector3.UnitY), V(-Vector3.UnitZ), V(Vector3.UnitZ)], PrimitiveType.Lines);

    public override void OnDestroy()
    {
        foreach (var (f, e) in _zoneMeshes.Values) { f.Dispose(); e.Dispose(); }
        _zoneMeshes.Clear();
        _model?.Dispose();
        _texture?.Dispose();
        _frustum?.Dispose();
        _segment?.Dispose();
        _cross?.Dispose();
        _trail?.Dispose();
        _model = _frustum = _segment = _cross = _trail = null;
        _texture = null;
    }
}
