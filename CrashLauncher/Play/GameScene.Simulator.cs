using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Runtime.Character;
using ImGuiNET;

namespace CrashLauncher;

// Amedo 2026-10-26
public sealed partial class GameScene
{
    private const float SimBezel = 14f, SimMargin = 24f;

    private bool _simulatorOn;
    private SimulatorDeviceList? _simDevices;
    private int _simDevice;
    private bool _simPortrait;
    private bool _simFit = true;
    private float _simScale = 0.3f;
    private float _simShownScale;
    private bool _simSafeArea;
    private Vector2 _simMin, _simSize;
    private TouchPad? _simTouchPad;
    private bool _simLoadFailed;
    private TouchOverlay? _simOverlay;
    private readonly List<(long Id, Vector2 Pos)> _simTouches = new();

    private static string InputFolder => Path.Combine(AppContext.BaseDirectory, "Assets", "Input");

    private SimulatorDevice? SimDevice
    {
        get
        {
            if (_simDevices is null && !_simLoadFailed)
            {
                try
                {
                    _simDevices = SimulatorDeviceList.Load();
                    _simTouchPad = new TouchPad(TouchLayout.Load(Path.Combine(InputFolder, "layout.json")));
                }
                catch (Exception e)
                {
                    _simLoadFailed = true;
                    _browser.Log("Simulator: couldn't load the devices or the touch layout: " + e.Message);
                }
            }
            return _simDevices is { Devices.Count: > 0 } list ? list.Devices[Math.Clamp(_simDevice, 0, list.Devices.Count - 1)] : null;
        }
    }

    private bool SimulatorActive => _simulatorOn && _workspace == Workspace.Scene;

    private (int W, int H) SimScreenPixels(SimulatorDevice d) => _simPortrait ? (d.Width, d.Height) : (d.Height, d.Width);

    private Vector2 SimPoint(SimulatorDevice d, float x, float y) =>
        _simPortrait ? new Vector2(x, y) * _simShownScale : new Vector2(y, d.Width - x) * _simShownScale;

    private void DrawSimulatorToolbarItems()
    {
        if (SimDevice is not { } device) return;
        ImGui.SetNextItemWidth(230f);
        if (ImGui.BeginCombo("##simdevice", device.Name))
        {
            for (int i = 0; i < _simDevices!.Devices.Count; i++)
                if (ImGui.Selectable(_simDevices.Devices[i].Name, i == _simDevice)) _simDevice = i;
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        var (pw, ph) = SimScreenPixels(device);
        ImGui.TextDisabled($"{pw}x{ph}");
        ImGui.SameLine(0f, 16f);
        float percent = _simShownScale * 100f;
        ImGui.SetNextItemWidth(140f);
        if (ImGui.SliderFloat("Scale##simscale", ref percent, 5f, 100f, "%.0f%%"))
        {
            _simScale = percent / 100f;
            _simFit = false;
        }
        ImGui.SameLine();
        if (ImGui.Button("Fit to Screen##simfit")) _simFit = true;
        ImGui.SameLine();
        if (ImGui.Button((_simPortrait ? "Portrait" : "Landscape") + "##simrotate")) _simPortrait = !_simPortrait;
        ImGui.SameLine();
        ToolbarToggle("Safe Area##simsafe", ref _simSafeArea, "Outlines the part of the screen the camera cutout and the system bars leave free.");
    }

    private void FitSimulatorViewport()
    {
        if (!SimulatorActive || SimDevice is not { } device) return;
        var engine = Engine.Instance;
        var area = engine.SceneViewport ?? (0, 0, engine.Width, engine.Height);
        var (pw, ph) = SimScreenPixels(device);
        float fit = MathF.Min((area.W - 2f * SimMargin) / pw, (area.H - 2f * SimMargin) / ph);
        _simShownScale = MathF.Max(0.01f, _simFit ? fit : MathF.Min(_simScale, fit));
        int w = Math.Max(16, (int)MathF.Round(pw * _simShownScale)), h = Math.Max(16, (int)MathF.Round(ph * _simShownScale));
        int x = area.X + (area.W - w) / 2, y = area.Y + (area.H - h) / 2;
        engine.SceneViewport = (x, y, w, h);
        _simMin = new Vector2(x, y);
        _simSize = new Vector2(w, h);
    }

    private void DrawSimulatorOverlay()
    {
        _simTouches.Clear();
        if (!SimulatorActive || SimDevice is not { } device) return;
        var dl = ImGui.GetBackgroundDrawList();
        var min = _simMin;
        var max = _simMin + _simSize;
        float corner = device.Corner * _simShownScale;
        var half = new Vector2(SimBezel * 0.5f);
        dl.AddRect(min - half, max + half, 0xFF000000, corner + SimBezel * 0.5f, ImDrawFlags.None, SimBezel);
        dl.AddRect(min - half * 2f, max + half * 2f, ImGui.GetColorU32(new Vector4(0.45f, 0.45f, 0.48f, 1f)), corner + SimBezel, ImDrawFlags.None, 1.5f);

        if (device.Cutout is { } cut)
        {
            var c = min + SimPoint(device, cut.X, cut.Y);
            if (cut.Type == "island")
            {
                var size = (_simPortrait ? new Vector2(cut.W, cut.H) : new Vector2(cut.H, cut.W)) * _simShownScale * 0.5f;
                dl.AddRectFilled(c - size, c + size, 0xFF000000, MathF.Min(size.X, size.Y));
            }
            else dl.AddCircleFilled(c, cut.W * 0.5f * _simShownScale, 0xFF000000);
        }

        if (_simSafeArea)
        {
            var near = device.SafeTop * _simShownScale;
            var far = device.SafeBottom * _simShownScale;
            var a = _simPortrait ? min + new Vector2(0f, near) : min + new Vector2(near, 0f);
            var b = _simPortrait ? max - new Vector2(0f, far) : max - new Vector2(far, 0f);
            dl.AddRect(a, b, ImGui.GetColorU32(new Vector4(0.3f, 0.9f, 0.5f, 0.9f)), 0f, ImDrawFlags.None, 2f);
        }

        if (_simTouchPad is { } pad) (_simOverlay ??= new TouchOverlay(InputFolder)).Draw(dl, pad, _simMin, _simSize);
        if (Simulating) CaptureSimulatorTouch();
    }

    private void CaptureSimulatorTouch()
    {
        ImGui.SetNextWindowPos(_simMin);
        ImGui.SetNextWindowSize(_simSize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
                                       ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus;
        bool open = ImGui.Begin("##simtouch", flags);
        ImGui.PopStyleVar();
        if (open)
        {
            ImGui.InvisibleButton("##simscreen", _simSize);
            if (ImGui.IsItemActive()) _simTouches.Add((1L, ImGui.GetMousePos() - _simMin));
        }
        ImGui.End();
    }

    private void ApplySimulatorTouch(CharacterPad pad)
    {
        if (SimulatorActive && _simTouchPad is { } touch) touch.Apply(_simTouches, _simSize.X, _simSize.Y, EngineTime.Delta, pad);
    }
}
