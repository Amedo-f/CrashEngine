using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

// Amedo 2026-09-30
public static class GlBlend
{
    private static int _state = -1;

    public static bool IsOn(GL gl)
    {
        if (_state < 0) _state = gl.IsEnabled(EnableCap.Blend) ? 1 : 0;
        return _state == 1;
    }

    public static void Set(GL gl, bool on)
    {
        if (_state == (on ? 1 : 0)) return;
        Force(gl, on);
    }

    public static void Force(GL gl, bool on)
    {
        if (on) gl.Enable(EnableCap.Blend);
        else gl.Disable(EnableCap.Blend);
        _state = on ? 1 : 0;
    }

    public static void Invalidate() => _state = -1;

    public static void Assume(bool on) => _state = on ? 1 : 0; // Amedo
}
