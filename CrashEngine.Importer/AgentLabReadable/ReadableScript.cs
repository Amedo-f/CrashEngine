using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Importer.AgentLabReadable;

// Amedo 2026-09-25
public enum FieldKind { UInt, Hex, Bool, NotBool, Enum, Variant, Float, SlotList }

public sealed class FieldSpec
{
    public string Name = "";
    public FieldKind Kind;
    public int Word;
    public int Shift;
    public int Bits = 32;
    public uint Default;
    public bool HasDefault;
    public Dictionary<uint, string>? Names;
    public int CountWord, CountShift, CountBits;

    public uint Mask => Bits >= 32 ? 0xFFFFFFFFu : ((1u << Bits) - 1u) << Shift;
}

public sealed class CommandSpec
{
    public ushort Op;
    public string Name = "";
    public int ArgCount;
    public uint[] PadDefault = Array.Empty<uint>();
    public List<FieldSpec> Fields = new();

    public CommandSpec F(string name, FieldKind kind, int word, int shift = 0, int bits = 32, uint? def = null, Dictionary<uint, string>? names = null)
    {
        Fields.Add(new FieldSpec { Name = name, Kind = kind, Word = word, Shift = shift, Bits = bits, Default = def ?? 0, HasDefault = def.HasValue, Names = names });
        return this;
    }

    public CommandSpec Slots(string name, int word, int countWord, int countShift, int countBits)
    {
        Fields.Add(new FieldSpec { Name = name, Kind = FieldKind.SlotList, Word = word, CountWord = countWord, CountShift = countShift, CountBits = countBits });
        return this;
    }

    public uint CoveredMask(int word)
    {
        uint m = 0;
        foreach (var f in Fields)
        {
            if (f.Kind == FieldKind.SlotList)
            {
                if (f.Word == word) m = 0xFFFFFFFF;
                if (f.CountWord == word) m |= ((1u << f.CountBits) - 1u) << f.CountShift;
            }
            else if (f.Word == word) m |= f.Mask;
        }
        return m;
    }
}

public static class ReadableScript
{
    private static readonly Regex RawLine = new(@"^(\s*)@PS2 (\w+)\((.*)\);\s*$", RegexOptions.Compiled);
    private static readonly Regex ReadLine = new(@"^(\s*)([A-Za-z_]\w*)\((.*)\);\s*$", RegexOptions.Compiled);

    public static IReadOnlyDictionary<string, CommandSpec> ByName => _byName;
    public static IReadOnlyDictionary<ushort, CommandSpec> ByOp => _byOp;
    private static readonly Dictionary<string, CommandSpec> _byName = new();
    private static readonly Dictionary<ushort, CommandSpec> _byOp = new();

    static ReadableScript()
    {
        foreach (var s in ReadableCommands.All()) { _byName[s.Name] = s; _byOp[s.Op] = s; }
    }

    public static string? Decode(CommandSpec spec, uint[] args)
    {
        if (args.Length != spec.ArgCount) return null;
        var parts = new List<string>();
        foreach (var f in spec.Fields)
        {
            string? v = DecodeField(f, args);
            if (v is null) return null;
            if (v.Length > 0) parts.Add($"{f.Name}: {v}");
        }
        for (int w = 0; w < spec.ArgCount; w++)
        {
            uint rest = args[w] & ~spec.CoveredMask(w);
            uint def = (w < spec.PadDefault.Length ? spec.PadDefault[w] : 0) & ~spec.CoveredMask(w);
            if (rest != def) parts.Add($"pad{w}: 0x{rest:X8}");
        }
        string text = string.Join(", ", parts);
        try
        {
            var back = Encode(spec, ParseFields(text));
            if (!back.SequenceEqual(args)) return null;
        }
        catch { return null; }
        return text;
    }

