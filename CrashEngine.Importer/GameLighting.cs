using System.Numerics;
using System.Runtime.CompilerServices;
using CrashEngine.Core;
using CrashEngine.Renderer;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using TwinLight = Twinsanity.TwinsanityInterchange.Common.Lights.Light;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace CrashEngine.Importer;

// Amedo 2026-09-26
public sealed class GameLightSet
{
    public Vector3 Ambient;
    public readonly Vector3[] Direction = new Vector3[3];
    public readonly Vector3[] Color = new Vector3[3];
    public int Used;
}

// Amedo 2026-09-26
public sealed class GameLightingEnvironment : Component
{
    public static bool Enabled { get; set; } = true;

    public PS2AnyScenery? Scenery { get; set; }
    public Vector3? AmbientOverride { get; set; }
    public List<(Vector3 Color, Vector3 Direction)>? DirectionalOverride { get; set; }

    private readonly float[] _intensity = new float[3];
    private readonly Vector4[] _color = new Vector4[3];
    private readonly Vector3[] _dir = new Vector3[3];

    public bool HasLights => Scenery is { HasLighting: true } s &&
        s.AmbientLights.Count + s.DirectionalLights.Count + s.PointLights.Count + s.NegativeLights.Count > 0;

    // Amedo
    public Vector3? FirstDirectional()
    {
        if (Scenery is not { HasLighting: true } s) return null;
        bool[]? mask = s.Sceneries.Count > 0 ? s.Sceneries[0].LightsEnabler : null;
        for (int g = 0, n = BitLimit(s, mask); g < n; g++) // Amedo
        {
            if (!EnabledLight_1C77C0(s, mask, g, out int type, out int k) || type != 1) continue;
            var dir = DirectionalOverride is { } ov && k < ov.Count ? ov[k].Direction : V3(s.DirectionalLights[k].UnkVec3);
            if (GameMath.LengthSquared(dir) < 1e-12f) continue;
            return GameMath.Normalize(dir);
        }
        return null;
    }

    // Amedo
    public override void OnRenderDepth(Silk.NET.OpenGL.GL gl)
    {
        if (!SunShadowPass.InDepthPass) return;
        AddPointLights(); // Amedo
        if (SunShadowPass.LevelDirection is not null) return;
        if (FirstDirectional() is { } dir) SunShadowPass.LevelDirection = dir;
    }

    // Amedo
    private void AddPointLights()
    {
        if (Scenery is not { HasLighting: true } s || s.PointLights.Count == 0) return;
        bool[]? mask = s.Sceneries.Count > 0 ? s.Sceneries[0].LightsEnabler : null;
        var world = Entity?.Transform.World ?? Matrix4x4.Identity;
        for (int g = 0, n = BitLimit(s, mask); g < n; g++) // Amedo
        {
            if (!EnabledLight_1C77C0(s, mask, g, out int type, out int k) || type != 2) continue;
            var p = s.PointLights[k];
            SunShadowPass.AddPointLight(Vector3.Transform(V3(p.Position), world), V3(p.Color), p.Radius, p.UnkShort);
        }
    }

    private static Vector3 V3(TwinVec4 v) => new(v.X, v.Y, v.Z);
    private static Vector4 V4(TwinVec4 v) => new(v.X, v.Y, v.Z, v.W);

    // Amedo
    private static int BitLimit(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery s, bool[]? mask) =>
        mask is null ? Math.Min(128, s.AmbientLights.Count + s.DirectionalLights.Count + s.PointLights.Count + s.NegativeLights.Count) : Math.Min(128, mask.Length);

    // Amedo
    private static bool EnabledLight_1C77C0(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery s, bool[]? mask, int bit, out int type, out int index)
    {
        type = index = -1;
        if (mask is not null && !mask[bit]) return false;
        (type, index) = s.LightAt(bit);
        int count = type switch { 0 => s.AmbientLights.Count, 1 => s.DirectionalLights.Count, 2 => s.PointLights.Count, 3 => s.NegativeLights.Count, _ => 0 };
        return index >= 0 && index < count;
    }

