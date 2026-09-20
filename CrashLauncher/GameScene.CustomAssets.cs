using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using Constants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPath;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject; // Amedo 2026-09-19
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private bool         _showAddToAssetsPopup;
    private List<Entity> _addToAssetsTargets = new();
    private string       _addToAssetsName = "Custom";
    private string       _addToAssetsNewFolder = "";

    private string GetCustomSceneryAssetsRoot() =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "CustomSceneryAssets");

    private void OpenAddToAssetsPopup(List<Entity> sceneryTargets)
    {
        _addToAssetsTargets = sceneryTargets;
        _addToAssetsName = "Custom";
        _addToAssetsNewFolder = "";
        _showAddToAssetsPopup = true;
    }

    private void DrawAddToAssetsPopup()
    {
        if (!_showAddToAssetsPopup) return;

        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var sm2 = chunkSource?.Sm2;
        var scenery = sm2?.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);

        ImGui.SetNextWindowSize(new Vector2(360f, 220f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Add to Assets##addtoassets", ref _showAddToAssetsPopup, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        if (scenery is null || sm2 is null)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "This level has no real scenery tree to save from.");
            ImGui.End();
            return;
        }

        var tileIndices = new List<int>();
        var onlyMeshIds = new Dictionary<int, HashSet<uint>>();
        foreach (var e in _addToAssetsTargets)
        {
            var tile = e.Get<SceneryTile>();
            if (tile?.Node is null) continue;
            int idx = scenery.Sceneries.IndexOf(tile.Node);
            if (idx < 0) continue;
            if (!tileIndices.Contains(idx)) tileIndices.Add(idx);
            if (!onlyMeshIds.TryGetValue(idx, out var set)) onlyMeshIds[idx] = set = new HashSet<uint>();
            set.Add(tile.SourceId);
        }

        if (tileIndices.Count == 0)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Couldn't resolve any selected tile back to a real scenery node.");
            ImGui.End();
            return;
        }

        ImGui.TextWrapped($"Saving an independent copy of {tileIndices.Count} tile(s), keeping their relative layout.");
        ImGui.Separator();

        ImGui.TextUnformatted("Name:");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##addToAssetsName", ref _addToAssetsName, 64);

        ImGui.Spacing();
        ImGui.TextUnformatted("Save into:");
        var root = GetCustomSceneryAssetsRoot();
        var folders = CustomSceneryAssetLibrary.ListAllFolders(root);

        void SaveTo(string folder)
        {
            var name = string.IsNullOrWhiteSpace(_addToAssetsName) ? "Custom" : _addToAssetsName;
            var container = BlankSceneTemplate.CreateBlankSm2("CustomAsset");
            var scratchTexCache = new Dictionary<uint, Texture2D>();
            if (!MeshDecoder.TransplantSceneryTiles(Engine.Instance.GL, container, scratchTexCache,
                    sm2, tileIndices, out string cloneLog, targetPosition: null, onlyMeshIds: onlyMeshIds))
            {
                _browser.Log($"Assets: couldn't build the asset copy -- {cloneLog}");
                return;
            }
            var containerIndices = MeshDecoder.GetSceneryTileList(container).Select(t => t.Index).ToList();
            CustomSceneryAssetLibrary.Save(root, folder, name, container, containerIndices);
            _browser.Log($"Assets: saved an independent copy of {tileIndices.Count} tile(s) to My Assets/{folder}/{name}.");
            _showAddToAssetsPopup = false;
        }

        if (ImGui.Button("📁 Save to a new folder (auto)##autoNewFolder", new Vector2(-1f, 0f)))
            SaveTo(CustomSceneryAssetLibrary.UniqueNewFolderLeaf(root, ""));

        ImGui.Spacing();
        ImGui.TextDisabled(folders.Count == 0 ? "  (no folders yet -- or use the button above)" : "or pick an existing folder:");
        ImGui.BeginChild("##addToAssetsFolders", new Vector2(-1f, 80f), ImGuiChildFlags.Border);
        foreach (var folder in folders)
        {
            if (ImGui.Selectable(folder)) SaveTo(folder);
        }
        ImGui.EndChild();

        ImGui.SetNextItemWidth(-70f);
        ImGui.InputTextWithHint("##addToAssetsNewFolder", "or type a new folder name...", ref _addToAssetsNewFolder, 64);
        ImGui.SameLine();
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_addToAssetsNewFolder));
        if (ImGui.Button("Save##addToAssetsNewFolderSave", new Vector2(-1f, 0f)))
            SaveTo(_addToAssetsNewFolder.Trim());
        ImGui.EndDisabled();

        ImGui.End();
    }

    private bool    _showAddObjectToAssetsPopup;
    private Entity? _addObjectTarget;
    private string  _addObjectName = "Object";
    private string  _addObjectNewFolder = "";
    private readonly Dictionary<(string Folder, string File), Texture2D?> _objectThumbCache = new();

    private void OpenAddObjectToAssetsPopup(Entity objectEntity)
    {
        _addObjectTarget = objectEntity;
        _addObjectName = "Object";
        _addObjectNewFolder = "";
        _showAddObjectToAssetsPopup = true;
    }

    private List<(GpuMesh Mesh, Material Mat, Matrix4x4 Model)> CollectEntityMeshParts(Entity e)
    {
        var parts = new List<(GpuMesh, Material, Matrix4x4)>();
        void Visit(Entity x)
        {
            if (!x.Active) return;
            if (x.Get<CrashEngine.Importer.MeshRenderer>() is { Mesh: { } mesh, Enabled: true } mr)
                parts.Add((mesh, mr.Material ?? new Material(), x.Transform.World));
            foreach (var c in x.Children) Visit(c);
        }
        Visit(e);
        return parts;
    }

    private void DrawAddObjectToAssetsPopup()
    {
        if (!_showAddObjectToAssetsPopup) return;
        var target = _addObjectTarget;
        var inst = target?.Get<InstanceData>();
        ImGui.SetNextWindowSize(new Vector2(360f, 250f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Add Object to Assets##addobjtoassets", ref _showAddObjectToAssetsPopup, ImGuiWindowFlags.NoCollapse))
        { ImGui.End(); return; }
        if (target is null || inst?.Source is null)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "No object instance selected.");
            ImGui.End(); return;
        }

        ImGui.TextWrapped($"Save object 0x{inst.ObjectId:X4} with its full data + settings (references '{System.IO.Path.GetFileName(_rm2)}').");
        ImGui.Separator();
        ImGui.TextUnformatted("Name:");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##addObjName", ref _addObjectName, 64);

        ImGui.Spacing();
        ImGui.TextUnformatted("Save into:");
        var root = GetCustomSceneryAssetsRoot();
        var folders = CustomSceneryAssetLibrary.ListAllFolders(root);

        void SaveObjectTo(string folder)
        {
            if (SaveObjectAsset(target!, folder, _addObjectName))
                _showAddObjectToAssetsPopup = false;
        }

        if (ImGui.Button("📁 Save to a new folder (auto)##objAutoFolder", new Vector2(-1f, 0f)))
            SaveObjectTo(CustomSceneryAssetLibrary.UniqueNewFolderLeaf(root, ""));
        ImGui.Spacing();
        ImGui.TextDisabled(folders.Count == 0 ? "  (no folders yet -- or use the button above)" : "or pick an existing folder:");
        ImGui.BeginChild("##addObjFolders", new Vector2(-1f, 80f), ImGuiChildFlags.Border);
        foreach (var f in folders) if (ImGui.Selectable(f)) SaveObjectTo(f);
        ImGui.EndChild();
        ImGui.SetNextItemWidth(-70f);
        ImGui.InputTextWithHint("##addObjNewFolder", "or type a new folder name...", ref _addObjectNewFolder, 64);
        ImGui.SameLine();
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_addObjectNewFolder));
        if (ImGui.Button("Save##addObjNewFolderSave", new Vector2(-1f, 0f)))
            SaveObjectTo(_addObjectNewFolder.Trim());
        ImGui.EndDisabled();
        ImGui.End();
    }

    private bool SaveObjectAsset(Entity target, string folder, string name)
    {
        var inst = target.Get<InstanceData>();
        var s = inst?.Source;
        if (inst is null || s is null) { _browser.Log("Add Object to Assets: not an object instance."); return false; }

        var sourceLevel = (_rm2.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? _rm2[..^4] : _rm2)
            .Replace('/', '\\').TrimStart('\\');

        var cfg = new CustomSceneryAssetLibrary.CustomObjectInstanceConfig(
            (long)s.StateFlags, (int)s.RefListIndex, (int)s.OnSpawnHeaderScriptID,
            (int)s.InstancesRelated, (int)s.PositionsRelated, (int)s.PathsRelated,
            s.Positions.Select(p => (int)p).ToList(),
            s.Paths.Select(p => (int)p).ToList(),
            new List<uint>(s.ParamList1), new List<float>(s.ParamList2), new List<uint>(s.ParamList3));

        var parts = CollectEntityMeshParts(target);
        byte[]? thumb = parts.Count > 0 ? BakeThumbnailPixelsFromParts(Engine.Instance.GL, parts, out _) : null;

        var entry = new CustomSceneryAssetLibrary.CustomObjectAssetEntry(
            string.IsNullOrWhiteSpace(name) ? "Object" : name, sourceLevel, inst.ObjectId, 96, cfg);
        CustomSceneryAssetLibrary.SaveObject(GetCustomSceneryAssetsRoot(), folder, entry.Name, entry, thumb);
        _browser.Log($"Assets: saved object '{entry.Name}' (0x{inst.ObjectId:X4}) to My Assets/{folder}.");
        return true;
    }

    private Texture2D? GetOrLoadObjectThumb(string folder, string fileName, int size)
    {
        var key = (folder, fileName);
        if (_objectThumbCache.TryGetValue(key, out var cached)) return cached;
        var px = CustomSceneryAssetLibrary.LoadObjectThumb(GetCustomSceneryAssetsRoot(), folder, fileName);
        Texture2D? tex = px is not null && px.Length >= size * size * 4 ? new Texture2D(Engine.Instance.GL, px, (uint)size, (uint)size) : null;
        _objectThumbCache[key] = tex;
        return tex;
    }

    private void ImportObjectAssetEntry(string folder, string fileName, CustomSceneryAssetLibrary.CustomObjectAssetEntry entry)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Import Object: no level loaded."); return; }
        try
        {
            PS2AnyTwinsanityRM2 sourceRm2;
            using (var pkg = PackageReader.Open(_extractedRoot))
            {
                pkg.ShadowDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "SavedChunks");
                using var stream = pkg.OpenByPath($"{entry.SourceLevel}.rm2");
                if (stream is null) { _browser.Log($"Import Object: source level '{entry.SourceLevel}.rm2' not found."); return; }
                sourceRm2 = new PS2AnyTwinsanityRM2();
                using var reader = new BinaryReader(stream);
                sourceRm2.Read(reader, (int)stream.Length);
            }

            // Amedo 2026-09-19
            uint instanceObjectId;
            var srcObjSec = sourceRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                                    ?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
            bool isGlobalObject = srcObjSec?.GetItem<PS2AnyObject>(entry.ObjectId) is null;
            if (isGlobalObject)
            {
                var globalObjSec = chunkSource.GlobalRm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                                                        ?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
                if (globalObjSec?.GetItem<PS2AnyObject>(entry.ObjectId) is null)
                {
                    _browser.Log($"Import Object: object 0x{entry.ObjectId:X4} isn't in source level '{entry.SourceLevel}' nor in the global data — can't import.");
                    return;
                }
                instanceObjectId = entry.ObjectId;
                _browser.Log($"Import Object: '{entry.Name}' (0x{entry.ObjectId:X4}) is a global/shared object — placing an instance that references it directly (no transplant needed).");
            }
            else
            {
                var rec = MeshDecoder.TransplantObjectFull(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables,
                    chunkSource.TexCache, sourceRm2, entry.ObjectId, out string log);
                if (rec is null) { _browser.Log($"Import Object: {log}"); return; }
                chunkSource.FullTransplants[rec.RootObjectId] = rec;
                instanceObjectId = rec.RootObjectId;
            }

            var section = AllEntities(chunkRoot).Select(x => x.Get<InstanceData>()?.Section).FirstOrDefault(sec => sec is not null);
            if (section is null)
            {
                var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_LAYOUT_1_SECTION);
                section = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION);
                if (section is null) { _browser.Log("Import Object: level has no instances section."); return; }
            }

            var cfg = entry.Instance;
            var copiedPositions = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPosition>(sourceRm2, chunkSource.Rm2, section,
                cfg.Positions.Select(p => (ushort)p).ToList(), (int)Constants.LAYOUT_POSITIONS_SECTION, out _);
            var copiedPaths = MeshDecoder.CopyInstanceLayoutRefs<PS2AnyPath>(sourceRm2, chunkSource.Rm2, section,
                cfg.Paths.Select(p => (ushort)p).ToList(), (int)Constants.LAYOUT_PATHS_SECTION, out _);

            var newInst = new PS2AnyInstance
            {
                Position              = new TwinVec4(0f, 0f, 0f, 1f),
                RotationX             = new TwinIntegerRotation(),
                RotationY             = new TwinIntegerRotation(),
                RotationZ             = new TwinIntegerRotation(),
                ObjectId              = (ushort)instanceObjectId,
                RefListIndex          = (short)cfg.RefListIndex,
                OnSpawnHeaderScriptID = (ushort)cfg.OnSpawnHeaderScriptID,
                StateFlags            = (uint)cfg.StateFlags,
                InstancesRelated      = 0, Instances = new List<ushort>(),
                PositionsRelated      = (ushort)cfg.PositionsRelated, Positions = copiedPositions,
                PathsRelated          = (ushort)cfg.PathsRelated,     Paths     = copiedPaths,
                ParamList1            = new List<uint>(cfg.ParamList1),
                ParamList2            = new List<float>(cfg.ParamList2),
                ParamList3            = new List<uint>(cfg.ParamList3),
            };

            var instRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "Instances") ?? chunkRoot;
            var newEntity = AddOrReuseInstance(section, newInst, instRoot, chunkSource);
            SelectClicked(newEntity, false);
            _browser.Log($"Assets: imported object '{entry.Name}' (0x{entry.ObjectId:X4}) at the origin (0,0,0). Save Chunk to keep it.");
        }
        catch (Exception ex) { _browser.Log($"Import Object failed: {ex.Message}"); }
    }
}
