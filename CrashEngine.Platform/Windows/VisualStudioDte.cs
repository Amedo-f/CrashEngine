using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CrashEngine.Platform.Windows;

// Amedo 2026-11-18
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class VisualStudioDte
{
    public enum Shown { Yes, NotOpen, Busy }

    private const int RetryLater = unchecked((int)0x8001010A), CallRejected = unchecked((int)0x80010001);

    public static Shown TryShow(string solution, string file, int line, int column)
    {
        bool busy = false;
        if (Find(solution, ref busy) is not { } found) return busy ? Shown.Busy : Shown.NotOpen;
        try
        {
            dynamic dte = found;
            dte.ItemOperations.OpenFile(file);
            if (line > 0) dte.ActiveDocument.Selection.MoveToLineAndOffset(line, Math.Max(1, column));
            dte.MainWindow.Activate();
            SetForegroundWindow(new IntPtr((int)dte.MainWindow.HWnd));
            return Shown.Yes;
        }
        catch (Exception e) when (IsBusy(e))
        {
            return Shown.Busy;
        }
    }

    private static bool IsBusy(Exception e) =>
        e is COMException { HResult: RetryLater or CallRejected }
        || e is MissingMemberException && (e.Message.Contains("8001010A", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("80010001", StringComparison.OrdinalIgnoreCase));

    private static object? Find(string solution, ref bool busy)
    {
        if (GetRunningObjectTable(0, out var table) != 0 || CreateBindCtx(0, out var context) != 0) return null;
        table.EnumRunning(out var running);
        var one = new IMoniker[1];
        while (running.Next(1, one, IntPtr.Zero) == 0)
        {
            try
            {
                one[0].GetDisplayName(context, null, out string name);
                if (!name.StartsWith("!VisualStudio.DTE.", StringComparison.Ordinal)) continue;
                table.GetObject(one[0], out object dte);
                if (string.Equals((string)((dynamic)dte).Solution.FullName, solution, StringComparison.OrdinalIgnoreCase)) return dte;
            }
            catch (Exception e) when (IsBusy(e))
            {
                busy = true;
            }
            catch (Exception e) when (e is COMException or InvalidCastException or MissingMemberException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
            }
        }
        return null;
    }

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
}
