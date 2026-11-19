using System.Numerics;
using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

// Amedo 2026-10-01
public sealed class ShaderStorage : IDisposable
{
    private readonly GL _gl;
    private uint _id;
    private int _capacity;

    public ShaderStorage(GL gl) => _gl = gl;

    public unsafe void Upload(ReadOnlySpan<Vector4> data, uint binding)
    {
        if (_id == 0) _id = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _id);
        int bytes = data.Length * sizeof(Vector4);
        if (bytes > _capacity) _capacity = Math.Max(bytes, Math.Max(_capacity * 2, 64 * 1024));
        _gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)_capacity, null, BufferUsageARB.StreamDraw);
        fixed (Vector4* p = data) _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, (nuint)bytes, p);
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, binding, _id);
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
    }

    // Amedo 2026-11-18
    public void Upload(ReadOnlySpan<Matrix4x4> data, uint binding) => Upload(System.Runtime.InteropServices.MemoryMarshal.Cast<Matrix4x4, Vector4>(data), binding);

    // Amedo 2026-11-18
    public void Bind(uint binding)
    {
        if (_id != 0) _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, binding, _id);
    }

    public void Dispose()
    {
        if (_id != 0) _gl.DeleteBuffer(_id);
        _id = 0;
    }
}
