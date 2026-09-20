using System.Numerics;
using CrashEngine.Core;

namespace CrashLauncher;

public interface IEditAction
{
    void Undo();
    void Redo();

    IEnumerable<Entity> AffectedEntities { get; }
}

public sealed class EditorUndoStack
{
    private const int MaxDepth = 200;
    private readonly List<IEditAction> _undo = new();
    private readonly List<IEditAction> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Clear() { _undo.Clear(); _redo.Clear(); }

    public void Push(IEditAction action)
    {
        _undo.Add(action);
        if (_undo.Count > MaxDepth) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public IEditAction? Undo()
    {
        if (_undo.Count == 0) return null;
        var a = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        a.Undo();
        _redo.Add(a);
        return a;
    }

    public IEditAction? Redo()
    {
        if (_redo.Count == 0) return null;
        var a = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        a.Redo();
        _undo.Add(a);
        return a;
    }
}

public readonly struct TransformSnapshot
{
    public readonly Vector3    Position;
    public readonly Quaternion Rotation;
    public readonly Vector3    Scale;
    public readonly Matrix4x4? LocalMatrix;

    public TransformSnapshot(Transform t)
    {
        Position    = t.Position;
        Rotation    = t.Rotation;
        Scale       = t.Scale;
        LocalMatrix = t.LocalMatrix;
    }

    public void ApplyTo(Transform t)
    {
        t.Position    = Position;
        t.Rotation    = Rotation;
        t.Scale       = Scale;
        t.LocalMatrix = LocalMatrix;
    }

    public bool Equals(in TransformSnapshot other) =>
        Position == other.Position && Rotation == other.Rotation &&
        Scale == other.Scale && LocalMatrix == other.LocalMatrix;
}

public sealed class TransformEditAction : IEditAction
{
    public required Entity            Entity;
    public required TransformSnapshot Before;
    public required TransformSnapshot After;

    public void Undo() => Before.ApplyTo(Entity.Transform);
    public void Redo() => After.ApplyTo(Entity.Transform);
    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
}

public sealed class CompositeEditAction : IEditAction
{
    public required IReadOnlyList<IEditAction> Actions;

    public void Undo() { for (int i = Actions.Count - 1; i >= 0; i--) Actions[i].Undo(); }
    public void Redo() { foreach (var a in Actions) a.Redo(); }
    public IEnumerable<Entity> AffectedEntities => Actions.SelectMany(a => a.AffectedEntities);
}

public sealed class InverseEditAction : IEditAction
{
    public required IEditAction Inner;
    public void Undo() => Inner.Redo();
    public void Redo() => Inner.Undo();
    public IEnumerable<Entity> AffectedEntities => Inner.AffectedEntities;
}

public sealed class InstanceAddAction : IEditAction
{
    public required Entity Parent;
    public required Entity Entity;
    public required Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection Section;
    public required Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem Item;

    public void Undo()
    {
        Parent.RemoveChild(Entity);
        Section.RemoveItem<Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem>(Item.GetID());
    }

    public void Redo()
    {
        Parent.AddChild(Entity);
        Section.AddItem(Item);
    }

    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
}

public sealed class TriggerVolumeNeuterAction : IEditAction
{
    public required Entity Entity;
    public required Entity Parent;
    public required Twinsanity.TwinsanityInterchange.Common.TwinTrigger Trigger;

    private float _savedX, _savedY, _savedZ;
    private bool  _saved;

    public void Redo()
    {
        if (!_saved) { _savedX = Trigger.Scale.X; _savedY = Trigger.Scale.Y; _savedZ = Trigger.Scale.Z; _saved = true; }
        Trigger.Scale.X = 0f; Trigger.Scale.Y = 0f; Trigger.Scale.Z = 0f;
        Parent.RemoveChild(Entity);
    }

    public void Undo()
    {
        Trigger.Scale.X = _savedX; Trigger.Scale.Y = _savedY; Trigger.Scale.Z = _savedZ;
        Parent.AddChild(Entity);
    }

    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
}

public sealed class ParticleEmitterAddAction : IEditAction
{
    public required Entity Parent;
    public required Entity Entity;
    public required List<Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter> List;
    public required Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter Item;

    public void Undo()
    {
        Parent.RemoveChild(Entity);
        List.Remove(Item);
    }

