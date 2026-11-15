using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Runtime.Debug;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-15
internal sealed class PlayerCameraMarker : Component
{
}

// Amedo 2026-11-15
public sealed partial class GameScene : Scene
{
    private const long PlayerCameraSearchMs = 1000;

    private MarkerModel? _playerCameraModel;
    private bool _playerCameraModelTried;
    private Entity? _playerCamera;
    private Entity? _playerCameraCrash;
    private long _playerCameraSearchAt;
    private Matrix4x4 _playerCameraCrashLocal;
    private bool _playerCameraSettled;
    private bool _playerCameraMoved;
    private Matrix4x4? _playerCameraPlaced;
    private readonly PlayerCameraStart _playerCameraStart = new();

    private MarkerModel? EnsurePlayerCameraModel(Silk.NET.OpenGL.GL gl)
    {
        if (_playerCameraModelTried) return _playerCameraModel;
        _playerCameraModelTried = true;
        return _playerCameraModel = LoadMarkerModel(gl, "GameCamera.glb");
    }

    private Entity MakePlayerCamera(MarkerModel model)
    {
        var e = new Entity("Player Camera");
        e.Add(new PlayerCameraMarker());
        var iconEnt = new Entity("Player Camera_icon");
        iconEnt.Transform.Scale = new Vector3(GameCameraOverlay.ModelScale);
        var rdr = iconEnt.Add(new DirectCubeRenderer { Mesh = model.Mesh, Color = Vector4.One });
        rdr.Albedo          = new Texture2D(Engine.Instance.GL, model.Pixels, (uint)model.TexWidth, (uint)model.TexHeight);
        rdr.Mat.Culling     = Material.CullMode.Both;
        rdr.Mat.AlwaysOnTop = false;
        e.AddChild(iconEnt);
        return e;
    }

    private Entity? PlayerCameraCrash(Entity chunkRoot)
    {
        if (_playerCameraCrash is { Parent: not null } c && c.Get<InstanceData>() is { ObjectId: 0 }) return c;
        _playerCameraCrash = null;
        long now = Environment.TickCount64;
        if (now < _playerCameraSearchAt) return null;
        _playerCameraSearchAt = now + PlayerCameraSearchMs;
        _playerCameraSettled = false;
        return _playerCameraCrash = AllEntities(chunkRoot).FirstOrDefault(x => x.Get<InstanceData>() is { ObjectId: 0 });
    }

    private void UpdatePlayerCamera()
    {
        if (_camerasRoot is not { Parent: { } chunkRoot } root || EnsurePlayerCameraModel(Engine.Instance.GL) is not { } model) return;
        if (_playerCamera is null || !ReferenceEquals(_playerCamera.Parent, root))
        {
            _playerCamera = MakePlayerCamera(model);
            root.AddChild(_playerCamera);
            _playerCameraCrash = null;
            _playerCameraSearchAt = 0;
            _playerCameraSettled = false;
            _playerCameraMoved = false;
            _playerCameraPlaced = null;
        }
        if (!root.Active) return;
        var crash = PlayerCameraCrash(chunkRoot);
        _playerCamera.Active = crash is not null && !Simulating;
        if (crash is null || !_playerCamera.Active) return;

        var local = crash.Transform.Local;
        if (!_playerCameraSettled || (local != _playerCameraCrashLocal && !_gizmoDragging && !ImGui.IsAnyItemActive()))
        {
            var collision = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>() is { } src ? LevelCollision(src) : null;
            _playerCameraStart.Settle(local, collision);
            _playerCameraCrashLocal = local;
            _playerCameraSettled = true;
        }

        var rootWorld = root.Transform.World;
        float s = GameCameraOverlay.ModelScale;
        _playerCamera.Children[0].Transform.Scale = new Vector3(rootWorld.GetDeterminant() < 0f ? -s : s, s, s);
        if (_playerCamera.Transform.LocalMatrix is { } now && _playerCameraPlaced is { } placed && now != placed) _playerCameraMoved = true;
        if (_playerCameraMoved) return;
        if (crash.Parent is not { } space || !Matrix4x4.Invert(rootWorld, out var rootInv)) return;
        var toRoot = space.Transform.World * rootInv;
        var start = _playerCameraStart;
        var drag = local.Translation - _playerCameraCrashLocal.Translation;
        var eye = Vector3.Transform(start.Eye + drag, toRoot);
        var forward = GameMath.Normalize(Vector3.TransformNormal(start.Forward, toRoot));
        var up = GameMath.Normalize(Vector3.TransformNormal(start.Up, toRoot));
        var right = GameMath.Normalize(Vector3.Cross(forward, up));
        var u = Vector3.Cross(right, forward);
        var at = new Matrix4x4(right.X, right.Y, right.Z, 0f, u.X, u.Y, u.Z, 0f,
                               -forward.X, -forward.Y, -forward.Z, 0f, eye.X, eye.Y, eye.Z, 1f);
        _playerCamera.Transform.LocalMatrix = at;
        _playerCameraPlaced = at;
    }

    private bool HitPlayerCamera(Vector3 orig, Vector3 dir, out float t)
    {
        t = float.MaxValue;
        return _playerCamera is { Active: true } cam && _playerCameraModel is { } model && IsIconMarker(cam)
            && RayHitsMarkerModel(cam.Children[0], model, orig, dir, out t);
    }

    private void SnapPlayerCameraBehindCrash()
    {
        _playerCameraMoved = false;
        _playerCameraPlaced = null;
        if (_playerCamera is { } cam) cam.Transform.LocalMatrix = null;
    }

    private bool PlayerCameraWorldLine(out Vector3 eye, out Vector3 target)
    {
        eye = target = default;
        if (_playerCameraMoved || _playerCamera is not { Active: true } cam || _playerCameraCrash?.Parent is not { } space) return false;
        var drag = _playerCameraCrash.Transform.Local.Translation - _playerCameraCrashLocal.Translation;
        eye = cam.Transform.World.Translation;
        target = Vector3.Transform(_playerCameraStart.Target + drag, space.Transform.World);
        return true;
    }

    private void DrawPlayerCameraLine()
    {
        if (_selected is null || !_selected.Has<PlayerCameraMarker>() || !PlayerCameraWorldLine(out var eye, out var target)) return;
        if (!WorldToScreen(eye, ViewW, ViewH, out var a) || !WorldToScreen(target, ViewW, ViewH, out var b)) return;
        var dl = ImGui.GetBackgroundDrawList();
        uint col = ImGui.GetColorU32(GameCameraOverlay.TargetColor);
        dl.AddLine(a, b, col, 1.5f);
        dl.AddCircleFilled(b, 4f, col);
    }
}
