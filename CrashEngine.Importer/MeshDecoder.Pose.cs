using CrashEngine.Renderer;
using SysVec3 = System.Numerics.Vector3;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using TwinMorphAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinMorphAnimation;
using TwinJointSettings = Twinsanity.TwinsanityInterchange.Common.Animation.JointSettings;
using TwinTransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;

namespace CrashEngine.Importer;

public static partial class MeshDecoder
{
    public static Dictionary<int, Matrix4x4> ComputeJointWorlds(PS2AnyOGI ogi)
    {
        var world = new Dictionary<int, Matrix4x4>();
        if (ogi.Joints.Count == 0) return world;

        world[ogi.Joints[0].Index] = BindLocal(ogi.Joints[0]); // Amedo 2026-09-26
        for (int i = 1; i < ogi.Joints.Count; i++)
        {
            var j = ogi.Joints[i];
            var local = BindLocal(j); // Amedo 2026-09-26
            var parent = world.TryGetValue(j.ParentIndex, out var pw) ? pw : Matrix4x4.Identity;
            world[j.Index] = local * parent;
        }
        return world;
    }

    // Amedo 2026-09-26
    private static Matrix4x4 BindLocal(Twinsanity.TwinsanityInterchange.Common.TwinJoint j)
    {
        var q = new Quaternion(j.LocalRotation.X, j.LocalRotation.Y, j.LocalRotation.Z, j.LocalRotation.W);
        return Matrix4x4.CreateFromQuaternion(q) *
               Matrix4x4.CreateTranslation(j.LocalTranslation.X, j.LocalTranslation.Y, j.LocalTranslation.Z);
    }


    public static Matrix4x4[] SampleAnimationPose(PS2AnyOGI ogi, TwinAnimation anim, float frameF, bool loop = false) // Amedo 2026-09-26
        => SampleAnimationPose(ogi, anim, frameF, out _, loop);

    // Amedo 2026-09-26
    public static Matrix4x4[] SampleAnimationPose(PS2AnyOGI ogi, TwinAnimation anim, float frameF,
        out Dictionary<int, Matrix4x4> jointWorlds, bool loop = false)
    {
        if (SampleLocalPose(ogi, anim, frameF, loop) is not { } pose)
        {
            jointWorlds = new Dictionary<int, Matrix4x4>();
            return IdentityBones();
        }
        return ComposePose(ogi, pose, out jointWorlds);
    }

    // Amedo 2026-09-27
    public readonly record struct PoseLayer(TwinAnimation? Anim, int F0, int F1, float Frac, float Weight);

    // Amedo 2026-09-28
    public interface IJointControllers
    {
        void Rotate(int reactId, ref Quaternion rot, bool hasRotation);
        bool Transform(int reactId, ref Matrix4x4 model);
    }

    // Amedo 2026-10-02
    public interface IJointBasis
    {
        bool WantsPreWorld(int reactId, out Quaternion rotBefore);
        void PreWorld(int reactId, in Matrix4x4 world);
    }

    // Amedo
    public interface IJointPoser
    {
        bool Pose(int reactId, int joint, ref SysVec3 translate, ref Quaternion rot, ref int flags, in Matrix4x4 current, in Matrix4x4 parent, bool hasParent);
    }

