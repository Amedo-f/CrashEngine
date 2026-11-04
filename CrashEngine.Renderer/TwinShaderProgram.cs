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
        var defaults = new List<CrashEngine.Core.GlslSource.UniformDefault>();
        uint v = Compile(gl, shaderDir, vertFile, ShaderType.VertexShader, defaults);
        uint f = Compile(gl, shaderDir, fragFile, ShaderType.FragmentShader, defaults);

        _id = gl.CreateProgram();
        gl.AttachShader(_id, v);
        gl.AttachShader(_id, f);
        gl.LinkProgram(_id);
        gl.GetProgram(_id, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0) throw new Exception("Program link failed:\n" + gl.GetProgramInfoLog(_id));

        gl.DetachShader(_id, v); gl.DetachShader(_id, f);
        gl.DeleteShader(v);      gl.DeleteShader(f);
        CrashEngine.Core.GlslSource.ApplyDefaults(gl, _id, defaults);
    }

    private static uint Compile(GL gl, string dir, string file, ShaderType type, List<CrashEngine.Core.GlslSource.UniformDefault> defaults)
    {
        var src = "#version 450 core\r\n" + LoadWithIncludes(Path.Combine(dir, file));
        uint s = gl.CreateShader(type);
        gl.ShaderSource(s, CrashEngine.Core.GlslSource.ForContext(src, defaults));
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

    // Amedo 2026-10-01
    public sealed class Slot
    {
        internal int Loc;
        internal int Kind;
        internal int I;
        internal Vector4 V;
        internal Matrix4x4 M;
        internal bool Later, Queued;
        internal int LaterKind, LaterI;
        internal Vector4 LaterV;
    }

    private readonly List<Slot> _later = new();
    private static readonly List<TwinShaderProgram> _withLater = new();

    public void SetLater(Slot s, int v) => Later(s, 1, v, default);
    public void SetLater(Slot s, float v) => Later(s, 2, 0, new Vector4(v, 0f, 0f, 0f));
    public void SetLater(Slot s, Vector2 v) => Later(s, 3, 0, new Vector4(v, 0f, 0f));
    public void SetLater(Slot s, Vector4 v) => Later(s, 5, 0, v);

    private void Later(Slot s, int kind, int i, Vector4 v)
    {
        if (s.Loc < 0) return;
        if (s.Kind == kind && (kind == 1 ? s.I == i : Same(s.V, v)))
        {
            s.Later = false;
            return;
        }
        s.Later = true;
        s.LaterKind = kind;
        s.LaterI = i;
        s.LaterV = v;
        if (s.Queued) return;
        s.Queued = true;
        if (_later.Count == 0) _withLater.Add(this);
        _later.Add(s);
    }

    public static void SendLater()
    {
        if (_withLater.Count == 0) return;
        foreach (var p in _withLater)
        {
            foreach (var s in p._later)
            {
                s.Queued = false;
                if (!s.Later) continue;
                s.Later = false;
                s.Kind = s.LaterKind;
                s.I = s.LaterI;
                s.V = s.LaterV;
                switch (s.Kind)
                {
                    case 1: p._gl.ProgramUniform1(p._id, s.Loc, s.I); break;
                    case 2: p._gl.ProgramUniform1(p._id, s.Loc, s.V.X); break;
                    case 3: p._gl.ProgramUniform2(p._id, s.Loc, s.V.X, s.V.Y); break;
                    case 5: p._gl.ProgramUniform4(p._id, s.Loc, s.V.X, s.V.Y, s.V.Z, s.V.W); break;
                }
            }
            p._later.Clear();
        }
        _withLater.Clear();
    }

    // Amedo 2026-10-01
    public Slot Uniform(string n) => S(n);

    private readonly Dictionary<Type, object> _cached = new();

    public T Cached<T>(Func<TwinShaderProgram, T> make) where T : class
    {
        if (_cached.TryGetValue(typeof(T), out var o)) return (T)o;
        var made = make(this);
        _cached[typeof(T)] = made;
        return made;
    }

    private readonly Dictionary<string, Slot> _slots = new();
    private readonly Dictionary<string, Slot> _literal = new(ReferenceEqualityComparer.Instance); // Amedo 2026-09-29

    private Slot S(string n)
    {
        if (_literal.TryGetValue(n, out var s)) return s; // Amedo 2026-09-29
        if (!_slots.TryGetValue(n, out s))
        {
            s = new Slot { Loc = Loc(n) };
            _slots[n] = s;
        }
        if (ReferenceEquals(string.IsInterned(n), n)) _literal[n] = s;
        return s;
    }

    private static bool Same(Vector4 a, Vector4 b) =>
        BitConverter.SingleToInt32Bits(a.X) == BitConverter.SingleToInt32Bits(b.X) &&
        BitConverter.SingleToInt32Bits(a.Y) == BitConverter.SingleToInt32Bits(b.Y) &&
        BitConverter.SingleToInt32Bits(a.Z) == BitConverter.SingleToInt32Bits(b.Z) &&
        BitConverter.SingleToInt32Bits(a.W) == BitConverter.SingleToInt32Bits(b.W);

    private bool Changed(Slot s, int kind, Vector4 v)
    {
        s.Later = false;
        if (s.Loc < 0 || (s.Kind == kind && Same(s.V, v))) return false;
        s.Kind = kind;
        s.V = v;
        return true;
    }

    public void Set(string n, int v) => Set(S(n), v); // Amedo 2026-10-01

    // Amedo 2026-10-01
    public void Set(Slot s, int v)
    {
        s.Later = false;
        if (s.Loc < 0 || (s.Kind == 1 && s.I == v)) return;
        s.Kind = 1;
        s.I = v;
        _gl.Uniform1(s.Loc, v);
    }

    public void Set(string n, float v)   => Set(S(n), v); // Amedo 2026-10-01
    public void Set(string n, Vector2 v) => Set(S(n), v);
    public void Set(string n, Vector3 v) => Set(S(n), v);
    public void Set(string n, Vector4 v) => Set(S(n), v);

    // Amedo 2026-10-01
    public void Set(Slot s, float v)   { if (Changed(s, 2, new Vector4(v, 0f, 0f, 0f))) _gl.Uniform1(s.Loc, v); }
    public void Set(Slot s, Vector2 v) { if (Changed(s, 3, new Vector4(v, 0f, 0f))) _gl.Uniform2(s.Loc, v.X, v.Y); }
    public void Set(Slot s, Vector3 v) { if (Changed(s, 4, new Vector4(v, 0f))) _gl.Uniform3(s.Loc, v.X, v.Y, v.Z); }
    public void Set(Slot s, Vector4 v) { if (Changed(s, 5, v)) _gl.Uniform4(s.Loc, v.X, v.Y, v.Z, v.W); }

    public void Set(string n, Matrix4x4 m) => Set(S(n), m); // Amedo 2026-10-01

    // Amedo 2026-10-01
    public void Set(Slot s, Matrix4x4 m)
    {
        s.Later = false;
        if (s.Loc < 0) return;
        if (s.Kind == 6 && System.Runtime.InteropServices.MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in s.M))
                .SequenceEqual(System.Runtime.InteropServices.MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in m)))) return;
        s.Kind = 6;
        s.M = m;
        _gl.UniformMatrix4(s.Loc, 1, false, in m.M11);
    }

    public unsafe void SetMatrixArray(string n, ReadOnlySpan<Matrix4x4> mats)
    {
        var l = Loc(n);
        if (l < 0 || mats.Length == 0) return;
        fixed (Matrix4x4* p = mats)
            _gl.UniformMatrix4(l, (uint)mats.Length, false, (float*)p);
    }

    // Amedo 2026-09-26
    public unsafe void SetVector3Array(string n, ReadOnlySpan<Vector3> v)
    {
        var l = Loc(n);
        if (l < 0 || v.Length == 0) return;
        fixed (Vector3* p = v)
            _gl.Uniform3(l, (uint)v.Length, (float*)p);
    }

    // Amedo
    public unsafe void SetVector4Array(string n, ReadOnlySpan<Vector4> v)
    {
        var l = Loc(n);
        if (l < 0 || v.Length == 0) return;
        fixed (Vector4* p = v)
            _gl.Uniform4(l, (uint)v.Length, (float*)p);
    }

    public void Dispose()
    {
        _withLater.Remove(this);
        _later.Clear();
        _gl.DeleteProgram(_id);
    }
}
