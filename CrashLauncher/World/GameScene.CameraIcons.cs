using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-15
public sealed partial class GameScene : Scene
{
    private const float CameraIconWorldWidth = 1.5f;
    private static readonly Vector3 CameraZoneCentre = new(0f, 1f, 0f);

    private MarkerModel? _cameraIconModel;
    private bool _cameraIconModelTried;
    private MarkerModel? EnsureCameraIconModel(GL gl)
    {
        if (_cameraIconModelTried) return _cameraIconModel;
        _cameraIconModelTried = true;
        return _cameraIconModel = LoadMarkerModel(gl, "CameraIcon.glb");
    }

    private Entity? _cameraIconHost;
    private readonly Dictionary<Entity, Entity> _cameraIcons = new();
    private readonly List<Entity> _cameraIconsGone = new();

    private Entity MakeCameraIcon(MarkerModel model, string name)
    {
        var e = new Entity(name);
        var iconEnt = new Entity($"{name}_icon");
        float k = CameraIconWorldWidth / (model.Max.X - model.Min.X);
        iconEnt.Transform.Scale = new Vector3(k, k, k);
        var rdr = iconEnt.Add(new DirectCubeRenderer { Mesh = model.Mesh, Color = Vector4.One });
        rdr.Albedo          = new Texture2D(Engine.Instance.GL, model.Pixels, (uint)model.TexWidth, (uint)model.TexHeight);
        rdr.Mat.Culling     = Material.CullMode.Both;
        rdr.Mat.AlwaysOnTop = false;
        e.AddChild(iconEnt);
        return e;
    }

    private void UpdateCameraIcons()
    {
        if (_camerasRoot is not { } root || EnsureCameraIconModel(Engine.Instance.GL) is not { } model) return;
        if (_cameraIconHost is null || !ReferenceEquals(_cameraIconHost.Parent, root))
        {
            _cameraIconHost = new Entity("__CameraIcons");
            root.AddChild(_cameraIconHost);
            _cameraIcons.Clear();
        }
        if (!root.Active) return;

        _cameraIconsGone.Clear();
        foreach (var (zone, icon) in _cameraIcons)
            if (!ReferenceEquals(zone.Parent, root)) _cameraIconsGone.Add(zone);
        foreach (var zone in _cameraIconsGone)
        {
            _cameraIconHost.RemoveChild(_cameraIcons[zone]);
            _cameraIcons.Remove(zone);
        }

        bool haveEye = TryCameraEye(out var eye);
        foreach (var zone in root.Children)
        {
            if (!zone.Has<CameraMarker>()) continue;
            if (!_cameraIcons.TryGetValue(zone, out var icon))
            {
                icon = MakeCameraIcon(model, $"{zone.Name}_camera");
                _cameraIconHost.AddChild(icon);
                _cameraIcons[zone] = icon;
            }
            icon.Active = zone.Active;
            icon.Transform.Position = Vector3.Transform(CameraZoneCentre, zone.Transform.Local);
            if (zone.Active && haveEye) TurnIconToCamera(icon, eye, pitch: true);
        }
    }

    private Entity? HitTestCameraIcon3D(Vector2 mouse, int sw, int sh)
    {
        if (_camerasRoot is not { Active: true }) return null;
        var (orig, dir) = GetMouseRay(mouse, sw, sh);
        Entity? best = null;
        float bestT = float.MaxValue;
        if (_cameraIconModel is { } model)
            foreach (var (zone, icon) in _cameraIcons)
                if (zone.Active && RayHitsMarkerModel(icon.Children[0], model, orig, dir, out float t) && t < bestT) { bestT = t; best = zone; }
        if (HitPlayerCamera(orig, dir, out float tp) && tp < bestT) best = _playerCamera;
        return best;
    }
}