    public void Redo()
    {
        Parent.AddChild(Entity);
        if (!List.Contains(Item)) List.Add(Item);
    }

    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
}

public sealed class InstanceCompactDeleteAction : IEditAction
{
    public required Entity Entity;
    public required Entity Parent;
    public required Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection Section;
    public required Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance Item;

    private int  _removedAtIndex = -1;
    private uint _removedId;
    private readonly List<(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance Item, uint OldId)> _renumbered = new();
    private readonly List<(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance Owner, List<UInt16> OldList)> _refSnapshots = new();

    public void Redo()
    {
        _renumbered.Clear();
        _refSnapshots.Clear();

        int count = Section.GetItemsAmount();
        _removedAtIndex = -1;
        for (int i = 0; i < count; i++)
            if (ReferenceEquals(Section.GetItem(i), Item)) { _removedAtIndex = i; break; }
        _removedId = Item.GetID();
        if (_removedAtIndex < 0) return;

        for (int i = 0; i < count; i++)
        {
            if (Section.GetItem(i) is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance owner) continue;
            if (owner.Instances.Count == 0) continue;
            bool touched = owner.Instances.Any(v => v == _removedId || v > _removedId);
            if (!touched) continue;
            _refSnapshots.Add((owner, new List<UInt16>(owner.Instances)));
            var newList = owner.Instances
                .Where(v => v != _removedId)
                .Select(v => v > _removedId ? (UInt16)(v - 1) : v)
                .ToList();
            owner.Instances.Clear();
            owner.Instances.AddRange(newList);
        }

        Section.RemoveItem<Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem>(_removedId);

        int newCount = Section.GetItemsAmount();
        for (int i = _removedAtIndex; i < newCount; i++)
        {
            if (Section.GetItem(i) is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance it) continue;
            _renumbered.Add((it, it.GetID()));
            it.SetID(it.GetID() - 1);
        }

        Parent.RemoveChild(Entity);
    }

    public void Undo()
    {
        foreach (var (it, oldId) in _renumbered) it.SetID(oldId);

        Section.AddItem(Item);
        Section.ChangeItemPosition(_removedId, _removedAtIndex);

        foreach (var (owner, oldList) in _refSnapshots)
        {
            owner.Instances.Clear();
            owner.Instances.AddRange(oldList);
        }

        Parent.AddChild(Entity);
    }

    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
}

public sealed class InstanceNeuterAction : IEditAction
{
    public required Entity Entity;
    public required Entity Parent;
    public required Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance Item;

    public ushort? PlaceholderObjectId;

    private bool _snapshotted;
    private ushort _objectId;
    private short  _refListIndex;
    private ushort _onSpawnHeaderScriptID;
    private uint   _stateFlags;
    private List<uint>   _paramList1 = new();
    private List<float>  _paramList2 = new();
    private List<uint>   _paramList3 = new();
    private List<ushort> _instances  = new();
    private List<ushort> _positions  = new();
    private List<ushort> _paths      = new();

    public void Redo()
    {
        if (!_snapshotted)
        {
            _objectId               = Item.ObjectId;
            _refListIndex          = Item.RefListIndex;
            _onSpawnHeaderScriptID = Item.OnSpawnHeaderScriptID;
            _stateFlags            = Item.StateFlags;
            _paramList1 = new List<uint>(Item.ParamList1);
            _paramList2 = new List<float>(Item.ParamList2);
            _paramList3 = new List<uint>(Item.ParamList3);
            _instances  = new List<ushort>(Item.Instances);
            _positions  = new List<ushort>(Item.Positions);
            _paths      = new List<ushort>(Item.Paths);
            _snapshotted = true;
        }

        if (PlaceholderObjectId is { } placeholder) Item.ObjectId = placeholder;
        Item.RefListIndex          = 0;
        Item.OnSpawnHeaderScriptID = 0xFFFF;
        Item.StateFlags            = 0;
        Item.ParamList1.Clear();
        Item.ParamList2.Clear();
        Item.ParamList3.Clear();
        Item.Instances.Clear();
        Item.Positions.Clear();
        Item.Paths.Clear();

        Parent.RemoveChild(Entity);
    }

