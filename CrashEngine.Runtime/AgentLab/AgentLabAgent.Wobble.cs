using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private const float WobbleUnits = 10430.378f;
    private const float WobbleRadians = 9.58738e-5f;

    private sealed class WobbleState
    {
        public List<uint> Args = null!;
        public readonly float[] Amp = new float[3];
        public readonly int[] Phase = new int[3];
        public readonly int[] PhaseOffset = new int[3]; // Amedo
        public readonly int[] Angles = new int[3];
        public Matrix4x4? AttachedTurn; // Amedo
        public int SubMode;
        public double Start;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector3 Offset;
        public int Mode;
        public int Settle;
        public Vector3 RestPose;
        public Quaternion RestQuat = Quaternion.Identity;
        public float SettleDistance2 = 4f; // Amedo
        public bool HasRestPose;
        public Vector3 SpringBox;
    }

    private bool _springRest_88; // Amedo

    private WobbleState? _wobble;

    public bool Wobbling => _wobble is not null;

    private static uint WArg(List<uint> a, int i) => i < a.Count ? a[i] : 0u;
    private static float WFloat(List<uint> a, int i) => BitConverter.UInt32BitsToSingle(WArg(a, i));

    private float RandomSigned() => World.Rng.RandomSigned_18C0F0(); // Amedo

    private void SetWobble(List<uint> args)
    {
        if (_wobble is { } current && ReferenceEquals(current.Args, args)) return;
        uint a = WArg(args, 26);
        int mode = (int)((a >> 28) & 7);
        if (mode == 4)
        {
            World.Unimplemented("command", 12, "wobble mode 4 (0x23FFA0)");
            return;
        }
        var w = _wobble ??= new WobbleState();
        w.Args = args;
        w.Start = World.Now;
        w.Mode = mode;
        w.Settle = 0;
        w.SettleDistance2 = 4f; // Amedo
        if ((WArg(args, 12) & 0xFF) == 0xFF) { w.RestPose = LocalMatrix.Translation; w.RestQuat = LocalRotation; w.HasRestPose = true; } // Amedo
        if (mode == 1) StartSprings_2380C0(w); // Amedo
        if (mode is 1 or 2 or 3) // Amedo
        {
            StartContactRigid(args, mode);
            if (mode == 1) SpringsReady_2380C0(w); // Amedo
            UpdateContactRigid(w);
            return;
        }
        for (int i = 0; i < 3; i++)
        {
            w.Amp[i] = WFloat(args, 3 + i);
            float phase = WFloat(args, 6 + i);
            if (((a >> (16 + i)) & 1) != 0) phase = World.Rng.RandomAround_18C210(0f, phase); // Amedo
            w.Phase[i] = (int)(phase * WobbleUnits);
            w.PhaseOffset[i] = w.Phase[i]; // Amedo
        }
        w.SubMode = (int)(WArg(args, 27) & 7);
        UpdateWobble(StepDt);
    }

    private void ClearWobble() => _wobble = null;

    // Amedo
    private void LetGoOfTrajectory_240220()
    {
        if (_wobble is { Mode: >= 1 and <= 3 }) ReleaseRigidBody_23DED8(); // Amedo
    }

    // Amedo
    private void ReleaseTrajectory_23DD80()
    {
        if (_wobble is not null) LetGoOfTrajectory_240220();
        _wobble = null;
    }

    // Amedo
    private void ReleaseMotionBlock_23DDD0()
    {
        if (_contactResponse is not null)
        {
            if (_wobble is not null) LetGoOfTrajectory_240220();
            _wobble = null;
            _contactResponse = null;
        }
        AgentFlags &= ~0x8000u;
    }

    // Amedo
    private void AlterWobblePhase_21C320(List<uint> a)
    {
        if (_wobble is not { } w) return;
        var root = RootController();
        uint axes = WArg(a, 3);
        for (int i = 0; i < 3; i++)
            if (((axes >> i) & 1) != 0) w.PhaseOffset[i] = (int)(root.SoundVariable(WArg(a, i)) * 182.04445f);
    }

    // Amedo
    private void SetWobbleAmplitude_223AF8(List<uint> a)
    {
        if (_wobble is not { } w) return;
        var root = RootController();
        uint axes = WArg(a, 3);
        for (int i = 0; i < 3; i++)
            if (((axes >> i) & 1) != 0) w.Amp[i] = root.SoundVariable(WArg(a, i));
    }

    internal void PreWobble()
    {
        float dt = StepDt; // Amedo
        if (_wobble is { Mode: 1 or 2 or 3 } rigid) { UpdateContactRigid(rigid); return; } // Amedo
        if (_wobble is not null) UpdateWobble(dt); // Amedo 2026-09-30
    }

    // Amedo
    private void HoldTrajectory_23B140()
    {
        if (_wobble is not { Mode: 0 } w || (WArg(w.Args, 12) & 0xFF) != 0xFF) return;
        if (((WArg(w.Args, 26) >> 12) & 1) == 0)
        {
            if (LocalMatrix.Translation != w.RestPose) SetLocalPosition(w.RestPose);
            return;
        }
        if (_attachedTo is not null) return; // Amedo: 0x18E620 on the attachment, rebuilt by TrajectoryFrame
        if (LocalRotation != w.RestQuat) SetLocalRotation(w.RestQuat);
    }

    // Amedo
    private void SetTrajectoryRest50(Vector3 position)
    {
        if (_wobble is { } w) w.RestPose = position;
    }

    // Amedo
    private void SetTrajectoryRest60(Quaternion rotation)
    {
        if (_wobble is { } w) w.RestQuat = rotation;
    }

    private void UpdateWobble(float dt)
    {
        if (_wobble is not { } w) return;
        var args = w.Args;
        uint a = WArg(args, 26), b = WArg(args, 27);
        Span<float> angle = stackalloc float[3];
        if (w.SubMode == 0)
            for (int i = 0; i < 3; i++)
                if (((b >> (3 + i)) & 1) != 0) angle[i] = Oscillate(w, i, dt);
        if (w.SubMode is 1 or 2 && WobbleSource_238748(w.SubMode)?._wobble is { } source) // Amedo
            for (int i = 0; i < 3; i++)
                if (((b >> (3 + i)) & 1) != 0)
                {
                    w.Phase[i] = (source.Phase[i] + w.PhaseOffset[i]) & 0xFFFF;
                    angle[i] = Wave(w, i);
                }

        bool rest = (WArg(args, 12) & 0xFF) == 0xFF;
        int space = (int)(a & 7);
        if (((a >> 12) & 1) != 0)
        {
            if (rest && space is 1 or 2 or 7)
                for (int i = 0; i < 3; i++) w.Angles[i] = (int)(angle[i] * WobbleUnits);
            else
                w.Rotation = Camera.CamMath.Euler_186780(Camera.CamMath.FromValue_11BFA0(angle[0], 0), // Amedo
                    Camera.CamMath.FromValue_11BFA0(angle[1], 0), Camera.CamMath.FromValue_11BFA0(angle[2], 0));
        }
        else w.Offset = new Vector3(angle[0], angle[1], angle[2]);

        float t = (World.NowTicks - TicksOf(w.Start)) * TickTime; // Amedo
        float duration = WFloat(args, 11);
        if (((a >> 13) & 1) != 0)
        {
            if (duration < t) { EndWobble(); return; }
            float rate = WFloat(args, 10);
            for (int i = 0; i < 3; i++) w.Amp[i] = WFloat(args, 3 + i) - rate * t;
        }
        else if (((b >> 7) & 1) != 0)
        {
            float s = t / duration;
            float k = 3f * s * s - 2f * s * s * s;
            for (int i = 0; i < 3; i++) w.Amp[i] = WFloat(args, 3 + i) * k;
        }
    }

    // Amedo
    private AgentLabAgent? WobbleSource_238748(int subMode)
    {
        if (subMode == 2) return LinkAt(0);
        if (!_hasFocusObject || _focusObject is not { } focus) return null;
        if (!focus.Asleep) return focus;
        _focusObject = null;
        _hasFocusObject = _hasFocusPosition = false;
        return null;
    }

    private float Oscillate(WobbleState w, int axis, float dt)
    {
        var args = w.Args;
        uint a = WArg(args, 26);
        int wave = (int)((a >> (3 + 3 * axis)) & 7);
        float freq = WFloat(args, axis);
        int step = wave == 5 ? (int)(w.Amp[axis] * dt * freq * WobbleUnits) : (int)(dt * freq * WobbleUnits);
        w.Phase[axis] = (w.Phase[axis] + step) & 0xFFFF;
        return Wave(w, axis);
    }

    // Amedo
    private float Wave(WobbleState w, int axis)
    {
        var args = w.Args;
        uint a = WArg(args, 26);
        int wave = (int)((a >> (3 + 3 * axis)) & 7);
        float freq = WFloat(args, axis);
        float v;
        switch (wave)
        {
            case 1: v = w.Amp[axis] * Camera.CamMath.SinCos(w.Phase[axis] * WobbleRadians).Sin; break; // Amedo
            case 2:
            case 3: v = ((int)(freq * (w.Phase[axis] * WobbleRadians)) & 1) != 0 ? -w.Amp[axis] : w.Amp[axis]; break;
            case 4: v = World.Rng.GetRandFloat__18C130(w.Amp[axis]); break; // Amedo
            case 5: v = w.Phase[axis] * WobbleRadians; break;
            default: return 0f;
        }
        return ((a >> (20 + 2 * axis)) & 3) switch
        {
            2 => MathF.Abs(v),
            1 => -MathF.Abs(v),
            _ => v,
        };
    }

    private void EndWobble() => _wobble = null;

    // Amedo
    private static Quaternion Rotate_19A5B8(Quaternion q, Quaternion node)
    {
        const float E = 1e-4f;
        if (MathF.Abs(q.X) <= E && MathF.Abs(q.Y) <= E && MathF.Abs(q.Z) <= E && (MathF.Abs(q.W - 1f) <= E || MathF.Abs(q.W + 1f) <= E)) return node;
        var r = Camera.CamMath.Mul_18D558(q, node);
        float k = Camera.CamMath.QuatInvLength_11C0B8(r, 0f, 1e-10f);
        return new Quaternion(r.X * k, r.Y * k, r.Z * k, r.W * k);
    }

    // Amedo
    private void TrajectoryFrame_23A6D8()
    {
        if (_wobble is not { Mode: 0 } w) return;
        var args = w.Args;
        uint a = WArg(args, 26);
        if ((WArg(args, 12) & 0xFF) != 0xFF) return;
        int space = (int)(a & 7);
        var rotation = LocalRotation;
        var position = LocalMatrix.Translation;
        if (((a >> 12) & 1) != 0)
        {
            Quaternion LocalAxes(Quaternion from) // Amedo
            {
                if (w.Angles[0] != 0) from = Camera.CamMath.Mul_18D558(from, Camera.CamMath.RotationX_18D590(w.Angles[0]));
                if (w.Angles[1] != 0) from = Camera.CamMath.Mul_18D558(from, Camera.CamMath.RotationY_18D620(w.Angles[1]));
                if (w.Angles[2] != 0) from = Camera.CamMath.Mul_18D558(from, Camera.CamMath.RotationZ_18D6B0(w.Angles[2]));
                return from;
            }
            if (space == 1 && _attachedTo is not null) // Amedo
            {
                w.AttachedTurn = Camera.CamMath.QuatToMatrix_18E620(Camera.CamMath.Euler_186780(w.Angles[0], w.Angles[1], w.Angles[2]));
                return;
            }
            w.AttachedTurn = null;
            Quaternion result = space switch
            {
                1 => LocalAxes(RotationOf(_initMatrix)), // Amedo
                2 => LocalAxes(rotation),
                7 when _storedSpace is { } stored => LocalAxes(RotationOf(stored)),
                _ => Rotate_19A5B8(w.Rotation, rotation), // Amedo
            };
            SetLocalRotation(result);
            return;
        }
        var offset = space switch
        {
            1 => Vector3.Transform(w.Offset, RotationOf(_initMatrix)), // Amedo
            2 => Vector3.Transform(w.Offset, rotation),
            7 when _storedSpace is { } stored => Vector3.TransformNormal(w.Offset, stored),
            _ => w.Offset,
        };
        if ((a & 0x0C008000) != 0) World.Unimplemented("command", 12, "position wobble flags 0x0C008000");
        if (MathF.Abs(offset.X) <= 5e-5f && MathF.Abs(offset.Y) <= 5e-5f && MathF.Abs(offset.Z) <= 5e-5f) return;
        SetLocalPosition(position + offset);
    }
}
