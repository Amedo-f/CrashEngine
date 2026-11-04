using System.Numerics;
using CrashEngine.Importer;
using CrashEngine.Runtime.Camera;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-10-05
public sealed partial class CharacterController
{
    private const float ArmEps = 2.4999998e-09f;

    public Func<Matrix4x4?>? HeldMatrix_11C;
    public Action<Matrix4x4>? SetHeldMatrix_11C;

    internal bool ArmBone_14C7D8(int reactId, ref Matrix4x4 m)
    {
        if (Link is not { } link) return false;
        bool leader = link.Follower_15FE98() is not null;
        int upper = leader ? 0x11 : 0xD, fore = upper + 1, hand = upper + 2;
        bool back = reactId is >= 2 and <= 4;
        if (!back && reactId != upper && reactId != fore && reactId != hand) return false;
        float time;
        if (leader) time = link.Time1A4;
        else if (link.Leader_15FE50()?.Link is { } far) time = far.Time1A4;
        else return false;
        bool logic = AnimatedObject.InLogicCompose;
        bool phase2 = link.Phase == 2;
        int hold = link.Hold;
        Matrix4x4 c;
        if (back)
        {
            if (hold != 2) return false;
            var rot = m;
            rot.M41 = rot.M42 = rot.M43 = 0f;
            rot.M44 = 1f;
            float angle = BackTwist_15FCC0(link, time) * (link.Scale40 * 0.25f);
            c = GameMath.RotY_18BBC0(GameMath.FromValue_11BFA0(angle, 0)) * rot;
            c.M41 = m.M41;
            c.M42 = m.M42;
            c.M43 = m.M43;
            c.M44 = m.M44;
        }
        else if (reactId == upper)
        {
            if (phase2) return false;
            var p = m.Translation;
            if (logic)
            {
                link.Point60 = p;
                if (link.Point50.X == 1e10f) link.Point50 = p;
                if (hold == 0) link.Hold = 1;
                link.ShownValid = true;
                link.ShownHold = hold;
                link.ShownVec20 = link.Vec20;
                link.ShownVec30 = link.Vec30;
                link.ShownF0 = link.VecF0;
                link.ShownF100 = link.Vec100;
                link.ShownPoint50 = link.Point50;
            }
            else if (!link.ShownValid) return false;
            else hold = link.ShownHold;
            if (hold != 2) return false;
            var p50 = logic ? link.Point50 : link.ShownPoint50;
            var p70 = p - p50;
            if (logic) link.Vec70 = p70;
            link.ShownVec70 = p70;
            c = Aim_14C5C8(logic ? link.Vec20 : link.ShownVec20);
            c.Translation = p50 + p70;
        }
        else if (reactId == fore)
        {
            if (phase2) return false;
            if (!logic)
            {
                if (!link.ShownValid) return false;
                hold = link.ShownHold;
            }
            if (hold != 2) return false;
            c = Aim_14C5C8(logic ? link.Vec30 : link.ShownVec30);
            c.Translation = (logic ? link.VecF0 : link.ShownF0) + (logic ? link.Vec70 : link.ShownVec70);
        }
        else
        {
            if (!logic)
            {
                if (phase2 || !link.ShownValid || link.ShownHold != 2) return false;
                c = Aim_14C5C8(link.ShownVec30);
                c.Translation = link.ShownF0 + HandReach(link.ShownF0, link.ShownF100, leader) + link.ShownVec70;
            }
            else
            {
                c = m;
                if (hold == 2)
                {
                    var aim = Aim_14C5C8(link.Vec30);
                    aim.Translation = link.VecF0 + HandReach(link.VecF0, link.Vec100, leader) + link.Vec70;
                    var grip = Bones.Kind == 0 ? new Vector3(0.2f, 0.11f, -0.05f) : new Vector3(-0.1f, -0.05f, -0.05f);
                    if (!phase2) c = aim;
                    link.Vec80 = Vector3.Transform(Vector3.Transform(grip, c), GameMatrix);
                }
                if (leader)
                {
                    link.Point50 = link.Point60;
                    Setup_14D208(link);
                }
            }
        }
        if (hold != 2) return false;
        if (phase2 && !back && !(0.75f <= time)) return false;
        float blend = link.Blend214;
        Matrix4x4 r = c;
        if (blend < 1f)
        {
            r = m;
            r.M11 += (c.M11 - m.M11) * blend; r.M12 += (c.M12 - m.M12) * blend; r.M13 += (c.M13 - m.M13) * blend;
            r.M21 += (c.M21 - m.M21) * blend; r.M22 += (c.M22 - m.M22) * blend; r.M23 += (c.M23 - m.M23) * blend;
            r.M31 += (c.M31 - m.M31) * blend; r.M32 += (c.M32 - m.M32) * blend; r.M33 += (c.M33 - m.M33) * blend;
            r.M41 += (c.M41 - m.M41) * blend; r.M42 += (c.M42 - m.M42) * blend; r.M43 += (c.M43 - m.M43) * blend;
        }
        r.M14 = r.M24 = r.M34 = 0f;
        r.M44 = 1f;
        m = r;
        return true;
    }

    private static Vector3 HandReach(Vector3 f0, Vector3 f100, bool leader)
    {
        var d = f100 - f0;
        return d * GameMath.InvLength_11C030(ArmEps, new Vector4(d, 0f)) * (leader ? 0.3f : 0.256f);
    }

