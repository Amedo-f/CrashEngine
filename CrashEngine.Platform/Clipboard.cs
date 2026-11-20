using CrashEngine.Platform.Linux;
using CrashEngine.Platform.Windows;

namespace CrashEngine.Platform;

// Amedo 2026-11-20
public static class Clipboard
{
    public static bool Supported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    public static string GetText()
    {
        if (OperatingSystem.IsWindows()) return Win32Clipboard.GetText();
        if (OperatingSystem.IsLinux()) return GlfwClipboard.GetText();
        return "";
    }

    public static void SetText(string text)
    {
        if (OperatingSystem.IsWindows()) Win32Clipboard.SetText(text);
        else if (OperatingSystem.IsLinux()) GlfwClipboard.SetText(text);
    }
}
