using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

// Amedo 2026-10-01
public static class GlState
{
    private static int _cull = -1, _cullFace = -1, _depthMask = -1, _depthFunc = -1, _depthTest = -1, _offsetFill = -1, _offsetLine = -1;
    private static long _vertexArray = -1;

    private static GL? _gl;
    private static readonly uint[] _texture = NoTextures();
    private static int _activeUnit = -1;
    private static int _stencilFunc = -1, _stencilRef;
    private static uint _stencilMask;
    private static bool _stencilLater;
    private static int _laterFunc, _laterRef;
    private static uint _laterMask;

    private static uint[] NoTextures()
    {
        var t = new uint[32];
        Array.Fill(t, uint.MaxValue);
        return t;
    }

    static GlState() => CrashEngine.Core.Engine.GpuContextStarted += ForgetContext;

    private static void ForgetContext()
    {
        _gl = null;
        _stencilLater = false;
        Invalidate();
    }

    public static void Invalidate()
    {
        if (_gl is { } gl) SendLater(gl);
        _cull = _cullFace = _depthMask = _depthFunc = _depthTest = _offsetFill = _offsetLine = -1;
        _vertexArray = -1;
        _stencilFunc = -1;
        ForgetTextures();
    }

    public static void ForgetTextures()
    {
        _activeUnit = -1;
        Array.Fill(_texture, uint.MaxValue);
    }

    public static void ActiveTexture(GL gl, uint unit)
    {
        if (_activeUnit == unit) return;
        gl.ActiveTexture(TextureUnit.Texture0 + (int)unit);
        _activeUnit = (int)unit;
    }

    public static void Texture(GL gl, uint unit, uint id)
    {
        ActiveTexture(gl, unit);
        if (unit < _texture.Length && _texture[unit] == id) return;
        gl.BindTexture(TextureTarget.Texture2D, id);
        if (unit < _texture.Length) _texture[unit] = id;
    }

    public static void StencilFunc(GL gl, StencilFunction func, int reference, uint mask)
    {
        _gl = gl;
        _stencilLater = false;
        if (_stencilFunc == (int)func && _stencilRef == reference && _stencilMask == mask) return;
        gl.StencilFunc(func, reference, mask);
        _stencilFunc = (int)func;
        _stencilRef = reference;
        _stencilMask = mask;
    }

    public static void StencilFuncLater(GL gl, StencilFunction func, int reference, uint mask)
    {
        _gl = gl;
        if (_stencilFunc == (int)func && _stencilRef == reference && _stencilMask == mask)
        {
            _stencilLater = false;
            return;
        }
        _stencilLater = true;
        _laterFunc = (int)func;
        _laterRef = reference;
        _laterMask = mask;
    }

    public static void SendLater(GL gl)
    {
        if (!_stencilLater) return;
        _stencilLater = false;
        gl.StencilFunc((StencilFunction)_laterFunc, _laterRef, _laterMask);
        _stencilFunc = _laterFunc;
        _stencilRef = _laterRef;
        _stencilMask = _laterMask;
    }

    public static void VertexArray(GL gl, uint vao)
    {
        if (_vertexArray == vao) return;
        gl.BindVertexArray(vao);
        _vertexArray = vao;
    }

    public static void ForgetVertexArray() => _vertexArray = -1;

    private static void Cap(GL gl, EnableCap cap, ref int state, bool on)
    {
        int v = on ? 1 : 0;
        if (state == v) return;
        if (on) gl.Enable(cap);
        else gl.Disable(cap);
        state = v;
    }

    public static void Culling(GL gl, bool on) => Cap(gl, EnableCap.CullFace, ref _cull, on);

    public static void CullFace(GL gl, TriangleFace face)
    {
        if (_cullFace == (int)face) return;
        gl.CullFace(face);
        _cullFace = (int)face;
    }

    public static void DepthTest(GL gl, bool on) => Cap(gl, EnableCap.DepthTest, ref _depthTest, on);

    public static void DepthMask(GL gl, bool on)
    {
        int v = on ? 1 : 0;
        if (_depthMask == v) return;
        gl.DepthMask(on);
        _depthMask = v;
    }

    public static void DepthFunc(GL gl, DepthFunction func)
    {
        if (_depthFunc == (int)func) return;
        gl.DepthFunc(func);
        _depthFunc = (int)func;
    }

    public static void PolygonOffset(GL gl, bool on)
    {
        Cap(gl, EnableCap.PolygonOffsetFill, ref _offsetFill, on);
        Cap(gl, EnableCap.PolygonOffsetLine, ref _offsetLine, on);
    }
}