    private static float BackTwist_15FCC0(CharacterLink link, float time)
    {
        if (link.Phase != 2) return 1f;
        if (time < 0.25f) return 1f - time * 4f;
        if (time < 0.75f) return 0f;
        return time * 4f - 3f;
    }

    private Matrix4x4 Aim_14C5C8(Vector3 dir)
    {
        var a = dir * GameMath.InvLength_11C030(ArmEps, new Vector4(dir, 0f));
        var c = Vector3.Cross(a, Vector3.UnitX);
        c *= GameMath.InvLength_11C030(ArmEps, new Vector4(c, 0f));
        var b = Vector3.Cross(c, a);
        b *= GameMath.InvLength_11C030(ArmEps, new Vector4(b, 0f));
        if (Bones.Kind == 0)
        {
            b = -b;
            c = -c;
        }
        else
        {
            a = -a;
            c = -c;
        }
        return new Matrix4x4(a.X, a.Y, a.Z, 0f, b.X, b.Y, b.Z, 0f, c.X, c.Y, c.Z, 0f, 0f, 0f, 0f, 1f);
    }

    private void Setup_14D208(CharacterLink link)
    {
        if (link.Follower_15FE98() is not { } follower || follower.Link is not { } far) return;
        if (link.Hold == 0 || far.Hold == 0) return;
        var wl = GameMatrix;
        var wf = follower.GameMatrix;
        var il = GameMath.RigidInverse_18E808(wl);
        il.M44 = 1f;
        var iff = GameMath.RigidInverse_18E808(wf);
        iff.M44 = 1f;
        var pl = Vector3.Transform(link.Point50, wl);
        var pf = Vector3.Transform(far.Point50, wf);
        var t = Vector3.TransformNormal(pf - pl, il);
        link.Ik110 ??= LinkIk.Create_14D680();
        var pts = link.Points90;
        link.Ik110.Solve(Vector3.Zero, t, pts);
        link.Length0E0 = GameMath.Length(t);
        link.Hold = 2;
        far.Hold = 2;
        var err = t - pts[4];
        link.Vec20 = pts[0];
        link.Vec30 = pts[1] - pts[0];
        pts[2] += err;
        pts[3] += err;
        far.Vec20 = pts[3] - t;
        far.Vec30 = pts[2] - pts[3];
        for (int i = 0; i < pts.Length; i++) pts[i] += link.Point50;
        link.VecF0 = pts[0];
        link.Vec100 = pts[1];
        far.VecF0 = Vector3.Transform(Vector3.Transform(pts[3], wl), iff);
        far.Vec100 = Vector3.Transform(Vector3.Transform(pts[2], wl), iff);
        link.Partner1B0 = wf;
    }

    private bool HandPoints_13FC50(out Vector3 h1, out Vector3 h2)
    {
        h1 = h2 = default;
        if ((MotionFlagsHigh & LeaderBit) != 0)
        {
            if (Link is not { Hold: 2 } link || link.Follower_15FE98()?.Link is not { } far) return false;
            h1 = link.Vec80;
            h2 = far.Vec80;
            return true;
        }
        return (MotionFlagsHigh & FollowerBit) != 0 && Link?.Leader_15FE50() is { } lead && lead.HandPoints_13FC50(out h1, out h2);
    }

    private void PlaceHeld_12EA78(CharacterLink link)
    {
        if (HeldMatrix_11C?.Invoke() is not { } m || SetHeldMatrix_11C is not { } set) return;
        if (!HandPoints_13FC50(out var h1, out var h2)) return;
        if (!(GameMath.Length(h1) < 1000f) || !(GameMath.Length(h2) < 1000f)) return;
        if (link.Leader_15FE50() is not { } lead || lead.MotionState is 6 or 10) return;
        var d = h2 - h1;
        d *= GameMath.InvLength_11C030(ArmEps, new Vector4(d, 0f));
        var up = new Vector3(m.M21, m.M22, m.M23);
        var axis = Vector3.Cross(up, d);
        up *= GameMath.InvLength_11C030(ArmEps, new Vector4(up, 0f));
        if (5e-5f < GameMath.LengthSquared(axis))
        {
            axis *= GameMath.InvLength_11C030(ArmEps, new Vector4(axis, 0f));
            int angle = GameMath.Acos_18B738(GameMath.Dot(up, d));
            if (angle != 0) m *= Matrix4x4.CreateFromAxisAngle(axis, angle * GameMath.UnitToRad);
        }
        var r0 = new Vector3(m.M11, m.M12, m.M13);
        r0 *= GameMath.InvLength_11C030(ArmEps, new Vector4(r0, 0f));
        var r1 = new Vector3(m.M21, m.M22, m.M23);
        r1 *= GameMath.InvLength_11C030(ArmEps, new Vector4(r1, 0f));
        var r2 = Vector3.Cross(r0, r1);
        r0 = Vector3.Cross(r1, r2);
        var mid = (h1 + h2) * 0.5f;
        set(new Matrix4x4(r0.X, r0.Y, r0.Z, 0f, r1.X, r1.Y, r1.Z, 0f, r2.X, r2.Y, r2.Z, 0f, mid.X, mid.Y, mid.Z, 1f));
    }
}
