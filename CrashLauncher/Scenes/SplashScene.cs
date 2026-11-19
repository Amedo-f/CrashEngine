using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Renderer;
using ImGuiNET;

namespace CrashLauncher;

// Amedo 2026-11-12
public sealed class SplashScene : Scene
{
    private readonly CrashProject _project;
    private readonly string _scriptOut;
    private readonly CancellationTokenSource _cts = new();
    private readonly ManualResetEventSlim _gate = new(true);
    private Texture2D? _art, _logo;
    private volatile float _progress;
    private volatile string _step = "Starting...";
    private volatile string? _error;
    private volatile bool _done;

    public SplashScene(CrashProject project, string scriptOut)
    {
        _project = project;
        _scriptOut = scriptOut;
    }

    protected override void Build()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Splash");
        _art = Texture2D.FromFile(Engine.Instance.GL, Path.Combine(dir, "splash.png"));
        _logo = Texture2D.FromFile(Engine.Instance.GL, Path.Combine(dir, "logo.png"));
        Engine.Instance.SetTitleDetail($"Creating {_project.Name}");
        Task.Run(() =>
        {
            try
            {
                ProjectCreator.Build(_project, s => { _step = s; Console.WriteLine(s); }, p => _progress = p, _cts.Token, _gate);
                _done = true;
            }
            catch (OperationCanceledException) { _error = "Cancelled. The project keeps what was unpacked; finish it later from the start screen."; }
            catch (Exception ex) { _error = $"ERROR: {ex.Message}"; }
        });
    }

    public override void OnImGuiRender()
    {
        if (_done)
        {
            _done = false;
            Engine.Instance.SetTitleDetail("");
            ProjectCreator.ShowEditor(_project.DiscContentPathPS2!, _project.ProjectPath);
            return;
        }
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.WorkPos);
        ImGui.SetNextWindowSize(vp.WorkSize);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.05f, 0.05f, 0.07f, 1f));
        ImGui.Begin("##splash", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleColor();
        var dl = ImGui.GetWindowDrawList();
        float w = Math.Min(vp.WorkSize.X - 80f, 1000f), artH = w * 0.52f, bandH = 170f;
        var top = vp.WorkPos + new Vector2((vp.WorkSize.X - w) * 0.5f, Math.Max(20f, (vp.WorkSize.Y - artH - bandH) * 0.5f));
        if (_art is not null) dl.AddImage((nint)_art.GlId, top, top + new Vector2(w, artH));
        var barTop = top + new Vector2(0f, artH);
        dl.AddRectFilled(barTop, barTop + new Vector2(w, 6f), ImGui.GetColorU32(new Vector4(0.85f, 0.85f, 0.85f, 1f)));
        dl.AddRectFilled(barTop, barTop + new Vector2(w * Math.Clamp(_progress, 0f, 1f), 6f), ImGui.GetColorU32(new Vector4(0.97f, 0.55f, 0.12f, 1f)));
        var band = barTop + new Vector2(0f, 6f);
        dl.AddRectFilled(band, band + new Vector2(w, bandH), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f)));
        if (_logo is not null) dl.AddImage((nint)_logo.GlId, band + new Vector2(30f, 30f), band + new Vector2(110f, 110f));
        var white = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        var gray = ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 1f));
        string step = _error ?? _step, pct = $"{_progress * 100f:0}%";
        if (step.Length > 70) step = step[..67] + "...";
        float right = w - 30f;
        DrawText(dl, s_title, 34f, band + new Vector2(126f, 48f), white, "CrashEngine");
        DrawText(dl, s_medium, 17f, band + new Vector2(right, 34f), _error is null ? gray : ImGui.GetColorU32(new Vector4(1f, 0.45f, 0.4f, 1f)), step, alignRight: true);
        DrawText(dl, s_large, 21f, band + new Vector2(right, 62f), white, _project.Name, alignRight: true);
        DrawText(dl, s_small, 15f, band + new Vector2(30f, bandH - 32f), gray, AppVersion.Text.Length > 0 ? $"v{AppVersion.Text}" : ""); // Amedo 2026-11-19
        DrawText(dl, s_large, 21f, band + new Vector2(right, bandH - 40f), white, pct, alignRight: true);
        ImGui.SetCursorScreenPos(band + new Vector2(w - 130f, bandH + 14f));
        if (_error is null)
        {
            if (ImGui.Button("Cancel", new Vector2(130f, 28f))) _cts.Cancel();
        }
        else if (ImGui.Button("Back", new Vector2(130f, 28f)))
        {
            Engine.Instance.SetTitleDetail("");
            Engine.Instance.ActiveScene = new ProjectSetupScene(_scriptOut);
        }
        ImGui.End();
    }

    private static ImFontPtr? s_title, s_large, s_medium, s_small;

    public static void AddFonts(ImFontAtlasPtr atlas, string fontFile) // Amedo 2026-11-14
    {
        var latin = EditorFonts.Ranges([0x0020, 0x00FF, 0x2010, 0x2027, 0]);
        s_title = atlas.AddFontFromFileTTF(fontFile, 34f, EditorFonts.SharpConfig(), latin);
        s_large = atlas.AddFontFromFileTTF(fontFile, 21f, EditorFonts.SharpConfig(), EditorFonts.Ranges(EditorFonts.UiRanges));
        s_medium = atlas.AddFontFromFileTTF(fontFile, 17f, EditorFonts.SharpConfig(), latin);
        s_small = atlas.AddFontFromFileTTF(fontFile, 15f, EditorFonts.SharpConfig(), latin);
    }

    private static void DrawText(ImDrawListPtr dl, ImFontPtr? baked, float size, Vector2 pos, uint color, string text, bool alignRight = false)
    {
        var font = baked ?? ImGui.GetFont();
        if (alignRight) pos.X -= font.CalcTextSizeA(size, float.MaxValue, 0f, text).X;
        dl.AddText(font, size, pos, color, text);
    }

    protected override void OnDestroy()
    {
        _cts.Cancel();
        _art?.Dispose();
        _logo?.Dispose();
    }
}
