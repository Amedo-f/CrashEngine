using System.Runtime.InteropServices;
using System.Text;
using ImGuiNET;

namespace CrashEngine.Core;

public static class Win32Clipboard
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(IntPtr hMem);

    private static string GetClipboardTextRaw()
    {
        if (!OpenClipboard(IntPtr.Zero)) return "";
        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return "";
            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero) return "";
            try { return Marshal.PtrToStringUni(ptr) ?? ""; }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    private static void SetClipboardTextRaw(string text)
    {
        if (!OpenClipboard(IntPtr.Zero)) return;
        try
        {
            EmptyClipboard();
            var bytes = (text.Length + 1) * 2;
            var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hMem == IntPtr.Zero) return;
            var target = GlobalLock(hMem);
            if (target == IntPtr.Zero) return;
            try { Marshal.Copy((text + '\0').ToCharArray(), 0, target, text.Length + 1); }
            finally { GlobalUnlock(hMem); }
            SetClipboardData(CF_UNICODETEXT, hMem);
        }
        finally { CloseClipboard(); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetFn(IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetFn(IntPtr userData, IntPtr utf8Text);

    private static readonly GetFn s_get = _ =>
    {
        var text = GetClipboardTextRaw();
        var utf8 = Encoding.UTF8.GetBytes(text + '\0');
        if (s_getBuffer != IntPtr.Zero) Marshal.FreeHGlobal(s_getBuffer);
        s_getBuffer = Marshal.AllocHGlobal(utf8.Length);
        Marshal.Copy(utf8, 0, s_getBuffer, utf8.Length);
        return s_getBuffer;
    };
    private static readonly SetFn s_set = (_, utf8Text) =>
        SetClipboardTextRaw(Marshal.PtrToStringUTF8(utf8Text) ?? "");

    private static IntPtr s_getBuffer = IntPtr.Zero;
    private static readonly IntPtr s_getPtr = Marshal.GetFunctionPointerForDelegate(s_get);
    private static readonly IntPtr s_setPtr = Marshal.GetFunctionPointerForDelegate(s_set);

    public static unsafe void Hook(ImGuiIOPtr io)
    {
        io.NativePtr->GetClipboardTextFn = s_getPtr;
        io.NativePtr->SetClipboardTextFn = s_setPtr;
    }
}
