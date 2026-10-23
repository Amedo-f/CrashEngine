using CrashEngine.Core;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

// Amedo: the level's point lights lit per pixel on scenery + objects, the nearest ones casting shadows (cube maps)
public static partial class SunShadowPass
{
    public const int MaxPointLights = 4;
    public const int PointMapSize = 512;
    public const int PointTextureUnit = 9;
    private const float PointNear = 0.05f;
    private const float PointReach = 25f;

    private struct PointSource
    {
        public Vector3 Position;
        public Vector3 Color;
        public float Radius;
        public int Exponent;
        public float Range;
    }

    private static PointSource[] _sources = new PointSource[64];
    private static int _sourceCount;
    private static readonly PointSource[] _slots = new PointSource[MaxPointLights];
    private static readonly bool[] _slotUsed = new bool[MaxPointLights];
    private static readonly bool[] _slotFresh = new bool[MaxPointLights];
    private static readonly Vector4[] _pointPosRange = new Vector4[MaxPointLights];
    private static readonly Vector4[] _pointColor = new Vector4[MaxPointLights];
    private static int _pointCount;
    private static uint _pointFbo, _pointDepth;
    private static bool _pointFailed, _pointMode;
    private static TwinShaderProgram? _pointProgram;

    public static bool PointWanted => Wanted && GraphicsSettings.Current.PointLights;
    public static int PointLightCount => _pointCount;

    public static void AddPointLight(Vector3 position, Vector3 color, float radius, int exponent)
    {
        if (!InDepthPass || !PointWanted || radius <= 0f || color == Vector3.Zero) return;
        if (_sourceCount == _sources.Length) Array.Resize(ref _sources, _sources.Length * 2);
        ref var s = ref _sources[_sourceCount++];
        s.Position = position;
        s.Color = color;
        s.Radius = radius;
        s.Exponent = Math.Max(exponent, 0);
        s.Range = RangeOf(radius * 0.5f * MathF.Max(color.X, MathF.Max(color.Y, color.Z)), s.Exponent);
    }

    // Amedo: the game's falloff f = 25 / (d^2 + 25), light = Radius * f^exp * dot(N, d * f); range = where it drops under 1/255
    private static float RangeOf(float peak, int exponent)
    {
        for (float d = 1f; d < 400f; d *= 1.1f)
        {
            float f = 25f / (d * d + 25f);
            if (peak * MathF.Pow(f, exponent) * d * f < 1f / 255f) return d;
        }
        return 400f;
    }

    private static void BeginPointLights() => _sourceCount = 0;

