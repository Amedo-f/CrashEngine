using System.Runtime.InteropServices;

namespace CrashEngine.Platform.Windows;

public static class WinMessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public static void ShowError(string text, string caption) => MessageBoxW(IntPtr.Zero, text, caption, 0x10);
}
