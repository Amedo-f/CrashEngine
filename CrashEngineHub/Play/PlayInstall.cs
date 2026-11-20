using System.Text.Json;
using System.Text.Json.Nodes;
using CrashEngine.Install;
using CrashEngine.Platform;

namespace CrashEngineHub;

// Amedo 2026-11-12
public static class PlayInstall
{
    public const string GameName = "Crash Twinsanity";
    public const string GameFolder = "game";
    public static readonly string DiscFolder = Path.Combine("game", "disc", "ps2"); // Amedo 2026-11-20

    public sealed record Request(string Iso, GameDiscs.Disc Disc, string InstallFolder, string ShortcutFolder, string? Language, ModCard? Mod);

    public static string Target(string installFolder) => Path.Combine(installFolder, GameName);

    public static string Exe(string installFolder) => Path.Combine(Target(installFolder), PlatformInfo.ExeFile(GameName));

    public static long DiscBytes(string iso) => File.Exists(iso) ? new FileInfo(iso).Length : 0;

    // Amedo 2026-11-20
    public static bool GameRunning(string installFolder)
    {
        string target = Path.GetFullPath(Target(installFolder)) + Path.DirectorySeparatorChar;
        foreach (var name in new[] { GameName, "CrashGame" })
            foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                using (p)
                {
                    try
                    {
                        if (p.MainModule?.FileName is { } file && file.StartsWith(target, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    catch (Exception) { }
                }
        return false;
    }

    private const string HubMark = ".crashengine_hub";

    public static bool ForeignFolder(string installFolder)
    {
        var target = Target(installFolder);
        return Directory.Exists(target) && !File.Exists(Path.Combine(target, HubMark)) && Directory.EnumerateFileSystemEntries(target).Any();
    }

    public static string? CheckSpace(string iso, string installFolder)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(installFolder));
            if (root is null) return null;
            long need = DiscBytes(iso) + 400L * 1024 * 1024, free = new DriveInfo(root).AvailableFreeSpace;
            return free < need ? $"Not enough space on {root}: needs about {need / 1e9:0.0} GB, {free / 1e9:0.0} GB free." : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException) { return null; }
    }

    public static void Run(Request r, Action<string, float> progress)
    {
        var target = Target(r.InstallFolder);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, HubMark), "");
        var disc = Path.Combine(target, DiscFolder);
        if (!SameDisc(disc, r.Disc))
        {
            using var iso = File.OpenRead(r.Iso);
            GameDiscs.UnpackTo(iso, r.Disc, disc, p => progress($"Copying the disc ({r.Disc.Region})...", p));
        }
        progress("Copying the game...", 0f);
        CopyPlayer(target, progress);
        string project = GameFolder;
        if (r.Mod is null)
        {
            var tson = Path.Combine(target, GameFolder, GameName + ".tson");
            if (!File.Exists(tson)) GameContent.WriteProject(Path.Combine(target, GameFolder), null);
            SetLanguage(Path.Combine(target, GameFolder), r.Language);
        }
        else
        {
            project = Path.Combine("mods", string.Concat(r.Mod.Name.Split(Path.GetInvalidFileNameChars())));
            var folder = Path.Combine(target, project);
            using (var mod = File.OpenRead(r.Mod.File)) Mods.Extract(mod, folder, p => progress($"Installing {r.Mod.Name}...", p));
            GameContent.PointModAtDisc(folder, DiscFolder);
            SetLanguage(folder, r.Language);
        }
        WriteGameJson(target, project);
        WriteGraphicsJson(target);
        progress("Making the shortcut...", 1f);
        PlatformFiles.CreateShortcut(r.ShortcutFolder, GameName, Exe(r.InstallFolder), target); // Amedo 2026-11-20
    }

    private static bool SameDisc(string folder, GameDiscs.Disc disc) =>
        GameDiscs.IsReady(folder) && File.ReadAllText(Path.Combine(folder, ".ready")).Trim() == disc.ExeSha256;

    private static void CopyPlayer(string target, Action<string, float> progress)
    {
        if (PlayerPackage.HasShipped) PlayerPackage.Copy(PlayerPackage.ShippedFolder, target);
        else if (PlayerPackage.FindCrashGameProject() is { } csproj)
        {
            progress("Building the game (first time, this takes a minute)...", 0.5f);
            var (exit, errors) = PlayerPackage.Publish(csproj, target);
            if (exit != 0) throw new InvalidOperationException($"Building the game failed: {string.Join(" | ", errors.Take(3))}");
        }
        else throw new InvalidOperationException("The game player (Player folder) is missing next to the Hub.");
        PlayerPackage.NameExe(target, GameName);
    }

    private static void SetLanguage(string projectFolder, string? language)
    {
        foreach (var tson in Directory.GetFiles(projectFolder, "*.tson"))
        {
            var json = JsonNode.Parse(File.ReadAllText(tson))!.AsObject();
            if (language is null) json.Remove("DiscLanguage");
            else json["DiscLanguage"] = language;
            File.WriteAllText(tson, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static void WriteGameJson(string target, string project)
    {
        var file = Path.Combine(target, "game.json");
        JsonObject json;
        try { json = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file))!.AsObject() : new JsonObject(); }
        catch (JsonException) { json = new JsonObject(); }
        json["project"] = project;
        json.Remove("level");
        json.TryAdd("windowed", false);
        json.TryAdd("volume", 1.0);
        File.WriteAllText(file, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public const int PlayerSharpness = 5;

    // Amedo 2026-11-12
    private static void WriteGraphicsJson(string target)
    {
        var file = Path.Combine(target, "graphics.json");
        if (File.Exists(file)) return;
        File.WriteAllText(file, new JsonObject { ["Sharpness"] = PlayerSharpness }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