    // Amedo 2026-09-27
    public static Matrix4x4[] SampleLayeredPose(PS2AnyOGI ogi, IReadOnlyList<PoseLayer> layers, out Dictionary<int, Matrix4x4> jointWorlds,
                                                IJointControllers? controllers = null, // Amedo 2026-09-28
                                                PoseBuffers? buffers = null, // Amedo 2026-09-29
                                                int reactSlots = 0xFF) // Amedo
    {
        int n = ogi.Joints.Count; // Amedo 2026-09-29
        var bind = Bind(ogi); // Amedo
        var pose = buffers is null ? new JointPose[n] : buffers.Pose.Length == n ? buffers.Pose : buffers.Pose = new JointPose[n];
        for (int i = 0; i < ogi.Joints.Count; i++)
        {
            var j = ogi.Joints[i];
            var bindT = new SysVec3(j.LocalTranslation.X, j.LocalTranslation.Y, j.LocalTranslation.Z);
            var bindR = new Quaternion(j.LocalRotation.X, j.LocalRotation.Y, j.LocalRotation.Z, j.LocalRotation.W);
            int flags = 0;
            SysVec3 t = bindT, s = SysVec3.One;
            Quaternion r = bindR;
            for (int li = 0; li < layers.Count; li++) // Amedo 2026-09-29
            {
                var layer = layers[li];
                SysVec3 lt = bindT, ls = SysVec3.One;
                Quaternion lr = bindR;
                if (layer.Anim is { } a && i < a.JointSettings.Count && a.AnimatedTransformations.Count > 0)
                {
                    var js = a.JointSettings[i];
                    (lt, lr, ls) = SampleJointTRS(a, js, layer.F0, layer.F1, layer.Frac, j.AdditionalAnimationRotation);
                    if (js.UseParentJointScale) flags |= 8;
                }
                if (1f <= layer.Weight)
                {
                    r = lr;
                    t = lt;
                    s = ls;
                }
                else
                {
                    r = GameSlerp((flags & 1) != 0 ? r : bindR, lr, layer.Weight);
                    var ps = (flags & 4) != 0 ? s : SysVec3.One;
                    s = ps + (ls - ps) * layer.Weight;
                    var pt = (flags & 2) != 0 ? t : bindT;
                    t = pt + (lt - pt) * layer.Weight;
                }
                flags |= 7;
            }
            if (controllers is not null && ReactJoint_2952A0(ogi, bind, i, reactSlots)) controllers.Rotate(j.ReactId, ref r, (flags & 1) != 0); // Amedo
            pose[i] = new JointPose { Translate = t, Rot = r, Scale = s, UseParentScale = (flags & 8) != 0, Flags = flags & 7 }; // Amedo
        }
        return ComposePose(ogi, pose, out jointWorlds, controllers, buffers, reactSlots);
    }

    // Amedo
    public static int JointIndexByID_2989E8(PS2AnyOGI ogi, int reactId)
    {
        for (int i = 0; i < ogi.Joints.Count; i++)
            if (ogi.Joints[i].ReactId == (reactId & 0xFF)) return i;
        return 0xFF;
    }

    // Amedo
    private static bool ReactJoint_2952A0(PS2AnyOGI ogi, BindData bind, int joint, int reactSlots)
    {
        int react = ogi.Joints[joint].ReactId;
        return react < Math.Min((int)ogi.HeaderReactJointAmount, reactSlots) && bind.ReactFirst[joint];
    }

    // Amedo 2026-09-29
    public sealed class PoseBuffers
    {
        internal readonly Matrix4x4[] Bones = new Matrix4x4[RenderPipeline.MaxBones];
        internal readonly Dictionary<int, Matrix4x4> Worlds = new();
        internal readonly Dictionary<int, SysVec3> RawScale = new();
        internal JointPose[] Pose = [];
        internal readonly List<PoseLayer> Layers = new();
    }

    // Amedo 2026-09-29
    private sealed class BindData
    {
        public Matrix4x4[] Inverse = [];
        public bool[] HasInverse = [];
        public bool[] ReactFirst = []; // Amedo
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PS2AnyOGI, BindData> Binds = new(); // Amedo 2026-09-29

    // Amedo 2026-09-29
    private static BindData Bind(PS2AnyOGI ogi) => Binds.GetValue(ogi, o =>
    {
        var world = ComputeJointWorlds(o);
        var data = new BindData { Inverse = new Matrix4x4[o.Joints.Count], HasInverse = new bool[o.Joints.Count], ReactFirst = new bool[o.Joints.Count] };
        for (int i = 0; i < o.Joints.Count; i++) data.ReactFirst[i] = JointIndexByID_2989E8(o, o.Joints[i].ReactId) == i; // Amedo
        for (int i = 0; i < o.Joints.Count; i++)
            if (world.TryGetValue(o.Joints[i].Index, out var bw) && Matrix4x4.Invert(bw, out var inv))
            {
                data.Inverse[i] = inv;
                data.HasInverse[i] = true;
            }
        return data;
    });

    // Amedo 2026-09-26
    internal struct JointPose // Amedo 2026-09-29
    {
        public SysVec3 Translate;
        public Quaternion Rot;
        public SysVec3 Scale;
        public bool UseParentScale;
        public int Flags; // Amedo
    }

