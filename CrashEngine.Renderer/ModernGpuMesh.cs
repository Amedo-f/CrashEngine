using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CrashEngine.Renderer;

// Amedo 2026-11-18
public sealed class ModernGpuMesh : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;
        public Vector4 Color;
        public uint Joints01, Joints23, Joints45, Joints67;
        public Vector4 Weights0;
        public Vector4 Weights1;
    }

    private readonly GL _gl;
    private readonly uint _vao, _vbo, _ebo;
    public readonly int IndexCount;
    public readonly Vector3 Min, Max;

    public unsafe ModernGpuMesh(GL gl, ReadOnlySpan<Vertex> vertices, ReadOnlySpan<uint> indices)
    {
        _gl = gl;
        IndexCount = indices.Length;
        Min = new Vector3(float.MaxValue);
        Max = new Vector3(float.MinValue);
        foreach (ref readonly var v in vertices)
        {
            Min = Vector3.Min(Min, v.Position);
            Max = Vector3.Max(Max, v.Position);
        }
        _vao = GlNames.VertexArray(gl);
        _vbo = GlNames.Buffer(gl);
        _ebo = GlNames.Buffer(gl);
        GlState.VertexArray(gl, _vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (Vertex* p = vertices)
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(Vertex)), p, BufferUsageARB.StaticDraw);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* p = indices)
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);

        uint stride = (uint)sizeof(Vertex);
        gl.EnableVertexAttribArray(0); gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(3); gl.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
        gl.EnableVertexAttribArray(11); gl.VertexAttribPointer(11, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
        gl.EnableVertexAttribArray(2); gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)24);
        gl.EnableVertexAttribArray(1); gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)32);
        gl.EnableVertexAttribArray(12); gl.VertexAttribIPointer(12, 4, VertexAttribIType.UnsignedInt, stride, (void*)48);
        gl.EnableVertexAttribArray(13); gl.VertexAttribPointer(13, 4, VertexAttribPointerType.Float, false, stride, (void*)64);
        gl.EnableVertexAttribArray(14); gl.VertexAttribPointer(14, 4, VertexAttribPointerType.Float, false, stride, (void*)80);
        GpuStats.Meshes++;
    }

    public unsafe void Draw(GL gl)
    {
        GlState.VertexArray(gl, _vao);
        TwinShaderProgram.SendLater();
        GlState.SendLater(gl);
        gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        GpuStats.Draws++;
    }

    public void Dispose()
    {
        GpuStats.Meshes--;
        GlState.ForgetVertexArray();
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
