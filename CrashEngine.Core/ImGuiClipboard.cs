using System.Runtime.InteropServices;
using System.Text;
using CrashEngine.Platform.Windows;
using ImGuiNET;

namespace CrashEngine.Core;

public static class ImGuiClipboard
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetFn(IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetFn(IntPtr userData, IntPtr utf8Text);

    private static readonly GetFn s_get = _ =>
    {
        var text = Win32Clipboard.GetText();
        var utf8 = Encoding.UTF8.GetBytes(text + '\0');
        if (s_getBuffer != IntPtr.Zero) Marshal.FreeHGlobal(s_getBuffer);
        s_getBuffer = Marshal.AllocHGlobal(utf8.Length);
        Marshal.Copy(utf8, 0, s_getBuffer, utf8.Length);
        return s_getBuffer;
    };
    private static readonly SetFn s_set = (_, utf8Text) =>
        Win32Clipboard.SetText(Marshal.PtrToStringUTF8(utf8Text) ?? "");

    private static IntPtr s_getBuffer = IntPtr.Zero;
    private static readonly IntPtr s_getPtr = Marshal.GetFunctionPointerForDelegate(s_get);
    private static readonly IntPtr s_setPtr = Marshal.GetFunctionPointerForDelegate(s_set);

    public static unsafe void Hook(ImGuiIOPtr io)
    {
        if (!OperatingSystem.IsWindows()) return; // Amedo 2026-10-26
        io.NativePtr->GetClipboardTextFn = s_getPtr;
        io.NativePtr->SetClipboardTextFn = s_setPtr;
    }
}
