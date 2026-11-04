using System.Globalization;
using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime;

// Amedo
public sealed class InputTape : IDisposable
{
    public const string Magic = "# CrashEngine input tape 1";
    private const int HashEvery = 60;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static InputTape? Current { get; set; }

    public string Path { get; }
    public bool Replaying { get; }
    public long Step { get; private set; } = -1;
    public long Length { get; private set; }
    public string? Level { get; private set; }
    public IReadOnlyDictionary<string, string> Header => _header;
    public string? Divergence { get; private set; }
    public int HashChecks { get; private set; }
    public Action<string>? Log { get; set; }

    private readonly Dictionary<string, string> _header = new();
    private readonly StreamWriter? _writer;
    private readonly byte[] _pad = new byte[17];
    private readonly byte[] _lastPad = new byte[17];
    private bool _padWritten;
    private string? _lastViewer, _lastCamera, _lastControl = "";
    private uint _rngAfterStep;
    private bool _rngKnown, _movie;

    private readonly List<(long Step, char Kind, string[] Args)> _lines = new();
    private int _cursor;
    private readonly List<string> _finishes = new();
    private bool _replayMovie;
    private byte[]? _replayPad;
    private float[]? _replayViewer, _replayCamera; // Amedo
    private bool? _replayShows; // Amedo
    private int _lastShows = -1; // Amedo
    private string? _expectedHash;
    private readonly List<string> _expectedDetail = new(); // Amedo
    private string? _expectedPlayer; // Amedo
    public string? PlayerDivergence { get; private set; } // Amedo

    private InputTape(string path, StreamWriter writer)
    {
        Path = path;
        _writer = writer;
    }

    private InputTape(string path)
    {
        Path = path;
        Replaying = true;
        foreach (var raw in File.ReadLines(path))
        {
            if (raw.Length == 0) continue;
            if (raw[0] == '#')
            {
                var kv = raw.TrimStart('#', ' ').Split(' ', 2);
                if (kv.Length == 2) _header[kv[0]] = kv[1];
                continue;
            }
            var parts = raw.Split(' ');
            if (parts.Length < 2 || !long.TryParse(parts[1], NumberStyles.Integer, Inv, out long step)) continue;
            _lines.Add((step, parts[0][0], parts[2..]));
            Length = Math.Max(Length, step);
        }
        Level = _header.GetValueOrDefault("level");
        for (; _cursor < _lines.Count && _lines[_cursor].Step < 0; _cursor++)
            if (_lines[_cursor].Kind == 's') _finishes.Add(string.Join(' ', _lines[_cursor].Args));
    }

