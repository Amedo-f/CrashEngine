using System.Numerics;
using System.Runtime.CompilerServices;
using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;

namespace CrashEngine.Importer;

// Amedo 2026-09-26
public static class ParticleFx
{
    private static readonly int[] DrawOrder = { 3, 2, 1, 0 };
    private static readonly ConditionalWeakTable<TwinParticleSystem, ParticleDef> Defs = new();
    private static ParticleBatch? _batch;
    private static ParticleBatch.Vertex[] _verts = new ParticleBatch.Vertex[4096];

    public static ParticleDef DefFor(TwinParticleSystem sys) => Defs.GetValue(sys, s => new ParticleDef(s));

    public static void Draw(GL gl, ParticleWorld world, Matrix4x4 view, Matrix4x4 projection, Matrix4x4 toEditor,
                            MeshDecoder.ParticleTexturePage[] pages, bool drawDistanceCull)
    {
        _batch ??= new ParticleBatch(gl);
        var right = Vector3.Normalize(new Vector3(view.M11, view.M21, view.M31));
        var down = -Vector3.Normalize(new Vector3(view.M12, view.M22, view.M32));
        float now = world.RenderTime;
        bool begun = false;

        foreach (int blend in DrawOrder)
        {
            for (int page = 0; page < pages.Length; page++)
            {
                var tex = pages[page].Tex;
                if (tex is null) continue;
                int n = 0;
                foreach (var em in world.Emitters)
                {
                    if (!em.Enabled || em.Def.Blend != blend || em.Def.Page != page || !world.SceneDrawn_1E8(em)) continue; // Amedo
                    if (drawDistanceCull && !em.IgnoreDistance && em.LastDistance > em.Def.DrawMaxDist) continue; // Amedo 2026-09-27
                    n = AppendEmitter(em, now, toEditor, right, down, pages[page], n);
                }
                if (n == 0) continue;
                if (!begun) { _batch.Begin(view * projection); begun = true; }
                _batch.Draw(_verts.AsSpan(0, n), tex, (ParticleBatch.BlendKind)blend);
            }
        }
        if (begun) _batch.End();
    }

    private static ParticleBatch.Vertex V(Vector3 p, Vector2 uv, Vector4 c) => new() { Position = p, UV = uv, Color = c };

    // Amedo 2026-09-30
    public static void DrawDecals(GL gl, ParticleWorld world, Matrix4x4 view, Matrix4x4 projection, Matrix4x4 toEditor)
    {
        var decals = world.Decals;
        if (decals.Count == 0 || decals.Assets is not { Texture: { } tex } assets) return;
        _batch ??= new ParticleBatch(gl);
        float carry = world.RenderTime - world.Clock;
        bool begun = false;
        for (int key = 0; key < DecalWorld.Keys; key++)
        {
            int n = 0;
            for (int g = 0; g < DecalAssets.Groups; g++)
                for (int b = decals.GroupHead[g]; b >= 0; b = decals.Pool[b].Next)
                {
                    var batch = decals.Pool[b];
                    if (batch.Key != key) continue;
                    for (int i = 0; i < batch.Count; i++)
                    {
                        ref readonly var d = ref batch.Items[i];
                        if (assets.TypeOf(g, d.Type) is not { } t || 2 * d.Flags + 1 >= assets.Uv.Length) continue;
                        float u3 = (1f - MathF.Max(0f, (d.Frames + 1) / 60f - carry) * t.Rate) * 3f;
                        int s = (int)u3;
                        float f = u3 - s;
                        if (s > 2) { s = 2; f = 1f; }
                        else if (s < 0) s = 0;
                        var c = Vector4.Lerp(t.Colour[s], t.Colour[s + 1], f);
                        var col = new Vector4(MathF.Truncate(c.X), MathF.Truncate(c.Y), MathF.Truncate(c.Z), MathF.Truncate(c.W)) * (1f / 128f);
                        var size = Vector4.Lerp(t.Size[s], t.Size[s + 1], f);
                        var side = d.Side * size.X;
                        var dir = d.Dir * size.Z;
                        var a = (dir - side) * 0.5f;
                        var bb = (dir + side) * 0.5f;
                        var row0 = assets.Uv[2 * d.Flags];
                        var row1 = assets.Uv[2 * d.Flags + 1];
                        if (n + 4 > _verts.Length) Array.Resize(ref _verts, _verts.Length * 2);
                        _verts[n++] = V(Vector3.Transform(d.Position + a, toEditor), new Vector2(row0.X, row0.Y), col);
                        _verts[n++] = V(Vector3.Transform(d.Position + bb, toEditor), new Vector2(row0.Z, row0.W), col);
                        _verts[n++] = V(Vector3.Transform(d.Position - bb, toEditor), new Vector2(row1.X, row1.Y), col);
                        _verts[n++] = V(Vector3.Transform(d.Position - a, toEditor), new Vector2(row1.Z, row1.W), col);
                    }
                }
            if (n == 0) continue;
            if (!begun) { _batch.Begin(view * projection); begun = true; }
            _batch.Draw(_verts.AsSpan(0, n), tex, (ParticleBatch.BlendKind)key);
        }
        if (begun) _batch.End();
    }