    // Amedo 2026-09-26
    private static JointPose[]? SampleLocalPose(PS2AnyOGI ogi, TwinAnimation anim, float frameF, bool loop)
    {
        if (ogi.Joints.Count == 0 || anim.JointSettings.Count == 0 || anim.AnimatedTransformations.Count == 0)
            return null;

        var (f0, f1, t) = AnimFramePair(anim.AnimatedTransformations.Count, frameF, loop);
        var pose = new JointPose[ogi.Joints.Count];
        for (int i = 0; i < ogi.Joints.Count; i++)
        {
            var j = ogi.Joints[i];
            if (i < anim.JointSettings.Count)
            {
                var js = anim.JointSettings[i];
                var (translate, rot, scale) = SampleJointTRS(anim, js, f0, f1, t, j.AdditionalAnimationRotation);
                pose[i] = new JointPose { Translate = translate, Rot = rot, Scale = scale, UseParentScale = js.UseParentJointScale };
            }
            else
            {
                pose[i] = new JointPose
                {
                    Translate = new SysVec3(j.LocalTranslation.X, j.LocalTranslation.Y, j.LocalTranslation.Z),
                    Rot       = new Quaternion(j.LocalRotation.X, j.LocalRotation.Y, j.LocalRotation.Z, j.LocalRotation.W),
                    Scale     = SysVec3.One,
                };
            }
        }
        return pose;
    }

    // Amedo 2026-09-26
    private static Matrix4x4[] ComposePose(PS2AnyOGI ogi, JointPose[] pose, out Dictionary<int, Matrix4x4> jointWorlds,
                                           IJointControllers? controllers = null, // Amedo 2026-09-28
                                           PoseBuffers? buffers = null, // Amedo 2026-09-29
                                           int reactSlots = 0xFF) // Amedo
    {
        Matrix4x4[] bones;
        Dictionary<int, SysVec3> rawScale;
        Dictionary<int, Matrix4x4> animWorld;
        if (buffers is null)
        {
            bones     = IdentityBones();
            rawScale  = new Dictionary<int, SysVec3>();
            animWorld = new Dictionary<int, Matrix4x4>();
        }
        else
        {
            bones = buffers.Bones;
            Array.Fill(bones, Matrix4x4.Identity);
            rawScale = buffers.RawScale;
            rawScale.Clear();
            animWorld = buffers.Worlds;
            animWorld.Clear();
        }
        var bind = Bind(ogi);

        for (int i = 0; i < ogi.Joints.Count; i++)
        {
            var j = ogi.Joints[i];
            var p = pose[i];
            bool hasParent = i > 0 && animWorld.ContainsKey(j.ParentIndex);
            var local = Matrix4x4.CreateScale(p.Scale) * Matrix4x4.CreateFromQuaternion(p.Rot);
            if (p.UseParentScale && hasParent && rawScale.TryGetValue(j.ParentIndex, out var ps))
                local *= Matrix4x4.CreateScale(VuRcp(ps.X), VuRcp(ps.Y), VuRcp(ps.Z));
            local.Translation = p.Translate;

            rawScale[j.Index]  = p.Scale;
            var world = hasParent ? local * animWorld[j.ParentIndex] : local; // Amedo 2026-09-28
            bool react = controllers is not null && ReactJoint_2952A0(ogi, bind, i, reactSlots); // Amedo
            if (react && controllers is IJointPoser poser) // Amedo
            {
                var parent = hasParent ? animWorld[j.ParentIndex] : Matrix4x4.Identity;
                var t = p.Translate;
                var r = p.Rot;
                int f = p.Flags;
                if (poser.Pose(j.ReactId, i, ref t, ref r, ref f, world, parent, hasParent))
                {
                    p.Translate = t;
                    p.Rot = r;
                    p.Flags = f;
                    pose[i] = p;
                    local = Matrix4x4.CreateScale(p.Scale) * Matrix4x4.CreateFromQuaternion(p.Rot);
                    if (p.UseParentScale && hasParent && rawScale.TryGetValue(j.ParentIndex, out var ps2))
                        local *= Matrix4x4.CreateScale(VuRcp(ps2.X), VuRcp(ps2.Y), VuRcp(ps2.Z));
                    local.Translation = p.Translate;
                    world = hasParent ? local * parent : local;
                }
            }
            if (react && controllers is IJointBasis basis && basis.WantsPreWorld(j.ReactId, out var before)) // Amedo
            {
                var pre = Matrix4x4.CreateScale(p.Scale) * Matrix4x4.CreateFromQuaternion(before);
                if (p.UseParentScale && hasParent && rawScale.TryGetValue(j.ParentIndex, out var pps))
                    pre *= Matrix4x4.CreateScale(VuRcp(pps.X), VuRcp(pps.Y), VuRcp(pps.Z));
                pre.Translation = p.Translate;
                basis.PreWorld(j.ReactId, hasParent ? pre * animWorld[j.ParentIndex] : pre);
            }
            if (react) controllers!.Transform(j.ReactId, ref world); // Amedo
            animWorld[j.Index] = world;
        }

        for (int i = 0; i < ogi.Joints.Count; i++) // Amedo 2026-09-29
        {
            var j = ogi.Joints[i];
            if (j.Index < 0 || j.Index >= RenderPipeline.MaxBones) continue;
            if (!bind.HasInverse[i]) continue;
            bones[j.Index] = bind.Inverse[i] * animWorld[j.Index];
        }
        jointWorlds = animWorld;
        return bones;
    }

