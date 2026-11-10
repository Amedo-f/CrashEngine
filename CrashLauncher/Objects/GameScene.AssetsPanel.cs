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

    private bool _showProject = true;

    private void DrawAssetsPanel()
    {
        if (!_showProject) return;
        int sw = Engine.Instance.Width, sh = Engine.Instance.Height;
        ImGui.SetNextWindowPos(new Vector2(sw - 420f, (float)sh - 180f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(420f, 180f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(240f, 120f), new Vector2(float.MaxValue, float.MaxValue));
        if (!_windowChrome.Begin("Project##panel", ref _showProject)) return; // Amedo

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
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
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
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
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

            CrashEngine.Renderer.GlBlend.Force(gl, false); // Amedo 2026-09-30
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
        if (SceneryImportTarget() is not ({ } chunkSource, { } destSm2, { } sceneryRoot)) return;

        var camPos = _camera?.Transform.Position ?? Vector3.Zero;
        if (!MeshDecoder.TransplantSceneryTiles(Engine.Instance.GL, destSm2, chunkSource.SceneryTexCache,
                sourceSm2, new List<int> { tileIndex }, out string log, targetPosition: camPos))
        {
            _browser.Log($"Assets: {log}");
            return;
        }

        RebuildSceneryKeepingMoves(sceneryRoot, chunkSource, destSm2);

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

    private (ChunkSource ChunkSource, PS2AnyTwinsanitySM2 Sm2, Entity SceneryRoot)? SceneryImportTarget()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var destSm2 = chunkSource?.Sm2;
        var sceneryRoot = chunkRoot is null ? null : AllEntities(chunkRoot).FirstOrDefault(x => x.Name == "Scenery");
        if (chunkSource is null || destSm2 is null || sceneryRoot is null)
        { _browser.Log("Assets: this chunk has no Scenery root to import into."); return null; }
        return (chunkSource, destSm2, sceneryRoot);
    }

    private static void RebuildSceneryKeepingMoves(Entity sceneryRoot, ChunkSource chunkSource, PS2AnyTwinsanitySM2 sm2)
    {
        var preMoves = CaptureLiveSceneryTransforms(sceneryRoot);
        foreach (var child in sceneryRoot.Children.ToList())
            sceneryRoot.RemoveChild(child);
        chunkSource.SceneryTables = MeshDecoder.BuildSceneryMeshes(Engine.Instance.GL, sm2, sceneryRoot, chunkSource.SceneryTexCache, applyGlobalEffects: false);
        ReapplyLiveSceneryTransforms(sceneryRoot, preMoves);
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

}