    public void Compute(Vector3 objectPos, GameLightSet result)
    {
        var s = Scenery!;
        for (int i = 0; i < 3; i++)
        {
            _intensity[i] = -1f;
            _color[i] = new Vector4(0f, 0f, 0f, 1f);
            _dir[i] = Vector3.Zero;
        }
        var ambient = Vector3.Zero;
        bool[]? mask = s.Sceneries.Count > 0 ? s.Sceneries[0].LightsEnabler : null;

        for (int g = 0, n = BitLimit(s, mask); g < n; g++) // Amedo
        {
            if (!EnabledLight_1C77C0(s, mask, g, out int type, out int k)) continue;
            if (type == 0)
            {
                var a = s.AmbientLights[k];
                var c = k == 0 && AmbientOverride is { } ao ? ao : V3(a.Color);
                ambient += c * a.Radius;
                continue;
            }

            Vector3 dir;
            float intensity;
            Vector4 color;
            if (type == 1)
            {
                var d = s.DirectionalLights[k];
                dir = V3(d.UnkVec3);
                intensity = d.Radius;
                color = V4(d.Color);
                if (DirectionalOverride is { } ov && k < ov.Count)
                {
                    color = new Vector4(ov[k].Color, d.Color.W);
                    dir = ov[k].Direction;
                }
            }
            else if (type == 2)
            {
                var p = s.PointLights[k];
                dir = PointTerms(p, objectPos, out float f);
                intensity = Falloff(p.Radius, f, p.UnkShort);
                color = V4(p.Color);
            }
            else
            {
                var sp = s.NegativeLights[k];
                dir = PointTerms(sp, objectPos, out float f);
                intensity = SpotIntensity(sp, dir, Falloff(sp.Radius, f, (short)sp.UnkUShort1));
                color = V4(sp.Color);
            }
            Insert(dir, intensity, color);
        }

        result.Ambient = ambient * 0.5f;
        result.Used = 0;
        for (int i = 0; i < 3; i++)
        {
            var rgb = new Vector3(_color[i].X, _color[i].Y, _color[i].Z);
            bool used = _intensity[i] >= 0f && result.Used == i;
            result.Color[i] = used ? rgb * (_intensity[i] * 0.5f) : rgb;
            result.Direction[i] = _dir[i];
            if (used) result.Used++;
        }
    }

    private void Insert(Vector3 dir, float intensity, Vector4 color)
    {
        for (int t = 0; t < 3; t++)
        {
            if (!(_intensity[t] < intensity)) continue;
            for (int j = 2; j > t; j--)
            {
                _intensity[j] = _intensity[j - 1];
                _color[j] = _color[j - 1];
                _dir[j] = _dir[j - 1];
            }
            _intensity[t] = intensity;
            _color[t] = color;
            _dir[t] = dir;
            return;
        }
    }

    private static Vector3 PointTerms(TwinLight l, Vector3 objectPos, out float f)
    {
        var d = V3(l.Position) - objectPos;
        f = 25f / (d.X * d.X + d.Y * d.Y + d.Z * d.Z + 25f); // Amedo
        return d * f;
    }

    private static float Falloff(float radius, float f, int exponent)
    {
        float i = radius;
        for (int k = exponent; k > 0; k--) i *= f;
        return i;
    }

    private static float SpotIntensity(Twinsanity.TwinsanityInterchange.Common.Lights.NegativeLight sp, Vector3 dir, float intensity)
    {
        var v = -V3(sp.UnkVec3); // Amedo
        float c = dir.X * v.X + dir.Y * v.Y + dir.Z * v.Z;
        if (c < sp.UnkFloat2) return 0f;
        if (c < sp.UnkFloat1)
            intensity *= Math.Clamp((c - sp.UnkFloat2) / (sp.UnkFloat1 - sp.UnkFloat2), 0f, 1f);
        int e = sp.UnkUShort2;
        float p = 1f;
        for (int bit = 0x80; bit != 0; bit >>= 1)
            p = (e & bit) != 0 ? p * (p * c) : p * p; // Amedo
        return intensity * p;
    }
}

// Amedo 2026-09-26
public static class GameLightingDraw
{
    private sealed class Owner
    {
        public Entity? Instance;
        public Entity? InstanceParent; // Amedo 2026-09-30
        public Entity? ChunkRoot;
        public GameLightingEnvironment? Environment;
    }

