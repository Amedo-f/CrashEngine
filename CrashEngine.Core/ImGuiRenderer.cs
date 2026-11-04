using ImGuiNET;
using Silk.NET.OpenGL;

namespace CrashEngine.Core;

// Amedo
public static class ImGuiBlend
{
    internal static readonly nint AddToken = 0x41444431, MixToken = 0x4D495831;

    public static void Add(ImDrawListPtr list) => list.AddCallback(AddToken, 0);
    public static void Mix(ImDrawListPtr list) => list.AddCallback(MixToken, 0);
}

// Amedo
internal sealed unsafe class ImGuiRenderer : IDisposable
{
    private const string VertexSource = @"#version 330
        layout (location = 0) in vec2 Position;
        layout (location = 1) in vec2 UV;
        layout (location = 2) in vec4 Color;
        uniform mat4 ProjMtx;
        out vec2 Frag_UV;
        out vec4 Frag_Color;
        void main()
        {
            Frag_UV = UV;
            Frag_Color = Color;
            gl_Position = ProjMtx * vec4(Position.xy,0,1);
        }";

    private const string FragmentSource = @"#version 330
        in vec2 Frag_UV;
        in vec4 Frag_Color;
        uniform sampler2D Texture;
        layout (location = 0) out vec4 Out_Color;
        void main()
        {
            Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
        }";

    private readonly GL _gl;
    private readonly uint _program, _vbo, _ebo;
    private readonly int _tex, _proj;
    private readonly uint _pos, _uv, _col;
    private uint _vao;
    private bool _add;

    public ImGuiRenderer(GL gl)
    {
        _gl = gl;
        _gl.GetInteger(GLEnum.TextureBinding2D, out int lastTexture);
        _gl.GetInteger(GLEnum.ArrayBufferBinding, out int lastArrayBuffer);
        _gl.GetInteger(GLEnum.VertexArrayBinding, out int lastVertexArray);
        _program = Link(Compile(ShaderType.VertexShader, VertexSource), Compile(ShaderType.FragmentShader, FragmentSource));
        _tex = _gl.GetUniformLocation(_program, "Texture");
        _proj = _gl.GetUniformLocation(_program, "ProjMtx");
        _pos = (uint)_gl.GetAttribLocation(_program, "Position");
        _uv = (uint)_gl.GetAttribLocation(_program, "UV");
        _col = (uint)_gl.GetAttribLocation(_program, "Color");
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();
        _vao = _gl.GenVertexArray(); // Amedo
        _gl.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
        _gl.BindBuffer(GLEnum.ArrayBuffer, (uint)lastArrayBuffer);
        _gl.BindVertexArray((uint)lastVertexArray);
    }

