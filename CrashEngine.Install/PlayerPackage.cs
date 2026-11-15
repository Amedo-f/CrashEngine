using System.Diagnostics;
using CrashEngine.Platform;

namespace CrashEngine.Install;

// Amedo 2026-11-12
public static class PlayerPackage
{
    public const string PlayerExe = "CrashGame";

    public static string ShippedFolder => Path.Combine(AppContext.BaseDirectory, "Player");

    public static bool HasShipped => File.Exists(Path.Combine(ShippedFolder, PlatformInfo.ExeFile(PlayerExe)));

    public static string? FindCrashGameProject()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "CrashGame", "CrashGame.csproj");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static void Copy(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(from, file);
            if (rel.Equals("game.json", StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(to, rel), overwrite: true);
        }
    }

    public static (int ExitCode, string[] Errors) Publish(string csproj, string outDir)
    {
        var psi = new ProcessStartInfo("dotnet",
            $"publish \"{csproj}\" -c Release -r {PlatformInfo.Rid} --self-contained true -p:PublishSingleFile=false -o \"{outDir}\"")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        var errors = new List<string>();
        using var process = Process.Start(psi)!;
        process.OutputDataReceived += (_, e) => { if (e.Data is { } line && line.Contains("error", StringComparison.OrdinalIgnoreCase)) lock (errors) errors.Add(line.Trim()); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 } line) lock (errors) errors.Add(line.Trim()); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        lock (errors) return (process.ExitCode, errors.Distinct().ToArray());
    }

    public static void NameExe(string outDir, string exeName)
    {
        var exe = Path.Combine(outDir, PlatformInfo.ExeFile(exeName));
        if (exeName != PlayerExe) File.Move(Path.Combine(outDir, PlatformInfo.ExeFile(PlayerExe)), exe, overwrite: true);
        PlatformInfo.MakeExecutable(exe);
    }
}
