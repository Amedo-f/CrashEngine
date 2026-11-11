namespace CrashEngine.Runtime.Game;

// Amedo 2026-11-11
public abstract partial class SaveCode
{
    private bool CancelAnswer_Pal_2A12B0(int op)
    {
        if (op is < 3 or >= 5) return false;
        Ask(0xA);
        return true;
    }
}
