using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private void DrawMyAssetsUnified()
    {
        var root = GetCustomSceneryAssetsRoot();
        var cur  = _assetsMyAssetsFolder;

        var subs    = CustomSceneryAssetLibrary.ListSubFolders(root, cur);
        var entries = CustomSceneryAssetLibrary.ListEntries(root, cur);
        if (subs.Count == 0 && entries.Count == 0)
            ImGui.TextDisabled(cur.Length == 0
                ? "  (empty -- right-click here for \"New Folder\", or select a scenery\n   tile and use \"Add to Assets...\" in the Inspector)"
                : "  (this folder is empty -- right-click for \"New Folder\", or add scenery)");

        const float cell = 84f;
        float avail = ImGui.GetContentRegionAvail().X;
        int columns = Math.Max(1, (int)(avail / (cell + 8f)));
        int col = 0;
        void Wrap() { if (++col < columns) ImGui.SameLine(); else col = 0; }

        foreach (var sub in subs)
        {
            if (!string.IsNullOrWhiteSpace(_assetsFilter) && !sub.Contains(_assetsFilter, StringComparison.OrdinalIgnoreCase)) continue;
            var subPath = cur.Length == 0 ? sub : System.IO.Path.Combine(cur, sub);
            ImGui.PushID("fld_" + sub);
            ImGui.BeginGroup();
            if (DrawFolderTile("##fldbtn", cell)) { _assetsMyAssetsFolder = subPath; _assetsFilter = ""; }
            if (ImGui.BeginPopupContextItem("##fldctx"))
            {
                if (ImGui.MenuItem("Open"))   { _assetsMyAssetsFolder = subPath; _assetsFilter = ""; }
                if (ImGui.MenuItem("Rename")) { _assetRenameIsObject = false; _assetRenameIsFolder = true; _assetRenameFolder = subPath; _assetRenameFile = ""; _assetRenameBuffer = sub; _assetRenameOpen = true; }
                if (ImGui.MenuItem("Delete")) { CustomSceneryAssetLibrary.DeleteFolder(root, subPath); _browser.Log($"Assets: deleted folder My Assets/{subPath}."); }
                ImGui.EndPopup();
            }
            ImGui.TextWrapped(sub);
            ImGui.EndGroup();
            ImGui.PopID();
            Wrap();
        }

        var allFolders = CustomSceneryAssetLibrary.ListAllFolders(root);
        foreach (var (fileName, entry) in entries)
        {
            if (!string.IsNullOrWhiteSpace(_assetsFilter) && !entry.Name.Contains(_assetsFilter, StringComparison.OrdinalIgnoreCase)) continue;
            var thumb = GetOrBakeCustomAssetThumbnail(cur, fileName, entry);
            ImGui.PushID(fileName);
            ImGui.BeginGroup();
            if (thumb is not null)
            {
                if (ImGui.ImageButton($"##thumb{fileName}", (nint)thumb.GlId, new Vector2(cell, cell), new Vector2(1, 1), new Vector2(0, 0)))
                    ImportCustomAssetEntry(cur, fileName, entry);
            }
            else ImGui.Button("?##nothumb", new Vector2(cell, cell));

            if (ImGui.BeginPopupContextItem($"##entctx{fileName}"))
            {
                if (ImGui.MenuItem("Rename")) { _assetRenameIsObject = false; _assetRenameIsFolder = false; _assetRenameFolder = cur; _assetRenameFile = fileName; _assetRenameBuffer = entry.Name; _assetRenameOpen = true; }
                if (ImGui.BeginMenu("Move to folder"))
                {
                    foreach (var f in allFolders)
                    {
                        if (string.Equals(f, cur, StringComparison.OrdinalIgnoreCase)) continue;
                        if (ImGui.MenuItem(f))
                        {
                            CustomSceneryAssetLibrary.MoveEntry(root, cur, fileName, f);
                            InvalidateCustomAssetCaches(cur, fileName);
                            _browser.Log($"Assets: moved '{entry.Name}' to My Assets/{f}.");
                        }
                    }
                    ImGui.EndMenu();
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Delete"))
                {
                    CustomSceneryAssetLibrary.DeleteEntry(root, cur, fileName);
                    InvalidateCustomAssetCaches(cur, fileName);
                    _browser.Log($"Assets: deleted '{entry.Name}'.");
                }
                ImGui.EndPopup();
            }
            ImGui.TextWrapped(entry.Name);
            ImGui.EndGroup();
            ImGui.PopID();
            Wrap();
        }

        foreach (var (fileName, oentry) in CustomSceneryAssetLibrary.ListObjectEntries(root, cur))
        {
            if (!string.IsNullOrWhiteSpace(_assetsFilter) && !oentry.Name.Contains(_assetsFilter, StringComparison.OrdinalIgnoreCase)) continue;
            var thumb = GetOrLoadObjectThumb(cur, fileName, oentry.ThumbSize > 0 ? oentry.ThumbSize : 96);
            ImGui.PushID("obj_" + fileName);
            ImGui.BeginGroup();
            if (thumb is not null)
            {
                if (ImGui.ImageButton($"##othumb{fileName}", (nint)thumb.GlId, new Vector2(cell, cell), new Vector2(1, 1), new Vector2(0, 0)))
                    ImportObjectAssetEntry(cur, fileName, oentry);
            }
            else if (ImGui.Button($"[obj]##{fileName}", new Vector2(cell, cell)))
                ImportObjectAssetEntry(cur, fileName, oentry);

            if (ImGui.BeginPopupContextItem($"##objctx{fileName}"))
            {
                if (ImGui.MenuItem("Rename")) { _assetRenameIsObject = true; _assetRenameIsFolder = false; _assetRenameFolder = cur; _assetRenameFile = fileName; _assetRenameBuffer = oentry.Name; _assetRenameOpen = true; }
                if (ImGui.BeginMenu("Move to folder"))
                {
                    foreach (var f in allFolders)
                    {
                        if (string.Equals(f, cur, StringComparison.OrdinalIgnoreCase)) continue;
                        if (ImGui.MenuItem(f))
                        {
                            CustomSceneryAssetLibrary.MoveObjectEntry(root, cur, fileName, f);
                            _objectThumbCache.Remove((cur, fileName));
                            _browser.Log($"Assets: moved object '{oentry.Name}' to My Assets/{f}.");
                        }
                    }
                    ImGui.EndMenu();
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Delete"))
                {
                    CustomSceneryAssetLibrary.DeleteObjectEntry(root, cur, fileName);
                    _objectThumbCache.Remove((cur, fileName));
                    _browser.Log($"Assets: deleted object '{oentry.Name}'.");
                }
                ImGui.EndPopup();
            }
            ImGui.TextWrapped(oentry.Name);
            ImGui.EndGroup();
            ImGui.PopID();
            Wrap();
        }

        if (ImGui.BeginPopupContextWindow("##myassetsbg", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            if (ImGui.MenuItem("New Folder"))
            {
                var leaf    = CustomSceneryAssetLibrary.UniqueNewFolderLeaf(root, cur);
                var created = CustomSceneryAssetLibrary.CreateSubFolder(root, cur, leaf);
                _assetRenameIsObject = false; _assetRenameIsFolder = true; _assetRenameFolder = created; _assetRenameFile = "";
                _assetRenameBuffer = System.IO.Path.GetFileName(created); _assetRenameOpen = true;
            }
            ImGui.EndPopup();
        }
    }

    private static bool DrawFolderTile(string id, float cell)
    {
        var pos = ImGui.GetCursorScreenPos();
        bool clicked = ImGui.InvisibleButton(id, new Vector2(cell, cell));
        var dl = ImGui.GetWindowDrawList();
        uint fill = ImGui.GetColorU32(ImGui.IsItemHovered() ? new Vector4(0.87f, 0.81f, 0.56f, 1f) : new Vector4(0.75f, 0.70f, 0.49f, 1f));
        float p = cell * 0.16f;
        var a = new Vector2(pos.X + p, pos.Y + p * 1.7f);
        var b = new Vector2(pos.X + cell - p, pos.Y + cell - p);
        dl.AddRectFilled(new Vector2(a.X, a.Y - p * 0.8f), new Vector2(a.X + (b.X - a.X) * 0.42f, a.Y + p * 0.5f), fill, 3f);
        dl.AddRectFilled(a, b, fill, 5f);
        return clicked;
    }

    private void DrawAssetRenamePopup()
    {
        if (_assetRenameOpen) { ImGui.OpenPopup("Rename##assetrename"); _assetRenameOpen = false; }
        if (!ImGui.BeginPopup("Rename##assetrename")) return;
        ImGui.TextUnformatted(_assetRenameIsFolder ? "Rename folder:" : "Rename asset:");
        ImGui.SetNextItemWidth(220f);
        bool commit = ImGui.InputText("##assetrenamebuf", ref _assetRenameBuffer, 64, ImGuiInputTextFlags.EnterReturnsTrue);
        bool ok = ImGui.Button("OK", new Vector2(80f, 0f)) || commit;
        ImGui.SameLine();
        bool cancel = ImGui.Button("Cancel", new Vector2(80f, 0f));
        if (ok)
        {
            var root = GetCustomSceneryAssetsRoot();
            if (_assetRenameIsFolder)
            {
                var parent = System.IO.Path.GetDirectoryName(_assetRenameFolder) ?? "";
                CustomSceneryAssetLibrary.RenameFolder(root, _assetRenameFolder, _assetRenameBuffer);
                if (_assetsMyAssetsFolder.StartsWith(_assetRenameFolder, StringComparison.OrdinalIgnoreCase))
                    _assetsMyAssetsFolder = parent;
            }
            else if (_assetRenameIsObject)
            {
                CustomSceneryAssetLibrary.RenameObjectEntry(root, _assetRenameFolder, _assetRenameFile, _assetRenameBuffer);
                _objectThumbCache.Remove((_assetRenameFolder, _assetRenameFile));
            }
            else
            {
                CustomSceneryAssetLibrary.RenameEntry(root, _assetRenameFolder, _assetRenameFile, _assetRenameBuffer);
                InvalidateCustomAssetCaches(_assetRenameFolder, _assetRenameFile);
            }
            ImGui.CloseCurrentPopup();
        }
        else if (cancel) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private readonly Dictionary<(string Folder, string FileName), PS2AnyTwinsanitySM2?> _customAssetSm2Cache = new();
    private readonly Dictionary<(string Folder, string FileName), Dictionary<uint, Texture2D>> _customAssetTexCache = new();

    private (PS2AnyTwinsanitySM2? Sm2, Dictionary<uint, Texture2D> TexCache) LoadCustomAssetContainer(string folder, string fileName, CustomSceneryAssetEntry entry)
    {
        var key = (folder, fileName);
        if (!_customAssetSm2Cache.TryGetValue(key, out var sm2))
            _customAssetSm2Cache[key] = sm2 = CustomSceneryAssetLibrary.LoadContainer(GetCustomSceneryAssetsRoot(), folder, entry.ContainerFile);
        if (!_customAssetTexCache.TryGetValue(key, out var texCache))
            _customAssetTexCache[key] = texCache = new Dictionary<uint, Texture2D>();
        return (sm2, texCache);
    }

    private void InvalidateCustomAssetCaches(string folder, string fileName)
    {
        var key = (folder, fileName);
        _assetsCustomThumbCache.Remove(key);
        _customAssetSm2Cache.Remove(key);
        _customAssetTexCache.Remove(key);
    }

    private Texture2D? GetOrBakeCustomAssetThumbnail(string folder, string fileName, CustomSceneryAssetEntry entry)
    {
        var key = (folder, fileName);
        if (_assetsCustomThumbCache.TryGetValue(key, out var cached)) return cached;

        var (sm2, texCache) = LoadCustomAssetContainer(folder, fileName, entry);
        if (sm2 is null) { _assetsCustomThumbCache[key] = null; return null; }

        var gl = Engine.Instance.GL;
        var parts = MeshDecoder.DecodeSceneryTilesPreview(gl, sm2, texCache, entry.TileIndices);
        if (parts is null || parts.Count == 0) { _assetsCustomThumbCache[key] = null; return null; }

        var tex = BakeThumbnailFromParts(gl, parts);
        _assetsCustomThumbCache[key] = tex;
        return tex;
    }

    private void ImportCustomAssetEntry(string folder, string fileName, CustomSceneryAssetEntry entry)
    {
        var (sourceSm2, _) = LoadCustomAssetContainer(folder, fileName, entry);
        if (sourceSm2 is null)
        {
            _browser.Log($"Assets: couldn't load the saved copy for '{entry.Name}' ({entry.ContainerFile} missing or unreadable).");
            return;
        }

        if (SceneryImportTarget() is not ({ } chunkSource, { } destSm2, { } sceneryRoot)) return;

        var spawnPos = Vector3.Zero;

        var newIds = new List<uint>();
        if (!MeshDecoder.TransplantSceneryTiles(Engine.Instance.GL, destSm2, chunkSource.SceneryTexCache,
                sourceSm2, entry.TileIndices, out string log, targetPosition: spawnPos, newSourceIds: newIds))
        {
            _browser.Log($"Assets: {log}");
            return;
        }
        var newIdSet = new HashSet<uint>(newIds);

        RebuildSceneryKeepingMoves(sceneryRoot, chunkSource, destSm2);

        var justImported = sceneryRoot.Children.Where(c =>
            c.Get<SceneryTile>() is { } t && newIdSet.Contains(t.SourceId))
            .ToList();
        foreach (var e in justImported) SelectClicked(e, e != justImported[0]);

        _browser.Log($"Assets: imported '{entry.Name}' ({entry.TileIndices.Count} tile(s)) from My Assets -> {log}. " +
                      "Not undoable -- Reload Level from Disc to undo. Save Chunk to persist.");
    }
}