    // Amedo 2026-09-30
    public static float FadeSeconds { get; set; } = 1f;
    private static int _epoch;

    // Amedo 2026-09-30
    public static void SkipFades() => _epoch++;

    public static void Forget()
    {
        Owners.Clear();
        Cache.Clear();
        _epoch++;
    }

    public static void ForgetChunk(Entity chunkRoot)
    {
        foreach (var (entity, owner) in Owners)
            if (ReferenceEquals(owner.ChunkRoot, chunkRoot)) Owners.Remove(entity);
        foreach (var (_, lit) in Cache)
            if (ReferenceEquals(lit.Environment?.Entity, chunkRoot)) lit.Environment = null;
    }

    private static readonly ConditionalWeakTable<Entity, Owner> Owners = new();
    private static readonly GameLightSet Set = new();

    // Amedo 2026-09-29
    private sealed class Lit
    {
        public int Frame = -1;
        public bool Ok;
        public Vector3 Ambient;
        public readonly Vector3[] Direction = new Vector3[3];
        public readonly Vector3[] Color = new Vector3[3];
        public GameLightingEnvironment? Environment; // Amedo 2026-09-30
        public int Epoch = -1;
        public bool Fading;
        public float FadeStart;
        public Vector3 FromAmbient;
        public readonly Vector3[] FromDirection = new Vector3[3];
        public readonly Vector3[] FromColor = new Vector3[3];
    }

    private static readonly ConditionalWeakTable<Entity, Lit> Cache = new();
    private static readonly string[] DirNames = ["ObjLightDir[0]", "ObjLightDir[1]", "ObjLightDir[2]", "ObjLightDir[3]", "ObjLightDir[4]", "ObjLightDir[5]"]; // Amedo 2026-09-30
    private static readonly string[] ColorNames = ["ObjLightColor[0]", "ObjLightColor[1]", "ObjLightColor[2]", "ObjLightColor[3]", "ObjLightColor[4]", "ObjLightColor[5]"];

    private static Owner Resolve(Entity e)
    {
        var o = new Owner();
        for (var cur = e; cur is not null; cur = cur.Parent)
        {
            if (o.Instance is null && cur.Has<InstanceData>()) { o.Instance = cur; o.InstanceParent = cur.Parent; } // Amedo 2026-09-30
            if (cur.Get<GameLightingEnvironment>() is { } env)
            {
                o.Environment = env;
                o.ChunkRoot = cur;
                break;
            }
        }
        return o;
    }

    // Amedo 2026-10-01
    private sealed class Uniforms(TwinShaderProgram sh)
    {
        public readonly TwinShaderProgram.Slot Lit = sh.Uniform("ObjLit");
        public readonly TwinShaderProgram.Slot Count = sh.Uniform("ObjLightCount");
        public readonly TwinShaderProgram.Slot Ambient = sh.Uniform("ObjAmbient");
        public readonly TwinShaderProgram.Slot[] Dir = Array.ConvertAll(DirNames, sh.Uniform);
        public readonly TwinShaderProgram.Slot[] Color = Array.ConvertAll(ColorNames, sh.Uniform);
    }

    private static Uniforms U(TwinShaderProgram sh) => sh.Cached(static s => new Uniforms(s)); // Amedo 2026-10-01

    public static bool Apply(TwinShaderProgram sh, Entity meshEntity, Material mat)
    {
        if (Current(meshEntity, mat) is not { } lit) return false; // Amedo 2026-10-01
        var u = U(sh); // Amedo 2026-10-01
        sh.Set(u.Lit, 1);
        float t = 1f; // Amedo 2026-09-30
        if (lit.Fading && RenderPipeline.Instance is { } rp)
        {
            t = Math.Clamp((rp.TimeSeconds - lit.FadeStart) / FadeSeconds, 0f, 1f);
            if (t >= 1f) lit.Fading = false;
        }
        if (!lit.Fading)
        {
            sh.Set(u.Count, 3);
            sh.Set(u.Ambient, lit.Ambient);
            for (int i = 0; i < 3; i++)
            {
                sh.Set(u.Dir[i], lit.Direction[i]);
                sh.Set(u.Color[i], lit.Color[i]);
            }
            return true;
        }
        sh.Set(u.Count, 6);
        sh.Set(u.Ambient, Vector3.Lerp(lit.FromAmbient, lit.Ambient, t));
        for (int i = 0; i < 3; i++)
        {
            sh.Set(u.Dir[i], lit.Direction[i]);
            sh.Set(u.Color[i], lit.Color[i] * t);
            sh.Set(u.Dir[i + 3], lit.FromDirection[i]);
            sh.Set(u.Color[i + 3], lit.FromColor[i] * (1f - t));
        }
        return true;
    }

