using System.Globalization;
using System.Text.RegularExpressions;
using Silk.NET.OpenGL;

namespace CrashEngine.Core;

public static partial class GlslSource
{
    private const string EsHeader =
        "#version 320 es\n" +
        "precision highp float;\n" +
        "precision highp int;\n" +
        "precision highp sampler2D;\n" +
        "precision highp sampler3D;\n" +
        "precision highp samplerCube;\n" +
        "precision highp sampler2DShadow;\n" +
        "precision highp samplerCubeShadow;\n" +
        "precision highp sampler2DArray;\n" +
        "precision highp sampler2DArrayShadow;\n" +
        "precision highp samplerCubeArray;\n" +
        "precision highp samplerCubeArrayShadow;\n" +
        "precision highp isampler2D;\n" +
        "precision highp usampler2D;\n" +
        "precision highp samplerBuffer;\n";

    public sealed record UniformDefault(string Type, string Name, float[] Values);

    public static readonly HashSet<string> Defines = new();

    public static string FragmentPrecision { get; set; } = "highp";

    public static string ForContext(string src, List<UniformDefault>? defaults = null)
    {
        foreach (var name in Defines) src = WithDefine(src, name);
        return Engine.Instance is { Gles: true } ? ToEs(src, defaults) : src;
    }

    private static string WithDefine(string src, string name)
    {
        var m = VersionLine().Match(src);
        int at = m.Success ? m.Index + m.Length : 0;
        return src[..at] + $"\n#define {name}\n" + src[at..];
    }

    public static string ToEs(string src, List<UniformDefault>? defaults = null)
    {
        var m = VersionLine().Match(src);
        src = m.Success ? src[..m.Index] + EsHeader + src[(m.Index + m.Length)..] : EsHeader + src;
        if (FragmentPrecision != "highp" && !src.Contains("gl_Position") && FirstFunction().Match(src) is { Success: true } f)
        {
            var rest = FloatUniform().Replace(src[f.Index..], "${head}uniform highp ${type}");
            rest = FloatConst().Replace(rest, "${head}const highp ${type}");
            rest = StructBody().Replace(rest, s => StructMember().Replace(s.Value, "${head}highp ${type}"));
            src = src[..f.Index] + $"precision {FragmentPrecision} float;\n" + rest;
        }
        return UniformInitializer().Replace(src, u =>
        {
            var value = u.Groups["value"].Value;
            value = value[(value.IndexOf('(') + 1)..];
            defaults?.Add(new UniformDefault(u.Groups["type"].Value, u.Groups["name"].Value,
                Number().Matches(value).Select(n => float.Parse(n.Value, CultureInfo.InvariantCulture)).ToArray()));
            return $"{u.Groups["head"].Value};";
        });
    }

    public static void ApplyDefaults(GL gl, uint program, List<UniformDefault> defaults)
    {
        if (defaults.Count == 0) return;
        gl.GetInteger(GetPName.CurrentProgram, out int previous);
        gl.UseProgram(program);
        foreach (var d in defaults)
        {
            int loc = gl.GetUniformLocation(program, d.Name);
            if (loc < 0) continue;
            float V(int i) => d.Values.Length == 1 ? d.Values[0] : d.Values[i];
            switch (d.Type)
            {
                case "float": gl.Uniform1(loc, V(0)); break;
                case "vec2": gl.Uniform2(loc, V(0), V(1)); break;
                case "vec3": gl.Uniform3(loc, V(0), V(1), V(2)); break;
                case "vec4": gl.Uniform4(loc, V(0), V(1), V(2), V(3)); break;
                case "int": gl.Uniform1(loc, (int)V(0)); break;
                default: throw new NotSupportedException($"uniform {d.Type} {d.Name} has an initial value that GLES can't take");
            }
        }
        gl.UseProgram((uint)previous);
    }

    [GeneratedRegex(@"^[ \t]*#version[^\r\n]*", RegexOptions.Multiline)]
    private static partial Regex VersionLine();

    [GeneratedRegex(@"^[ \t]*(?:void|float|int|bool|vec[234]|ivec[234]|mat[234])[ \t]+\w+[ \t]*\(", RegexOptions.Multiline)]
    private static partial Regex FirstFunction();

    [GeneratedRegex(@"(?<head>^[ \t]*)uniform[ \t]+(?<type>float|vec[234]|mat[234])\b", RegexOptions.Multiline)]
    private static partial Regex FloatUniform();

    [GeneratedRegex(@"(?<head>^[ \t]*)const[ \t]+(?<type>float|vec[234]|mat[234])\b", RegexOptions.Multiline)]
    private static partial Regex FloatConst();

    [GeneratedRegex(@"\bstruct\s+\w+\s*\{[^}]*\}")]
    private static partial Regex StructBody();

    [GeneratedRegex(@"(?<head>(?:[{;]|\n)[ \t]*)(?<type>float|vec[234]|mat[234])\b")]
    private static partial Regex StructMember();

    [GeneratedRegex(@"(?<head>\buniform\s+(?:(?:lowp|mediump|highp)\s+)?(?<type>\w+)\s+(?<name>\w+))\s*=\s*(?<value>[^;]+);")]
    private static partial Regex UniformInitializer();

    [GeneratedRegex(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex Number();
}
