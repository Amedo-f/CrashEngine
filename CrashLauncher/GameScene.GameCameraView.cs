using CrashEngine.Core;
using CrashEngine.Runtime;
using CrashEngine.Runtime.Debug;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-10-06
public sealed partial class GameScene
{
    private const float EjectBack = 3.2f, EjectUp = 1.1f;
    private bool _showGameCameraModel = true;
    private GameCameraOverlay? _gameCameraOverlay;
    private uint _gameCameraLabelZone = uint.MaxValue;
    private int _gameCameraLabelIndex = -1;
    private string _gameCameraLabel = "";
    private bool _gameCameraErrorLogged;

    private static string GameCameraModelPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "GameCamera.glb");

    private void AddGameCameraOverlay(Entity chunkRoot)
    {
        var root = new Entity("GameCameraView");
        _gameCameraOverlay = root.Add(new GameCameraOverlay(() => _simulation, () => _camera?.Aspect ?? 16f / 9f, GameCameraModelPath));
        chunkRoot.AddChild(root);
    }

    private void UpdateGameCameraOverlay()
    {
        if (_gameCameraOverlay is not { } overlay) return;
        overlay.Visible = Simulating && !_simGameCamera && _showGameCameraModel && _simulation?.GameCamera is not null;
        if (overlay.LoadError is { } error && !_gameCameraErrorLogged)
        {
            _gameCameraErrorLogged = true;
            _browser.Log("Play Scene: " + error);
        }
    }

    private void SetSimGameCamera(bool on)
    {
        _simGameCamera = on;
        if (!on) EjectGameCamera();
    }

    private void EjectGameCamera()
    {
        if (_camera is null) return;
        if (_simulation is { } sim && sim.GameCameraView(out var eye, out var forward, out var up, out _))
        {
            var right = Vector3.Normalize(Vector3.Cross(forward, up));
            var u = Vector3.Cross(right, forward);
            _camera.SetExternal(eye - forward * EjectBack + u * EjectUp, forward, up, _camera.Fov, _camera.Near, _camera.Far);
        }
        _camera.External = false;
    }

    private void DrawGameCameraLabel(SceneSimulation sim)
    {
        if (_gameCameraOverlay is not { Visible: true } || sim.GameCamera is not { } cam || !sim.GameCameraView(out var eye, out _, out var up, out _)) return;
        int sw = ViewW, sh = ViewH; // Amedo
        if (!WorldToScreen(eye + up * GameCameraOverlay.ModelScale * 0.6f, sw, sh, out var at)) return;
        if (cam.CurrentZone != _gameCameraLabelZone || cam.CameraIndex != _gameCameraLabelIndex)
        {
            _gameCameraLabelZone = cam.CurrentZone;
            _gameCameraLabelIndex = cam.CameraIndex;
            string kind = cam.CameraIndex switch { 0 => "player camera", 3 => "cutscene camera", var i => $"camera {i}" };
            string zone = cam.CurrentZone == 0 ? "no camera zone (follow)" : cam.ZoneLabel(cam.CurrentZone) ?? $"zone 0x{cam.CurrentZone:X}";
            _gameCameraLabel = $"Game Camera: {kind}\n{zone}";
        }
        var size = ImGui.CalcTextSize(_gameCameraLabel);
        var min = at - new Vector2(size.X * 0.5f + 6f, size.Y + 10f);
        var dl = ImGui.GetBackgroundDrawList();
        dl.AddRectFilled(min, min + size + new Vector2(12f, 8f), ImGui.GetColorU32(new Vector4(0.05f, 0.08f, 0.16f, 0.72f)), 4f);
        dl.AddText(min + new Vector2(6f, 4f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)), _gameCameraLabel);
    }
}
