using System.Diagnostics;
using System.Text.Json;
using CrashEngine.Importer;

namespace CrashLauncher;

// Amedo 2026-09-30
public sealed partial class GameScene
{
    private volatile bool _buildingPc;

    private const string BuiltGameName = "Crash Twinsanity";
    private string _pcOutputPath = "";
    private bool _pcPathLoaded;
    private string PcOutputPathFile => Path.Combine(ProjectRoot(), ".pc_output_path.txt");

    private void EnsurePcOutputPathLoaded()
    {
        if (_pcPathLoaded) return;
        _pcPathLoaded = true;
        try { if (File.Exists(PcOutputPathFile)) _pcOutputPath = File.ReadAllText(PcOutputPathFile).Trim(); }
        catch { }
    }

    private void SavePcOutputPath()
    {
        try { File.WriteAllText(PcOutputPathFile, _pcOutputPath ?? ""); } catch { }
    }

    private void ChoosePcOutputPath(Action? then)
    {
        ShowFolderDialog($"Choose where Build to PC puts the game (it builds into a \"{BuiltGameName}\" folder inside it)",
            _pcOutputPath.Trim(), path =>
            {
                if (path is null) return;
                _pcOutputPath = path;
                SavePcOutputPath();
                then?.Invoke();
            });
    }

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
            _browser.Log($"Launch Player: preparing the test copy of the game in {outDir} (the Build to PC folder is not touched)...");
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
    private void StartLaunchedGame(string outDir) => _playerProcess = StartGame(outDir, "CrashGame", "Launch Player");

    private void RunBuiltGame(string outDir) => StartGame(outDir, BuiltGameName, "Build And Run")?.Dispose();

    private Process? StartGame(string outDir, string exeName, string label)
    {
        var exe = Path.Combine(outDir, exeName + ".exe");
        try
        {
            var process = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = outDir, UseShellExecute = false });
            _browser.Log($"{label}: started {exe} (pid {process?.Id}; Esc quits).");
            return process;
        }
        catch (Exception ex)
        {
            _browser.Log($"{label}: failed to start -- " + ex.Message);
            return null;
        }
    }

    // Amedo 2026-09-30
    private void BuildPc(bool run = false)
    {
        if (_buildingPc) return;
        EnsurePcOutputPathLoaded();
        if (string.IsNullOrWhiteSpace(_pcOutputPath))
        {
            _browser.Log("Build to PC: choose the folder first (kept for this project until you change it).");
            ChoosePcOutputPath(() => BuildPc(run));
            return;
        }
        BuildGameInto(Path.Combine(_pcOutputPath.Trim(), BuiltGameName), run ? "Build And Run" : "Build to PC", run ? RunBuiltGame : null, BuiltGameName);
    }

    // Amedo
    private void BuildGameInto(string outDir, string label, Action<string>? then, string exeName = "CrashGame")
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
                _browser.Log($"{label}: no game player was found (expected a Player folder next to CrashLauncher.exe, or the CrashGame project inside CrashEngine).");
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
                    NameGameExe(outDir, exeName);
                    LogBuilt(label, outDir, level, exeName); // Amedo
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
                NameGameExe(outDir, exeName);
                LogBuilt(label, outDir, level, exeName); // Amedo
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
    private void LogBuilt(string label, string outDir, string? level, string exeName)
    {
        _browser.Log($"{label}: done -> {Path.Combine(outDir, exeName + ".exe")} (starts in {level ?? "the default level"}; Esc quits, F1 stats, F3 frame rate)");
    }

    private static void NameGameExe(string outDir, string exeName)
    {
        if (exeName == "CrashGame") return;
        File.Move(Path.Combine(outDir, "CrashGame.exe"), Path.Combine(outDir, exeName + ".exe"), overwrite: true);
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
