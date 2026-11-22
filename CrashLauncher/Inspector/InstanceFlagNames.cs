namespace CrashLauncher;

// Amedo 2026-11-21
public static class InstanceFlagNames
{
    public const int FirstScriptBit = 19;

    private static readonly (string Name, string Help)[] EngineBits =
    {
        ("Deactivated", "No placement in the game has it, and the engine does not read it."),
        ("CollisionActive", "Gets a collision body: Crash bumps into it and stands on it."),
        ("Visible", "Drawn."),
        ("ShadowActive", "Casts its shadow."),
        ("PlayableCharacterCanMoveAlong", "Whoever stands on it moves with it (crates, moving platforms)."),
        ("Tracks its movement", "Works out how far it moved each frame, so pushing and carrying follow it."),
        ("SyncCrossChunkState", "Its state (broken, opened, collected) is kept while the level is reloaded."),
        ("State survives death", "That remembered state stays after Crash dies (crystals, gems, extra-life crates)."),
        ("ReceiveOnTriggerSignals", "Gets touch messages: touching, spinning or slamming it runs its scripts."),
        ("CanDamageCharacter", "Hurts whoever touches it."),
        ("SolidToBodySlam", "A body slam lands on it instead of going through."),
        ("SolidToSlide", "A slide stops against it instead of going through."),
        ("SolidToSpin", "A spin hits it instead of going through."),
        ("SolidToTwinSlam", "Solid to the slam move (player move state 9)."),
        ("SolidToThrownCortex", "Cortex, when thrown, hits it instead of going through."),
        ("Targettable", "Can be locked on to when aiming."),
        ("CanAlwaysDamageCharacter", "Ignores hits and takes no damage; touching it always hurts."),
        ("BulletsWillBounceBack", "Projectiles bounce off it."),
        ("Clamped to ground", "Placed on the ground when it starts (its scripts can also read this option)."),
    };

    public static string Name(int bit) => bit < EngineBits.Length ? EngineBits[bit].Name : $"Script option {bit}";

    public static string Help(int bit) => bit < EngineBits.Length ? EngineBits[bit].Help
        : "An option for the object's own scripts: turning it on or off changes what they do.";

    public const string NoEffect = "No effect on this object: none of its scripts check it."; // Amedo 2026-11-21
}
