using CrashEngine.Core;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

// Amedo 2026-09-26
public sealed class GameShadowPass : Component
{
    public const int ShapeCount = 8;
    public const int ReceiverBit = 0x80;
    public const int CountBits = 0x7F;
    public const float Darkness = 32f / 255f;

    public static bool ShowShadows { get; set; } = true;
    public static GameShadowPass? Instance { get; private set; }
    public static bool MarkingReceivers { get; internal set; }
    public static bool ReceiversMarked { get; internal set; } // Amedo

    public override bool IsTranslucent => true;
    public override int  RenderOrder   => int.MinValue; // Amedo 2026-09-26

    public bool HasShapes { get; private set; }
    public int LastVolumeCount { get; private set; }

    private readonly List<(int Type, Matrix4x4 Model)> _volumes = new();
    private readonly uint[] _shapeVao = new uint[ShapeCount];
    private readonly uint[] _shapeVbo = new uint[ShapeCount];
    private readonly int[] _shapeVerts = new int[ShapeCount];

    private GL _gl = null!;
    private ShaderProgram? _volumeShader, _maskShader, _compositeShader;
    private uint _screenVao, _fbo, _depthStencil, _mask;
    private int _fboW, _fboH;
    private bool _halfResFailed;
    private bool _blitVerified; // Amedo 2026-10-01
    private int _stencilBits;

    public static bool MarksReceivers(Entity? pipelineEntity) =>
        ShowShadows && !SunShadowPass.Wanted && Instance is { } p && p._stencilBits > 0 && p.Entity?.Scene is { } s && s == pipelineEntity?.Scene; // Amedo

    public override void OnLoad()
    {
        Instance = this;
        _gl = Engine.Instance.GL;
        var dir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string Read(string f) => File.ReadAllText(Path.Combine(dir, f));
        _volumeShader = new ShaderProgram(_gl, Read("ShadowVolume.vert"), Read("ShadowVolume.frag"));
        _maskShader = new ShaderProgram(_gl, Read("ShadowScreen.vert"), Read("ShadowMask.frag"));
        _compositeShader = new ShaderProgram(_gl, Read("ShadowScreen.vert"), Read("ShadowComposite.frag"));
        _screenVao = _gl.GenVertexArray();

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.GetFramebufferAttachmentParameter(FramebufferTarget.DrawFramebuffer, (FramebufferAttachment)GLEnum.Stencil,
            FramebufferAttachmentParameterName.StencilSize, out _stencilBits);
        ClearErrors(_gl);
        Console.WriteLine($"[GameShadowPass] default framebuffer stencil bits: {_stencilBits}");
    }

