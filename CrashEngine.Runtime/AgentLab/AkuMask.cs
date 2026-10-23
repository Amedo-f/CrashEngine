using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
internal sealed class AkuMask
{
    private static readonly Vector3 Offset = new(0.7f, 2f, 0f);

    private readonly AgentLabAgent _owner;
    private float _t;
    private float _distance;
    private Vector3 _away;

    public int State { get; private set; }
    public AgentLabAgent? Target { get; private set; }
    public ushort[] Emitters { get; } = [0xFFFF, 0xFFFF, 0xFFFF];
    public int EmitterCount { get; set; }

    public AkuMask(AgentLabAgent owner) => _owner = owner;

    private AgentLabWorld World => _owner.World;
    private bool TargetIsPlayer => Target is not null && ReferenceEquals(Target, World.Player);
    private int HitPoints => World.PlayerCharacter?.HitPoints ?? 0;
    private bool Invincible => World.PlayerCharacter is { } c && (c.MotionWord & 0x400) != 0;

    public void Init()
    {
        Hide();
        Target = _owner.SpawnedBy;
        State  = 0;
    }

    private bool _modeHidden; // Amedo 2026-09-28
    private bool _modeWasVisible;

    public void Update(float dt)
    {
        if (World.CutsceneActive) // Amedo 2026-09-28
        {
            if (!_modeHidden)
            {
                _modeHidden = true;
                _modeWasVisible = _owner.Rendered;
                _owner.SetRendered(false);
            }
        }
        else if (_modeHidden)
        {
            _modeHidden = false;
            if (_modeWasVisible) _owner.SetRendered(true);
        }
        switch (State)
        {
            case 0: Idle(); break;
            case 1: FlyIn(dt); break;
            case 2: FlyAway(dt); break;
            case 3: Normal(dt); break;
            case 4: Boosted(dt); break;
            case 5: Face_117AD8(dt); break; // Amedo 2026-10-04
        }
    }

    private void Hide()
    {
        _owner.ClearTrails(); // Amedo 2026-09-27
        _owner.ClearAgentFlags(0x8); // Amedo
        _owner.SetRendered(false);
    }

    private void Idle()
    {
        if (!TargetIsPlayer)
        {
            _owner.SetRendered(false);
            return;
        }
        if (HitPoints is 2 or 3 or 4) Appear();
    }

    private void Appear()
    {
        _t = 0f;
        var p = Target!.GamePosition;
        var start = new Vector3(p.X, p.Y + 20f, p.Z + 3f);
        SetPosition(start);
        float dx = start.X - p.X, dy = start.Y - p.Y, dz = start.Z - p.Z;
        _distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        _owner.SetRendered(true);
        State = 1;
    }

    private void FlyIn(float dt)
    {
        if (!TargetIsPlayer)
        {
            Hide();
            State = 0;
            return;
        }
        var t = TargetPoint();
        var m = _owner.GamePosition;
        _t += dt * 4f;
        if (1f <= _t)
        {
            Arrive();
            return;
        }
        float r = _distance * Smooth(_t * 0.5f + 0.5f);
        var d = m - t;
        float k = Character.CharacterController.InverseLength(d);
        SetPosition(new Vector3(d.X * k * r + t.X, d.Y * k * r + t.Y, d.Z * k * r + t.Z));
        CopyRotation();
    }

    private void Arrive()
    {
        State = 3;
        _owner.PlaySlot(0, 0f, true);
        _owner.PlaySoundSlot(0); // Amedo 2026-09-28
    }

    private void Normal(float dt)
    {
        if (!TargetIsPlayer)
        {
            Hide();
            State = 0;
            return;
        }
        int hp = HitPoints;
        if (hp < 2) Leave();
        else if (hp < 3) Follow(dt);
        else GoGolden();
    }

