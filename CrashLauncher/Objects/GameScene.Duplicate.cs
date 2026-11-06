using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Stealth;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using TwinSceneryLeaf = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryLeaf;
using TwinSceneryBaseType = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryBaseType;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void BeginDuplicatePick()
    {
        if (_selectedSet.Count == 0) return;
        _dupGizmoPivot = GizmoPivot();
        _choosingDuplicateDirection = true;
    }

    private void DuplicateSelected(Vector3 offsetDir, int repeatCount = 1,
        Vector3? arrayStepOffset = null, Vector3 arrayStepRotationDeg = default, float arrayStepGap = 0f,
        bool offsetInParentSpace = false) // Amedo 2026-11-04
    {
        if (_selectedSet.Count == 0 || repeatCount < 1) return;

        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource is null) return;

        var actions      = new List<IEditAction>();
        var newSelection = new List<Entity>();

        foreach (var original in _selectedSet.ToList())
        {
            var parent = original.Parent;
            if (parent is null) continue;

        for (int step = 1; step <= repeatCount; step++)
        {
            var rotDeltaDeg = arrayStepRotationDeg * step;
            Vector3 offset;
            if (arrayStepOffset is { } stepOff)
            {
                offset = stepOff * step;
            }
            else
            {
                offset = offsetDir * (1.5f + arrayStepGap) * step;
                if (TryGetWorldAABB(original, out var dupAabbMin, out var dupAabbMax))
                {
                    float extent = MathF.Abs(offsetDir.X) > 0.5f ? (dupAabbMax.X - dupAabbMin.X)
                                  : MathF.Abs(offsetDir.Y) > 0.5f ? (dupAabbMax.Y - dupAabbMin.Y)
                                  : (dupAabbMax.Z - dupAabbMin.Z);
                    offset = offsetDir * (MathF.Max(extent, 0.01f) + arrayStepGap) * step;
                }
            }

            // Amedo 2026-09-21
            if (!Matrix4x4.Invert(parent.Transform.World, out var invParentWorld)) invParentWorld = Matrix4x4.Identity;
            var localOffset = offsetInParentSpace ? offset : Vector3.TransformNormal(offset, invParentWorld); // Amedo 2026-11-04

            if (original.Get<InstanceData>() is { } data && chunkSource.MeshTables is not null)
            {
                ChunkExporter.SyncInstance(original, data);
                var orig  = data.Source;
                var clone = new PS2AnyInstance
                {
                    Position              = new TwinVec4(orig.Position.X, orig.Position.Y, orig.Position.Z, orig.Position.W),
                    RotationX             = new TwinIntegerRotation { Angle = orig.RotationX.Angle, Fract = orig.RotationX.Fract },
                    RotationY             = new TwinIntegerRotation { Angle = orig.RotationY.Angle, Fract = orig.RotationY.Fract },
                    RotationZ             = new TwinIntegerRotation { Angle = orig.RotationZ.Angle, Fract = orig.RotationZ.Fract },
                    ObjectId              = orig.ObjectId,
                    RefListIndex          = orig.RefListIndex,
                    OnSpawnHeaderScriptID = orig.OnSpawnHeaderScriptID,
                    StateFlags            = orig.StateFlags,
                    InstancesRelated      = orig.InstancesRelated,
                    Instances             = new List<ushort>(orig.Instances),
                    PositionsRelated      = orig.PositionsRelated,
                    Positions             = new List<ushort>(orig.Positions),
                    PathsRelated          = orig.PathsRelated,
                    Paths                 = new List<ushort>(orig.Paths),
                    ParamList1            = new List<uint>(orig.ParamList1),
                    ParamList2            = new List<float>(orig.ParamList2),
                    ParamList3            = new List<uint>(orig.ParamList3),
                };
                if (rotDeltaDeg != Vector3.Zero)
                {
                    clone.RotationX = new TwinIntegerRotation();
                    clone.RotationX.SetRotation(orig.RotationX.GetRotation() + rotDeltaDeg.X);
                    clone.RotationY = new TwinIntegerRotation();
                    clone.RotationY.SetRotation(orig.RotationY.GetRotation() + rotDeltaDeg.Y);
                    clone.RotationZ = new TwinIntegerRotation();
                    clone.RotationZ.SetRotation(orig.RotationZ.GetRotation() + rotDeltaDeg.Z);
                }
                clone.SetID(GenerateUniqueInstanceId(data.Section));
                var clonePos = clone.Position; // Amedo
                clonePos.X += localOffset.X;
                clonePos.Y += localOffset.Y;
                clonePos.Z += localOffset.Z;
                clone.Position = clonePos; // Amedo

                data.Section.AddItem(clone);

                var newEntity = ChunkImporter.ImportInstance(clone, parent, new Dictionary<uint, PatrolPath>(), data.Section, isUserAdded: true); MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, chunkSource.MeshTables, newEntity);

                actions.Add(new InstanceAddAction { Parent = parent, Entity = newEntity, Section = data.Section, Item = clone });
                newSelection.Add(newEntity);
            }
            else if (original.Get<SceneryTile>() is { } tile && chunkSource.SceneryTables is not null)
            {
                if (original.Transform.LocalMatrix is { } liveLm2)
                {
                    tile.Source.Column1 = new TwinVec4(liveLm2.M11, liveLm2.M12, liveLm2.M13, liveLm2.M14);
                    tile.Source.Column2 = new TwinVec4(liveLm2.M21, liveLm2.M22, liveLm2.M23, liveLm2.M24);
                    tile.Source.Column3 = new TwinVec4(liveLm2.M31, liveLm2.M32, liveLm2.M33, liveLm2.M34);
                    tile.Source.Column4 = new TwinVec4(liveLm2.M41, liveLm2.M42, liveLm2.M43, liveLm2.M44);
                }
                var origMat = tile.Source;
                var newMat  = new TwinMat4
                {
                    Column1 = new TwinVec4(origMat.Column1.X, origMat.Column1.Y, origMat.Column1.Z, origMat.Column1.W),
                    Column2 = new TwinVec4(origMat.Column2.X, origMat.Column2.Y, origMat.Column2.Z, origMat.Column2.W),
                    Column3 = new TwinVec4(origMat.Column3.X, origMat.Column3.Y, origMat.Column3.Z, origMat.Column3.W),
                    Column4 = new TwinVec4(origMat.Column4.X + localOffset.X, origMat.Column4.Y + localOffset.Y, origMat.Column4.Z + localOffset.Z, origMat.Column4.W),
                };
                if (rotDeltaDeg != Vector3.Zero)
                {
                    var (ex, ey, ez) = DecomposeXyzEulerDegrees(MeshDecoder.TwinMatToSys(origMat));
                    float nrx = (ex + rotDeltaDeg.X) * MathF.PI / 180f;
                    float nry = (ey + rotDeltaDeg.Y) * MathF.PI / 180f;
                    float nrz = (ez + rotDeltaDeg.Z) * MathF.PI / 180f;
                    var newRotSys = Matrix4x4.CreateRotationX(nrx) * Matrix4x4.CreateRotationY(nry) * Matrix4x4.CreateRotationZ(nrz);
                    newMat.Column1 = new TwinVec4(newRotSys.M11, newRotSys.M12, newRotSys.M13, newRotSys.M14);
                    newMat.Column2 = new TwinVec4(newRotSys.M21, newRotSys.M22, newRotSys.M23, newRotSys.M24);
                    newMat.Column3 = new TwinVec4(newRotSys.M31, newRotSys.M32, newRotSys.M33, newRotSys.M34);
                }

                var node = tile.Node;
                var origBbox = FindTileBoundingBox(tile);
                var newBbox = origBbox is not null ? MeshDecoder.CloneSceneryBox(origBbox) : new[] { new TwinVec4(), new TwinVec4() };

                const bool UseIndependentLeafForDuplicate = true;

                var scenery = UseIndependentLeafForDuplicate ? chunkSource.Sm2?.GetItem<PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM) : null;
                TwinSceneryBaseType dupNode;
                if (scenery is not null)
                {
                    var leaf = new TwinSceneryLeaf();
                    if (tile.IsLod) { leaf.LodIDs.Add(tile.SourceId); leaf.LodModelMatrices.Add(newMat); }
                    else             { leaf.MeshIDs.Add(tile.SourceId); leaf.MeshModelMatrices.Add(newMat); }
                    leaf.BoundingBoxes.Add(newBbox);
                    Array.Copy(node.LightsEnabler, leaf.LightsEnabler, node.LightsEnabler.Length);

                    var dupPos = new Vector3(newMat.Column4.X, newMat.Column4.Y, newMat.Column4.Z);
                    var a = dupPos + new Vector3(newBbox[0].X, newBbox[0].Y, newBbox[0].Z);
                    var b = dupPos + new Vector3(newBbox[1].X, newBbox[1].Y, newBbox[1].Z);
                    MeshDecoder.GraftIndependentSceneryLeaf(scenery, leaf, Vector3.Min(a, b), Vector3.Max(a, b));
                    dupNode = leaf;

                    int origIdx = tile.IsLod ? node.LodModelMatrices.IndexOf(tile.Source) : node.MeshModelMatrices.IndexOf(tile.Source);
                    int origBboxIdx = tile.IsLod ? node.MeshIDs.Count + origIdx : origIdx;
                    if (origIdx >= 0 && origBboxIdx >= 0 && origBboxIdx < node.BoundingBoxes.Count)
                    {
                        var origLeaf = new TwinSceneryLeaf();
                        if (tile.IsLod)
                        {
                            var id = node.LodIDs[origIdx];
                            node.LodIDs.RemoveAt(origIdx);
                            node.LodModelMatrices.RemoveAt(origIdx);
                            origLeaf.LodIDs.Add(id);
                            origLeaf.LodModelMatrices.Add(tile.Source);
                        }
                        else
                        {
                            var id = node.MeshIDs[origIdx];
                            node.MeshIDs.RemoveAt(origIdx);
                            node.MeshModelMatrices.RemoveAt(origIdx);
                            origLeaf.MeshIDs.Add(id);
                            origLeaf.MeshModelMatrices.Add(tile.Source);
                        }
                        var origOwnBbox = node.BoundingBoxes[origBboxIdx];
                        node.BoundingBoxes.RemoveAt(origBboxIdx);
                        origLeaf.BoundingBoxes.Add(origOwnBbox);
                        Array.Copy(node.LightsEnabler, origLeaf.LightsEnabler, node.LightsEnabler.Length);

                        MeshDecoder.RefitSceneryBounds(node, withLods: true);

                        var origPos = new Vector3(origMat.Column4.X, origMat.Column4.Y, origMat.Column4.Z);
                        var oa = origPos + new Vector3(origOwnBbox[0].X, origOwnBbox[0].Y, origOwnBbox[0].Z);
                        var ob = origPos + new Vector3(origOwnBbox[1].X, origOwnBbox[1].Y, origOwnBbox[1].Z);
                        MeshDecoder.GraftIndependentSceneryLeaf(scenery, origLeaf, Vector3.Min(oa, ob), Vector3.Max(oa, ob));
                        tile.Node = origLeaf;
                    }
                }
                else
                {
                    if (tile.IsLod) { node.LodIDs.Add(tile.SourceId); node.LodModelMatrices.Add(newMat); }
                    else             { node.MeshIDs.Add(tile.SourceId); node.MeshModelMatrices.Add(newMat); }
                    int insertAt = tile.IsLod ? node.BoundingBoxes.Count : node.MeshIDs.Count - 1;
                    node.BoundingBoxes.Insert(Math.Clamp(insertAt, 0, node.BoundingBoxes.Count), newBbox);

                    MeshDecoder.RefitSceneryBounds(node, withLods: true);
                    dupNode = node;
                }

                var newEntity = new Entity($"{original.Name}_copy");
                newEntity.Add(new SceneryTile { Source = newMat, Node = dupNode, SourceId = tile.SourceId, IsLod = tile.IsLod });
                newEntity.Transform.LocalMatrix = MeshDecoder.TwinMatToSys(newMat);
                parent.AddChild(newEntity);
                MeshDecoder.BuildMeshForSceneryTile(Engine.Instance.GL, chunkSource.SceneryTables, newEntity);

                var sceneryAction = new SceneryAddAction
                {
                    Parent = parent, Entity = newEntity, Node = dupNode,
                    IsLod = tile.IsLod, SourceId = tile.SourceId, Matrix = newMat, BoundingBox = newBbox,
                };

                actions.Add(sceneryAction);
                newSelection.Add(newEntity);
            }
            else if (original.Get<LoadWallMarker>() is { } wallMarker)
            {
                var sm2 = chunkSource.Sm2;
                var linkItem = sm2?.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
                if (sm2 is null || linkItem is null) continue;

                static TwinMat4 OffsetMat(TwinMat4 m, Vector3 o) => new TwinMat4
                {
                    Column1 = new TwinVec4(m.Column1.X + o.X, m.Column1.Y + o.Y, m.Column1.Z + o.Z, m.Column1.W),
                    Column2 = new TwinVec4(m.Column2.X + o.X, m.Column2.Y + o.Y, m.Column2.Z + o.Z, m.Column2.W),
                    Column3 = new TwinVec4(m.Column3.X + o.X, m.Column3.Y + o.Y, m.Column3.Z + o.Z, m.Column3.W),
                    Column4 = new TwinVec4(m.Column4.X + o.X, m.Column4.Y + o.Y, m.Column4.Z + o.Z, m.Column4.W),
                };

                var orig  = wallMarker.Source;
                var clone = new TwinChunkLink
                {
                    UnkFlag                 = orig.UnkFlag,
                    Path                    = orig.Path,
                    IsRendered              = orig.IsRendered,
                    UnkNum                  = orig.UnkNum,
                    IsLoadWallActive        = orig.IsLoadWallActive,
                    KeepLoaded              = orig.KeepLoaded,
                    ObjectMatrix            = OffsetMat(MeshDecoder.CloneTwinMat4(orig.ObjectMatrix), offset),
                    ChunkMatrix             = OffsetMat(MeshDecoder.CloneTwinMat4(orig.ChunkMatrix), offset),
                    LoadingWall             = orig.LoadingWall is null ? null : OffsetMat(MeshDecoder.CloneTwinMat4(orig.LoadingWall), offset),
                    ChunkLinksCollisionData = new(orig.ChunkLinksCollisionData),
                };
                linkItem.LinksList.Add(clone);

                if (clone.LoadingWall is null) continue;

                var newEntity = MakeQuadMarker(parent, $"{original.Name}_copy", clone,
                    new Vector4(1f, 0.4f, 0.1f, 0.6f));

                actions.Add(new LoadWallAddAction { Parent = parent, Entity = newEntity, LinkItem = linkItem, Link = clone });
                newSelection.Add(newEntity);
            }
            else if (original.Get<CrashEngine.Importer.AiPositionMarker>() is { } aiMarker)
            {
                var rm2 = chunkSource.Rm2;
                BaseTwinSection? aiSec = null;
                for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION && aiSec is null; lid++)
                    aiSec = rm2?.GetItem<BaseTwinSection>((uint)lid)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);
                if (aiSec is null || _aiPosRoot is null) continue;

                uint newId = 0; var used = new HashSet<uint>();
                for (int i = 0; i < aiSec.GetItemsAmount(); i++)
                    if (aiSec.GetItem(i) is Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition p) used.Add(p.GetID());
                while (used.Contains(newId)) newId++;

                var src = aiMarker.Source;
                var clone = new Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition
                {
                    Position = new TwinVec4(src.Position.X + offset.X, src.Position.Y + offset.Y, src.Position.Z + offset.Z, src.Position.W),
                    UnkShort = src.UnkShort,
                };
                clone.SetID(newId);
                aiSec.AddItem(clone);

                var newEntity = MakeAiPositionMarker(_aiPosRoot, $"AiPosition_{newId:X4}", clone.Position); // Amedo
                newEntity.Add(new CrashEngine.Importer.AiPositionMarker { Source = clone });
                newSelection.Add(newEntity);
            }
        }
        }

        if (actions.Count == 0 && newSelection.Count == 0)
        {
            _browser.Log("Duplicate (Ctrl+D): no real game objects/scenery tiles/load walls/AI positions in the selection (cubes can't be duplicated).");
            return;
        }

        // Amedo 2026-09-20
        var beforeSel = _selectedSet.ToList();

        _selectedSet.Clear();
        foreach (var e in newSelection) _selectedSet.Add(e);
        _selected = newSelection.Count > 0 ? newSelection[0] : null;
        _revealSelectionInTree = true;

        if (actions.Count > 0)
        {
            var addAction = actions.Count == 1 ? actions[0] : new CompositeEditAction { Actions = actions };
            var selAction = new SelectionChangeAction
            {
                Before = beforeSel,
                After  = new List<Entity>(newSelection),
                Apply  = ApplySelectionList,
            };
            _undoStack.Push(new CompositeEditAction
            {
                Actions = new IEditAction[] { addAction, selAction },
                HandlesSelectionItself = true,
            });
        }
        SyncSelSnapshot();

        var verb = repeatCount > 1 ? "Array" : "Duplicated";
        _browser.Log($"{verb}: {newSelection.Count} item(s) added (offset next to the original). No collision auto-generated — " +
                      "use Generate Mesh Collision if the duplicate(s) need any.");
    }
}
