using System.Numerics;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-10-05
public sealed class CharacterLink
{
    public const ulong PhaseMask = 0xF00000000UL;
    public const ulong LeaderFlag = 1UL << 44;
    public const ulong PartnerFlag20000 = 1UL << 45;

    public readonly CharacterController Owner;
    public CharacterController? Follower;
    public CharacterController? Leader;
    public ulong Flags;
    public float Scale40;
    public Vector3 Point50 = new(1e10f, 0f, 0f);
    public Vector3 Vec80, VecF0, Vec100;
    public Vector3 Vec20, Vec30, Point60, Vec70; // Amedo 2026-10-05
    public readonly Vector3[] Points90 = new Vector3[5];
    public float Length0E0;
    public LinkIk? Ik110;
    public Matrix4x4 Partner1B0;
    public float Time1A4, Time1A8;
    public int Ticks1A0;
    public Vector3 Target1F0;
    public Vector4 Vec200;
    public float Value210;
    public float Blend214;

    public CharacterLink(CharacterController owner, CharacterController partner, bool leader)
    {
        Owner = owner;
        if (leader) Follower = partner;
        else Leader = partner;
        Flags = (Flags & 0xFFFFE000FFFFFFFFUL) | 0x100000000UL | (leader ? LeaderFlag : 0UL);
        Scale40 = 1f;
        if ((Flags & LeaderFlag) == 0)
        {
            Target1F0 = owner.GameMatrix.Translation;
            Vec200 = new Vector4(0f, 0f, 0f, 1f);
            Value210 = 1f;
        }
        else if (partner.Self is { } other && partner.InstanceFlagsOf is { } flagsOf)
        {
            if ((flagsOf(other) & 0x20000) == 0x20000) Flags |= PartnerFlag20000;
            partner.ClearInstanceFlags?.Invoke(other, 0x20000);
        }
    }

    public int Phase => (int)((Flags >> 32) & 0xF);

    // Amedo 2026-10-05
    public int Hold
    {
        get => (int)((Flags >> 40) & 0xF);
        set => Flags = (Flags & ~(0xFUL << 40)) | ((ulong)(value & 0xF) << 40);
    }

    internal bool ShownValid;
    internal int ShownHold;
    internal Vector3 ShownVec20, ShownVec30, ShownF0, ShownF100, ShownPoint50, ShownVec70;
    public bool IsLeader => (Flags & LeaderFlag) != 0;

    public CharacterController? Follower_15FE98() => Follower is { } f && f.Alive?.Invoke() != false ? f : null;

    public CharacterController? Leader_15FE50() => Leader is { } l && l.Alive?.Invoke() != false ? l : null;

    // Amedo 2026-10-06
    public bool SlamWindow_15FC00()
    {
        float t;
        if (Follower_15FE98() is not null) t = Time1A4;
        else if (Leader_15FE50()?.Link is { } leader) t = leader.Time1A4;
        else return false;
        return Phase == 2 && 0.2f < t && t < 0.99f;
    }
}