    // Amedo: called after scene.RenderDepth (lights + casters collected); picks the nearest lights and renders their cube maps
    private static void RenderPointLights(GL gl, Vector3 eyePos)
    {
        _pointCount = 0;
        if (!PointWanted || _sourceCount == 0 || !EnsurePointTarget(gl))
        {
            Array.Clear(_slotUsed);
            return;
        }

        Span<int> chosen = stackalloc int[MaxPointLights];
        Span<float> score = stackalloc float[MaxPointLights];
        int count = 0;
        for (int i = 0; i < _sourceCount; i++)
        {
            ref var s = ref _sources[i];
            float d = Vector3.Distance(s.Position, eyePos) - s.Range;
            if (d > PointReach) continue;
            int at = count < MaxPointLights ? count++ : MaxPointLights;
            if (at == MaxPointLights)
            {
                if (d >= score[MaxPointLights - 1]) continue;
                at = MaxPointLights - 1;
            }
            while (at > 0 && score[at - 1] > d)
            {
                score[at] = score[at - 1];
                chosen[at] = chosen[at - 1];
                at--;
            }
            score[at] = d;
            chosen[at] = i;
        }

        // keep each light in the slot it had (its cube map is still valid); new lights take free slots and render now
        Span<bool> keep = stackalloc bool[MaxPointLights];
        Span<int> slotOf = stackalloc int[MaxPointLights];
        for (int c = 0; c < count; c++)
        {
            slotOf[c] = -1;
            for (int k = 0; k < MaxPointLights; k++)
                if (_slotUsed[k] && !keep[k] && _slots[k].Position == _sources[chosen[c]].Position) { slotOf[c] = k; keep[k] = true; break; }
        }
        for (int k = 0; k < MaxPointLights; k++) _slotFresh[k] = false;
        for (int c = 0; c < count; c++)
        {
            if (slotOf[c] >= 0) continue;
            for (int k = 0; k < MaxPointLights; k++)
                if (!keep[k]) { slotOf[c] = k; keep[k] = true; _slotFresh[k] = true; break; }
        }
        for (int k = 0; k < MaxPointLights; k++) _slotUsed[k] = keep[k];
        for (int c = 0; c < count; c++) _slots[slotOf[c]] = _sources[chosen[c]];

        float amount = GraphicsSettings.Current.PointLightAmount;
        for (int k = 0; k < MaxPointLights; k++)
        {
            if (!_slotUsed[k])
            {
                _pointPosRange[k] = Vector4.Zero;
                _pointColor[k] = Vector4.Zero;
                continue;
            }
            ref var s = ref _slots[k];
            _pointPosRange[k] = new Vector4(s.Position, s.Range);
            _pointColor[k] = new Vector4(s.Color * (s.Radius * 0.5f * amount), s.Exponent);
        }
        _pointCount = MaxPointLights;

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _pointFbo);
        gl.Viewport(0, 0, PointMapSize, PointMapSize);
        _pointMode = true;
        try
        {
            int nearest = count > 0 ? slotOf[0] : -1;
            int turn = count > 1 ? slotOf[1 + _frame % (count - 1)] : -1;
            for (int k = 0; k < MaxPointLights; k++)
            {
                if (!_slotUsed[k]) continue;
                if (_slotFresh[k])
                {
                    _staticValid[k] = false;
                    _movers[k].Clear();
                }
                if (k == nearest || k == turn || _slotFresh[k]) RenderSlot(gl, k);
            }
        }
        finally
        {
            _pointMode = false;
        }
    }

    private static readonly (Vector3 Dir, Vector3 Up)[] Faces =
    [
        (Vector3.UnitX, -Vector3.UnitY), (-Vector3.UnitX, -Vector3.UnitY),
        (Vector3.UnitY, Vector3.UnitZ), (-Vector3.UnitY, -Vector3.UnitZ),
        (Vector3.UnitZ, -Vector3.UnitY), (-Vector3.UnitZ, -Vector3.UnitY),
    ];

    // Amedo: per light, what stands still (scenery, resting objects) is drawn once into a cached cube; every frame that
    // cache is copied and only characters and what is seen moving are drawn on top. A cached caster that moves (more
    // than a small sway) becomes a mover for that light and the cache is rebuilt once without it.
    private static readonly bool[] _staticValid = new bool[MaxPointLights];
    private static readonly Dictionary<ISunCaster, Matrix4x4>[] _staticSet = [new(), new(), new(), new()];
    private static readonly HashSet<ISunCaster>[] _movers = [new(), new(), new(), new()];
    private static int[] _inRange = new int[256], _drawList = new int[256];
    private static uint _pointStatic;
    private static readonly int[] _rebuilds = new int[MaxPointLights];
    private static readonly int[] _builtAt = new int[MaxPointLights];
    private const int RebuildEvery = 20; // Amedo: a moved object's old shadow lingers at most this many frames
    private const float ShadowReach = 0.5f; // Amedo: past half its reach a point light is too dim for a visible shadow

    private static void RenderSlot(GL gl, int slot)
    {
        ref var s = ref _slots[slot];
        var set = _staticSet[slot];
        var movers = _movers[slot];
        int inRange = 0, matched = 0;
        if (_inRange.Length < _casterCount)
        {
            _inRange = new int[_casterCount];
            _drawList = new int[_casterCount];
        }
        for (int k = 0; k < _casterCount; k++)
        {
            ref var c = ref _casters[k];
            if (c.PointRadius != float.MaxValue && Vector3.Distance(c.PointCenter, s.Position) - c.PointRadius > s.Range * ShadowReach) continue;
            _inRange[inRange++] = k;
            if (!c.Still || !_staticValid[slot] || movers.Contains(c.Who)) continue;
            if (!set.TryGetValue(c.Who, out var world)) continue;
            matched++;
            if (Near(world, c.World)) continue;
            movers.Add(c.Who);
            _staticValid[slot] = false;
        }
        if (_staticValid[slot] && matched < set.Count) _staticValid[slot] = false;

        bool rebuilt = !_staticValid[slot] && (_slotFresh[slot] || _frame - _builtAt[slot] >= RebuildEvery);
        if (rebuilt)
        {
            _builtAt[slot] = _frame;
            set.Clear();
            int still = 0;
            for (int i = 0; i < inRange; i++)
            {
                ref var c = ref _casters[_inRange[i]];
                if (!c.Still || movers.Contains(c.Who)) continue;
                _drawList[still++] = _inRange[i];
                set[c.Who] = c.World;
            }
            DrawCube(gl, slot, _pointStatic, _drawList, still, clear: true);
            _staticValid[slot] = true;
        }

        gl.CopyImageSubData(_pointStatic, CopyImageSubDataTarget.TextureCubeMapArray, 0, 0, 0, slot * 6,
                            _pointDepth, CopyImageSubDataTarget.TextureCubeMapArray, 0, 0, 0, slot * 6, PointMapSize, PointMapSize, 6);
        int moving = 0;
        for (int i = 0; i < inRange; i++)
        {
            ref var c = ref _casters[_inRange[i]];
            if (!c.Still || movers.Contains(c.Who) || !set.ContainsKey(c.Who)) _drawList[moving++] = _inRange[i];
        }
        if (moving > 0) DrawCube(gl, slot, _pointDepth, _drawList, moving, clear: false);
        if (DebugView != 0)
        {
            if (rebuilt) _rebuilds[slot]++;
            if (_frame % 120 == 0)
            {
                int chars = 0, unbounded = 0, notStill = 0;
                for (int i = 0; i < inRange; i++)
                {
                    ref var c = ref _casters[_inRange[i]];
                    if (c.Character) chars++;
                    if (c.Radius == float.MaxValue) unbounded++;
                    if (!c.Still) notStill++;
                }
                Console.WriteLine($"[pointstat] frame {_frame} slot {slot}: in range {inRange}, characters {chars}, unbounded {unbounded}, not still {notStill}, movers {movers.Count}, cached {set.Count}, drawn moving {moving}, rebuilds {_rebuilds[slot]}");
            }
        }
    }

    // Amedo: small sways (bobbing books, swaying grass) keep the cache; the shadow is off by under 5 cm
    private static bool Near(in Matrix4x4 a, in Matrix4x4 b) =>
        Vector3.DistanceSquared(a.Translation, b.Translation) < 0.05f * 0.05f
        && MathF.Abs(a.M11 - b.M11) + MathF.Abs(a.M12 - b.M12) + MathF.Abs(a.M13 - b.M13)
         + MathF.Abs(a.M21 - b.M21) + MathF.Abs(a.M22 - b.M22) + MathF.Abs(a.M23 - b.M23)
         + MathF.Abs(a.M31 - b.M31) + MathF.Abs(a.M32 - b.M32) + MathF.Abs(a.M33 - b.M33) < 0.05f;

    private static void DrawCube(GL gl, int slot, uint target, int[] list, int count, bool clear)
    {
        ref var s = ref _slots[slot];
        var program = _pointProgram!;
        program.Use();
        _current = program;
        program.Set("PointLightPos", s.Position);
        program.Set("PointLightFar", s.Range);
        program.Set("StartProjection", CameraComponent.CreatePerspectiveRH(MathF.PI * 0.5f, 1f, PointNear, s.Range));
        for (int face = 0; face < 6; face++)
        {
            var (dir, up) = Faces[face];
            program.Use();
            _current = program;
            program.Set("StartView", CameraComponent.CreateLookAtRH(s.Position, s.Position + dir, up));
            gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, target, 0, slot * 6 + face);
            GlState.DepthMask(gl, true);
            if (clear) gl.Clear(ClearBufferMask.DepthBufferBit);
            int draws = Draws;
            for (int i = 0; i < count; i++)
            {
                ref var c = ref _casters[list[i]];
                if (c.PointRadius != float.MaxValue)
                {
                    var d = c.PointCenter - s.Position;
                    float along = Vector3.Dot(d, dir);
                    if (along < -c.PointRadius) continue;
                    var side = d - dir * along;
                    if (MathF.Max(MathF.Abs(side.X), MathF.Max(MathF.Abs(side.Y), MathF.Abs(side.Z))) > along + c.PointRadius * 1.5f) continue;
                }
                c.Who.DrawSunDepth(gl, c.World);
            }
            AfterCascade?.Invoke(gl);
            if (DebugView != 0 && _frame % 120 < 2)
                Console.WriteLine($"[point] slot {slot} face {face} {(clear ? "still" : "moving")} at {s.Position} range {s.Range:0.0}: {Draws - draws} draws");
        }
    }

    private static void SetPointUniforms(GL gl, TwinShaderProgram sh)
    {
        gl.ActiveTexture(Silk.NET.OpenGL.TextureUnit.Texture0 + PointTextureUnit);
        gl.BindTexture(TextureTarget.TextureCubeMapArray, _pointDepth);
        gl.ActiveTexture(Silk.NET.OpenGL.TextureUnit.Texture0);
        sh.Set("PointCount", _pointCount);
        sh.SetVector4Array("PointPosRange", _pointPosRange);
        sh.SetVector4Array("PointColor", _pointColor);
    }

    private static unsafe bool EnsurePointTarget(GL gl)
    {
        if (_pointFbo != 0) return true;
        if (_pointFailed) return false;
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Shaders");
            _pointProgram = new TwinShaderProgram(gl, dir, "MainPass.vert", "PointDepth.frag");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[SunShadowPass] point depth shader failed, point lights off: " + ex.Message);
            _pointFailed = true;
            return false;
        }
        _pointDepth = gl.GenTexture();
        gl.BindTexture(TextureTarget.TextureCubeMapArray, _pointDepth);
        gl.TexImage3D(TextureTarget.TextureCubeMapArray, 0, InternalFormat.DepthComponent32f, PointMapSize, PointMapSize, MaxPointLights * 6, 0, PixelFormat.DepthComponent, PixelType.Float, null);
        gl.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
        gl.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
        gl.BindTexture(TextureTarget.TextureCubeMapArray, 0);
        _pointStatic = gl.GenTexture();
        gl.BindTexture(TextureTarget.TextureCubeMapArray, _pointStatic);
        gl.TexImage3D(TextureTarget.TextureCubeMapArray, 0, InternalFormat.DepthComponent32f, PointMapSize, PointMapSize, MaxPointLights * 6, 0, PixelFormat.DepthComponent, PixelType.Float, null);
        gl.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        gl.BindTexture(TextureTarget.TextureCubeMapArray, 0);

        gl.GetInteger(GetPName.DrawFramebufferBinding, out int previous);
        _pointFbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _pointFbo);
        gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, _pointDepth, 0, 0);
        gl.DrawBuffer(DrawBufferMode.None);
        gl.ReadBuffer(ReadBufferMode.None);
        bool ok = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)previous);
        if (!ok)
        {
            Console.WriteLine("[SunShadowPass] point shadow target incomplete, point lights off");
            gl.DeleteFramebuffer(_pointFbo);
            gl.DeleteTexture(_pointDepth);
            _pointFbo = _pointDepth = 0;
            _pointFailed = true;
        }
        return ok;
    }
}