    public unsafe void SetShape(int type, ReadOnlySpan<Vector3> triangles)
    {
        if ((uint)type >= ShapeCount || triangles.Length < 3) return;
        if (_shapeVao[type] == 0)
        {
            _shapeVao[type] = _gl.GenVertexArray();
            _shapeVbo[type] = _gl.GenBuffer();
        }
        GlState.VertexArray(_gl, _shapeVao[type]); // Amedo 2026-10-01
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _shapeVbo[type]);
        fixed (Vector3* p = triangles)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(triangles.Length * sizeof(Vector3)), p, BufferUsageARB.StaticDraw);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vector3), (void*)0);
        GlState.VertexArray(_gl, 0); // Amedo 2026-10-01
        _shapeVerts[type] = triangles.Length;
        HasShapes = true;
    }

    public void Submit(int type, in Matrix4x4 model)
    {
        if ((uint)type < ShapeCount && _shapeVerts[type] > 0) _volumes.Add((type, model));
    }

    public override void OnRender(GL gl)
    {
        try
        {
            LastVolumeCount = _volumes.Count;
            if (ShowShadows && MarkingReceivers && _volumes.Count > 0 && _stencilBits > 0 && RenderPipeline.Instance?.Camera is { } cam)
                Draw(gl, cam.View * cam.Projection);
        }
        finally
        {
            _volumes.Clear();
            MarkingReceivers = false;
            gl.Disable(EnableCap.StencilTest);
            gl.StencilMask(0xFF);
        }
    }

    private void Draw(GL gl, Matrix4x4 viewProjection)
    {
        int w = Engine.Instance.SceneWidth, h = Engine.Instance.SceneHeight; // Amedo
        uint scene = Engine.Instance.SceneFramebuffer;
        if (w < 2 || h < 2) return;

        gl.Disable(EnableCap.ScissorTest);
        bool half = !_halfResFailed && EnsureTargets(gl, w / 2, h / 2);
        if (half)
        {
            if (!_blitVerified) ClearErrors(gl); // Amedo 2026-10-01
            uint source = scene; // Amedo
            if (Engine.Instance.SceneSamples > 1 && EnsureResolve(gl, w, h))
            {
                gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, scene);
                gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _resolveFbo);
                gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h, ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest);
                source = _resolveFbo;
            }
            gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, source);
            gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _fbo);
            gl.BlitFramebuffer(0, 0, w, h, 0, 0, _fboW, _fboH,
                ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest);
            if (!_blitVerified && gl.GetError() != GLEnum.NoError) // Amedo 2026-10-01
            {
                _halfResFailed = true;
                half = false;
                Console.WriteLine("[GameShadowPass] depth/stencil blit unsupported, using full-resolution shadows");
            }
            _blitVerified = half;
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, half ? _fbo : scene); // Amedo
        }
        if (half)
        {
            gl.Viewport(0, 0, (uint)_fboW, (uint)_fboH);
            gl.ColorMask(true, true, true, true);
            gl.ClearColor(0f, 0f, 0f, 0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);
        }

        gl.Enable(EnableCap.StencilTest);
        gl.StencilMask(CountBits);
        gl.ClearStencil(0);
        gl.Clear(ClearBufferMask.StencilBufferBit);

        gl.ColorMask(false, false, false, false);
        GlState.DepthMask(gl, false); // Amedo 2026-10-01
        GlState.DepthTest(gl, true);
        GlState.DepthFunc(gl, DepthFunction.Lequal);
        GlState.Culling(gl, false);
        GlBlend.Set(gl, false); // Amedo 2026-09-30
        gl.StencilFunc(StencilFunction.Equal, 0, ReceiverBit);
        _volumeShader!.Use();
        foreach (var (type, model) in _volumes)
        {
            bool mirrored = model.GetDeterminant() < 0f;
            gl.StencilOpSeparate(TriangleFace.Front, StencilOp.Keep, StencilOp.Keep, mirrored ? StencilOp.DecrWrap : StencilOp.IncrWrap);
            gl.StencilOpSeparate(TriangleFace.Back, StencilOp.Keep, StencilOp.Keep, mirrored ? StencilOp.IncrWrap : StencilOp.DecrWrap);
            _volumeShader.Set("MVP", model * viewProjection);
            GlState.VertexArray(gl, _shapeVao[type]); // Amedo 2026-10-01
            gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_shapeVerts[type]);
            GpuStats.Draws++; // Amedo 2026-09-30
            GpuStats.Triangles += _shapeVerts[type] / 3;
        }
        GpuStats.Draws += half ? 2 : 1; // Amedo 2026-09-30

        GlState.DepthTest(gl, false); // Amedo 2026-10-01
        gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
        gl.StencilFunc(StencilFunction.Notequal, 0, CountBits);
        GlState.VertexArray(gl, _screenVao); // Amedo 2026-10-01
        if (half)
        {
            gl.ColorMask(true, false, false, false);
            _maskShader!.Use();
            gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, scene); // Amedo
            gl.Viewport(0, 0, (uint)w, (uint)h);
            gl.Disable(EnableCap.StencilTest);
        }

        gl.ColorMask(true, true, true, false);
        GlBlend.Set(gl, true); // Amedo 2026-09-30
        gl.BlendEquation(BlendEquationModeEXT.FuncReverseSubtract);
        gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        _compositeShader!.Use();
        _compositeShader.Set("UseMask", half ? 1 : 0);
        _compositeShader.Set("Darkness", Darkness);
        _compositeShader.Set("Mask", 0);
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, half ? _mask : 0);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        gl.BindTexture(TextureTarget.Texture2D, 0);

        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        GlBlend.Set(gl, false); // Amedo 2026-09-30
        gl.ColorMask(true, true, true, true);
        GlState.DepthMask(gl, true); // Amedo 2026-10-01
        GlState.DepthTest(gl, true);
        GlState.DepthFunc(gl, DepthFunction.Lequal);
        GlState.VertexArray(gl, 0); // Amedo 2026-10-01
        RenderPipeline.Instance?.Shader.Use();
    }

    private unsafe bool EnsureTargets(GL gl, int w, int h)
    {
        if (_fbo != 0 && _fboW == w && _fboH == h) return true;
        if (_fbo == 0)
        {
            _fbo = gl.GenFramebuffer();
            _depthStencil = gl.GenRenderbuffer();
            _mask = gl.GenTexture();
        }

        gl.BindTexture(TextureTarget.Texture2D, _mask);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, (uint)w, (uint)h, 0, PixelFormat.Red, PixelType.UnsignedByte, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        gl.BindTexture(TextureTarget.Texture2D, 0);

        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthStencil);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)w, (uint)h);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _mask, 0);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
            RenderbufferTarget.Renderbuffer, _depthStencil);
        bool ok = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Engine.Instance.SceneFramebuffer); // Amedo

        _fboW = w;
        _fboH = h;
        if (!ok)
        {
            _halfResFailed = true;
            Console.WriteLine("[GameShadowPass] half-resolution target incomplete, using full-resolution shadows");
        }
        return ok;
    }

    private uint _resolveFbo, _resolveDepth; // Amedo
    private int _resolveW, _resolveH;

    // Amedo
    private bool EnsureResolve(GL gl, int w, int h)
    {
        if (_resolveFbo != 0 && _resolveW == w && _resolveH == h) return true;
        if (_resolveFbo == 0)
        {
            _resolveFbo = gl.GenFramebuffer();
            _resolveDepth = gl.GenRenderbuffer();
        }
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _resolveDepth);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)w, (uint)h);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _resolveFbo);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _resolveDepth);
        bool ok = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Engine.Instance.SceneFramebuffer);
        _resolveW = w;
        _resolveH = h;
        if (!ok) Console.WriteLine("[GameShadowPass] MSAA depth copy target incomplete");
        return ok;
    }

    private static void ClearErrors(GL gl)
    {
        for (int i = 0; i < 16 && gl.GetError() != GLEnum.NoError; i++) { }
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GlState.ForgetVertexArray(); // Amedo 2026-10-01
        for (int i = 0; i < ShapeCount; i++)
        {
            if (_shapeVao[i] != 0) _gl.DeleteVertexArray(_shapeVao[i]);
            if (_shapeVbo[i] != 0) _gl.DeleteBuffer(_shapeVbo[i]);
        }
        if (_fbo != 0)
        {
            _gl.DeleteFramebuffer(_fbo);
            _gl.DeleteRenderbuffer(_depthStencil);
            _gl.DeleteTexture(_mask);
        }
        if (_resolveFbo != 0) // Amedo
        {
            _gl.DeleteFramebuffer(_resolveFbo);
            _gl.DeleteRenderbuffer(_resolveDepth);
        }
        if (_screenVao != 0) _gl.DeleteVertexArray(_screenVao);
        _volumeShader?.Dispose();
        _maskShader?.Dispose();
        _compositeShader?.Dispose();
    }
}
