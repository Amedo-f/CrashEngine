using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
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
        Vector3? arrayStepOffset = null, Vector3 arrayStepRotationDeg = default, float arrayStepGap = 0f)
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
                clone.Position.X += offset.X;
                clone.Position.Y += offset.Y;
                clone.Position.Z += offset.Z;

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
                    Column4 = new TwinVec4(origMat.Column4.X + offset.X, origMat.Column4.Y + offset.Y, origMat.Column4.Z + offset.Z, origMat.Column4.W),
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
                var newBbox = origBbox is not null
                    ? new[]
                      {
                          new TwinVec4(origBbox[0].X, origBbox[0].Y, origBbox[0].Z, origBbox[0].W),
                          new TwinVec4(origBbox[1].X, origBbox[1].Y, origBbox[1].Z, origBbox[1].W),
                      }
                    : new[] { new TwinVec4(), new TwinVec4() };

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

                        var oMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                        var oMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                        bool oAny = false;
                        for (int j = 0; j < node.MeshIDs.Count; j++)
                        {
                            var p = new Vector3(node.MeshModelMatrices[j].Column4.X, node.MeshModelMatrices[j].Column4.Y, node.MeshModelMatrices[j].Column4.Z);
                            var bx = node.BoundingBoxes[j];
                            oMin = Vector3.Min(oMin, p + new Vector3(bx[0].X, bx[0].Y, bx[0].Z));
                            oMax = Vector3.Max(oMax, p + new Vector3(bx[1].X, bx[1].Y, bx[1].Z));
                            oAny = true;
                        }
                        for (int j = 0; j < node.LodIDs.Count; j++)
                        {
                            var p = new Vector3(node.LodModelMatrices[j].Column4.X, node.LodModelMatrices[j].Column4.Y, node.LodModelMatrices[j].Column4.Z);
                            var bx = node.BoundingBoxes[node.MeshIDs.Count + j];
                            oMin = Vector3.Min(oMin, p + new Vector3(bx[0].X, bx[0].Y, bx[0].Z));
                            oMax = Vector3.Max(oMax, p + new Vector3(bx[1].X, bx[1].Y, bx[1].Z));
                            oAny = true;
                        }
                        if (oAny) MeshDecoder.ApplySceneryBounds(node, oMin, oMax);

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

                    var bMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    var bMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                    bool any = false;
                    for (int j = 0; j < node.MeshIDs.Count; j++)
                    {
                        var pos = new Vector3(node.MeshModelMatrices[j].Column4.X, node.MeshModelMatrices[j].Column4.Y, node.MeshModelMatrices[j].Column4.Z);
                        var box = node.BoundingBoxes[j];
                        var a = pos + new Vector3(box[0].X, box[0].Y, box[0].Z);
                        var b = pos + new Vector3(box[1].X, box[1].Y, box[1].Z);
                        bMin = Vector3.Min(bMin, Vector3.Min(a, b));
                        bMax = Vector3.Max(bMax, Vector3.Max(a, b));
                        any = true;
                    }
                    for (int j = 0; j < node.LodIDs.Count; j++)
                    {
                        var pos = new Vector3(node.LodModelMatrices[j].Column4.X, node.LodModelMatrices[j].Column4.Y, node.LodModelMatrices[j].Column4.Z);
                        var box = node.BoundingBoxes[node.MeshIDs.Count + j];
                        var a = pos + new Vector3(box[0].X, box[0].Y, box[0].Z);
                        var b = pos + new Vector3(box[1].X, box[1].Y, box[1].Z);
                        bMin = Vector3.Min(bMin, Vector3.Min(a, b));
                        bMax = Vector3.Max(bMax, Vector3.Max(a, b));
                        any = true;
                    }
                    if (any) MeshDecoder.ApplySceneryBounds(node, bMin, bMax);
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

                static TwinMat4 CloneMat(TwinMat4 m) => new TwinMat4
                {
                    Column1 = new TwinVec4(m.Column1.X, m.Column1.Y, m.Column1.Z, m.Column1.W),
                    Column2 = new TwinVec4(m.Column2.X, m.Column2.Y, m.Column2.Z, m.Column2.W),
                    Column3 = new TwinVec4(m.Column3.X, m.Column3.Y, m.Column3.Z, m.Column3.W),
                    Column4 = new TwinVec4(m.Column4.X, m.Column4.Y, m.Column4.Z, m.Column4.W),
                };
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
                    ObjectMatrix            = OffsetMat(CloneMat(orig.ObjectMatrix), offset),
                    ChunkMatrix             = OffsetMat(CloneMat(orig.ChunkMatrix), offset),
                    LoadingWall             = orig.LoadingWall is null ? null : OffsetMat(CloneMat(orig.LoadingWall), offset),
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

                var newEntity = MakePointMarker(_aiPosRoot, $"AiPosition_{newId:X4}", clone.Position, new Vector4(1f, 0.25f, 0.25f, 0.7f));
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

        if (actions.Count > 0)
            _undoStack.Push(actions.Count == 1 ? actions[0] : new CompositeEditAction { Actions = actions });

        _selectedSet.Clear();
        foreach (var e in newSelection) _selectedSet.Add(e);
        _selected = newSelection.Count > 0 ? newSelection[0] : null;
        _revealSelectionInTree = true;

        var verb = repeatCount > 1 ? "Array" : "Duplicated";
        _browser.Log($"{verb}: {newSelection.Count} item(s) added (offset next to the original). No collision auto-generated — " +
                      "use Generate Mesh Collision if the duplicate(s) need any.");
    }

    private static TwinVec4[]? FindTileBoundingBox(SceneryTile tile)
    {
        var node = tile.Node;
        int idx = tile.IsLod ? node.LodModelMatrices.IndexOf(tile.Source) : node.MeshModelMatrices.IndexOf(tile.Source);
        if (idx < 0) return null;
        int bboxIdx = tile.IsLod ? node.MeshIDs.Count + idx : idx;
        return bboxIdx >= 0 && bboxIdx < node.BoundingBoxes.Count ? node.BoundingBoxes[bboxIdx] : null;
    }

    private static (float X, float Y, float Z) DecomposeXyzEulerDegrees(Matrix4x4 m)
    {
        float sb = Math.Clamp(-m.M13, -1f, 1f);
        float b  = MathF.Asin(sb);
        float cb = MathF.Cos(b);
        float a, c;
        if (MathF.Abs(cb) > 1e-5f)
        {
            a = MathF.Atan2(m.M23, m.M33);
            c = MathF.Atan2(m.M12, m.M11);
        }
        else
        {
            c = 0f;
            a = sb > 0 ? MathF.Atan2(m.M21, m.M22) : MathF.Atan2(-m.M21, m.M22);
        }
        return (a * 180f / MathF.PI, b * 180f / MathF.PI, c * 180f / MathF.PI);
    }

    private void ConvertSceneryTileToObject(Entity tileEntity)
    {
        var tile = tileEntity.Get<SceneryTile>();
        if (tile is null) { _browser.Log("Convert to Object: not a real scenery tile."); return; }

        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null || chunkSource.Sm2 is null || chunkSource.MeshTables is null)
        { _browser.Log("Convert to Object: no level (with real scenery) currently loaded."); return; }

        if (chunkSource.SceneryTables is null || MeshDecoder.ResolveSceneryMeshId(chunkSource.SceneryTables, tile) is not { } meshId)
        { _browser.Log("Convert to Object: couldn't resolve this tile's real mesh."); return; }

        var bmin = new Vector3(float.MaxValue); var bmax = new Vector3(float.MinValue);
        bool anyVerts = false;
        Matrix4x4.Invert(tileEntity.Transform.World, out var tileWorldInv);
        foreach (var ent in AllEntities(tileEntity))
        {
            var mr = ent.Get<CrashEngine.Importer.MeshRenderer>();
            if (mr?.Mesh?.RaycastVertices is not { } verts) continue;
            var local = ent.Transform.World * tileWorldInv;
            foreach (var v in verts)
            {
                var p = Vector3.Transform(v.Position, local);
                bmin = Vector3.Min(bmin, p); bmax = Vector3.Max(bmax, p);
                anyVerts = true;
            }
        }
        if (!anyVerts) { _browser.Log($"Convert to Object: '{tileEntity.Name}' has no real mesh geometry."); return; }

        var instRootForPlacement = chunkRoot.Children.FirstOrDefault(c => c.Name == "Instances") ?? chunkRoot;
        var placementLocal = Matrix4x4.Invert(instRootForPlacement.Transform.World, out var instRootInv)
            ? tileEntity.Transform.World * instRootInv : tileEntity.Transform.World;
        Matrix4x4.Decompose(placementLocal, out var scale, out var rotQuat, out var worldPos);
        var rotMat = Matrix4x4.CreateFromQuaternion(rotQuat);
        var (ex, ey, ez) = DecomposeXyzEulerDegrees(rotMat);
        var rebuilt = Matrix4x4.CreateRotationX(ex * MathF.PI / 180f) *
                      Matrix4x4.CreateRotationY(ey * MathF.PI / 180f) *
                      Matrix4x4.CreateRotationZ(ez * MathF.PI / 180f);
        float rotError = 0f;
        rotError = MathF.Max(rotError, MathF.Abs(rebuilt.M11 - rotMat.M11)); rotError = MathF.Max(rotError, MathF.Abs(rebuilt.M12 - rotMat.M12)); rotError = MathF.Max(rotError, MathF.Abs(rebuilt.M13 - rotMat.M13));
        rotError = MathF.Max(rotError, MathF.Abs(rebuilt.M21 - rotMat.M21)); rotError = MathF.Max(rotError, MathF.Abs(rebuilt.M22 - rotMat.M22)); rotError = MathF.Max(rotError, MathF.Abs(rebuilt.M23 - rotMat.M23));
        rotError = MathF.Max(rotError, MathF.Abs(rebuilt.M31 - rotMat.M31)); rotError = MathF.Max(rotError, MathF.Abs(rebuilt.M32 - rotMat.M32)); rotError = MathF.Max(rotError, MathF.Abs(rebuilt.M33 - rotMat.M33));
        if (rotError > 0.01f)
        {
            _browser.Log($"Convert to Object: aborted — orientation decomposition didn't verify (error {rotError:E2}), refusing to write a possibly-wrong rotation.");
            return;
        }

        var gl = Engine.Instance.GL;
        var rec = MeshDecoder.ConvertSceneryMeshToObject(gl, chunkSource.Rm2, chunkSource.MeshTables,
            chunkSource.TexCache, chunkSource.Sm2, chunkSource.GlobalRm2, meshId,
            new TwinVec4(bmin.X, bmin.Y, bmin.Z, 1f), new TwinVec4(bmax.X, bmax.Y, bmax.Z, 1f),
            scale, tileEntity.Name, out string log);
        if (rec is null) { _browser.Log($"Convert to Object: {log}"); return; }

        var section = AllEntities(chunkRoot).Select(e => e.Get<InstanceData>()?.Section).FirstOrDefault(s => s is not null);
        if (section is null)
        {
            var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_LAYOUT_1_SECTION);
            section = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
            if (section is null) { _browser.Log("Convert to Object: this level has no instances section to add to."); return; }
        }

        uint templateStateFlags = (uint)(
            Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState.Visible |
            Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState.ShadowActive);

        var inst = new PS2AnyInstance
        {
            Position              = new TwinVec4(worldPos.X, worldPos.Y, worldPos.Z, 1f),
            RotationX             = new TwinIntegerRotation(),
            RotationY             = new TwinIntegerRotation(),
            RotationZ             = new TwinIntegerRotation(),
            ObjectId              = (ushort)rec.ObjectId,
            RefListIndex          = -1,
            OnSpawnHeaderScriptID = 0xFFFF,
            StateFlags            = templateStateFlags,
            InstancesRelated      = 0, Instances = new List<ushort>(),
            PositionsRelated      = 0, Positions = new List<ushort>(),
            PathsRelated          = 0, Paths     = new List<ushort>(),
            ParamList1            = new List<uint>(),
            ParamList2            = new List<float>(),
            ParamList3            = new List<uint>(),
        };
        inst.RotationX.SetRotation(ex);
        inst.RotationY.SetRotation(ey);
        inst.RotationZ.SetRotation(ez);

        var newEntity = AddOrReuseInstance(section, inst, instRootForPlacement, chunkSource);

        var bbox = FindTileBoundingBox(tile);
        string scaleNote = (MathF.Abs(scale.X - 1f) > 0.01f || MathF.Abs(scale.Y - 1f) > 0.01f || MathF.Abs(scale.Z - 1f) > 0.01f)
            ? $" (tile had non-1.0 scale {scale.X:F2},{scale.Y:F2},{scale.Z:F2} — baked into the new geometry directly.)"
            : "";
        if (bbox is null)
        {
            _browser.Log($"Convert to Object: added the new real Object ({log}) but could NOT " +
                          "safely remove the original tile (its bounding-box entry wasn't found) " +
                          $"-- delete the old tile manually, the level now has both.{scaleNote}");
        }
        else
        {
            var deleteTile = new SceneryAddAction
            {
                Parent = tileEntity.Parent!, Entity = tileEntity, Node = tile.Node,
                IsLod = tile.IsLod, SourceId = tile.SourceId, Matrix = tile.Source, BoundingBox = bbox,
            };
            deleteTile.Undo();
            _undoStack.Push(new InverseEditAction { Inner = deleteTile });
            _browser.Log($"Converted '{tileEntity.Name}' to a real Object: {log} Original scenery " +
                          "tile removed (on the Undo stack). The new Object isn't itself on the " +
                          "Undo stack yet, but Delete Selected removes it normally. Save Chunk to " +
                          $"keep it, then Build ISO + test that it no longer disappears near the camera.{scaleNote}");
        }
        SelectClicked(newEntity, false);
    }

    private readonly List<Action<float>> _pulseResetters = new();

    private void UpdateSelectionPulse()
    {
        foreach (var reset in _pulseResetters) reset(1f);
        _pulseResetters.Clear();
        if (_selectedSet.Count == 0) return;

        float pulse = 1f + MathF.Sin(EngineTime.Total * 4.5f) * 0.3f;

        void Pulse(Action<float> set) { set(pulse); _pulseResetters.Add(set); }

        foreach (var root in _selectedSet)
            foreach (var e in AllEntities(root))
            {
                if (e.Get<CrashEngine.Importer.MeshRenderer>() is { } mr1) Pulse(v => mr1.SelectPulse = v);
                if (e.Get<CrashEngine.Renderer.MeshRenderer>()  is { } mr2) Pulse(v => mr2.SelectPulse = v);
                if (e.Get<DirectCubeRenderer>()                 is { } dcr) Pulse(v => dcr.SelectPulse = v);
            }
    }

    private const bool USE_COMPACT_DELETE_FOR_NATIVE = false;

    private static ushort? FindInertPlaceholderObjectId(PS2AnyTwinsanityRM2 rm2)
    {
        var code   = rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION);
        var objSec = code?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
        if (objSec is null) return null;

        for (int i = 0; i < objSec.GetItemsAmount(); i++)
        {
            if (objSec.GetItem(i) is not PS2AnyObject o) continue;
            bool hasMesh = o.OGISlots.Any(s => s != 0xFFFF);
            if (!hasMesh && o.TriggerBehaviours.Count == 0)
                return (ushort)o.GetID();
        }
        return null;
    }

    private const string EmptyPlaceholderObjectName = "CE_EmptyPlaceholder";

    private static ushort GetOrCreateEmptyPlaceholderObjectId(BaseTwinSection objSec, BaseTwinSection? globalObjSec)
    {
        for (int i = 0; i < objSec.GetItemsAmount(); i++)
            if (objSec.GetItem(i) is PS2AnyObject existing && existing.Name == EmptyPlaceholderObjectName)
                return (ushort)existing.GetID();

        uint newId = 0;
        while (objSec.ContainsItem(newId) || (globalObjSec?.ContainsItem(newId) ?? false)) newId++;

        var empty = new PS2AnyObject
        {
            Type = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType.GenericObject,
            Name = EmptyPlaceholderObjectName,
            BehaviourPack = new Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommandPack(),
        };
        empty.SetID(newId);
        objSec.AddItem(empty);
        return (ushort)newId;
    }

    private void DeleteSelected()
    {
        if (_selectedSet.Count == 0) return;

        var actions   = new List<IEditAction>();
        int cubeCount = 0, importCount = 0, aiPosDelCount = 0;
        var deletedTransplantObjIds = new HashSet<uint>();

        var chunkSourceForPlaceholder = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var objSecForPlaceholder = chunkSourceForPlaceholder?.Rm2
            .GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
        var globalObjSecForPlaceholder = chunkSourceForPlaceholder?.GlobalRm2
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
        ushort? placeholderObjectId = objSecForPlaceholder is not null
            ? GetOrCreateEmptyPlaceholderObjectId(objSecForPlaceholder, globalObjSecForPlaceholder)
            : null;

        foreach (var e in _selectedSet.ToList())
        {
            var parent = e.Parent;

            foreach (var desc in AllEntities(e))
            {
                if (desc == e) continue;
                if (desc.Has<CollisionShapeMarker>() && _cubes.FirstOrDefault(c => c.Ent == desc) is { } childCube
                    && !_pendingDel.Contains(childCube))
                { _pendingDel.Add(childCube); cubeCount++; }
            }

            if (parent is not null && e.Get<InstanceData>() is { } data)
            {
                if (data.IsUserAdded)
                {
                    var add = new InstanceAddAction { Parent = parent, Entity = e, Section = data.Section, Item = (ITwinItem)data.Source };
                    add.Undo();
                    actions.Add(new InverseEditAction { Inner = add });
                }
                else if (USE_COMPACT_DELETE_FOR_NATIVE)
                {
                    var compact = new InstanceCompactDeleteAction { Entity = e, Parent = parent, Section = data.Section, Item = data.Source };
                    compact.Redo();
                    actions.Add(compact);
                }
                else
                {
                    var neuter = new InstanceNeuterAction { Entity = e, Parent = parent, Item = data.Source, PlaceholderObjectId = placeholderObjectId };
                    neuter.Redo();
                    actions.Add(neuter);
                }
                deletedTransplantObjIds.Add(data.ObjectId);
            }
            else if (parent is not null && e.Get<SceneryTile>() is { } tile)
            {
                var bbox = FindTileBoundingBox(tile);
                if (bbox is null) continue;
                var add = new SceneryAddAction
                {
                    Parent = parent, Entity = e, Node = tile.Node,
                    IsLod = tile.IsLod, SourceId = tile.SourceId, Matrix = tile.Source, BoundingBox = bbox,
                };
                add.Undo();

                // Amedo 2026-09-20
                var csBake = chunkSourceForPlaceholder;
                if (csBake?.Sm2 is not null && csBake.SceneryBakes.TryGetValue(tile.SourceId, out var bakeRec))
                {
                    MeshDecoder.RemoveSceneryBake(csBake.Sm2, bakeRec, csBake.SceneryTexCache);
                    csBake.SceneryBakes.Remove(tile.SourceId);
                    importCount++;
                    _browser.Log($"Deleted imported scenery (mesh 0x{tile.SourceId:X}) and freed its baked graphics " +
                                  $"({bakeRec.ModelIds.Count} model, {bakeRec.MaterialIds.Count} material, {bakeRec.TextureIds.Count} texture) — chunk stays clean.");
                }
                else
                {
                    actions.Add(new InverseEditAction { Inner = add });
                }
            }
            else if (parent is not null && e.Get<TriggerMarker>() is { } trigMarker)
            {
                var neuter = new TriggerVolumeNeuterAction { Entity = e, Parent = parent, Trigger = trigMarker.Source.Trigger };
                neuter.Redo();
                actions.Add(neuter);
            }
            else if (parent is not null && e.Get<CameraMarker>() is { } camMarker)
            {
                var neuter = new TriggerVolumeNeuterAction { Entity = e, Parent = parent, Trigger = camMarker.Source.CamTrigger };
                neuter.Redo();
                actions.Add(neuter);
            }
            else if (parent is not null && e.Get<ParticleEmitterMarker>() is { } emitterMarker)
            {
                var particleData = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2
                    .GetItem<PS2AnyParticleData>((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
                if (particleData is not null)
                {
                    var add = new ParticleEmitterAddAction { Parent = parent, Entity = e, List = particleData.ParticleEmitters, Item = emitterMarker.Source };
                    add.Undo();
                    actions.Add(new InverseEditAction { Inner = add });
                }
            }
            else if (parent is not null && e.Get<CrashEngine.Importer.LoadWallMarker>() is { } wallMarker)
            {
                DeleteLoadLinkPair(wallMarker.Source, actions);
            }
            else if (parent is not null && e.Get<CrashEngine.Importer.LinkedSceneryLink>() is { } sceneryLink)
            {
                DeleteLoadLinkPair(sceneryLink.Source, actions);
            }
            else if (parent is not null && e.Get<AiPositionMarker>() is { } aiMarker)
            {
                var ap = aiMarker.Source;
                uint apId = ap.GetID();
                var rm2 = chunkSourceForPlaceholder?.Rm2;
                if (rm2 is not null)
                    for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
                    {
                        var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
                        layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION)?.RemoveItem(ap);
                        var pathSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
                        if (pathSec is not null)
                            for (int i = pathSec.GetItemsAmount() - 1; i >= 0; i--)
                                if (pathSec.GetItem(i) is Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPath pth
                                    && (pth.Args[0] == apId || pth.Args[1] == apId))
                                    pathSec.RemoveItem(pth);
                    }
                parent.RemoveChild(e);
                aiPosDelCount++;
            }
            // Amedo 2026-09-19
            else if (parent is not null && e.Get<CrashEngine.Importer.SceneryLightMarker>() is { } lightMarker && lightMarker.Source is not null)
            {
                var scenery = chunkSourceForPlaceholder?.Sm2
                    ?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);
                if (scenery is not null)
                {
                    if (lightMarker.IsNegative && lightMarker.Source is Twinsanity.TwinsanityInterchange.Common.Lights.NegativeLight nl)
                        scenery.NegativeLights.Remove(nl);
                    else if (!lightMarker.IsNegative && lightMarker.Source is Twinsanity.TwinsanityInterchange.Common.Lights.PointLight pl)
                        scenery.PointLights.Remove(pl);
                }
                parent.RemoveChild(e);
                aiPosDelCount++;
            }
            else if (e.Has<PositionMarker>())
            {
                _browser.Log("Delete: (non-AI) Position markers aren't deletable yet -- they may be " +
                              "referenced by an instance's Positions list by numeric id.");
            }
            else if (_cubes.FirstOrDefault(c => c.Ent == e) is { } cube)
            {
                if (!_pendingDel.Contains(cube)) _pendingDel.Add(cube);
                cubeCount++;
            }
            else if (_importedRoots.Contains(e))
            {
                if (!_pendingDelImports.Contains(e)) _pendingDelImports.Add(e);
                importCount++;
            }
        }

        if (actions.Count == 0 && cubeCount == 0 && importCount == 0 && aiPosDelCount == 0)
        {
            _browser.Log("Delete: nothing deletable in the selection.");
            return;
        }
        if (aiPosDelCount > 0)
            _browser.Log($"Delete: removed {aiPosDelCount} AI position(s) + any paths that referenced them. Save Chunk to persist.");

        var chunkSourceForCleanup = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        if (chunkSourceForCleanup?.MeshTables is not null)
        {
            foreach (var objId in deletedTransplantObjIds)
            {
                if (!chunkSourceForCleanup.Transplants.TryGetValue(objId, out var rec)) continue;
                bool stillUsed = FlatEntities().Any(fe => fe.Get<InstanceData>()?.ObjectId == objId);
                if (stillUsed) continue;
                MeshDecoder.RemoveTransplant(chunkSourceForCleanup.Rm2,
                    chunkSourceForCleanup.MeshTables, chunkSourceForCleanup.TexCache, rec);
                chunkSourceForCleanup.Transplants.Remove(objId);
                _browser.Log($"Removed transplant data for object 0x{objId:X4} (last instance deleted) — chunk is clean.");
            }

            foreach (var objId in deletedTransplantObjIds)
            {
                if (!chunkSourceForCleanup.FullTransplants.TryGetValue(objId, out var fullRec)) continue;
                bool stillUsed = FlatEntities().Any(fe => fe.Get<InstanceData>()?.ObjectId == objId);
                if (stillUsed) continue;
                MeshDecoder.RemoveFullTransplant(chunkSourceForCleanup.Rm2,
                    chunkSourceForCleanup.MeshTables, chunkSourceForCleanup.TexCache, fullRec);
                chunkSourceForCleanup.FullTransplants.Remove(objId);
                _browser.Log($"Removed full transplant data for object 0x{objId:X4} (last instance deleted) — " +
                              "object graph + scripts + graphics all cleaned up.");
            }
        }

        if (actions.Count > 0)
            _undoStack.Push(actions.Count == 1 ? actions[0] : new CompositeEditAction { Actions = actions });

        _selectedSet.Clear();
        _selected = null;
        _browser.Log($"Deleted {actions.Count + cubeCount + importCount} object(s)" +
                     (actions.Count > 0 && (cubeCount + importCount) > 0 ? " (mixed — only PS2 objects are undo-able)." : "."));
    }

    private void DeleteLoadLinkPair(TwinChunkLink link, List<IEditAction> actions)
    {
        var linkItem = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Sm2
            ?.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
        if (linkItem is null) return;

        var pairs = new List<(Entity Parent, Entity Entity)>();
        foreach (var ent in FlatEntities())
        {
            if (ent.Parent is null) continue;
            if (ReferenceEquals(ent.Get<CrashEngine.Importer.LoadWallMarker>()?.Source, link) ||
                ReferenceEquals(ent.Get<CrashEngine.Importer.LinkedSceneryLink>()?.Source, link))
                pairs.Add((ent.Parent, ent));
        }
        if (pairs.Count == 0) return;

        var action = new LoadLinkPairDeleteAction { Entities = pairs, LinkItem = linkItem, Link = link };
        action.Redo();
        actions.Add(action);
    }
}
