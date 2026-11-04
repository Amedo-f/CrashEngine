using System.Numerics;
using CrashEngine.Assets;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using TwinJointSettings = Twinsanity.TwinsanityInterchange.Common.Animation.JointSettings;
using TwinAnimatedTransformation = Twinsanity.TwinsanityInterchange.Common.Animation.AnimatedTransformation;
using TransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinJoint = Twinsanity.TwinsanityInterchange.Common.TwinJoint;
using Vector3 = System.Numerics.Vector3;

namespace CrashLauncher;

// Amedo 2026-10-05
internal static class CharacterBuild
{
    private const byte NoReactId = 0xFF;
    private const byte NoParent = 0xFF;

    internal static readonly Matrix4x4 MirrorX = Matrix4x4.CreateScale(-1f, 1f, 1f);

    internal static Matrix4x4 Mirrored(Matrix4x4 m) => MirrorX * m * MirrorX;

    private static (Quaternion Rot, Vector3 Pos) RigidParts(Matrix4x4 m)
    {
        Matrix4x4.Decompose(m, out _, out var q, out var t);
        return (Quaternion.Normalize(q), t);
    }

    private static TwinVec4 V4(Vector3 v, float w = 1f) => new(v.X, v.Y, v.Z, w);

    private static TwinVec4 V4(Quaternion q) => new(q.X, q.Y, q.Z, q.W);

    internal static List<TwinJoint> Joints(IReadOnlyList<RawJoint> joints, IReadOnlyList<Matrix4x4> worlds)
    {
        var list = new List<TwinJoint>();
        for (int i = 0; i < joints.Count; i++)
        {
            int parent = joints[i].Parent;
            var local = parent < 0 ? worlds[i] : worlds[i] * (Matrix4x4.Invert(worlds[parent], out var inv) ? inv : Matrix4x4.Identity);
            var (lq, lt) = RigidParts(local);
            var (wq, wt) = RigidParts(worlds[i]);
            list.Add(new TwinJoint
            {
                ReactId = NoReactId,
                Index = i,
                ParentIndex = parent < 0 ? NoParent : parent,
                ChildrenAmt1 = joints.Count(j => j.Parent == i),
                ChildrenAmt2 = 0,
                LocalTranslation = V4(lt),
                WorldTranslation = V4(wt),
                LocalRotation = V4(lq),
                UnusedRotation = V4(wq),
                AdditionalAnimationRotation = new TwinVec4(0f, 0f, 0f, 1f),
            });
        }
        return list;
    }

    internal static TwinAnimation Animation(IReadOnlyList<Matrix4x4[]> frames, int jointCount, out int clamped)
    {
        clamped = 0;
        int count = frames.Count;
        var raw = new int[jointCount][][];
        for (int j = 0; j < jointCount; j++)
        {
            raw[j] = new int[9][];
            for (int ch = 0; ch < 9; ch++) raw[j][ch] = new int[count];
            var near = Vector3.Zero;
            for (int f = 0; f < count; f++)
            {
                var (q, t) = RigidParts(frames[f][j]);
                var e = AnimChannelOps.GameEulerFrom(q, near);
                near = e;
                int[] v =
                [
                    AnimChannelOps.ValueToRaw(t.X, 0), AnimChannelOps.ValueToRaw(t.Y, 1), AnimChannelOps.ValueToRaw(t.Z, 2),
                    AnimChannelOps.EulerRaw(e.X), AnimChannelOps.EulerRaw(e.Y), AnimChannelOps.EulerRaw(e.Z),
                    AnimChannelOps.ValueToRaw(1f, 6), AnimChannelOps.ValueToRaw(1f, 7), AnimChannelOps.ValueToRaw(1f, 8),
                ];
                for (int ch = 0; ch < 9; ch++)
                {
                    if (ch < 3 && (v[ch] > short.MaxValue || v[ch] < short.MinValue)) clamped++;
                    raw[j][ch][f] = v[ch];
                }
            }
        }

        var anim = new TwinAnimation { TotalFrames = (ushort)count };
        for (int f = 0; f < count; f++) anim.AnimatedTransformations.Add(new TwinAnimatedTransformation(0));
        for (int j = 0; j < jointCount; j++)
        {
            var js = new TwinJointSettings { FacialShapesAmount = 0, UseParentJointScale = false, UseAdditionalRotation = false };
            var animated = new bool[9];
            for (int ch = 0; ch < 9; ch++)
            {
                var c = raw[j][ch];
                animated[ch] = c.Any(x => x != c[0]);
                AnimChannelOps.SetType(js, ch, animated[ch] ? TransformType.Animated : TransformType.Static);
            }
            js.TransformationIndex = animated.All(a => a) ? AnimChannelOps.NoBlock : (ushort)anim.StaticTransformations.Count;
            js.AnimationTransformationIndex = animated.Any(a => a) ? (ushort)anim.AnimatedTransformations[0].Count : AnimChannelOps.NoBlock;
            js.ChannelCounts = AnimChannelOps.CountsOf(js);
            for (int ch = 0; ch < 9; ch++)
            {
                if (animated[ch])
                    for (int f = 0; f < count; f++) anim.AnimatedTransformations[f].Transforms.Add(AnimChannelOps.Raw(AnimChannelOps.Wrap(raw[j][ch][f], ch)));
                else anim.StaticTransformations.Add(AnimChannelOps.Raw(AnimChannelOps.Wrap(raw[j][ch][0], ch)));
            }
            anim.JointSettings.Add(js);
        }
        return anim;
    }
}