    private void Follow(float dt)
    {
        var m = _owner.GamePosition;
        Vector3 t;
        if (World.PlayerCharacter?.Vehicle is null) // Amedo 2026-09-29
        {
            t = TargetPoint();
            CopyRotation();
        }
        else
        {
            var (min, max) = Target!.TreeBox;
            t = new Vector3((max.X - min.X) * 0.5f + min.X + Offset.X,
                            (max.Y - min.Y) * 0.5f + min.Y + Offset.Y,
                            (max.Z - min.Z) * 0.5f + min.Z + Offset.Z);
            TurnTowards_22CD68(dt * 8f, t);
        }
        float k = dt * 4f;
        var step = new Vector3((t.X - m.X) * k, (t.Y - m.Y) * k, (t.Z - m.Z) * k);
        if (MathF.Abs(step.X) <= 5e-5f && MathF.Abs(step.Y) <= 5e-5f && MathF.Abs(step.Z) <= 5e-5f) return;
        SetPosition(new Vector3(m.X + step.X, m.Y + step.Y, m.Z + step.Z));
    }

    private void Leave()
    {
        _t = 0f;
        var p = Target!.GamePosition;
        _away = new Vector3(p.X, p.Y + 20f, p.Z + 3f);
        State = 2;
        float dx = p.X - _away.X, dy = p.Y - _away.Y, dz = p.Z - _away.Z;
        _distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        _owner.PlaySlot(2, 0f, false);
        _owner.PlaySoundSlot(1); // Amedo 2026-09-28
    }

    private void FlyAway(float dt)
    {
        if (!TargetIsPlayer)
        {
            Hide();
            State = 0;
            return;
        }
        var dest = _away;
        var m = _owner.GamePosition;
        _t += dt;
        if (1f <= _t)
        {
            Hide();
            State = 0;
            return;
        }
        float r = _distance * (1f - Smooth(_t * 0.5f));
        var d = m - dest;
        float k = Character.CharacterController.InverseLength(d);
        SetPosition(new Vector3(d.X * k * r + dest.X, d.Y * k * r + dest.Y, d.Z * k * r + dest.Z));
        CopyRotation();
    }

    private void GoGolden()
    {
        State = 4;
        _owner.PlaySlot(1, 0f, true);
        _owner.PlaySoundSlot(2); // Amedo 2026-09-28
        _owner.ClearTrails(); // Amedo 2026-09-27
        _owner.AddDefaultTrail(Emitters[2]);
    }

    private void Boosted(float dt)
    {
        if (!TargetIsPlayer)
        {
            Hide();
            State = 0;
            return;
        }
        if (HitPoints < 3) LeaveGolden();
        else if (!Invincible) Follow(dt);
        else GoFace_1178E8(); // Amedo 2026-10-04
    }

    // Amedo 2026-10-04
    private readonly int[] _faceFx = [-1, -1, -1];
    private bool _onFace;
    private bool _fxLost;
    private object? _faceChunk;
    private Matrix4x4 _faceTarget;

    // Amedo 2026-10-04
    private Matrix4x4 FaceTarget(float forward, float down)
    {
        var m = Target!.ExitAnchor_293B70(1);
        m.Translation += new Vector3(m.M31, m.M32, m.M33) * forward - new Vector3(0f, down, 0f);
        return m;
    }

    // Amedo 2026-10-04
    private void GoFace_1178E8()
    {
        State = 5;
        _owner.ClearTrails();
        _owner.PlaySoundSlot(4);
        _owner.PlaySlot(2, 0f, true);
        _onFace = false;
        _fxLost = false;
        _faceChunk = Target!.Chunk;
        _t = 0f;
        _faceTarget = FaceTarget(0.15f, 0f);
        _distance = Vector3.Distance(_owner.GamePosition, _faceTarget.Translation);
    }

