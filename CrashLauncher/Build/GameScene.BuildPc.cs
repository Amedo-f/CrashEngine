using System.Diagnostics;
using System.Text.Json;
using CrashEngine.Importer;

namespace CrashLauncher;

// Amedo 2026-09-30
public sealed partial class GameScene
{
    private volatile bool _buildingPc;

    // Amedo 2026-09-30
    private static string? FindCrashGameProject()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "CrashGame", "CrashGame.csproj");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // Amedo
    private string ProjectRoot() => Path.GetDirectoryName(_scriptOut) ?? _extractedRoot;

    // Amedo
    private void LaunchPlayer()
    {
        if (_playerProcess is { HasExited: false }) { _browser.Log("Launch Player: already running."); return; }
        if (_buildingPc) { _browser.Log("Launch Player: a build is running, wait for it to finish."); return; }
        var outDir = Path.Combine(ProjectRoot(), "LaunchPlayer");
        if (!File.Exists(Path.Combine(outDir, "CrashGame.exe")) || PlayerIsStale(outDir))
        {
            _browser.Log($"Launch Player: preparing the test copy of the game in {outDir} (Build\\PC is not touched)...");
            BuildGameInto(outDir, "Launch Player", StartLaunchedGame);
            return;
        }
        if (Roots.Any(r => r.Has<ChunkSource>()) && !SaveChunk(confirm: false))
        {
            _browser.Log("Launch Player: stopped, the open level could not be saved (the game plays the saved chunks).");
            return;
        }
        try { WriteGameConfig(outDir, ProjectRoot(), Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2Path); }
        catch (Exception ex) { _browser.Log($"Launch Player failed: {ex.Message}"); return; }
        StartLaunchedGame(outDir);
    }

    // Amedo
    private static bool PlayerIsStale(string outDir)
    {
        static bool Newer(string source, string built) =>
            !File.Exists(built) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(built);
        var csproj = FindCrashGameProject();
        if (csproj is null)
        {
            var player = Path.Combine(AppContext.BaseDirectory, "Player");
            return Directory.Exists(player) && Directory.GetFiles(player, "*.dll")
                .Any(f => Newer(f, Path.Combine(outDir, Path.GetFileName(f))));
        }
        var engine = Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
            .Where(f => Path.GetFileName(f).StartsWith("CrashEngine", StringComparison.OrdinalIgnoreCase)
                     || Path.GetFileName(f).StartsWith("Twinsanity", StringComparison.OrdinalIgnoreCase));
        if (engine.Select(f => (f, built: Path.Combine(outDir, Path.GetFileName(f))))
                  .Any(p => File.Exists(p.built) && Newer(p.f, p.built))) return true;
        var builtGame = Path.Combine(outDir, "CrashGame.dll");
        return Directory.GetFiles(Path.GetDirectoryName(csproj)!, "*.cs").Append(csproj).Any(f => Newer(f, builtGame));
    }

    // Amedo
    private void StartLaunchedGame(string outDir)
    {
        var exe = Path.Combine(outDir, "CrashGame.exe");
        try
        {
            _playerProcess = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = outDir, UseShellExecute = false });
            _browser.Log($"Launch Player: started {exe} (pid {_playerProcess?.Id}; Esc quits).");
        }
        catch (Exception ex)
        {
            _browser.Log("Launch Player: failed to start -- " + ex.Message);
        }
    }

    // Amedo 2026-09-30
    private void BuildPc() => BuildGameInto(Path.Combine(ProjectRoot(), "Build", "PC"), "Build to PC", null); // Amedo

    // Amedo
    private void BuildGameInto(string outDir, string label, Action<string>? then)
    {
        if (_buildingPc) return;
        if (Roots.Any(r => r.Has<ChunkSource>()) && !SaveChunk(confirm: false)) // Amedo 2026-10-04
        {
            _browser.Log($"{label}: stopped, the open level could not be saved (the build plays the saved chunks).");
            return;
        }
        var projectRoot = ProjectRoot();
        var level = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2Path;
        var csproj = FindCrashGameProject();
        if (csproj is null) // Amedo
        {
            var player = Path.Combine(AppContext.BaseDirectory, "Player");
            if (!File.Exists(Path.Combine(player, "CrashGame.exe")))
            {
                _browser.Log($"{label}: no game player was found (expected a Player folder next to CrashLauncher.exe, or the CrashGame project next to CrashEngine).");
                return;
            }
            _buildingPc = true;
            _browser.Log($"{label}: copying the game player into {outDir} (the open level was saved first)...");
            Task.Run(() =>
            {
                try
                {
                    CopyPlayer(player, outDir);
                    WriteGameConfig(outDir, projectRoot, level);
                    LogBuilt(label, outDir, level); // Amedo
                    then?.Invoke(outDir); // Amedo
                }
                catch (Exception ex)
                {
                    _browser.Log($"{label} failed: {ex.Message} (close the game if it is running)");
                }
                finally
                {
                    _buildingPc = false;
                }
            });
            return;
        }
        _buildingPc = true;
        _browser.Log($"{label}: building {Path.GetFileName(csproj)} into {outDir} (the open level was saved first; the game reads the project settings the same way Play Scene does)..."); // Amedo 2026-10-04
        Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("dotnet",
                    $"publish \"{csproj}\" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o \"{outDir}\"")
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
                if (process.ExitCode != 0)
                {
                    lock (errors) _browser.Log($"{label} failed (exit {process.ExitCode}): {string.Join(" | ", errors.Distinct().Take(3))}");
                    return;
                }
                WriteGameConfig(outDir, projectRoot, level); // Amedo
                LogBuilt(label, outDir, level); // Amedo
                then?.Invoke(outDir); // Amedo
            }
            catch (Exception ex)
            {
                _browser.Log($"{label} failed: {ex.Message} (needs the .NET 9 SDK: dotnet)");
            }
            finally
            {
                _buildingPc = false;
            }
        });
    }

    // Amedo
    private void WriteGameConfig(string outDir, string projectRoot, string? level)
    {
        var config = new Dictionary<string, object> { ["project"] = projectRoot, ["windowed"] = false, ["volume"] = 1.0 };
        if (!string.IsNullOrEmpty(level)) config["level"] = Path.ChangeExtension(level, null);
        File.WriteAllText(Path.Combine(outDir, "game.json"), JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        string projectGraphics = Path.Combine(projectRoot, "graphics.json"), builtGraphics = Path.Combine(outDir, "graphics.json"); // Amedo
        if (File.Exists(projectGraphics)) File.Copy(projectGraphics, builtGraphics, overwrite: true);
        else if (File.Exists(builtGraphics)) File.Delete(builtGraphics);
        string smoothFrom = Path.Combine(ModeDir("Cache"), "SmoothNormals"), smoothTo = Path.Combine(outDir, "Cache", "SmoothNormals"); // Amedo
        if (Directory.Exists(smoothFrom))
        {
            Directory.CreateDirectory(smoothTo);
            foreach (var f in Directory.GetFiles(smoothFrom, "*.smn")) File.Copy(f, Path.Combine(smoothTo, Path.GetFileName(f)), overwrite: true);
        }
    }

    // Amedo
    private void LogBuilt(string label, string outDir, string? level)
    {
        _browser.Log($"{label}: done -> {Path.Combine(outDir, "CrashGame.exe")} (starts in {level ?? "the default level"}; Esc quits, F1 stats, F3 frame rate)");
    }

    // Amedo
    private static void CopyPlayer(string from, string to)
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

	private void StopPlayer()
	{
		if (_playerProcess is not { HasExited: false }) { _browser.Log("Stop Player: not running."); return; }
		try { _playerProcess.Kill(entireProcessTree: true); _browser.Log("Stop Player: stopped."); }
		catch (Exception ex) { _browser.Log("Stop Player: failed to stop -- " + ex.Message); }
	}
}
