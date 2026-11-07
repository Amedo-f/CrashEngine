using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;
using CrashEngine.Core;

namespace CrashEngine.Renderer;

public sealed partial class GpuMesh : IDisposable, IGpuResource // Amedo 2026-09-29
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
        public Vector3 SmoothNormal; // Amedo
    }

    private readonly GL            _gl;
    private uint                   _vao, _vbo, _ebo; // Amedo 2026-09-29
    private MeshPool.Page?         _page; // Amedo 2026-10-01
    private int                    _baseVertex, _firstIndex;
    private object?                _owner;
    private Vertex[]?              _pendingVerts;
    private uint[]?                _pendingIndices;
    public bool Uploaded { get; private set; }
    private readonly PrimitiveType _prim;
    private readonly bool          _useElements;
    public  readonly int           IndexCount;
    public  readonly int           VertexCount;

    public Vector3[]? RaycastPositions { get; private set; }
    public readonly uint[]?    RaycastIndices;

    public Vertex[]? RaycastVertices { get; private set; }

    public static bool KeepVertices { get; set; } = true;

    private Vector3? _mirroredDirections;

    public Vector3 MirroredDirectionSum() => _mirroredDirections ?? (RaycastVertices is { } verts ? MirroredDirectionSum(verts) : Vector3.Zero);

    private static Vector3 MirroredDirectionSum(Vertex[] verts)
    {
        var sum = Vector3.Zero;
        foreach (ref readonly var v in verts.AsSpan())
        {
            var p = new Vector3(-v.Position.X, v.Position.Y, v.Position.Z);
            if (GameMath.LengthSquared(p) > 1e-12f) sum += GameMath.Normalize(p);
        }
        return sum;
    }

    // Amedo 2026-09-29
    public bool Dynamic { get; private set; }
    private int _bounds;
    private Vector3 _min, _max;

    public bool LocalBounds(out Vector3 min, out Vector3 max)
    {
        if (_bounds == 0)
        {
            _bounds = 2;
            if (RaycastVertices is { Length: > 0 } verts)
            {
                bool weighted = false;
                var lo = verts[0].Position;
                var hi = lo;
                foreach (ref readonly var v in verts.AsSpan())
                {
                    lo = Vector3.Min(lo, v.Position);
                    hi = Vector3.Max(hi, v.Position);
                    if (v.BoneWeights != Vector3.Zero) weighted = true;
                }
                if (!weighted)
                {
                    _min = lo;
                    _max = hi;
                    _bounds = 1;
                }
            }
        }
        min = _min;
        max = _max;
        return _bounds == 1 && !Dynamic;
    }

    // Amedo
    private bool _bindBounds;
    private Vector3 _bindMin, _bindMax;

    public bool BindBounds(out Vector3 min, out Vector3 max)
    {
        if (!_bindBounds && RaycastVertices is { Length: > 0 } verts)
        {
            _bindBounds = true;
            _bindMin = _bindMax = verts[0].Position;
            foreach (ref readonly var v in verts.AsSpan())
            {
                _bindMin = Vector3.Min(_bindMin, v.Position);
                _bindMax = Vector3.Max(_bindMax, v.Position);
            }
        }
        min = _bindMin;
        max = _bindMax;
        return _bindBounds;
    }

    public GpuMesh(GL gl, Vertex[] verts, uint[] indices) : this(gl, verts, indices, false) { } // Amedo 2026-09-29

    public static GpuMesh FromOwned(GL gl, Vertex[] verts, uint[] indices) => new(gl, verts, indices, true); // Amedo 2026-09-29

    private unsafe GpuMesh(GL gl, Vertex[] verts, uint[] indices, bool owned) // Amedo 2026-09-29
    {
        _gl          = gl;
        _prim        = PrimitiveType.Triangles;
        _useElements = true;
        IndexCount   = indices.Length;
        VertexCount  = verts.Length;

        RaycastPositions = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) RaycastPositions[i] = verts[i].Position;
        RaycastIndices  = owned ? indices : (uint[])indices.Clone(); // Amedo 2026-09-29
        RaycastVertices = owned ? verts : (Vertex[])verts.Clone();
        if (!HasSmoothNormals(RaycastVertices)) SmoothNormals([RaycastVertices], [RaycastIndices]); // Amedo

        if (GpuUploadQueue.Current is { } queue) // Amedo 2026-09-29
        {
            _pendingVerts   = RaycastVertices; // Amedo 2026-09-29
            _pendingIndices = RaycastIndices;
            queue.Add(this);
            return;
        }
        Create(RaycastVertices, RaycastIndices); // Amedo
    }

    public unsafe GpuMesh(GL gl, Vertex[] verts, PrimitiveType prim)
    {
        _gl          = gl;
        _prim        = prim;
        _useElements = false;
        IndexCount   = 0;
        VertexCount  = verts.Length;

        if (GpuUploadQueue.Current is { } queue) // Amedo 2026-09-29
        {
            _pendingVerts = (Vertex[])verts.Clone();
            queue.Add(this);
            return;
        }
        Create(verts, null);
    }

    // Amedo 2026-09-29
    private unsafe void Create(Vertex[] verts, uint[]? indices)
    {
        GpuUploadQueue.RequireMainThread("GpuMesh");
        _owner = GpuOwner.Track(this);
        var gl = _gl;
        _bytes = (long)verts.Length * sizeof(Vertex) + (indices?.Length ?? 0) * sizeof(uint); // Amedo 2026-10-01
        if (indices is not null && !Dynamic && MeshPool.Allocate(gl, verts, indices, out _baseVertex, out _firstIndex) is { } page)
        {
            _page = page;
            _vao = page.Vao;
            Uploaded = true;
            GpuStats.MeshBytes += _bytes;
            GpuStats.Meshes++;
            ForgetVertices();
            return;
        }
        Own(verts, indices);
        ForgetVertices();
    }

    private void ForgetVertices()
    {
        if (KeepVertices || Dynamic || RaycastVertices is null) return;
        LocalBounds(out _, out _);
        BindBounds(out _, out _);
        _mirroredDirections = MirroredDirectionSum(RaycastVertices);
        RaycastVertices = null;
        RaycastPositions = null;
    }

    // Amedo 2026-10-01
    private unsafe void Own(ReadOnlySpan<Vertex> verts, uint[]? indices)
    {
        var gl = _gl;
        _vao = GlNames.VertexArray(gl); // Amedo 2026-09-30
        _vbo = GlNames.Buffer(gl);
        _ebo = indices is null ? 0 : GlNames.Buffer(gl);

        GlState.VertexArray(gl, _vao); // Amedo 2026-10-01

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (Vertex* p = verts)
            gl.BufferData(BufferTargetARB.ArrayBuffer,
                          (nuint)(verts.Length * sizeof(Vertex)), p,
                          BufferUsageARB.StaticDraw);

        if (indices is not null)
        {
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (uint* p = indices)
                gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                              (nuint)(indices.Length * sizeof(uint)), p,
                              BufferUsageARB.StaticDraw);
        }

        SetAttribs(gl);
        GlState.VertexArray(gl, 0); // Amedo 2026-10-01
        if (Uploaded) return;
        Uploaded = true;
        GpuStats.MeshBytes += _bytes;
        GpuStats.Meshes++;
    }

    // Amedo 2026-10-01
    private void Unpool(ReadOnlySpan<Vertex> now)
    {
        if (_page is not { } page) return;
        _page = null;
        MeshPool.Free(_gl, page, _baseVertex, VertexCount, _firstIndex, IndexCount);
        Own(RaycastVertices ?? now, RaycastIndices);
    }

    private long _bytes; // Amedo 2026-09-30

    public void Warm() => LocalBounds(out _, out _); // Amedo 2026-09-29

    public long PendingBytes => _pendingVerts is { } pv ? (long)pv.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<Vertex>() + (_pendingIndices?.Length ?? 0) * sizeof(uint) : 0; // Amedo

    public string Describe() => $"mesh {VertexCount} vertices {IndexCount} indices {_bytes / 1024} KB{(_page is null ? " own" : "")}"; // Amedo

    // Amedo 2026-09-29
    public void Upload()
    {
        if (Uploaded || _pendingVerts is not { } verts) return;
        Create(verts, _pendingIndices);
        _pendingVerts = null;
        _pendingIndices = null;
    }

    internal static unsafe void SetAttribs(GL gl) // Amedo 2026-10-01
    {
        uint stride = (uint)sizeof(Vertex);
        gl.EnableVertexAttribArray(0); gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1); gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)32);
        gl.EnableVertexAttribArray(2); gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)24);
        gl.EnableVertexAttribArray(3); gl.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
        gl.EnableVertexAttribArray(5); gl.VertexAttribPointer(5, 3, VertexAttribPointerType.Float, false, stride, (void*)48);
        gl.EnableVertexAttribArray(6); gl.VertexAttribPointer(6, 3, VertexAttribPointerType.Float, false, stride, (void*)60);
        gl.EnableVertexAttribArray(11); gl.VertexAttribPointer(11, 3, VertexAttribPointerType.Float, false, stride, (void*)72); // Amedo
    }

    public unsafe void UpdateVertices(GL gl, ReadOnlySpan<Vertex> verts)
    {
        Dynamic = true; // Amedo 2026-09-29
        if (!Uploaded) Upload();
        Unpool(verts); // Amedo 2026-10-01
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (Vertex* p = verts)
            gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(verts.Length * sizeof(Vertex)), p);
    }

    public unsafe void Draw(GL gl)
    {
        if (!Uploaded) Upload(); // Amedo 2026-09-29
        GlState.VertexArray(gl, _vao); // Amedo 2026-10-01
        TwinShaderProgram.SendLater();
        GlState.SendLater(gl);
        if (_page is not null)
            gl.DrawElementsBaseVertex(PrimitiveType.Triangles, (uint)IndexCount, DrawElementsType.UnsignedInt, (void*)((long)_firstIndex * sizeof(uint)), _baseVertex);
        else if (_useElements)
            gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount,
                            DrawElementsType.UnsignedInt, (void*)0);
        else
            gl.DrawArrays(_prim, 0, (uint)VertexCount);
        GpuStats.Draws++; // Amedo 2026-09-30
        GpuStats.Triangles += (_useElements ? IndexCount : VertexCount) / 3;
    }

    // Amedo 2026-10-01
    public unsafe void DrawInstanced(GL gl, int count)
    {
        if (!Uploaded) Upload();
        GlState.VertexArray(gl, _vao);
        TwinShaderProgram.SendLater();
        GlState.SendLater(gl);
        if (_page is not null)
            gl.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, (uint)IndexCount, DrawElementsType.UnsignedInt, (void*)((long)_firstIndex * sizeof(uint)), (uint)count, _baseVertex);
        else if (_useElements)
            gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)IndexCount, DrawElementsType.UnsignedInt, (void*)0, (uint)count);
        else
            gl.DrawArraysInstanced(_prim, 0, (uint)VertexCount, (uint)count);
        GpuStats.Draws++;
        GpuStats.Triangles += (_useElements ? IndexCount : VertexCount) / 3 * count;
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
            float det = GameMath.Dot(e1, h);
            if (MathF.Abs(det) < eps) continue;
            float invDet = 1f / det;
            var s = orig - a;
            float u = GameMath.Dot(s, h) * invDet;
            if (u < 0f || u > 1f) continue;
            var q = Vector3.Cross(s, e1);
            float v = GameMath.Dot(dir, q) * invDet;
            if (v < 0f || u + v > 1f) continue;
            float tri = GameMath.Dot(e2, q) * invDet;
            if (tri > eps && tri < t) { t = tri; hit = true; }
        }
        return hit;
    }

    public void Dispose()
    {
        _pendingVerts = null; // Amedo 2026-09-29
        _pendingIndices = null;
        if (!Uploaded) return;
        Uploaded = false;
        GpuOwner.Forget(_owner, this);
        _owner = null;
        GpuStats.MeshBytes -= _bytes; // Amedo 2026-09-30
        GpuStats.Meshes--;
        if (_page is { } page) // Amedo 2026-10-01
        {
            _page = null;
            MeshPool.Free(_gl, page, _baseVertex, VertexCount, _firstIndex, IndexCount);
            return;
        }
        GlState.ForgetVertexArray();
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        if (_ebo != 0) _gl.DeleteBuffer(_ebo);
    }
}