    // Amedo 2026-10-01
    public static int Instance(Entity meshEntity, Material mat, Span<Vector4> into)
    {
        if (Current(meshEntity, mat) is not { } lit)
        {
            into[..7].Clear();
            return 0;
        }
        if (lit.Fading) return -1;
        into[0] = new Vector4(lit.Ambient, 1f);
        for (int i = 0; i < 3; i++)
        {
            into[1 + i] = new Vector4(lit.Direction[i], 0f);
            into[4 + i] = new Vector4(lit.Color[i], 0f);
        }
        return 1;
    }

    // Amedo 2026-10-01
    private static Lit? Current(Entity meshEntity, Material mat)
    {
        if (!GameLightingEnvironment.Enabled || !mat.GameLit) return null;
        var o = Owners.GetValue(meshEntity, Resolve);
        if (o.Instance is { } moved && !ReferenceEquals(moved.Parent, o.InstanceParent)) // Amedo 2026-09-30
        {
            Owners.Remove(meshEntity);
            o = Owners.GetValue(meshEntity, Resolve);
        }
        if (o.Instance is null || o.Environment is not { HasLights: true } env || o.ChunkRoot is null) return null;

        var lit = Cache.GetOrCreateValue(o.Instance); // Amedo 2026-09-29
        if (lit.Frame != RenderPipeline.Frame)
        {
            lit.Frame = RenderPipeline.Frame;
            bool instant = lit.Epoch != _epoch; // Amedo 2026-09-30
            lit.Epoch = _epoch;
            if (!ReferenceEquals(lit.Environment, env))
            {
                lit.Fading = lit.Environment is not null && lit.Ok && !instant && FadeSeconds > 0f && RenderPipeline.Instance is not null;
                if (lit.Fading)
                {
                    lit.FadeStart = RenderPipeline.Instance!.TimeSeconds;
                    lit.FromAmbient = lit.Ambient;
                    for (int i = 0; i < 3; i++)
                    {
                        lit.FromDirection[i] = lit.Direction[i];
                        lit.FromColor[i] = lit.Color[i];
                    }
                }
                lit.Environment = env;
            }
            var chunkWorld = o.ChunkRoot.Transform.World;
            lit.Ok = Matrix4x4.Invert(chunkWorld, out var toGame);
            if (lit.Ok)
            {
                var pos = Vector3.Transform(o.Instance.Transform.World.Translation, toGame);
                env.Compute(pos, Set);
                lit.Ambient = Set.Ambient;
                for (int i = 0; i < 3; i++)
                {
                    lit.Direction[i] = Vector3.TransformNormal(Set.Direction[i], chunkWorld);
                    lit.Color[i] = Set.Color[i];
                }
            }
        }
        return lit.Ok ? lit : null; // Amedo 2026-10-01
    }

    public static void Reset(TwinShaderProgram sh) => sh.SetLater(U(sh).Lit, 0); // Amedo 2026-10-01

    // Amedo 2026-09-30
    public static bool Shown(Entity instance, out Vector3 ambient, out Vector3 light0, out float fade, out string? chunk)
    {
        ambient = light0 = default;
        fade = 1f;
        chunk = null;
        if (!Cache.TryGetValue(instance, out var lit) || !lit.Ok) return false;
        if (lit.Fading && RenderPipeline.Instance is { } rp) fade = Math.Clamp((rp.TimeSeconds - lit.FadeStart) / FadeSeconds, 0f, 1f);
        bool blend = lit.Fading && fade < 1f;
        ambient = blend ? Vector3.Lerp(lit.FromAmbient, lit.Ambient, fade) : lit.Ambient;
        light0 = blend ? lit.Color[0] * fade + lit.FromColor[0] * (1f - fade) : lit.Color[0];
        chunk = lit.Environment?.Entity?.Name;
        return true;
    }
}
