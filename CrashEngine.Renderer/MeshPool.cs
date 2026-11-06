using CrashEngine.Core;
using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

// Amedo 2026-10-01
public static class MeshPool
{
    public const int PageVertices = 256 * 1024, PageIndices = 1024 * 1024;

    public static bool Enabled { get; set; } = true;
    public static int PageCount => Pages.Count;

    internal sealed class Page
    {
        public uint Vao, Vbo, Ebo;
        public readonly Ranges Vertices, Indices;
        public int Users;
        public long Capacity;

        public Page(int vertices, int indices)
        {
            Vertices = new Ranges(vertices);
            Indices = new Ranges(indices);
        }
    }

    internal sealed class Ranges
    {
        private readonly List<(int Start, int Length)> _free = new();

        public Ranges(int capacity) => _free.Add((0, capacity));

        public int Take(int length)
        {
            for (int i = 0; i < _free.Count; i++)
            {
                var (start, free) = _free[i];
                if (free < length) continue;
                if (free == length) _free.RemoveAt(i);
                else _free[i] = (start + length, free - length);
                return start;
            }
            return -1;
        }

        public void Give(int start, int length)
        {
            int i = 0;
            while (i < _free.Count && _free[i].Start < start) i++;
            _free.Insert(i, (start, length));
            if (i + 1 < _free.Count && _free[i].Start + _free[i].Length == _free[i + 1].Start)
            {
                _free[i] = (_free[i].Start, _free[i].Length + _free[i + 1].Length);
                _free.RemoveAt(i + 1);
            }
            if (i > 0 && _free[i - 1].Start + _free[i - 1].Length == _free[i].Start)
            {
                _free[i - 1] = (_free[i - 1].Start, _free[i - 1].Length + _free[i].Length);
                _free.RemoveAt(i);
            }
        }
    }

    private static readonly List<Page> Pages = new();

    static MeshPool() => Engine.GpuContextStarted += ForgetContext;

    private static void ForgetContext() => Pages.Clear();

    internal static unsafe Page? Allocate(GL gl, ReadOnlySpan<GpuMesh.Vertex> verts, ReadOnlySpan<uint> indices, out int baseVertex, out int firstIndex)
    {
        baseVertex = firstIndex = -1;
        if (!Enabled || verts.Length == 0 || indices.Length == 0) return null;
        Page? page = null;
        foreach (var p in Pages)
        {
            int v = p.Vertices.Take(verts.Length);
            if (v < 0) continue;
            int i = p.Indices.Take(indices.Length);
            if (i < 0) { p.Vertices.Give(v, verts.Length); continue; }
            page = p;
            baseVertex = v;
            firstIndex = i;
            break;
        }
        if (page is null)
        {
            page = Create(gl, Math.Max(PageVertices, verts.Length), Math.Max(PageIndices, indices.Length));
            baseVertex = page.Vertices.Take(verts.Length);
            firstIndex = page.Indices.Take(indices.Length);
        }
        fixed (GpuMesh.Vertex* pv = verts)
            if (!StagingRing.Copy(gl, page.Vbo, (long)baseVertex * sizeof(GpuMesh.Vertex), pv, (long)verts.Length * sizeof(GpuMesh.Vertex))) // Amedo
                Write(gl, page.Vbo, (nint)((long)baseVertex * sizeof(GpuMesh.Vertex)), (nuint)(verts.Length * sizeof(GpuMesh.Vertex)), pv);
        fixed (uint* pi = indices)
            if (!StagingRing.Copy(gl, page.Ebo, (long)firstIndex * sizeof(uint), pi, (long)indices.Length * sizeof(uint))) // Amedo
                Write(gl, page.Ebo, (nint)((long)firstIndex * sizeof(uint)), (nuint)(indices.Length * sizeof(uint)), pi);
        page.Users++;
        return page;
    }

    private static unsafe void Write(GL gl, uint buffer, nint offset, nuint size, void* data)
    {
        if (GlCaps.Dsa)
        {
            gl.NamedBufferSubData(buffer, offset, size, data);
            return;
        }
        gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, buffer);
        gl.BufferSubData(BufferTargetARB.CopyWriteBuffer, offset, size, data);
        gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);
    }

    // Amedo
    public static void Maintain(GL gl)
    {
        if (!Enabled || Pages.Count == 0 || GpuUploadQueue.LastFrame >= PerfStats.FrameNumber - 1) return;
        foreach (var p in Pages)
            if (p.Users == 0) return;
        Create(gl, PageVertices, PageIndices);
    }

    internal static void Free(GL gl, Page page, int baseVertex, int vertices, int firstIndex, int indices)
    {
        page.Vertices.Give(baseVertex, vertices);
        page.Indices.Give(firstIndex, indices);
        if (--page.Users > 0 || Pages.Count <= 1 || !Pages.Contains(page)) return;
        bool spare = true; // Amedo
        foreach (var p in Pages)
            if (!ReferenceEquals(p, page) && p.Users == 0) spare = false;
        if (spare) return;
        Pages.Remove(page);
        GlState.ForgetVertexArray();
        gl.DeleteVertexArray(page.Vao);
        gl.DeleteBuffer(page.Vbo);
        gl.DeleteBuffer(page.Ebo);
        GpuStats.MeshPoolBytes -= page.Capacity;
    }

    private static unsafe Page Create(GL gl, int vertices, int indices)
    {
        var page = new Page(vertices, indices)
        {
            Vao = GlNames.VertexArray(gl),
            Vbo = GlNames.Buffer(gl),
            Ebo = GlNames.Buffer(gl),
        };
        GlState.VertexArray(gl, page.Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, page.Vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)((long)vertices * sizeof(GpuMesh.Vertex)), null, BufferUsageARB.StaticDraw);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, page.Ebo);
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)((long)indices * sizeof(uint)), null, BufferUsageARB.StaticDraw);
        GpuMesh.SetAttribs(gl);
        GlState.VertexArray(gl, 0);
        page.Capacity = (long)vertices * sizeof(GpuMesh.Vertex) + (long)indices * sizeof(uint);
        GpuStats.MeshPoolBytes += page.Capacity;
        Pages.Add(page);
        return page;
    }
}
