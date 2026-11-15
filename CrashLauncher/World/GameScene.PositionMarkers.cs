using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace CrashLauncher;

// Amedo 2026-11-15
public sealed partial class GameScene : Scene
{
    private static readonly Vector4 PositionMarkerColor = new(1f, 0.9f, 0.15f, 0.7f);
    private static readonly Vector4 PositionRingColor = new(0.95f, 0.78f, 0.1f, 1f);
    private const float PositionMarkerScale = 0.5f;
    private const float PositionPinHeightPerScale = 1f / 0.3f;

    private Entity MakePositionMarker(Entity parent, string name, TwinVec4 pos)
    {
        var gl = Engine.Instance.GL;
        if (EnsurePositionPinModel(gl) is not { } pin) return MakePointMarker(parent, name, pos, PositionMarkerColor);

        var e = new Entity(name);
        e.Transform.Position = new Vector3(pos.X, pos.Y, pos.Z);
        e.Transform.Scale    = new Vector3(PositionMarkerScale);

        var iconEnt = new Entity($"{name}_icon");
        iconEnt.Transform.Scale = new Vector3(PositionPinHeightPerScale / pin.Height);
        var iconRdr = iconEnt.Add(new DirectCubeRenderer { Mesh = pin.Mesh, Color = Vector4.One });
        iconRdr.Albedo          = new Texture2D(gl, pin.Pixels, (uint)pin.TexWidth, (uint)pin.TexHeight);
        iconRdr.Mat.Culling     = Material.CullMode.Both;
        iconRdr.Mat.AlwaysOnTop = false;
        e.AddChild(iconEnt);

        var ringEnt = new Entity($"{name}_ring");
        ringEnt.Transform.Scale = new Vector3(AiRingRadius, 1f, AiRingRadius);
        var ringRdr = ringEnt.Add(new DirectCubeRenderer { Mesh = EnsureAiRingMesh(gl), Color = PositionRingColor });
        ringRdr.Mat.Culling         = Material.CullMode.Both;
        ringRdr.Mat.AlwaysOnTop     = false;
        ringRdr.Mat.IgnoreDepthTest = false;
        e.AddChild(ringEnt);

        parent.AddChild(e);
        return e;
    }

    private MarkerModel? _positionPin;
    private bool _positionPinTried;
    private MarkerModel? EnsurePositionPinModel(GL gl)
    {
        if (_positionPinTried) return _positionPin;
        _positionPinTried = true;
        return _positionPin = LoadMarkerModel(gl, "PositionPin.glb");
    }

    private void UpdatePositionPins()
    {
        if (_positionsRoot is not { Active: true } root || _positionPin is null) return;
        if (root.Parent?.Get<ChunkSource>() is { } src) UpdateAiRings(root, src);
        if (!TryCameraEye(out var eye)) return;
        foreach (var e in root.Children)
            if (e.Active && IsIconMarker(e)) TurnIconToCamera(e, eye, pitch: false);
    }
}
