using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

public sealed class TwinShaderProgram : IDisposable
{
    private readonly GL   _gl;
    private readonly uint _id;
    private readonly Dictionary<string, int> _loc = new();

    public uint Handle => _id;

    public TwinShaderProgram(GL gl, string shaderDir, string vertFile, string fragFile)
    {
        _gl = gl;
        uint v = Compile(gl, shaderDir, vertFile, ShaderType.VertexShader);
        uint f = Compile(gl, shaderDir, fragFile, ShaderType.FragmentShader);

        _id = gl.CreateProgram();
        gl.AttachShader(_id, v);
        gl.AttachShader(_id, f);
        gl.LinkProgram(_id);
        gl.GetProgram(_id, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0) throw new Exception("Program link failed:\n" + gl.GetProgramInfoLog(_id));

        gl.DetachShader(_id, v); gl.DetachShader(_id, f);
        gl.DeleteShader(v);      gl.DeleteShader(f);
    }

    private static uint Compile(GL gl, string dir, string file, ShaderType type)
    {
        var src = "#version 450 core\r\n" + LoadWithIncludes(Path.Combine(dir, file));
        uint s = gl.CreateShader(type);
        gl.ShaderSource(s, src);
        gl.CompileShader(s);
        gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0) throw new Exception($"{type} ({file}) compile failed:\n" + gl.GetShaderInfoLog(s));
        return s;
    }

    private static string LoadWithIncludes(string path)
    {
        var full  = Path.GetFullPath(path);
        var dir   = Path.GetDirectoryName(full)!;
        var lines = File.ReadAllLines(full);
        var sb    = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#include"))
            {
                int a = line.IndexOf('"');
                int b = line.LastIndexOf('"');
                if (a >= 0 && b > a)
                {
                    var inc = line.Substring(a + 1, b - a - 1);
                    var incPath = Path.GetFullPath(Path.Combine(dir, inc));
                    sb.AppendLine(LoadWithIncludes(incPath));
                    continue;
                }
            }
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    public void Use() => _gl.UseProgram(_id);

    private int Loc(string n)
    {
        if (_loc.TryGetValue(n, out var l)) return l;
        l = _gl.GetUniformLocation(_id, n);
        _loc[n] = l;
        return l;
    }

    public void Set(string n, int v)     { var l = Loc(n); if (l >= 0) _gl.Uniform1(l, v); }
    public void Set(string n, float v)   { var l = Loc(n); if (l >= 0) _gl.Uniform1(l, v); }
    public void Set(string n, Vector2 v) { var l = Loc(n); if (l >= 0) _gl.Uniform2(l, v.X, v.Y); }
    public void Set(string n, Vector3 v) { var l = Loc(n); if (l >= 0) _gl.Uniform3(l, v.X, v.Y, v.Z); }
    public void Set(string n, Vector4 v) { var l = Loc(n); if (l >= 0) _gl.Uniform4(l, v.X, v.Y, v.Z, v.W); }
    public void Set(string n, Matrix4x4 m) { var l = Loc(n); if (l >= 0) _gl.UniformMatrix4(l, 1, false, in m.M11); }

    public unsafe void SetMatrixArray(string n, ReadOnlySpan<Matrix4x4> mats)
    {
        var l = Loc(n);
        if (l < 0 || mats.Length == 0) return;
        fixed (Matrix4x4* p = mats)
            _gl.UniformMatrix4(l, (uint)mats.Length, false, (float*)p);
    }

    public void Dispose() => _gl.DeleteProgram(_id);
}
