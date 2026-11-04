using Silk.NET.OpenGL;

namespace CrashEngine.Core;

public static unsafe class GlesExtensions
{
    public static delegate* unmanaged<uint, uint, ulong*, void> GetQueryObjectui64;
    public static delegate* unmanaged<uint, uint, uint, uint, int, int, void> FramebufferTexture2DMultisample;
    public static delegate* unmanaged<uint, int, uint, int, int, void> RenderbufferStorageMultisample;

    internal static void Load(GL gl, HashSet<string> extensions)
    {
        if (extensions.Contains("GL_EXT_disjoint_timer_query"))
            GetQueryObjectui64 = (delegate* unmanaged<uint, uint, ulong*, void>)gl.Context.GetProcAddress("glGetQueryObjectui64vEXT");
        if (extensions.Contains("GL_EXT_multisampled_render_to_texture"))
        {
            FramebufferTexture2DMultisample = (delegate* unmanaged<uint, uint, uint, uint, int, int, void>)gl.Context.GetProcAddress("glFramebufferTexture2DMultisampleEXT");
            RenderbufferStorageMultisample = (delegate* unmanaged<uint, int, uint, int, int, void>)gl.Context.GetProcAddress("glRenderbufferStorageMultisampleEXT");
        }
    }
}