    private uint Compile(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, GlslSource.ForContext(source));
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException($"ImGui {type}: {_gl.GetShaderInfoLog(shader)}");
        return shader;
    }

    private uint Link(uint vertex, uint fragment)
    {
        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vertex);
        _gl.AttachShader(program, fragment);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException($"ImGui link: {_gl.GetProgramInfoLog(program)}");
        _gl.DetachShader(program, vertex);
        _gl.DetachShader(program, fragment);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);
        return program;
    }

    // Amedo
    private void SetupRenderState(ImDrawDataPtr data)
    {
        _gl.Enable(GLEnum.Blend);
        _gl.BlendEquation(GLEnum.FuncAdd);
        _gl.BlendFuncSeparate(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha, GLEnum.One, GLEnum.OneMinusSrcAlpha);
        _add = false;
        _gl.Disable(GLEnum.CullFace);
        _gl.Disable(GLEnum.DepthTest);
        _gl.Disable(GLEnum.StencilTest);
        _gl.Enable(GLEnum.ScissorTest);
        if (!GlCaps.Gles) _gl.Disable(GLEnum.PrimitiveRestart); // Amedo 2026-10-26
        if (GlCaps.PolygonMode) _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill); // Amedo 2026-10-26
        float l = data.DisplayPos.X, r = data.DisplayPos.X + data.DisplaySize.X;
        float t = data.DisplayPos.Y, b = data.DisplayPos.Y + data.DisplaySize.Y;
        Span<float> ortho = stackalloc float[]
        {
            2f / (r - l), 0f, 0f, 0f,
            0f, 2f / (t - b), 0f, 0f,
            0f, 0f, -1f, 0f,
            (r + l) / (l - r), (t + b) / (b - t), 0f, 1f,
        };
        _gl.UseProgram(_program);
        _gl.Uniform1(_tex, 0);
        _gl.UniformMatrix4(_proj, 1, false, ortho);
        _gl.BindSampler(0, 0);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);
        _gl.BindBuffer(GLEnum.ElementArrayBuffer, _ebo);
        _gl.EnableVertexAttribArray(_pos);
        _gl.EnableVertexAttribArray(_uv);
        _gl.EnableVertexAttribArray(_col);
        _gl.VertexAttribPointer(_pos, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)0);
        _gl.VertexAttribPointer(_uv, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)8);
        _gl.VertexAttribPointer(_col, 4, GLEnum.UnsignedByte, true, (uint)sizeof(ImDrawVert), (void*)16);
    }

    // Amedo
    private void SetBlend(bool add)
    {
        if (_add == add) return;
        _add = add;
        if (add) _gl.BlendFuncSeparate(GLEnum.SrcAlpha, GLEnum.One, GLEnum.Zero, GLEnum.One);
        else _gl.BlendFuncSeparate(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha, GLEnum.One, GLEnum.OneMinusSrcAlpha);
    }

    // Amedo
    public void Render(ImDrawDataPtr data)
    {
        int fbWidth = (int)(data.DisplaySize.X * data.FramebufferScale.X);
        int fbHeight = (int)(data.DisplaySize.Y * data.FramebufferScale.Y);
        if (fbWidth <= 0 || fbHeight <= 0) return;

        _gl.ActiveTexture(GLEnum.Texture0); // Amedo
        SetupRenderState(data);

        var clipOff = data.DisplayPos;
        var clipScale = data.FramebufferScale;
        for (int n = 0; n < data.CmdListsCount; n++)
        {
            var list = data.CmdLists[n];
            SetBlend(false);
            _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(list.VtxBuffer.Size * sizeof(ImDrawVert)), (void*)list.VtxBuffer.Data, GLEnum.StreamDraw);
            _gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(list.IdxBuffer.Size * sizeof(ushort)), (void*)list.IdxBuffer.Data, GLEnum.StreamDraw);
            for (int i = 0; i < list.CmdBuffer.Size; i++)
            {
                var cmd = list.CmdBuffer[i];
                if (cmd.UserCallback != 0)
                {
                    if (cmd.UserCallback == ImGuiBlend.AddToken) SetBlend(true);
                    else if (cmd.UserCallback == ImGuiBlend.MixToken) SetBlend(false);
                    else if (cmd.UserCallback == -1 || cmd.UserCallback == -8) SetupRenderState(data);
                    else throw new NotImplementedException();
                    continue;
                }
                float x = (cmd.ClipRect.X - clipOff.X) * clipScale.X;
                float y = (cmd.ClipRect.Y - clipOff.Y) * clipScale.Y;
                float z = (cmd.ClipRect.Z - clipOff.X) * clipScale.X;
                float w = (cmd.ClipRect.W - clipOff.Y) * clipScale.Y;
                if (x < fbWidth && y < fbHeight && z >= 0f && w >= 0f)
                {
                    _gl.Scissor((int)x, (int)(fbHeight - w), (uint)(z - x), (uint)(w - y));
                    _gl.BindTexture(GLEnum.Texture2D, (uint)cmd.TextureId);
                    _gl.DrawElementsBaseVertex(GLEnum.Triangles, cmd.ElemCount, GLEnum.UnsignedShort, (void*)(cmd.IdxOffset * sizeof(ushort)), (int)cmd.VtxOffset);
                }
            }
        }

        _gl.UseProgram(0); // Amedo
        _gl.BindTexture(GLEnum.Texture2D, 0);
        _gl.BindVertexArray(0);
        _gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        _gl.BlendEquation(GLEnum.FuncAdd);
        _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        _gl.Disable(GLEnum.Blend);
        _gl.Disable(GLEnum.CullFace);
        _gl.Enable(GLEnum.DepthTest);
        _gl.Disable(GLEnum.StencilTest);
        _gl.Disable(GLEnum.ScissorTest);
        if (!GlCaps.Gles) _gl.Disable(GLEnum.PrimitiveRestart); // Amedo 2026-10-26
        if (GlCaps.PolygonMode) _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill); // Amedo 2026-10-26
        _gl.Scissor(0, 0, (uint)fbWidth, (uint)fbHeight);
        Engine.UiStateReset?.Invoke();
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao); // Amedo
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteProgram(_program);
    }
}
