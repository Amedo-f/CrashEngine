using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawInspectorObjectDefinition(Entity e, InstanceData inst)
    {
        var scriptChunkSrc = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var objSecForInsp = scriptChunkSrc?.Rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
        var behSecForInsp = scriptChunkSrc?.Rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
        var localObjForInsp = objSecForInsp?.GetItem<PS2AnyObject>(inst.ObjectId);
        var globalObjSecForInsp = scriptChunkSrc?.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
        var globalObjForInsp = globalObjSecForInsp?.GetItem<PS2AnyObject>(inst.ObjectId);
        var globalBehSecForInsp = scriptChunkSrc?.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);

        var thisObj = globalObjForInsp ?? localObjForInsp;
        bool editingGlobal = globalObjForInsp is not null;
        bool hasCollision = localObjForInsp is not null && globalObjForInsp is not null;

        DrawInspectorSpawnState(inst, thisObj, scriptChunkSrc);

        DrawInspectorObjectTypeAndFlags(thisObj, editingGlobal, hasCollision, localObjForInsp, scriptChunkSrc);

        DrawInspectorObjectGameSettings(e, inst, thisObj, scriptChunkSrc, behSecForInsp);

        DrawInspectorObjectSounds(thisObj, scriptChunkSrc);

        DrawInspectorObjectOgi(e, inst, thisObj);


        DrawInspectorCrateLandingBounce(thisObj, scriptChunkSrc);

        DrawTextSection(e, inst, localObjForInsp ?? globalObjForInsp, behSecForInsp, globalBehSecForInsp); // Amedo 2026-10-01

        var behSecForList = editingGlobal ? globalBehSecForInsp : behSecForInsp;
        if (thisObj is not null && thisObj.TriggerBehaviours.Count > 0)
        {
            if (ImGui.CollapsingHeader($"Messages -> Behaviours ({thisObj.TriggerBehaviours.Count})##objmsgbeh")) // Amedo 2026-09-25
                DrawMessageBehaviours(thisObj);
        }

        if (thisObj is not null && behSecForList is not null && thisObj.RefBehaviours.Count > 0)
        {
            if (ImGui.CollapsingHeader($"Behaviours ({thisObj.RefBehaviours.Count})##objbeh"))
            {
                DrawBehavioursSectionBody(thisObj, behSecForList, editingGlobal, scriptChunkSrc);
            }
        }
    }

    private void DrawInspectorSpawnState(InstanceData inst, PS2AnyObject? thisObj, ChunkSource? scriptChunkSrc)
    {
        const ushort CrateOutlineStarterId = 0x0028;
        ushort rawSpawnScriptId = inst.Source.OnSpawnHeaderScriptID;
        bool spawnStateHandled = false;
        if (thisObj?.Type == Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType.Crate &&
            (rawSpawnScriptId == 0xFFFF || rawSpawnScriptId == CrateOutlineStarterId))
        {
            spawnStateHandled = true;
            bool isOutline = rawSpawnScriptId == CrateOutlineStarterId;
            if (ImGui.Checkbox("Starts as Outline (ghost, waits for a linked switch)##crateoutline", ref isOutline))
            {
                ushort newId = isOutline ? CrateOutlineStarterId : (ushort)0xFFFF;
                inst.Source.OnSpawnHeaderScriptID = newId;
                inst.OnSpawnScriptId = newId;
            }
            if (isOutline)
                ImGui.TextDisabled("Raw Instance Script 0x0028 (COM_GENERIC_CRATE_OUTLINE, GLOBAL —\n" +
                                    "same constant works for any crate, in any level).");
        }
        if (!spawnStateHandled && rawSpawnScriptId != 0xFFFF)
        {
            if (!_spawnScriptInfoCache.TryGetValue(rawSpawnScriptId, out var spawnInfo))
            {
                spawnInfo = scriptChunkSrc is not null
                    ? ScriptDumper.ResolveSpawnScript(scriptChunkSrc.Rm2, scriptChunkSrc.GlobalRm2, rawSpawnScriptId)
                    : null;
                _spawnScriptInfoCache[rawSpawnScriptId] = spawnInfo;
            }

            if (spawnInfo is null)
            {
                ImGui.TextColored(new Vector4(1f, 0.55f, 0.3f, 1f),
                    $"Spawn state: unresolved (raw id 0x{rawSpawnScriptId:X4}) — couldn't find or\n" +
                    "decode the real script this points to. Not necessarily the normal state.");
            }
            else
            {
                ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f), $"Spawn state: {spawnInfo.FriendlyName}");
                ImGui.TextDisabled($"({spawnInfo.GraphName}, id 0x{spawnInfo.ResolvedGraphId:X4} " +
                                   $"{(spawnInfo.IsGlobal ? "GLOBAL" : "LOCAL")}" +
                                   (spawnInfo.ChainDepth > 0 ? $", via {spawnInfo.ChainDepth} Starter hop(s)" : "") +
                                   $" — raw Instance Script 0x{rawSpawnScriptId:X4})");
                if (ImGui.CollapsingHeader("Script##spawnscript"))
                {
                    ImGui.BeginChild("##scriptText", new Vector2(-1f, 200f), ImGuiChildFlags.Border);
                    ImGui.TextUnformatted(spawnInfo.DecompiledText);
                    ImGui.EndChild();
                }
            }
        }
    }

    private void DrawInspectorObjectTypeAndFlags(PS2AnyObject? thisObj, bool editingGlobal, bool hasCollision, PS2AnyObject? localObjForInsp, ChunkSource? scriptChunkSrc)
    {
        if (thisObj is not null)
        {
            if (ImGui.CollapsingHeader("Object Type##objtype"))
            {
                ImGui.TextDisabled(editingGlobal
                    ? "Editing the GLOBAL (Startup\\Default.rm2) object definition."
                    : "Changes apply to EVERY placed instance of this exact object type.");
                var typeValues = Enum.GetValues<Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType>();
                int curIdx = Array.IndexOf(typeValues, thisObj.Type);
                string[] typeNames = typeValues.Select(t => t.ToString()).ToArray();
                if (ImGui.Combo("Type##objtypecombo", ref curIdx, typeNames, typeNames.Length))
                {
                    thisObj.Type = typeValues[curIdx];
                    if (editingGlobal && scriptChunkSrc is not null)
                        scriptChunkSrc.GlobalRm2Dirty = true;
                }
                ImGui.TextDisabled("Save Chunk to persist, then Build ISO + test.");
            }
        }

        if (thisObj is not null && thisObj.HasInstanceProperties)
        {
            if (ImGui.CollapsingHeader("Object Flags (shared by every instance of this type)##objflags"))
            {
                ImGui.TextDisabled(editingGlobal
                    ? "Editing the GLOBAL (Startup\\Default.rm2) object definition —\n" +
                      "affects EVERY level that uses this object type, not just this chunk."
                    : "Changes apply to EVERY placed instance of this exact\n" +
                      "object type in this chunk, not just the one selected.");
                if (hasCollision)
                    ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1f),
                        $"Note: this ObjectId ALSO has a separate LOCAL entry ('{localObjForInsp!.Name}')\n" +
                        "in this chunk — editing GLOBAL here since local edits were confirmed\n" +
                        "to have no in-game effect for this kind of shared object.");
                var flagNames = Enum.GetValues<Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState>();
                foreach (var flag in flagNames)
                {
                    if (flag == 0) continue;
                    bool on = thisObj.InstanceStateFlags.HasFlag(flag);
                    if (ImGui.Checkbox($"{flag}##objflag_{flag}", ref on))
                    {
                        thisObj.InstanceStateFlags = on
                            ? thisObj.InstanceStateFlags | flag
                            : thisObj.InstanceStateFlags & ~flag;
                        if (editingGlobal && scriptChunkSrc is not null)
                            scriptChunkSrc.GlobalRm2Dirty = true;
                    }
                }
                ImGui.TextDisabled("Save Chunk to persist, then Build ISO + test.");
            }
        }
    }

    private void DrawInspectorObjectOgi(Entity e, InstanceData inst, PS2AnyObject? thisObj)
    {
        if (thisObj is not null && thisObj.RefOGIs.Count > 0)
        {
            if (ImGui.CollapsingHeader($"OGI ({thisObj.RefOGIs.Count})##objogihdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var ogiChunkSrc = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
                uint? defaultOgiId = ogiChunkSrc?.MeshTables is not null
                    ? MeshDecoder.ResolveDefaultOgiId(ogiChunkSrc.MeshTables, inst.ObjectId)
                    : null;
                bool previewingThisInst = _ogiPreviewInstance == inst.Source.GetID();
                if (!previewingThisInst)
                    ImGui.TextDisabled("\"(currently shown)\" = what this engine is rendering for\n" +
                                        "this instance right now — its best available signal for\n" +
                                        "what the real game shows, NOT a guaranteed match (see code\n" +
                                        "comment). Preview never changes what's actually saved.");

                foreach (var ogiId in thisObj.RefOGIs)
                {
                    ImGui.PushID((int)ogiId);
                    var meshParts = ogiChunkSrc?.MeshTables is not null
                        ? MeshDecoder.GetObjectMeshPartsByOgi(ogiChunkSrc.MeshTables, ogiId)
                        : null;
                    string tag = ogiId == defaultOgiId ? " (currently shown)" : "";
                    if (meshParts is null || meshParts.Count == 0)
                    {
                        ImGui.TextDisabled($"OGI 0x{ogiId:X4}{tag} — no renderable parts (script-only / manager OGI).");
                    }
                    else
                    {
                        bool thisOneActive = previewingThisInst && _ogiPreviewOgiId == ogiId;
                        ImGui.Text($"OGI 0x{ogiId:X4}{tag} — {meshParts.Count} part(s){(thisOneActive ? "  [PREVIEWING]" : "")}");
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Preview##ogiprev") && ogiChunkSrc?.MeshTables is not null &&
                            MeshDecoder.PreviewOgiOnInstance(Engine.Instance.GL, ogiChunkSrc.MeshTables, e, ogiId))
                        {
                            _ogiPreviewInstance = inst.Source.GetID();
                            _ogiPreviewOgiId = ogiId;
                            _browser.Log($"OGI preview: showing OGI 0x{ogiId:X4} on this instance only (in-memory, not saved).");
                        }
                    }
                    ImGui.PopID();
                }
                if (previewingThisInst)
                {
                    if (ImGui.Button("Reset to Currently-Shown##ogipreviewreset") && ogiChunkSrc?.MeshTables is not null)
                    {
                        MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, ogiChunkSrc.MeshTables, e);
                        _ogiPreviewInstance = null;
                        _ogiPreviewOgiId = null;
                    }
                }
            }
        }
    }
}
