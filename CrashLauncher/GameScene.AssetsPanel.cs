using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using Silk.NET.OpenGL;
using System.Numerics;
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private string _assetsLevel    = "";
    private string _assetsCategory = "";
    private string _assetsFilter   = "";

    private List<string>? _assetsLevelList;
    private readonly Dictionary<string, PS2AnyTwinsanitySM2?> _assetsSm2Cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<uint, Texture2D>> _assetsTexCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Level, int Tile), Texture2D?> _assetsThumbCache = new();
    private PreviewFbo? _assetsThumbFbo;

    private bool   _assetsMyAssets = true;
    private string _assetsMyAssetsFolder = "";
    private readonly Dictionary<(string Folder, string FileName), Texture2D?> _assetsCustomThumbCache = new();
    private bool   _assetRenameOpen;
    private bool   _assetRenameIsFolder;
    private bool   _assetRenameIsObject;
    private string _assetRenameFolder = "";
    private string _assetRenameFile   = "";
    private string _assetRenameBuffer = "";

    private void DrawAssetsPanel()
    {
        int sw = Engine.Instance.Width, sh = Engine.Instance.Height;
        ImGui.SetNextWindowPos(new Vector2(sw - 420f, (float)sh - 180f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(420f, 180f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(240f, 120f), new Vector2(float.MaxValue, float.MaxValue));
        ImGui.Begin("Assets", ImGuiWindowFlags.NoCollapse);

        if (ImGui.SmallButton("Assets"))
        {
            _assetsLevel = ""; _assetsCategory = ""; _assetsFilter = "";
            _assetsMyAssets = true; _assetsMyAssetsFolder = "";
        }
        if (_assetsMyAssets)
        {
            ImGui.SameLine(); ImGui.TextDisabled(">");
            ImGui.SameLine();
            if (ImGui.SmallButton("My Assets")) { _assetsMyAssetsFolder = ""; _assetsFilter = ""; }
            if (_assetsMyAssetsFolder.Length > 0)
            {
                var segs = _assetsMyAssetsFolder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                var accum = "";
                for (int i = 0; i < segs.Length; i++)
                {
                    accum = i == 0 ? segs[i] : accum + System.IO.Path.DirectorySeparatorChar + segs[i];
                    ImGui.SameLine(); ImGui.TextDisabled(">"); ImGui.SameLine();
                    if (ImGui.SmallButton($"{segs[i]}##crumb{i}")) { _assetsMyAssetsFolder = accum; _assetsFilter = ""; }
                }
            }
        }
        if (_assetsLevel.Length > 0)
        {
            ImGui.SameLine(); ImGui.TextDisabled(">");
            ImGui.SameLine();
            var levelName = System.IO.Path.GetFileName(_assetsLevel);
            if (ImGui.SmallButton(levelName)) { _assetsCategory = ""; _assetsFilter = ""; }
        }
        if (_assetsCategory.Length > 0)
        {
            ImGui.SameLine(); ImGui.TextDisabled(">");
            ImGui.SameLine();
            ImGui.TextUnformatted(_assetsCategory);
        }
        ImGui.Separator();

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##assetsFilter", "Search...", ref _assetsFilter, 128);

        ImGui.BeginChild("##assetsBody", Vector2.Zero, ImGuiChildFlags.Border);
        if (_assetsMyAssets)
            DrawMyAssetsUnified();
        else if (_assetsLevel.Length == 0)
            DrawAssetsLevelList();
        else if (_assetsCategory.Length == 0)
            DrawAssetsCategoryList();
        else
            DrawAssetsSceneryGrid();
        ImGui.EndChild();

        DrawAssetRenamePopup();

        ImGui.End();
    }

    private List<string> GetAssetsLevelList()
    {
        if (_assetsLevelList is not null) return _assetsLevelList;
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var pkg = PackageReader.Open(GetPristineArchiveSource());
            foreach (var rec in pkg.GetByExtension(".rm2"))
            {
                var rel = rec.Path.Replace('/', '\\');
                if (rel.EndsWith(@"Startup\Default.rm2", StringComparison.OrdinalIgnoreCase)) continue;
                if (seen.Add(rel)) list.Add(rel);
            }
        }
        catch (Exception ex) { _browser.Log($"Assets: level scan failed: {ex.Message}"); }

        try
        {
            var savedChunksDir = SavedChunksDir;
            if (Directory.Exists(savedChunksDir))
                foreach (var f in Directory.GetFiles(savedChunksDir, "*.rm2", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(savedChunksDir, f);
                    if (seen.Add(rel)) list.Add(rel);
                }
        }
        catch (Exception ex) { _browser.Log($"Assets: SavedChunks scan failed: {ex.Message}"); }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        _assetsLevelList = list;
        return list;
    }

    private void DrawAssetsLevelList()
    {
        if (ImGui.Selectable("  ⭐ My Assets"))
        {
            _assetsMyAssets = true;
            _assetsMyAssetsFolder = "";
            _assetsFilter = "";
        }
        ImGui.Separator();
        foreach (var lvl in GetAssetsLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_assetsFilter) &&
                !lvl.Contains(_assetsFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable($"  {lvl}"))
            {
                _assetsLevel = lvl.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? lvl[..^4] : lvl;
                _assetsCategory = "";
                _assetsFilter = "";
            }
        }
    }

    private void DrawAssetsCategoryList()
    {
        var sm2 = GetAssetsSm2(_assetsLevel);
        var tileCount = sm2 is null ? 0 : MeshDecoder.GetSceneryTileList(sm2).Count;
        ImGui.BeginDisabled(tileCount == 0);
        if (ImGui.Selectable($"  📦 Scenery ({tileCount})"))
            _assetsCategory = "Scenery";
        ImGui.EndDisabled();
        if (tileCount == 0)
            ImGui.TextDisabled("  (this level has no scenery tiles)");
        ImGui.TextDisabled("  Characters / Enemies -- not yet implemented (follow-up)");
    }

    private PS2AnyTwinsanitySM2? GetAssetsSm2(string levelBasePath)
    {
        if (_assetsSm2Cache.TryGetValue(levelBasePath, out var cached)) return cached;
        PS2AnyTwinsanitySM2? sm2 = null;
        try
        {
            using var pkg = PackageReader.Open(GetPristineArchiveSource());
            pkg.ShadowDir = SavedChunksDir;
            using var stream = pkg.OpenByPath($"{levelBasePath}.sm2");
            if (stream is not null)
            {
                sm2 = new PS2AnyTwinsanitySM2();
                using var reader = new BinaryReader(stream);
                sm2.Read(reader, (int)stream.Length);
            }
        }
        catch (Exception ex) { _browser.Log($"Assets: failed to open {levelBasePath}.sm2: {ex.Message}"); }
        _assetsSm2Cache[levelBasePath] = sm2;
        return sm2;
    }

    private void DrawAssetsSceneryGrid()
    {
        var sm2 = GetAssetsSm2(_assetsLevel);
        if (sm2 is null) { ImGui.TextDisabled("(couldn't load this level's scenery)"); return; }
        if (!_assetsTexCache.TryGetValue(_assetsLevel, out var texCache))
            _assetsTexCache[_assetsLevel] = texCache = new Dictionary<uint, Texture2D>();

        const float cell = 84f;
        float avail = ImGui.GetContentRegionAvail().X;
        int columns = Math.Max(1, (int)(avail / (cell + 8f)));
        int col = 0;

        foreach (var tile in MeshDecoder.GetSceneryTileList(sm2))
        {
            var label = $"tile {tile.Index}";
            if (!string.IsNullOrWhiteSpace(_assetsFilter) &&
                !label.Contains(_assetsFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var thumb = GetOrBakeSceneryThumbnail(_assetsLevel, tile.Index, sm2, texCache);
            ImGui.PushID(tile.Index);
            ImGui.BeginGroup();
            if (thumb is not null)
            {
                if (ImGui.ImageButton($"##thumb{tile.Index}", (nint)thumb.GlId, new Vector2(cell, cell),
                        new Vector2(1, 1), new Vector2(0, 0)))
                    ImportSingleSceneryTile(_assetsLevel, tile.Index, sm2);
                if (ImGui.IsItemHovered())
                    MaybeTooltip($"Tile {tile.Index} — {tile.MeshCount} mesh(es). Click to import this ONE\n" +
                                 "tile (fresh graphics ids) at the camera position. Not undoable.");
            }
            else
            {
                ImGui.Button("?##nothumb", new Vector2(cell, cell));
            }
            ImGui.TextWrapped(label);
            ImGui.EndGroup();
            ImGui.PopID();

            if (++col < columns) ImGui.SameLine(); else col = 0;
        }
    }

    private Texture2D? GetOrBakeSceneryThumbnail(string levelBasePath, int tileIndex,
        PS2AnyTwinsanitySM2 sm2, Dictionary<uint, Texture2D> texCache)
    {
        var key = (levelBasePath, tileIndex);
        if (_assetsThumbCache.TryGetValue(key, out var cached)) return cached;

        var gl = Engine.Instance.GL;
        var parts = MeshDecoder.DecodeSceneryTilePreview(gl, sm2, texCache, tileIndex);
        if (parts is null || parts.Count == 0) { _assetsThumbCache[key] = null; return null; }

        var tex = BakeThumbnailFromParts(gl, parts);
        _assetsThumbCache[key] = tex;
        return tex;
    }

    private Texture2D BakeThumbnailFromParts(GL gl, List<(GpuMesh Mesh, Material Mat, Matrix4x4 Model)> parts)
        => new Texture2D(gl, BakeThumbnailPixelsFromParts(gl, parts, out int size), (uint)size, (uint)size);

    private byte[] BakeThumbnailPixelsFromParts(GL gl, List<(GpuMesh Mesh, Material Mat, Matrix4x4 Model)> parts, out int size)
    {
        size = 96;
        _assetsThumbFbo ??= new PreviewFbo(gl, size, size);
        _assetsThumbFbo.Resize(size, size);
        var bg = new Vector3(0.16f, 0.16f, 0.19f);
        _assetsThumbFbo.Begin(bg);

        var pipeline = RenderPipeline.Instance;
        if (pipeline is not null)
        {
            const float fovY = 50f * MathF.PI / 180f;
            var bmin = new Vector3(float.MaxValue);
            var bmax = new Vector3(float.MinValue);
            bool anyVerts = false;
            foreach (var (mesh, _, model) in parts)
            {
                if (mesh.RaycastPositions is not { } positions) continue;
                foreach (var p in positions)
                {
                    var wp = Vector3.Transform(p, model);
                    bmin = Vector3.Min(bmin, wp);
                    bmax = Vector3.Max(bmax, wp);
                    anyVerts = true;
                }
            }

            Vector3 center; float radius;
            if (anyVerts)
            {
                center = (bmin + bmax) * 0.5f;
                radius = MathF.Max(Vector3.Distance(bmin, bmax) * 0.5f, 0.05f);
            }
            else
            {
                var centers = parts.Select(p => p.Model.Translation).ToList();
                center = centers.Count == 0 ? Vector3.Zero : centers.Aggregate(Vector3.Zero, (a, b) => a + b) / centers.Count;
                radius = 2f;
            }
            float dist = radius / MathF.Sin(fovY * 0.5f) * 1.15f;

            var sh = pipeline.Shader;
            sh.Use();
            float yr = 35f * MathF.PI / 180f, pr = 25f * MathF.PI / 180f;
            var eye = center + new Vector3(MathF.Cos(pr) * MathF.Sin(yr), MathF.Sin(pr), MathF.Cos(pr) * MathF.Cos(yr)) * dist;
            var view = CameraComponent.CreateLookAtRH(eye, center, Vector3.UnitY);
            var proj = CameraComponent.CreatePerspectiveRH(fovY, 1f, MathF.Max(0.01f, dist * 0.01f), MathF.Max(20000f, dist * 4f));
            sh.Set("StartView", view);
            sh.Set("StartProjection", proj);
            sh.Set("EyePosition", eye);
            sh.Set("EyeDirection", Vector3.Normalize(center - eye));
            sh.Set("Resolution", new Vector2(size, size));
            sh.Set("Time", 0f);
            sh.Set("FogColor", bg);
            sh.Set("Diffuse", Vector4.One);
            sh.Set("Opacity", 1f);
            sh.Set("FlipY", 0f);
            sh.Set("DiffuseOnly", 0f);
            sh.SetMatrixArray("BoneMatrices", RenderPipeline.IdentityBoneMatrices);

            gl.Disable(EnableCap.Blend);
            foreach (var (mesh, mat, model) in parts)
            {
                sh.Set("StartModel", model);
                mat.FogEnabled = false;
                mat.Apply(gl, sh);
                mesh.Draw(gl);
                mat.Restore(gl, sh);
            }
        }

        var pixels = _assetsThumbFbo.ReadPixels();
        _assetsThumbFbo.End(Engine.Instance.Width, Engine.Instance.Height);
        return pixels;
    }

    private void ImportSingleSceneryTile(string sourceLevelBasePath, int tileIndex, PS2AnyTwinsanitySM2 sourceSm2)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var destSm2 = chunkSource?.Sm2;
        var sceneryRoot = chunkRoot is null ? null : AllEntities(chunkRoot).FirstOrDefault(x => x.Name == "Scenery");
        if (destSm2 is null || sceneryRoot is null)
        { _browser.Log("Assets: this chunk has no Scenery root to import into."); return; }

        var camPos = _camera?.Transform.Position ?? Vector3.Zero;
        if (!MeshDecoder.TransplantSceneryTiles(Engine.Instance.GL, destSm2, chunkSource!.SceneryTexCache,
                sourceSm2, new List<int> { tileIndex }, out string log, targetPosition: camPos))
        {
            _browser.Log($"Assets: {log}");
            return;
        }

        var preMoves = CaptureLiveSceneryTransforms(sceneryRoot);

        foreach (var child in sceneryRoot.Children.ToList())
            sceneryRoot.RemoveChild(child);
        chunkSource.SceneryTables = MeshDecoder.BuildSceneryMeshes(Engine.Instance.GL, destSm2, sceneryRoot, chunkSource.SceneryTexCache, applyGlobalEffects: false);
        ReapplyLiveSceneryTransforms(sceneryRoot, preMoves);

        var justImported = sceneryRoot.Children.FirstOrDefault(c =>
            c.Get<SceneryTile>() is { } t &&
            Vector3.DistanceSquared(new Vector3(t.Source.Column4.X, t.Source.Column4.Y, t.Source.Column4.Z), camPos) < 0.001f);
        if (justImported is not null) SelectClicked(justImported, false);

        _browser.Log($"Assets: imported tile {tileIndex} from '{sourceLevelBasePath}' -> {log}. " +
                      "Not undoable -- Reload Level from Disc to undo. Save Chunk to persist.");
    }

    private static (uint, long, long, long) SceneryKey(SceneryTile tile)
    {
        var p = tile.Source.Column4;
        return (tile.SourceId,
                (long)MathF.Round(p.X / 0.01f), (long)MathF.Round(p.Y / 0.01f), (long)MathF.Round(p.Z / 0.01f));
    }

    private static Dictionary<(uint, long, long, long), Matrix4x4> CaptureLiveSceneryTransforms(Entity sceneryRoot)
    {
        var map = new Dictionary<(uint, long, long, long), Matrix4x4>();
        void Visit(Entity e)
        {
            if (e.Get<SceneryTile>() is { } tile && tile.Source is not null) map[SceneryKey(tile)] = e.Transform.Local;
            foreach (var c in e.Children) Visit(c);
        }
        Visit(sceneryRoot);
        return map;
    }

    private static void ReapplyLiveSceneryTransforms(Entity sceneryRoot, Dictionary<(uint, long, long, long), Matrix4x4> pre)
    {
        void Visit(Entity e)
        {
            if (e.Get<SceneryTile>() is { } tile && tile.Source is not null && pre.TryGetValue(SceneryKey(tile), out var m))
                e.Transform.LocalMatrix = m;
            foreach (var c in e.Children) Visit(c);
        }
        Visit(sceneryRoot);
    }

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
            if (ImGui.IsItemHovered()) MaybeTooltip($"{sub}\nClick to open. Right-click to rename/delete.");
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
                if (ImGui.IsItemHovered())
                    MaybeTooltip($"{entry.Name} — {entry.TileIndices.Count} tile(s), independent copy.\n" +
                                 "Click to import at the camera. Right-click to rename/move/delete.");
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
            if (ImGui.IsItemHovered())
                MaybeTooltip($"{oentry.Name} — object 0x{oentry.ObjectId:X4} (full data + settings).\nClick to import into this level. Right-click to rename/move/delete.\nSource level: {oentry.SourceLevel}");

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

        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var destSm2 = chunkSource?.Sm2;
        var sceneryRoot = chunkRoot is null ? null : AllEntities(chunkRoot).FirstOrDefault(x => x.Name == "Scenery");
        if (destSm2 is null || sceneryRoot is null)
        { _browser.Log("Assets: this chunk has no Scenery root to import into."); return; }

        var spawnPos = Vector3.Zero;

        var newIds = new List<uint>();
        if (!MeshDecoder.TransplantSceneryTiles(Engine.Instance.GL, destSm2, chunkSource!.SceneryTexCache,
                sourceSm2, entry.TileIndices, out string log, targetPosition: spawnPos, newSourceIds: newIds))
        {
            _browser.Log($"Assets: {log}");
            return;
        }
        var newIdSet = new HashSet<uint>(newIds);

        // Amedo 2026-09-16
        var preMoves = CaptureLiveSceneryTransforms(sceneryRoot);

        foreach (var child in sceneryRoot.Children.ToList())
            sceneryRoot.RemoveChild(child);
        chunkSource.SceneryTables = MeshDecoder.BuildSceneryMeshes(Engine.Instance.GL, destSm2, sceneryRoot, chunkSource.SceneryTexCache, applyGlobalEffects: false);
        ReapplyLiveSceneryTransforms(sceneryRoot, preMoves);

        var justImported = sceneryRoot.Children.Where(c =>
            c.Get<SceneryTile>() is { } t && newIdSet.Contains(t.SourceId))
            .ToList();
        foreach (var e in justImported) SelectClicked(e, e != justImported[0]);

        _browser.Log($"Assets: imported '{entry.Name}' ({entry.TileIndices.Count} tile(s)) from My Assets -> {log}. " +
                      "Not undoable -- Reload Level from Disc to undo. Save Chunk to persist.");
    }
}
