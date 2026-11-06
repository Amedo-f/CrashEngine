using System.Numerics;

namespace CrashEngine.Runtime.Physics;

// Amedo 2026-09-27
public sealed class PhysicsController
{
    public const ulong VerticalGravity = 1UL << 40;
    public const ulong FullGravity = 1UL << 41;
    public const ulong Inactive = 3UL << 42;
    public const ulong InPairList = 1UL << 44;
    public const ulong Damped = 1UL << 45;
    public const ulong Gripped = 1UL << 46;
    public const ulong HasB4 = 1UL << 47;
    public const ulong HasB8 = 1UL << 48;
    public const ulong Gravitates = 1UL << 49;
    public const ulong TouchedAgent = 1UL << 51;
    public const ulong TouchedTerrain = 1UL << 52;
    public const ulong TouchedTerrainNow = 1UL << 53;
    public const ulong Bit55 = 1UL << 55;
    public const ulong Launched = 1UL << 56;
    public const ulong Bit57 = 1UL << 57;
    public const ulong LaunchTimed = 1UL << 58; // Amedo
    public const ulong Airborne = 1UL << 59;
    public const ulong MaxTiltSet = 1UL << 60;
    public const ulong TiltPowerSet = 1UL << 61;
    public const ulong Bounces = 1UL << 62;

    public const ulong StateFloor = 0x2;
    public const ulong StateWall = 0x20;
    public const ulong StateSmoothNormal = 0x10;
    public const ulong StateSurfaceEvent = 0x100000;
    public const ulong StateHadContact = 0x1000000;
    public const ulong StateCounter = 0x7800;
    public const ulong StateRideCount = 0x78000; // Amedo
    public const ulong StateDeactivated = 0x400; // Amedo

    public ulong Flags = 0xFFFFFFFFUL;
    public ulong State = 0x87801;
    public Vector3 GroundNormal = Vector3.UnitY;
    public float Ratio = 1f;
    public float Gravity;
    public float Damping = 1f;
    public float Bounce;
    public float Grip = 1f;
    public float ParamB4;
    public float MaxTilt;
    public float ParamBC = 1f;
    public float AirTime;
    public float Magnet_9C; // Amedo
    public float GripNow;
    public TriangleCache? Terrain;
    public RigidBody? Body;
    public object? RideObject_80; // Amedo
    public Vector3 RideLocal_70;

    public int PairType => (int)(Flags >> 32) & 0xF;
    public int TerrainType => (int)(Flags >> 36) & 0xF;
    public bool Has(ulong bit) => (Flags & bit) != 0;

    public ChunkRigidBodies? Lists_D8; // Amedo
    public int First_DC = 0xFFFF, Second_DE = 0xFF; // Amedo
    public object? Owner_94; // Amedo

    // Amedo
    public void SetPairTypeBits(int type) => Flags = (Flags & ~(0xFUL << 32)) | ((ulong)(type & 0xF) << 32);

    // Amedo
    public void SetTerrainTypeBits(int type) => Flags = (Flags & ~(0xFUL << 36)) | ((ulong)(type & 0xF) << 36);

    public void SetDamping(float v) { Flags |= Damped; Damping = v; }
    public void SetBounce(float v) { Flags |= Bounces; Bounce = v; }
    public void SetGrip(float v) { Grip = v; Flags |= Gripped; }
    public void SetB4(float v) { ParamB4 = v; Flags |= HasB4; }
    public void SetB8(float v) { MaxTilt = v; Flags |= HasB8; }

    public void SetState(ulong bit, bool on) => State = on ? State | bit : State & ~bit;
}
