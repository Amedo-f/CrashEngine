using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using PS2AnyAIPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPath;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
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

    private MarkerModel? _aiSkull;
    private bool _aiSkullTried;
    private MarkerModel? EnsureAiSkullModel(GL gl)
    {
        if (_aiSkullTried) return _aiSkull;
        _aiSkullTried = true;
        return _aiSkull = LoadMarkerModel(gl, "AiSkull.glb");
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
                if (!Matrix4x4.Invert(e.Transform.World, out var toLocal)) continue;
                var to = Vector3.TransformNormal(eye - icon.Transform.World.Translation, toLocal);
                float flat = MathF.Sqrt(to.X * to.X + to.Z * to.Z);
                icon.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(MathF.Atan2(to.X, to.Z), -MathF.Atan2(to.Y, flat), 0f);
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
}