    // Amedo 2026-10-04
    private void Face_117AD8(float dt)
    {
        if (!TargetIsPlayer)
        {
            StopFaceFx();
            Hide();
            State = 0;
            return;
        }
        if (HitPoints < 2)
        {
            StopFaceFx();
            Leave();
            return;
        }
        if (!Invincible)
        {
            LeaveFace_1181D8();
            return;
        }
        _faceTarget = FaceTarget(0.1f, 0.2f);
        var p = _owner.GamePosition;
        if (!ReferenceEquals(_faceChunk, Target!.Chunk))
        {
            _faceChunk = Target.Chunk;
            if (!World.ClassicAku)
            {
                StopFaceFx();
                _fxLost = true;
            }
        }
        for (int i = 0; i < _faceFx.Length && !_fxLost; i++)
            _faceFx[i] = _owner.UpdateEmitter_23E8F8(Emitters[0] == 0xFFFF ? (ushort)0xFFFF : (ushort)(Emitters[0] + i), 5, Target!, _faceFx[i]);
        _owner.SetRendered(Target!.Rendered);
        if (_onFace)
        {
            World.PlayerCharacter?.Bones.SetNose_15F388(0.5f);
            return;
        }
        _t += dt * 8f;
        var t = _faceTarget.Translation;
        float r = _distance * Smooth(_t * 0.5f + 0.5f);
        var d = p - t;
        float k = Character.CharacterController.InverseLength(d);
        SetPosition(new Vector3(d.X * k * r + t.X, d.Y * k * r + t.Y, d.Z * k * r + t.Z));
        SetRotationFrom(_faceTarget);
        if (_t < 1f) return;
        SetPosition(t);
        _onFace = true;
        Target!.AttachAtExit_195C78(_owner, 1);
    }

    // Amedo 2026-10-04
    private void LeaveFace_1181D8()
    {
        State = 4;
        _owner.SetRendered(true);
        StopFaceFx();
        Target?.DetachExitPoint_1965F8(1, keepLink: true); // Amedo
        _onFace = false;
        _owner.PlaySoundSlot(6);
        _owner.PlaySlot(1, 0f, true);
        Target?.ClearTrails();
        _owner.ClearTrails();
        _owner.AddDefaultTrail(Emitters[2]);
    }

    // Amedo 2026-10-04
    private void StopFaceFx()
    {
        for (int i = 0; i < _faceFx.Length; i++) _faceFx[i] = _owner.StopEmitter_23EB00(_faceFx[i]);
    }

    // Amedo 2026-10-04
    private void SetRotationFrom(Matrix4x4 source)
    {
        var m = Camera.CamMath.QuatToMatrix_18E620(Camera.CamMath.MatrixToQuat_186900(source));
        m.Translation = _owner.GamePosition;
        _owner.GameMatrix = m;
    }

    private void LeaveGolden()
    {
        State = 3;
        _owner.PlaySlot(0, 0f, true);
        _owner.PlaySoundSlot(3); // Amedo 2026-09-28
        _owner.ClearTrails(); // Amedo 2026-09-27
    }

    private static float Smooth(float u)
    {
        float a = u + u;
        float b = u * 3f;
        a *= u;
        b = u * b;
        a *= u;
        return b - a;
    }

    private Vector3 TargetPoint() => Vector3.Transform(Offset, Target!.GameMatrix);

    private void SetPosition(Vector3 p)
    {
        var m = _owner.GameMatrix;
        m.Translation = p;
        _owner.GameMatrix = m;
    }

    private void CopyRotation()
    {
        var m = Target!.GameMatrix;
        m.Translation = _owner.GamePosition;
        _owner.GameMatrix = m;
    }

    // Amedo 2026-09-29
    private void TurnTowards_22CD68(float rate, Vector3 target)
    {
        float t = MathF.Min(rate, 1f);
        var node = _owner.GameMatrix;
        var pos = node.Translation;
        var d = new Vector3(target.X - pos.X, 0f, target.Z - pos.Z);
        d *= Character.CharacterController.InverseLength(d);
        if (d.X == 0f && d.Z == 0f) return;
        var fwd = new Vector3(node.M31, 0f, node.M33);
        if (0.99999f < fwd.X * d.X + fwd.Y * d.Y + fwd.Z * d.Z) return;
        var f = Camera.CamMath.Slerp_188300(t, fwd, d);
        f *= Character.CharacterController.InverseLength(f);
        Camera.CamMath.Frame_1879D0(f, out var right, out var up);
        var rows = new Matrix4x4(right.X, right.Y, right.Z, 0f, up.X, up.Y, up.Z, 0f, f.X, f.Y, f.Z, 0f, 0f, 0f, 1f, 1f);
        var m = Camera.CamMath.QuatToMatrix_18E620(Camera.CamMath.MatrixToQuat_186900(rows));
        m.Translation = pos;
        _owner.GameMatrix = m;
    }
}
