using System.Diagnostics;
using System.Text.Json;
using CrashEngine.Importer;
using CrashEngine.Install;
using CrashEngine.Platform;

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

    // Amedo
    private string ProjectRoot() => Path.GetDirectoryName(_scriptOut) ?? _extractedRoot;

    // Amedo
    private void LaunchPlayer()
    {
        if (_playerProcess is { HasExited: false }) { _browser.Log("Launch Player: already running."); return; }
        if (_buildingPc) { _browser.Log("Launch Player: a build is running, wait for it to finish."); return; }
        var outDir = Path.Combine(ProjectRoot(), "LaunchPlayer");
        if (!File.Exists(Path.Combine(outDir, PlatformInfo.ExeFile(PlayerPackage.PlayerExe))) || PlayerIsStale(outDir))
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
        var csproj = PlayerPackage.FindCrashGameProject(); // Amedo 2026-11-12
        if (csproj is null)
        {
            var player = PlayerPackage.ShippedFolder; // Amedo 2026-11-12
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
        var exe = Path.Combine(outDir, PlatformInfo.ExeFile(exeName));
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
        var csproj = PlayerPackage.FindCrashGameProject(); // Amedo 2026-11-12
        if (csproj is null) // Amedo
        {
            var player = PlayerPackage.ShippedFolder; // Amedo 2026-11-12
            if (!PlayerPackage.HasShipped) // Amedo 2026-11-12
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
                    PlayerPackage.Copy(player, outDir); // Amedo 2026-11-12
                    WriteGameConfig(outDir, projectRoot, level);
                    PlayerPackage.NameExe(outDir, exeName); // Amedo 2026-11-12
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
                var (exit, errors) = PlayerPackage.Publish(csproj, outDir); // Amedo 2026-11-12
                if (exit != 0)
                {
                    _browser.Log($"{label} failed (exit {exit}): {string.Join(" | ", errors.Take(3))}");
                    return;
                }
                WriteGameConfig(outDir, projectRoot, level); // Amedo
                PlayerPackage.NameExe(outDir, exeName); // Amedo 2026-11-12
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
        var config = new Dictionary<string, object> { ["project"] = Path.GetFullPath(projectRoot), ["windowed"] = false, ["volume"] = 1.0 };
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
        _browser.Log($"{label}: done -> {Path.Combine(outDir, PlatformInfo.ExeFile(exeName))} (starts in {level ?? "the default level"}; Esc quits, F1 stats, F3 frame rate)");
    }

	private void StopPlayer()
	{
		if (_playerProcess is not { HasExited: false }) { _browser.Log("Stop Player: not running."); return; }
		try { _playerProcess.Kill(entireProcessTree: true); _browser.Log("Stop Player: stopped."); }
		catch (Exception ex) { _browser.Log("Stop Player: failed to stop -- " + ex.Message); }
	}
}
