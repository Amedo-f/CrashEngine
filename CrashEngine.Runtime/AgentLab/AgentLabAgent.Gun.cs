using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    internal bool LockOnFlag_200 => OwnCharacter is { } c ? (c.MotionWord & 0x200) != 0 : (Motion10 & 0x200) != 0;
    internal bool HasLinkList => _hasLinkList;
    internal AgentLabAgent? FirstLink => LinkAt(0);

    internal AgentLabAgent? GunWeapon_14A2C8(AgentLabAgent? weapon)
    {
        foreach (var n in _attachments)
        {
            if (n.Mode == SpringMode || !Holds(n) || ReferenceEquals(weapon, n.Child)) continue;
            weapon = n.Child;
        }
        return weapon;
    }

    internal AgentLabAgent? AimSlot118_14A910()
    {
        if (_slot118 is { } s && (s.Destroyed || (s.AgentFlags & 1) != 0)) _slot118 = null;
        return _slot118;
    }

    internal static float MaxBoxSide_1FF878((Vector3 Min, Vector3 Max) box)
    {
        float dz = box.Max.Z - box.Min.Z, dy = box.Max.Y - box.Min.Y, dx = box.Max.X - box.Min.X;
        float r = dx;
        if (r < dy) r = dy;
        if (r < dz) r = dz;
        return r;
    }

    internal static float LineDistanceSquared_184340(in Vector3 a, in Vector3 b, in Vector3 p)
    {
        // Amedo 2026-11-21
        static float D(in Vector3 u, in Vector3 v) => GameMath.Ps2Add(GameMath.Ps2Add(GameMath.Ps2Mul(u.X, v.X), GameMath.Ps2Mul(u.Y, v.Y)), GameMath.Ps2Mul(u.Z, v.Z));
        static float Sq(float x, float y, float z) => GameMath.Ps2Add(GameMath.Ps2Add(GameMath.Ps2Mul(x, x), GameMath.Ps2Mul(y, y)), GameMath.Ps2Mul(z, z));
        float t = GameMath.Ps2Sub(GameMath.Ps2Sub(D(b, p), D(a, p)), GameMath.Ps2Sub(D(b, a), D(a, a)));
        float m = GameMath.Ps2Mul(t, -t);
        float qx = GameMath.Ps2Sub(p.X, a.X), qy = GameMath.Ps2Sub(p.Y, a.Y), qz = GameMath.Ps2Sub(p.Z, a.Z);
        if (!(m < 0f)) return Sq(qx, qy, qz);
        float l = Sq(GameMath.Ps2Sub(a.X, b.X), GameMath.Ps2Sub(a.Y, b.Y), GameMath.Ps2Sub(a.Z, b.Z));
        float inv = 1e-10f < l || l < -1e-10f ? GameMath.Ps2Div(1f, l) : 0f;
        return MathF.Abs(GameMath.Ps2Add(GameMath.Ps2Mul(m, inv), Sq(qx, qy, qz)));
    }

    private float ShotHitMe_126708()
    {
        if (World.GunHolder_30A5B0 is not { Destroyed: false } holder || holder.OwnCharacter?.Gun is not { } gun) return 0f;
        if (!gun.ShotWithin_15F108(GameMath.Trunc(GameMath.SecToTick * 0.2f))) return 0f;
        var hm = holder.GameMatrix;
        var from = hm.Translation;
        var fwd = new Vector3(hm.M31, hm.M32, hm.M33);
        var me = GamePosition;
        var d = new Vector4(me.X - from.X, me.Y - from.Y, me.Z - from.Z, 1f);
        float k = GameMath.InvLength_11C030(2.4999998e-09f, d);
        float nx = d.X * k, ny = d.Y * k, nz = d.Z * k;
        if (!(0f < fwd.X * nx + fwd.Y * ny + fwd.Z * nz)) return 0f;
        float r = MaxBoxSide_1FF878(TreeBox);
        var a = new Vector3(from.X, 0f, from.Z);
        var b = new Vector3(fwd.X * 1000f + from.X, 0f, fwd.Z * 1000f + from.Z);
        return LineDistanceSquared_184340(a, b, new Vector3(me.X, 0f, me.Z)) < r * r ? 1f : 0f;
    }
}
