using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-20
[SupportedOSPlatform("linux")]
internal static class GlfwClipboard
{
    private const string Glfw = "libglfw.so.3";

    [DllImport(Glfw)] private static extern IntPtr glfwGetClipboardString(IntPtr window);
    [DllImport(Glfw)] private static extern void glfwSetClipboardString(IntPtr window, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    public static string GetText()
    {
        try { return Marshal.PtrToStringUTF8(glfwGetClipboardString(IntPtr.Zero)) ?? ""; }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return ""; }
    }

    public static void SetText(string text)
    {
        try { glfwSetClipboardString(IntPtr.Zero, text); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }
}
