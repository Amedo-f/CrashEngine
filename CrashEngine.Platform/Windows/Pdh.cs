using System.Runtime.InteropServices;

namespace CrashEngine.Platform.Windows;

public static class Pdh
{
    public const uint FmtDouble = 0x00000200, FmtLarge = 0x00000400, FmtNoCap100 = 0x00008000;
    private const uint MoreData = 0x800007D2;

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] public static extern uint PdhOpenQueryW(string? source, IntPtr user, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] public static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr user, out IntPtr counter);
    [DllImport("pdh.dll")] public static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll")] public static extern uint PdhCloseQuery(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    public static List<(string Name, double Value)> Read(IntPtr counter, uint format)
    {
        var result = new List<(string, double)>();
        if (counter == IntPtr.Zero) return result;
        uint size = 0;
        uint status = PdhGetFormattedCounterArrayW(counter, format | FmtNoCap100, ref size, out uint count, IntPtr.Zero);
        if (status != MoreData || size == 0) return result;
        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(counter, format | FmtNoCap100, ref size, out count, buffer) != 0) return result;
            int itemSize = IntPtr.Size + 16;
            for (int i = 0; i < count; i++)
            {
                IntPtr item = buffer + i * itemSize;
                string name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item)) ?? "";
                uint cstatus = (uint)Marshal.ReadInt32(item + IntPtr.Size);
                if (cstatus != 0 && cstatus != 1) continue;
                double value = format == FmtLarge ? Marshal.ReadInt64(item + IntPtr.Size + 8) : BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item + IntPtr.Size + 8));
                result.Add((name, value));
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return result;
    }
}