    public void Undo()
    {
        Item.ObjectId               = _objectId;
        Item.RefListIndex          = _refListIndex;
        Item.OnSpawnHeaderScriptID = _onSpawnHeaderScriptID;
        Item.StateFlags            = _stateFlags;
        Item.ParamList1.Clear(); Item.ParamList1.AddRange(_paramList1);
        Item.ParamList2.Clear(); Item.ParamList2.AddRange(_paramList2);
        Item.ParamList3.Clear(); Item.ParamList3.AddRange(_paramList3);
        Item.Instances.Clear();  Item.Instances.AddRange(_instances);
        Item.Positions.Clear();  Item.Positions.AddRange(_positions);
        Item.Paths.Clear();      Item.Paths.AddRange(_paths);

        Parent.AddChild(Entity);
    }

    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
}

public sealed class SceneryAddAction : IEditAction
{
    public required Entity Parent;
    public required Entity Entity;
    public required Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryBaseType Node;
    public required bool IsLod;
    public required uint SourceId;
    public required Twinsanity.TwinsanityInterchange.Common.Matrix4 Matrix;
    public required Twinsanity.TwinsanityInterchange.Common.Vector4[] BoundingBox;

    public void Undo()
    {
        Parent.RemoveChild(Entity);
        if (IsLod)
        {
            int i = Node.LodModelMatrices.IndexOf(Matrix);
            if (i >= 0) { Node.LodModelMatrices.RemoveAt(i); Node.LodIDs.RemoveAt(i); }
        }
        else
        {
            int i = Node.MeshModelMatrices.IndexOf(Matrix);
            if (i >= 0) { Node.MeshModelMatrices.RemoveAt(i); Node.MeshIDs.RemoveAt(i); }
        }
        int bi = Node.BoundingBoxes.IndexOf(BoundingBox);
        if (bi >= 0) Node.BoundingBoxes.RemoveAt(bi);
    }

    public void Redo()
    {
        Parent.AddChild(Entity);
        if (IsLod) { Node.LodIDs.Add(SourceId); Node.LodModelMatrices.Add(Matrix); }
        else       { Node.MeshIDs.Add(SourceId); Node.MeshModelMatrices.Add(Matrix); }
        int insertAt = IsLod ? Node.BoundingBoxes.Count : Node.MeshIDs.Count - 1;
        Node.BoundingBoxes.Insert(Math.Clamp(insertAt, 0, Node.BoundingBoxes.Count), BoundingBox);
    }

    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
}

public sealed class LoadWallAddAction : IEditAction
{
    public required Entity Parent;
    public required Entity Entity;
    public required Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink LinkItem;
    public required Twinsanity.TwinsanityInterchange.Common.TwinChunkLink Link;

    public void Undo()
    {
        Parent.RemoveChild(Entity);
        LinkItem.LinksList.Remove(Link);
    }

    public void Redo()
    {
        Parent.AddChild(Entity);
        LinkItem.LinksList.Add(Link);
    }

    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
}

public sealed class LoadLinkPairDeleteAction : IEditAction
{
    public required List<(Entity Parent, Entity Entity)> Entities;
    public required Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink LinkItem;
    public required Twinsanity.TwinsanityInterchange.Common.TwinChunkLink Link;

    public void Redo()
    {
        foreach (var (parent, entity) in Entities) parent.RemoveChild(entity);
        LinkItem.LinksList.Remove(Link);
    }

    public void Undo()
    {
        LinkItem.LinksList.Add(Link);
        foreach (var (parent, entity) in Entities) parent.AddChild(entity);
    }

    public IEnumerable<Entity> AffectedEntities => Entities.Select(x => x.Entity);
}

public sealed class LoadZoneBoxAddAction : IEditAction
{
    public required Entity Parent;
    public required Entity Entity;
    public required Twinsanity.TwinsanityInterchange.Common.TwinChunkLink Link;
    public required Twinsanity.TwinsanityInterchange.Common.TwinChunkLinkBoundingBoxBuilder Box;

    public void Undo()
    {
        Parent.RemoveChild(Entity);
        Link.ChunkLinksCollisionData.Remove(Box);
    }

    public void Redo()
    {
        Parent.AddChild(Entity);
        Link.ChunkLinksCollisionData.Add(Box);
    }

    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
}

public sealed class ClearCollisionAction : IEditAction
{
    public required Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData Coll;

