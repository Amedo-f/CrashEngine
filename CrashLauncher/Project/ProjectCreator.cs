using System.Diagnostics;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Install;

namespace CrashLauncher;

// Amedo 2026-11-12
public static class ProjectCreator
{
    public static string? Validate(string name, string location, string disc, out CrashProject project)
    {
        project = new CrashProject { Name = name.Trim(), Path = location.Trim(), DiscContentPathPS2 = disc.Trim() };
        if (project.Name.Length == 0 || project.Path.Length == 0) return "ERROR: Project name and path are required!";
        bool isIso = File.Exists(disc) && disc.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) && Ps2IsoReader.IsIso(disc);
        if (!isIso && !CrashProject.ValidateDiscPS2(disc)) return "ERROR: Select a PS2 .iso file, or an extracted disc folder that has SYSTEM.CNF.";
        if (Directory.Exists(project.ProjectPath) && Directory.GetFiles(project.ProjectPath, "*.tson").Length > 0)
            return $"ERROR: A project already exists at \"{project.ProjectPath}\" — pick a different project name or path, or use \"Open existing project\" below.";
        return null;
    }

    public static void Build(CrashProject project, Action<string> log, Action<float> progress, CancellationToken ct, ManualResetEventSlim gate)
    {
        project.CreateProjectStructure();
        project.Save();
        var discSource = project.DiscContentPathPS2 ?? "";
        if (File.Exists(discSource) && discSource.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
        {
            var target = Path.Combine(project.ProjectPath, "disc", "ps2");
            Directory.CreateDirectory(target);
            log("Extracting the disc image into the project (this can take a minute)...");
            int n = Ps2IsoReader.Extract(discSource, target, (file, p) => progress(p * 0.5f));
            log($"Extracted {n} file(s) from the ISO.");
            project.DiscContentPathPS2 = target;
        }
        else
        {
            log("Copying disc contents to project...");
            project.CopyDiscContents(p => progress(p * 0.5f), ct, gate);
        }
        log("Reading game archives...");
        using (var pkg = PackageReader.Open(project.DiscContentPathPS2!))
        {
            if (pkg.Records.Count == 0) throw new InvalidDataException("No game archives found in Crash6/!");
            log($"{pkg.Records.Count} records in archive");
            var manifestPath = Path.Combine(project.ProjectPath, ".unpack_manifest.txt");
            var doneSet = File.Exists(manifestPath) ? new HashSet<string>(File.ReadAllLines(manifestPath)) : new HashSet<string>();
            var manifestLock = new object();
            void OnRecordDone(string path) { lock (manifestLock) File.AppendAllText(manifestPath, path + "\n"); }
            log("Unpacking PS2 assets...");
            var result = AssetUnpacker.Unpack(pkg, project.ProjectPath, log, p => progress(0.5f + p * 0.5f), doneSet, OnRecordDone, ct, gate);
            log($"Unpacked {result.Files} files, {result.Chunks} chunks, {result.Assets} assets, {result.Textures} textures ({result.Skipped} already done, {result.Errors} warnings)");
            try { File.Delete(manifestPath); } catch (IOException) { }
        }
        ct.ThrowIfCancellationRequested();
        log("Serializing project...");
        project.Completed = true;
        project.Save();
        CrashProject.AddRecent(project.ProjectPath);
    }

    public static string? Open(string folderOrTson)
    {
        var project = CrashProject.Open(folderOrTson);
        if (project is null || string.IsNullOrEmpty(project.DiscContentPathPS2))
            return "ERROR: Not a valid project (missing .tson or disc path)";
        if (!Directory.Exists(project.DiscContentPathPS2))
            return $"ERROR: Disc content path no longer exists: {project.DiscContentPathPS2}";
        CrashProject.AddRecent(project.ProjectPath);
        ShowEditor(project.DiscContentPathPS2, project.ProjectPath);
        return null;
    }

    public static void ShowEditor(string disc, string? projectPath, string? fallbackScripts = null) =>
        Engine.Instance.ActiveScene = new LevelBrowserScene(disc, projectPath is not null ? Path.Combine(projectPath, "Scripts") : fallbackScripts!, projectPath);

    public static bool StartHub()
    {
        if (AppLocator.Beside(AppLocator.HubExe) is not { } hub) return false;
        var running = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(hub));
        bool open = running.Length > 0;
        foreach (var p in running) p.Dispose();
        if (!open) Process.Start(new ProcessStartInfo(hub) { WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false })?.Dispose();
        return true;
    }

    public static void Close(string scriptOut)
    {
        if (StartHub()) Engine.Instance.Quit();
        else Engine.Instance.ActiveScene = new ProjectSetupScene(scriptOut);
    }
}
