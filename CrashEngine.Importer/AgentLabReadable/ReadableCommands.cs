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

    // Amedo 2026-11-18
    private static readonly Dictionary<uint, string> Toggles = new()
    {
        [0] = "Keep",
        [1] = "On",
        [2] = "Off",
        [3] = "Same",
    };

    // Amedo 2026-11-18
    private static readonly Dictionary<uint, string> Designators = new()
    {
        [0xDF] = "Spawner",
        [0xEF] = "RouteEdgeEnd",
        [0xF0] = "Self",
        [0xF2] = "Player",
        [0xF5] = "BlastSender",
        [0xF6] = "StoredPosition",
        [0xF7] = "SecondSlot",
        [0xF8] = "Slot",
        [0xFA] = "RouteNode",
        [0xFB] = "FocusObject",
        [0xFC] = "FocusPosition",
        [0xFD] = "NextKey",
        [0xFE] = "CurrentKey",
        [0xFF] = "None",
    };

    // Amedo 2026-11-18
    private static readonly Dictionary<uint, string> SensorTypes = new()
    {
        [0] = "Flee",
        [1] = "Decay",
        [2] = "Speed",
    };

    // Amedo 2026-11-18
    private static readonly Dictionary<uint, string> AssignTargets = new()
    {
        [0] = "Focus",
        [1] = "Slot",
        [2] = "SecondSlot",
        [3] = "StoredPosition",
    };

    // Amedo 2026-11-18
    private static readonly Dictionary<uint, string> AttachSources = new()
    {
        [0] = "Focus",
        [1] = "Slot",
        [2] = "Cached",
    };

    // Amedo 2026-11-18
    private static CommandSpec Words(ushort op, string name, params (string Name, FieldKind Kind, uint? Def)[] words)
    {
        var spec = C(op, name, words.Length);
        for (int i = 0; i < words.Length; i++) spec.F(words[i].Name, words[i].Kind, i, def: words[i].Def);
        return spec;
    }

    // Amedo 2026-11-18
    private static readonly Dictionary<uint, string> WobbleWaves = new()
    {
        [0] = "None",
        [1] = "Sine",
        [2] = "Square",
        [3] = "SquareAlt",
        [4] = "Random",
        [5] = "Spin",
    };

    // Amedo 2026-11-18
    private static readonly Dictionary<uint, string> ConstraintTypes = new()
    {
        [0] = "None",
        [1] = "Point",
        [2] = "Line",
        [3] = "Plane",
    };

    // Amedo 2026-11-18
    private static uint BlockDef(uint[] d, int w, int shift, int bits) => bits >= 32 ? d[w] : (d[w] >> shift) & ((1u << bits) - 1u);
    private static void BlockFloat(CommandSpec s, int at, uint[] d, string name, int w) => s.F(name, FieldKind.Float, at + w, def: d[w]);
    private static void BlockFlag(CommandSpec s, int at, uint[] d, string name, int w, int shift) => s.F(name, FieldKind.Bool, at + w, shift, 1, def: BlockDef(d, w, shift, 1));
    private static void BlockUInt(CommandSpec s, int at, uint[] d, string name, int w, int shift, int bits) => s.F(name, FieldKind.UInt, at + w, shift, bits, def: BlockDef(d, w, shift, bits));

    // Amedo 2026-11-18
    private static CommandSpec PhysicsBlock(CommandSpec spec, int at, uint[] d, bool springs)
    {
        string[] numbers = ["damping", "axisDamping", "inertiaScale", "gravity", "grip", "twist", "bounce", "contactDecel"];
        for (int w = 0; w < numbers.Length; w++) BlockFloat(spec, at, d, numbers[w], w);
        if (springs)
        {
            BlockFloat(spec, at, d, "springDamping", 8);
            BlockFloat(spec, at, d, "springStiffness", 9);
            BlockFloat(spec, at, d, "springSide", 10);
        }
        else BlockFloat(spec, at, d, "buoyancy", 9);
        BlockFloat(spec, at, d, "centerOfMassX", 12);
        BlockFloat(spec, at, d, "centerOfMassY", 13);
        BlockFloat(spec, at, d, "centerOfMassZ", 14);
        BlockFloat(spec, at, d, "turnToFocus", 18);
        BlockFloat(spec, at, d, "constraintX", 19);
        BlockFloat(spec, at, d, "constraintY", 20);
        BlockFloat(spec, at, d, "constraintZ", 21);
        BlockUInt(spec, at, d, "lockAxes", 23, 0, 3);
        BlockFlag(spec, at, d, "noTerrain", 23, 3);
        BlockUInt(spec, at, d, "dampAxis", 23, 4, 2);
        spec.F("constraint", FieldKind.Enum, at + 23, 6, 4, def: BlockDef(d, 23, 6, 4), names: ConstraintTypes, enumType: "ConstraintType");
        BlockUInt(spec, at, d, "substeps", 23, 11, 3);
        BlockFlag(spec, at, d, "forceVolumes", 23, 14);
        BlockFlag(spec, at, d, "neverSleep", 23, 15);
        BlockFlag(spec, at, d, "limitAngle", 23, 18);
        BlockFlag(spec, at, d, "buoyant", 23, 19);
        BlockFloat(spec, at, d, "mass", 24);
        BlockFloat(spec, at, d, "angleLimit", 25);
        return ContactFields(spec, at, d);
    }

    // Amedo 2026-11-18
    private static CommandSpec WobbleBlock(CommandSpec spec, int at, uint[] d)
    {
        string[] numbers = ["freqX", "freqY", "freqZ", "ampX", "ampY", "ampZ", "phaseX", "phaseY", "phaseZ", "stiffness", "rate", "duration"];
        for (int w = 0; w < numbers.Length; w++) BlockFloat(spec, at, d, numbers[w], w);
        BlockUInt(spec, at, d, "rest", 12, 0, 8);
        BlockUInt(spec, at, d, "space", 26, 0, 3);
        foreach (var (axis, shift) in new[] { ("X", 3), ("Y", 6), ("Z", 9) })
            spec.F($"wave{axis}", FieldKind.Enum, at + 26, shift, 3, def: BlockDef(d, 26, shift, 3), names: WobbleWaves, enumType: "WobbleWave");
        BlockFlag(spec, at, d, "rotate", 26, 12);
        BlockFlag(spec, at, d, "fade", 26, 13);
        BlockFlag(spec, at, d, "randomPhaseX", 26, 16);
        BlockFlag(spec, at, d, "randomPhaseY", 26, 17);
        BlockFlag(spec, at, d, "randomPhaseZ", 26, 18);
        BlockUInt(spec, at, d, "signX", 26, 20, 2);
        BlockUInt(spec, at, d, "signY", 26, 22, 2);
        BlockUInt(spec, at, d, "signZ", 26, 24, 2);
        BlockUInt(spec, at, d, "follow", 27, 0, 3);
        BlockFlag(spec, at, d, "axisX", 27, 3);
        BlockFlag(spec, at, d, "axisY", 27, 4);
        BlockFlag(spec, at, d, "axisZ", 27, 5);
        BlockFlag(spec, at, d, "easeIn", 27, 7);
        return ContactFields(spec, at, d);
    }

    // Amedo 2026-11-18
    private static CommandSpec ContactFields(CommandSpec spec, int at, uint[] d)
    {
        BlockFloat(spec, at, d, "contactGrip", 15);
        BlockFloat(spec, at, d, "ownerGrip", 16);
        BlockFlag(spec, at, d, "snapToRest", 26, 14);
        BlockUInt(spec, at, d, "mode", 26, 28, 3);
        BlockFlag(spec, at, d, "useCenterOfMass", 27, 8);
        BlockFlag(spec, at, d, "faceVelocity", 27, 10);
        BlockFlag(spec, at, d, "stickOnTouch", 27, 13);
        BlockFlag(spec, at, d, "wobbleOnTouch", 27, 14);
        BlockFlag(spec, at, d, "preWobble", 27, 15);
        BlockFlag(spec, at, d, "messageOnTouch", 27, 16);
        spec.F("components", FieldKind.Hex, at + 28, def: d[28]);
        BlockFloat(spec, at, d, "push", 29);
        BlockUInt(spec, at, d, "touchMessage", 30, 0, 16);
        BlockUInt(spec, at, d, "stickMessage", 31, 0, 16);
        BlockUInt(spec, at, d, "stickFilter", 32, 0, 16);
        return spec;
    }

    // Amedo 2026-11-18
    private static CommandSpec RequestFocus(ushort op, string name, uint storeIn, uint radius) =>
        C(op, name, 13, 0xCDCDCDCD, 0, 0, 0, 0x3F800000, 0, 0, 0, 0x80000000, 0xCDCDCD80, 0, 0, 0xCDCDCDCD)
            .F("offsetX", FieldKind.Float, 1, def: 0)
            .F("offsetY", FieldKind.Float, 2, def: 0)
            .F("offsetZ", FieldKind.Float, 3, def: 0)
            .F("object1", FieldKind.UInt, 5, 0, 16, def: 0xFFFF)
            .F("object2", FieldKind.UInt, 5, 16, 16, def: 0xFFFF)
            .F("object3", FieldKind.UInt, 6, 0, 16, def: 0xFFFF)
            .F("object4", FieldKind.UInt, 6, 16, 16, def: 0xFFFF)
            .F("heldObject1", FieldKind.UInt, 7, 0, 16, def: 0xFFFF)
            .F("heldObject2", FieldKind.UInt, 7, 16, 16, def: 0xFFFF)
            .F("space", FieldKind.UInt, 8, 0, 4, def: 2)
            .F("participant", FieldKind.UInt, 8, 4, 4, def: 0xF)
            .F("key", FieldKind.Enum, 8, 8, 8, def: 0xFF, names: Designators, enumType: "Designator")
            .F("objectCount", FieldKind.UInt, 8, 16, 3, def: 1)
            .F("nearest", FieldKind.Bool, 8, 19, 1, def: 1)
            .F("useOffset", FieldKind.Bool, 8, 20, 1, def: 0)
            .F("pickRandom", FieldKind.Bool, 8, 22, 1, def: 1)
            .F("attachFilter", FieldKind.UInt, 8, 23, 4, def: 0)
            .F("busyFilter", FieldKind.UInt, 8, 27, 2, def: 0)
            .F("markBusy", FieldKind.Bool, 8, 29, 1, def: 0)
            .F("alsoSecondSlot", FieldKind.Bool, 8, 30, 1, def: 0)
            .F("storeIn", FieldKind.Enum, 9, 0, 2, def: storeIn, names: AssignTargets, enumType: "AssignTarget")
            .F("includeNonReceivers", FieldKind.Bool, 9, 2, 1, def: 0)
            .F("heldCount", FieldKind.UInt, 9, 3, 2, def: 0)
            .F("allowCurrent", FieldKind.Bool, 9, 5, 1, def: 0)
            .F("visibleOnly", FieldKind.Bool, 9, 6, 1, def: 0)
            .F("components", FieldKind.Hex, 10, def: 0x3FF000)
            .F("radius", FieldKind.Float, 11, def: radius);

    private static CommandSpec C(ushort op, string name, int args, params uint[] pad) =>
        new() { Op = op, Name = name, ArgCount = args, PadDefault = pad };

    public static IEnumerable<CommandSpec> All()
    {
        foreach (var (op, name) in new[] { ((ushort)9, "DoAnimation"), ((ushort)543, "CA_DoAnimation") }) // Amedo 2026-11-18
            yield return C(op, name, 6, 0x00000FF0)
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
                .F("mode", FieldKind.Enum, 1, 16, 16, def: mode, names: MessageModes, enumType: "MessageMode") // Amedo 2026-11-18
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

        // Amedo 2026-11-18
        yield return C(139, "SetInfluence", 1)
            .F("influence", FieldKind.Float, 0);

        yield return C(117, "ApplyBlastRadius", 2)
            .F("radius", FieldKind.Float, 0)
            .F("strength", FieldKind.Float, 1);

        yield return C(122, "CopyDesignator", 1, 0xCD000000)
            .F("from", FieldKind.Enum, 0, 0, 8, names: Designators, enumType: "Designator")
            .F("to", FieldKind.Enum, 0, 8, 8, names: Designators, enumType: "Designator")
            .F("owner", FieldKind.Enum, 0, 16, 8, def: 0xF0, names: Designators, enumType: "Designator");

        yield return C(78, "SetObject", 2, 0x80000000, 0xCDCDCDC0)
            .F("busy", FieldKind.Enum, 0, 0, 2, def: 0, names: Toggles, enumType: "Toggle")
            .F("visible", FieldKind.Enum, 0, 2, 2, def: 0, names: Toggles, enumType: "Toggle")
            .F("collidable", FieldKind.Enum, 0, 4, 2, def: 0, names: Toggles, enumType: "Toggle")
            .F("awake", FieldKind.Enum, 0, 6, 2, def: 0, names: Toggles, enumType: "Toggle")
            .F("bodyFlag20", FieldKind.Enum, 0, 10, 2, def: 0, names: Toggles, enumType: "Toggle")
            .F("animRange", FieldKind.UInt, 0, 13, 8, def: 0)
            .F("activityRadius", FieldKind.UInt, 0, 21, 8, def: 0)
            .F("setAnimRange", FieldKind.Bool, 0, 29, 1, def: 0)
            .F("setActivityRadius", FieldKind.Bool, 0, 30, 1, def: 0)
            .F("bodyFlag40", FieldKind.Enum, 1, 0, 2, def: 0, names: Toggles, enumType: "Toggle")
            .F("carries", FieldKind.Enum, 1, 2, 2, def: 0, names: Toggles, enumType: "Toggle")
            .F("solid", FieldKind.Enum, 1, 4, 2, def: 0, names: Toggles, enumType: "Toggle");

        foreach (var (op, name) in new[] { ((ushort)67, "RequestMessSourceAsFocus"), ((ushort)101, "RequestMessSourceAsFocus2"), ((ushort)102, "RequestMessSourceAsFocus3") })
            yield return C(op, name, 1, 0xCDCDCDC8)
                .F("into", FieldKind.Enum, 0, 0, 3, def: 0, names: AssignTargets, enumType: "AssignTarget");

        yield return C(569, "ExitVehicleMode", 1)
            .F("player", FieldKind.UInt, 0);

        yield return C(528, "ReduceHitPoints", 1)
            .F("amount", FieldKind.Int, 0);

        yield return C(587, "TouchResponse", 1, 0xCDCDC000)
            .F("motion10000", FieldKind.Enum, 0, 0, 2, def: 2, names: Toggles, enumType: "Toggle")
            .F("motion40000", FieldKind.Enum, 0, 4, 2, def: 0, names: Toggles, enumType: "Toggle")
            .F("motionE000", FieldKind.Enum, 0, 10, 2, def: 3, names: Toggles, enumType: "Toggle");

        yield return C(75, "DestroySpawnedAttachment", 1, 0xCDCDCC00)
            .F("exit", FieldKind.UInt, 0, 0, 8, def: 0xFF)
            .F("all", FieldKind.Bool, 0, 8, 1, def: 1);

        // Amedo 2026-11-18
        foreach (var (op, name, into) in new[] { ((ushort)146, "FocusOnLinked", 0u), ((ushort)155, "SetSlotToLinked", 1u), ((ushort)154, "StoreLinkedPosition", 3u) })
            yield return C(op, name, 1, 0xCDCD0000)
                .F("item", FieldKind.UInt, 0, 0, 8)
                .F("fromLinks", FieldKind.Bool, 0, 8, 1, def: 1)
                .F("atCursor", FieldKind.Bool, 0, 9, 1, def: 0)
                .F("ofFocus", FieldKind.Bool, 0, 10, 1, def: 0)
                .F("ofSlot", FieldKind.Bool, 0, 11, 1, def: 0)
                .F("into", FieldKind.Enum, 0, 12, 3, def: into, names: AssignTargets, enumType: "AssignTarget")
                .F("last", FieldKind.Bool, 0, 15, 1, def: 0);

        foreach (var (op, name, into) in new[] { ((ushort)28, "SetFocusToKey", 0u), ((ushort)103, "SetFocusToKey2", 3u) })
            yield return C(op, name, 1, 0xCDCDC000)
                .F("key", FieldKind.UInt, 0, 0, 8)
                .F("into", FieldKind.Enum, 0, 9, 3, def: into, names: AssignTargets, enumType: "AssignTarget")
                .F("ofFocus", FieldKind.Bool, 0, 12, 1, def: 0)
                .F("ofSlot", FieldKind.Bool, 0, 13, 1, def: 0);

        foreach (var (op, name, into) in new[] { ((ushort)45, "SetFocusToAgent", 0u), ((ushort)90, "SetFocusToAgent2", 1u), ((ushort)91, "SetFocusToAgent3", 2u), ((ushort)104, "SetFocusToAgent4", 3u) })
            yield return C(op, name, 1, 0xCDCDC800)
                .F("agent", FieldKind.Enum, 0, 0, 8, names: Designators, enumType: "Designator")
                .F("into", FieldKind.Enum, 0, 8, 3, def: into, names: AssignTargets, enumType: "AssignTarget");

        yield return C(576, "FocusOnPlayer", 1, 0xCDCDCD00)
            .F("player", FieldKind.UInt, 0, 0, 8);

        yield return C(171, "RequestOnTarget", 2, 0x0000CD00, 0xCDC80000)
            .F("target", FieldKind.Enum, 0, 0, 8, names: Designators, enumType: "Designator")
            .F("objectId", FieldKind.Hex, 0, 16, 16, def: 0xFF)
            .F("slot", FieldKind.UInt, 1, 0, 16)
            .F("channel", FieldKind.UInt, 1, 16, 1, def: 0)
            .F("allLinks", FieldKind.Bool, 1, 17, 1, def: 0)
            .F("control", FieldKind.Bool, 1, 18, 1, def: 1);

        yield return C(172, "ReleaseTarget", 1, 0xCDCDCC00)
            .F("target", FieldKind.Enum, 0, 0, 8, names: Designators, enumType: "Designator")
            .F("allLinks", FieldKind.Bool, 0, 9, 1, def: 0);

        yield return C(118, "SetLookTarget", 2, 0xCDCDC000)
            .F("participant", FieldKind.UInt, 0, 0, 8, def: 0xFF)
            .F("player", FieldKind.Bool, 0, 8, 1, def: 0)
            .F("secondSlot", FieldKind.Bool, 0, 9, 1, def: 0)
            .F("focus", FieldKind.Bool, 0, 10, 1, def: 0)
            .F("persistent", FieldKind.Bool, 0, 11, 1, def: 0)
            .F("weight", FieldKind.Float, 1);

        yield return C(79, "Keep", 1, 0xCDCDCDC0)
            .F("trails", FieldKind.Bool, 0, 0, 1, def: 0)
            .F("wobble", FieldKind.Bool, 0, 1, 1, def: 0)
            .F("keep400", FieldKind.Bool, 0, 2, 1, def: 0)
            .F("keep800", FieldKind.Bool, 0, 3, 1, def: 0)
            .F("keep1000", FieldKind.Bool, 0, 4, 1, def: 0)
            .F("sensors", FieldKind.Bool, 0, 5, 1, def: 0);

        yield return C(4, "SetKey", 1, 0xC0000000)
            .F("key", FieldKind.UInt, 0, 0, 8, def: 0xFF)
            .F("first", FieldKind.UInt, 0, 8, 8, def: 0)
            .F("last", FieldKind.UInt, 0, 16, 8, def: 0xFF)
            .F("notCurrent", FieldKind.Bool, 0, 25, 1, def: 0)
            .F("nearest", FieldKind.Bool, 0, 26, 1, def: 0)
            .F("random", FieldKind.Bool, 0, 27, 1, def: 1)
            .F("toLast", FieldKind.Bool, 0, 28, 1, def: 0);

        foreach (var (op, name) in new[] { ((ushort)183, "TakeControl"), ((ushort)184, "ReleaseControl") })
            yield return C(op, name, 1, 0xCDCDCC00)
                .F("target", FieldKind.Enum, 0, 0, 8, names: Designators, enumType: "Designator")
                .F("own", FieldKind.Bool, 0, 8, 1, def: 0);

        yield return C(14, "NowMoveForwards", 1)
            .F("speed", FieldKind.Variant, 0);

        // Amedo 2026-11-18
        yield return C(11, "DoSound", 15, 0xCD0000F0, 0, 0, 0, 0, 0, 0x3F000000, 0, 0, 0, 0xCDCDCDCD)
            .F("count", FieldKind.UInt, 0, 0, 4, def: 1)
            .F("category", FieldKind.UInt, 0, 8, 3, def: 0)
            .F("trackedNoStore", FieldKind.Bool, 0, 11, 1, def: 0)
            .F("tracked", FieldKind.Bool, 0, 12, 1, def: 0)
            .F("randomPitch", FieldKind.Bool, 0, 14, 1, def: 0)
            .F("randomVolume", FieldKind.Bool, 0, 15, 1, def: 0)
            .F("setPitch", FieldKind.Bool, 0, 16, 1, def: 0)
            .F("variableVolume", FieldKind.Bool, 0, 17, 1, def: 0)
            .F("shake", FieldKind.Bool, 0, 18, 1, def: 0)
            .F("surface", FieldKind.UInt, 0, 19, 4, def: 0xF)
            .F("store", FieldKind.Bool, 0, 23, 1, def: 0)
            .F("sound0", FieldKind.UInt, 1, 0, 16)
            .F("sound1", FieldKind.UInt, 1, 16, 16, def: 0xCDCD)
            .F("sound2", FieldKind.UInt, 2, 0, 16, def: 0xCDCD)
            .F("sound3", FieldKind.UInt, 2, 16, 16, def: 0xCDCD)
            .F("sound4", FieldKind.UInt, 3, 0, 16, def: 0xCDCD)
            .F("sound5", FieldKind.UInt, 3, 16, 16, def: 0xCDCD)
            .F("sound6", FieldKind.UInt, 4, 0, 16, def: 0xCDCD)
            .F("sound7", FieldKind.UInt, 4, 16, 16, def: 0xCDCD)
            .F("volume", FieldKind.Variant, 5, def: 0x4)
            .F("pitch", FieldKind.Float, 7, def: 0xCDCDCDCD)
            .F("pitchSpread", FieldKind.Float, 8, def: 0xCDCDCDCD)
            .F("volumeSpread", FieldKind.Float, 9, def: 0xCDCDCDCD)
            .F("shakeStrength", FieldKind.Float, 11, def: 0xCDCDCDCD)
            .F("shakeXZ", FieldKind.Float, 12, def: 0)
            .F("shakeY", FieldKind.Float, 13, def: 0)
            .F("shakeFalloff", FieldKind.Float, 14, def: 0);

        foreach (var (op, name) in new[] { ((ushort)10, "DoParticle"), ((ushort)544, "CA_DoParticle") }) // Amedo 2026-11-18
            yield return C(op, name, 9, 0x80000000, 0xC0000000, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD)
                .F("system", FieldKind.UInt, 0, 0, 16)
                .F("count", FieldKind.UInt, 0, 16, 7, def: 1)
                .F("attached", FieldKind.Bool, 0, 23, 1, def: 1)
                .F("attached2", FieldKind.Bool, 0, 24, 1, def: 0)
                .F("frame", FieldKind.UInt, 0, 25, 6, def: 0x3F)
                .F("local", FieldKind.Bool, 1, 0, 1, def: 0)
                .F("axis", FieldKind.UInt, 1, 1, 4, def: 0)
                .F("useOffset", FieldKind.Bool, 1, 5, 1, def: 0)
                .F("target", FieldKind.Enum, 1, 6, 8, def: 0xFF, names: Designators, enumType: "Designator")
                .F("actorOverride", FieldKind.Hex, 1, 14, 8, def: 0xFF)
                .F("key", FieldKind.UInt, 1, 22, 8, def: 0xFF)
                .F("x", FieldKind.Float, 5, def: 0x4CEB79A3)
                .F("y", FieldKind.Float, 6, def: 0x4CEB79A3)
                .F("z", FieldKind.Float, 7, def: 0x4CEB79A3)
                .F("w", FieldKind.Float, 8, def: 0x4CEB79A3);

        // Amedo 2026-11-18
        yield return C(31, "ClearThreats", 1, 0xCDCDCD00).F("restartable", FieldKind.Bool, 0, 0, 1, def: 0);
        yield return C(34, "SetState", 1, 0xCDCDCD00).F("on", FieldKind.UInt, 0, 0, 8, def: 1);
        yield return C(27, "StoreCurrentSpace", 1, 0xCDC00000)
            .F("participant", FieldKind.UInt, 0, 0, 8, def: 0xFF)
            .F("key", FieldKind.Enum, 0, 8, 8, def: 0xFF, names: Designators, enumType: "Designator");
        yield return C(589, "CutsceneStart", 1).F("seconds", FieldKind.Float, 0);
        yield return C(590, "CutsceneEnd", 1).F("seconds", FieldKind.Float, 0);
        yield return C(620, "HideBottomText", 1).F("seconds", FieldKind.Float, 0);
        yield return C(602, "FadeoutScreen", 6, 0xCDCDCDC0)
            .F("mode", FieldKind.UInt, 0, 0, 3)
            .F("setColour", FieldKind.Bool, 0, 3, 1, def: 0)
            .F("duration", FieldKind.Float, 1)
            .F("red", FieldKind.Float, 3, def: 0)
            .F("green", FieldKind.Float, 4, def: 0)
            .F("blue", FieldKind.Float, 5, def: 0);
        yield return C(529, "SetHitPoints", 1).F("hitPoints", FieldKind.Int, 0);
        yield return C(636, "AddAmmo", 1).F("amount", FieldKind.Int, 0);
        yield return C(639, "DamageBoss", 1).F("amount", FieldKind.Int, 0);
        yield return C(633, "SetPlayerMode", 3)
            .F("mode", FieldKind.UInt, 0)
            .F("mode2", FieldKind.UInt, 1, def: 0)
            .F("mode3", FieldKind.UInt, 2);
        foreach (var (op, name, from) in new[] { ((ushort)534, "SetGlobalProgression", 0x11u), ((ushort)647, "SetGlobalProgression2", 0u) })
            yield return C(op, name, 2)
                .F("value", FieldKind.Int, 0, def: 0xFFFFFFFF)
                .F("fromValue", FieldKind.Variant, 1, def: from);
        yield return C(625, "SwitchCharacter", 2, 0xCDCDC000)
            .F("link", FieldKind.UInt, 0, 0, 4, def: 0xF)
            .F("useLink", FieldKind.Bool, 0, 4, 1, def: 0)
            .F("target", FieldKind.Enum, 0, 5, 8, def: 0xFF, names: Designators, enumType: "Designator")
            .F("slot", FieldKind.UInt, 1);
        yield return C(167, "SetSurface", 1, 0xCC000000)
            .F("surface", FieldKind.UInt, 0, 0, 16)
            .F("allHulls", FieldKind.Bool, 0, 16, 1, def: 1)
            .F("hull", FieldKind.UInt, 0, 17, 8, def: 0xFF);
        yield return C(33, "TriggerLinkedObjects", 1, 0xCDCDCDCC).F("fromRequester", FieldKind.Bool, 0, 0, 1, def: 0);
        yield return C(659, "CharactersToIdle", 1, 0xCDCDCDC0)
            .F("all", FieldKind.Bool, 0, 0, 1, def: 0)
            .F("player0", FieldKind.Bool, 0, 1, 1, def: 0)
            .F("player1", FieldKind.Bool, 0, 2, 1, def: 0)
            .F("player5", FieldKind.Bool, 0, 3, 1, def: 0);
        yield return C(74, "ForceAnimationUpdate", 1, 0xCDCDCDC0).F("mode", FieldKind.UInt, 0, 0, 4, def: 1);
        yield return C(48, "DropAttachedObject", 9, 0xC0000000, 0, 0, 0, 0x3F800000, 0, 0, 0, 0xCDCDCDCD)
            .F("message", FieldKind.Hex, 0, 0, 16, def: 0xFFFF)
            .F("exit", FieldKind.UInt, 0, 16, 8, def: 0)
            .F("mode", FieldKind.UInt, 0, 24, 3, def: 0)
            .F("notBusy", FieldKind.Bool, 0, 29, 1, def: 0);
        yield return C(163, "UnlinkFromTarget", 1, 0xCDCDC000)
            .F("target", FieldKind.Enum, 0, 0, 8, names: Designators, enumType: "Designator")
            .F("allLinks", FieldKind.Bool, 0, 8, 1, def: 0)
            .F("byIndex", FieldKind.Bool, 0, 9, 1, def: 0)
            .F("atCursor", FieldKind.Bool, 0, 10, 1, def: 0)
            .F("force", FieldKind.Bool, 0, 11, 1, def: 0);
        yield return C(185, "PrepareSpeech", 2, 0, 0xCDCDCDCC)
            .F("track", FieldKind.Variant, 0)
            .F("loop", FieldKind.Bool, 1, 0, 1, def: 0);
        yield return C(40, "SetLogicalRadius", 2, 0xCDCDCDCC)
            .F("fromBox", FieldKind.Bool, 0, 0, 1, def: 0)
            .F("radius", FieldKind.Float, 1);
        yield return C(638, "EnableBossMode", 3)
            .F("icon", FieldKind.UInt, 0)
            .F("health", FieldKind.Variant, 1)
            .F("iconWidth", FieldKind.Float, 2);
        yield return C(603, "DisplayBottomText", 7)
            .F("text", FieldKind.Int, 0)
            .F("x", FieldKind.Float, 1, def: 0x3F000000)
            .F("y", FieldKind.Float, 2, def: 0x3F6B851F)
            .F("red", FieldKind.Float, 3, def: 0x3F800000)
            .F("green", FieldKind.Float, 4, def: 0x3F800000)
            .F("blue", FieldKind.Float, 5, def: 0x3F800000)
            .F("seconds", FieldKind.Float, 6, def: 0);
        yield return C(205, "SetFocusPositionAtAngle", 2)
            .F("distance", FieldKind.Float, 0)
            .F("angle", FieldKind.Float, 1);
        yield return C(524, "SetKeyNearestPlayer", 3, 0, 0, 0xCD000000)
            .F("range", FieldKind.Float, 0, def: 0)
            .F("lead", FieldKind.Float, 1, def: 0)
            .F("includeCurrent", FieldKind.UInt, 2, 0, 8, def: 0)
            .F("first", FieldKind.UInt, 2, 8, 8, def: 0)
            .F("last", FieldKind.UInt, 2, 16, 8, def: 0xFF);

        foreach (var (op, name, face) in new[] { ((ushort)3, "PositionWarp", 0u), ((ushort)105, "SetRotation2", 1u) })
            yield return C(op, name, 5, 0xCC000000, 0, 0, 0, 0x3F800000)
                .F("participant", FieldKind.UInt, 0, 0, 8, def: 0xFF)
                .F("key", FieldKind.Enum, 0, 8, 8, names: Designators, enumType: "Designator")
                .F("space", FieldKind.UInt, 0, 16, 4, def: 0)
                .F("useOffset", FieldKind.Bool, 0, 21, 1, def: 0)
                .F("face", FieldKind.Bool, 0, 22, 1, def: face)
                .F("body", FieldKind.Bool, 0, 23, 1, def: 0)
                .F("water", FieldKind.Bool, 0, 24, 1, def: 0)
                .F("x", FieldKind.Float, 1, def: 0)
                .F("y", FieldKind.Float, 2, def: 0)
                .F("z", FieldKind.Float, 3, def: 0);

        foreach (var (op, name, face) in new[] { ((ushort)177, "WarpAgent", 0u), ((ushort)179, "WarpAgentFacing", 1u) })
            yield return C(op, name, 9, 0, 0xCDCDCD00, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0, 0, 0, 0x3F800000)
                .F("participant", FieldKind.UInt, 0, 0, 8, def: 0xFF)
                .F("key", FieldKind.Enum, 0, 8, 8, names: Designators, enumType: "Designator")
                .F("space", FieldKind.UInt, 0, 16, 4, def: 0)
                .F("useOffset", FieldKind.Bool, 0, 21, 1, def: 0)
                .F("face", FieldKind.Bool, 0, 22, 1, def: face)
                .F("body", FieldKind.Bool, 0, 23, 1, def: 0)
                .F("agent", FieldKind.Enum, 0, 24, 8, names: Designators, enumType: "Designator")
                .F("source", FieldKind.Enum, 1, 0, 8, def: 0xFF, names: Designators, enumType: "Designator")
                .F("x", FieldKind.Float, 5, def: 0)
                .F("y", FieldKind.Float, 6, def: 0)
                .F("z", FieldKind.Float, 7, def: 0);

        yield return C(595, "CutsceneTargets", 3, 0, 0xCDCDCDCC, 0xCDCD0000)
            .F("target1", FieldKind.Enum, 0, 0, 8, names: Designators, enumType: "Designator")
            .F("target2", FieldKind.Enum, 0, 8, 8, def: 0xFF, names: Designators, enumType: "Designator")
            .F("position1", FieldKind.Enum, 0, 16, 8, def: 0xFF, names: Designators, enumType: "Designator")
            .F("position2", FieldKind.Enum, 0, 24, 8, def: 0xFF, names: Designators, enumType: "Designator")
            .F("mirror", FieldKind.Bool, 1, 0, 1, def: 0) // Amedo 2026-11-18
            .F("trackTargets", FieldKind.Bool, 1, 1, 1, def: 1)
            .F("path1", FieldKind.UInt, 2, 0, 8, def: 0xFF)
            .F("path2", FieldKind.UInt, 2, 8, 8, def: 0xFF);

        foreach (var (op, name, into) in new[] { ((ushort)60, "SetFocusPosition", 0u), ((ushort)108, "SetFocusPosition2", 3u) })
            yield return C(op, name, 21, 0xCDCDCDCD, 0, 0, 0, 0x3F800000, 0, 0xCDCDCDCC, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xCDCDCD00, 0, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD)
                .F("x", FieldKind.Float, 1, def: 0)
                .F("y", FieldKind.Float, 2, def: 0)
                .F("z", FieldKind.Float, 3, def: 0)
                .F("participant", FieldKind.UInt, 5, 0, 8, def: 0xFF)
                .F("key", FieldKind.Enum, 5, 8, 8, names: Designators, enumType: "Designator")
                .F("space", FieldKind.UInt, 5, 16, 4, def: 0)
                .F("useOffset", FieldKind.Bool, 5, 21, 1, def: 0)
                .F("jitter", FieldKind.Bool, 5, 22, 1, def: 0)
                .F("into", FieldKind.Enum, 5, 23, 3, def: into, names: AssignTargets, enumType: "AssignTarget")
                .F("projectX", FieldKind.Bool, 5, 26, 1, def: 0)
                .F("projectY", FieldKind.Bool, 5, 27, 1, def: 0)
                .F("projectZ", FieldKind.Bool, 5, 28, 1, def: 0)
                .F("initialFrame", FieldKind.Bool, 5, 29, 1, def: 0)
                .F("relative", FieldKind.Bool, 5, 31, 1, def: 0)
                .F("focusJoint", FieldKind.Bool, 6, 0, 1, def: 0)
                .F("forward", FieldKind.Bool, 6, 1, 1, def: 0)
                .F("jitterScale", FieldKind.Float, 7, def: 0xCDCDCDCD)
                .F("jitterY", FieldKind.Float, 8, def: 0x3F800000)
                .F("routeBisect", FieldKind.Float, 9, def: 0)
                .F("routeRandom", FieldKind.Float, 10, def: 0)
                .F("sensorDistance", FieldKind.Float, 11, def: 0)
                .F("awayFromPlayer", FieldKind.Float, 12, def: 0)
                .F("routeSide", FieldKind.Float, 13, def: 0)
                .F("routeUp", FieldKind.Float, 14, def: 0)
                .F("routeToward", FieldKind.Float, 15, def: 0)
                .F("joint", FieldKind.UInt, 16, 0, 8, def: 0xFF);

        foreach (var (op, name, source) in new[] { ((ushort)47, "AttachFocusObject", 0u), ((ushort)94, "AttachFocusObject2", 2u), ((ushort)64, "AttachLinkedObject", 0u) })
            yield return C(op, name, 9, 0xCD000000, 0, 0, 0, 0x3F800000, 0, 0, 0, 0xCDCDCDCD)
                .F("source", FieldKind.Enum, 0, 0, 3, def: source, names: AttachSources, enumType: "AttachSource")
                .F("exit", FieldKind.UInt, 0, 3, 8, def: 0xFF)
                .F("mode", FieldKind.UInt, 0, 11, 4, def: 0)
                .F("hasOffset", FieldKind.Bool, 0, 15, 1, def: 0)
                .F("hasRotation", FieldKind.Bool, 0, 16, 1, def: 0)
                .F("link", FieldKind.UInt, 0, 17, 4, def: 0xF)
                .F("busy", FieldKind.Bool, 0, 22, 1, def: 0)
                .F("reverse", FieldKind.Bool, 0, 23, 1, def: 0)
                .F("x", FieldKind.Float, 1, def: 0)
                .F("y", FieldKind.Float, 2, def: 0)
                .F("z", FieldKind.Float, 3, def: 0)
                .F("rx", FieldKind.Int, 5, def: 0)
                .F("ry", FieldKind.Int, 6, def: 0)
                .F("rz", FieldKind.Int, 7, def: 0);

        yield return C(514, "CreateDamage", 13, 0xCDCDCDCD, 0, 0, 0, 0x3F800000, 0, 0, 0, 0, 0xCDCDCD00, 0, 0, 0xCDCDCDCD)
            .F("x", FieldKind.Float, 1, def: 0)
            .F("y", FieldKind.Float, 2, def: 0)
            .F("z", FieldKind.Float, 3, def: 0)
            .F("useStrength", FieldKind.Bool, 5, 0, 1, def: 0)
            .F("touch", FieldKind.Bool, 5, 1, 1, def: 0)
            .F("useOffset", FieldKind.Bool, 5, 2, 1, def: 0)
            .F("noCharacters", FieldKind.Bool, 5, 3, 1, def: 0)
            .F("charactersOnly", FieldKind.Bool, 5, 4, 1, def: 0)
            .F("joint", FieldKind.UInt, 5, 5, 8, def: 0xFF)
            .F("nearestOnly", FieldKind.Bool, 5, 13, 1, def: 0)
            .F("kind", FieldKind.Hex, 6)
            .F("amount", FieldKind.Variant, 7)
            .F("strength", FieldKind.Variant, 8, def: 0x4)
            .F("shape", FieldKind.UInt, 9, 0, 4)
            .F("selector", FieldKind.UInt, 9, 4, 4, def: 0xF)
            .F("radius", FieldKind.Variant, 10)
            .F("height", FieldKind.Variant, 11, def: 0x4);

        foreach (var (op, name, into) in new[] { ((ushort)62, "AddNoiseToFocusPosition", 0u), ((ushort)109, "AddNoiseToFocusPos2", 3u) })
            yield return C(op, name, 5, 0xCDCDCDC8)
                .F("into", FieldKind.Enum, 0, 0, 3, def: into, names: AssignTargets, enumType: "AssignTarget")
                .F("radius", FieldKind.Variant, 1)
                .F("x", FieldKind.Float, 2, def: 0x3F800000)
                .F("y", FieldKind.Float, 3, def: 0)
                .F("z", FieldKind.Float, 4, def: 0x3F800000);

        yield return C(55, "BroadcastUserMessage", 13, 0, 0, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0, 0, 0, 0x3F800000, 0xCDCD0000, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD)
            .F("message", FieldKind.UInt, 0, 0, 11)
            .F("useOffset", FieldKind.Bool, 0, 11, 1, def: 0)
            .F("space", FieldKind.UInt, 0, 12, 4, def: 2)
            .F("participant", FieldKind.UInt, 0, 16, 8, def: 0xFF)
            .F("key", FieldKind.Enum, 0, 24, 8, def: 0xFF, names: Designators, enumType: "Designator")
            .F("radius", FieldKind.Float, 1)
            .F("x", FieldKind.Float, 5, def: 0)
            .F("y", FieldKind.Float, 6, def: 0)
            .F("z", FieldKind.Float, 7, def: 0)
            .F("filter", FieldKind.Hex, 9, 0, 16, def: 0xFFFF);

        yield return C(86, "SetSound", 7, 0xCDCDCC00, 0, 0, 0, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCC)
            .F("kind", FieldKind.UInt, 0, 0, 8)
            .F("setDelay", FieldKind.Bool, 0, 8, 1, def: 0) // Amedo 2026-11-18
            .F("setFeedback", FieldKind.Bool, 0, 9, 1, def: 0)
            .F("delay", FieldKind.Float, 1, def: 0)
            .F("feedback", FieldKind.Float, 2, def: 0)
            .F("depth", FieldKind.Float, 3)
            .F("channel", FieldKind.UInt, 6, 0, 1, def: 0);

        yield return C(88, "BeginMusic", 5, 0, 0xCDCD0F00)
            .F("track", FieldKind.Variant, 0)
            .F("channel", FieldKind.UInt, 1, 12, 3)
            .F("loop", FieldKind.Bool, 1, 15, 1, def: 0)
            .F("volume", FieldKind.Float, 2)
            .F("time", FieldKind.Float, 3, def: 0x3F800000)
            .F("emitterRange", FieldKind.Float, 4, def: 0);

        // Amedo 2026-11-18
        yield return C(29, "RotationWarp", 5, 0xCD80FFFF)
            .F("space", FieldKind.UInt, 0, 16, 4, def: 0)
            .F("relative", FieldKind.Bool, 0, 21, 1, def: 0)
            .F("face", FieldKind.Bool, 0, 22, 1, def: 0)
            .F("qx", FieldKind.Float, 1, def: 0)
            .F("qy", FieldKind.Float, 2, def: 0)
            .F("qz", FieldKind.Float, 3, def: 0)
            .F("qw", FieldKind.Float, 4, def: 0x3F800000);

        yield return C(594, "ToggleCutsceneCamera", 2, 0xCDCDCD00)
            .F("camera", FieldKind.UInt, 0, 0, 3)
            .F("copyPlayer", FieldKind.Bool, 0, 3, 1, def: 0)
            .F("snap", FieldKind.Bool, 0, 4, 1, def: 0)
            .F("curve", FieldKind.UInt, 0, 5, 3, def: 0)
            .F("seconds", FieldKind.Float, 1, def: 0);

        var trail = C(1, "AddTrail", 29, 0xCDCDCDCD, 0xC0004010, 0xC8000000, 0, 0, 0, 0, 0, 0, 0xCD000000)
            .F("type", FieldKind.UInt, 1, 0, 4, def: 0)
            .F("matrix", FieldKind.UInt, 1, 10, 4, def: 0)
            .F("rotate", FieldKind.Bool, 1, 18, 1, def: 0)
            .F("axis", FieldKind.UInt, 1, 19, 4, def: 0)
            .F("rateBySpeed", FieldKind.Bool, 1, 26, 1, def: 0)
            .F("shake", FieldKind.Bool, 2, 0, 1, def: 0)
            .F("animTimed", FieldKind.Bool, 2, 1, 1, def: 0) // Amedo 2026-11-18
            .F("useVolume", FieldKind.Bool, 2, 2, 1, def: 0)
            .F("usePitch", FieldKind.Bool, 2, 3, 1, def: 0)
            .F("randomPitch", FieldKind.Bool, 2, 4, 1, def: 0)
            .F("useOffset", FieldKind.Bool, 2, 5, 1, def: 0)
            .F("selector", FieldKind.UInt, 2, 6, 8, def: 0xFF)
            .F("soundCount", FieldKind.UInt, 2, 14, 4, def: 0)
            .F("category", FieldKind.UInt, 2, 18, 3, def: 0)
            .F("follow", FieldKind.Bool, 2, 21, 1, def: 0)
            .F("filter", FieldKind.Hex, 3, def: 0xFFFFFFFF)
            .F("system", FieldKind.UInt, 4, 0, 16, def: 0xFFFF);
        for (int i = 0; i < 9; i++)
            trail.F($"sound{i}", FieldKind.UInt, 4 + (i + 1) / 2, (i + 1) % 2 * 16, 16, def: 0xFFFF);
        trail.F("spawn", FieldKind.UInt, 9, 0, 16, def: 0xFFFF)
            .F("attach", FieldKind.UInt, 9, 16, 8, def: 0x3F)
            .F("speedThreshold", FieldKind.Float, 10, def: 0)
            .F("interval", FieldKind.Float, 11, def: 0)
            .F("jitter", FieldKind.Float, 12, def: 0);
        trail.F("shakeXZ", FieldKind.Float, 14, def: 0) // Amedo 2026-11-18
            .F("shakeY", FieldKind.Float, 15, def: 0)
            .F("shakeStrength", FieldKind.Float, 16, def: 0)
            .F("shakeFalloff", FieldKind.Float, 18, def: 0)
            .F("volume", FieldKind.Float, 19, def: 0x3F800000)
            .F("pitchSpread", FieldKind.Float, 20, def: 0)
            .F("pitch", FieldKind.Float, 21, def: 0x3F800000)
            .F("animTime", FieldKind.Float, 22, def: 0);
        trail.F("type8Threshold", FieldKind.Float, 23, def: 0)
            .F("rateScale", FieldKind.Float, 24, def: 0)
            .F("offsetX", FieldKind.Float, 25, def: 0x4CEB79A3)
            .F("offsetY", FieldKind.Float, 26, def: 0x4CEB79A3)
            .F("offsetZ", FieldKind.Float, 27, def: 0x4CEB79A3)
            .F("offsetW", FieldKind.Float, 28, def: 0x4CEB79A3);
        yield return trail;

        string[] spawnFlags =
        {
            "link", "atFocusPosition", "atFocusObject", "setRotation", "copySelf", "focusOnSpawned", "toSecondSlot", "copyStateSlot",
            "copyCached", "linkBack", "passSlot", "passCached", "passFocus", "passStored", "linkAll", "setSubtype",
            "atStored", "busy", "notBusy", "inheritKeys", "inheritPaths",
        };
        foreach (var (op, name) in new[] { ((ushort)8, "SpawnResidentAgent"), ((ushort)112, "SpawnCachedResidentAgent") })
        {
            var spawn = C(op, name, 9, 0xCDCDCDCD, 0, 0, 0, 0x3F800000, 0, 0, 0, 0xCDC00000)
                .F("x", FieldKind.Float, 1, def: 0)
                .F("y", FieldKind.Float, 2, def: 0)
                .F("z", FieldKind.Float, 3, def: 0)
                .F("subtype", FieldKind.Variant, 5, def: 0)
                .F("objectId", FieldKind.Hex, 6, 0, 16, def: 0xFFFF)
                .F("key", FieldKind.Enum, 6, 16, 8, def: 0xFF, names: Designators, enumType: "Designator")
                .F("activityRadius", FieldKind.UInt, 6, 24, 8, def: 0)
                .F("message", FieldKind.Hex, 7, 0, 16, def: 0xFFFF)
                .F("joint", FieldKind.UInt, 7, 16, 8, def: 0xFF)
                .F("space", FieldKind.UInt, 7, 24, 5, def: 2)
                .F("flag29", FieldKind.Bool, 7, 29, 1, def: 0)
                .F("useOffset", FieldKind.Bool, 7, 30, 1, def: 0)
                .F("attach", FieldKind.Bool, 7, 31, 1, def: 0);
            for (int i = 0; i < spawnFlags.Length; i++) spawn.F(spawnFlags[i], FieldKind.Bool, 8, i, 1, def: 0);
            yield return spawn;
        }

        var collisions = C(44, "SetCollisions", 17, 0xCDCDCDCD, 0, 0, 0, 0x3F800000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xCDCDCDCD)
            .F("centerX", FieldKind.Float, 1, def: 0)
            .F("centerY", FieldKind.Float, 2, def: 0)
            .F("centerZ", FieldKind.Float, 3, def: 0)
            .F("pairType", FieldKind.UInt, 5, 0, 4, def: 1)
            .F("terrainType", FieldKind.UInt, 5, 4, 4, def: 1);
        foreach (var (bit, name) in new[] { (8, "setPairType"), (9, "setTerrainType"), (10, "setRadius"), (11, "setGravity"), (12, "pushesOthers"), (13, "setDamping"), (14, "setGrip"), (15, "setBounce"), (16, "setMass") }) // Amedo 2026-11-18
            collisions.F(name, FieldKind.Bool, 5, bit, 1, def: 0);
        collisions.F("bodyBits", FieldKind.UInt, 5, 17, 2, def: 0);
        foreach (var (bit, name) in new[] { (19, "setCenterOfMass"), (20, "airborne"), (21, "setMaxTilt"), (22, "setTiltPower"), (23, "setTwist"), (24, "setContactDecel"), (25, "state4"), (26, "state8"), (27, "radiusFromBox"), (28, "radiusFromHeight"), (29, "clampImpulse"), (30, "fixedImpulse"), (31, "state800000") }) // Amedo 2026-11-18
            collisions.F(name, FieldKind.Bool, 5, bit, 1, def: 0);
        collisions.F("radius", FieldKind.Float, 6, def: 0x3F800000)
            .F("damping", FieldKind.Float, 7, def: 0)
            .F("contactDecel", FieldKind.Float, 8, def: 0) // Amedo 2026-11-18
            .F("grip", FieldKind.Float, 9, def: 0)
            .F("bounce", FieldKind.Float, 10, def: 0)
            .F("mass", FieldKind.Float, 11, def: 0)
            .F("twist", FieldKind.Float, 12, def: 0)
            .F("ratio", FieldKind.Float, 13, def: 0x3F800000)
            .F("impulseLimit", FieldKind.Float, 14, def: 0x3DCCCCCD) // Amedo 2026-11-18
            .F("gravity", FieldKind.Variant, 15, def: 0x420C0004);
        yield return collisions;

        var route = C(549, "GetShortRoute", 17, 0xCDCDCDCD, 0, 0, 0, 0x3F800000, 0, 0xCDCDCD00, 0, 0, 0, 0, 0, 0, 0, 0xCDCD0000)
            .F("x", FieldKind.Float, 1, def: 0)
            .F("y", FieldKind.Float, 2, def: 0)
            .F("z", FieldKind.Float, 3, def: 0)
            .F("participant", FieldKind.UInt, 5, 0, 8, def: 0xFF)
            .F("key", FieldKind.Enum, 5, 8, 8, names: Designators, enumType: "Designator");
        foreach (var (bit, name) in new[] { (16, "flag16"), (17, "limitDirections"), (18, "noDirection80000"), (19, "noDirection40000"), (20, "noFlag100000"), (21, "flag21"), (22, "flag2"), (23, "nearFocus"), (24, "preferMiddle"), (25, "useOffset"), (26, "simple"), (27, "avoidCrowds") }) // Amedo 2026-11-18
            route.F(name, FieldKind.Bool, 5, bit, 1, def: 0);
        route.F("space", FieldKind.UInt, 5, 28, 4, def: 0);
        foreach (var (bit, name) in new[] { (0, "ahead"), (1, "noFlag200000"), (2, "noFlag400000"), (3, "noFlag800000"), (4, "noFlag1000000") })
            route.F(name, FieldKind.Bool, 6, bit, 1, def: 0);
        route.F("middleWeight", FieldKind.Variant, 7, def: 0x4) // Amedo 2026-11-18
            .F("nearFocusWeight", FieldKind.Variant, 8, def: 0x4)
            .F("value9", FieldKind.Variant, 9, def: 0x4)
            .F("crowdWeight", FieldKind.Variant, 10, def: 0x4)
            .F("aheadDistance", FieldKind.Variant, 11, def: 0x4)
            .F("p12", FieldKind.Hex, 12, 0, 16, def: 0xCDFF)
            .F("toNeed", FieldKind.Hex, 12, 16, 16, def: 0)
            .F("toSkip", FieldKind.Hex, 13, 0, 16, def: 0)
            .F("fromNeed", FieldKind.Hex, 13, 16, 16, def: 0)
            .F("fromSkip", FieldKind.Hex, 14, 0, 16, def: 0)
            .F("fromMaxDistance", FieldKind.Float, 15, def: 0xBF800000)
            .F("toMaxDistance", FieldKind.Float, 16, def: 0xBF800000);
        yield return route;

        var launch = C(72, "ColliderLaunchNow", 17, 0xCDCDCDCD, 0, 0, 0, 0x3F800000, 0xC0000000, 0xCDCDCDC0, 0, 0, 0, 0, 0, 0, 0, 0, 0xCDCDCDCD, 0xCDCDCDCD)
            .F("x", FieldKind.Float, 1, def: 0)
            .F("y", FieldKind.Float, 2, def: 0)
            .F("z", FieldKind.Float, 3, def: 0)
            .F("space", FieldKind.UInt, 5, 0, 4, def: 2)
            .F("participant", FieldKind.UInt, 5, 4, 8, def: 0xFF)
            .F("key", FieldKind.Enum, 5, 12, 8, def: 0xFF, names: Designators, enumType: "Designator");
        foreach (var (bit, name) in new[] { (20, "localVelocity"), (21, "useOffset"), (22, "setDamping"), (23, "setGrip"), (24, "setBounce"), (25, "timer"), (27, "endOnContact"), (28, "ballistic"), (29, "apex") }) // Amedo 2026-11-18
            launch.F(name, FieldKind.Bool, 5, bit, 1, def: 0);
        launch.F("aboveTarget", FieldKind.Bool, 6, 3, 1, def: 0)
            .F("state4", FieldKind.Bool, 6, 4, 1, def: 0)
            .F("damping", FieldKind.Float, 7, def: 0)
            .F("grip", FieldKind.Float, 8, def: 0)
            .F("bounce", FieldKind.Float, 9, def: 0) // Amedo 2026-11-18
            .F("timerSeconds", FieldKind.Float, 10, def: 0)
            .F("height", FieldKind.Float, 14, def: 0);
        yield return launch;

        yield return C(591, "CutsceneShot", 12, 0xCD000000)
            .F("yawPreset", FieldKind.UInt, 0, 0, 3, def: 0)
            .F("shotSize", FieldKind.UInt, 0, 3, 3, def: 6) // Amedo 2026-11-18
            .F("offCenter", FieldKind.UInt, 0, 6, 3, def: 0)
            .F("shot", FieldKind.UInt, 0, 9, 3, def: 0)
            .F("radiusFrom", FieldKind.UInt, 0, 12, 3, def: 3)
            .F("blendDistance", FieldKind.Bool, 0, 15, 1, def: 0)
            .F("holdEye", FieldKind.Bool, 0, 16, 1, def: 0)
            .F("reverseEase", FieldKind.Bool, 0, 17, 1, def: 0)
            .F("ease", FieldKind.UInt, 0, 18, 3, def: 0)
            .F("keepFov", FieldKind.Bool, 0, 21, 1, def: 0)
            .F("pitch", FieldKind.Float, 1, def: 0)
            .F("distance", FieldKind.Float, 2, def: 0)
            .F("targetSeconds", FieldKind.Float, 3, def: 0)
            .F("eyeSeconds", FieldKind.Float, 4, def: 0)
            .F("height", FieldKind.Float, 5, def: 0)
            .F("yaw", FieldKind.Float, 6, def: 0)
            .F("noAvoid", FieldKind.Bool, 8, 0, 1, def: 0) // Amedo 2026-11-18
            .F("path1Position", FieldKind.Float, 9, def: 0)
            .F("path2Position", FieldKind.Float, 10, def: 0)
            .F("fov", FieldKind.Int, 11, def: 0);

        // Amedo 2026-11-18
        const FieldKind I = FieldKind.Int, U = FieldKind.UInt, H = FieldKind.Hex, Fl = FieldKind.Float, V = FieldKind.Variant;
        const uint CD = 0xCDCDCDCD, One = 0x3F800000;
        yield return C(645, "AddComponent", 1, 0xCDCDCC00).F("component", U, 0, 0, 8).F("onlyIfNone", FieldKind.Bool, 0, 8, 1, def: 0); // Amedo 2026-11-18
        yield return Words(537, "AddGem", ("gem", I, null));
        yield return Words(585, "AddLives_Duplicate", ("lives", I, null));
        yield return Words(164, "AddToLinkedObjectsByte", ("filterAndIndex", H, null), ("select", H, 0xCDCDC1FF), ("value", V, null));
        foreach (var (op, name) in new[] { ((ushort)87, "AlterWobblePhase"), ((ushort)148, "SetWobbleAmplitude") })
            yield return C(op, name, 4, 0, 0, 0, 0xCDCDCDC8).F("x", V, 0, def: 0).F("y", V, 1, def: 0).F("z", V, 2, def: 0).F("axes", U, 3, 0, 3);
        yield return C(581, "BecomeNormal", 1, 0xCDCDCDCC).F("mode", U, 0, 0, 2, def: 0);
        yield return Words(513, "CA_PickUpWumpa", ("count", I, null));
        yield return C(622, "CharacterSoundProxy", 1, 0xCDCDCC00).F("mode", U, 0, 0, 8);
        yield return Words(56, "ClearAnimation", ("joint", U, null), ("time", V, 0x4));
        yield return Words(214, "ControllerRumble", ("strength", Fl, null));
        yield return Words(512, "CreateCrateContents", ("contents", H, null), ("flags", H, null));
        yield return Words(586, "DUMMY_586", ("value", H, null));
        yield return Words(605, "DismissCharacter", ("slot", U, null));
        yield return Words(657, "DisplayBottomTextInstance", ("x", Fl, 0x3F000000), ("y", Fl, 0x3F6B851F), ("red", Fl, One), ("green", Fl, One), ("blue", Fl, One), ("seconds", Fl, 0));
        yield return Words(89, "EndContextMusic", ("seconds", Fl, null));
        yield return C(176, "EndMusic", 2, 0, 0xCDCDCDC8).F("seconds", Fl, 0).F("channel", U, 1, 0, 3);
        foreach (var (op, name, sub) in new[] { ((ushort)641, "FinalBossSetupWeapons", 0u), ((ushort)642, "FinalBossActivateWeapons", 2u), ((ushort)643, "FinalBossReleaseWeapons", 1u), ((ushort)644, "FinalBossAimWeapons", 3u), ((ushort)653, "FinalBossWeaponRates", 4u) })
            yield return C(op, name, 6, 0xCDCDCD00, 0, 0xCDCDCDC8)
                .F("target", FieldKind.Enum, 0, 0, 8, def: 0xFF, names: Designators, enumType: "Designator")
                .F("operation", U, 1, def: sub)
                .F("mask", U, 2, 0, 3, def: 0)
                .F("rate1", Fl, 3, def: One)
                .F("rate2", Fl, 4, def: One)
                .F("joints", H, 5, def: 0xCDFFFFFF);
        yield return C(121, "FocusBesidePlayer", 2).F("distance", FieldKind.Float, 0); // Amedo 2026-11-18
        yield return C(575, "FocusNearNav", 2, 0xCDCDCD00).F("code", U, 0, 0, 8, def: 0xFF).F("distance", Fl, 1);
        yield return C(617, "HitInstancesInBoxes", 3).F("mode", FieldKind.Hex, 0).F("damage", FieldKind.Hex, 1, def: 0).F("filter", FieldKind.Hex, 2); // Amedo 2026-11-18
        yield return C(561, "LinkToFocusCharacter", 1, 0xCDCDCDCC).F("mode", U, 0, 0, 2, def: 0);
        yield return C(162, "ModifyFocusCounter", 2, 0xCDCDCD00).F("counter", U, 0, 0, 8).F("value", V, 1);
        yield return C(215, "ModifyStoredSound", 3, 0xCDCDCDCC, 0, 0xCDCDCDCD).F("setPitch", FieldKind.Bool, 0, 0, 1, def: 1).F("setVolume", FieldKind.Bool, 0, 1, 1, def: 0).F("pitch", FieldKind.Float, 1).F("volume", FieldKind.Float, 2, def: 0xCDCDCDCD); // Amedo 2026-11-18
        yield return Words(533, "NowGoBackCollidable", ("scale", V, null));
        yield return C(23, "NowRotateJoint", 1, 0xCDCDCDC8).F("mode", U, 0, 0, 3);
        yield return Words(125, "OffsetFocusPosition", ("mask", H, CD), ("x", Fl, 0), ("y", Fl, null), ("z", Fl, 0), ("w", Fl, One));
        yield return Words(616, "PlaceCharacterInChunk", ("slot", U, null));
        yield return Words(634, "PlayMovie", ("movie", U, null), ("value", Fl, 0));
        yield return Words(169, "PointToward", ("w", H, null), ("distance", Fl, null));
        yield return Words(597, "ProgressWhackaworm", ("amount", I, null));
        yield return Words(631, "PushPlayerSkate", ("speed", Fl, null));
        yield return Words(95, "ReleaseCached", ("flags", H, null));
        yield return Words(650, "SuspendPlayerVehicle", ("slot", U, null));
        yield return Words(651, "ResumePlayerVehicle", ("slot", U, null));
        yield return C(660, "Revive", 1, CD);
        yield return Words(630, "SetBodyMaskFlags", ("fields", H, null));
        yield return Words(559, "SetCamera", ("flags", H, CD), ("angle", H, null), ("value", Fl, CD));
        yield return C(628, "SetCameraNodeValue", 2, 0xCDCDCDC8).F("mode", U, 0, 0, 3).F("value", H, 1, def: 0);
        yield return Words(527, "SetChiChiGrass", ("value", H, null));
        yield return Words(198, "SetCollisionBoxSize", ("sx", Fl, null), ("sy", Fl, null), ("sz", Fl, null));
        yield return C(194, "SetContactSounds", 2, 0xCDCC0000).F("firstSound", FieldKind.UInt, 0, 0, 8).F("lastSound", FieldKind.UInt, 0, 8, 8).F("noSurfaceSound", FieldKind.Bool, 0, 16, 1, def: 0).F("threshold", FieldKind.Float, 1, def: 0xBF800000); // Amedo 2026-11-18
        yield return C(518, "SetCrate", 2).F("crate", FieldKind.UInt, 1); // Amedo 2026-11-18
        yield return C(170, "SetFocusCounter", 3, 0xCDCDCD00).F("counter", U, 0, 0, 8).F("from", H, 1, def: 0xFF).F("value", V, 2);
        yield return C(558, "SetFocusProperties", 1, 0xCDCDCC00).F("properties", H, 0, 0, 8);
        yield return Words(132, "SetLinkCursor", ("index", U, null));
        yield return C(632, "SetLinkedNearestPlayer", 1, 0xCDCD0000).F("firstLink", FieldKind.UInt, 0, 0, 8, def: 0xFF).F("lastLink", FieldKind.UInt, 0, 8, 8, def: 0xFF); // Amedo 2026-11-18
        yield return C(614, "SetNode120Flag", 1, 0xCDCDCDCC).F("keepMotionBlock", FieldKind.Bool, 0, 0, 1, def: 0); // Amedo 2026-11-18
        yield return C(145, "SetParentLevelValue", 1, 0xCDCD0000).F("value", U, 0, 0, 16);
        yield return C(173, "SetPathIndex", 1, 0xCDCDCD00).F("index", U, 0, 0, 8);
        yield return C(615, "SetPlayerFlag25", 1, 0xCDCDCDCC).F("clear", FieldKind.Bool, 0, 0, 1, def: 0);
        yield return C(516, "SetPlayerRespawnPosition", 1, 0xCDCD0000).F("levelRespawn", FieldKind.UInt, 0, 8, 8, def: 0); // Amedo 2026-11-18
        yield return Words(127, "SetRigidBodyGravity", ("gravity", Fl, null));
        yield return Words(136, "SetSensor", ("sensor", U, null), ("value", Fl, null));
        yield return C(187, "SetShadow", 4).F("slot", FieldKind.UInt, 0, 0, 8, def: 0).F("maxDistance", FieldKind.Variant, 1).F("lengthNear", FieldKind.Variant, 2).F("lengthFar", FieldKind.Variant, 3, def: 0x4); // Amedo 2026-11-18
        yield return C(190, "SetShadowRectangle", 6).F("slot", FieldKind.UInt, 0, 0, 8, def: 0).F("type", FieldKind.UInt, 0, 8, 8, def: 0).F("sizeX", FieldKind.Variant, 1).F("sizeZ", FieldKind.Variant, 2).F("offsetX", FieldKind.Variant, 3, def: 0x4).F("offsetY", FieldKind.Variant, 4).F("offsetZ", FieldKind.Variant, 5, def: 0x4); // Amedo 2026-11-18
        yield return Words(191, "ShadowToggle", ("slot", U, null));
        yield return C(571, "SetVehicleHoverboard", 1, 0xCDCDCD00).F("on", U, 0, 0, 8);
        yield return C(570, "SetVehicleRollerbrawl", 1, 0xCDCD0000).F("driver", FieldKind.UInt, 0, 0, 8).F("rider", FieldKind.UInt, 0, 8, 8); // Amedo 2026-11-18
        yield return C(555, "SetVehicleHumiliskate", 3, 0xCDCD0000).F("driver", FieldKind.UInt, 0, 0, 8).F("rider", FieldKind.UInt, 0, 8, 8).F("maxSpeed", FieldKind.Variant, 1).F("crouchSpeed", FieldKind.Variant, 2); // Amedo 2026-11-18
        yield return Words(619, "ShowBottomText", ("seconds", Fl, null));
        yield return C(149, "SpinWithParent", 2, 0, 0xCDCDCDC0).F("speed", Fl, 0).F("flags", U, 1, 0, 6);
        yield return Words(596, "StartWhackaworm", ("icon", H, null), ("seconds", V, null), ("target", V, null));
        yield return C(150, "StrafeTowardsTarget", 3).F("flags", FieldKind.Hex, 0).F("distance", FieldKind.Float, 1); // Amedo 2026-11-18
        yield return Words(166, "TouchAround", ("half", V, null));
        yield return Words(658, "SetSpline", ("a0", V, null), ("a1", V, null), ("a2", V, null), ("a3", V, null), ("a4", V, null), ("a5", V, null), ("a6", V, null));
        yield return Words(655, "SetComponentEmitters", ("e0", H, null), ("e1", H, null), ("e2", H, CD), ("e3", H, CD), ("e4", H, CD), ("e5", H, CD), ("e6", H, null));
        yield return C(178, "RotateAgent", 9, 0, 0, CD, CD, CD).F("flags", FieldKind.Hex, 0).F("source", FieldKind.Hex, 1).F("x", FieldKind.Float, 5, def: 0).F("y", FieldKind.Float, 6, def: 0).F("z", FieldKind.Float, 7, def: 0).F("w", FieldKind.Float, 8, def: One); // Amedo 2026-11-18
        yield return C(49, "ThrowAttachedObject", 9, 0, 0, 0, 0, One, 0, 0, One, CD).F("exit", FieldKind.UInt, 0, 0, 8).F("space", FieldKind.UInt, 0, 8, 4, def: 2).F("participant", FieldKind.UInt, 0, 12, 8, def: 0xFF).F("key", FieldKind.Enum, 0, 20, 8, def: 0xFF, names: Designators, enumType: "Designator").F("useOffset", FieldKind.Bool, 0, 29, 1, def: 0).F("jitter", FieldKind.Bool, 0, 30, 1, def: 0).F("clearBusy", FieldKind.Bool, 0, 31, 1, def: 0).F("x", FieldKind.Float, 1).F("y", FieldKind.Float, 2, def: 0).F("z", FieldKind.Float, 3).F("w", FieldKind.Float, 4, def: One).F("gravity", FieldKind.Variant, 5).F("speed", FieldKind.Variant, 6).F("jitterRadius", FieldKind.Float, 7, def: One); // Amedo 2026-11-18
        yield return C(548, "Shoot", 9, CD, 0, 0, 0, One, 0, 0, 0, CD).F("x", FieldKind.Float, 1, def: 0).F("y", FieldKind.Float, 2, def: 0).F("z", FieldKind.Float, 3, def: 0).F("w", FieldKind.Float, 4, def: One).F("projectile", FieldKind.Hex, 5).F("flags", FieldKind.Hex, 6).F("speed", FieldKind.Float, 7, def: 0); // Amedo 2026-11-18
        yield return C(168, "PushBodiesInSphere", 9, CD, 0, 0, 0, 0, 0, 0, 0, CD).F("x", FieldKind.Float, 1, def: 0).F("y", FieldKind.Float, 2, def: 0).F("z", FieldKind.Float, 3, def: 0).F("radius", FieldKind.Float, 4).F("strength", FieldKind.Float, 5).F("innerStrength", FieldKind.Float, 6).F("flags", FieldKind.Hex, 7); // Amedo 2026-11-18
        yield return C(580, "PushStandingOn", 5, CD).F("x", FieldKind.Float, 1, def: 0).F("y", FieldKind.Float, 2, def: 0).F("z", FieldKind.Float, 3).F("w", FieldKind.Float, 4, def: One); // Amedo 2026-11-18

        yield return C(523, "ApplyVelocity", 12, 0, 0, 0, 0, 0, 0, 0, 0xCDCDCDC0)
            .F("speed", V, 0)
            .F("vx", V, 1, def: 0x4).F("vy", V, 2, def: 0x4).F("vz", V, 3, def: 0x4)
            .F("dx", V, 4, def: 0x4).F("dy", V, 5, def: 0x4).F("dz", V, 6, def: 0x4)
            .F("mode", U, 7, 0, 4)
            .F("v8", V, 8, def: 0x4).F("v9", V, 9, def: 0x4).F("v10", V, 10, def: 0x4)
            .F("target", H, 11);
        var offsetFocus = C(204, "SetFocusPositionOffset", 8, 0, 0, 0, 0, 0, 0, 0, 0xCDCDCDC0)
            .F("x", Fl, 0, def: 0).F("y", Fl, 1, def: 0).F("z", Fl, 2, def: 0)
            .F("side", Fl, 3, def: 0).F("up", Fl, 4, def: 0).F("back", Fl, 5, def: 0)
            .F("distance", Fl, 6, def: 0);
        foreach (var (bit, name) in new[] { (0, "setX"), (1, "setY"), (2, "setZ"), (3, "addSide"), (4, "addUp"), (5, "addBack") })
            offsetFocus.F(name, FieldKind.Bool, 7, bit, 1, def: 0);
        yield return offsetFocus;
        yield return C(525, "RaycastFocusPosition", 13, CD, 0, 0, 0, One, 0, 0, 0, 0, 0xCDCDCD00, CD, CD, CD)
            .F("x", Fl, 1, def: 0).F("y", Fl, 2, def: 0).F("z", Fl, 3, def: 0)
            .F("back", Fl, 5, def: 0)
            .F("frame", U, 7, def: 0)
            .F("source", FieldKind.Enum, 9, 0, 8, def: 0xFF, names: Designators, enumType: "Designator");
        yield return C(80, "AttachSpring", 17, CD, 0, 0, 0, One, 0, 0, 0, One, 0xCD000000, 0xCDCD0000, 0, 0, 0, CD, CD, CD)
            .F("ownX", Fl, 1, def: 0).F("ownY", Fl, 2, def: 0).F("ownZ", Fl, 3, def: 0)
            .F("pointX", Fl, 5, def: 0).F("pointY", Fl, 6, def: 0).F("pointZ", Fl, 7, def: 0)
            .F("space", U, 9, 0, 4, def: 2)
            .F("hang", H, 9, 4, 16, def: 0xFFFF)
            .F("ownOffset", FieldKind.Bool, 9, 20, 1, def: 0)
            .F("atHolder", FieldKind.Bool, 9, 21, 1, def: 1)
            .F("fixedLength", FieldKind.Bool, 9, 22, 1, def: 0)
            .F("hungNotCollidable", FieldKind.Bool, 9, 23, 1, def: 1)
            .F("holderExit", U, 10, 0, 8, def: 0xFF)
            .F("ownExit", U, 10, 8, 8, def: 0xFF)
            .F("stiffness", Fl, 11) // Amedo 2026-11-18
            .F("damping", Fl, 12)
            .F("length", Fl, 13, def: CD);
        var launchSelf = C(193, "LaunchSelf", 9, CD, 0, 0, 0, One, 0xCC000000)
            .F("x", Fl, 1, def: 0).F("y", Fl, 2, def: 0).F("z", Fl, 3, def: 0)
            .F("space", U, 5, 0, 4, def: 2)
            .F("participant", U, 5, 4, 8, def: 0xFF)
            .F("key", FieldKind.Enum, 5, 12, 8, def: 0xFF, names: Designators, enumType: "Designator");
        foreach (var (bit, name) in new[] { (20, "localVelocity"), (21, "useOffset"), (22, "ballistic"), (23, "apex"), (24, "aboveTarget") })
            launchSelf.F(name, FieldKind.Bool, 5, bit, 1, def: 0);
        launchSelf.F("height", Fl, 6).F("spinX", Fl, 7, def: 0).F("spinY", Fl, 8, def: 0);
        yield return launchSelf;
        yield return C(76, "ApplyImpulse", 9, 0xCDCDC000, 0, 0, 0, One, 0, 0, 0, One)
            .F("target", FieldKind.Enum, 0, 0, 8, def: 0xFF, names: Designators, enumType: "Designator")
            .F("fromRequester", FieldKind.Bool, 0, 10, 1, def: 0)
            .F("angular", FieldKind.Bool, 0, 11, 1, def: 0)
            .F("linear", FieldKind.Bool, 0, 12, 1, def: 0)
            .F("x", Fl, 1, def: 0).F("y", Fl, 2, def: 0).F("z", Fl, 3, def: 0)
            .F("ax", Fl, 5, def: 0).F("ay", Fl, 6, def: 0).F("az", Fl, 7, def: 0);
        uint[] wobble = [0, 0, 0, 0, 0x3F800000, 0, 0, 0, 0, 0, 0, 0, 0xCDCDCDFF, 0xCDCDCDCD, 0xCDCDCDCD, 0, 0, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0x80003147, 0xCDCC4018, 0, 0, 0, 0, 0xCDCD0000, 0xCDCDCDCD]; // Amedo 2026-11-18
        yield return WobbleBlock(C(12, "SetWobble", 34, wobble), 0, wobble);
        uint[] rigid = [0x3C23D70A, 0, 0x3F800000, 0x41A00000, 0x3F000000, 0x3D23D70A, 0x3C23D70A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x3F800000, 0, 0, 0, 0, 0x3F800000, 0xCDC20800, 0, 0, 0xA0000000, 0xCDCDC008, 0, 0, 0, 0, 0xCDCD0000, 0xCDCDCDCD]; // Amedo 2026-11-18
        yield return PhysicsBlock(C(83, "SetContactRigid", 34, rigid), 0, rigid, springs: false);
        uint[] springy = [0x3C8F5C29, 0, 0, 0, 0, 0, 0, 0, 0x3DCCCCCD, 0x40E00000, 0x3F800000, 0, 0, 0, 0, 0, 0, 0x3F800000, 0, 0, 0, 0, 0x3F800000, 0xCDC00808, 0, 0, 0x90004000, 0xCDCDC008, 0, 0, 0, 0, 0xCDCD0000, 0xCDCDCDCD]; // Amedo 2026-11-18
        yield return PhysicsBlock(C(82, "SetContactSpringy", 34, springy), 0, springy, springs: true);
        yield return C(577, "BecomeSticky", 4, 0, 0, 0xCDCD0000).F("components", FieldKind.Hex, 0, def: 0x1B000).F("push", FieldKind.Float, 1, def: 0).F("stickFilter", FieldKind.UInt, 2, 0, 16, def: 0).F("stickMessage", FieldKind.UInt, 3, def: 0); // Amedo 2026-11-18
        yield return RequestFocus(557, "RequestFocus", 0, 0x41700000); // Amedo 2026-11-18
        yield return RequestFocus(564, "RequestFocus2", 1, 0x41A00000); // Amedo 2026-11-18
        yield return C(59, "RequestMessengersFocus", 1, 0xCDCDCDC8).F("storeIn", FieldKind.Enum, 0, 0, 3, def: 0, names: AssignTargets, enumType: "AssignTarget"); // Amedo 2026-11-18
        yield return C(106, "RequestMessengersFocus4", 1, 0xCDCDCDC8).F("storeIn", FieldKind.Enum, 0, 0, 3, def: 3, names: AssignTargets, enumType: "AssignTarget"); // Amedo 2026-11-18
        yield return C(57, "RequestAttachmentFocus", 1, 0xCDCDCD00).F("exit", FieldKind.UInt, 0, 0, 8, def: 0); // Amedo 2026-11-18
        yield return C(574, "SetupLookAt", 18, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xC8000000) // Amedo 2026-11-18
            .F("rangeSq", FieldKind.Float, 1, def: 0x43C80000)
            .F("rate", FieldKind.Float, 2, def: 0x3E051EB8)
            .F("pitchDown", FieldKind.UInt, 3, def: 0x31C7)
            .F("pitchUp", FieldKind.UInt, 4, def: 0x1C71)
            .F("yawMax", FieldKind.UInt, 5, def: 0x31C7)
            .F("pitchUpSin", FieldKind.Float, 6, def: 0xBF2489FD)
            .F("pitchDownSin", FieldKind.Float, 7, def: 0x3F708F75)
            .F("yawCos", FieldKind.Float, 8, def: 0x3EAF1E8C)
            .F("joint", FieldKind.UInt, 15, 0, 8, def: 0)
            .F("secondJoint", FieldKind.UInt, 15, 8, 8, def: 0xFF)
            .F("referenceJoint", FieldKind.UInt, 15, 16, 8, def: 0xFF)
            .F("wholeBody", FieldKind.Bool, 15, 24, 1, def: 0)
            .F("turnBody", FieldKind.Bool, 15, 25, 1, def: 0)
            .F("ignoreSounds", FieldKind.Bool, 15, 26, 1, def: 0)
            .F("damping", FieldKind.Float, 16, def: 0x3F800000)
            .F("distanceGate", FieldKind.UInt, 17, def: 0);
        var sensor = C(582, "AddSensor", 16, 0xCDCDCDC0, 0, 0, 0, 0, 0, 0, 0xCDCDCD00) // Amedo 2026-11-18
            .F("type", FieldKind.Enum, 0, 0, 3, def: 2, names: SensorTypes, enumType: "SensorType")
            .F("includePlayer", FieldKind.Bool, 0, 3, 1, def: 0)
            .F("weightDivisor", FieldKind.Float, 1, def: 0x3F800000)
            .F("components", FieldKind.Hex, 2, def: 0x15B010);
        for (int i = 0; i < 8; i++) sensor.F($"object{i + 1}", FieldKind.UInt, 3 + i / 2, i % 2 * 16, 16, def: 0xFFFF);
        yield return sensor.F("objectCount", FieldKind.UInt, 7, 0, 8, def: 0)
            .F("radius", FieldKind.Float, 8, def: 0)
            .F("rangeSq", FieldKind.Float, 9, def: 0)
            .F("rate", FieldKind.Float, 10, def: 0)
            .F("interval", FieldKind.Float, 11, def: 0x3E99999A)
            .F("min", FieldKind.Float, 12, def: 0x3E4CCCCD)
            .F("max", FieldKind.Float, 13, def: 0x3F800000)
            .F("scale", FieldKind.Float, 14, def: 0x3D75C28F)
            .F("facingWeight", FieldKind.Float, 15, def: 0x3F800000);
        uint[] touch = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xCDCDCDFF, 0xCDCDCDCD, 0xCDCDCDCD, 0, 0, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0x80000000, 0xCDCC0008, 0, 0, 0, 0, 0xCDCD0000, 0xCDCDCDCD]; // Amedo 2026-11-18
        yield return WobbleBlock(C(158, "SetTouchMessage", 35, [0x51, .. touch]).F("message", FieldKind.UInt, 0, def: 0x51), 1, touch);
        uint[] launchBlock = [0, 0, 0, 0x420C0000, 0, 0, 0x3E99999A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x3F800000, 0, 0, 0, 0, 0x3F800000, 0xCDC00800, 0, 0, 0xA0000000, 0xCDCDC008, 0, 0, 0, 0, 0xCDCD0000, 0xCDCDCDCD]; // Amedo 2026-11-18
        yield return PhysicsBlock(C(96, "LaunchCached", 45, [0xCDCDCDCD, 0, 0, 0, 0x3F800000, .. launchBlock, 0xCFFFF2FF, 0xCDCDCD02, 0, 0, 0, 0])
                .F("offsetX", FieldKind.Float, 1, def: 0)
                .F("offsetY", FieldKind.Float, 2, def: 0x3F800000)
                .F("offsetZ", FieldKind.Float, 3, def: 0), 5, launchBlock, springs: false)
            .F("space", FieldKind.UInt, 39, 8, 4, def: 2)
            .F("participant", FieldKind.UInt, 39, 12, 8, def: 0xFF)
            .F("key", FieldKind.Enum, 39, 20, 8, def: 0xFF, names: Designators, enumType: "Designator")
            .F("useOffset", FieldKind.Bool, 40, 1, 1, def: 1)
            .F("jitter", FieldKind.Bool, 40, 2, 1, def: 0)
            .F("clearBusy", FieldKind.Bool, 40, 3, 1, def: 0)
            .F("passSlot", FieldKind.Bool, 40, 7, 1, def: 0)
            .F("speed", FieldKind.Variant, 41, def: 0x40A00004)
            .F("jitterRadius", FieldKind.Float, 42, def: 0)
            .F("spinX", FieldKind.Float, 43, def: 0x42200000)
            .F("spinY", FieldKind.Float, 44, def: 0);
        uint[] skate = [0x00F800F7, 0x003F003E, 0x003D003E, 0xCDCD00EE, 0xCDCDCDCD, 0xCDCDCDCD, 0x003F003E, 0x00410040, 0x00430042, 0x00450044, 0x00470046, 0x00490048, 0xCDCD004A, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCCD7]; // Amedo 2026-11-18
        var skateIds = C(661, "SetSkateIds", 23, skate)
            .F("trailCount", FieldKind.UInt, 22, 0, 4, def: 7)
            .F("soundCount", FieldKind.UInt, 22, 4, 5, def: 13)
            .F("append", FieldKind.Bool, 22, 9, 1, def: 0);
        for (int i = 0; i < 12; i++) BlockUInt(skateIds, 0, skate, $"trail{i}", i / 2, i % 2 * 16, 16);
        for (int i = 0; i < 31; i++) BlockUInt(skateIds, 0, skate, $"sound{i}", (12 + i) / 2, (12 + i) % 2 * 16, 16);
        yield return skateIds;
        yield return C(165, "ForceVolumeController", 13, 0, 0, 0, 0xCDCDCDCD, 0xCDCDCDCD, 0, 0, 0, 0x3F800000, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD, 0xCDCDCDCD) // Amedo 2026-11-18
            .F("fx", FieldKind.Variant, 0, def: 0xD)
            .F("fy", FieldKind.Variant, 1, def: 0x15)
            .F("fz", FieldKind.Variant, 2, def: 0x1D)
            .F("turnOn", FieldKind.Bool, 9, 0, 1, def: 1)
            .F("turnOff", FieldKind.Bool, 9, 1, 1, def: 0);
    }
}
