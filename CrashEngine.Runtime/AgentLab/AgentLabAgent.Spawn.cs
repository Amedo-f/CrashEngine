using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private AgentLabAgent? _spawnedBy;
    private bool _keepCached;
    private int _spawnCount;

    public AgentLabAgent? SpawnedBy => _spawnedBy;

    internal int InstanceId_154 { get; set; } = -1; // Amedo

    private (BaseTwinSection, uint)? _stateSlot16; // Amedo

    // Amedo
    internal (BaseTwinSection, uint) StateSlot_16() =>
        _stateSlot16 ?? (Instance.Section, (Instance.Source.StateFlags & 0x40) != 0 ? Instance.Source.GetID() : 0xFFFFu);

    public static bool SelfRespawnInitialRotation { get; set; } = true; // Amedo 2026-09-29

    // Amedo 2026-09-28
    internal void ReleaseSpawnSlot()
    {
        if (_spawnedBy is { _spawnCount: > 0 } parent) parent._spawnCount--;
    }

    private AgentLabAgent? CachedTemplate()
    {
        if (_slot11C is { Destroyed: true } && !_keepCached) _slot11C = null;
        return _slot11C;
    }

    private void CacheLinkedInstance()
    {
        if (_slot11C is null)
        {
            _slot11C    = TakeFirstLink();
            _keepCached = true;
        }
        if (CachedTemplate() is not { } template) return;
        template.Entity.Active = false;
        template._outOfScene   = true;
        template.LeaveTree(); // Amedo
        template.InChunk_A0 = false;
        Trace($"cached template {template.Entity.Name}");
    }

    private void SpawnResidentAgent(PS2BehaviourCommand cmd)
    {
        var a = cmd.Arguments;
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        ulong x = Arg(7) | ((ulong)Arg(8) << 32);
        bool Bit(int b) => (x & (1UL << b)) != 0;

        if (_spawnCount >= AgentLabWorld.MaxSpawnsPerAgent)
        {
            World.Unimplemented("spawn", AgentLabWorld.MaxSpawnsPerAgent, "per-spawner cap reached");
            return;
        }

        AgentLabAgent? source = null;
        ushort objectId;
        if (Bit(36))
        {
            source   = this;
            objectId = (ushort)(Instance.ObjectId & 0x7FFF);
        }
        else if (Bit(40))
        {
            if (CachedTemplate() is not { } template) return;
            source   = template;
            objectId = (ushort)(template.Instance.ObjectId & 0x7FFF);
        }
        else objectId = (ushort)(Arg(6) & 0x7FFF);

        var current = LocalMatrix;
        current.Translation = Vector3.Zero;
        var rotation = current;
        if (Bit(35))
        {
            int mode = (int)((x >> 24) & 0x1F);
            if ((x & 0x1F000000) == 0x2000000) rotation = current;
            else if (mode == 0) rotation = Matrix4x4.Identity;
            else if (mode == 1) { rotation = _initMatrix; rotation.Translation = Vector3.Zero; }
            else
            {
                World.Unimplemented("spawn", mode, "rotation source space");
                rotation = Matrix4x4.Identity;
            }
        }
        int? subtype = Bit(47) ? RootController().ValueOf(Arg(5)) & 0xFF : null; // Amedo

        var offset = new Vector3(F(1), F(2), F(3));
        int joint = (int)((Arg(7) >> 16) & 0xFF);
        Vector3 position;
        if (Bit(31))
        {
            position = GamePosition;
            source   = null;
        }
        else if (joint != 0xFF)
        {
            var exit = HasExitNode_2952A0(joint) ? ExitAnchor_293B70(joint) : AnchorMatrix(joint); // Amedo
            rotation = exit;
            rotation.Translation = Vector3.Zero;
            position = exit.Translation + ((x & 0x40000000) != 0 ? Vector3.TransformNormal(offset, exit) : Vector3.Zero);
        }
        else
        {
            int key = Bit(34) ? RefFocusObject : Bit(33) ? RefFocusPosition : Bit(48) ? RefStored : (int)((Arg(6) >> 16) & 0xFF);
            int space = (int)((x >> 24) & 0x1F);
            position = SourcePosition(SourceNone, key, space, (x & 0x40000000) != 0 ? offset : null);
            if (SelfRespawnInitialRotation && Bit(36) && !Bit(35) && key == 0xFF && space == 1) // Amedo 2026-09-29
            {
                rotation = _initMatrix;
                rotation.Translation = Vector3.Zero;
            }
        }

        var spawned = Spawn(objectId, source, position, rotation, subtype);
        if (spawned is null)
        {
            if (Bit(32)) AddLink(null); // Amedo
            return;
        }
        _spawnCount++;
        if (Bit(39)) spawned._stateSlot16 = StateSlot_16(); // Amedo

        if (Bit(31)) // Amedo
        {
            AttachmentsOf_196E20();
            if (joint != WholeBody) HangOnExitPoint_195C78(spawned, joint, 0, null, false);
            else AttachInstance_196220(spawned, 0, null);
        }
        if (Bit(32)) AddLink(spawned);
        if (Bit(41)) spawned.AddLink(this);
        ushort message = (ushort)(Arg(7) & 0xFFFF);
        if (message != 0xFFFF) spawned.Deliver(message, this); // Amedo 2026-10-05

        if (Bit(37))
        {
            _focusObject      = spawned;
            _focusCamera      = false; // Amedo
            _hasFocusObject   = true;
            _hasFocusPosition = false;
        }
        if (Bit(38)) _slot11C = spawned;
        if (Bit(42)) spawned._slot118 = Alive(ref _slot118);
        if (Bit(43)) spawned._slot11C = CachedTemplate();
        if (Bit(44))
        {
            spawned._focusObject      = _focusObject;
            spawned._focusCamera      = _focusCamera; // Amedo
            spawned._focusPosition    = _focusPosition;
            spawned._hasFocusObject   = _hasFocusObject;
            spawned._hasFocusPosition = _hasFocusPosition;
        }
        if (Bit(45) && _hasStoredPosition) spawned.Store(_storedPosition);
        if (Bit(46)) LinkAllOf_195B90(spawned); // Amedo
        if (Bit(49)) spawned._busy = true;
        if (Bit(50)) spawned._busy = false;
        uint radius = Arg(6) >> 24;
        if (radius != 0) spawned.SetActivityRadius(radius);
        if (Bit(51)) spawned.InheritKeys(this);
        if (Bit(52)) spawned.InheritPaths(this); // Amedo 2026-09-28
        Trace($"spawned {spawned.Entity.Name} at {position}{(source is null ? "" : $" (copy of {source.Entity.Name})")}");
    }

    private AgentLabAgent? Spawn(ushort objectId, AgentLabAgent? source, Vector3 position, Matrix4x4 rotation, int? subtype)
    {
        var inst = new PS2AnyInstance
        {
            Position              = new TwinVec4(position.X, position.Y, position.Z, 1f),
            ObjectId              = objectId,
            OnSpawnHeaderScriptID = 0xFFFF,
        };
        if (source is not null)
        {
            var s = source.Instance.Source;
            inst.OnSpawnHeaderScriptID = s.OnSpawnHeaderScriptID;
            inst.StateFlags            = s.StateFlags;
            inst.ParamList1            = new List<uint>(s.ParamList1);
            inst.ParamList2            = new List<float>(s.ParamList2);
            inst.ParamList3            = new List<uint>(s.ParamList3);
        }
        else if (World.GameObject(objectId) is { } obj)
        {
            inst.StateFlags = (uint)obj.InstanceStateFlags;
            inst.ParamList1 = new List<uint>(obj.InstFlags);
            inst.ParamList2 = new List<float>(obj.InstFloats);
            inst.ParamList3 = new List<uint>(obj.InstIntegers);
        }
        else
        {
            World.Unimplemented("spawn", objectId, $"object 0x{objectId:X4} not resident");
            return null;
        }
        if (subtype is { } st)
        {
            if (inst.ParamList3.Count == 0) inst.ParamList3.Add(0);
            inst.ParamList3[0] = (uint)st;
        }

        var local = rotation;
        local.Translation = position;
        var spawned = World.CreateSpawned(this, inst, local);
        if (spawned is null) return null;
        spawned._spawnedBy = this;
        spawned.Begin();
        return spawned;
    }

    private void InheritKeys(AgentLabAgent from)
    {
        _keys.Clear();
        _keys.AddRange(from._keys);
        _keyFirst   = from._keyFirst;
        _keyLast    = from._keyLast;
        _keyCurrent = from._keyCurrent;
        _endReached = from._endReached;
    }

    private float PlayerNearerAnotherKey()
    {
        if (World.Player is not { } player || Key(_keyCurrent) is not { } current) return 0f;
        var p = player.GamePosition;
        float best = Vector3.DistanceSquared(p, current);
        for (int i = 0; i < _keys.Count; i++)
            if (i != _keyCurrent && Vector3.DistanceSquared(p, _keys[i]) < best) return 1f;
        return 0f;
    }
}
