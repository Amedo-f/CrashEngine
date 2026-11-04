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
}
