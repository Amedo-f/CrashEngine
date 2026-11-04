using Silk.NET.OpenGL;

namespace CrashEngine.Core;

public static class GlCaps
{
    public static bool Gles { get; private set; }
    public static bool Dsa { get; private set; } = true;
    public static bool BufferStorage { get; private set; } = true;
    public static bool DepthClamp { get; private set; } = true;
    public static bool GpuTimer { get; private set; } = true;
    public static bool PolygonMode { get; private set; } = true;
    public static int VertexStorageBlocks { get; private set; } = 16;
    public static bool TiledMsaa { get; private set; }
    public static bool InvalidateFramebuffer { get; private set; }

    internal static unsafe void Detect(GL gl, bool gles)
    {
        Gles = gles;
        if (!gles) return;
        var ext = new HashSet<string>();
        gl.GetInteger(GetPName.NumExtensions, out int count);
        for (uint i = 0; i < count; i++)
            if (gl.GetStringS(StringName.Extensions, i) is { } name) ext.Add(name);
        Dsa = false;
        BufferStorage = ext.Contains("GL_EXT_buffer_storage");
        DepthClamp = ext.Contains("GL_EXT_depth_clamp");
        GlesExtensions.Load(gl, ext);
        GpuTimer = GlesExtensions.GetQueryObjectui64 != null;
        TiledMsaa = GlesExtensions.FramebufferTexture2DMultisample != null && GlesExtensions.RenderbufferStorageMultisample != null;
        InvalidateFramebuffer = true;
        PolygonMode = false;
        gl.GetInteger(GetPName.MaxVertexShaderStorageBlocks, out int blocks);
        VertexStorageBlocks = blocks;
        Console.WriteLine($"[GlCaps] GLES: buffer storage {BufferStorage}, depth clamp {DepthClamp}, GPU timer {GpuTimer}, tiled MSAA {TiledMsaa}, vertex storage blocks {VertexStorageBlocks}, {count} extensions");
    }
}
