using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

// Amedo
public sealed partial class GameScene
{
    private (uint ObjectId, bool Scripts, bool Look)? _uniqueStatus;
    private (string Rm2Path, long Now, long Disc)? _levelSize;

    // Amedo
    private ChunkSource? ChunkSourceFor(Entity e)
    {
        for (var p = e; p is not null; p = p.Parent)
            if (p.Get<ChunkSource>() is { } src) return src;
        return Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
    }

    // Amedo
    private IEnumerable<Entity> InstancesOfObject(ChunkSource src, uint objectId) =>
        Roots.Where(r => ReferenceEquals(r.Get<ChunkSource>(), src)).SelectMany(AllEntities)
             .Where(x => x.Get<InstanceData>() is { } d && d.ObjectId == objectId);

    // Amedo
    private PS2AnyObject? LevelObject(ChunkSource src, uint objectId)
    {
        foreach (var rm2 in new[] { src.Rm2, src.GlobalRm2 })
            if (rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                   ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION) is { } objs && objs.ContainsItem(objectId))
                return objs.GetItem<PS2AnyObject>(objectId);
        return null;
    }

    // Amedo
    private PS2BehaviourGraph? ObjectGraph(ChunkSource src, PS2AnyObject? obj, uint sharedId, out bool own)
    {
        own = false;
        var behSec = src.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
        var shared = behSec is not null && behSec.ContainsItem(sharedId) ? behSec.GetItem<PS2BehaviourGraph>(sharedId) : null;
        if (obj is null || shared is null) return shared;
        if (MeshDecoder.ObjectBehaviourNamed(src.Rm2, src.GlobalRm2, obj, shared.Name) is { } g && g.GetID() >= MeshDecoder.UniqueBehaviourIdFloor)
        {
            own = true;
            return g;
        }
        return shared;
    }

    // Amedo
    private bool ObjectRunsBehaviour(ChunkSource src, PS2AnyObject obj, uint sharedId) =>
        obj.RefBehaviours.Contains((ushort)sharedId) || ObjectGraph(src, obj, sharedId, out bool own) is not null && own;

    // Amedo
    private ushort? MakeSelectionUnique(Entity e)
    {
        if (ChunkSourceFor(e) is not { MeshTables: { } tables } src || e.Get<InstanceData>() is not { } data) return null;
        if (MeshDecoder.IsUniqueObject(src.Rm2, data.ObjectId)) return data.ObjectId;
        uint oldId = data.ObjectId;
        var group = (_selectedSet.Contains(e) ? _selectedSet : new HashSet<Entity> { e })
            .Where(x => x.Get<InstanceData>()?.ObjectId == oldId).ToList();
        if (MeshDecoder.MakeObjectUnique(src.Rm2, tables, oldId, out var log) is not { } newId)
        {
            _browser.Log($"Make Unique failed: {log}");
            return null;
        }
        var gl = Engine.Instance.GL;
        foreach (var g in group)
        {
            var d = g.Get<InstanceData>()!;
            d.ObjectId = newId;
            d.Source.ObjectId = newId;
            MeshDecoder.BuildMeshForInstance(gl, tables, g);
        }
        _uniqueStatus = null;
        _browser.Log($"Make Unique: {log} Placements moved to it: {group.Count}. Save Chunk to persist.");
        return newId;
    }

    // Amedo
    private bool MakeSelectionOwnScripts(Entity e)
    {
        if (MakeSelectionUnique(e) is not { } id || ChunkSourceFor(e) is not { } src) return false;
        int n = MeshDecoder.OwnObjectScripts(src.Rm2, src.GlobalRm2, id, out var map, out var log);
        if (n > 0)
            foreach (var x in InstancesOfObject(src, id))
            {
                var s = x.Get<InstanceData>()!.Source;
                s.OnSpawnHeaderScriptID = MeshDecoder.RemapBehaviourId(map, s.OnSpawnHeaderScriptID);
            }
        _uniqueStatus = null;
        _browser.Log($"Own Scripts: {log}");
        return true;
    }

    // Amedo
    private void MakeSelectionOwnLook(Entity e)
    {
        if (MakeSelectionUnique(e) is not { } id || ChunkSourceFor(e) is not { MeshTables: { } tables } src) return;
        var gl = Engine.Instance.GL;
        MeshDecoder.OwnObjectLook(gl, src.Rm2, tables, src.TexCache, id, out var log);
        foreach (var x in InstancesOfObject(src, id)) MeshDecoder.BuildMeshForInstance(gl, tables, x);
        _uniqueStatus = null;
        _browser.Log($"Own Model & Textures: {log}");
    }

    // Amedo
    private void DrawUniqueObject(Entity e, InstanceData inst)
    {
        if (!ImGui.CollapsingHeader("Unique Object##uniqueobject")) return;
        if (ChunkSourceFor(e) is not { MeshTables: { } tables } src) return;
        var obj = LevelObject(src, inst.ObjectId);
        if (obj is null) { ImGui.TextDisabled("Object not found."); return; }
        int users = InstancesOfObject(src, inst.ObjectId).Count();

        if (!MeshDecoder.IsUniqueObject(src.Rm2, inst.ObjectId))
        {
            bool global = src.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION)?.ContainsItem(inst.ObjectId) == true;
            ImGui.TextWrapped($"Shared: '{obj.Name}' is used by {users} placement(s) in this level" +
                              (global ? " and by every level (it lives in Default.rm2)." : "."));
            int group = _selectedSet.Contains(e) ? _selectedSet.Count(x => x.Get<InstanceData>()?.ObjectId == inst.ObjectId) : 1;
            if (ImGui.Button($"Make Unique ({group} placement{(group == 1 ? "" : "s")})##makeunique")) MakeSelectionUnique(e);
        }
        else
        {
            if (_uniqueStatus is not { } st || st.ObjectId != inst.ObjectId)
                _uniqueStatus = st = (inst.ObjectId, MeshDecoder.OwnsScripts(src.Rm2, src.GlobalRm2, obj), MeshDecoder.OwnsLook(src.Rm2, tables, obj));
            ImGui.TextWrapped($"Unique: own object 0x{inst.ObjectId:X4} ('{obj.Name}'), used by {users} placement(s).");
            ImGui.TextDisabled($"Scripts: {(st.Scripts ? "own" : "shared")}   Model & textures: {(st.Look ? "own" : "shared")}");
            if (!st.Scripts && ImGui.Button("Own Scripts##ownscripts")) MakeSelectionOwnScripts(e);
            if (!st.Scripts && !st.Look) ImGui.SameLine();
            if (!st.Look && ImGui.Button("Own Model & Textures##ownlook")) MakeSelectionOwnLook(e);
        }

        if (ImGui.Button("Measure Level Size##levelsize")) MeasureLevelSize(src);
        if (_levelSize is { } size && size.Rm2Path == src.Rm2Path)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(size.Disc > 0
                ? $"{size.Now / 1024.0:F0} KB (disc {size.Disc / 1024.0:F0} KB, {(size.Now - size.Disc) / 1024.0:+0;-0} KB)"
                : $"{size.Now / 1024.0:F0} KB");
        }
        ImGui.TextDisabled("Save Chunk to persist.");
    }

    // Amedo
    private void MeasureLevelSize(ChunkSource src)
    {
        long now = 0, disc = 0;
        try
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) src.Rm2.Write(w);
            now = ms.Length;
        }
        catch (Exception ex) { _browser.Log($"Measure Level Size failed: {ex.Message}"); return; }
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var s = pkg.OpenByPath(src.Rm2Path);
            if (s is not null) disc = s.Length;
        }
        catch { }
        _levelSize = (src.Rm2Path, now, disc);
    }
}
