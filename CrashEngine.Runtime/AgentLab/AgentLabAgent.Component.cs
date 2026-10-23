using CrashEngine.Importer;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private AkuMask? _mask;

    public int? MaskState => _mask?.State;
    private bool _renderedLoose = true;
    public bool Rendered => ControllerType >= 0 ? (AgentFlags & 0x400) != 0 : _renderedLoose;

    private void AddComponent(uint arg0)
    {
        if ((arg0 & 0x100) != 0 && (_mask is not null || _bossAim is not null || _spline is not null || _skate is not null)) return; // Amedo
        int type = (int)(arg0 & 0xFF);
        switch (type) // Amedo
        {
            case 0:
                ClearComponent_23DC90();
                _bossAim = new BossAim_115D88(this);
                return;
            case 1:
                ClearComponent_23DC90();
                _mask = new AkuMask(this);
                _mask.Init();
                return;
            case 2: // Amedo
                ClearComponent_23DC90();
                _spline = new SplineFollower_121F48();
                return;
            case 3: // Amedo
                ClearComponent_23DC90();
                _skate = new SkateController_121FD0(this);
                SkateStart_1220F8(_skate);
                return;
        }
    }

    // Amedo
    private void ClearComponent_23DC90()
    {
        if (_bossAim?.Owner?.Anim is { } anim) _bossAim.Detach_121960(anim);
        _bossAim = null;
        _mask = null;
        _spline = null; // Amedo
        _skate = null; // Amedo
    }

    private void SetComponentEmitters(List<uint> args)
    {
        if (_mask is null) return;
        int count = args.Count > 6 ? (int)(args[6] & 0xF) : 0;
        for (int i = 0; i < count; i++)
        {
            uint word = args[i / 2];
            ushort id = (ushort)(i % 2 == 0 ? word & 0xFFFF : word >> 16);
            int slot = _mask.EmitterCount & 0xF;
            _mask.EmitterCount = (_mask.EmitterCount + 1) & 0xF;
            if (slot < _mask.Emitters.Length) _mask.Emitters[slot] = id;
        }
    }

    // Amedo 2026-10-01
    private System.Numerics.Matrix4x4 _motionPrev, _motionCur;
    private float _motionDt;
    private bool _motionValid;

    // Amedo 2026-10-01
    internal bool HasMotionComponent => (Instance.Source.StateFlags & 0x20) != 0;

    // Amedo 2026-10-01
    private void UpdateMotionComponent_192270()
    {
        if (!HasMotionComponent) return;
        if (!_motionValid || !World.ChunkClocksRunning) // Amedo
        {
            ResetMotionComponent_192130();
            return;
        }
        _motionPrev = _motionCur;
        _motionCur = GameMatrix;
        _motionDt = (int)Math.Round(World.Dt * 9000.0) * (1f / 9000f);
    }

    // Amedo 2026-10-01
    private void ResetMotionComponent_192130()
    {
        _motionDt = 0.016666668f;
        _motionPrev = GameMatrix;
        _motionCur = _motionPrev;
        _motionValid = true;
    }

    // Amedo 2026-10-05
    internal System.Numerics.Vector3? MotionVelocity_192438()
    {
        if (!HasMotionComponent) return null;
        var (prev, cur, dt) = MotionFrames_192360();
        return (cur.Translation - prev.Translation) * (1f / dt);
    }

    // Amedo 2026-10-01
    internal (System.Numerics.Matrix4x4 Prev, System.Numerics.Matrix4x4 Cur, float Dt) MotionFrames_192360()
    {
        if (!_motionValid) ResetMotionComponent_192130();
        return (_motionPrev, _motionCur, _motionDt);
    }

    // Amedo 2026-10-01
    internal void MotionGroup_192270()
    {
        if (_destroyed || !Entity.Active) return;
        UpdateMotionComponent_192270();
    }

    // Amedo 2026-10-01
    internal void ObjectComponent_114()
    {
        if (_destroyed || !Entity.Active) return;
        float dt = (int)Math.Round(World.Dt * 9000.0) * (1f / 9000f); // Amedo
        if (_mask is not null) _mask.Update(dt);
        else if (_spline is { } spline) SplineUpdate_118E48(spline, dt);
        else if (_skate is { } skate) SkateFrame_1197D8(skate); // Amedo
    }

    // Amedo 2026-10-01
    internal void CrateGroup_17A9A0()
    {
        if (_destroyed || !Entity.Active || ControllerType != 13) return;
        CrateStep((int)Math.Round(World.Dt * 9000.0) * (1f / 9000f));
    }

    public void SetRendered(bool on)
    {
        if (ControllerType >= 0) AgentFlags = on ? AgentFlags | 0x400 : AgentFlags & ~0x400u;
        else _renderedLoose = on;
        ApplyRendered();
    }

    private void ApplyRendered()
    {
        if (Anim is { } anim && anim.VisibleRoot is { } shown) // Amedo 2026-09-28
        {
            shown.Active = Rendered;
            return;
        }
        foreach (var child in Entity.Children) child.Active = Rendered;
    }

    internal void PlaySlot(int slot, float blend, bool loop)
    {
        if (Anim is null) return;
        if (GameObject is { } own && (slot >= own.OGISlots.Count || own.OGISlots[slot] == 0xFFFF)) return; // Amedo
        var group = Anim.Groups.FirstOrDefault(g => g.SlotIndex == slot);
        if (group is null)
        {
            if (Anim.ModelGroups.FirstOrDefault(g => g.SlotIndex == slot) is { } model) // Amedo 2026-09-28
            {
                Trace($"slot {slot}: model only (OGI {model.Ogi.GetID():X})");
                Anim.ShowModel(model, blend);
            }
            else Anim.RequestNone(blend);
            if (!Rendered) ApplyRendered();
            return;
        }
        bool blends = 0f < blend && (int)(blend * 9000f) != 0; // Amedo
        uint flags = (loop ? AnimatedObject.LoopFlag : 0u) | (blends ? AnimatedObject.BlendFlag : 0u) | AnimatedObject.AppendFlag;
        Anim.Request(group, flags, blend, 1f);
        if (!Rendered) ApplyRendered();
    }
}
