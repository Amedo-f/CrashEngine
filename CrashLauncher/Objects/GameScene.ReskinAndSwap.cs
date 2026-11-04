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

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private string                   _swapObjectFilter = "";
    private string                   _swapLevelPath = "";
    private bool                     _swapObjectFull;
    private List<string>?            _swapLevelList;
    private CrashProject?            _gameProject;
    private bool                     _gameProjectLoaded;
    private readonly Dictionary<string, PS2AnyTwinsanityRM2> _foreignRm2Cache = new();
    private string                   _animSwapFilter = "";
    private string                   _swapSkinFilter = "";
    private string                   _swapSkinLevelPath = "";
    private readonly Dictionary<uint, (ushort OriginalOgiId, ushort CloneOgiId, uint SourceOgiId)> _reskinCloneOgi = new();
    private readonly Dictionary<uint, Dictionary<int, (int TargetIdx, int PairNumber)>> _reskinJointMaps = new();
    private Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2? _pristineRm2ForRecovery;
    private bool _pristineRm2RecoveryFailed;

    private bool TryRecoverReskinInfo(uint objectId, object tablesHandle)
    {
        if (_reskinCloneOgi.ContainsKey(objectId)) return true;
        var liveOgiSlots = MeshDecoder.GetObjectOgiSlots(tablesHandle, objectId);
        if (liveOgiSlots is null) return false;

        if (_pristineRm2ForRecovery is null && !_pristineRm2RecoveryFailed)
        {
            try
            {
                using var pkg = PackageReader.Open(ResolvePristinePackageSource());
                using var stream = pkg.OpenByPath(_rm2) ?? throw new FileNotFoundException(_rm2);
                using var reader = new BinaryReader(stream);
                var fresh = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
                fresh.Read(reader, (int)stream.Length);
                _pristineRm2ForRecovery = fresh;
            }
            catch (Exception ex)
            {
                _pristineRm2RecoveryFailed = true;
                _browser.Log($"Reskin recovery: couldn't read pristine chunk data: {ex.Message}");
            }
        }
        if (_pristineRm2ForRecovery is null) return false;

        var code   = _pristineRm2ForRecovery.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION);
        var objSec = code?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
        var pristineObj = objSec?.GetItem<PS2AnyObject>(objectId);
        if (pristineObj is null) return false;

        for (int i = 0; i < liveOgiSlots.Count && i < pristineObj.OGISlots.Count; i++)
        {
            if (liveOgiSlots[i] == pristineObj.OGISlots[i]) continue;
            _reskinCloneOgi[objectId] = (pristineObj.OGISlots[i], liveOgiSlots[i], 0);
            return true;
        }
        return false;
    }

    private void DrawSwapObjectAndCharacterSection(Entity e, InstanceData inst)
    {
        ImGui.Separator();
        if (ImGui.Button("Add Behaviour From Object...##addbeh", new Vector2(-1f, 0f)))
            OpenAddBehaviourFromObject(inst.ObjectId);
        if (ImGui.IsItemHovered())
            MaybeTooltip("Copy selected behaviours from another object (e.g. an enemy) onto THIS\n" +
                         "object — pick a source level, a source object, then tick which behaviours.\n" +
                         "EXPERIMENTAL: a behaviour that drives its source object's own animations/\n" +
                         "sounds may not work here (static/different object). Save Chunk + Build ISO\n" +
                         "to test; Reload Level from Disc to revert if it breaks.");

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Swap Object##swap"))
        {
            var swapChunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
            var swapChunkSrc  = swapChunkRoot?.Get<ChunkSource>();
            if (swapChunkSrc?.MeshTables is null)
            {
                ImGui.TextDisabled("(chunk not loaded)");
            }
            else
            {
                ImGui.TextDisabled("Source Level:");
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.BeginCombo("##swapLevel", string.IsNullOrEmpty(_swapLevelPath) ? "(this level)" : _swapLevelPath))
                {
                    if (ImGui.Selectable("(this level)", string.IsNullOrEmpty(_swapLevelPath)))
                        _swapLevelPath = "";
                    foreach (var lvl in GetSwapLevelList())
                        if (ImGui.Selectable(lvl, lvl == _swapLevelPath))
                            _swapLevelPath = lvl;
                    ImGui.EndCombo();
                }

                var foreignRm2 = string.IsNullOrEmpty(_swapLevelPath) ? null : GetForeignRm2(_swapLevelPath);
                var catalog = foreignRm2 is not null
                    ? MeshDecoder.GetObjectCatalogFromRaw(foreignRm2)
                    : MeshDecoder.GetObjectCatalog(swapChunkSrc.MeshTables);

                ImGui.Checkbox("Full swap (bring scripts, AI, physics too)##swapfull", ref _swapObjectFull);
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Off (default): only the mesh/texture changes, same as before —\n" +
                                      "fine for a crate becoming a Wumpa fruit, etc.\n" +
                                      "On: also brings the target's full script/behaviour graph AND\n" +
                                      "its own native physics/jump-power/spawn tuning — turn this on\n" +
                                      "when swapping to another PLAYABLE character (Nina, Cortex...),\n" +
                                      "not just a different-looking prop.");

                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint("##swapFilter", "Search objects...", ref _swapObjectFilter, 128);
                ImGui.BeginChild("##swapList", new Vector2(-1f, 160f), ImGuiChildFlags.Border);
                foreach (var (id, name) in catalog)
                {
                    if (!string.IsNullOrWhiteSpace(_swapObjectFilter) &&
                        !name.Contains(_swapObjectFilter, StringComparison.OrdinalIgnoreCase) &&
                        !$"{id:X4}".Contains(_swapObjectFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    bool isCurrent = foreignRm2 is null && id == inst.ObjectId;
                    if (ImGui.Selectable($"{name}  (0x{id:X4})##swap{id:X4}", isCurrent))
                        SwapInstanceObject(e, inst, id, foreignRm2, _swapObjectFull);
                }
                ImGui.EndChild();
            }
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Swap Character (keep animations)##swapskin"))
        {
            var skinChunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
            var skinChunkSrc  = skinChunkRoot?.Get<ChunkSource>();
            if (skinChunkSrc?.MeshTables is null)
            {
                ImGui.TextDisabled("(chunk not loaded)");
            }
            else
            {
                ImGui.TextDisabled("Picks a different object's mesh/texture — this");
                ImGui.TextDisabled("object's own animations keep playing unchanged.");

                TryRecoverReskinInfo(inst.ObjectId, skinChunkSrc.MeshTables);
                if (_reskinCloneOgi.TryGetValue(inst.ObjectId, out var reskinInfo))
                {
                    if (ImGui.Button("Reset to Original##resetskin", new Vector2(-1f, 0f)))
                        ResetCharacterSkin(e, inst);
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Puts this object's ORIGINAL mesh/texture back —\n" +
                                          "picking this object as its own \"Swap Object\" target\n" +
                                          "does NOT do this (that just re-confirms the same\n" +
                                          "ObjectId, which still resolves to the reskin).\n" +
                                          "Keeps the reskin (and any Bone Mapping on it) around\n" +
                                          "unreferenced, so picking the same source again later\n" +
                                          "brings it right back — use \"Discard Reskin\" instead\n" +
                                          "to actually forget it.");

                    if (ImGui.Button("Discard Reskin##discardskin", new Vector2(-1f, 0f)))
                    {
                        ResetCharacterSkin(e, inst);
                        _reskinCloneOgi.Remove(inst.ObjectId);
                        _reskinJointMaps.Remove(inst.ObjectId);
                    }
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Puts the original mesh back AND forgets this reskin\n" +
                                          "entirely (including any Bone Mapping on it) — picking\n" +
                                          "a source afterward always starts a completely fresh\n" +
                                          "clone. Use this when you actually want to switch to a\n" +
                                          "genuinely different character, not just temporarily\n" +
                                          "preview the original.");

                    bool haveSource = reskinInfo.SourceOgiId != 0;
                    if (!haveSource) ImGui.BeginDisabled();
                    if (ImGui.Button("Bone Mapping...##bonemap", new Vector2(-1f, 0f)))
                    {
                        var targetId = inst.ObjectId;
                        var sourceId = reskinInfo.SourceOgiId;
                        var cloneOgi = reskinInfo.CloneOgiId;
                        if (!_reskinJointMaps.TryGetValue(targetId, out var jointMap))
                            _reskinJointMaps[targetId] = jointMap = new();
                        Engine.Instance.ActiveScene = new BoneMappingScene(
                            skinChunkSrc.Rm2, skinChunkSrc.MeshTables, skinChunkSrc.TexCache, targetId, sourceId, cloneOgi, jointMap, this,
                            () =>
                            {
                                MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, skinChunkSrc.MeshTables, e);
                                _browser.Log("Bone Mapping: applied — skin remapped to the target's joints.");
                            });
                    }
                    if (!haveSource)
                    {
                        ImGui.EndDisabled();
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("This reskin was recovered from an earlier session —\n" +
                                              "the original source object/level is no longer known,\n" +
                                              "so Bone Mapping has no source skeleton to compare\n" +
                                              "against. Re-pick a source below to re-enable it.");
                    }
                    else if (ImGui.IsItemHovered())
                        MaybeTooltip("Fixes deformed limbs when the reskin source has a\n" +
                                          "different bone layout — manually map each source\n" +
                                          "bone to the matching target bone in a 3D preview.");
                }

                ImGui.TextDisabled("Source Level:");
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.BeginCombo("##swapSkinLevel", string.IsNullOrEmpty(_swapSkinLevelPath) ? "(this level)" : _swapSkinLevelPath))
                {
                    if (ImGui.Selectable("(this level)", string.IsNullOrEmpty(_swapSkinLevelPath)))
                        _swapSkinLevelPath = "";
                    foreach (var lvl in GetSwapLevelList())
                        if (ImGui.Selectable(lvl, lvl == _swapSkinLevelPath))
                            _swapSkinLevelPath = lvl;
                    ImGui.EndCombo();
                }

                var skinForeignRm2 = string.IsNullOrEmpty(_swapSkinLevelPath) ? null : GetForeignRm2(_swapSkinLevelPath);
                var skinCatalog = skinForeignRm2 is not null
                    ? MeshDecoder.GetReskinnableObjectCatalogFromRaw(skinForeignRm2)
                    : MeshDecoder.GetReskinnableObjectCatalog(skinChunkSrc.MeshTables);

                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint("##skinFilter", "Search objects...", ref _swapSkinFilter, 128);
                ImGui.BeginChild("##skinList", new Vector2(-1f, 160f), ImGuiChildFlags.Border);
                foreach (var (id, name) in skinCatalog)
                {
                    if (!string.IsNullOrWhiteSpace(_swapSkinFilter) &&
                        !name.Contains(_swapSkinFilter, StringComparison.OrdinalIgnoreCase) &&
                        !$"{id:X4}".Contains(_swapSkinFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    bool isCurrentSkin = skinForeignRm2 is null && id == inst.ObjectId;
                    if (ImGui.Selectable($"{name}  (0x{id:X4})##skin{id:X4}", isCurrentSkin))
                        SwapCharacterSkin(e, inst, id, skinForeignRm2);
                }
                ImGui.EndChild();
            }
        }
    }

    private void SwapInstanceObject(Entity e, InstanceData data, uint newObjectId,
        PS2AnyTwinsanityRM2? sourceRm2 = null, bool full = false)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.MeshTables is null) return;

        string oldName      = e.Name;
        uint   finalObjectId = newObjectId;

        if (sourceRm2 is not null)
        {
            if (full)
            {
                var fullRec = MeshDecoder.TransplantObjectFull(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                    chunkSource.TexCache, sourceRm2, newObjectId, out string fullLog);
                if (fullRec is null)
                {
                    _browser.Log($"Swap Object (full, cross-level) failed: {fullLog}");
                    return;
                }
                finalObjectId = fullRec.RootObjectId;
                chunkSource.FullTransplants[fullRec.RootObjectId] = fullRec;
                _browser.Log(fullLog);
            }
            else
            {
                var result = MeshDecoder.TransplantObject(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                    chunkSource.TexCache, sourceRm2, newObjectId, out string transplantLog);
                if (result is null)
                {
                    _browser.Log($"Swap Object (cross-level) failed: {transplantLog}");
                    return;
                }
                finalObjectId = result.ObjectId;
                chunkSource.Transplants[result.ObjectId] = result;
                _browser.Log(transplantLog);
            }
        }

        if (full)
        {
            var searchRm2 = sourceRm2 ?? chunkSource.Rm2;
            PS2AnyInstance? srcInst = null;
            for (int lid = 0; lid <= 7 && srcInst is null; lid++)
            {
                var srcLayout  = searchRm2.GetItem<BaseTwinSection>((uint)lid);
                var srcInstSec = srcLayout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (srcInstSec is null) continue;
                for (int i = 0; i < srcInstSec.GetItemsAmount(); i++)
                {
                    var cand = (PS2AnyInstance)srcInstSec.GetItem(i);
                    if (cand.ObjectId == (ushort)newObjectId) { srcInst = cand; break; }
                }
            }

            if (srcInst is null)
            {
                _browser.Log("Swap Object (full): target has no native placed Instance anywhere to copy " +
                              "physics/spawn tuning from — kept this instance's OLD StateFlags/ParamLists, " +
                              "which may not suit the new object.");
            }
            else
            {
                var copiedPositions = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPosition>(
                    searchRm2, chunkSource.Rm2, data.Section, srcInst.Positions,
                    (int)TwinConstants.LAYOUT_POSITIONS_SECTION, out int posNotFound);
                var copiedPaths = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPath>(
                    searchRm2, chunkSource.Rm2, data.Section, srcInst.Paths,
                    (int)TwinConstants.LAYOUT_PATHS_SECTION, out int pathNotFound);
                EnsurePositionMarkersExist(chunkRoot!, chunkSource.Rm2, srcInst.Positions);

                data.Source.StateFlags            = srcInst.StateFlags;
                data.Source.RefListIndex          = srcInst.RefListIndex;
                data.Source.OnSpawnHeaderScriptID = srcInst.OnSpawnHeaderScriptID;
                data.Source.ParamList1            = new List<uint>(srcInst.ParamList1);
                data.Source.ParamList2            = new List<float>(srcInst.ParamList2);
                data.Source.ParamList3            = new List<uint>(srcInst.ParamList3);
                data.Source.PositionsRelated      = srcInst.PositionsRelated;
                data.Source.Positions             = copiedPositions;
                data.Source.PathsRelated          = srcInst.PathsRelated;
                data.Source.Paths                 = copiedPaths;

                data.StateFlags      = srcInst.StateFlags;
                data.OnSpawnScriptId = srcInst.OnSpawnHeaderScriptID;
                data.LinkedPaths     = copiedPaths.Select(p => (uint)p).ToList();

                _browser.Log($"Swap Object (full): copied the target's own physics/spawn tuning " +
                              $"(StateFlags=0x{srcInst.StateFlags:X8}, " +
                              $"{srcInst.ParamList1.Count + srcInst.ParamList2.Count + srcInst.ParamList3.Count} ParamList value(s)) " +
                              $"and {copiedPositions.Count} Position(s)/{copiedPaths.Count} Path(s) from its own native placement.");
            }
        }

        data.Source.ObjectId = (ushort)finalObjectId;
        data.ObjectId        = (ushort)finalObjectId;
        MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, chunkSource.MeshTables, e);
        _browser.Log($"Swapped {oldName} -> {e.Name} (ObjectId 0x{finalObjectId:X4}).");
    }

    private void SwapCharacterSkin(Entity e, InstanceData data, uint sourceObjectId, PS2AnyTwinsanityRM2? sourceRm2 = null)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.MeshTables is null) return;

        uint sourceOgiId;
        if (sourceRm2 is not null)
        {
            var rawOgiId = MeshDecoder.ResolveDefaultOgiIdFromRaw(sourceRm2, sourceObjectId);
            if (rawOgiId is null)
            {
                _browser.Log($"Swap Character (cross-level): ObjectId 0x{sourceObjectId:X4} has no resolvable default OGI in the source level.");
                return;
            }
            var transplant = MeshDecoder.TransplantOgiOnly(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                chunkSource.TexCache, sourceRm2, rawOgiId.Value, out string transplantLog);
            if (transplant is null)
            {
                _browser.Log($"Swap Character (cross-level): {transplantLog}");
                return;
            }
            sourceOgiId = rawOgiId.Value;
            chunkSource.Transplants[data.ObjectId] = transplant;
            _browser.Log(transplantLog);
        }
        else
        {
            var localOgiId = MeshDecoder.ResolveDefaultOgiId(chunkSource.MeshTables, sourceObjectId);
            if (localOgiId is null)
            {
                _browser.Log($"Swap Character: ObjectId 0x{sourceObjectId:X4} has no resolvable default OGI.");
                return;
            }
            sourceOgiId = localOgiId.Value;
        }

        bool ok;
        string log;
        if (_reskinCloneOgi.TryGetValue(data.ObjectId, out var existing))
        {
            bool assumeSame = existing.SourceOgiId == 0 || sourceOgiId == existing.SourceOgiId;
            ok = assumeSame
                ? MeshDecoder.RedoReskin(chunkSource.MeshTables, data.ObjectId, existing.OriginalOgiId, existing.CloneOgiId, out log)
                : MeshDecoder.UpdateReskinClone(chunkSource.MeshTables, existing.CloneOgiId, sourceOgiId, out log);
            if (ok) _reskinCloneOgi[data.ObjectId] = (existing.OriginalOgiId, existing.CloneOgiId, sourceOgiId);
        }
        else
        {
            ok = MeshDecoder.SwapCharacterSkin(chunkSource.Rm2, chunkSource.MeshTables, data.ObjectId, sourceOgiId,
                out ushort originalOgiId, out ushort newCloneId, out log);
            if (ok) _reskinCloneOgi[data.ObjectId] = (originalOgiId, newCloneId, sourceOgiId);
        }

        if (!ok)
        {
            _browser.Log($"Swap Character: {log}");
            return;
        }
        MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, chunkSource.MeshTables, e);
        _browser.Log($"Swap Character: {log} Animations unchanged. Skeleton compatibility isn't " +
                      "checked, verify visually. Not undoable. Save Chunk + Build ISO, then test in an emulator.");
    }

    private void ResetCharacterSkin(Entity e, InstanceData data)
    {
        if (!_reskinCloneOgi.TryGetValue(data.ObjectId, out var existing)) return;
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.MeshTables is null) return;

        if (!MeshDecoder.ResetCharacterSkin(chunkSource.MeshTables, data.ObjectId, existing.OriginalOgiId, existing.CloneOgiId, out string log))
        {
            _browser.Log($"Swap Character: {log}");
            return;
        }
        MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, chunkSource.MeshTables, e);
        _browser.Log($"Swap Character: {log}");
    }
}
