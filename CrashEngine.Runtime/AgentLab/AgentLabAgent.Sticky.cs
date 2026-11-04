using System.Numerics;
using CrashEngine.Runtime.Camera;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private const int MotionBlockWords = 34;
    private float _attachPush1C;

    private void BecomeSticky_11F440(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        if (_contactResponse is null)
        {
            var plain = new List<uint>(new uint[MotionBlockWords]);
            plain[12] = 0xFF;
            _contactResponse = plain;
            _ownedResponse = plain;
            AgentFlags |= 0x8000;
        }
        AgentFlags |= 0x8000;
        var mb = WritableResponse();
        mb[27] |= 0x2000;
        mb[28] |= Arg(0);
        mb[29] = Arg(1);
        mb[31] = Arg(3);
        if ((Arg(2) & 0xFFFF) != 0) mb[32] = (mb[32] & 0xFFFF0000u) | (Arg(2) & 0xFFFF);
        Trace($"BecomeSticky mask 0x{mb[28]:X} push {BitConverter.UInt32BitsToSingle(mb[29])} filter 0x{mb[32] & 0xFFFF:X} event 0x{mb[31] & 0xFFFF:X}");
    }

    private void BecomeNormal_11F4F8(uint arg0)
    {
        if (_contactResponse is null) return;
        var mb = WritableResponse();
        mb[27] = (mb[27] & ~0x2000u) | ((arg0 & 1) << 13);
        if ((arg0 & 1) == 0) MakeMotionBlockNormal_23F250();
    }

    private void SetNode120Flag_11F610(uint arg0)
    {
        if (_contactResponse is null) return;
        var mb = WritableResponse();
        if ((arg0 & 1) == 0) MakeMotionBlockNormal_23F250();
        mb[32] &= 0xFFFF0000u;
        mb[27] &= ~0x2000u;
        mb[28] = 0;
        mb[29] = 0;
        mb[31] = 0;
    }

    private bool SticksToMotionBlock_2362B8(List<uint> mb, AgentLabAgent sender)
    {
        uint filter = WArg(mb, 32) & 0xFFFF;
        if (filter == 0) return (sender.ComponentMask & WArg(mb, 28)) != 0;
        uint id = sender.ControllerType >= 0 && sender.GameObject is { } go ? go.GetID() : 0xFFFFu;
        return (id & 0x7FFF) == filter;
    }

    private void StickToMotionBlock_236120(List<uint> mb, AgentLabAgent sender)
    {
        AttachmentsOf_196E20(); // Amedo
        _attachPush1C = BitConverter.UInt32BitsToSingle(WArg(mb, 29));
        if (AttachInstance_196220(sender, 9, null)) Trace($"stuck {sender.Entity.Name} (push {_attachPush1C})"); // Amedo
        if (WArg(mb, 31) != 0) sender.Deliver((ushort)(WArg(mb, 31) & 0xFFFF), this);
    }

    // Amedo
    private void MakeMotionBlockNormal_23F250()
    {
        AttachmentsOf_196E20();
        ReleaseLinkedInstances_196E88(2);
    }

    private void PushFromHolder_192768(AgentLabAgent child, float f)
    {
        var (hmin, hmax) = CommittedBox;
        var (cmin, cmax) = child.CommittedBox;
        var hc = (hmax - hmin) * 0.5f + hmin;
        var cc = (cmax - cmin) * 0.5f + cmin;
        var d = new Vector4(cc.X - hc.X, cc.Y - hc.Y, cc.Z - hc.Z, 1f);
        float k = GameMath.InvLength_11C030(2.4999998e-09f, d);
        var move = new Vector3(d.X * k * f, d.Y * k * f, d.Z * k * f);
        if (MathF.Abs(move.X) <= 5e-05f && MathF.Abs(move.Y) <= 5e-05f && MathF.Abs(move.Z) <= 5e-05f) return;
        var p = child.GamePosition;
        child.SetLocalPosition(new Vector3(p.X + move.X, p.Y + move.Y, p.Z + move.Z));
    }
}
