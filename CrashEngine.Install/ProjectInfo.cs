using System.Text.Json;
using CrashEngine.Core;

namespace CrashEngine.Install;

// Amedo 2026-11-12
public sealed record ProjectInfo(string Name, string Folder, string Tson, string? Disc, string Region, DateTime Modified, bool Completed)
{
    public static ProjectInfo? Read(string folderOrTson)
    {
        string? tson = folderOrTson.EndsWith(".tson", StringComparison.OrdinalIgnoreCase) && File.Exists(folderOrTson) ? folderOrTson
            : Directory.Exists(folderOrTson) ? Directory.GetFiles(folderOrTson, "*.tson").FirstOrDefault() : null;
        if (tson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(tson));
            var root = doc.RootElement;
            string name = root.TryGetProperty("Name", out var n) && n.GetString() is { Length: > 0 } s ? s : Path.GetFileNameWithoutExtension(tson);
            string? disc = root.TryGetProperty("DiscContentPathPS2", out var d) ? d.GetString() : null;
            bool completed = !root.TryGetProperty("Completed", out var c) || c.ValueKind != JsonValueKind.False;
            return new ProjectInfo(name, Path.GetDirectoryName(tson)!, tson, disc, RegionOf(disc), File.GetLastWriteTime(tson), completed);
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    public static string RegionOf(string? disc)
    {
        if (disc is null || !Directory.Exists(disc)) return "";
        foreach (var known in GameDiscs.Known)
            if (File.Exists(GamePaths.FindOnDisk(disc, known.Exe))) return known.Region; // Amedo 2026-11-20
        return "";
    }
}