    private static Texture2D? _white; // Amedo

    // Amedo
    public static void DrawSkidMarks(GL gl, ParticleWorld world, Matrix4x4 view, Matrix4x4 projection, Matrix4x4 toEditor)
    {
        if (!Matrix4x4.Invert(view, out var eyeWorld)) return;
        var toWorld = world.SkidToAnchor * toEditor;
        var eye = eyeWorld.Translation;
        float s = 1f;
        if (projection.M44 == 0f)
        {
            float depth = -Vector3.Transform(toWorld.Translation, view).Z;
            float k = 1.15f + 0.15f * (GameFog.ZBias / GameFog.ZScale) * depth;
            if (!(0f < k)) return;
            s = 1f / k;
        }
        _batch ??= new ParticleBatch(gl);
        _white ??= Texture2D.White(gl);
        bool date = GameShadowPass.ReceiversMarked;
        bool begun = false;
        for (int pass = 0; pass < 2; pass++)
        {
            bool add = pass == 0;
            int n = 0;
            foreach (var ring in world.SkidMarks)
                for (int i = 0; i < SkidMarkRing.Count; i++)
                {
                    if (!ring.Used[i] || ring.Quads[i].Add != add) continue;
                    ref readonly var q = ref ring.Quads[i];
                    if (n + 4 > _verts.Length) Array.Resize(ref _verts, _verts.Length * 2);
                    _verts[n++] = SkidVertex(q.A, q.ColorA, toWorld, eye, s);
                    _verts[n++] = SkidVertex(q.B, q.ColorB, toWorld, eye, s);
                    _verts[n++] = SkidVertex(q.C, q.ColorC, toWorld, eye, s);
                    _verts[n++] = SkidVertex(q.D, q.ColorD, toWorld, eye, s);
                }
            if (n == 0) continue;
            if (!begun)
            {
                _batch.Begin(view * projection);
                if (date)
                {
                    gl.Enable(EnableCap.StencilTest);
                    gl.StencilMask(0);
                    gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
                    gl.StencilFunc(StencilFunction.Equal, 0, GameShadowPass.ReceiverBit);
                }
                begun = true;
            }
            _batch.Draw(_verts.AsSpan(0, n), _white, add ? ParticleBatch.BlendKind.Add : ParticleBatch.BlendKind.Subtract, 0f);
        }
        if (!begun) return;
        if (date)
        {
            gl.Disable(EnableCap.StencilTest);
            gl.StencilMask(0xFF);
        }
        _batch.End();
    }

