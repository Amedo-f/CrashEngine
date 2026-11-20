using System.Runtime.InteropServices;
using System.Text;
using CrashEngine.Platform;
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
        var text = Clipboard.GetText(); // Amedo 2026-11-20
        var utf8 = Encoding.UTF8.GetBytes(text + '\0');
        if (s_getBuffer != IntPtr.Zero) Marshal.FreeHGlobal(s_getBuffer);
        s_getBuffer = Marshal.AllocHGlobal(utf8.Length);
        Marshal.Copy(utf8, 0, s_getBuffer, utf8.Length);
        return s_getBuffer;
    };
    private static readonly SetFn s_set = (_, utf8Text) =>
        Clipboard.SetText(Marshal.PtrToStringUTF8(utf8Text) ?? ""); // Amedo 2026-11-20

    private static IntPtr s_getBuffer = IntPtr.Zero;
    private static readonly IntPtr s_getPtr = Marshal.GetFunctionPointerForDelegate(s_get);
    private static readonly IntPtr s_setPtr = Marshal.GetFunctionPointerForDelegate(s_set);

    public static unsafe void Hook(ImGuiIOPtr io)
    {
        if (!Clipboard.Supported) return; // Amedo 2026-11-20
        io.NativePtr->GetClipboardTextFn = s_getPtr;
        io.NativePtr->SetClipboardTextFn = s_setPtr;
    }
}
