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
using PS2AnyPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPath;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;
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
using ITwinObject = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private ushort?                    _lastAddedInstanceId;

    private bool                     _showObjectTransplantLevelPicker;
    private string                   _objectTransplantLevelFilter = "";
    private bool                     _showObjectTransplantObjectPicker;
    private string                   _objectTransplantSelectedLevel = "";
    private string                   _objectTransplantObjectFilter = "";
    private List<(uint Id, string Name, ITwinObject.ObjectType Type)>? _objectTransplantObjectList;

    private void DrawObjectTransplantLevelPickerWindow()
    {
        if (!_showObjectTransplantLevelPicker) return;
        ImGui.SetNextWindowSize(new Vector2(480f, 420f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Add Object: pick a source level##objtransplantlevel", ref _showObjectTransplantLevelPicker))
        {
            ImGui.End();
            return;
        }
        ImGui.TextDisabled("Pick the level to copy an object from (full script/OGI/animation/sound transplant).");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##objTransplantLevelFilter", "Search levels...", ref _objectTransplantLevelFilter, 128);
        ImGui.BeginChild("##objTransplantLevelList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var lvl in GetSwapLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_objectTransplantLevelFilter) &&
                !lvl.Contains(_objectTransplantLevelFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable(lvl))
            {
                _objectTransplantSelectedLevel = lvl;
                _objectTransplantObjectList = LoadObjectListForLevel(lvl);
                _showObjectTransplantLevelPicker = false;
                _showObjectTransplantObjectPicker = true;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private List<(uint Id, string Name, ITwinObject.ObjectType Type)> LoadObjectListForLevel(string levelPath)
    {
        var result = new List<(uint, string, ITwinObject.ObjectType)>();
        var seenIds = new HashSet<uint>();
        try
        {
            var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? levelPath[..^4] : levelPath;
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2");
            if (stream is not null)
            {
                var srcRm2 = new PS2AnyTwinsanityRM2();
                using (var reader = new BinaryReader(stream)) srcRm2.Read(reader, (int)stream.Length);
                var objSec = srcRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                    ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
                if (objSec is not null)
                {
                    for (int i = 0; i < objSec.GetItemsAmount(); i++)
                    {
                        var obj = (PS2AnyObject)objSec.GetItem(i);
                        if (seenIds.Add(obj.GetID())) result.Add((obj.GetID(), obj.Name, obj.Type));
                    }
                }
            }

            using var globalStream = pkg.OpenByPath(@"Startup\Default.rm2");
            if (globalStream is not null)
            {
                var globalRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
                using (var reader = new BinaryReader(globalStream)) globalRm2.Read(reader, (int)globalStream.Length);
                var gObjSec = globalRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                    ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
                if (gObjSec is not null)
                {
                    for (int i = 0; i < gObjSec.GetItemsAmount(); i++)
                    {
                        var obj = (PS2AnyObject)gObjSec.GetItem(i);
                        if (seenIds.Add(obj.GetID())) result.Add((obj.GetID(), obj.Name, obj.Type));
                    }
                }
            }
        }
        catch (Exception ex) { _browser.Log($"Add Object: failed to list objects in {levelPath}: {ex.Message}"); }
        return result;
    }

    private void DrawObjectTransplantObjectPickerWindow()
    {
        if (!_showObjectTransplantObjectPicker) return;
        ImGui.SetNextWindowSize(new Vector2(560f, 460f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Add Object: pick an object from {_objectTransplantSelectedLevel}##objtransplantobj", ref _showObjectTransplantObjectPicker))
        {
            ImGui.End();
            return;
        }
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##objTransplantObjFilter", "Search by name or hex id...", ref _objectTransplantObjectFilter, 128);
        ImGui.BeginChild("##objTransplantObjList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var (id, name, type) in _objectTransplantObjectList ?? Enumerable.Empty<(uint, string, ITwinObject.ObjectType)>())
        {
            if (!string.IsNullOrWhiteSpace(_objectTransplantObjectFilter) &&
                !name.Contains(_objectTransplantObjectFilter, StringComparison.OrdinalIgnoreCase) &&
                !$"{id:X4}".Contains(_objectTransplantObjectFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable($"0x{id:X4}  {name}  ({type})"))
            {
                AddFullObjectTransplant(_objectTransplantSelectedLevel, id, name);
                _showObjectTransplantObjectPicker = false;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void AddFullObjectTransplant(string sourceLevelPath, uint objectId, string objectName)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Object: no level currently loaded."); return; }

        var basePath = sourceLevelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? sourceLevelPath[..^4] : sourceLevelPath;

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2")
                ?? throw new FileNotFoundException($"'{basePath}.rm2' not found in the package.");
            var sourceRm2 = new PS2AnyTwinsanityRM2();
            using (var reader = new BinaryReader(stream))
                sourceRm2.Read(reader, (int)stream.Length);

            PS2AnyTwinsanityRM2 effectiveSourceRm2 = sourceRm2;
            var localObjSecCheck = sourceRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
            if (localObjSecCheck?.GetItem<PS2AnyObject>(objectId) is null)
            {
                using var globalStream = pkg.OpenByPath(@"Startup\Default.rm2");
                if (globalStream is not null)
                {
                    var globalRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
                    using (var reader = new BinaryReader(globalStream)) globalRm2.Read(reader, (int)globalStream.Length);
                    var gObjSecCheck = globalRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                        ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
                    if (gObjSecCheck?.GetItem<PS2AnyObject>(objectId) is not null)
                        effectiveSourceRm2 = globalRm2;
                }
            }

            var rec = MeshDecoder.TransplantObjectFull(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                chunkSource.TexCache, effectiveSourceRm2, objectId, out string log);
            if (rec is null) { _browser.Log($"Add Object: {log}"); return; }
            chunkSource.FullTransplants[rec.RootObjectId] = rec;

            var section = AllEntities(chunkRoot).Select(e => e.Get<InstanceData>()?.Section).FirstOrDefault(s => s is not null);
            if (section is null)
            {
                var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_LAYOUT_1_SECTION);
                section = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (section is null) { _browser.Log("Add Object: this level has no instances section to add to."); return; }
            }

            PS2AnyInstance? srcInst = null;
            for (int lid = 0; lid <= 7 && srcInst is null; lid++)
            {
                var srcLayout = sourceRm2.GetItem<BaseTwinSection>((uint)lid);
                var srcInstSec = srcLayout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (srcInstSec is null) continue;
                for (int i = 0; i < srcInstSec.GetItemsAmount(); i++)
                {
                    var cand = (PS2AnyInstance)srcInstSec.GetItem(i);
                    if (cand.ObjectId == (ushort)objectId) { srcInst = cand; break; }
                }
            }

            var srcPositions = srcInst?.Positions ?? new List<ushort>();
            var srcPaths     = srcInst?.Paths ?? new List<ushort>();
            var copiedPositions = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPosition>(
                sourceRm2, chunkSource.Rm2, section, srcPositions,
                (int)TwinConstants.LAYOUT_POSITIONS_SECTION, out int posNotFound);
            var copiedPaths = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPath>(
                sourceRm2, chunkSource.Rm2, section, srcPaths,
                (int)TwinConstants.LAYOUT_PATHS_SECTION, out int pathNotFound);
            EnsurePositionMarkersExist(chunkRoot, chunkSource.Rm2, srcPositions);

            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            var inst = new PS2AnyInstance
            {
                Position              = new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f),
                RotationX             = new TwinIntegerRotation(),
                RotationY             = new TwinIntegerRotation(),
                RotationZ             = new TwinIntegerRotation(),
                ObjectId              = (ushort)rec.RootObjectId,
                RefListIndex          = srcInst?.RefListIndex ?? -1,
                OnSpawnHeaderScriptID = srcInst?.OnSpawnHeaderScriptID ?? 0xFFFF,
                StateFlags            = srcInst?.StateFlags ?? 0,
                InstancesRelated      = 0, Instances = new List<ushort>(),
                PositionsRelated      = srcInst?.PositionsRelated ?? 0, Positions = copiedPositions,
                PathsRelated          = srcInst?.PathsRelated ?? 0,     Paths     = copiedPaths,
                ParamList1            = srcInst is not null ? new List<uint>(srcInst.ParamList1) : new List<uint>(),
                ParamList2            = srcInst is not null ? new List<float>(srcInst.ParamList2) : new List<float>(),
                ParamList3            = srcInst is not null ? new List<uint>(srcInst.ParamList3) : new List<uint>(),
            };
            if (srcInst is not null && srcInst.Instances.Count > 0)
                _browser.Log($"Add Object: source instance also references {srcInst.Instances.Count} " +
                              "other placed Instance id(s) — these are NOT copied/remapped (left empty); " +
                              "each would itself need its own full object transplant, out of scope here.");
            if (posNotFound > 0)
                _browser.Log($"Add Object: {posNotFound} of {srcPositions.Count} referenced Position(s) not found in the source level — skipped.");
            if (pathNotFound > 0)
                _browser.Log($"Add Object: {pathNotFound} of {srcPaths.Count} referenced Path(s) not found in the source level — skipped.");
            if (copiedPositions.Count > 0 || copiedPaths.Count > 0)
                _browser.Log($"Add Object: copied {copiedPositions.Count} Position(s) and {copiedPaths.Count} Path(s) referenced by the source instance.");
            var instRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "Instances") ?? chunkRoot;
            var newEntity = AddOrReuseInstance(section, inst, instRoot, chunkSource);

            _lastAddedInstanceId = (ushort?)newEntity.Get<InstanceData>()?.Source?.GetID();

            SelectClicked(newEntity, false);
            _browser.Log($"Add Object: {log} '{objectName}' placed at the camera position. Not on the Undo " +
                          "stack yet, but Delete Selected now fully removes it (object graph + OGIs/" +
                          "Animations/Behaviours/Sounds/graphics) once its last instance is gone. " +
                          "Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Object failed: {ex.Message}"); }
    }
}
