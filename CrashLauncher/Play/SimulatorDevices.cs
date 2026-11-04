using System.Text.Json;

namespace CrashLauncher;

// Amedo 2026-10-26
public sealed record SimulatorCutout(string Type, float X, float Y, float W, float H);

// Amedo 2026-10-26
public sealed record SimulatorDevice(string Name, int Width, int Height, float Corner, SimulatorCutout? Cutout, float SafeTop, float SafeBottom);

// Amedo 2026-10-26
public sealed record SimulatorDeviceList(int Version, List<SimulatorDevice> Devices)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Assets", "Devices", "devices.json");

    public static SimulatorDeviceList Load() =>
        JsonSerializer.Deserialize<SimulatorDeviceList>(File.ReadAllText(FilePath), Json) ?? throw new InvalidDataException(FilePath);
}