    private static string? DecodeField(FieldSpec f, uint[] a)
    {
        switch (f.Kind)
        {
            case FieldKind.SlotList:
            {
                int n = (int)((a[f.CountWord] >> f.CountShift) & ((1u << f.CountBits) - 1u));
                if (n > 4) return null;
                uint w = a[f.Word];
                var sl = new List<string>();
                for (int i = 0; i < 4; i++)
                {
                    uint b = (w >> (8 * i)) & 0xFF;
                    if (i < n) sl.Add(b.ToString(CultureInfo.InvariantCulture));
                    else if (b != 0xFF) return null;
                }
                return "[" + string.Join(", ", sl) + "]";
            }
            case FieldKind.Variant:
                return FormatVariant(a[f.Word], f);
            case FieldKind.Float:
            {
                uint bits = a[f.Word];
                if (f.HasDefault && bits == f.Default) return "";
                float fl = BitConverter.UInt32BitsToSingle(bits);
                if (float.IsNaN(fl) || float.IsInfinity(fl)) return $"raw(0x{bits:X8})";
                return FloatText(fl, bits, 0xFFFFFFFF);
            }
        }
        uint val = f.Bits >= 32 ? a[f.Word] : (a[f.Word] >> f.Shift) & ((1u << f.Bits) - 1u);
        if (f.HasDefault && val == f.Default) return "";
        return f.Kind switch
        {
            FieldKind.UInt => val.ToString(CultureInfo.InvariantCulture),
            FieldKind.Hex => $"0x{val:X}",
            FieldKind.Bool => val != 0 ? "true" : "false",
            FieldKind.NotBool => val == 0 ? "true" : "false",
            FieldKind.Enum => f.Names != null && f.Names.TryGetValue(val, out var nm) ? nm : $"0x{val:X}",
            _ => null,
        };
    }

    private static string FormatVariant(uint w, FieldSpec f)
    {
        if (f.HasDefault && w == f.Default) return "";
        uint tag = w & 7;
        switch (tag)
        {
            case 0: return ((int)w >> 3).ToString(CultureInfo.InvariantCulture);
            case 1: return $"var({w >> 3})";
            case 5: return $"fvar({w >> 3})";
            case 4:
            {
                uint bits = w & ~7u;
                float fl = BitConverter.UInt32BitsToSingle(bits);
                if (float.IsNaN(fl) || float.IsInfinity(fl)) return $"raw(0x{w:X8})";
                return FloatText(fl, bits, ~7u) + "f";
            }
            default: return $"raw(0x{w:X8})";
        }
    }