    private List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle>? _triangles;
    private List<Twinsanity.TwinsanityInterchange.Common.Vector4>?                          _vectors;
    private List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTrigger>?   _triggers;
    private List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation>?   _groups;

    public void Undo()
    {
        if (_triangles is null) return;
        Coll.Triangles = _triangles;
        Coll.Vectors   = _vectors!;
        Coll.Triggers  = _triggers!;
        Coll.Groups    = _groups!;
    }

    public void Redo()
    {
        _triangles = Coll.Triangles;
        _vectors   = Coll.Vectors;
        _triggers  = Coll.Triggers;
        _groups    = Coll.Groups;
        Coll.Triangles = new();
        Coll.Vectors   = new();
        Coll.Triggers  = new();
        Coll.Groups    = new();
    }

    public IEnumerable<Entity> AffectedEntities => Array.Empty<Entity>();
}

public sealed class GenerateMeshCollisionAction : IEditAction
{
    public required Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData Coll;
    public required List<(int Index, Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle Original)> Degenerated;
    public required List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle> NewTriangles;
    public required List<Twinsanity.TwinsanityInterchange.Common.Vector4>                          NewVectors;
    public required List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation>   NewGroups;

    private int _triInsertAt = -1, _vecInsertAt = -1;

    private void RebuildTriggers()
    {
        var rebuilt = GameScene.BuildTriggerTree(Coll.Groups, Coll.Triangles, Coll.Vectors);
        Coll.Triggers.Clear();
        Coll.Triggers.AddRange(rebuilt);
    }

    public void Undo()
    {
        if (_triInsertAt < 0) return;
        if (NewGroups.Count > 0) Coll.Groups.RemoveRange(Coll.Groups.Count - NewGroups.Count, NewGroups.Count);
        if (NewTriangles.Count > 0) Coll.Triangles.RemoveRange(_triInsertAt, NewTriangles.Count);
        if (NewVectors.Count > 0)   Coll.Vectors.RemoveRange(_vecInsertAt, NewVectors.Count);
        foreach (var (index, original) in Degenerated) Coll.Triangles[index] = original;
        RebuildTriggers();
    }

    public void Redo()
    {
        foreach (var (index, original) in Degenerated)
            Coll.Triangles[index] = new Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle
            {
                Vector1Index = original.Vector1Index, Vector2Index = original.Vector1Index,
                Vector3Index = original.Vector1Index, SurfaceIndex = original.SurfaceIndex,
            };

        _triInsertAt = Coll.Triangles.Count;
        _vecInsertAt = Coll.Vectors.Count;
        Coll.Triangles.AddRange(NewTriangles);
        Coll.Vectors.AddRange(NewVectors);
        Coll.Groups.AddRange(NewGroups);
        RebuildTriggers();
    }

    public IEnumerable<Entity> AffectedEntities => Array.Empty<Entity>();
}

public sealed class AddCollisionTrianglesAction : IEditAction
{
    public required Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData Coll;
    public required List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle> NewTriangles;
    public required List<Twinsanity.TwinsanityInterchange.Common.Vector4> NewVectors;
    public required int TriInsertAt;
    public required int VecInsertAt;

    private void RebuildTriggers()
    {
        var rebuilt = GameScene.BuildTriggerTree(Coll.Groups, Coll.Triangles, Coll.Vectors);
        Coll.Triggers.Clear();
        Coll.Triggers.AddRange(rebuilt);
    }

    public void Undo()
    {
        if (NewTriangles.Count > 0) Coll.Groups.RemoveAt(Coll.Groups.Count - 1);
        Coll.Triangles.RemoveRange(TriInsertAt, NewTriangles.Count);
        Coll.Vectors.RemoveRange(VecInsertAt, NewVectors.Count);
        if (NewTriangles.Count > 0) RebuildTriggers();
    }

    public void Redo()
    {
        Coll.Triangles.InsertRange(TriInsertAt, NewTriangles);
        Coll.Vectors.InsertRange(VecInsertAt, NewVectors);
        if (NewTriangles.Count > 0)
        {
            Coll.Groups.Add(new Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation
            { Offset = (uint)TriInsertAt, Size = (uint)NewTriangles.Count });
            RebuildTriggers();
        }
    }

    public IEnumerable<Entity> AffectedEntities => Array.Empty<Entity>();
}

