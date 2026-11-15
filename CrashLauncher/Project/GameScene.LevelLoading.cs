using System.Diagnostics;
using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Renderer;
using CrashEngine.Runtime;
using ImGuiNET;
using Silk.NET.OpenGL;

namespace CrashLauncher;

// Amedo 2026-11-12: a level opens without stopping the editor. Reading it and matching its collision run in the background
// (the same build lane and GPU queue CrashGame uses for its levels), its GPU data goes up a few milliseconds each frame, and
// a loading screen shows meanwhile. If the background read fails, the level loads the old way in one frame.
public sealed partial class GameScene
{
    public static bool LoadLevelsInBackground { get; set; } = true;
    private const double LevelUploadBudgetMs = 6.0;
    private static readonly string[] MarkerModelFileNames = { "AiSkull.glb", "LightBulb.glb", "PositionPin.glb", "CameraIcon.glb", "GameCamera.glb" };

    private sealed class LevelLoad(RenderPipeline pipeline, bool useShadowDir, Action? then)
    {
        public RenderPipeline Pipeline { get; } = pipeline;
        public bool UseShadowDir { get; } = useShadowDir;
        public Action? Then { get; } = then;
        public GpuUploadQueue Uploads { get; } = new();
        public Stopwatch Clock { get; } = Stopwatch.StartNew();
        public Task<LevelParts?>? Read;
        public Task<CollisionTileMatch?>? Match;
        public LevelParts? Parts;

        public string Step =>
            Parts is null ? "Reading the scene..." :
            Uploads.Done < Uploads.Count ? $"Sending it to the GPU ({Uploads.Done}/{Uploads.Count})" :
            "Matching the collision to the scenery...";

        public float? Progress => Parts is null ? null : Uploads.Count == 0 ? 1f : (float)Uploads.Done / Uploads.Count;
    }

    private LevelLoad? _levelLoad;

    public bool LevelLoading => _levelLoad is not null;

    private void BeginLevelLoad(GL gl, RenderPipeline pipeline, bool useShadowDir, Action? then = null)
    {
        if (!LoadLevelsInBackground)
        {
            LoadLevelEntities(gl, pipeline, useShadowDir);
            then?.Invoke();
            return;
        }
        (string Source, string? ShadowDir) package;
        try
        {
            StartLevelLoad();
            package = LevelPackage(useShadowDir);
        }
        catch (Exception ex)
        {
            LevelLoadFailed(ex);
            then?.Invoke();
            return;
        }
        var load = new LevelLoad(pipeline, useShadowDir, then);
        load.Read = SceneSimulation.OnBuildLane(() => ReadInBackground(gl, package.Source, package.ShadowDir, load.Uploads));
        _levelLoad = load;
    }

    private LevelParts? ReadInBackground(GL gl, string source, string? shadowDir, GpuUploadQueue uploads)
    {
        try
        {
            var parts = ReadLevel(gl, source, shadowDir, uploads);
            uploads.Warm();
            foreach (var file in MarkerModelFileNames) ReadMarkerModel(file);
            return parts;
        }
        catch (Exception ex)
        {
            _browser.Log($"Background load failed, loading it the old way: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private CollisionTileMatch? MatchInBackground(Entity root)
    {
        try { return MatchCollisionTiles(root); }
        catch (Exception ex)
        {
            _browser.Log($"Collision baseline failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // Amedo 2026-11-12: true while the level is still loading (the editor waits; only the loading screen draws)
    private bool UpdateLevelLoad()
    {
        if (_levelLoad is not { } load) return false;
        var gl = Engine.Instance.GL;
        if (load.Parts is null)
        {
            if (!load.Read!.IsCompleted) return true;
            if (load.Read.Result is not { } parts)
            {
                _levelLoad = null;
                LoadLevelEntities(gl, load.Pipeline, load.UseShadowDir);
                load.Then?.Invoke();
                return false;
            }
            try { PrepareLoadedLevel(parts.Root); }
            catch (Exception ex)
            {
                _levelLoad = null;
                LevelLoadFailed(ex);
                load.Then?.Invoke();
                return false;
            }
            load.Parts = parts;
            load.Match = Task.Run(() => MatchInBackground(parts.Root));
        }
        if (!load.Uploads.Upload(LevelUploadBudgetMs) || !load.Match!.IsCompleted) return true;
        _levelLoad = null;
        try { FinishLevelLoad(gl, load.Pipeline, load.UseShadowDir, load.Parts, load.Match.Result); }
        catch (Exception ex) { LevelLoadFailed(ex); }
        load.Then?.Invoke();
        _browser.Log($"Scene ready in {load.Clock.ElapsedMilliseconds} ms ({load.Uploads.Count} GPU items in {load.Uploads.Slices} frames).");
        return false;
    }

    private bool DrawLevelLoading()
    {
        if (_levelLoad is not { } load) return false;
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.WorkPos);
        ImGui.SetNextWindowSize(vp.WorkSize);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.07f, 0.07f, 0.09f, 1f));
        ImGui.Begin("##levelloading", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoNav);
        ImGui.PopStyleColor();

        const float width = 420f, barHeight = 6f;
        var center = vp.WorkPos + vp.WorkSize * 0.5f;
        var bar = new Vector2(MathF.Floor(center.X - width * 0.5f), MathF.Floor(center.Y));
        var dl = ImGui.GetWindowDrawList();
        dl.AddText(bar - new Vector2(0f, 46f), ImGui.GetColorU32(ImGuiCol.Text), $"Opening {GamePaths.FileNameWithoutExtension(_rm2)}");
        dl.AddText(bar - new Vector2(0f, 24f), ImGui.GetColorU32(ImGuiCol.TextDisabled), load.Step);
        dl.AddRectFilled(bar, bar + new Vector2(width, barHeight), ImGui.GetColorU32(new Vector4(0.25f, 0.25f, 0.28f, 1f)));
        uint fill = ImGui.GetColorU32(new Vector4(0.97f, 0.55f, 0.12f, 1f));
        if (load.Progress is { } done)
            dl.AddRectFilled(bar, bar + new Vector2(width * done, barHeight), fill);
        else
        {
            float t = (float)(load.Clock.Elapsed.TotalSeconds % 1.2 / 1.2) * 1.25f;
            float from = Math.Max(0f, t - 0.25f), to = Math.Min(1f, t);
            dl.AddRectFilled(bar + new Vector2(width * from, 0f), bar + new Vector2(width * to, barHeight), fill);
        }
        ImGui.End();
        return true;
    }
}
