using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashEngine.Importer;

// Amedo
public static partial class MeshDecoder
{
    public const ushort UniqueObjectIdFloor = 0x8000;
    public const ushort UniqueBehaviourIdFloor = 0x4000;
    private const ushort UniqueBehaviourIdCeiling = 0x7FFE;

    // Amedo
    private static BaseTwinSection? CodeSection(PS2AnyTwinsanityRM2? rm2, int id) =>
        rm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)?.GetItem<BaseTwinSection>((uint)id);

    // Amedo
    private static BaseTwinSection? GraphicsSection(PS2AnyTwinsanityRM2? rm2, int id) =>
        rm2?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION)?.GetItem<BaseTwinSection>((uint)id);

    // Amedo
    public static bool IsUniqueObject(PS2AnyTwinsanityRM2 rm2, uint objectId) =>
        objectId >= UniqueObjectIdFloor && CodeSection(rm2, Constants.CODE_GAME_OBJECTS_SECTION)?.ContainsItem(objectId) == true;

    // Amedo
    public static ushort? MakeObjectUnique(PS2AnyTwinsanityRM2 rm2, object tablesHandle, uint objectId, out string log)
    {
        log = "";
        if (tablesHandle is not RmTables tables) { log = "tables handle invalid"; return null; }
        var objSec = CodeSection(rm2, Constants.CODE_GAME_OBJECTS_SECTION);
        if (objSec is null) { log = "level has no GameObjects section"; return null; }
        if (!tables.Objects.TryGetValue(objectId, out var source)) { log = $"ObjectId 0x{objectId:X4} not found"; return null; }

        var clone = CloneItem(source);
        var globalObjSec = CodeSection(tables.GlobalSource, Constants.CODE_GAME_OBJECTS_SECTION);
        ushort id;
        do id = NewCodeLocalId(objSec); while (tables.Objects.ContainsKey(id) || globalObjSec?.ContainsItem(id) == true);
        clone.SetID(id);
        objSec.AddItem(clone);
        tables.Objects[id] = clone;
        log = $"'{source.Name}' 0x{objectId:X4} -> own object 0x{id:X4} (scripts, model and textures still shared until made own).";
        return id;
    }

    // Amedo
    private static BaseTwinItem? FindBehaviourItem(BaseTwinSection? local, BaseTwinSection? global, uint id) =>
        local?.ContainsItem(id) == true ? local.GetItem<BaseTwinItem>(id)
        : global?.ContainsItem(id) == true ? global.GetItem<BaseTwinItem>(id) : null;

    // Amedo
    private static uint? NormalizeBehaviourId(BaseTwinSection? local, BaseTwinSection? global, uint id)
    {
        if (id == 0xFFFF) return null;
        if (FindBehaviourItem(local, global, id) is not null) return id;
        uint low = id & 0x7FFF;
        return FindBehaviourItem(local, global, low) is not null ? low : null;
    }

    // Amedo
    private static uint AssignerGraphId(BaseTwinSection? local, BaseTwinSection? global, int behaviour) =>
        behaviour > 0 && FindBehaviourItem(local, global, (uint)(behaviour - 1)) is PS2BehaviourGraph ? (uint)(behaviour - 1) : (uint)behaviour;

    // Amedo
    public static List<uint> ObjectBehaviourClosure(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, PS2AnyObject obj)
    {
        var local = CodeSection(rm2, Constants.CODE_BEHAVIOURS_SECTION);
        var global = CodeSection(globalRm2, Constants.CODE_BEHAVIOURS_SECTION);
        var seen = new HashSet<uint>();
        var order = new List<uint>();
        var queue = new Queue<uint>();
        void Push(uint raw)
        {
            if (NormalizeBehaviourId(local, global, raw) is { } id && seen.Add(id)) { queue.Enqueue(id); order.Add(id); }
        }
        foreach (var b in obj.BehaviourSlots) Push(b);
        foreach (var b in obj.RefBehaviours) Push(b);
        foreach (var t in obj.TriggerBehaviours) Push(t.TriggerBehaviour);
        while (queue.Count > 0)
        {
            switch (FindBehaviourItem(local, global, queue.Dequeue()))
            {
                case TwinBehaviourStarter starter:
                    foreach (var a in starter.Assigners)
                        if (a.Behaviour > 0) Push(AssignerGraphId(local, global, a.Behaviour));
                    break;
                case PS2BehaviourGraph graph:
                    foreach (var state in graph.ScriptStates)
                        if (state.BehaviourIndexOrSlot != -1 && !state.UsesObjectSlot) Push((ushort)state.BehaviourIndexOrSlot);
                    break;
            }
        }
        return order;
    }

    // Amedo
    public static bool OwnsScripts(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, PS2AnyObject obj)
    {
        var local = CodeSection(rm2, Constants.CODE_BEHAVIOURS_SECTION);
        return ObjectBehaviourClosure(rm2, globalRm2, obj).All(id => id >= UniqueBehaviourIdFloor && local?.ContainsItem(id) == true);
    }

    // Amedo
    public static int OwnObjectScripts(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, uint objectId,
        out Dictionary<uint, uint> map, out string log)
    {
        map = new Dictionary<uint, uint>();
        var obj = CodeSection(rm2, Constants.CODE_GAME_OBJECTS_SECTION) is { } objSec && objSec.ContainsItem(objectId)
            ? objSec.GetItem<PS2AnyObject>(objectId) : null;
        var local = CodeSection(rm2, Constants.CODE_BEHAVIOURS_SECTION);
        var global = CodeSection(globalRm2, Constants.CODE_BEHAVIOURS_SECTION);
        if (obj is null || local is null) { log = "make the object unique first (it must live in this level)"; return 0; }

        var closure = ObjectBehaviourClosure(rm2, globalRm2, obj);
        var rng = IdRandom; // Amedo 2026-11-05
        foreach (var oldId in closure)
        {
            if (oldId >= UniqueBehaviourIdFloor && local.ContainsItem(oldId)) continue;
            uint newId;
            do newId = (uint)rng.Next(UniqueBehaviourIdFloor / 2, UniqueBehaviourIdCeiling / 2 + 1) * 2 + (oldId & 1);
            while (newId > UniqueBehaviourIdCeiling || local.ContainsItem(newId) || global?.ContainsItem(newId) == true || map.ContainsValue(newId));
            map[oldId] = newId;
        }
        if (map.Count == 0) { log = $"'{obj.Name}' already owns all {closure.Count} of its scripts."; return 0; }

        var clones = new List<BaseTwinItem>();
        foreach (var (oldId, newId) in map)
        {
            var clone = CloneItemDynamic(FindBehaviourItem(local, global, oldId)!);
            clone.SetID(newId);
            clones.Add(clone);
        }

        var ids = map;
        ushort Remap(ushort raw) => RemapBehaviourId(ids, raw);
        foreach (var item in clones.Concat(closure.Where(id => !ids.ContainsKey(id)).Select(id => FindBehaviourItem(local, global, id)).OfType<BaseTwinItem>()
                                               .Where(i => i.GetID() >= UniqueBehaviourIdFloor && local.ContainsItem(i.GetID()))))
            switch (item)
            {
                case TwinBehaviourStarter starter:
                    foreach (var a in starter.Assigners)
                    {
                        if (a.Behaviour <= 0) continue;
                        uint graphId = AssignerGraphId(local, global, a.Behaviour);
                        if (map.TryGetValue(graphId, out var n)) a.Behaviour = (int)n + (a.Behaviour - (int)graphId);
                    }
                    break;
                case PS2BehaviourGraph graph:
                    foreach (var state in graph.ScriptStates)
                        if (state.BehaviourIndexOrSlot != -1 && !state.UsesObjectSlot)
                            state.BehaviourIndexOrSlot = unchecked((short)Remap((ushort)state.BehaviourIndexOrSlot));
                    break;
            }
        foreach (var clone in clones) local.AddItem(clone);

        for (int i = 0; i < obj.BehaviourSlots.Count; i++) obj.BehaviourSlots[i] = Remap(obj.BehaviourSlots[i]);
        for (int i = 0; i < obj.RefBehaviours.Count; i++) obj.RefBehaviours[i] = Remap(obj.RefBehaviours[i]);
        foreach (var t in obj.TriggerBehaviours) t.TriggerBehaviour = Remap(t.TriggerBehaviour);

        log = $"'{obj.Name}' 0x{objectId:X4} now owns {map.Count} script(s): " +
              string.Join(", ", map.Select(kv => $"0x{kv.Key:X}->0x{kv.Value:X}"));
        return map.Count;
    }

    // Amedo
    public static ushort RemapBehaviourId(IReadOnlyDictionary<uint, uint> map, ushort raw)
    {
        if (map.TryGetValue(raw, out var n)) return (ushort)n;
        if (raw != 0xFFFF && map.TryGetValue((uint)raw & 0x7FFF, out n)) return (ushort)((raw & 0x8000) | n);
        return raw;
    }

    // Amedo
    public static PS2BehaviourGraph? ObjectBehaviourNamed(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, PS2AnyObject obj, string name)
    {
        var local = CodeSection(rm2, Constants.CODE_BEHAVIOURS_SECTION);
        var global = CodeSection(globalRm2, Constants.CODE_BEHAVIOURS_SECTION);
        foreach (var id in ObjectBehaviourClosure(rm2, globalRm2, obj))
            if (FindBehaviourItem(local, global, id) is PS2BehaviourGraph g && g.Name == name) return g;
        return null;
    }

    // Amedo
    public static bool OwnsLook(PS2AnyTwinsanityRM2 rm2, object tablesHandle, PS2AnyObject obj)
    {
        if (tablesHandle is not RmTables tables) return false;
        var mine = ObjectOgis(obj).ToHashSet();
        if (mine.Count == 0) return true;
        var ogiSec = CodeSection(rm2, Constants.CODE_OGIS_SECTION);
        if (mine.Any(id => id < UniqueObjectIdFloor || ogiSec?.ContainsItem(id) != true)) return false;
        var myMaterials = new HashSet<uint>();
        var theirMaterials = new HashSet<uint>();
        foreach (var (ogiId, ogi) in tables.OGIs)
            foreach (var m in OgiMaterials(tables, rm2, ogi))
                (mine.Contains((ushort)ogiId) ? myMaterials : theirMaterials).Add(m);
        return !myMaterials.Overlaps(theirMaterials);
    }

    // Amedo
    private static IEnumerable<ushort> ObjectOgis(PS2AnyObject obj) =>
        obj.OGISlots.Where(id => id != 0xFFFF).Concat(obj.RefOGIs).Distinct();

    // Amedo
    private static IEnumerable<uint> OgiMaterials(RmTables tables, PS2AnyTwinsanityRM2 rm2, PS2AnyOGI ogi)
    {
        foreach (var rmId in ogi.RigidModelIds)
            if (tables.RigidModels.TryGetValue(rmId, out var rm))
                foreach (var m in rm.Materials) yield return m;
        foreach (var rm2Src in new[] { rm2, tables.GlobalSource })
        {
            if (ogi.SkinID != 0 && GraphicsSection(rm2Src, Constants.GRAPHICS_SKINS_SECTION) is { } skins && skins.ContainsItem(ogi.SkinID))
                foreach (var ss in skins.GetItem<PS2AnySkin>(ogi.SkinID).SubSkins) yield return ss.Material;
            if (ogi.BlendSkinID != 0 && GraphicsSection(rm2Src, Constants.GRAPHICS_BLEND_SKINS_SECTION) is { } blends && blends.ContainsItem(ogi.BlendSkinID))
                foreach (var sb in blends.GetItem<PS2AnyBlendSkin>(ogi.BlendSkinID).SubBlends) yield return sb.Material;
        }
    }

    // Amedo
    public static int OwnObjectLook(GL gl, PS2AnyTwinsanityRM2 rm2, object tablesHandle, Dictionary<uint, Texture2D> texCache,
        uint objectId, out string log)
    {
        log = "";
        if (tablesHandle is not RmTables tables) { log = "tables handle invalid"; return 0; }
        var obj = CodeSection(rm2, Constants.CODE_GAME_OBJECTS_SECTION) is { } objSec && objSec.ContainsItem(objectId)
            ? objSec.GetItem<PS2AnyObject>(objectId) : null;
        if (obj is null) { log = "make the object unique first (it must live in this level)"; return 0; }

        var glob = tables.GlobalSource;
        var ogiSec = CodeSection(rm2, Constants.CODE_OGIS_SECTION);
        var gfx = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var rmSec = GraphicsSection(rm2, Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var meshSec = GraphicsSection(rm2, Constants.GRAPHICS_MESHES_SECTION);
        var modelSec = GraphicsSection(rm2, Constants.GRAPHICS_MODELS_SECTION);
        var matSec = GraphicsSection(rm2, Constants.GRAPHICS_MATERIALS_SECTION);
        var skinSec = GraphicsSection(rm2, Constants.GRAPHICS_SKINS_SECTION);
        var blendSec = GraphicsSection(rm2, Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var texSec = GraphicsSection(rm2, Constants.GRAPHICS_TEXTURES_SECTION);
        if (gfx is null || ogiSec is null || rmSec is null || modelSec is null || matSec is null || skinSec is null || blendSec is null || texSec is null)
        { log = "level is missing a required CODE/GRAPHICS section"; return 0; }

        T? Find<T>(int sectionId, uint id) where T : BaseTwinItem
        {
            foreach (var src in new[] { rm2, glob })
                if (GraphicsSection(src, sectionId) is { } s && s.ContainsItem(id)) return s.GetItem<T>(id);
            return null;
        }
        uint NewId(int sectionId)
        {
            var local = GraphicsSection(rm2, sectionId)!;
            var global = GraphicsSection(glob, sectionId);
            uint id;
            do id = NewGraphicsLocalId(local, global, sectionId == Constants.GRAPHICS_RIGID_MODELS_SECTION ? meshSec : null);
            while (sectionId == Constants.GRAPHICS_TEXTURES_SECTION && texCache.ContainsKey(id));
            return id;
        }

        int copies = 0;
        var texMap = new Dictionary<uint, uint>();
        var matMap = new Dictionary<uint, uint>();
        uint OwnTexture(uint texId)
        {
            if (texMap.TryGetValue(texId, out var done)) return done;
            if (Find<PS2AnyTexture>(Constants.GRAPHICS_TEXTURES_SECTION, texId) is not { } src) return texMap[texId] = texId;
            var clone = CloneItem(src);
            uint id = NewId(Constants.GRAPHICS_TEXTURES_SECTION);
            clone.SetID(id);
            texSec.AddItem(clone);
            if (ChunkImporter.DecodeTexture(gl, clone) is { } t) texCache[id] = t;
            copies++;
            return texMap[texId] = id;
        }
        uint OwnMaterial(uint matId)
        {
            if (matMap.TryGetValue(matId, out var done)) return done;
            if (Find<PS2AnyMaterial>(Constants.GRAPHICS_MATERIALS_SECTION, matId) is not { } src) return matMap[matId] = matId;
            var clone = CloneItem(src);
            uint id = NewId(Constants.GRAPHICS_MATERIALS_SECTION);
            clone.SetID(id);
            foreach (var sh in clone.Shaders) sh.TextureId = OwnTexture(sh.TextureId);
            matSec.AddItem(clone);
            copies++;
            return matMap[matId] = id;
        }

        var ogiMap = new Dictionary<ushort, ushort>();
        var rmMap = new Dictionary<uint, uint>();
        foreach (var oldOgi in ObjectOgis(obj).ToList())
        {
            if (!tables.OGIs.TryGetValue(oldOgi, out var srcOgi)) continue;
            var ogi = CloneItem(srcOgi);
            ushort ogiId;
            do ogiId = NewCodeLocalId(ogiSec); while (tables.OGIs.ContainsKey(ogiId));
            ogi.SetID(ogiId);

            for (int r = 0; r < ogi.RigidModelIds.Count; r++)
            {
                uint oldRm = ogi.RigidModelIds[r];
                if (rmMap.TryGetValue(oldRm, out var doneRm)) { ogi.RigidModelIds[r] = doneRm; continue; }
                if (!tables.RigidModels.TryGetValue(oldRm, out var srcRm)) continue;
                var rmClone = CloneItem(srcRm);
                uint rmId = NewId(Constants.GRAPHICS_RIGID_MODELS_SECTION);
                rmClone.SetID(rmId);
                if (tables.Models.TryGetValue(srcRm.Model, out var srcModelItem) && srcModelItem is PS2AnyModel srcModel)
                {
                    var model = CloneItem(srcModel);
                    uint modelId = NewId(Constants.GRAPHICS_MODELS_SECTION);
                    model.SetID(modelId);
                    modelSec.AddItem(model);
                    tables.Models[modelId] = model;
                    rmClone.Model = modelId;
                    copies++;
                }
                for (int m = 0; m < rmClone.Materials.Count; m++) rmClone.Materials[m] = OwnMaterial(rmClone.Materials[m]);
                rmSec.AddItem(rmClone);
                tables.RigidModels[rmId] = rmClone;
                rmMap[oldRm] = rmId;
                ogi.RigidModelIds[r] = rmId;
                copies++;
            }
            if (ogi.SkinID != 0 && Find<PS2AnySkin>(Constants.GRAPHICS_SKINS_SECTION, ogi.SkinID) is { } srcSkin)
            {
                foreach (var ss in srcSkin.SubSkins) ss.CalculateData();
                var skin = CloneItem(srcSkin);
                uint skinId = NewId(Constants.GRAPHICS_SKINS_SECTION);
                skin.SetID(skinId);
                foreach (var ss in skin.SubSkins) ss.Material = OwnMaterial(ss.Material);
                skinSec.AddItem(skin);
                ogi.SkinID = skinId;
                copies++;
            }
            if (ogi.BlendSkinID != 0 && Find<PS2AnyBlendSkin>(Constants.GRAPHICS_BLEND_SKINS_SECTION, ogi.BlendSkinID) is { } srcBlend)
            {
                foreach (var sb in srcBlend.SubBlends)
                    foreach (var model in sb.Models) model.CalculateData();
                var blend = CloneItem(srcBlend);
                uint blendId = NewId(Constants.GRAPHICS_BLEND_SKINS_SECTION);
                blend.SetID(blendId);
                foreach (var sb in blend.SubBlends) sb.Material = OwnMaterial(sb.Material);
                blendSec.AddItem(blend);
                ogi.BlendSkinID = blendId;
                copies++;
            }
            ogiSec.AddItem(ogi);
            tables.OGIs[ogiId] = ogi;
            ogiMap[oldOgi] = ogiId;
            copies++;
        }

        for (int i = 0; i < obj.OGISlots.Count; i++) if (ogiMap.TryGetValue(obj.OGISlots[i], out var n)) obj.OGISlots[i] = n;
        for (int i = 0; i < obj.RefOGIs.Count; i++) if (ogiMap.TryGetValue(obj.RefOGIs[i], out var n)) obj.RefOGIs[i] = n;

        var modelById = BuildModelLookup(gfx);
        foreach (var kv in BuildMeshLookup(gfx, modelById, gl, texCache, glob)) tables.ModelGpu[kv.Key] = kv.Value;
        foreach (var kv in BuildSkinLookup(gfx, gl, texCache)) tables.SkinGpu[kv.Key] = kv.Value;
        foreach (var kv in BuildBlendSkinLookup(gfx, gl, texCache, tables.BlendSkinRaw)) tables.BlendSkinGpu[kv.Key] = kv.Value;

        log = $"'{obj.Name}' 0x{objectId:X4} now owns its look: {ogiMap.Count} OGI, {rmMap.Count} rigid model(s), " +
              $"{matMap.Count(kv => kv.Key != kv.Value)} material(s), {texMap.Count(kv => kv.Key != kv.Value)} texture(s) ({copies} item(s) copied).";
        return copies;
    }
}
