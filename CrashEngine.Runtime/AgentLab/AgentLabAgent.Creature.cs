using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private uint _motion14;
    private Vector4 _velocity60 = new(0f, 0f, 0f, 1f);

    public uint Motion14 => _motion14;

    private void CreatureApplyState_13D688()
    {
        _motion14 = (_motion14 & ~1u) | ((Instance.Source.StateFlags & 0x40000) != 0 ? 1u : 0u);
        InitCreatureHitPoints();
        if ((_motion14 & 1) != 0) CreatureSnap();
    }

    internal void CreatureGroup_17A9A0()
    {
        if (_destroyed || !Entity.Active || ControllerType != 15 || OwnCharacter is not null) return;
        CreatureFrame_140A70();
    }

    private void CreatureFrame_140A70()
    {
        if ((_motion14 & 0x20) != 0)
        {
            if ((_motion14 & 4) != 0) _motion14 &= ~0x20u;
            return;
        }
        if ((_motion14 & 1) != 0)
        {
            CreatureSnap();
            return;
        }
        float down = _velocity60.X * 0f + _velocity60.Y * -1f + _velocity60.Z * 0f;
        if (FloatProp_2618B8(3) < down) _motion14 |= 0x20;
    }

    private void CreatureSnap()
    {
        bool hit = SnapInstanceToGround_13D428();
        _motion14 = (_motion14 & ~4u) | (hit ? 4u : 0u);
    }

    internal bool SnapInstanceToGround_13D428()
    {
        var pos = GamePosition;
        var start = new Vector3(pos.X, pos.Y + 1f, pos.Z);
        var ray = new Vector3(0f, -7f, 0f);
        if (!SceneRay_280958(start, ref ray, 0x15B010, false)) return false;
        float lift = FloatProp_2618B8(5);
        var p = new Vector3(start.X + ray.X, start.Y + ray.Y + lift, start.Z + ray.Z);
        if (p != pos) SetLocalPosition(p);
        return true;
    }

    private float FloatProp_2618B8(int index)
    {
        var own = Instance.Source.ParamList2;
        int count = own?.Count ?? 0;
        if (index < count) return FloatParams is { } runtime && index < runtime.Length ? runtime[index] : own![index];
        var defaults = GameObject?.InstFloats;
        int k = index - count;
        return defaults is not null && k < defaults.Count ? defaults[k] : 0f;
    }
}
