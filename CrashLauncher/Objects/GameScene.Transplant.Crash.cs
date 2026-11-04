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
    private bool   _showCrashPicker;
    private string _crashPickerFilter = "";

    private void DrawCrashPickerWindow()
    {
        if (!_showCrashPicker) return;
        ImGui.SetNextWindowSize(new Vector2(480f, 420f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Add Crash (full, with scripts)##crashpicker", ref _showCrashPicker))
        {
            ImGui.End();
            return;
        }
        ImGui.TextDisabled("Pick a level that already has a real, working Crash to copy from\n" +
                            "(confirmed identical between beach/gpa09 — any real level works).");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##crashPickerFilter", "Search levels...", ref _crashPickerFilter, 128);
        ImGui.BeginChild("##crashPickerList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var lvl in GetSwapLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_crashPickerFilter) &&
                !lvl.Contains(_crashPickerFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable(lvl))
            {
                AddFullCrashTransplant(lvl);
                _showCrashPicker = false;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void AutoAddCrashFromTemplate(string sourceLevelPath)
    {
        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        if (chunkSource?.Rm2 is null) { _browser.Log("New Scene: can't auto-add Crash -- no chunk loaded."); return; }
        if (HasCrashInstance(chunkSource.Rm2)) { _browser.Log("New Scene: Crash already present -- auto-add skipped."); return; }
        AddFullCrashTransplant(sourceLevelPath, System.Numerics.Vector3.Zero);
        if (HasCrashInstance(chunkSource.Rm2))
        {
            SaveChunk();
            _browser.Log("New Scene: auto-transplanted Crash from beach at the origin and saved. The scene is ready to build/edit.");
        }
    }

    private static bool HasCrashInstance(PS2AnyTwinsanityRM2 rm2)
    {
        for (uint lid = (uint)TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= (uint)TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var inst = rm2.GetItem<BaseTwinSection>(lid)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
            if (inst is null) continue;
            for (int i = 0; i < inst.GetItemsAmount(); i++)
                if (inst.GetItem(i) is PS2AnyInstance pi && pi.StateFlags == 0x7D2E) return true;
        }
        return false;
    }

    private void AddFullCrashTransplant(string sourceLevelPath, System.Numerics.Vector3? spawnOverride = null)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Crash: no level currently loaded."); return; }

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

            var rec = MeshDecoder.TransplantObjectFull(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                chunkSource.TexCache, sourceRm2, 0x0000, out string log);
            if (rec is null) { _browser.Log($"Add Crash: {log}"); return; }
            chunkSource.FullTransplants[rec.RootObjectId] = rec;

            var section = AllEntities(chunkRoot).Select(e => e.Get<InstanceData>()?.Section).FirstOrDefault(s => s is not null);
            if (section is null)
            {
                var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_LAYOUT_1_SECTION);
                section = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (section is null) { _browser.Log("Add Crash: this level has no instances section to add to."); return; }
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
                    if (cand.ObjectId == 0x0000) { srcInst = cand; break; }
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

            var spawnPos = spawnOverride ?? _camera?.Transform.Position ?? Vector3.Zero;
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
                _browser.Log($"Add Crash: source instance also references {srcInst.Instances.Count} " +
                              "other placed Instance id(s) — these are NOT copied/remapped (left empty); " +
                              "each would itself need its own full object transplant, out of scope here.");
            if (posNotFound > 0)
                _browser.Log($"Add Crash: {posNotFound} of {srcPositions.Count} referenced Position(s) not found in the source level — skipped.");
            if (pathNotFound > 0)
                _browser.Log($"Add Crash: {pathNotFound} of {srcPaths.Count} referenced Path(s) not found in the source level — skipped.");
            if (copiedPositions.Count > 0 || copiedPaths.Count > 0)
                _browser.Log($"Add Crash: copied {copiedPositions.Count} Position(s) and {copiedPaths.Count} Path(s) referenced by the source instance.");
            var instRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "Instances") ?? chunkRoot;
            var newEntity = AddOrReuseInstance(section, inst, instRoot, chunkSource);

            SelectClicked(newEntity, false);
            _browser.Log($"Add Crash: {log} Placed at the camera position. Not on the Undo stack yet, but " +
                          "Delete Selected now fully removes it (object graph + OGIs/Animations/Behaviours/" +
                          "Sounds/graphics) once its last instance is gone — nothing orphaned. Save Chunk " +
                          "to keep it, then Build ISO + test.");

            const uint TextMasterId = 0x0332;
            var srcObjSecForTm = sourceRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
            if (srcObjSecForTm is not null && srcObjSecForTm.ContainsItem(TextMasterId) &&
                !chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)!
                    .GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION)!.ContainsItem(TextMasterId))
            {
                var tmRec = MeshDecoder.TransplantObjectFull(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                    chunkSource.TexCache, sourceRm2, TextMasterId, out string tmLog);
                if (tmRec is not null)
                {
                    chunkSource.FullTransplants[tmRec.RootObjectId] = tmRec;
                    PS2AnyInstance? tmSrcInst = null;
                    for (int lid = 0; lid <= 7 && tmSrcInst is null; lid++)
                    {
                        var srcLayout = sourceRm2.GetItem<BaseTwinSection>((uint)lid);
                        var srcInstSec = srcLayout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                        if (srcInstSec is null) continue;
                        for (int i = 0; i < srcInstSec.GetItemsAmount(); i++)
                        {
                            var cand = (PS2AnyInstance)srcInstSec.GetItem(i);
                            if (cand.ObjectId == TextMasterId) { tmSrcInst = cand; break; }
                        }
                    }
                    var tmInst = new PS2AnyInstance
                    {
                        Position              = tmSrcInst?.Position ?? new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f),
                        RotationX             = new TwinIntegerRotation(),
                        RotationY             = new TwinIntegerRotation(),
                        RotationZ             = new TwinIntegerRotation(),
                        ObjectId              = (ushort)tmRec.RootObjectId,
                        RefListIndex          = tmSrcInst?.RefListIndex ?? -1,
                        OnSpawnHeaderScriptID = tmSrcInst?.OnSpawnHeaderScriptID ?? 0xFFFF,
                        StateFlags            = tmSrcInst?.StateFlags ?? 0,
                        InstancesRelated      = 0, Instances = new List<ushort>(),
                        PositionsRelated      = 0, Positions = new List<ushort>(),
                        PathsRelated          = 0, Paths     = new List<ushort>(),
                        ParamList1            = tmSrcInst is not null ? new List<uint>(tmSrcInst.ParamList1) : new List<uint>(),
                        ParamList2            = tmSrcInst is not null ? new List<float>(tmSrcInst.ParamList2) : new List<float>(),
                        ParamList3            = tmSrcInst is not null ? new List<uint>(tmSrcInst.ParamList3) : new List<uint>(),
                    };
                    var tmEntity = AddOrReuseInstance(section, tmInst, instRoot, chunkSource);
                    _browser.Log($"Add Crash: also added act_UTIL_TEXTMASTER ({tmLog})");
                }
                else _browser.Log($"Add Crash: act_UTIL_TEXTMASTER transplant failed: {tmLog}");
            }
        }
        catch (Exception ex) { _browser.Log($"Add Crash failed: {ex.Message}"); }
    }
}
