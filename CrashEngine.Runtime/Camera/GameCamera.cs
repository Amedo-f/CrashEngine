using System.Numerics;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public const uint TimeStruct = 0x323530;
    public const uint TicksPerSecond = 0x3097DC;
    public const uint SecondsPerTick = 0x3097E0;

    public CamMemory M { get; }
    public Dictionary<string, int> Unimplemented { get; } = new();
    public Action<string>? Trace { get; set; }
    public Action<string>? Log { get; set; }
    public Dictionary<uint, SubCamera> SubCameras { get; } = new(); // Amedo 2026-09-29

    private SubCamera? Sub(uint a) => a != 0 && SubCameras.TryGetValue(a, out var s) ? s : null;

    public GameCamera(CamMemory memory)
    {
        M = memory;
        MapConstants();
    }

    public void Missing(string what)
    {
        if (Unimplemented.TryGetValue(what, out int n)) { Unimplemented[what] = n + 1; return; }
        Unimplemented[what] = 1;
        Log?.Invoke($"[camera] not implemented yet: {what}");
    }

    public uint Now => M.U(TimeStruct + 4);

    public void SetTime(uint nowTicks, uint dtTicks)
    {
        M.B(TimeStruct, 1);
        M.U(TimeStruct + 4, nowTicks);
        M.U(TimeStruct + 8, dtTicks);
    }

    private void MapConstants()
    {
        if (!M.IsMapped(TimeStruct)) M.Map(TimeStruct, 0x10, "time");
        if (!M.IsMapped(0x3097D0))
        {
            M.Map(0x3097D0, 0x20, "time constants");
            M.F(TicksPerSecond, 9000f);
            M.F(SecondsPerTick, 1f / 9000f);
        }
        if (!M.IsMapped(0x30A1B0))
        {
            M.Map(0x30A1B0, 0xB0, "camera constants");
            float[] f = [0.01f, 0.01f, 2f];
            for (int i = 0; i < f.Length; i++) M.F(0x30A1B0 + (uint)i * 4, f[i]);
            (uint A, int V)[] angles =
            [
                (0x30A1C8, 0x8000), (0x30A1D0, 0x8000), (0x30A1D8, 0x2AAA), (0x30A1E0, 0x5555), (0x30A1E8, -0x2000),
                (0x30A1F0, 0x3555), (0x30A1F8, 0x4FA), (0x30A200, 0x8000), (0x30A208, 0x4000), (0x30A210, 0x71C),
                (0x30A218, 0x238E), (0x30A220, 0x11C7), (0x30A228, 0x8E38), (0x30A230, 0x7C71), (0x30A238, 0x8000),
                (0x30A240, 0x3555), (0x30A248, 0x2AAA), (0x30A250, 0x1C71),
            ];
            foreach (var (a, v) in angles) M.I(a, v);
            M.F(0x30A258, 0.01f);
            M.F(0x30A25C, 0.01f);
        }
        if (!M.IsMapped(0x30A440))
        {
            M.Map(0x30A440, 0x20, "camera constants 2");
            M.I(0x30A440, 1);
            M.I(0x30A448, 0x1FFF);
            M.I(0x30A450, 0x1FFF);
        }
        if (!M.IsMapped(0x30A620))
        {
            M.Map(0x30A620, 0x60, "camera constants 3");
            M.I(0x30A628, 18);
            M.I(0x30A630, 0x6000);
            M.I(0x30A638, -0x6000);
            M.I(0x30A640, 0x4000);
            M.I(0x30A648, 0x71C);
            M.I(0x30A658, 0xAAA);
            M.I(0x30A660, 0x20000);
            M.I(0x30A668, 0x4000);
        }
        if (!M.IsMapped(0x30B7B0))
        {
            M.Map(0x30B7B0, 0x40, "camera offsets");
            M.V(0x30B7B0, new Vector4(0f, 1.5f, 0f, 1f));
            M.V(0x30B7C0, new Vector4(0f, 3.5f, 0f, 1f));
            M.V(0x30B7D0, new Vector4(0f, 0f, -1f, 1f));
            M.V(0x30B7E0, new Vector4(0f, 11f, 0f, 1f));
        }
        if (!M.IsMapped(0x3234D0))
        {
            M.Map(0x3234D0, 0x10, "zero point");
            M.V(0x3234D0, new Vector4(0f, 0f, 0f, 1f));
        }
        if (!M.IsMapped(0x323510))
        {
            M.Map(0x323510, 0x10, "up axis");
            M.V(0x323510, new Vector4(0f, 1f, 0f, 1f));
        }
        if (!M.IsMapped(0x3BD7B0)) M.Map(0x3BD7B0, 0xA0, "camera shake");
    }
}
