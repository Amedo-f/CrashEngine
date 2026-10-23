using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

// Amedo 2026-10-01
public static class GlState
{
    private static int _cull = -1, _cullFace = -1, _depthMask = -1, _depthFunc = -1, _depthTest = -1, _offsetFill = -1, _offsetLine = -1;
    private static long _vertexArray = -1;

    public static void Invalidate()
    {
        _cull = _cullFace = _depthMask = _depthFunc = _depthTest = _offsetFill = _offsetLine = -1;
        _vertexArray = -1;
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
