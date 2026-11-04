using System.Numerics;
using CrashEngine.Runtime.Camera;
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
        float aa = a.X * a.X + a.Y * a.Y + a.Z * a.Z;
        float ba = b.X * a.X + b.Y * a.Y + b.Z * a.Z;
        float bp = b.X * p.X + b.Y * p.Y + b.Z * p.Z;
        float ap = a.X * p.X + a.Y * p.Y + a.Z * p.Z;
        float t = (bp - ap) - (ba - aa);
        float m = t * -t;
        float qx = p.X - a.X, qy = p.Y - a.Y, qz = p.Z - a.Z;
        if (!(m < 0f)) return qx * qx + qy * qy + qz * qz;
        float ex = a.X - b.X, ey = a.Y - b.Y, ez = a.Z - b.Z;
        float l = ex * ex + ey * ey + ez * ez;
        float inv = 1e-10f < l || l < -1e-10f ? 1f / l : 0f;
        return MathF.Abs(m * inv + (qx * qx + qy * qy + qz * qz));
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
