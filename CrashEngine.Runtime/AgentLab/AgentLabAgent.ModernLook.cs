using CrashEngine.Importer;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-11-18
public sealed partial class AgentLabAgent
{
    private ModernCharacterRenderer? ModernLook =>
        Entity?.Children.FirstOrDefault(c => c.Name == ModernCharacterRenderer.LookName)?.Get<ModernCharacterRenderer>();

    private bool PlayModernSlot(int slot, uint arg0, List<uint> args, float blend)
    {
        if (ModernLook is not { } look) return false;
        if (GameObject is { } own && (slot >= own.OGISlots.Count || own.OGISlots[slot] == 0xFFFF)) return true;
        int clip = look.ClipForSlot(slot);
        if (clip < 0) return true;
        Trace($"slot {slot}: modern clip '{look.Data.Clips[clip].Name}'");
        look.Request(clip, arg0, blend, RequestSpeed(arg0, args, look.Data.Clips[clip].Duration), BitConverter.UInt32BitsToSingle(args[4]));
        if (!Rendered) ApplyRendered();
        return true;
    }
}
