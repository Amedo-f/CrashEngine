using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public const uint Shake = 0x3BD7B0, ShakeHandle = 0x3BD7E0, RumbleTable = 0x3C6B80;
    private static readonly float ShakeEps = BitConverter.UInt32BitsToSingle(0x312BCC76);

    public CrashEngine.Core.GameRandom Rng { get; set; } = CrashEngine.Core.GameRandom.Shared; // Amedo
    public Action<uint, float>? Rumble { get; set; }

    public void ShakeInit_26F508(uint s)
    {
        M.U(s + 0x30, 0);
        Zero4(s);
        Zero4(s + 0x10);
        Zero4(s + 0x20);
        M.M(s + 0x40, Matrix4x4.Identity);
        M.F(s + 0x80, 1f);
        M.F(s + 0x88, 0.1f);
        M.F(s + 0x84, 1f);
    }

    public void ShakeTarget_177A90(uint cameraObj)
    {
        if (Handle(ShakeHandle) == cameraObj) return;
        M.U(ShakeHandle, cameraObj == 0 ? 0u : HandleOf(cameraObj));
    }

    private void NodeSyncPosition(uint node)
    {
        if ((M.Q(node + 0x60) & 4) == 0) return;
        M.Copy16(node + 0x40, node + 0x30);
        M.Q(node + 0x60, M.Q(node + 0x60) & ~5UL);
    }

    public void NodeSetMatrix_18AF40(uint node, Matrix4x4 m)
    {
        M.M(node, m);
        M.Q(node + 0x60, (M.Q(node + 0x60) & ~3UL) | 0xC);
    }

    public void ShakeUpdate_26FB40(uint s)
    {
        uint obj = Handle(s + 0x30);
        if (obj == 0) return;
        uint node = M.U(obj + 8);
        NodeSyncPosition(node);
        NodeRefresh_18AFA8(node);
        M.M(s + 0x40, GameMath.RigidInverse_18E808(M.M(node)));
        var p = M.V(s);
        var add = M.V(s + 0x20);
        p = new Vector4(p.X + add.X, p.Y + add.Y, p.Z + add.Z, p.W);
        M.V(s, p);
        Zero4(s + 0x20);
        var prev = M.V(s + 0x10);
        float nx = p.X + (p.X - prev.X), ny = p.Y + (p.Y - prev.Y), nz = p.Z + (p.Z - prev.Z);
        M.V(s + 0x10, p);
        float k = M.F(s + 0x88);
        nx -= nx * k;
        ny -= ny * k;
        nz -= nz * k;
        float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
        float max = M.F(s + 0x80);
        if (max < len)
        {
            float r = max / len;
            nz *= r;
            nx *= r;
            ny *= r;
        }
        M.F(s, nx);
        M.F(s + 4, ny);
        M.F(s + 8, nz);
    }

    private Vector4 ShakeLocal(uint s, Vector4 source)
    {
        uint node = M.U(Handle(s + 0x30) + 8);
        NodeSyncPosition(node);
        var e = M.V(node + 0x40);
        return GameMath.Rotate3_18E4B8(M.M(s + 0x40), new Vector4(e.X - source.X, e.Y - source.Y, e.Z - source.Z, e.W));
    }

    public void Impulse_26F5E8(float strength, float falloff, uint s, Vector4 source)
    {
        if (Handle(s + 0x30) == 0) return;
        var v = ShakeLocal(s, source);
        if (0.0001f < falloff) strength /= GameMath.LengthSq3(v) * falloff + 1f;
        float inv = GameMath.InvLength_11C030(ShakeEps, v);
        v = new Vector4(v.X * inv, v.Y * inv, v.Z * inv, v.W);
        var jittered = Rng.JitterVector_23C848(new Vector3(v.X, v.Y, v.Z), 0.2f, 1f, 1f, 1f);
        v = new Vector4(jittered, v.W);
        float ix = v.X * strength, iy = v.Y * strength, iz = v.Z * strength;
        M.F(s + 0x20, M.F(s + 0x20) + ix);
        M.F(s + 0x24, M.F(s + 0x24) + iy);
        M.F(s + 0x28, M.F(s + 0x28) + iz);
        Rumble_27B4F0(strength);
    }

    public void ImpulseAxes_26F7E8(float ax, float ay, float az, float falloff, uint s, Vector4 source)
    {
        if (Handle(s + 0x30) == 0) return;
        var v = ShakeLocal(s, source);
        float ox, oy, oz, peak;
        if (0.0001f < falloff)
        {
            float k = 1f / (GameMath.LengthSq3(v) * falloff + 1f);
            float kz = az * k, kx = ax * k, ky = ay * k;
            ox = 0f < v.X ? kx : -kx;
            oy = 0f < v.Y ? ky : -ky;
            oz = 0f < v.Z ? kz : -kz;
            peak = kx;
            if (peak < ky) peak = ky;
            if (peak < kz) peak = kz;
        }
        else
        {
            float inv = GameMath.InvLength_11C030(ShakeEps, v);
            ox = v.X * inv * ax;
            oy = v.Y * inv * ay;
            oz = v.Z * inv * az;
            peak = ax;
            if (peak < ay) peak = ay;
            if (peak < az) peak = az;
        }
        M.F(s + 0x20, M.F(s + 0x20) + ox);
        M.F(s + 0x24, M.F(s + 0x24) + oy);
        M.F(s + 0x28, M.F(s + 0x28) + oz);
        Rumble_27B4F0(peak);
    }

    public void Rumble_27B4F0(float strength)
    {
        int level = GameMath.Trunc(strength * 1024f);
        if (!(level < 0x100)) level = 0xFF;
        if (!(0x13 < level)) level = 0x14;
        RumbleSet_2B36D0(0x21u | ((uint)(level & 0xFF) << 7), 0.4f);
    }

    public void RumbleSet_2B36D0(uint word, float seconds)
    {
        if (!M.IsMapped(RumbleTable)) M.Map(RumbleTable, 0x100, "pad rumble table");
        uint e = RumbleTable + ((word & 0x1F) - 1) * 8;
        M.U(e, word | 0x20);
        M.F(e + 4, seconds);
        Rumble?.Invoke(word | 0x20, seconds);
    }
}
