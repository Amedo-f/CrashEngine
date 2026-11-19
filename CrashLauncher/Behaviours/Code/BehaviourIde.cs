using System.Diagnostics;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using CrashEngine.Platform.Windows;

namespace CrashLauncher;

// Amedo 2026-11-18
public static class BehaviourIde
{
    public const string ProjectName = "Behaviours";
    private const string ProjectGuid = "{3F2B6C1E-7A41-4E7B-9C5D-2B8E61A0C4D7}";
    private static readonly string[] EngineLibraries = ["CrashEngine.Core", "CrashEngine.Importer", "CrashEngine.Runtime", "CrashEngine.Renderer", "CrashEngine.Assets", "TwinTech"];
    private static readonly string[] BuildVariables = ["MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildSDKsPath", "MSBuildLoadMicrosoftTargetsReadOnly", "DOTNET_HOST_PATH"];
    private static readonly Regex OldNamespace = new(@"^namespace CrashEngine\.Behaviours;(?=\r?$)", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly TimeSpan LaunchWait = TimeSpan.FromMinutes(3);
    private const int BusyTries = 10;
    private static readonly object Gate = new();
    public static readonly System.Collections.Concurrent.ConcurrentQueue<string> Messages = new();
    private static Process? _launched;
    private static string? _devenv;

    public static string SolutionPath(string codeDir) => Path.Combine(codeDir, ProjectName + ".sln");

    public static List<string> WriteProject(string codeDir)
    {
        Directory.CreateDirectory(codeDir);
        string engine = AppContext.BaseDirectory;
        var project = new StringBuilder();
        project.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        project.AppendLine("  <!-- Made by CrashEngine so Visual Studio knows the engine and its rules. The editor turns these scripts into the game's data itself; this project is never built into the game. -->");
        project.AppendLine("  <PropertyGroup>");
        project.AppendLine("    <TargetFramework>net9.0</TargetFramework>");
        project.AppendLine("    <OutputType>Library</OutputType>");
        project.AppendLine("    <Nullable>enable</Nullable>");
        project.AppendLine("    <ImplicitUsings>disable</ImplicitUsings>");
        project.AppendLine($"    <AssemblyName>{ProjectName}</AssemblyName>");
        project.AppendLine("  </PropertyGroup>");
        project.AppendLine("  <ItemGroup>");
        foreach (var library in EngineLibraries)
            project.AppendLine($"    <Reference Include=\"{library}\"><HintPath>{SecurityElement.Escape(Path.Combine(engine, library + ".dll"))}</HintPath><Private>false</Private></Reference>");
        project.AppendLine($"    <Analyzer Include=\"{SecurityElement.Escape(Path.Combine(engine, "CrashEngine.Analyzers.dll"))}\" />");
        project.AppendLine("  </ItemGroup>");
        project.AppendLine("</Project>");
        WriteIfChanged(Path.Combine(codeDir, ProjectName + ".csproj"), project.ToString());
        WriteIfChanged(Path.Combine(codeDir, "GlobalUsings.cs"), "global using System;\nglobal using System.Linq;\nglobal using System.Collections.Generic;\n");
        WriteIfChanged(SolutionPath(codeDir), string.Join("\r\n",
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            "# Visual Studio Version 17",
            $"Project(\"{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}\") = \"{ProjectName}\", \"{ProjectName}.csproj\", \"{ProjectGuid}\"",
            "EndProject",
            "Global",
            "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution",
            "\t\tDebug|Any CPU = Debug|Any CPU",
            "\tEndGlobalSection",
            "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution",
            $"\t\t{ProjectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
            $"\t\t{ProjectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU",
            "\tEndGlobalSection",
            "EndGlobal", ""));
        return GiveLevelNamespaces(Path.Combine(codeDir, ProjectName));
    }

    private static List<string> GiveLevelNamespaces(string behavioursDir)
    {
        var changed = new List<string>();
        if (!Directory.Exists(behavioursDir)) return changed;
        foreach (var levelDir in Directory.EnumerateDirectories(behavioursDir))
        {
            string ns = "namespace " + BehaviourSource.Namespace(Path.GetFileName(levelDir)) + ";";
            foreach (var path in Directory.EnumerateFiles(levelDir, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                if (!OldNamespace.IsMatch(text)) continue;
                File.WriteAllText(path, OldNamespace.Replace(text, ns, 1));
                changed.Add(path);
            }
        }
        return changed;
    }

    public static void Open(string codeDir, string file, int line = 0, int column = 0)
    {
        string sln = SolutionPath(codeDir);
        Task.Run(() =>
        {
            try
            {
                lock (Gate) Messages.Enqueue(OpenNow(sln, file, line, column));
            }
            catch (Exception ex)
            {
                Messages.Enqueue("could not open Visual Studio: " + ex.Message);
            }
        });
    }

    private static string OpenNow(string sln, string file, int line, int column)
    {
        if (!OperatingSystem.IsWindows() || VisualStudio() is not { } devenv)
        {
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            return "Visual Studio was not found, so the file opened in its usual program.";
        }
        string opened = line > 0 ? $"Opened {Path.GetFileName(file)} at line {line} in Visual Studio." : $"Opened {Path.GetFileName(file)} in Visual Studio.";
        var shown = VisualStudioDte.TryShow(sln, file, line, column);
        for (int tries = 0; shown == VisualStudioDte.Shown.Busy && tries < BusyTries; tries++)
        {
            Thread.Sleep(1000);
            shown = VisualStudioDte.TryShow(sln, file, line, column);
        }
        if (shown == VisualStudioDte.Shown.Yes) return opened;
        if (shown == VisualStudioDte.Shown.Busy && _launched is not { HasExited: false })
            return "Visual Studio is busy (a message box may be open in it); close it and try again.";
        if (_launched is not { HasExited: false })
        {
            var start = new ProcessStartInfo(devenv) { UseShellExecute = false };
            foreach (var name in BuildVariables) start.Environment.Remove(name);
            start.ArgumentList.Add(sln);
            _launched = Process.Start(start);
            Messages.Enqueue("Opening Visual Studio with the behaviour project (the first time it takes a moment to load the engine)...");
        }
        var waited = Stopwatch.StartNew();
        while (waited.Elapsed < LaunchWait)
        {
            Thread.Sleep(1000);
            if (_launched is { HasExited: true }) return "Visual Studio closed before the behaviour project finished opening.";
            if (VisualStudioDte.TryShow(sln, file, line, column) == VisualStudioDte.Shown.Yes) return opened;
        }
        return "Visual Studio is taking long to open the behaviour project; double-click the file again once it is open.";
    }

    private static string? VisualStudio()
    {
        if (_devenv is not null) return _devenv;
        string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere)) return null;
        var find = new ProcessStartInfo(vswhere) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "-latest", "-property", "productPath" }) find.ArgumentList.Add(arg);
        using var process = Process.Start(find);
        string path = process?.StandardOutput.ReadToEnd().Trim() ?? "";
        process?.WaitForExit();
        return _devenv = File.Exists(path) ? path : null;
    }

    private static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        File.WriteAllText(path, text);
    }
}
