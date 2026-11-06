using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;

namespace CrashEngine.Importer;

public static partial class MeshDecoder
{
    private static T CloneItem<T>(T source) where T : BaseTwinItem, new()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
            source.Write(w);
        var bytes = ms.ToArray();
        var clone = new T();
        using var rs = new MemoryStream(bytes);
        using var r  = new BinaryReader(rs);
        clone.Read(r, bytes.Length);
        clone.SetID(source.GetID());
        return clone;
    }

    private static ushort NewCodeLocalId(BaseTwinSection section)
    {
        var rng = IdRandom; // Amedo 2026-11-05
        ushort id;
        do { id = (ushort)rng.Next(0x8000, 0xFFFF); }
        while (section.ContainsItem(id));
        return id;
    }

    private static uint NewGraphicsLocalId(BaseTwinSection section, params BaseTwinSection?[] alsoAvoid)
    {
        var rng = IdRandom; // Amedo 2026-11-05
        uint id;
        do { id = (uint)rng.Next(0x10000, 0x7FFFFFF); }
        while (id == 0 || section.ContainsItem(id) || alsoAvoid.Any(s => s?.ContainsItem(id) == true));
        return id;
    }

    public static TransplantRecord? TransplantObject(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanityRM2 sourceRm2, uint sourceObjectId, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }

        var src = Rm2Sections.Of(sourceRm2);
        if (src.Objects is null) { log = "source level has no GameObjects section"; return null; }
        var srcObj = src.Objects.GetItem<PS2AnyObject>(sourceObjectId);
        if (srcObj is null) { log = "object not found in source level"; return null; }

        var dest = Rm2Sections.Of(destRm2);
        var destObjSec = dest.Objects;
        var destSndSec = dest.Sounds;
        if (destObjSec is null || dest.Ogis is null || dest.Animations is null || dest.RigidModels is null ||
            dest.Models is null || dest.Materials is null || dest.Skins is null || dest.BlendSkins is null || dest.Textures is null)
        {
            log = "destination chunk is missing a required CODE/GRAPHICS section";
            return null;
        }

        var rec = new TransplantRecord();
        var copy = GraphicsCopy.For(gl, destTables, destTexCache, src, dest, rec);

        bool objectAlreadyPresent = destObjSec.ContainsItem(sourceObjectId);
        var newObj = objectAlreadyPresent ? destObjSec.GetItem<PS2AnyObject>(sourceObjectId)! : CloneItem(srcObj);

        int nulledObjSlots = 0, nulledSndSlots = 0, prunedRefObj = 0, prunedRefSnd = 0;
        if (!objectAlreadyPresent)
        {
            for (int i = 0; i < newObj.OGISlots.Count; i++)
            {
                if (newObj.OGISlots[i] == 0xFFFF) continue;
                if (!copy.Ogi(newObj.OGISlots[i])) newObj.OGISlots[i] = 0xFFFF;
            }
            for (int i = 0; i < newObj.AnimationSlots.Count; i++)
            {
                if (newObj.AnimationSlots[i] == 0xFFFF) continue;
                if (!copy.Animation(newObj.AnimationSlots[i])) newObj.AnimationSlots[i] = 0xFFFF;
            }

            bool ObjPresent(uint id) => id == 0xFFFF || (destObjSec?.ContainsItem(id) ?? false);
            bool SndPresent(uint id) => id == 0xFFFF || (destSndSec?.ContainsItem(id) ?? false);
            for (int i = 0; i < newObj.ObjectSlots.Count; i++)
                if (!ObjPresent(newObj.ObjectSlots[i])) { newObj.ObjectSlots[i] = 0xFFFF; nulledObjSlots++; }
            for (int i = 0; i < newObj.SoundSlots.Count; i++)
                if (!SndPresent(newObj.SoundSlots[i]))  { newObj.SoundSlots[i]  = 0xFFFF; nulledSndSlots++; }
            prunedRefObj = newObj.RefObjects.RemoveAll(id => !ObjPresent(id));
            prunedRefSnd = newObj.RefSounds.RemoveAll(id => !SndPresent(id));

            destObjSec.AddItem(newObj);
        }

        ushort newObjId = (ushort)sourceObjectId;
        destTables.Objects[newObjId] = newObj;
        rec.ObjectId = newObjId;
        rec.ObjectWasNew = !objectAlreadyPresent;

        RebuildTransplantGpu(gl, dest.Gfx!, destTables, destTexCache);

        log = objectAlreadyPresent
            ? $"'{newObj.Name}' (id 0x{newObjId:X4}) already exists in this chunk under the same shared id — reused as-is, nothing duplicated."
            : $"transplanted '{newObj.Name}' (id 0x{newObjId:X4}, preserved from source) — " +
              $"+{rec.OgiIds.Count} OGI, +{rec.AnimIds.Count} Animation, +{rec.RigidModelIds.Count} RigidModel, +{rec.ModelIds.Count} Model, " +
              $"+{rec.SkinIds.Count} Skin, +{rec.BlendSkinIds.Count} BlendSkin, +{rec.MaterialIds.Count} Material, +{rec.TextureIds.Count} Texture. " +
              $"Neutralized dangling refs (load-safety): {nulledObjSlots} ObjectSlot + {nulledSndSlots} SoundSlot nulled, {prunedRefObj} RefObject + {prunedRefSnd} RefSound dropped. Behaviours kept as-is.";
        return rec;
    }

    public static TransplantRecord? TransplantOgiOnly(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanityRM2 sourceRm2, uint sourceOgiId, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }

        var src = Rm2Sections.Of(sourceRm2);
        if (src.Ogis is null) { log = "source level has no OGIs section"; return null; }
        var srcOgi = src.Ogis.GetItem<PS2AnyOGI>(sourceOgiId);
        if (srcOgi is null) { log = "OGI not found in source level"; return null; }

        var dest = Rm2Sections.Of(destRm2);
        if (dest.Ogis is null || dest.RigidModels is null || dest.Models is null || dest.Materials is null ||
            dest.Skins is null || dest.BlendSkins is null || dest.Textures is null)
        { log = "destination chunk is missing a required GRAPHICS/OGI section"; return null; }

        var rec = new TransplantRecord();
        GraphicsCopy.For(gl, destTables, destTexCache, src, dest, rec).Ogi(sourceOgiId);

        RebuildTransplantGpu(gl, dest.Gfx!, destTables, destTexCache);

        log = rec.OgiIds.Count > 0
            ? $"transplanted OGI 0x{sourceOgiId:X4} (visual only — no object/script data brought in) — " +
              $"+{rec.RigidModelIds.Count} RigidModel, +{rec.ModelIds.Count} Model, +{rec.SkinIds.Count} Skin, " +
              $"+{rec.BlendSkinIds.Count} BlendSkin, +{rec.MaterialIds.Count} Material, +{rec.TextureIds.Count} Texture."
            : $"OGI 0x{sourceOgiId:X4} already exists in this chunk under the same shared id — reused as-is, nothing duplicated.";
        return rec;
    }

    public static void RemoveTransplant(PS2AnyTwinsanityRM2 destRm2, object destTablesHandle,
        Dictionary<uint, Texture2D> destTexCache, TransplantRecord rec)
    {
        if (destTablesHandle is not RmTables destTables) return;

        var dest = Rm2Sections.Of(destRm2);

        if (rec.ObjectWasNew)
        {
            dest.Objects?.RemoveItem<PS2AnyObject>(rec.ObjectId);
            destTables.Objects.Remove(rec.ObjectId);
        }

        foreach (var ogiId in rec.OgiIds)
        {
            dest.Ogis?.RemoveItem<PS2AnyOGI>(ogiId);
            destTables.OGIs.Remove(ogiId);
        }
        foreach (var animId in rec.AnimIds)
        {
            dest.Animations?.RemoveItem<PS2AnyAnimation>(animId);
            destTables.Animations.Remove(animId);
        }

        RemoveUnusedTransplantGraphics(destTables, destTexCache, dest,
            rec.RigidModelIds, rec.SkinIds, rec.BlendSkinIds, rec.ModelIds, rec.MaterialIds, rec.TextureIds);
    }

    public sealed class FullTransplantRecord
    {
        public uint RootObjectId;
        public readonly Dictionary<uint, uint> ObjectIdMap = new();
        public readonly List<uint> OgiIds = new();
        public readonly List<uint> AnimIds = new();
        public readonly List<uint> BehaviourIds = new();
        public readonly List<uint> SoundIds = new();
        public readonly List<uint> RigidModelIds = new();
        public readonly List<uint> ModelIds = new();
        public readonly List<uint> SkinIds = new();
        public readonly List<uint> BlendSkinIds = new();
        public readonly List<uint> MaterialIds = new();
        public readonly List<uint> TextureIds = new();
    }

    public static void RemoveFullTransplant(PS2AnyTwinsanityRM2 destRm2, object destTablesHandle,
        Dictionary<uint, Texture2D> destTexCache, FullTransplantRecord rec)
    {
        if (destTablesHandle is not RmTables destTables) return;

        var dest = Rm2Sections.Of(destRm2);

        var removedObjIds = new HashSet<uint>(rec.ObjectIdMap.Values);
        bool ObjStillNeeded(uint id) => destTables.Objects.Values.Any(o =>
            !removedObjIds.Contains(o.GetID()) &&
            (o.RefObjects.Contains((ushort)id) || o.ObjectSlots.Contains((ushort)id)));

        foreach (var newObjId in rec.ObjectIdMap.Values)
        {
            if (newObjId != rec.RootObjectId && ObjStillNeeded(newObjId)) continue;
            dest.Objects?.RemoveItem<PS2AnyObject>(newObjId);
            destTables.Objects.Remove(newObjId);
        }

        var remainingObjects = destTables.Objects.Values.ToList();
        bool OgiStillNeeded(uint id)  => remainingObjects.Any(o => o.OGISlots.Contains((ushort)id) || o.RefOGIs.Contains((ushort)id));
        bool AnimStillNeeded(uint id) => remainingObjects.Any(o => o.AnimationSlots.Contains((ushort)id) || o.RefAnimations.Contains((ushort)id));
        bool SoundStillNeeded(uint id) => remainingObjects.Any(o => o.SoundSlots.Contains((ushort)id) || o.RefSounds.Contains((ushort)id));
        bool BehStillNeeded(uint id)
        {
            if (remainingObjects.Any(o => o.BehaviourSlots.Contains((ushort)id) || o.RefBehaviours.Contains((ushort)id)))
                return true;
            for (int i = 0; i < (dest.Behaviours?.GetItemsAmount() ?? 0); i++)
                if (dest.Behaviours!.GetItem(i) is TwinBehaviourStarter starter &&
                    !rec.BehaviourIds.Contains(starter.GetID()) &&
                    starter.Assigners.Any(a => a.Behaviour == (int)id))
                    return true;
            return false;
        }

        foreach (var id in rec.BehaviourIds) if (!BehStillNeeded(id))   dest.Behaviours?.RemoveItem<BaseTwinItem>(id);
        foreach (var id in rec.SoundIds)     if (!SoundStillNeeded(id)) dest.Sounds?.RemoveItem<PS2AnySound>(id);

        foreach (var ogiId in rec.OgiIds)
        {
            if (OgiStillNeeded(ogiId)) continue;
            dest.Ogis?.RemoveItem<PS2AnyOGI>(ogiId);
            destTables.OGIs.Remove(ogiId);
        }
        foreach (var animId in rec.AnimIds)
        {
            if (AnimStillNeeded(animId)) continue;
            dest.Animations?.RemoveItem<PS2AnyAnimation>(animId);
            destTables.Animations.Remove(animId);
        }

        RemoveUnusedTransplantGraphics(destTables, destTexCache, dest,
            rec.RigidModelIds, rec.SkinIds, rec.BlendSkinIds, rec.ModelIds, rec.MaterialIds, rec.TextureIds);
    }

    private static BaseTwinItem CloneItemDynamic(BaseTwinItem source)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms)) source.Write(w);
        var bytes = ms.ToArray();
        var clone = (BaseTwinItem)Activator.CreateInstance(source.GetType())!;
        using var rs = new MemoryStream(bytes);
        using var r = new BinaryReader(rs);
        clone.Read(r, bytes.Length);
        clone.SetID(source.GetID());
        return clone;
    }

    public static List<uint> GetObjectBehaviourIds(PS2AnyTwinsanityRM2 rm2, uint objId)
    {
        var res = new List<uint>();
        var obj = Rm2Sections.Of(rm2).Objects?.GetItem<PS2AnyObject>(objId);
        if (obj is null) return res;
        foreach (var b in obj.BehaviourSlots) if (b != 0xFFFF && !res.Contains(b)) res.Add(b);
        foreach (var b in obj.RefBehaviours)  if (b != 0xFFFF && !res.Contains(b)) res.Add(b);
        return res;
    }

    public static List<uint> TransplantBehaviours(PS2AnyTwinsanityRM2 srcRm2, PS2AnyTwinsanityRM2 destRm2,
        uint srcObjId, uint targetObjId, IReadOnlyCollection<uint> selectedIds)
    {
        var src  = Rm2Sections.Of(srcRm2);
        var dest = Rm2Sections.Of(destRm2);
        var srcBehSec  = src.Behaviours;
        var srcObj     = src.Objects?.GetItem<PS2AnyObject>(srcObjId);
        var destBehSec = dest.Behaviours;
        var targetObj  = dest.Objects?.GetItem<PS2AnyObject>(targetObjId);
        var attached = new List<uint>();
        if (srcBehSec is null || destBehSec is null || srcObj is null || targetObj is null) return attached;

        var sel = new HashSet<uint>(selectedIds);

        void Clone(uint behId)
        {
            if (destBehSec.ContainsItem(behId)) return;
            var srcBeh = srcBehSec.GetItem<BaseTwinItem>(behId);
            if (srcBeh is null) return;
            destBehSec.AddItem(CloneItemDynamic(srcBeh));
            if (srcBeh is Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter starter)
                foreach (var a in starter.Assigners) if (a.Behaviour != 0) Clone((uint)a.Behaviour);
        }
        foreach (var id in selectedIds) Clone(id);

        targetObj.BehaviourSlots.Clear();
        foreach (var b in srcObj.BehaviourSlots)
            if (b == 0xFFFF || sel.Contains(b))
            {
                targetObj.BehaviourSlots.Add(b);
                if (b != 0xFFFF && destBehSec.ContainsItem(b) && !attached.Contains(b)) attached.Add(b);
            }
        targetObj.RefBehaviours.Clear();
        foreach (var b in srcObj.RefBehaviours)
            if (b == 0xFFFF || sel.Contains(b))
            {
                targetObj.RefBehaviours.Add(b);
                if (b != 0xFFFF && destBehSec.ContainsItem(b) && !attached.Contains(b)) attached.Add(b);
            }
        return attached;
    }

    public static FullTransplantRecord? TransplantObjectFull(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanityRM2 sourceRm2, uint rootSourceObjectId, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }

        var src = Rm2Sections.Of(sourceRm2);
        var srcObjSec = src.Objects;
        if (srcObjSec is null) { log = "source level has no GameObjects section"; return null; }
        var rootSrcObj = srcObjSec.GetItem<PS2AnyObject>(rootSourceObjectId);
        if (rootSrcObj is null) { log = "root object not found in source level"; return null; }

        var dest = Rm2Sections.Of(destRm2);
        var destObjSec = dest.Objects;
        var destBehSec = dest.Behaviours;
        var destSndSec = dest.Sounds;
        if (destObjSec is null || dest.Ogis is null || dest.Animations is null || destBehSec is null || destSndSec is null ||
            dest.RigidModels is null || dest.Models is null || dest.Materials is null || dest.Skins is null || dest.BlendSkins is null || dest.Textures is null)
        { log = "destination chunk is missing a required CODE/GRAPHICS section"; return null; }

        var rec = new FullTransplantRecord();
        var copy = GraphicsCopy.For(gl, destTables, destTexCache, src, dest, rec);

        void CloneBehaviour(uint behId)
        {
            if (destBehSec.ContainsItem(behId) || src.Behaviours is null) return;
            var srcBeh = src.Behaviours.GetItem<BaseTwinItem>(behId);
            if (srcBeh is null) return;
            var newBeh = CloneItemDynamic(srcBeh);
            destBehSec.AddItem(newBeh);
            rec.BehaviourIds.Add(behId);
            if (newBeh is TwinBehaviourStarter starter)
                foreach (var assigner in starter.Assigners)
                    if (assigner.Behaviour != 0)
                        CloneBehaviour((uint)assigner.Behaviour);
        }
        void CloneSound(uint sndId)
        {
            if (destSndSec.ContainsItem(sndId) || src.Sounds is null) return;
            var srcSnd = src.Sounds.GetItem<PS2AnySound>(sndId);
            if (srcSnd is null) return;
            var newSnd = new PS2AnySound
            {
                Header = srcSnd.Header, UnkFlag = srcSnd.UnkFlag, FreqFac = srcSnd.FreqFac,
                Param1 = srcSnd.Param1, Param2 = srcSnd.Param2, Param3 = srcSnd.Param3, Param4 = srcSnd.Param4,
                Sound = (byte[])srcSnd.Sound.Clone(),
            };
            newSnd.SetID(sndId);
            destSndSec.AddItem(newSnd);
            rec.SoundIds.Add(sndId);
        }

        var objQueue = new Queue<uint>();
        objQueue.Enqueue(rootSourceObjectId);
        var visited = new HashSet<uint>();
        int unhandledCodeModels = 0, unhandledUnknowns = 0, newObjectCount = 0;

        while (objQueue.Count > 0)
        {
            var srcId = objQueue.Dequeue();
            if (visited.Contains(srcId)) continue;
            visited.Add(srcId);
            rec.ObjectIdMap[srcId] = srcId;

            var srcObj = srcObjSec.GetItem<PS2AnyObject>(srcId);
            if (srcObj is null) continue;

            if (srcObj.RefCodeModels.Count > 0) unhandledCodeModels += srcObj.RefCodeModels.Count;
            if (srcObj.RefUnknowns.Count > 0)   unhandledUnknowns   += srcObj.RefUnknowns.Count;

            bool isNewObj = !destObjSec.ContainsItem(srcId);
            if (isNewObj)
            {
                var newObj = CloneItem(srcObj);
                destObjSec.AddItem(newObj);
                destTables.Objects[srcId] = newObj;
                newObjectCount++;
            }

            foreach (var s in srcObj.OGISlots)        if (s != 0xFFFF) copy.Ogi(s);
            foreach (var s in srcObj.AnimationSlots)  if (s != 0xFFFF) copy.Animation(s);
            foreach (var s in srcObj.SoundSlots)      if (s != 0xFFFF) CloneSound(s);
            foreach (var s in srcObj.BehaviourSlots)  if (s != 0xFFFF) CloneBehaviour(s);
            foreach (var refOgi in srcObj.RefOGIs)        copy.Ogi(refOgi);
            foreach (var refAnim in srcObj.RefAnimations) copy.Animation(refAnim);
            foreach (var refBeh in srcObj.RefBehaviours)  CloneBehaviour(refBeh);
            foreach (var refSnd in srcObj.RefSounds)      CloneSound(refSnd);

            foreach (var slotObjId in srcObj.ObjectSlots)
                if (slotObjId != 0xFFFF && !visited.Contains(slotObjId))
                    objQueue.Enqueue(slotObjId);
            foreach (var refObjId in srcObj.RefObjects)
                if (!visited.Contains(refObjId))
                    objQueue.Enqueue(refObjId);
        }

        RebuildTransplantGpu(gl, dest.Gfx!, destTables, destTexCache);

        rec.RootObjectId = rootSourceObjectId;
        log = $"Full transplant of '{rootSrcObj.Name}' (id 0x{rec.RootObjectId:X4}, unchanged — " +
              $"CODE ids are shared/global, never reassigned) — {newObjectCount} new GameObject(s) " +
              $"added (root + referenced; others already present were reused as-is), " +
              $"{rec.OgiIds.Count} OGI, {rec.AnimIds.Count} Animation, {rec.BehaviourIds.Count} Behaviour, " +
              $"{rec.SoundIds.Count} Sound, {rec.RigidModelIds.Count} RigidModel, {rec.ModelIds.Count} Model, " +
              $"{rec.SkinIds.Count} Skin, {rec.BlendSkinIds.Count} BlendSkin, {rec.MaterialIds.Count} Material, " +
              $"{rec.TextureIds.Count} Texture." +
              (unhandledCodeModels + unhandledUnknowns > 0
                  ? $" WARNING: {unhandledCodeModels} RefCodeModel(s) + {unhandledUnknowns} RefUnknown(s) " +
                    "were NOT copied (unhandled) — this object references data this transplant doesn't understand yet."
                  : "") +
              " Behaviour graphs copied as opaque data — any internal id references THEY make on " +
              "their own (not exposed via RefObjects/RefOGIs/RefAnimations/RefSounds) are NOT specially " +
              "handled, but since ids are never reassigned, any such reference that was valid in the " +
              "source is still valid here as long as its target got cloned too.";
        return rec;
    }

    public static List<ushort> CopyInstanceLayoutRefs<TItem>(
        PS2AnyTwinsanityRM2 sourceRm2, PS2AnyTwinsanityRM2 destRm2,
        BaseTwinSection destInstancesSection, List<ushort> srcIds, int sectionConstant,
        out int notFoundCount)
        where TItem : BaseTwinItem, new()
    {
        notFoundCount = 0;
        var result = new List<ushort>();
        if (srcIds.Count == 0) return result;

        BaseTwinSection? destSec = null;
        for (int lid = 0; lid <= 7 && destSec is null; lid++)
        {
            var layout = destRm2.GetItem<BaseTwinSection>((uint)lid);
            var instSec = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION);
            if (!ReferenceEquals(instSec, destInstancesSection)) continue;
            destSec = layout?.GetItem<BaseTwinSection>((uint)sectionConstant);
        }
        if (destSec is null) { notFoundCount = srcIds.Count; return result; }

        foreach (var srcId in srcIds)
        {
            TItem? srcItem = null;
            for (int lid = 0; lid <= 7 && srcItem is null; lid++)
            {
                var srcLayout = sourceRm2.GetItem<BaseTwinSection>((uint)lid);
                var srcSec = srcLayout?.GetItem<BaseTwinSection>((uint)sectionConstant);
                srcItem = srcSec?.GetItem<TItem>(srcId);
            }
            if (srcItem is null) { notFoundCount++; continue; }

            var clone = CloneItem(srcItem);
            uint newId = 0;
            while (destSec.ContainsItem(newId)) newId++;
            clone.SetID(newId);
            destSec.AddItem(clone);
            result.Add((ushort)newId);
        }
        return result;
    }

    public static bool TransplantSkydome(GL gl,
        PS2AnyTwinsanitySM2 destSm2, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanitySM2 sourceSm2, out string log)
    {
        var destGfx = destSm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var srcGfx  = sourceSm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        if (destGfx is null || srcGfx is null) { log = "missing GRAPHICS section on one side"; return false; }

        var srcSkySec = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKYDOMES_SECTION);
        var srcSky = srcSkySec is not null && srcSkySec.GetItemsAmount() > 0
            ? srcSkySec.GetItem(0) as PS2AnySkydome : null;
        if (srcSky is null) { log = "source level has no skydome data"; return false; }

        var copy = SceneryGraphicsCopy.Between(gl, destTexCache, srcGfx, destGfx, withLods: false);
        if (copy is null) { log = "source scenery is missing a required GRAPHICS subsection"; return false; }

        var missingMeshIds = new List<uint>();
        var newMeshIds = new List<uint>();
        foreach (var meshId in srcSky.Meshes)
        {
            var newRmId = copy.RigidModel(meshId);
            if (newRmId is null) { missingMeshIds.Add(meshId); continue; }
            newMeshIds.Add(newRmId.Value);
        }

        if (newMeshIds.Count == 0)
        {
            log = "source skydome had no resolvable meshes" +
                  (missingMeshIds.Count > 0 ? $" (all {missingMeshIds.Count} mesh id(s) unresolved: {string.Join(", ", missingMeshIds.Select(m => $"0x{m:X}"))})" : "");
            return false;
        }

        var destSkySec = destGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKYDOMES_SECTION);
        if (destSkySec is null)
        {
            destSkySec = new PS2AnySkydomesSection();
            destSkySec.SetID((uint)Constants.GRAPHICS_SKYDOMES_SECTION);
            destGfx.AddItem(destSkySec);
        }
        var destSky = destSkySec.GetItemsAmount() > 0 ? destSkySec.GetItem(0) as PS2AnySkydome : null;
        if (destSky is null)
        {
            destSky = new PS2AnySkydome();
            destSky.SetID(NewGraphicsLocalId(destSkySec));
            destSkySec.AddItem(destSky);
        }
        destSky.Meshes.Clear();
        destSky.Meshes.AddRange(newMeshIds);

        var destScenery = destSm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        if (destScenery is not null) destScenery.SkydomeID = destSky.GetID();

        var totalSource = srcSky.Meshes.Count;
        log = $"Skydome swapped — {newMeshIds.Count}/{totalSource} mesh(es), +{copy.ModelIdMap.Count} Model, " +
              $"+{copy.MatIdMap.Count} Material, +{copy.TexIdMap.Count} Texture. Old skydome data left in " +
              "place (unused, harmless).";
        var gaps = new List<string>();
        if (missingMeshIds.Count > 0) gaps.Add($"{missingMeshIds.Count} mesh id(s) not found: {string.Join(", ", missingMeshIds.Select(m => $"0x{m:X}"))}");
        if (copy.MissingModelIds.Count > 0) gaps.Add($"{copy.MissingModelIds.Count} Model id(s) not found: {string.Join(", ", copy.MissingModelIds.Select(m => $"0x{m:X}"))}");
        if (copy.MissingMaterialIds.Count > 0) gaps.Add($"{copy.MissingMaterialIds.Count} Material id(s) not found: {string.Join(", ", copy.MissingMaterialIds.Select(m => $"0x{m:X}"))}");
        if (copy.MissingTextureIds.Count > 0) gaps.Add($"{copy.MissingTextureIds.Count} Texture id(s) not found (left pointing at the OLD id, likely wrong/missing in-game): {string.Join(", ", copy.MissingTextureIds.Select(m => $"0x{m:X}"))}");
        if (gaps.Count > 0)
            log += "  INCOMPLETE — " + string.Join("; ", gaps);
        return true;
    }
}
