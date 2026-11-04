using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

public sealed class ShaderProgram : IDisposable
{
    private readonly GL   _gl;
    private readonly uint _id;

    public ShaderProgram(GL gl, string vert, string frag)
    {
        _gl = gl;
        var defaults = new List<CrashEngine.Core.GlslSource.UniformDefault>();
        uint v = Compile(ShaderType.VertexShader,   vert, defaults);
        uint f = Compile(ShaderType.FragmentShader, frag, defaults);

        _id = gl.CreateProgram();
        gl.AttachShader(_id, v);
        gl.AttachShader(_id, f);
        gl.LinkProgram(_id);
        gl.GetProgram(_id, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0) throw new Exception("Shader link:\n" + gl.GetProgramInfoLog(_id));

        gl.DeleteShader(v);
        gl.DeleteShader(f);
        CrashEngine.Core.GlslSource.ApplyDefaults(gl, _id, defaults);
    }

    private uint Compile(ShaderType type, string src, List<CrashEngine.Core.GlslSource.UniformDefault> defaults)
    {
        uint s = _gl.CreateShader(type);
        _gl.ShaderSource(s, CrashEngine.Core.GlslSource.ForContext(src, defaults));
        _gl.CompileShader(s);
        _gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0) throw new Exception($"{type}:\n" + _gl.GetShaderInfoLog(s));
        return s;
    }

    public void Use() => _gl.UseProgram(_id);

    private readonly Dictionary<string, int> _locations = new(); // Amedo 2026-10-01

    private int Loc(string n)
    {
        if (!_locations.TryGetValue(n, out int l)) _locations[n] = l = _gl.GetUniformLocation(_id, n);
        return l;
    }

    public void Set(string n, int     v) => _gl.Uniform1(Loc(n), v);
    public void Set(string n, float   v) => _gl.Uniform1(Loc(n), v);
    public void Set(string n, Vector2 v) => _gl.Uniform2(Loc(n), v.X, v.Y);
    public void Set(string n, Vector3 v) => _gl.Uniform3(Loc(n), v.X, v.Y, v.Z);
    public void Set(string n, Vector4 v) => _gl.Uniform4(Loc(n), v.X, v.Y, v.Z, v.W);

    public void Set(string n, Matrix4x4 m) =>
        _gl.UniformMatrix4(Loc(n), 1, false, in m.M11);

    public void Dispose() => _gl.DeleteProgram(_id);
}