    // Amedo 2026-09-26
    private static Matrix4x4[] IdentityBones()
    {
        var bones = new Matrix4x4[RenderPipeline.MaxBones];
        for (int i = 0; i < bones.Length; i++) bones[i] = Matrix4x4.Identity;
        return bones;
    }

    // Amedo 2026-09-26
    private static Quaternion GameSlerp(in Quaternion a, in Quaternion bIn, float t)
    {
        var b = bIn; // Amedo 2026-10-27
        float dot = Quaternion.Dot(a, b);
        if (dot < 0f)
        {
            dot = -dot;
            b   = Quaternion.Negate(b);
        }
        float s0, s1;
        if (dot >= 0.999f)
        {
            s0 = 1f - t;
            s1 = t;
        }
        else
        {
            float theta = MathF.Acos(dot), inv = 1f / MathF.Sin(theta);
            s0 = MathF.Sin((1f - t) * theta) * inv;
            s1 = MathF.Sin(t * theta) * inv;
        }
        return new Quaternion(a.X * s0 + b.X * s1, a.Y * s0 + b.Y * s1, a.Z * s0 + b.Z * s1, a.W * s0 + b.W * s1);
    }

    // Amedo 2026-09-26
    private static (int F0, int F1, float T) AnimFramePair(int frames, float frameF, bool loop)
    {
        int last = frames - 1;
        if (loop && frames > 1)
        {
            frameF %= frames;
            if (frameF < 0f) frameF += frames;
            if (frameF >= frames) frameF = 0f;
            int cur = (int)frameF;
            return (cur, cur == last ? 0 : cur + 1, frameF - cur);
        }
        frameF = Math.Clamp(frameF, 0, last);
        int f0 = (int)frameF;
        return (f0, Math.Min(f0 + 1, last), frameF - f0);
    }

    private static float VuRcp(float v) => v != 0f ? 1f / v : float.MaxValue; // Amedo 2026-09-26

    private static float GsAlpha(float a) => Math.Clamp(a * (255f / 128f), 0f, 1f); // Amedo 2026-09-27

    // Amedo 2026-09-27
    public static float[] SampleFacialWeights(TwinMorphAnimation facialAnim, int f0, int f1, float t)
    {
        var weights = new float[MaxMorphShapes];
        int frames = facialAnim.AnimatedTransformations.Count;
        if (facialAnim.JointSettings.Count == 0 || frames == 0)
            return weights;
        f0 = Math.Clamp(f0, 0, frames - 1);
        f1 = Math.Clamp(f1, 0, frames - 1);

        var js = facialAnim.JointSettings[0];
        int staticIdx = js.TransformationIndex;
        int curIdx    = js.AnimationTransformationIndex;
        int nextIdx   = js.AnimationTransformationIndex;

        int shapes = Math.Min((int)js.FacialShapesAmount, MaxMorphShapes);
        for (int i = 0; i < shapes; i++)
        {
            if (js.AnimationMorph[i] == TwinTransformType.Animated)
            {
                float w0 = facialAnim.AnimatedTransformations[f0].Transforms[curIdx++].Value;
                float w1 = facialAnim.AnimatedTransformations[f1].Transforms[nextIdx++].Value;
                weights[i] = w1 * t + w0 * (1f - t); // Amedo 2026-09-26
            }
            else
            {
                weights[i] = facialAnim.StaticTransformations[staticIdx++].Value;
            }
        }
        return weights;
    }

