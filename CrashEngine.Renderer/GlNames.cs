using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

// Amedo 2026-09-30
internal static class GlNames
{
    private const int Batch = 4096; // Amedo
    private static readonly uint[] Textures = new uint[Batch], Buffers = new uint[Batch], VertexArrays = new uint[Batch];
    private static int _textures, _buffers, _vertexArrays;

    public static uint Texture(GL gl)
    {
        if (_textures == 0)
        {
            gl.GenTextures(Batch, Textures);
            _textures = Batch;
        }
        return Textures[--_textures];
    }

    public static uint Buffer(GL gl)
    {
        if (_buffers == 0)
        {
            gl.GenBuffers(Batch, Buffers);
            _buffers = Batch;
        }
        return Buffers[--_buffers];
    }

    public static uint VertexArray(GL gl)
    {
        if (_vertexArrays == 0)
        {
            gl.GenVertexArrays(Batch, VertexArrays);
            _vertexArrays = Batch;
        }
        return VertexArrays[--_vertexArrays];
    }
}
