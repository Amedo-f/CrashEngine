using System.Numerics;
using CrashEngine.Importer;
using CrashEngine.Runtime.Camera;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    // Amedo
    private sealed class Attachment
    {
        public Matrix4x4? Relative;
        public Vector3 SpringPoint;
        public AgentLabAgent? Child;
        public NavNode? Target;
        public AgentLabAgent? Holder;
        public int Exit = WholeBody;
        public bool ExitNode;
        public int HolderExit = WholeBody;
        public float Stiffness, Damping, Length;
        public ulong Flags;

        public int Mode
        {
            get => (int)(Flags & 0xF);
            set => Flags = (Flags & ~0xFUL) | ((ulong)value & 0xF);
        }

        public int Kind
        {
            get => (int)((Flags >> 4) & 7);
            set => Flags = (Flags & ~0x70UL) | (((ulong)value & 7) << 4);
        }

        public bool Hang => (Flags & HangFlag) != 0;

        // Amedo
        public Matrix4x4? Applied => (Flags & RelativeFlag) != 0 ? Relative : null;
    }

    private const int SpringMode = 4; // Amedo
    private const int KindInstance = 0, KindPosition = 1, KindSpring = 2; // Amedo
    private const ulong HangFlag = 0x2000000; // Amedo
    private const ulong HoldSettled = 0x1000000; // Amedo
    private const ulong RelativeFlag = 0x80; // Amedo

    // Amedo
    private bool Holds(Attachment n) => n.Child is { Destroyed: false } c && c._attachedTo == this;

    private const int WholeBody = 0xFF;
    private const int MaxAttachments = 16;

    private readonly List<Attachment> _attachments = new();
    private bool _pathAllocated; // Amedo
    private bool _pathFreed_20; // Amedo
    private AgentLabAgent? _attachedTo;
    private AgentLabAgent? _ref28; // Amedo

    // Amedo 2026-09-30
    private bool _busy
    {
        get => (AgentFlags & 0x100) != 0;
        set => AgentFlags = value ? AgentFlags | 0x100 : AgentFlags & ~0x100u;
    }

    public AgentLabAgent? AttachedTo => _attachedTo;
    public AgentLabAgent? Parent28 => _ref28 is { Destroyed: false } p ? p : null; // Amedo
    public int AttachmentCount => _attachments.Count;

    // Amedo
    internal bool HasParent_1979E8(AgentLabAgent other)
    {
        for (var p = _attachedTo; p is not null; p = p._attachedTo)
            if (ReferenceEquals(p, other)) return true;
        return false;
    }

    // Amedo
    internal void SetAttachedTo_28(AgentLabAgent? holder) => _ref28 = holder;

    // Amedo
    private Attachment ConstructAttachment_1924F0(AgentLabAgent? child) => new() { Child = child };

    // Amedo
    private static Attachment ConstructPositionAttachment_192628(NavNode target) =>
        new() { Flags = KindPosition << 4 | 1, Target = target };

    // Amedo
    private static void DestroyAttachment_1926A0(Attachment node)
    {
        if (node.Kind != KindInstance || node.Child is not { } child) return;
        child.AgentFlags &= ~0x40u;
        child._attachedTo = null;
        child._ref28 = null;
    }

    // Amedo
    private void AttachToHolder_192768(Attachment node, bool relative, Matrix4x4? matrix, float push)
    {
        if (node.Child is not { } child) return;
        var holder = LocalMatrix;
        if (matrix is { } m) child.GameMatrix = m * holder; // Amedo
        if (push != 0f) PushFromHolder_192768(child, push);
        if (!relative) return;
        node.Relative = child.LocalMatrix * GameMath.RigidInverse_18E808(holder); // Amedo
        node.Flags |= RelativeFlag; // Amedo
    }

    // Amedo
    private void AttachAtExitPoint_192B98(Attachment node, int exit, bool relative, Matrix4x4? matrix)
    {
        node.Exit = exit;
        node.ExitNode = HasExitNode_2952A0(exit);
        if (node.Child is not { } child) return;
        if (matrix is { } m) child.GameMatrix = m * ExitAnchor_293B70(exit); // Amedo
        if (!relative) return;
        var anchor = ExitAnchor_293B70(exit);
        node.Relative = child.LocalMatrix * GameMath.RigidInverse_18E808(anchor); // Amedo
        node.Flags |= RelativeFlag; // Amedo
    }

    // Amedo
    private void SetAttachmentExitPoint_192EA0(Attachment node, int exit)
    {
        node.Exit = exit;
        if (exit != WholeBody) node.ExitNode = HasExitNode_2952A0(exit);
    }

    // Amedo
    private void HangFromHolder_192F10(Attachment node, AgentLabAgent child)
    {
        node.Child = child;
        node.Flags |= HangFlag;
        child._ref28 = this;
        child.AgentFlags |= 0x40;
        child._attachedTo = this;
        AgentFlags |= 0x80;
    }

    // Amedo
    private void HandOnVelocity_1931D0(Attachment node)
    {
        var v = Component5 is { } body ? body.Velocity : _velocity;
        if (node.Child is not { } child) return;
        child._prevVelocity = child._velocity; // Amedo
        child._velocity = v;
    }

    // Amedo
    private void MakeAttachmentMatrix_193388(Attachment node)
    {
        Matrix4x4 m;
        if (node.Kind == KindInstance) m = node.Child?.LocalMatrix ?? Matrix4x4.Identity;
        else if (node.Kind == KindPosition) m = Matrix4x4.CreateTranslation(node.Target?.Position ?? Vector3.Zero);
        else m = Matrix4x4.Identity;
        node.Relative = m * GameMath.RigidInverse_18E808(LocalMatrix); // Amedo
    }

    // Amedo
    private bool UpdateAttachment_193518(Attachment node)
    {
        if ((node.Kind == KindInstance || node.Hang) && node.Child is { } held && (held.AgentFlags & 0x40) == 0) return false;
        var anchor = node.ExitNode ? ExitAnchor_293B70(node.Exit) : LocalMatrix;
        switch (node.Mode)
        {
            case 0:
            {
                if (node.Kind != KindInstance) return true;
                if (node.Child is not { } child) return false;
                var turn = child._wobble?.AttachedTurn ?? Matrix4x4.Identity; // Amedo
                child.GameMatrix = turn * (node.Applied is { } r ? r * anchor : anchor); // Amedo
                HandOnVelocity_1931D0(node);
                return true;
            }
            case 1:
                if (node.Kind == KindInstance)
                {
                    if (node.Child is not { } child) return false;
                    var p = (node.Applied is { } r ? r * anchor : anchor).Translation; // Amedo
                    if (p != child.LocalMatrix.Translation) child.SetLocalPosition(p); // Amedo
                    HandOnVelocity_1931D0(node);
                }
                else if (node.Kind == KindPosition && node.Target is { } target)
                    target.Position = ((node.Relative ?? Matrix4x4.Identity) * anchor).Translation;
                return true;
            case 2:
                if (node.Kind == KindInstance && node.Child is not null) UpdateHanging_194040(node, anchor);
                return true;
            case SpringMode:
                UpdateSpring_194608(node, anchor);
                return true;
            case 5:
                World.Unimplemented("attachment", node.Mode, "hold in place (0x193948)");
                return true;
            case 6:
                HoldOnSpringStep_193B70(node, anchor);
                return true;
            default:
                return true;
        }
    }

    // Amedo
    private static void UpdateHanging_194040(Attachment node, Matrix4x4 anchor)
    {
        var child = node.Child!;
        Vector3 point;
        float length = 0f;
        if (node.Applied is { } rel) // Amedo
        {
            point  = (rel * anchor).Translation;
            length = MathF.Sqrt(rel.M41 * rel.M41 + rel.M42 * rel.M42 + rel.M43 * rel.M43);
        }
        else point = anchor.Translation;

        var forward = Unit(point - child.GamePosition);
        var cm = child.LocalMatrix;
        var childUp = new Vector3(cm.M21, cm.M22, cm.M23);
        var up = Unit(childUp + (new Vector3(anchor.M21, anchor.M22, anchor.M23) - childUp) * 0.1f);
        var right = Vector3.Cross(up, forward);
        var newUp = Vector3.Cross(forward, right);
        child.SetLocalRotation(Quaternion.CreateFromRotationMatrix(new Matrix4x4(right.X, right.Y, right.Z, 0f,
                                                                                 newUp.X, newUp.Y, newUp.Z, 0f,
                                                                                 forward.X, forward.Y, forward.Z, 0f,
                                                                                 0f, 0f, 0f, 1f)));
        child.SetLocalPosition(point - forward * length);
    }

    // Amedo
    private bool AddToPath_194A58(Attachment node)
    {
        switch (node.Kind)
        {
            case KindInstance:
                if (node.Child is { } child && PathIndexOf_194C18(child) != -1) return false;
                _attachments.Add(node);
                if (node.Child is { } stopped) stopped.MotionStop_20E910();
                return true;
            case KindPosition:
                if (_attachments.Any(n => n.Kind == KindPosition)) return false;
                _attachments.Add(node);
                return true;
            case KindSpring:
                _attachments.Add(node);
                return true;
            default:
                return true;
        }
    }

    // Amedo
    private int PathIndexOf_194C18(AgentLabAgent child) => _attachments.FindIndex(n => ReferenceEquals(n.Child, child));

    // Amedo
    private void RemoveFromPath_194CC0(int index, bool update)
    {
        var node = _attachments[index];
        if (update) UpdateAttachment_193518(node);
        DestroyAttachment_1926A0(node);
        _attachments.RemoveAt(index);
    }

    // Amedo
    private void ClearPath_194D80()
    {
        foreach (var node in _attachments) DestroyAttachment_1926A0(node);
        _attachments.Clear();
    }

    // Amedo
    private AgentLabAgent? UnslottedAttachment_194E10() => _attachments.FirstOrDefault(n => n.Exit == WholeBody)?.Child;

    // Amedo
    private AgentLabAgent? SlottedAttachment_194E70(int exit) => _attachments.FirstOrDefault(n => n.Exit == exit)?.Child;

    // Amedo
    private AgentLabAgent? TakeUnslotted_194EC8() => TakeAt(_attachments.FindIndex(n => n.Exit == WholeBody));

    // Amedo
    private AgentLabAgent? TakeSlotted_194F48(int exit) => TakeAt(_attachments.FindIndex(n => n.Exit == exit));

    private AgentLabAgent? TakeAt(int index)
    {
        if (index < 0) return null;
        var child = _attachments[index].Child;
        RemoveFromPath_194CC0(index, true);
        return child;
    }

    // Amedo
    private bool RemoveInstanceFromPath_194FC8(AgentLabAgent child, bool update)
    {
        int index = PathIndexOf_194C18(child);
        if (index == -1) return false;
        if (update) UpdateAttachment_193518(_attachments[index]);
        RemoveFromPath_194CC0(index, update);
        return true;
    }

    // Amedo
    private AgentLabAgent? UpdatePath_195048()
    {
        AgentLabAgent? failed = null;
        foreach (var node in _attachments)
            if (!UpdateAttachment_193518(node)) failed = node.Child;
        return failed;
    }

    // Amedo
    private bool LetGo_1957D8(AgentLabAgent? child)
    {
        if (child is null) return false;
        child._ref28 = null;
        FreeEmptyPath_195848();
        int index = IndexOfLinked_195708(child);
        return index != -1 && UnlinkAt_195748(index, false);
    }

    // Amedo
    private void FreeEmptyPath_195848()
    {
        if (!_pathAllocated || _attachments.Count != 0) return;
        _pathAllocated = false;
        _pathFreed_20 = true;
    }

    // Amedo
    private void HoldAttachmentOnSpring_1930B8(Attachment node, float length, float stiffness)
    {
        node.Damping = 0f;
        node.Stiffness = stiffness;
        node.Length = length;
        MakeAttachmentMatrix_193388(node);
        node.Mode = 6;
        if (node.Child is { } child)
        {
            child.AgentFlags |= 0x40;
            child._attachedTo = this;
            child._ref28 = this;
        }
        _ref28 = node.Child;
        node.Flags &= ~HoldSettled;
        var body = Component5;
        HandOnVelocity_1931D0(node);
        body?.Slow_291650(0.15f, 0.02f);
    }

    // Amedo
    private void HoldOnSpringStep_193B70(Attachment node, Matrix4x4 anchor)
    {
        var child = node.Child!;
        if ((node.Flags & HoldSettled) == 0)
        {
            var (cmin, cmax) = child.CommittedBox;
            var c = new Vector3((cmin.X + cmax.X) * 0.5f, (cmin.Y + cmax.Y) * 0.5f, (cmin.Z + cmax.Z) * 0.5f);
            var (hmin, hmax) = CommittedBox;
            var h = new Vector3((hmin.X + hmax.X) * 0.5f, (hmin.Y + hmax.Y) * 0.5f, (hmin.Z + hmax.Z) * 0.5f);
            var m = LocalMatrix;
            float z = node.Length;
            var target = new Vector3(h.X + (m.M11 * 0f + m.M21 * 0f + m.M31 * z),
                                     h.Y + (m.M12 * 0f + m.M22 * 0f + m.M32 * z),
                                     h.Z + (m.M13 * 0f + m.M23 * 0f + m.M33 * z));
            var d = target - c;
            if (0.1f < d.X * d.X + d.Y * d.Y + d.Z * d.Z)
            {
                d = new Vector3(d.X * 0.5f, d.Y * 0.5f, d.Z * 0.5f);
                bool still = MathF.Abs(d.X) <= 5e-5f && MathF.Abs(d.Y) <= 5e-5f && MathF.Abs(d.Z) <= 5e-5f;
                if (!still) child.SetLocalPosition(child.LocalMatrix.Translation + d);
                if (!still) SetLocalPosition(LocalMatrix.Translation - d);
                MakeAttachmentMatrix_193388(node);
                if (!node.ExitNode) anchor = LocalMatrix; // Amedo
            }
            else node.Flags |= HoldSettled;
        }
        child.GameMatrix = node.Relative is { } r ? r * anchor : anchor;
        HandOnVelocity_1931D0(node);
    }

    // Amedo
    private bool HoldOnSpring_196130(AgentLabAgent held, float length, float stiffness)
    {
        if (Add_195E48(held) is not { } node) return false;
        if (held._attachedTo is { _hasLinkList: true } previous) previous.UnlinkInstance_196520(held, update: true);
        HoldAttachmentOnSpring_1930B8(node, length, stiffness);
        return true;
    }

    // Amedo
    private Attachment? Add_195E48(AgentLabAgent? child)
    {
        if (!LinkInstance_195AC8(child, false)) return null;
        _pathAllocated = true;
        _pathFreed_20 = false;
        if (_attachments.Count >= MaxAttachments) return null;
        var node = ConstructAttachment_1924F0(child);
        AddToPath_194A58(node);
        _pathFreed_20 = false;
        World.LastAttachment_82F8 = node;
        return node;
    }

    // Amedo
    private bool AttachInstance_196220(AgentLabAgent child, uint flags, Matrix4x4? matrix) =>
        AttachNode(child, WholeBody, flags, matrix, link: true);

    // Amedo
    internal bool HangOnExitPoint_195C78(AgentLabAgent child, int exit, uint flags, Matrix4x4? matrix, bool noLink) =>
        AttachNode(child, exit, flags, matrix, link: !noLink);

    private bool AttachNode(AgentLabAgent child, int exit, uint flags, Matrix4x4? matrix, bool link)
    {
        if (link && !LinkInstance_195AC8(child, false)) return false;
        if (!_pathAllocated)
        {
            _pathAllocated = true;
            _pathFreed_20 = false;
        }
        else if ((flags & 2) != 0)
        {
            var replaced = exit == WholeBody ? TakeUnslotted_194EC8() : TakeSlotted_194F48(exit);
            if (replaced is not null && (flags & 4) != 0)
            {
                replaced._attachedTo = null;
                replaced.AgentFlags &= ~0x40u;
                if (replaced.Pickup is { } pickup) pickup.Launch(Vector3.Zero, -1f);
                else replaced.LaunchPhysics_22ECB0(Vector3.Zero, -1f);
            }
        }
        if (_attachments.Count >= MaxAttachments) return false;
        var node = ConstructAttachment_1924F0(child);
        if (exit == WholeBody) AttachToHolder_192768(node, (flags & 1) != 0, matrix, _attachPush1C);
        else AttachAtExitPoint_192B98(node, exit, (flags & 1) != 0, matrix);
        AddToPath_194A58(node);
        _pathFreed_20 = false;
        World.LastAttachment_82F8 = node;
        child._ref28 = this;
        child.AgentFlags |= 0x40;
        child._attachedTo = this;
        if ((flags & 8) != 0 && IndexOfLinked_195708(child) is var at and >= 0) _links[at] = (_links[at].Agent, (byte)(_links[at].Flags | 2));
        Trace($"attached {child.Entity.Name} at {(exit == WholeBody ? "body" : $"exit {exit}")}");
        return true;
    }

    // Amedo
    private bool AttachToAiPosition_196008(NavNode target)
    {
        if (!_pathAllocated)
        {
            _pathAllocated = true;
            _pathFreed_20 = false;
        }
        if (_attachments.Count >= MaxAttachments) return false;
        var node = ConstructPositionAttachment_192628(target);
        if (!AddToPath_194A58(node)) return false;
        _pathFreed_20 = false;
        node.SpringPoint = target.Position;
        target.Flags |= NavNode.Attached_30A71C;
        MakeAttachmentMatrix_193388(node);
        World.LastAttachment_82F8 = node;
        return true;
    }

    // Amedo
    private bool UnlinkInstance_196520(AgentLabAgent child, bool update = false, bool force = false)
    {
        int index = IndexOfLinked_195708(child);
        if (index < 0) return false;
        bool detached = false;
        if (_pathAllocated && RemoveInstanceFromPath_194FC8(child, update))
        {
            child._ref28 = null;
            FreeEmptyPath_195848();
            detached = true;
        }
        UnlinkAt_195748(index, force);
        return detached;
    }

    // Amedo
    internal AgentLabAgent? DetachExitPoint_1965F8(int exit, bool keepLink = false)
    {
        if (!_pathAllocated || TakeSlotted_194F48(exit) is not { } child) return null;
        if (keepLink) child._ref28 = null;
        else LetGo_1957D8(child);
        return child;
    }

    // Amedo
    private AgentLabAgent? DetachUnslotted_196670()
    {
        if (!_pathAllocated) return null;
        var child = TakeUnslotted_194EC8();
        LetGo_1957D8(child);
        return child;
    }

    // Amedo
    private void UnlinkAll_1966F8()
    {
        if (_pathAllocated) ClearPath_194D80();
        _pathAllocated = false;
        _pathFreed_20 = false;
        _links.Clear();
        _linkCursor = 0;
        _attachPush1C = 0f;
    }

    // Amedo
    private void ReleaseAttachmentsNode_196738(bool update, bool removeIfEmpty, bool clearOwner28)
    {
        if (clearOwner28) _ref28 = null;
        if (_pathAllocated)
        {
            for (int i = 0; i < _attachments.Count;)
            {
                var node = _attachments[i];
                switch (node.Kind)
                {
                    case KindInstance:
                        if (node.Child is { } child)
                        {
                            if (!UnlinkInstance_196520(child, update)) i++;
                        }
                        else
                        {
                            RemoveFromPath_194CC0(i, true);
                            UnlinkMissing_196B00();
                        }
                        break;
                    case KindPosition:
                        if (node.Target is { } target)
                        {
                            target.Position = SpringAnchor_193018(node);
                            target.Flags = (ushort)(target.Flags & ~NavNode.Attached_30A71C);
                        }
                        RemoveFromPath_194CC0(i, false);
                        UnlinkMissing_196B00();
                        break;
                    case KindSpring:
                        if (node.Hang && node.Child is { } hung)
                        {
                            if (!UnlinkInstance_196520(hung, update)) i++;
                        }
                        else
                        {
                            UnlinkMissing_196B00();
                            i++;
                        }
                        break;
                    default:
                        RemoveFromPath_194CC0(i, true);
                        UnlinkMissing_196B00();
                        break;
                }
                if (!_pathAllocated) break;
            }
            FreeEmptyPath_195848();
        }
        if (removeIfEmpty && _links.Count == 0) World.RemoveAttachments_197480(this);
    }

    // Amedo
    private bool FollowPath_196D58()
    {
        if (!_pathAllocated) return true;
        if (UpdatePath_195048() is { } failed) UnlinkInstance_196520(failed);
        return !_pathAllocated;
    }

    // Amedo
    internal void AttachmentsUpdate_196DB8()
    {
        if (FollowPath_196D58()) AgentFlags &= ~0x80u;
    }

    // Amedo
    internal void AttachmentsOf_196E20()
    {
        if (_hasLinkList) return;
        _hasLinkList = true;
        World.RegisterAttachments_197188(this);
    }

    // Amedo
    internal void AttachmentsRemoved() => _hasLinkList = false;

    // Amedo
    private bool LostAllAttachments_229E60()
    {
        if (!_hasLinkList || !_pathFreed_20) return false;
        _pathFreed_20 = false;
        return true;
    }

    // Amedo
    internal void AttachmentsStep_1959D8()
    {
        if (!_hasLinkList) return;
        ReleaseAttachmentsNode_196738(false, false, true);
        UnlinkUnkept_196948();
        AgentFlags &= ~0x80u;
        AgentFlags &= ~0x40u;
        _attachedTo = null;
        _attachPush1C = 0f;
        _linkCursor = 0;
        if (NoneKept_195C28()) World.RemoveAttachments_197480(this);
    }

    // Amedo 2026-10-04
    internal Matrix4x4 ExitAnchor_293B70(int exit)
    {
        if (Anim is not { } anim || !HasExitNode_2952A0(exit)) return UnitRows(AnchorMatrix(exit)); // Amedo
        if (anim.KeptExitPoint(exit, out var kept)) return kept; // Amedo
        var m = UnitRows(AnchorMatrix(exit));
        anim.KeepExitPoint(exit, m); // Amedo
        return m;
    }

    // Amedo
    internal Matrix4x4? ExitPoint_293B70(int exit) => HasExitNode_2952A0(exit) ? ExitAnchor_293B70(exit) : null;

    // Amedo 2026-10-04
    private static Matrix4x4 UnitRows(Matrix4x4 m)
    {
        var r0 = Unit(new Vector3(m.M11, m.M12, m.M13));
        var r1 = Unit(new Vector3(m.M21, m.M22, m.M23));
        var r2 = Unit(new Vector3(m.M31, m.M32, m.M33));
        return new Matrix4x4(r0.X, r0.Y, r0.Z, m.M14, r1.X, r1.Y, r1.Z, m.M24, r2.X, r2.Y, r2.Z, m.M34, m.M41, m.M42, m.M43, m.M44);
    }

    // Amedo 2026-10-04
    internal void AttachAtExit_195C78(AgentLabAgent child, int exit)
    {
        AttachmentsOf_196E20();
        HangOnExitPoint_195C78(child, exit, 0, null, noLink: true);
    }

    // Amedo
    private bool HasOgiAnimator_19DD30 => Ogi is { } ogi && !(ogi.Joints.Count == 1 && ogi.ExitPoints.Count == 0);

    // Amedo
    internal bool HasExitNode_2952A0(int exit) => exit >= 0 && HasOgiAnimator_19DD30 && GameObject is { } obj && exit < obj.ExitPointAmount;

    private Matrix4x4 AnchorMatrix(int exit)
    {
        if (exit == WholeBody || !HasExitNode_2952A0(exit)) return LocalMatrix; // Amedo
        var ogi = Ogi; // Amedo 2026-09-30
        if (ogi is null || exit >= ogi.ExitPoints.Count) return LocalMatrix; // Amedo
        var e = ogi.ExitPoints[exit];
        var joints = JointsOf(ogi); // Amedo 2026-09-30
        var joint = joints.TryGetValue((int)e.ParentJointIndex, out var jw) ? jw : Matrix4x4.Identity;
        return MeshDecoder.TwinMatToSys(e.Matrix) * joint * LocalMatrix;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI, Dictionary<int, Matrix4x4>> RestJoints = new(); // Amedo 2026-09-30

    // Amedo 2026-09-30
    private Dictionary<int, Matrix4x4> JointsOf(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI ogi) =>
        Anim?.JointWorlds ?? RestJoints.GetValue(ogi, MeshDecoder.ComputeJointWorlds);

    private void SetObject(uint arg0, uint arg1)
    {
        switch (arg0 & 3)
        {
            case 1: _busy = true; break;
            case 2: _busy = false; break;
        }
        switch ((arg1 >> 2) & 3) // Amedo
        {
            case 1: MotionCarries_18 = true; break;
            case 2: MotionCarries_18 = false; break;
        }
        if (_physics?.Body is { } body)
        {
            body.Flags = (uint)Toggle(body.Flags, (arg0 >> 10) & 3, 0x20);
            body.Flags = (uint)Toggle(body.Flags, arg1 & 3, 0x40);
        }
        AgentFlags = Toggle(AgentFlags, (arg0 >> 2) & 3, 0x400); // Amedo 2026-09-27
        if (((arg0 >> 2) & 3) != 0) ApplyRendered(); // Amedo 2026-09-27
        AgentFlags = Toggle(AgentFlags, (arg0 >> 4) & 3, 0x10);
        switch ((arg0 >> 6) & 3) // Amedo
        {
            case 1: Wake_1974C8(); break;
            case 2:
                ReleaseAttachments();
                Sleep_197508();
                break;
        }
        switch ((arg1 >> 4) & 3) // Amedo
        {
            case 1: SetSolid_1A1200(true); break;
            case 2: SetSolid_1A1200(false); break;
        }
        if ((arg0 & 0x40000000) != 0) SetActivityRadius((arg0 >> 21) & 0xFF);
        if ((arg0 & 0x20000000) != 0) _animExtra = ((arg0 >> 13) & 0xFF) == 0xFF ? (ushort)0xFFFF : (ushort)(((arg0 >> 13) & 0xFF) * ((arg0 >> 13) & 0xFF)); // Amedo 2026-10-01
    }

    private static uint Toggle(uint flags, uint mode, uint bit) => mode switch
    {
        1 => flags | bit,
        2 => flags & ~bit,
        _ => flags,
    };

    // Amedo 2026-10-01
    private void SpinWithParent_24EE10(uint arg0, uint flags)
    {
        if (_attachedTo is not { } parent) return;
        var pm = parent.GameMatrix;
        var axis = (flags & 8) != 0 ? new Vector3(pm.M11, pm.M12, pm.M13)
                 : (flags & 0x10) != 0 ? new Vector3(pm.M21, pm.M22, pm.M23)
                 : (flags & 0x20) != 0 ? new Vector3(pm.M31, pm.M32, pm.M33) : Vector3.Zero;
        float along = GameMath.Dot(Unit(axis), parent._velocity);
        int angle = GameMath.FromValue_11BFA0(along * BitConverter.UInt32BitsToSingle(arg0) * StepDt, 1);
        if (angle == 0) return;
        Quaternion turn;
        if ((flags & 1) != 0) turn = GameMath.RotationX_18D590(angle); // Amedo
        else if ((flags & 2) != 0) turn = GameMath.RotationY_18D620(angle);
        else if ((flags & 4) != 0) turn = GameMath.RotationZ_18D6B0(angle);
        else return;
        SetLocalRotation(GameMath.Mul_18D558(LocalRotation, turn));
    }

    // Amedo
    internal void ReleaseAttachments()
    {
        if (_hasLinkList) ReleaseAttachmentsNode_196738(false, false, false);
        AgentFlags &= ~0x40u;
        AgentFlags &= ~0x80u;
        _attachedTo = null;
    }

    // Amedo
    private void DetachAll_1292D0()
    {
        if (!_hasLinkList) return;
        ReleaseAttachmentsNode_196738(false, false, false);
        AgentFlags &= ~0x80u;
    }

    // Amedo
    private void DropAttachedObject(uint arg0)
    {
        int exit = (int)((arg0 >> 16) & 0xFF);
        int mode = (int)((arg0 >> 24) & 7);
        ushort message = (ushort)(arg0 & 0xFFFF);
        if ((AgentFlags & 0x80) == 0 || mode >= 4 || !_hasLinkList) return; // Amedo
        var child = exit != WholeBody ? DetachExitPoint_1965F8(exit) : DetachUnslotted_196670();
        if (child is null) return;
        if (_links.Count == 0) AgentFlags &= ~0x80u; // Amedo 2026-10-01
        if ((arg0 & 0x20000000) != 0) child._busy = false;
        child.AgentFlags &= ~0x40u; // Amedo
        child._attachedTo = null;
        Trace($"dropped {child.Entity.Name} from {(exit == WholeBody ? "body" : $"exit {exit}")}{(message != 0xFFFF ? $", message {message}" : "")}");
        if (child.Pickup is null && (child.AgentFlags & 0x8000) != 0) child.Touched_22FC98(0.1f, this); // Amedo 2026-10-05
        if (mode == 1)
        {
            if (child.Pickup is { } pickup) pickup.Launch(Vector3.Zero, -1f);
            else child.LaunchPhysics_22ECB0(Vector3.Zero, -1f);
        }
        else if (mode is 2 or 3) World.Unimplemented("command", 48, $"DropAttachedObject physics mode {mode} (A+0x84 vt+0x18/+0x20)");
        if (message != 0xFFFF) child.Deliver(message, this);
    }

    // Amedo 2026-10-06
    private void ThrowAttachedObject_24DD10(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint w = Arg(0);
        if ((AgentFlags & 0x80) == 0 || !_hasLinkList) return;
        int exit = (int)(w & 0xFF);
        var child = exit != WholeBody ? DetachExitPoint_1965F8(exit) : DetachUnslotted_196670(); // Amedo
        if (child is null) return;
        AgentFlags &= ~0x80u;
        if ((w & 0x80000000) != 0) child._busy = false;
        child.AgentFlags &= ~0x40u;
        float g = FloatValueOf(Arg(5));
        var target = SourcePosition((int)((w >> 12) & 0xFF), (int)((w >> 20) & 0xFF), (int)((w >> 8) & 0xF), (w & 0x20000000) != 0 ? new Vector3(F(1), F(2), F(3)) : null);
        if ((w & 0x40000000) != 0) target = World.Rng.JitterVector_23C848(target, F(7), 1f, 1f, 1f); // Amedo
        var p = child.GamePosition;
        float dx = target.X - p.X, dz = target.Z - p.Z, dy = target.Y - p.Y;
        float hd = MathF.Sqrt(dx * dx + dz * dz);
        float speed = FloatValueOf(Arg(6));
        float t = hd / speed;
        float gravity = 0f <= g ? g : 30f;
        float k = speed / hd;
        var v = new Vector3(dx * k, (dy + gravity * (t * 0.5f) * t) / t, dz * k);
        Trace($"throw {child.Entity.Name} to {target} v {v}");
        if (child.Pickup is { } pickup) pickup.Launch(v, g);
        else child.LaunchPhysics_22ECB0(v, g);
    }

    // Amedo
    private void RequestAttachmentFocus_221D68(uint arg0)
    {
        int exit = (int)(arg0 & 0xFF);
        AgentLabAgent? child = null;
        if (_hasLinkList && _pathAllocated) child = exit != WholeBody ? SlottedAttachment_194E70(exit) : UnslottedAttachment_194E10();
        if (child is not null) FocusOn(child);
        else _hasFocusObject = _hasFocusPosition = false;
    }

    // Amedo
    private void DestroySpawnedAttachment_21FD48(uint arg0)
    {
        if (!_hasLinkList) return;
        int exit = (int)(arg0 & 0xFF);
        if (exit == WholeBody && (arg0 & 0x100) != 0)
        {
            DestroySpawnedLinks_196BB8(true);
            return;
        }
        var child = !_pathAllocated ? null : exit != WholeBody ? SlottedAttachment_194E70(exit) : UnslottedAttachment_194E10();
        if (child is not { _spawnedBy: not null }) return;
        if (exit != WholeBody) DetachExitPoint_1965F8(exit);
        else DetachUnslotted_196670();
        Trace($"destroyed spawned attachment {child.Entity.Name}");
        child.DestroySelf();
    }

    // Amedo 2026-10-01
    private void DestroySpawnedLinks_196BB8(bool destroy)
    {
        int i = 0;
        while (i < _links.Count)
        {
            var link = _links[i].Agent;
            if (link._spawnedBy is null)
            {
                i++;
                continue;
            }
            UnlinkIndex_196CB0(i);
            if (!destroy)
            {
                i++;
                continue;
            }
            Trace($"destroyed spawned link {link.Entity.Name}");
            link.DestroySelf();
            if (i < _links.Count && ReferenceEquals(_links[i].Agent, link)) i++;
        }
    }

    // Amedo
    private AgentLabAgent? UnlinkIndex_196CB0(int index)
    {
        var child = _links[index].Agent;
        if (!_pathAllocated) return UnlinkAt_195748(index, false) ? child : null; // Amedo
        RemoveInstanceFromPath_194FC8(child!, true); // Amedo
        LetGo_1957D8(child);
        return child; // Amedo
    }

    // Amedo
    private void DestroyInstanceInBox_24F4B0()
    {
        var (min, max) = TreeBox;
        var hits = World.QueryBox_1ECFC0(min, max, 0x100, 1, 0, true, null, 0x20);
        if (hits.Count > 0) hits[0].DestroySelf();
    }

    // Amedo 2026-10-01
    private void DestroySelf()
    {
        ReleaseRoute_20EFC8(); // Amedo
        ReleaseRigidBody_23DED8(); // Amedo
        _destroyed = true;
        Entity.Active = false;
        ClearTrails();
        if (World.Spawned.Contains(this)) _spawnedBy?.DropLinksTo(this);
        ReleaseAttachments();
        if (!_hasLinkList) return; // Amedo
        UnlinkAll_1966F8();
        World.RemoveAttachments_197480(this);
    }

    // Amedo
    private float AttachedAtExit(int exit) =>
        _hasLinkList && _pathAllocated && (exit == WholeBody ? UnslottedAttachment_194E10() : SlottedAttachment_194E70(exit)) is not null ? 1f : 0f;

    // Amedo
    private void AttachLinkedObject(PS2BehaviourCommand cmd)
    {
        var a = cmd.Arguments;
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint w = Arg(0);

        AgentLabAgent? owner = this, child;
        int sel = (int)((w >> 17) & 0xF);
        if (sel != 0xF) child = LinkAt(sel);
        else
        {
            switch (w & 7)
            {
                case 0:
                    if ((w & 0x800000) != 0) { owner = FocusObject; child = this; } // Amedo 2026-09-29
                    else child = FocusObject;
                    break;
                case 1: child = Alive(ref _slot118); break;
                case 2: child = CachedTemplate(); break;
                default: return;
            }
        }
        if (owner is null || child is null || (child.AgentFlags & 0x40) != 0) return; // Amedo
        child.StopPhysicsToAttach_24C948();

        Matrix4x4? matrix = null;
        if ((w & 0x18000) != 0)
        {
            var m = Matrix4x4.Identity;
            if ((w & 0x10000) != 0) // Amedo 2026-09-27
            {
                const float unit = 9.58738e-5f;
                m = Matrix4x4.CreateRotationX((int)Arg(5) * unit) * Matrix4x4.CreateRotationY((int)Arg(6) * unit)
                  * Matrix4x4.CreateRotationZ((int)Arg(7) * unit);
            }
            m.Translation = new Vector3(F(1), F(2), F(3));
            matrix = m;
        }
        owner.AttachmentsOf_196E20();
        int exit = (int)((w >> 3) & 0xFF);
        bool attached = exit != WholeBody ? owner.HangOnExitPoint_195C78(child, exit, 1, matrix, false) : owner.AttachInstance_196220(child, 1, matrix);
        if (!attached) return;
        owner.AgentFlags |= 0x80; // Amedo 2026-09-30
        child._ref28 = owner;
        owner._ref28 = child;
        if ((w & 0x400000) != 0) child._busy = true;
        child._attachedTo = owner;
        child.AgentFlags |= 0x40;
        uint mode = (w >> 11) & 0xF;
        if (mode == 0) return;
        if (mode == 2 && World.LastAttachment_82F8 is Attachment last) last.Kind = KindInstance;
        SetLastAttachmentMode_196D30((int)mode);
    }

    // Amedo
    private void SetLastAttachmentMode_196D30(int mode)
    {
        if (World.LastAttachment_82F8 is Attachment last) last.Mode = mode;
        World.LastAttachment_82F8 = null;
    }

    // Amedo
    private void StopPhysicsToAttach_24C948()
    {
        if (_physics is { } ctrl)
        {
            ctrl.Body = null;
            if (ctrl.TerrainType >= 9) ctrl.Flags = (ctrl.Flags & ~(0xFUL << 36)) | (1UL << 36);
            if (ctrl.PairType >= 9) ctrl.Flags = (ctrl.Flags & ~(0xFUL << 32)) | (1UL << 32);
            if (_contactResponse is null) AgentFlags &= ~0x8000u;
        }
        ReleaseTrajectory_23DD80();
        ReleaseMotionBlock_23DDD0();
    }
}
