using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CrashEngine.Renderer;

// Amedo 2026-09-26
public sealed class ParticleBatch : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector2 UV;
        public Vector4 Color;
    }

    public enum BlendKind { Add = 0, Subtract = 1, Mix = 2, Opaque = 3 }

    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao, _vbo, _ebo;
    private int _vboCapacity;
    private int _eboQuads;

    public ParticleBatch(GL gl)
    {
        _gl = gl;
        var dir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        _shader = new ShaderProgram(gl,
            File.ReadAllText(Path.Combine(dir, "Particle.vert")),
            File.ReadAllText(Path.Combine(dir, "Particle.frag")));

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        _ebo = gl.GenBuffer();
        GlState.VertexArray(gl, _vao); // Amedo 2026-10-01
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        SetAttribs(gl);
        GlState.VertexArray(gl, 0); // Amedo 2026-10-01
    }

    private static unsafe void SetAttribs(GL gl)
    {
        uint stride = (uint)sizeof(Vertex);
        gl.EnableVertexAttribArray(0); gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1); gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)12);
        gl.EnableVertexAttribArray(2); gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)20);
    }

    public void Begin(Matrix4x4 viewProjection)
    {
        _shader.Use();
        _shader.Set("ViewProjection", viewProjection);
        _shader.Set("Tex", 0);
        GlState.VertexArray(_gl, _vao); // Amedo 2026-10-01
        GlState.Culling(_gl, false); // Amedo 2026-10-01
        GlState.DepthTest(_gl, true);
        GlState.DepthFunc(_gl, DepthFunction.Lequal);
    }

    public unsafe void Draw(ReadOnlySpan<Vertex> quadVertices, Texture2D texture, BlendKind blend, float alphaRef = 5f) // Amedo
    {
        int quads = quadVertices.Length / 4;
        if (quads == 0) return;
        EnsureIndices(quads);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        int count = quads * 4;
        if (count > _vboCapacity) _vboCapacity = Math.Max(count, _vboCapacity * 2);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vboCapacity * sizeof(Vertex)), null, BufferUsageARB.StreamDraw);
        fixed (Vertex* p = quadVertices)
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(count * sizeof(Vertex)), p);

        texture.Bind(0);
        if (blend == BlendKind.Opaque)
        {
            GlBlend.Set(_gl, false); // Amedo 2026-09-30
            GlState.DepthMask(_gl, true); // Amedo 2026-10-01
        }
        else
        {
            GlBlend.Set(_gl, true); // Amedo 2026-09-30
            GlState.DepthMask(_gl, false); // Amedo 2026-10-01
            switch (blend)
            {
                case BlendKind.Subtract:
                    _gl.BlendEquationSeparate(BlendEquationModeEXT.FuncReverseSubtract, BlendEquationModeEXT.FuncAdd);
                    _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
                    break;
                case BlendKind.Mix:
                    _gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
                    _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.Zero, BlendingFactor.One);
                    break;
                default:
                    _gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
                    _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
                    break;
            }
        }
        _shader.Set("Opaque", blend == BlendKind.Opaque ? 1 : 0);
        _shader.Set("AlphaRef", alphaRef); // Amedo
        GlState.SendLater(_gl);
        _gl.DrawElements(PrimitiveType.Triangles, (uint)(quads * 6), DrawElementsType.UnsignedInt, (void*)0);
        GpuStats.Draws++; // Amedo 2026-09-30
        GpuStats.Triangles += quads * 2;
    }

    public void End()
    {
        GlState.VertexArray(_gl, 0); // Amedo 2026-10-01
        GlState.DepthMask(_gl, true); // Amedo 2026-10-01
        _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GlBlend.Set(_gl, false); // Amedo 2026-09-30
    }

    private unsafe void EnsureIndices(int quads)
    {
        if (quads <= _eboQuads) return;
        int n = Math.Max(quads, Math.Max(256, _eboQuads * 2));
        var idx = new uint[n * 6];
        for (int q = 0; q < n; q++)
        {
            uint b = (uint)(q * 4);
            int i = q * 6;
            idx[i] = b; idx[i + 1] = b + 1; idx[i + 2] = b + 2;
            idx[i + 3] = b + 1; idx[i + 4] = b + 2; idx[i + 5] = b + 3;
        }
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* p = idx)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(idx.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);
        _eboQuads = n;
    }

    public void Dispose()
    {
        GlState.ForgetVertexArray(); // Amedo 2026-10-01
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _shader.Dispose();
    }
}
