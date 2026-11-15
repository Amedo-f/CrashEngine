using System.Numerics;
using CrashEngine.Core;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using PS2AnyAIPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPath;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

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
    public int Count => _undo.Count; // Amedo 2026-09-20

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

    // Amedo 2026-09-20
    public bool HandlesSelectionItself { get; init; }

    public void Undo() { for (int i = Actions.Count - 1; i >= 0; i--) Actions[i].Undo(); }
    public void Redo() { foreach (var a in Actions) a.Redo(); }
    public IEnumerable<Entity> AffectedEntities =>
        HandlesSelectionItself ? Array.Empty<Entity>() : Actions.SelectMany(a => a.AffectedEntities);
}

// Amedo 2026-09-20
public sealed class SelectionChangeAction : IEditAction
{
    public required IReadOnlyList<Entity> Before;
    public required IReadOnlyList<Entity> After;
    public required Action<IReadOnlyList<Entity>> Apply;

    public void Undo() => Apply(Before);
    public void Redo() => Apply(After);
    public IEnumerable<Entity> AffectedEntities => Array.Empty<Entity>();
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
        var scale = Trigger.Scale; // Amedo
        scale.X = 0f; scale.Y = 0f; scale.Z = 0f;
        Trigger.Scale = scale; // Amedo
        Parent.RemoveChild(Entity);
    }

    public void Undo()
    {
        var scale = Trigger.Scale; // Amedo
        scale.X = _savedX; scale.Y = _savedY; scale.Z = _savedZ;
        Trigger.Scale = scale; // Amedo
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


    // Amedo 2026-09-21
    public void Undo()
    {
        Parent.RemoveChild(Entity);

        var tile  = Entity.Get<CrashEngine.Importer.SceneryTile>();
        var node  = tile?.Node   ?? Node;
        var mat   = tile?.Source ?? Matrix;
        bool isLod = tile?.IsLod ?? IsLod;

        if (isLod)
        {
            int i = node.LodModelMatrices.IndexOf(mat);
            if (i >= 0)
            {
                node.LodModelMatrices.RemoveAt(i);
                node.LodIDs.RemoveAt(i);
                int bi = node.MeshIDs.Count + i;
                if (bi >= 0 && bi < node.BoundingBoxes.Count) node.BoundingBoxes.RemoveAt(bi);
            }
        }
        else
        {
            int i = node.MeshModelMatrices.IndexOf(mat);
            if (i >= 0)
            {
                node.MeshModelMatrices.RemoveAt(i);
                node.MeshIDs.RemoveAt(i);
                if (i < node.BoundingBoxes.Count) node.BoundingBoxes.RemoveAt(i);
            }
        }
    }

    public void Redo()
    {
        Parent.AddChild(Entity);
        if (IsLod) { Node.LodIDs.Add(SourceId); Node.LodModelMatrices.Add(Matrix); }
        else       { Node.MeshIDs.Add(SourceId); Node.MeshModelMatrices.Add(Matrix); }
        int insertAt = IsLod ? Node.BoundingBoxes.Count : Node.MeshIDs.Count - 1;
        Node.BoundingBoxes.Insert(Math.Clamp(insertAt, 0, Node.BoundingBoxes.Count), BoundingBox);

        // Amedo 2026-09-21
        if (Entity.Get<CrashEngine.Importer.SceneryTile>() is { } t) { t.Node = Node; t.Source = Matrix; }
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

public sealed class AiPositionDeleteAction : IEditAction
{
    public required Entity Parent;
    public required Entity Entity;
    public required ITwinItem Position;
    public required IReadOnlyList<BaseTwinSection> Layouts;

    private readonly List<(BaseTwinSection Section, ITwinItem[] Items)> _before = new();
    private int _childIndex = -1;

    public void Redo()
    {
        _before.Clear();
        foreach (var layout in Layouts)
            foreach (int kind in (ReadOnlySpan<int>)[TwinConstants.LAYOUT_AI_POSITIONS_SECTION, TwinConstants.LAYOUT_AI_PATHS_SECTION])
                if (layout.GetItem<BaseTwinSection>((uint)kind) is { } section)
                    _before.Add((section, Enumerable.Range(0, section.GetItemsAmount()).Select(section.GetItem).ToArray()));

        uint apId = Position.GetID();
        foreach (var layout in Layouts)
        {
            layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION)?.RemoveItem(Position);
            var pathSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
            if (pathSec is not null)
                for (int i = pathSec.GetItemsAmount() - 1; i >= 0; i--)
                    if (pathSec.GetItem(i) is PS2AnyAIPath pth
                        && (pth.Args[0] == apId || pth.Args[1] == apId))
                        pathSec.RemoveItem(pth);
        }
        _childIndex = Parent.Children.ToList().IndexOf(Entity);
        Parent.RemoveChild(Entity);
    }

    public void Undo()
    {
        foreach (var (section, items) in _before)
        {
            section.ClearItems();
            foreach (var item in items) section.AddItem(item);
        }
        Parent.InsertChild(Math.Clamp(_childIndex, 0, Parent.Children.Count), Entity);
    }

    public IEnumerable<Entity> AffectedEntities { get { yield return Entity; } }
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


// Amedo 2026-11-15
public sealed class LayoutEditAction : IEditAction
{
    public required List<(BaseTwinSection Section, ITwinItem[] Before, ITwinItem[] After)> Sections;
    public required List<(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance Instance, ushort[] BeforeKeys, ushort[] AfterKeys, uint BeforeFlags, uint AfterFlags)> Instances;
    public required List<(Entity Parent, Entity Entity, bool Added)> Entities;
    public List<(Action Undo, Action Redo)> Changes { get; init; } = new();
    public Entity? Focus;

    public void Undo() => Apply(after: false);
    public void Redo() => Apply(after: true);

    private void Apply(bool after)
    {
        foreach (var (section, before, afterItems) in Sections)
        {
            section.ClearItems();
            foreach (var item in after ? afterItems : before) section.AddItem(item);
        }
        foreach (var (instance, beforeKeys, afterKeys, beforeFlags, afterFlags) in Instances)
        {
            instance.Positions = new List<ushort>(after ? afterKeys : beforeKeys);
            instance.StateFlags = after ? afterFlags : beforeFlags;
        }
        foreach (var (parent, entity, added) in Entities)
        {
            bool present = after == added;
            if (present && entity.Parent is null) parent.AddChild(entity);
            else if (!present && entity.Parent is not null) parent.RemoveChild(entity);
        }
        if (after) foreach (var change in Changes) change.Redo();
        else for (int i = Changes.Count - 1; i >= 0; i--) Changes[i].Undo();
    }

    public IEnumerable<Entity> AffectedEntities { get { if (Focus is not null) yield return Focus; } }
}

// Amedo 2026-11-15
public sealed class LayoutEditRecorder
{
    private readonly List<(BaseTwinSection Section, ITwinItem[] Before)> _sections = new();
    private readonly List<(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance Instance, ushort[] Keys, uint Flags)> _instances = new();
    private readonly List<(Entity Parent, Entity Entity, bool Added)> _entities = new();
    private readonly List<(Action Undo, Action Redo)> _changes = new();

    public LayoutEditRecorder Section(BaseTwinSection? section)
    {
        if (section is not null && _sections.All(s => !ReferenceEquals(s.Section, section)))
            _sections.Add((section, Items(section)));
        return this;
    }

    public LayoutEditRecorder Instance(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance instance)
    {
        if (_instances.All(i => !ReferenceEquals(i.Instance, instance)))
            _instances.Add((instance, instance.Positions.ToArray(), instance.StateFlags));
        return this;
    }

    public void Added(Entity parent, Entity entity) => _entities.Add((parent, entity, true));
    public void Removed(Entity parent, Entity entity) => _entities.Add((parent, entity, false));
    public void Changed(Action undo, Action redo) => _changes.Add((undo, redo));

    public LayoutEditAction Finish(Entity? focus) => new()
    {
        Sections = _sections.Select(s => (s.Section, s.Before, Items(s.Section))).ToList(),
        Instances = _instances.Select(i => (i.Instance, i.Keys, i.Instance.Positions.ToArray(), i.Flags, i.Instance.StateFlags)).ToList(),
        Entities = _entities.ToList(),
        Changes = _changes.ToList(),
        Focus = focus,
    };

    private static ITwinItem[] Items(BaseTwinSection section) =>
        Enumerable.Range(0, section.GetItemsAmount()).Select(section.GetItem).ToArray();
}
