using System.Runtime.InteropServices;

namespace CrashLauncher;

internal static class Ps2ImageMakerNative
{
    public enum ProgressState
    {
        Failed = -1,
        EnumFiles,
        WriteSectors,
        WriteFiles,
        WriteEnd,
        Finished,
    }

    public class Progress
    {
        public string        File = "";
        public ProgressState ProgressS;
        public float         ProgressPercentage;
        public bool          Finished;
        public bool          NewState;
        public bool          NewFile;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct ProgressC
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string file_name;
        public int size;
        public ProgressState state;
        public float progress;
        public byte finished;
        public byte new_state;
        public byte new_file;
    }

    [DllImport("PS2ImageMaker", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern nint start_packing([MarshalAs(UnmanagedType.LPStr)] string game_path, [MarshalAs(UnmanagedType.LPStr)] string dest_path);
    [DllImport("PS2ImageMaker", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint poll_progress();

    private static Progress ToManaged(nint ptr)
    {
        var c = Marshal.PtrToStructure<ProgressC>(ptr);
        return new Progress
        {
            File               = c.file_name,
            ProgressS          = c.state,
            ProgressPercentage = c.progress,
            Finished           = c.finished != 0,
            NewState           = c.new_state != 0,
            NewFile            = c.new_file != 0,
        };
    }

    public static Progress StartPacking(string discContentPath, string isoOutputPath) =>
        ToManaged(start_packing(discContentPath, isoOutputPath));

    public static Progress PollProgress() => ToManaged(poll_progress());
}
