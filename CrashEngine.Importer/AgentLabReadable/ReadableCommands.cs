namespace CrashEngine.Importer.AgentLabReadable;

// Amedo 2026-09-25
public static class ReadableCommands
{
    private static readonly Dictionary<uint, string> MessageModes = new()
    {
        [0x00E0] = "Single",
        [0x00E1] = "Linked",
        [0x00E3] = "AllLinked",
        [0x00F1] = "LinkedLast",
    };

    private static CommandSpec C(ushort op, string name, int args, params uint[] pad) =>
        new() { Op = op, Name = name, ArgCount = args, PadDefault = pad };

    public static IEnumerable<CommandSpec> All()
    {
        yield return C(9, "DoAnimation", 6, 0x00000FF0)
            .Slots("slots", 5, 0, 0, 4)
            .F("blend", FieldKind.Variant, 1)
            .F("flags", FieldKind.Hex, 0, 12, 20, def: 0x3)
            .F("p2", FieldKind.Variant, 2, def: 0x4)
            .F("p3", FieldKind.Variant, 3, def: 0x4)
            .F("p4", FieldKind.Float, 4, def: 0);

        foreach (var (op, name, mode) in new[] { ((ushort)54, "SendUserMessage", 0x00E0u), ((ushort)65, "MessageLinkedObject", 0x00E3u) })
        {
            yield return C(op, name, 4, 0xCD000000)
                .F("message", FieldKind.UInt, 0, 0, 16)
                .F("to", FieldKind.Hex, 0, 16, 8, def: 0xFF)
                .F("mode", FieldKind.Enum, 1, 16, 16, def: mode, names: MessageModes)
                .F("selector", FieldKind.Hex, 1, 0, 16, def: 0xFF)
                .F("p2", FieldKind.Hex, 2, def: 0)
                .F("p3", FieldKind.Hex, 3, def: 0);
        }

        yield return C(66, "ClearUserMessage", 0);
        yield return C(63, "RestartDefaultBehaviour", 0);

        yield return C(68, "SetCounter", 2, 0xC8000000)
            .F("counter", FieldKind.UInt, 0, 0, 16)
            .F("global", FieldKind.NotBool, 0, 24, 1, def: 1)
            .F("object", FieldKind.Hex, 0, 16, 8, def: 0xFF)
            .F("random", FieldKind.Bool, 0, 25, 1, def: 0)
            .F("allLinked", FieldKind.Bool, 0, 26, 1, def: 0)
            .F("value", FieldKind.Variant, 1);

        yield return C(69, "ModifyCounter", 3, 0xCC000000, 0, 0xCDCD0000)
            .F("counter", FieldKind.UInt, 0, 0, 16)
            .F("global", FieldKind.NotBool, 0, 24, 1, def: 1)
            .F("object", FieldKind.Hex, 0, 16, 8, def: 0xFF)
            .F("allLinked", FieldKind.Bool, 0, 25, 1, def: 0)
            .F("add", FieldKind.Variant, 1)
            .F("subtype", FieldKind.Hex, 2, 0, 16, def: 0xFFFF);

        yield return C(42, "SetBehaviourPriority", 1, 0xCDCDCD00)
            .F("priority", FieldKind.UInt, 0, 0, 8);

        yield return C(124, "RunScriptSlot", 1, 0xCDCC0000)
            .F("slot", FieldKind.UInt, 0, 0, 16);

        yield return C(85, "DestroyMe", 1, 0xCDCDCDC0)
            .F("mode", FieldKind.UInt, 0, 0, 3, def: 2);

        yield return C(521, "SetPlayerInput", 2, 0, 0xCDCDCDCC)
            .F("change", FieldKind.Hex, 0, 0, 16)
            .F("off", FieldKind.Hex, 0, 16, 16, def: 0)
            .F("flags", FieldKind.Hex, 1, 0, 2, def: 0);

        yield return C(515, "SetAgent", 1)
            .F("change", FieldKind.Hex, 0, 0, 16)
            .F("value", FieldKind.Hex, 0, 16, 16, def: 0);
    }
}