    private static ParticleBatch.Vertex SkidVertex(Vector3 p, uint c, Matrix4x4 toWorld, Vector3 eye, float s) =>
        V(eye + (Vector3.Transform(p, toWorld) - eye) * s, Vector2.Zero,
          new Vector4((c & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, ((c >> 16) & 0xFF) / 255f, (c >> 24) / 128f));

    private static int AppendEmitter(ParticleEmitterSim em, float now, Matrix4x4 toEditor, Vector3 right, Vector3 down,
                                     MeshDecoder.ParticleTexturePage page, int n)
    {
        var d = em.Def;
        float iw = 1f / Math.Max(page.Width, 1), ih = 1f / Math.Max(page.Height, 1);
        var uv00 = new Vector2(d.UvRect.X * iw, d.UvRect.Y * ih);
        var uv01 = new Vector2(d.UvRect.X * iw, d.UvRect.W * ih);
        var uv10 = new Vector2(d.UvRect.Z * iw, d.UvRect.Y * ih);
        var uv11 = new Vector2(d.UvRect.Z * iw, d.UvRect.W * ih);
        var rx = right * em.Scale / ParticleDef.FocalX; // Amedo
        var dy = down * em.Scale / ParticleDef.FocalY;

        var ring = em.Ring;
        for (int i = 0; i < ring.Length; i++)
        {
            ref readonly var p = ref ring[i];
            if (!p.Used) continue;
            float age = now - p.Birth;
            if (age < 0f) continue;
            float s = age * p.Rate;
            if (s > ParticleDef.DeadSample) continue;

            int si = (int)s;
            float f = s - si;
            si &= 63;
            ref readonly var s0 = ref d.Samples[si];
            ref readonly var s1 = ref d.Samples[Math.Min(si + 1, ParticleDef.SampleCount - 1)];
            var a = Vector2.Lerp(s0.A, s1.A, f);
            var dd = Vector2.Lerp(s0.D, s1.D, f);
            var e = Vector2.Lerp(s0.E, s1.E, f);

            var local = p.Pos + p.Vel * age + new Vector3(0f, d.GravityHalf * em.Scale * age * age, 0f); // Amedo
            var c = Vector3.Transform(em.DrawOrigin + Vector3.TransformNormal(local, em.RenderRotation), toEditor); // Amedo 2026-09-27

            if (n + 4 > _verts.Length) Array.Resize(ref _verts, _verts.Length * 2);
            var col = s0.Color;
            _verts[n++] = V(c + rx * a.X + dy * a.Y, uv00, col);
            _verts[n++] = V(c + rx * (e.X - dd.X) + dy * (e.Y - dd.Y), uv01, col);
            _verts[n++] = V(c + rx * (a.X + dd.X) + dy * (a.Y + dd.Y), uv10, col);
            _verts[n++] = V(c + rx * e.X + dy * e.Y, uv11, col);
        }
        return n;
    }

    public static bool LiveBounds(ParticleWorld world, Matrix4x4 toEditor, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        float now = world.RenderTime;
        bool any = false;
        foreach (var em in world.Emitters)
        {
            if (!em.Enabled) continue;
            float half = 0f;
            foreach (var smp in em.Def.Samples)
                half = MathF.Max(half, MathF.Max(MathF.Abs(smp.A.X) + MathF.Abs(smp.D.X), MathF.Abs(smp.A.Y) + MathF.Abs(smp.D.Y)));
            half *= em.Scale / ParticleDef.FocalX; // Amedo
            foreach (ref readonly var p in em.Ring.AsSpan())
            {
                if (!p.Used) continue;
                float age = now - p.Birth;
                if (age < 0f || age * p.Rate > ParticleDef.DeadSample) continue;
                var local = p.Pos + p.Vel * age + new Vector3(0f, em.Def.GravityHalf * em.Scale * age * age, 0f); // Amedo
                var c = Vector3.Transform(em.DrawOrigin + Vector3.TransformNormal(local, em.RenderRotation), toEditor); // Amedo 2026-09-27
                min = Vector3.Min(min, c - new Vector3(half));
                max = Vector3.Max(max, c + new Vector3(half));
                any = true;
            }
        }
        return any;
    }
}

// Amedo 2026-09-26
public sealed class ParticleEffectsRenderer : Component
{
    public sealed class Link
    {
        public required Entity Marker;
        public required TwinParticleEmitter Source;
        public required ParticleEmitterSim Sim;
    }

    public readonly ParticleWorld World = new();
    public readonly List<Link> Links = new();
    public MeshDecoder.ParticleTexturePage[]? Pages { get; set; }
    public bool DistanceGating { get; set; } = true;
    public readonly HashSet<Entity> PausedRoots = new(); // Amedo 2026-09-29
    public Func<bool>? ClockStopped { get; set; } // Amedo

    public override bool IsTranslucent => true;

    public void AddEmitter(Entity marker, TwinParticleEmitter source, TwinParticleSystem sys)
    {
        var sim = new ParticleEmitterSim(ParticleFx.DefFor(sys), World, source.UnkInt1);
        sim.ApplyEmitter(source);
        sim.Origin = marker.Transform.Position;
        World.Emitters.Add(sim);
        Links.Add(new Link { Marker = marker, Source = source, Sim = sim });
    }

    public override void OnUpdate()
    {
        foreach (var l in Links)
        {
            bool present = l.Marker.Parent is { } parent && !PausedRoots.Contains(parent); // Amedo 2026-09-29
            if (!present && l.Sim.Enabled) l.Sim.Clear();
            l.Sim.Enabled = present;
            l.Sim.Origin = l.Marker.Transform.Position;
            l.Sim.ApplyEmitter(l.Source);
        }

        Vector3? cam = null;
        var camera = RenderPipeline.Instance?.Camera;
        if (camera is not null && Matrix4x4.Invert(Transform.World, out var inv))
            cam = Vector3.Transform(camera.Transform.Position, inv);
        World.Advance(ClockStopped?.Invoke() == true ? 0f : EngineTime.Delta, cam, DistanceGating); // Amedo
    }

    public DecalAssets? Decals { get => World.Decals.Assets; set => World.Decals.Assets = value; } // Amedo 2026-09-30

    public override void OnRender(GL gl)
    {
        var pipeline = RenderPipeline.Instance;
        var camera = pipeline?.Camera;
        if (camera is null) return;
        bool drew = false; // Amedo 2026-09-30
        if (World.SkidMarks.Count > 0) // Amedo
        {
            ParticleFx.DrawSkidMarks(gl, World, camera.View, camera.Projection, Transform.World);
            drew = true;
        }
        if (World.Decals.Count > 0)
        {
            ParticleFx.DrawDecals(gl, World, camera.View, camera.Projection, Transform.World);
            drew = true;
        }
        if (Pages is not null && World.Emitters.Count > 0)
        {
            ParticleFx.Draw(gl, World, camera.View, camera.Projection, Transform.World, Pages, drawDistanceCull: true);
            drew = true;
        }
        if (drew) pipeline!.Shader.Use();
    }
}
