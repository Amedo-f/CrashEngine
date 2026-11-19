using CrashEngine.Assets.Modern;
using CrashEngine.Core;
using CrashEngine.Importer;
using System.Text.Json;

namespace CrashLauncher;

// Amedo 2026-11-18
public sealed partial class GameScene
{
    private const string InstanceSuffix = "_Inst_";
    private static readonly string[] ModernExtensions = [".glb", ".gltf"];

    private sealed record ModernLink(ushort ObjectId, string ObjectName, string File, float Scale = 1f, string[]? Slots = null);

    private string ModernDir => ModeDir("Modern");
    private string ModernLinksPath => Path.Combine(ModernDir, GamePaths.FileNameWithoutExtension(_rm2) + ".json");

    private ModernCharacter? ReadModernCharacter(string modelPath)
    {
        if (!ModernExtensions.Contains(Path.GetExtension(modelPath).ToLowerInvariant())) return null;
        var data = ModernCharacterImporter.Import(modelPath, out string error);
        if (data is null) { _browser.Log($"Add Model: {error}"); return null; }
        if (data.Joints.Count == 0 || data.Clips.Count == 0) return null;
        if (GlCaps.ModernSkin) return data;
        _browser.Log($"Add Model: {Path.GetFileName(modelPath)} has bones, and this graphics card can't draw them; it comes in as a still model.");
        return null;
    }

    private void AddModernLook(Entity instance, string modelPath, ModernCharacter data)
    {
        if (instance.Get<InstanceData>() is not { } inst) return;
        Directory.CreateDirectory(ModernDir);
        string file = CopyIntoProject(modelPath);
        var links = ReadModernLinks();
        links.RemoveAll(l => l.ObjectId == inst.ObjectId);
        links.Add(new ModernLink(inst.ObjectId, ObjectNameOf(instance), file));
        WriteModernLinks(links);
        if (AttachModernLook(instance, Path.Combine(ModernDir, file), data, 1f, null) is { } look) SaveModernLook(look);
        _browser.Log($"Add Model (modern look, PC only): {data.Joints.Count} bones, {data.Meshes.Max(m => m.Influences)} weights per vertex, {data.VertexCount:N0} vertices, " +
                     $"{data.Clips.Count} animations, {Math.Min(data.Clips.Count, ModernCharacterRenderer.MaxSlots)} of them in slots 0–{Math.Min(data.Clips.Count, ModernCharacterRenderer.MaxSlots) - 1} " +
                     "(Inspector → Animations; a behaviour plays one with Do Animation and its slot number). The game object underneath is the still PS2 model that Build ISO uses. Save Chunk keeps the object and its slots." +
                     (data.Notes.Count > 0 ? " Notes: " + string.Join("; ", data.Notes.Take(8)) + (data.Notes.Count > 8 ? $"; and {data.Notes.Count - 8} more" : "") + "." : ""));
    }

    private void AttachModernLooks(Entity chunkRoot)
    {
        var links = ReadModernLinks();
        if (links.Count == 0) return;
        var cache = new Dictionary<string, ModernCharacter?>(StringComparer.OrdinalIgnoreCase);
        var found = new HashSet<ModernLink>(); // Amedo 2026-11-18
        foreach (var e in AllEntities(chunkRoot).ToList())
        {
            if (e.Get<InstanceData>() is not { } inst) continue;
            var link = links.FirstOrDefault(l => l.ObjectId == inst.ObjectId && l.ObjectName == ObjectNameOf(e));
            if (link is null) continue;
            found.Add(link); // Amedo 2026-11-18
            string path = Path.Combine(ModernDir, link.File);
            if (!cache.TryGetValue(path, out var data))
                cache[path] = data = File.Exists(path) ? ModernCharacterImporter.Import(path, out _) : null;
            if (data is null) { _browser.Log($"Modern look: {link.File} is missing or unreadable; {e.Name} stays the still model."); continue; }
            if (AttachModernLook(e, path, data, link.Scale, link.Slots) is { } look) SyncModernSlots(inst, look);
        }
        foreach (var link in links.Where(l => !found.Contains(l))) // Amedo 2026-11-18
            _browser.Log($"Modern look: {link.File} is linked to object 0x{link.ObjectId:X4} ({link.ObjectName}), which is not in this level, so it was not attached. " +
                         "If the model was imported with an older version of the editor, it came in as a still model: delete it and import the model again here.");
    }

    private ModernCharacterRenderer? AttachModernLook(Entity instance, string path, ModernCharacter data, float scale, string[]? slots) =>
        ModernCharacterRenderer.AttachTo(instance, Engine.Instance.GL, data, path, scale, slots);

    private void SaveModernLook(ModernCharacterRenderer look)
    {
        if (look.Entity?.Parent?.Get<InstanceData>() is not { } inst) return;
        SyncModernSlots(inst, look);
        var links = ReadModernLinks();
        int at = links.FindIndex(l => l.ObjectId == inst.ObjectId);
        if (at < 0) return;
        links[at] = links[at] with { Scale = look.Scale, Slots = look.Slots.Select(c => c >= 0 ? look.Data.Clips[c].Name : "").ToArray() };
        WriteModernLinks(links);
    }

    private void SyncModernSlots(InstanceData inst, ModernCharacterRenderer look)
    {
        var tables = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.MeshTables;
        if (tables is null || MeshDecoder.SetStillSlotCount(tables, inst.ObjectId, look.Slots.Count)) return;
        _browser.Log($"Modern look: object 0x{inst.ObjectId:X4} has its own PS2 animation slots, so its slot count was left as it is.");
    }

    private void WriteModernLinks(List<ModernLink> links) =>
        File.WriteAllText(ModernLinksPath, JsonSerializer.Serialize(links, new JsonSerializerOptions { WriteIndented = true }));

    private List<ModernLink> ReadModernLinks()
    {
        try { return File.Exists(ModernLinksPath) ? JsonSerializer.Deserialize<List<ModernLink>>(File.ReadAllText(ModernLinksPath)) ?? new() : new(); }
        catch (Exception ex) { _browser.Log($"Modern look: {Path.GetFileName(ModernLinksPath)} unreadable ({ex.Message})."); return new(); }
    }

    private string CopyIntoProject(string modelPath)
    {
        string name = Path.GetFileName(modelPath);
        string target = Path.Combine(ModernDir, name);
        for (int n = 2; File.Exists(target) && !SameFile(target, modelPath); n++)
        {
            name = $"{Path.GetFileNameWithoutExtension(modelPath)}_{n}{Path.GetExtension(modelPath)}";
            target = Path.Combine(ModernDir, name);
        }
        if (!File.Exists(target)) File.Copy(modelPath, target);
        return name;
    }

    private static bool SameFile(string a, string b) =>
        new FileInfo(a).Length == new FileInfo(b).Length && File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));

    private static string ObjectNameOf(Entity instance)
    {
        int at = instance.Name.LastIndexOf(InstanceSuffix, StringComparison.Ordinal);
        return at > 0 ? instance.Name[..at] : instance.Name;
    }
}
