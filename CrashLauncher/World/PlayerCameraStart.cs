using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Runtime.Camera;
using CrashEngine.Runtime.Physics;

namespace CrashLauncher;

// Amedo 2026-11-15
internal sealed class PlayerCameraStart
{
    private const uint StepTicks = 150;
    private const int SettleSteps = 120;

    public CamMemory Memory { get; private set; } = new();
    public uint Director { get; private set; }
    public uint CameraComponent { get; private set; }
    public Vector3 Eye { get; private set; }
    public Vector3 Forward { get; private set; } = -Vector3.UnitZ;
    public Vector3 Up { get; private set; } = Vector3.UnitY;
    public Vector3 Target { get; private set; }
    public Vector3 Crash { get; private set; }

    public uint Eyes => Director + 0x2F0;
    public uint Aim => Director + 0xE0;

    public void Settle(Matrix4x4 crash, CollisionWorld? collision)
    {
        var mem = new CamMemory();
        var cam = new GameCamera(mem) { Collision = collision };
        uint inst = cam.Alloc(0x160, "crash instance");
        uint node = cam.Alloc(0x70, "crash node");
        uint c = cam.Alloc(0x310, "crash controller");
        uint motion = cam.Alloc(0x40, "crash motion");
        mem.U(inst + 8, node);
        mem.B(inst + 0x153, 2);
        mem.U(c + 0x10, motion);
        var q = Quaternion.CreateFromRotationMatrix(crash);
        mem.M(node, crash);
        mem.V(node + 0x40, new Vector4(crash.Translation, 1f));
        mem.V(node + 0x50, new Vector4(q.X, q.Y, q.Z, q.W));
        mem.V(c + 0x60, new Vector4(0f, 0f, 0f, 1f));
        mem.V(c + 0xD0, new Vector4(crash.Translation, 1f));
        cam.SetTime(0, 0);
        uint comp = cam.CreatePlayerCamera(inst, c);
        uint c9 = cam.CameraComponent(cam.Handle(comp + 0x20));
        uint comp12 = cam.Alloc(0x20, "crash comp12");
        mem.U(comp12 + 0x18, c);
        mem.U(inst + 0xD8 + 0xC * 4, comp12);
        mem.U(inst + 0xD8 + 0x16 * 4, comp);
        cam.CreateGlobals(inst, cam.Handle(comp + 0x20));
        var pose = Matrix4x4.Identity;
        uint now = 0;
        for (int i = 0; i < SettleSteps; i++)
        {
            now += StepTicks;
            cam.SetTime(now, StepTicks);
            cam.SetInput(comp + 0x50, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            cam.PlayerCameraUpdate_172A48(comp, GameCamera.TimeStruct, inst, c);
            uint camNode = mem.U(cam.Handle(comp + 0x20) + 8);
            cam.NodeRefresh_18AFA8(camNode);
            (pose, int fov) = cam.CameraUpdate_26FD80(c9, GameCamera.TimeStruct, mem.M(camNode));
            mem.U(c9 + 0x18, mem.U(c9 + 0x18) | 1);
            mem.I(c9 + 0x1C, fov);
            cam.NodeSetMatrix_18AF40(camNode, pose);
        }
        Memory = mem;
        Director = comp + 0x50;
        CameraComponent = c9;
        Eye = pose.Translation;
        var f = new Vector3(pose.M31, pose.M32, pose.M33);
        var u = new Vector3(pose.M21, pose.M22, pose.M23);
        Forward = GameMath.LengthSquared(f) > 0f ? GameMath.Normalize(f) : -Vector3.UnitZ;
        Up = GameMath.LengthSquared(u) > 0f ? GameMath.Normalize(u) : Vector3.UnitY;
        var t = mem.V(Aim + 0x20);
        Target = new Vector3(t.X, t.Y, t.Z);
        Crash = crash.Translation;
    }

    public static float Degrees(int angle) => angle * 360f / 65536f;
}