    // Amedo 2026-09-26
    private static (SysVec3 Translate, Quaternion Rot, SysVec3 Scale) SampleJointTRS(
        TwinAnimation anim, TwinJointSettings js, int f0, int f1, float t,
        Twinsanity.TwinsanityInterchange.Common.Vector4 additionalRotation)
    {
        int staticIdx = js.TransformationIndex;
        int curIdx    = js.AnimationTransformationIndex;
        int nextIdx   = js.AnimationTransformationIndex;

        float Lerp(float c, float n) => c - c * t + n * t;

        var (cx, nx) = SampleLinear(anim, js.TranslateX, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (cy, ny) = SampleLinear(anim, js.TranslateY, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (cz, nz) = SampleLinear(anim, js.TranslateZ, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (crx, nrx) = SampleRotation(anim, js.RotateX, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (cry, nry) = SampleRotation(anim, js.RotateY, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (crz, nrz) = SampleRotation(anim, js.RotateZ, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (csx, nsx) = SampleLinear(anim, js.ScaleX, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (csy, nsy) = SampleLinear(anim, js.ScaleY, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (csz, nsz) = SampleLinear(anim, js.ScaleZ, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);

        var translate = new SysVec3(Lerp(cx, nx), Lerp(cy, ny), Lerp(cz, nz));
        var rot       = GameEulerToQuaternion(Lerp(crx, nrx), Lerp(cry, nry), Lerp(crz, nrz));
        var scale     = new SysVec3(Lerp(csx, nsx), Lerp(csy, nsy), Lerp(csz, nsz));

        if (js.UseAdditionalRotation)
            rot = new Quaternion(additionalRotation.X, additionalRotation.Y, additionalRotation.Z, additionalRotation.W) * rot;

        return (translate, rot, scale);
    }

    // Amedo 2026-09-26
    public static Quaternion GameEulerToQuaternion(float rx, float ry, float rz) // Amedo 2026-10-04
    {
        float hx = rx * 0.5f, hy = ry * 0.5f, hz = rz * 0.5f;
        float sx = MathF.Sin(hx), cx = MathF.Cos(hx);
        float sy = MathF.Sin(hy), cy = MathF.Cos(hy);
        float sz = MathF.Sin(hz), cz = MathF.Cos(hz);
        return new Quaternion(
            sx * cy * cz - cx * sy * sz,
            cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz);
    }

    private static (float cur, float next) SampleLinear(TwinAnimation anim, TwinTransformType type,
        int f0, int f1, ref int staticIdx, ref int curIdx, ref int nextIdx)
    {
        if (type == TwinTransformType.Animated)
        {
            float c = anim.AnimatedTransformations[f0].Transforms[curIdx++].Value;
            float n = anim.AnimatedTransformations[f1].Transforms[nextIdx++].Value;
            return (c, n);
        }
        float v = anim.StaticTransformations[staticIdx++].Value;
        return (v, v);
    }

    private static readonly float AnimAngleToRad = BitConverter.Int32BitsToSingle(0x38C90FDB); // Amedo 2026-09-26

    // Amedo 2026-09-26
    private static (float cur, float next) SampleRotation(TwinAnimation anim, TwinTransformType type,
        int f0, int f1, ref int staticIdx, ref int curIdx, ref int nextIdx)
    {
        if (type != TwinTransformType.Animated)
        {
            float s = (anim.StaticTransformations[staticIdx++].PureValue << 4) * AnimAngleToRad;
            return (s, s);
        }
        int a = anim.AnimatedTransformations[f0].Transforms[curIdx++].PureValue << 4;
        int b = anim.AnimatedTransformations[f1].Transforms[nextIdx++].PureValue << 4;
        int d = a - b;
        if (d > 0x8000) a -= 0x10000;
        else if (d < -0x8000) a += 0x10000;
        return (a * AnimAngleToRad, b * AnimAngleToRad);
    }
}
