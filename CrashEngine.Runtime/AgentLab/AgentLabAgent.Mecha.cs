using System.Numerics;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private const uint HullHitMask = 0x5B010;
    private const int HullHitPart = 7;

    internal void HullHits_136B70()
    {
        if (OwnCharacter is not { } c) return;
        var (min, max) = TreeBox;
        if (Body() is { Parts.Count: > 0 } body)
        {
            var own = body.Parts;
            foreach (var other in World.QueryBox_1ECFC0(min, max, HullHitMask, 1, 0x10, true, this, 0x100))
            {
                if (other.Body() is not { } ob) continue;
                for (int j = 0; j < ob.Parts.Count; j++)
                {
                    var (oh, om) = ob.Parts[j];
                    var (omin, omax) = oh.Bounds(om);
                    if (!CollisionWorld.Overlap(omin, omax, min, max)) continue;
                    for (int i = 0; i < own.Count; i++)
                    {
                        var (h, m) = own[i];
                        var (pmin, pmax) = h.Bounds(m);
                        if (!CollisionWorld.Overlap(omin, omax, pmin, pmax) || !ConvexHull.Overlaps_200370(oh, om, h, m)) continue;
                        Trace($"hull hit {other.Entity.Name} part {i} (its part {j})");
                        World.PostTouch_13E0A0(other, i == HullHitPart ? 6 : 3, this);
                    }
                }
            }
        }
        if (c.Gun is { } gun)
        {
            bool found = gun.AimPoint_14A860(out var aim);
            c.Bones.Look.Aim_15EE60(aim, found);
        }
    }
}