    private static string FloatText(float fl, uint bits, uint mask)
    {
        for (int digits = 1; digits <= 9; digits++)
        {
            string s = fl.ToString("G" + digits, CultureInfo.InvariantCulture);
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) &&
                (BitConverter.SingleToUInt32Bits(p) & mask) == (bits & mask))
                return s.Contains('.') || s.Contains('E') ? s : s + ".0";
        }
        return fl.ToString("R", CultureInfo.InvariantCulture);
    }

    public static List<(string key, string val)> ParseFields(string text)
    {
        var res = new List<(string, string)>();
        foreach (var part in SplitTop(text))
        {
            var p = part.Trim();
            if (p.Length == 0) continue;
            int c = p.IndexOf(':');
            if (c <= 0) throw new FormatException($"expected 'name: value' but got '{p}'");
            res.Add((p[..c].Trim(), p[(c + 1)..].Trim()));
        }
        return res;
    }

    private static IEnumerable<string> SplitTop(string s)
    {
        int depth = 0, start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '[' || s[i] == '(') depth++;
            else if (s[i] == ']' || s[i] == ')') depth--;
            else if (s[i] == ',' && depth == 0) { yield return s[start..i]; start = i + 1; }
        }
        yield return s[start..];
    }

    public static uint[] Encode(CommandSpec spec, List<(string key, string val)> fields)
    {
        var words = new uint[spec.ArgCount];
        for (int w = 0; w < spec.ArgCount; w++) words[w] = w < spec.PadDefault.Length ? spec.PadDefault[w] : 0;
        var given = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in fields)
        {
            if (!given.TryAdd(k, v)) throw new FormatException($"field '{k}' given twice");
        }
        foreach (var k in given.Keys)
        {
            if (k.StartsWith("pad") && int.TryParse(k[3..], out int pw) && pw >= 0 && pw < spec.ArgCount) continue;
            if (!spec.Fields.Any(f => f.Name == k)) throw new FormatException($"{spec.Name} has no field '{k}'");
        }
        for (int w = 0; w < spec.ArgCount; w++)
        {
            uint cov = spec.CoveredMask(w);
            if (given.TryGetValue($"pad{w}", out var pv)) words[w] = (words[w] & cov) | (ParseUInt(pv) & ~cov);
        }
        foreach (var f in spec.Fields)
        {
            given.TryGetValue(f.Name, out var v);
            EncodeField(spec, f, v, words);
        }
        return words;
    }

    private static void EncodeField(CommandSpec spec, FieldSpec f, string? v, uint[] words)
    {
        switch (f.Kind)
        {
            case FieldKind.SlotList:
            {
                var items = v is null ? new List<string>() : v.Trim().TrimStart('[').TrimEnd(']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (items.Count > 4) throw new FormatException($"{f.Name}: at most 4 slots");
                uint w = 0xFFFFFFFF;
                for (int i = 0; i < items.Count; i++)
                {
                    uint b = ParseUInt(items[i]);
                    if (b > 0xFF) throw new FormatException($"{f.Name}: slot {b} out of range");
                    w = (w & ~(0xFFu << (8 * i))) | (b << (8 * i));
                }
                words[f.Word] = w;
                uint cm = ((1u << f.CountBits) - 1u) << f.CountShift;
                words[f.CountWord] = (words[f.CountWord] & ~cm) | (((uint)items.Count << f.CountShift) & cm);
                return;
            }
            case FieldKind.Variant:
                words[f.Word] = v is null ? RequireDefault(spec, f) : ParseVariant(v);
                return;
            case FieldKind.Float:
                words[f.Word] = v is null ? RequireDefault(spec, f) : ParseFloatWord(v);
                return;
        }
        uint val = v is null ? RequireDefault(spec, f) : f.Kind switch
        {
            FieldKind.Bool => ParseBool(v) ? 1u : 0u,
            FieldKind.NotBool => ParseBool(v) ? 0u : 1u,
            FieldKind.Enum => f.Names?.FirstOrDefault(kv => kv.Value == v).Value is not null ? f.Names!.First(kv => kv.Value == v).Key : ParseUInt(v),
            _ => ParseUInt(v),
        };
        if (f.Bits < 32 && val > ((1u << f.Bits) - 1u)) throw new FormatException($"{f.Name}: {val} does not fit in {f.Bits} bits");
        words[f.Word] = f.Bits >= 32 ? val : (words[f.Word] & ~f.Mask) | (val << f.Shift);
    }

    private static uint RequireDefault(CommandSpec spec, FieldSpec f)
    {
        if (!f.HasDefault) throw new FormatException($"{spec.Name}: missing field '{f.Name}'");
        return f.Default;
    }

    private static bool ParseBool(string v) => v.Trim() switch
    {
        "true" => true,
        "false" => false,
        _ => throw new FormatException($"expected true/false, got '{v}'"),
    };

    private static uint ParseUInt(string v)
    {
        v = v.Trim();
        if (v.StartsWith("raw(") && v.EndsWith(")")) v = v[4..^1].Trim();
        if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return Convert.ToUInt32(v[2..], 16);
        if (v.StartsWith('-')) return unchecked((uint)int.Parse(v, CultureInfo.InvariantCulture));
        return uint.Parse(v, CultureInfo.InvariantCulture);
    }

    private static uint ParseVariant(string v)
    {
        v = v.Trim();
        if (v.StartsWith("raw(")) return ParseUInt(v);
        if (v.StartsWith("var(") && v.EndsWith(")")) return (ParseUInt(v[4..^1]) << 3) | 1u;
        if (v.StartsWith("fvar(") && v.EndsWith(")")) return (ParseUInt(v[5..^1]) << 3) | 5u;
        if (v.EndsWith("f") && !v.StartsWith("0x"))
        {
            float fl = float.Parse(v[..^1], NumberStyles.Float, CultureInfo.InvariantCulture);
            return (BitConverter.SingleToUInt32Bits(fl) & ~7u) | 4u;
        }
        int i = int.Parse(v, CultureInfo.InvariantCulture);
        return unchecked((uint)(i << 3));
    }

    private static uint ParseFloatWord(string v)
    {
        v = v.Trim();
        if (v.StartsWith("raw(")) return ParseUInt(v);
        return BitConverter.SingleToUInt32Bits(float.Parse(v.TrimEnd('f'), NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    public static uint[]? ParseRawArgs(string name, string argText, Func<string, int, string> typeOf)
    {
        var parts = argText.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 1 && parts[0].Length == 0) return Array.Empty<uint>();
        var res = new uint[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var a = parts[i];
            if (a.StartsWith("0x")) { res[i] = Convert.ToUInt32(a[2..], 16); continue; }
            switch (typeOf(name, i))
            {
                case "int32": res[i] = unchecked((uint)int.Parse(a, CultureInfo.InvariantCulture)); break;
                case "int16": res[i] = BitConverter.ToUInt32(BitConverter.GetBytes((int)short.Parse(a, CultureInfo.InvariantCulture)), 0); break;
                case "uint32": res[i] = uint.Parse(a, CultureInfo.InvariantCulture); break;
                case "uint16": res[i] = ushort.Parse(a, CultureInfo.InvariantCulture); break;
                case "byte": res[i] = byte.Parse(a, CultureInfo.InvariantCulture); break;
                case "single": res[i] = BitConverter.SingleToUInt32Bits(float.Parse(a, NumberStyles.Float, CultureInfo.InvariantCulture)); break;
                default: return null;
            }
        }
        return res;
    }

    private static Dictionary<string, List<string>>? _argTypes;

    public static string TypeOf(string name, int i)
    {
        _argTypes ??= PS2BehaviourGraph.GetAgentLabDefs().CommandMap.Values
            .GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.First().Arguments.Select(a => a.ToLowerInvariant()).ToList());
        return _argTypes.TryGetValue(name, out var l) && i < l.Count ? l[i] : "hex";
    }

    public static string ToReadable(string rawText) => ToReadable(rawText, TypeOf, out _, out _);

    public static string ToReadable(string rawText, Func<string, int, string> typeOf, out int converted, out int kept)
    {
        converted = 0; kept = 0;
        var sb = new StringBuilder();
        foreach (var line in SplitLines(rawText))
        {
            var m = RawLine.Match(line);
            if (m.Success && _byName.TryGetValue(m.Groups[2].Value, out var spec))
            {
                var args = ParseRawArgs(spec.Name, m.Groups[3].Value, typeOf);
                string? r = args is null ? null : Decode(spec, args);
                if (r is not null) { sb.Append(m.Groups[1].Value).Append(spec.Name).Append('(').Append(r).Append(");\n"); converted++; continue; }
                kept++;
            }
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    public static string FromReadable(string text)
    {
        var sb = new StringBuilder();
        int lineNo = 0;
        foreach (var line in SplitLines(text))
        {
            lineNo++;
            var m = ReadLine.Match(line);
            if (m.Success && _byName.TryGetValue(m.Groups[2].Value, out var spec))
            {
                uint[] words;
                try { words = Encode(spec, ParseFields(m.Groups[3].Value)); }
                catch (Exception ex) { throw new FormatException($"line {lineNo}: {ex.Message}"); }
                sb.Append(m.Groups[1].Value).Append("@PS2 ").Append(spec.Name).Append('(')
                  .Append(string.Join(", ", words.Select(x => $"0x{x:X8}"))).Append(");\n");
                continue;
            }
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    private static IEnumerable<string> SplitLines(string s)
    {
        var lines = s.Replace("\r\n", "\n").Split('\n');
        int n = lines.Length;
        if (n > 0 && lines[n - 1].Length == 0) n--;
        for (int i = 0; i < n; i++) yield return lines[i];
    }
}
