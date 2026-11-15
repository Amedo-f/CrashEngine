using System.IO.Compression;
using System.Text.Json;

namespace CrashEngine.Install;

// Amedo 2026-11-12
public sealed record ModCard(string Name, string Region, string File);

// Amedo 2026-11-12
public static class Mods
{
    public const string Extension = ".cemod";

    public static List<ModCard> List(string folder)
    {
        var list = new List<ModCard>();
        if (!Directory.Exists(folder)) return list;
        foreach (var file in Directory.GetFiles(folder, "*" + Extension).Order())
            if (ReadCard(file) is { } card) list.Add(card);
        return list;
    }

    public static ModCard? ReadCard(string file)
    {
        try
        {
            using var zip = ZipFile.OpenRead(file);
            using var card = zip.GetEntry("mod.json")?.Open();
            if (card is null) return null;
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(card);
            string name = json?.GetValueOrDefault("Name").GetString() ?? Path.GetFileNameWithoutExtension(file);
            string region = json?.GetValueOrDefault("Disc").GetString() ?? "";
            return new ModCard(name, region, file);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[tool] {file} is not a mod: {ex.Message}");
            return null;
        }
    }

    public static ModCard Add(Stream picked, string folder)
    {
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, "adding.tmp");
        using (var file = File.Create(temp)) picked.CopyTo(file);
        var card = ReadCard(temp);
        if (card is null)
        {
            File.Delete(temp);
            throw new InvalidOperationException("This file is not a Crash Engine mod (.cemod).");
        }
        var target = Path.Combine(folder, string.Concat(card.Name.Split(Path.GetInvalidFileNameChars())) + Extension);
        File.Move(temp, target, overwrite: true);
        return card with { File = target };
    }

    public static void Remove(ModCard mod) => File.Delete(mod.File);

    public static void Extract(Stream mod, string folder, Action<float> progress)
    {
        Directory.CreateDirectory(folder);
        using var zip = new ZipArchive(mod, ZipArchiveMode.Read, leaveOpen: true);
        long total = Math.Max(1, zip.Entries.Sum(e => e.Length)), done = 0;
        foreach (var entry in zip.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(folder, entry.FullName));
            if (!target.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar)) continue;
            if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
            done += entry.Length;
            progress((float)done / total);
        }
    }
}
