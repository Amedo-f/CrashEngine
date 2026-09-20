using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CrashEngine.Renderer;

public sealed class GpuMesh : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;
        public Vector4 Color;
        public Vector3 BoneIndices;
        public Vector3 BoneWeights;
    }

    private readonly GL            _gl;
    private readonly uint          _vao, _vbo, _ebo;
    private readonly PrimitiveType _prim;
    private readonly bool          _useElements;
    public  readonly int           IndexCount;
    public  readonly int           VertexCount;

    public readonly Vector3[]? RaycastPositions;
    public readonly uint[]?    RaycastIndices;

    public readonly Vertex[]? RaycastVertices;

    public unsafe GpuMesh(GL gl, Vertex[] verts, uint[] indices)
    {
        _gl          = gl;
        _prim        = PrimitiveType.Triangles;
        _useElements = true;
        IndexCount   = indices.Length;
        VertexCount  = verts.Length;

        RaycastPositions = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) RaycastPositions[i] = verts[i].Position;
        RaycastIndices  = (uint[])indices.Clone();
        RaycastVertices = (Vertex[])verts.Clone();

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        _ebo = gl.GenBuffer();

        gl.BindVertexArray(_vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (Vertex* p = verts)
            gl.BufferData(BufferTargetARB.ArrayBuffer,
                          (nuint)(verts.Length * sizeof(Vertex)), p,
                          BufferUsageARB.StaticDraw);

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* p = indices)
            gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                          (nuint)(indices.Length * sizeof(uint)), p,
                          BufferUsageARB.StaticDraw);

        SetAttribs(gl);
        gl.BindVertexArray(0);
    }

    public unsafe GpuMesh(GL gl, Vertex[] verts, PrimitiveType prim)
    {
        _gl          = gl;
        _prim        = prim;
        _useElements = false;
        IndexCount   = 0;
        VertexCount  = verts.Length;

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        _ebo = 0;

        gl.BindVertexArray(_vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (Vertex* p = verts)
            gl.BufferData(BufferTargetARB.ArrayBuffer,
                          (nuint)(verts.Length * sizeof(Vertex)), p,
                          BufferUsageARB.StaticDraw);

        SetAttribs(gl);
        gl.BindVertexArray(0);
    }

    private static unsafe void SetAttribs(GL gl)
    {
        uint stride = (uint)sizeof(Vertex);
        gl.EnableVertexAttribArray(0); gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1); gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)32);
        gl.EnableVertexAttribArray(2); gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)24);
        gl.EnableVertexAttribArray(3); gl.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
        gl.EnableVertexAttribArray(5); gl.VertexAttribPointer(5, 3, VertexAttribPointerType.Float, false, stride, (void*)48);
        gl.EnableVertexAttribArray(6); gl.VertexAttribPointer(6, 3, VertexAttribPointerType.Float, false, stride, (void*)60);
    }

    public unsafe void UpdateVertices(GL gl, ReadOnlySpan<Vertex> verts)
    {
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (Vertex* p = verts)
            gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(verts.Length * sizeof(Vertex)), p);
    }

    public unsafe void Draw(GL gl)
    {
        gl.BindVertexArray(_vao);
        if (_useElements)
            gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount,
                            DrawElementsType.UnsignedInt, (void*)0);
        else
            gl.DrawArrays(_prim, 0, (uint)VertexCount);
    }

    public bool RaycastTriangles(Vector3 orig, Vector3 dir, out float t)
    {
        t = float.MaxValue;
        bool hit = false;
        if (RaycastPositions is not { } pos || RaycastIndices is not { } idx) return false;

        const float eps = 1e-7f;
        for (int i = 0; i + 2 < idx.Length; i += 3)
        {
            var a = pos[idx[i]]; var b = pos[idx[i + 1]]; var c = pos[idx[i + 2]];
            var e1 = b - a; var e2 = c - a;
            var h  = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, h);
            if (MathF.Abs(det) < eps) continue;
            float invDet = 1f / det;
            var s = orig - a;
            float u = Vector3.Dot(s, h) * invDet;
            if (u < 0f || u > 1f) continue;
            var q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(dir, q) * invDet;
            if (v < 0f || u + v > 1f) continue;
            float tri = Vector3.Dot(e2, q) * invDet;
            if (tri > eps && tri < t) { t = tri; hit = true; }
        }
        return hit;
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        if (_ebo != 0) _gl.DeleteBuffer(_ebo);
    }
}
