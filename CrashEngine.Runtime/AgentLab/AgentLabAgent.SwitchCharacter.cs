namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private void SwitchCharacter_11B700(uint w, uint slot)
    {
        if (slot != 6)
        {
            Switch((int)slot);
            return;
        }
        AgentLabAgent? target = null;
        if ((w & 0x1FE0) == 0x1FE0)
        {
            if ((w & 0x10) != 0) target = LinkAt((int)(w & 0xF));
        }
        else target = CodeObject((int)((w >> 5) & 0xFF));
        if (target?.OwnCharacter is null) return;
        Switch(target.ParamInt_2619D8(0)); // Amedo
    }

    private void Switch(int slot)
    {
        bool ok = World.SwitchCharacter_177A90?.Invoke(slot, true) ?? false;
        Trace($"switch character to slot {slot}: {(ok ? "done" : "no character")} (0x177A90)");
        if (World.SwitchCharacter_177A90 is null) World.Unimplemented("command", 625, "SwitchCharacter without a scene");
    }

    // Amedo
    private void CameraFocusObject_10F2D8(AgentLabAgent? focus)
    {
        if (World.CutsceneCamera is not { } cam)
        {
            World.Unimplemented("command", focus is null ? 607 : 606, "camera focus object without a game camera");
            return;
        }
        cam.CameraFocusObject_10F2D8(focus);
    }
}