    // Amedo
    public static InputTape StartRecording(string path, string level, string host, IEnumerable<(string Key, string Value)>? extra = null)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var writer = new StreamWriter(path) { AutoFlush = false };
        writer.WriteLine(Magic);
        writer.WriteLine($"# level {level}");
        writer.WriteLine($"# host {host}");
        writer.WriteLine($"# created {DateTime.UtcNow.ToString("o", Inv)}");
        writer.WriteLine($"# logicticks {SceneSimulation.LogicTicks}");
        writer.WriteLine($"# widescreen {(Screen.Oleg.OverlayQueue.Widescreen_30A744 ? 1 : 0)}"); // Amedo
        foreach (var (k, v) in extra ?? []) writer.WriteLine($"# {k} {v}");
        var tape = new InputTape(path, writer) { Level = level };
        tape._header["level"] = level;
        return tape;
    }

    // Amedo
    public static InputTape OpenReplay(string path)
    {
        using (var r = new StreamReader(path))
            if (r.ReadLine() != Magic) throw new InvalidDataException($"{path} is not an input tape");
        return new InputTape(path);
    }

    public static string DefaultFolder =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrashEngineRecords", "inputs");

    public static string NewPath(string level) =>
        System.IO.Path.Combine(DefaultFolder, $"input_{System.IO.Path.GetFileName(level)}_{DateTime.Now:yyyyMMdd_HHmmss}.tape");

    public bool Done => Replaying && Step >= Length;

    // Amedo
    internal void BeginStep(SceneSimulation sim)
    {
        Step++;
        if (Replaying) Apply(sim);
        else Record(sim);
    }

    private void Record(SceneSimulation sim)
    {
        var w = _writer!;
        PackPad(sim.Pad, _pad);
        if (!_padWritten || !_pad.AsSpan().SequenceEqual(_lastPad))
        {
            w.WriteLine($"p {Step} {Convert.ToHexString(_pad)}");
            _pad.CopyTo(_lastPad, 0);
            _padWritten = true;
        }
        string? viewer = sim.HostViewer is { } hv ? Floats(M(hv.ViewProjection).Append(hv.Position.X).Append(hv.Position.Y).Append(hv.Position.Z)) : null;
        if (viewer is not null && viewer != _lastViewer) w.WriteLine($"v {Step} {viewer}");
        _lastViewer = viewer;
        string? camera = sim.HostCamera is { } hc ? Floats(B(hc.Right, hc.Up, hc.Forward)) : null;
        if (camera is not null && camera != _lastCamera) w.WriteLine($"c {Step} {camera}");
        _lastCamera = camera;
        int shows = sim.HostShowsGameCamera ? 1 : 0; // Amedo
        if (shows != _lastShows) w.WriteLine($"g {Step} {shows}");
        _lastShows = shows;
        string control = sim.ControlBasis is { } cb ? Floats(B(cb.Right, cb.Up, cb.Forward)) : "-";
        if (control != _lastControl) w.WriteLine($"k {Step} {control}");
        _lastControl = control;
        uint rng = GameRandom.Shared.State;
        if (!_rngKnown || rng != _rngAfterStep) w.WriteLine($"r {Step} {rng:X8}");
        _rngKnown = true;
    }

    private void Apply(SceneSimulation sim)
    {
        _finishes.Clear();
        for (; _cursor < _lines.Count && _lines[_cursor].Step < Step; _cursor++)
            if (Divergence is null && _lines[_cursor].Kind != 'e')
                Log?.Invoke($"[tape] step {Step}: skipped a recorded '{_lines[_cursor].Kind}' of step {_lines[_cursor].Step}");
        for (; _cursor < _lines.Count && _lines[_cursor].Step == Step; _cursor++)
        {
            var (_, kind, a) = _lines[_cursor];
            switch (kind)
            {
                case 'p': _replayPad = Convert.FromHexString(a[0]); break;
                case 'v': _replayViewer = Parse(a); break; // Amedo
                case 'c': _replayCamera = Parse(a); break; // Amedo
                case 'g': _replayShows = a[0] == "1"; break; // Amedo
                case 'k':
                    if (a[0] == "-") sim.ApplyTapeControl(null);
                    else
                    {
                        var k = Parse(a);
                        sim.ApplyTapeControl((new Vector3(k[0], k[1], k[2]), new Vector3(k[3], k[4], k[5]), new Vector3(k[6], k[7], k[8])));
                    }
                    break;
                case 'r': GameRandom.Shared.State = Convert.ToUInt32(a[0], 16); break;
                case 'm': _replayMovie = a[0] == "1"; break;
                case 's': _finishes.Add(string.Join(' ', a)); break;
                case 'h': _expectedHash = a[0]; break;
                case 'x': _expectedPlayer = string.Join(' ', a); break; // Amedo
                case 'd':
                case 'a': _expectedDetail.Add(kind + " " + string.Join(' ', a)); break; // Amedo
            }
        }
        if (_replayPad is not null) UnpackPad(_replayPad, sim.Pad);
        if (_replayViewer is { } f) // Amedo
            sim.ApplyTapeViewer(new Matrix4x4(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10], f[11], f[12], f[13], f[14], f[15]),
                                new Vector3(f[16], f[17], f[18]));
        if (_replayCamera is { } c) sim.ApplyTapeCamera(new Vector3(c[0], c[1], c[2]), new Vector3(c[3], c[4], c[5]), new Vector3(c[6], c[7], c[8]), _replayShows); // Amedo
    }

    // Amedo
    internal void EndStep(SceneSimulation sim)
    {
        if (Replaying)
        {
            bool diverged = Divergence is not null;
            if (_expectedPlayer is { } player) CheckPlayer(sim, player); // Amedo
            _expectedPlayer = null;
            if (_expectedHash is { } expected) CheckHash(sim, expected);
            _expectedHash = null;
            if (_expectedDetail.Count > 0)
            {
                if (Step == 0 || (!diverged && Divergence is not null)) CompareDetail(sim); // Amedo
                else _expectedDetail.Clear();
            }
            return;
        }
        _rngAfterStep = GameRandom.Shared.State;
        if (PlayerLine(sim) is { } x) _writer!.WriteLine($"x {Step} {x}"); // Amedo
        if (Step % HashEvery == 0)
        {
            _writer!.WriteLine($"h {Step} {StateHash(sim):X16}");
            foreach (var line in Detail(sim)) _writer.WriteLine($"{line[0]} {Step} {line[2..]}"); // Amedo
            _writer.Flush();
        }
    }

    private void CheckHash(SceneSimulation sim, string expected)
    {
        HashChecks++;
        string now = StateHash(sim).ToString("X16");
        if (now == expected || Divergence is not null) return;
        Divergence = $"step {Step} (t {Step * (double)SceneSimulation.LogicTicks / 9000.0:0.00}s): state hash {now}, recorded {expected}";
        Log?.Invoke($"[tape] replay DIVERGED at {Divergence}");
    }

    // Amedo
    private static readonly string[] PlayerFields = ["rng", "x", "y", "z", "m31", "m33", "vx", "vy", "vz", "camRx", "camRz", "camFx", "camFz", "buttons", "behaviour"];

    // Amedo
    private static string? PlayerLine(SceneSimulation sim)
    {
        var world = sim.World;
        if (world.Player is not { } p || p.Destroyed) return null;
        static string F(float f) => BitConverter.SingleToUInt32Bits(f).ToString("X8");
        var m = p.GameMatrix;
        var c = world.PlayerCharacter;
        var v = c?.Velocity ?? default;
        var r = c?.CameraRight ?? default;
        var f = c?.CameraForward ?? default;
        return string.Join(' ', GameRandom.Shared.State.ToString("X8"), F(m.M41), F(m.M42), F(m.M43), F(m.M31), F(m.M33), F(v.X), F(v.Y), F(v.Z),
                           F(r.X), F(r.Z), F(f.X), F(f.Z), sim.Pad.Buttons_00.ToString("X6"), (p.Behaviour?.GetID() ?? 0xFFFFFFFFu).ToString("X"));
    }

    // Amedo
    private void CheckPlayer(SceneSimulation sim, string expected)
    {
        if (PlayerDivergence is not null || PlayerLine(sim) is not { } now || now == expected) return;
        var a = expected.Split(' ');
        var b = now.Split(' ');
        var fields = Enumerable.Range(0, Math.Min(a.Length, b.Length)).Where(i => a[i] != b[i])
            .Select(i => $"{(i < PlayerFields.Length ? PlayerFields[i] : i.ToString(Inv))} {a[i]}/{b[i]}");
        PlayerDivergence = $"step {Step}: {string.Join(", ", fields)}";
        Log?.Invoke($"[tape] player first differs at {PlayerDivergence} (recorded/here)");
    }

    // Amedo
    private static IEnumerable<string> Detail(SceneSimulation sim)
    {
        var world = sim.World;
        yield return string.Create(Inv, $"d frame {world.Frame} ticks {world.NowTicks} agents {world.Agents.Count} rng {GameRandom.Shared.State:X8}");
        for (int i = 0; i < world.Agents.Count; i++)
        {
            var agent = world.Agents[i];
            if (agent.Destroyed) continue;
            var g = agent.GamePosition;
            yield return string.Create(Inv, $"a {i} {agent.Entity.Name.Replace(' ', '_')} {BitConverter.SingleToUInt32Bits(g.X):X8} {BitConverter.SingleToUInt32Bits(g.Y):X8} {BitConverter.SingleToUInt32Bits(g.Z):X8} {agent.Behaviour?.GetID() ?? 0xFFFFFFFFu:X}");
        }
    }

    // Amedo
    private void CompareDetail(SceneSimulation sim)
    {
        var now = Detail(sim).ToList();
        int shown = 0;
        if (now[0] != _expectedDetail[0]) Log?.Invoke($"[tape] step {Step}: recorded {_expectedDetail[0]} | here {now[0]}");
        var recorded = Keyed(_expectedDetail.Skip(1));
        var here = Keyed(now.Skip(1));
        foreach (var (name, line) in recorded)
            if (shown < 40 && (!here.TryGetValue(name, out var mine) || Tail(mine) != Tail(line)))
            {
                shown++;
                Log?.Invoke($"[tape] step {Step}: recorded {line} | here {(mine is null ? "missing" : mine)}");
            }
        foreach (var (name, line) in here)
            if (shown < 40 && !recorded.ContainsKey(name)) { shown++; Log?.Invoke($"[tape] step {Step}: extra here {line}"); }
        Log?.Invoke($"[tape] step {Step}: {recorded.Count} recorded agents, {here.Count} here, {shown} difference(s) shown");
        _expectedDetail.Clear();
        static string Tail(string l) => string.Join(' ', l.Split(' ').Skip(3));
        static Dictionary<string, string> Keyed(IEnumerable<string> lines) // Amedo
        {
            var keyed = new Dictionary<string, string>();
            foreach (var l in lines)
            {
                string name = l.Split(' ')[2], key = name;
                for (int n = 2; keyed.ContainsKey(key); n++) key = name + "#" + n;
                keyed[key] = l;
            }
            return keyed;
        }
    }

    // Amedo
    private static ulong StateHash(SceneSimulation sim)
    {
        var world = sim.World;
        ulong h = 1469598103934665603UL;
        void Mix(ulong v) { h ^= v; h *= 1099511628211UL; }
        Mix(GameRandom.Shared.State);
        Mix((ulong)world.Frame);
        Mix((ulong)world.NowTicks);
        Mix((ulong)world.Agents.Count);
        foreach (var agent in world.Agents)
        {
            if (agent.Destroyed) continue;
            var g = agent.GamePosition;
            Mix(BitConverter.SingleToUInt32Bits(g.X));
            Mix(BitConverter.SingleToUInt32Bits(g.Y));
            Mix(BitConverter.SingleToUInt32Bits(g.Z));
            Mix(agent.Behaviour?.GetID() ?? 0xFFFFFFFFu);
        }
        return h;
    }

    // Amedo
    internal bool MovieGate(bool playing)
    {
        if (!Replaying)
        {
            if (playing != _movie) _writer!.WriteLine($"m {Step} {(playing ? 1 : 0)}");
            _movie = playing;
            return playing;
        }
        return _replayMovie;
    }

    // Amedo
    internal void Finished(string name)
    {
        if (!Replaying) _writer!.WriteLine($"s {Step} {name}");
    }

    internal IReadOnlyList<string> FinishesThisStep => _finishes;

    // Amedo
    public void Note(char kind, string text)
    {
        if (!Replaying) _writer!.WriteLine($"{kind} {Step} {text}");
    }

    public void Dispose()
    {
        if (_writer is null) return;
        _writer.WriteLine($"e {Step}");
        _writer.Dispose();
    }

    private static void PackPad(Character.CharacterPad pad, byte[] o)
    {
        Array.Copy(pad.Pressure, o, 12);
        o[12] = pad.LeftX; o[13] = pad.LeftY; o[14] = pad.RightX; o[15] = pad.RightY;
        o[16] = (byte)((pad.L3 ? 1 : 0) | (pad.R3 ? 2 : 0) | (pad.Start ? 4 : 0) | (pad.Select ? 8 : 0));
    }

    private static void UnpackPad(byte[] b, Character.CharacterPad pad)
    {
        Array.Copy(b, pad.Pressure, 12);
        pad.LeftX = b[12]; pad.LeftY = b[13]; pad.RightX = b[14]; pad.RightY = b[15];
        pad.L3 = (b[16] & 1) != 0; pad.R3 = (b[16] & 2) != 0; pad.Start = (b[16] & 4) != 0; pad.Select = (b[16] & 8) != 0;
    }

    private static IEnumerable<float> M(Matrix4x4 m) =>
        [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];

    private static IEnumerable<float> B(in Vector3 r, in Vector3 u, in Vector3 f) => [r.X, r.Y, r.Z, u.X, u.Y, u.Z, f.X, f.Y, f.Z];

    private static string Floats(IEnumerable<float> v) => string.Join(' ', v.Select(x => BitConverter.SingleToUInt32Bits(x).ToString("X8")));

    private static float[] Parse(string[] a) => a.Select(s => BitConverter.UInt32BitsToSingle(Convert.ToUInt32(s, 16))).ToArray();
}
