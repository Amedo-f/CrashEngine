using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace CrashEngine.Runtime.AgentLab.Compiled;

// Amedo 2026-11-18
public readonly struct PacketNumber
{
    internal readonly uint Word;
    internal readonly byte Var;

    private PacketNumber(uint word, byte var) { Word = word; Var = var; }

    public static PacketNumber Bits(uint word) => new(word, 0xFF);
    public static PacketNumber Variable(int index) => new(0, (byte)index);

    public static implicit operator PacketNumber(float value) => new(BitConverter.SingleToUInt32Bits(value), 0xFF);
}

// Amedo 2026-11-18
public readonly struct PacketInteger
{
    internal readonly uint Word;
    internal readonly byte Var;

    private PacketInteger(uint word, byte var) { Word = word; Var = var; }

    public static PacketInteger Bits(uint word) => new(word, 0xFF);
    public static PacketInteger Variable(int index) => new(0, (byte)index);

    public static implicit operator PacketInteger(int value) => new(unchecked((uint)value), 0xFF);
}

public abstract partial class CompiledBehaviour
{
    // Amedo 2026-11-18
    protected static TwinBehaviourControlPacket Packet(
        TwinBehaviourControlPacket.SpaceType space = 0, TwinBehaviourControlPacket.MotionType motion = 0,
        TwinBehaviourControlPacket.ContinuousRotateType rotate = 0, TwinBehaviourControlPacket.AccelFunction accel = 0,
        bool translates = false, bool rotates = false, bool translationContinues = false, bool tracksDestination = false,
        bool interpolatesAngles = false, bool yawFaces = false, bool pitchFaces = false, bool orientsPredicts = false,
        bool keyIsLocal = false, bool usesRotator = false, bool usesInterpolator = false, bool usesPhysics = false,
        bool rotatesInWorldSpace = false, TwinBehaviourControlPacket.NaturalAxes axes = 0, bool stalls = false,
        PacketInteger? selector = null, PacketInteger? keyIndex = null, PacketNumber? moveSpeed = null, PacketNumber? turnSpeed = null,
        PacketNumber? rawPosX = null, PacketNumber? rawPosY = null, PacketNumber? rawPosZ = null,
        PacketNumber? pitch = null, PacketNumber? yaw = null, PacketNumber? roll = null,
        PacketNumber? delay = null, PacketNumber? duration = null, PacketNumber? tumble = null, PacketNumber? spin = null, PacketNumber? twist = null,
        PacketNumber? randRange = null, PacketNumber? power = null, PacketNumber? damping = null,
        PacketNumber? acDist = null, PacketNumber? decDist = null, PacketNumber? bounce = null,
        PacketInteger? syncUnit = null, PacketInteger? jointIndex = null,
        byte[]? order = null)
    {
        var p = new TwinBehaviourControlPacket
        {
            Space = space, Motion = motion, ContinuousRotate = rotate, AccelerationFunction = accel,
            Translates = translates, Rotates = rotates, TranslationContinues = translationContinues, TracksDestination = tracksDestination,
            InterpolatesAngles = interpolatesAngles, YawFaces = yawFaces, PitchFaces = pitchFaces, OrientsPredicts = orientsPredicts,
            KeyIsLocal = keyIsLocal, UsesRotator = usesRotator, UsesInterpolator = usesInterpolator, UsesPhysics = usesPhysics,
            ContinuouslyRotatesInWorldSpace = rotatesInWorldSpace, Axes = axes, Stalls = stalls,
        };
        Span<(bool Set, uint Word, byte Var)> fields =
        [
            Int(selector), Int(keyIndex), Num(moveSpeed), Num(turnSpeed), Num(rawPosX), Num(rawPosY), Num(rawPosZ),
            Num(pitch), Num(yaw), Num(roll), Num(delay), Num(duration), Num(tumble), Num(spin), Num(twist),
            Num(randRange), Num(power), Num(damping), Num(acDist), Num(decDist), Num(bounce), Int(syncUnit), Int(jointIndex),
        ];
        int count = 0, values = 0;
        for (int i = 0; i < fields.Length; i++)
        {
            if (!fields[i].Set) continue;
            count = i + 1;
            if (fields[i].Var == 0xFF) values++;
        }
        if (order is not null && order.Length != values) throw new ArgumentException($"order has {order.Length} entries for {values} values");
        var words = new uint[values];
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            var (set, word, var) = fields[i];
            if (!set) { p.Bytes.Add(0xFF); continue; }
            if (var != 0xFF) { p.Bytes.Add((byte)(0x80 | var)); continue; }
            byte slot = order is null ? (byte)n : order[n];
            words[slot] = word;
            p.Bytes.Add(slot);
            n++;
        }
        p.Floats.AddRange(words);
        return p;
    }

    private static (bool, uint, byte) Num(PacketNumber? v) => v is { } x ? (true, x.Word, x.Var) : (false, 0u, (byte)0xFF);
    private static (bool, uint, byte) Int(PacketInteger? v) => v is { } x ? (true, x.Word, x.Var) : (false, 0u, (byte)0xFF);
}
