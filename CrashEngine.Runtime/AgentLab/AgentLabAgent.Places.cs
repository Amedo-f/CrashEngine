using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private bool _sleepHid;

    public bool Asleep => (AgentFlags & 1) != 0;
    public bool Global { get; private set; }
    public WorldChunk? ChunkA0 => Global ? null : Chunk;
    public InstancePlaces? Places => World.Places_13C.GetValueOrDefault(Instance);
    public InstancePlace? Information => World.Information_40.GetValueOrDefault(Instance);

    // Amedo
    internal void Sleep_197508()
    {
        if (_spawnedBy is not null)
        {
            DestroySelf();
            return;
        }
        if (ChunkA0 is not null) ChunkSleep_1ED368();
        AgentFlags |= 1;
        World.MarkOrderDirty();
    }

    private void ChunkSleep_1ED368()
    {
        if (Asleep) return;
        LeaveTree();
        if (Entity.Active)
        {
            Entity.Active = false;
            _sleepHid = true;
        }
    }

    // Amedo
    internal void Wake_1974C8()
    {
        if (ChunkA0 is not null) ChunkWake_1F19F8();
        AgentFlags &= ~1u;
        World.MarkOrderDirty();
    }

    private void ChunkWake_1F19F8()
    {
        if (Asleep) World.Reregister_198730(this); // Amedo
        if (InChunk_A0 && Asleep) SortIntoCells_1EB020(); // Amedo
        if (!Asleep || !_sleepHid) return;
        Entity.Active = true;
        _sleepHid = false;
    }

    // Amedo
    internal void LeaveGlobal()
    {
        Global = false;
        if (Asleep) Wake_1974C8();
    }

    // Amedo
    internal void ReleaseParts_23CF38()
    {
        ClearTrails();
        ReleaseTrajectory_23DD80();
        ReleaseRigidBody_23DED8();
        ReleaseAttachments();
        DeleteLookAt_23DAE0();
        ReleasePerception_23DAA8();
        StopSound_23D4D8();
        PacketEnded_23D8B0();
    }

    // Amedo
    internal void Restart_22F5A8()
    {
        _bossAim?.Reset_121880(); // Amedo
        ReleaseParts_23CF38(); // Amedo
        StopRunners_23E498();
        ObjectNodeReset_22F460();
        InitController();
        _initPosition = WorldPosition;
        _initMatrix   = LocalMatrix;
        RestartDefaultBehaviour();
    }

    // Amedo
    private void ObjectNodeReset_22F460()
    {
        ClearRoute_20EAD8();
        PacketEnded_23D8B0();
        _controller = null;
        ReleaseTrajectory_23DD80();
        ClearTrails();
        ReleaseRigidBody_23DED8();
        ReleaseAttachments();
        _slot118 = null;
        ReleasePerception_23DAA8();
        _slot11C = null;
        _hasStoredPosition = false;
        DeleteLookAt_23DAE0();
        ReleaseMotionBlock_23DDD0();
        Message = NoMessage;
        _messageSource = null;
        MessageTime = double.NegativeInfinity;
        AgentFlags &= ~0x100u;
    }

    // Amedo
    internal InstancePlace PlaceOf_195398(WorldChunk chunk, uint flag) =>
        new() { Matrix = GameMatrix, Chunk = chunk.Name, Flags = flag & 1 };

    // Amedo
    private void Take_195278(InstancePlace p, uint flag)
    {
        p.Matrix = GameMatrix;
        if (ChunkA0 is { } c) p.Chunk = c.Name;
        p.Flags = (p.Flags & ~1u) | (flag & 1);
    }

    // Amedo
    internal InstancePlaces MakeInstancePlaces_197E40(WorldChunk chunk)
    {
        if (Places is { } p) return p;
        p = InstancePlaces.Construct_198068(this, chunk);
        World.Places_13C[Instance] = p;
        return p;
    }

    // Amedo
    internal void SetInformation(InstancePlace p)
    {
        ClearComebackPlacement_23CD30(); // Amedo
        World.Information_40[Instance] = p;
        _initMatrix = p.Matrix;
        if (Entity.Parent is { } parent && ChunkA0 is { } c && c.Name == p.Chunk)
            _initPosition = Vector3.Transform(p.Matrix.Translation, parent.Transform.World);
    }

    private (Matrix4x4 OwnMatrix, Vector3 OwnPosition)? _comeback70; // Amedo

    // Amedo
    internal void SetComebackPlacement_23CD80(Matrix4x4 m)
    {
        _comeback70 ??= (_initMatrix, _initPosition);
        _initMatrix = m;
        _initPosition = Entity.Parent is { } parent ? Vector3.Transform(m.Translation, parent.Transform.World) : m.Translation;
        if (Tracing) Trace($"comeback placement at {m.Translation} (0x23CD80)");
    }

    // Amedo
    internal void ClearComebackPlacement_23CD30()
    {
        if (_comeback70 is not { } kept) return;
        _comeback70 = null;
        _initMatrix = kept.OwnMatrix;
        _initPosition = kept.OwnPosition;
    }

    // Amedo
    internal void ChunkMakeGlobal_1F1B40()
    {
        if (!Asleep) Sleep_197508();
        if (_destroyed) return;
        Global = true;
        World.AddGlobal_198EA0(this);
    }

    // Amedo
    private bool PlacePlacesInChunk_1982C0(InstancePlaces places, WorldChunk chunk)
    {
        if (places.KeepLoaded_197E98(World) == 0) return false;
        if (places.FindIn_1982C0(World, chunk) is not { } found) return false;
        var info = found.Copy();
        if (ReferenceEquals(ChunkA0, chunk)) Take_195278(info, 0);
        SetInformation(info);
        if (!ReferenceEquals(ChunkA0, chunk)) ChunkMakeGlobal_1F1B40();
        places.Chunk = info.Chunk;
        Trace($"placed in {chunk.Name} at {info.Matrix.Translation}{(Global ? " (global until the next frame)" : " (already there: its place is where it stands)")}");
        return true;
    }

    // Amedo
    internal bool ReleasePlaces_1981A8(InstancePlaces places)
    {
        if (places.KeepLoaded_197E98(World) == 0) return true;
        var chunk = ChunkA0;
        if (chunk is not null && places.DropInChunk_197FA0(World, chunk) == 0) return true;
        var info = places.Entries[0].Copy();
        SetInformation(info);
        if (chunk is null)
        {
            Sleep_197508();
            Global = true;
            World.AddGlobal_198EA0(this);
            return false;
        }
        ChunkMakeGlobal_1F1B40();
        places.Chunk = info.Chunk;
        return false;
    }

    // Amedo
    private void PlaceCharacterInChunk_122AF8(uint slot)
    {
        var who = slot == 6 ? null : World.PlayerSlot_570((int)slot);
        if (who?.Places is not { } places) return;
        who.PlacePlacesInChunk_1982C0(places, Chunk);
    }

    // Amedo
    private void SetCharacterHomeChunk_122B68()
    {
        int slot = (int)(World.Progression >> 8) & 0xF;
        var who = slot == 6 ? null : World.PlayerSlot_570(slot);
        if (who?.OwnCharacter is not { } c) return;
        c.HomeChunk_90 = who.ChunkA0;
        Trace($"home chunk of {who.Entity.Name} = {who.ChunkA0?.Name ?? "none"}");
    }

    // Amedo
    private float CharacterHasHomeChunk_12C898()
    {
        int slot = (int)(World.Progression >> 8) & 0xF;
        var who = slot == 6 ? null : World.PlayerSlot_570(slot);
        if (who?.OwnCharacter is not { } c) return 0f;
        return ReferenceEquals(c.HomeChunk_90, who.ChunkA0) ? 0f : 1f;
    }
}
