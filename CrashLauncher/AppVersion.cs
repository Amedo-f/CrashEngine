using System.Reflection;

namespace CrashLauncher;

// Amedo 2026-11-19
internal static class AppVersion
{
    public static string Text { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "";
}
