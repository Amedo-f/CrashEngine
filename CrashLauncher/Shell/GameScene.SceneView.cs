using System.Numerics;
using System.Runtime.InteropServices;
using CrashEngine.Core;
using CrashEngine.Renderer;
using ImGuiNET;

namespace CrashLauncher;

// Amedo
public sealed partial class GameScene
{
    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr igDockBuilderGetCentralNode(uint nodeId);

    private const int DockNodePosOffset = 72;
    private const int DockNodeSizeOffset = 80;

    private static int ViewW => Engine.Instance.ViewWidth;
    private static int ViewH => Engine.Instance.ViewHeight;
    private static Vector2 ViewOrigin => Engine.Instance.SceneViewport is { } v ? new Vector2(v.X, v.Y) : Vector2.Zero;

    // Amedo
    private static bool MouseInView(Vector2 mouse)
    {
        var o = ViewOrigin;
        return mouse.X >= o.X && mouse.Y >= o.Y && mouse.X < o.X + ViewW && mouse.Y < o.Y + ViewH;
    }

    // Amedo
    private static unsafe void UpdateSceneViewport(uint dockSpaceId, float stripHeight)
    {
        var engine = Engine.Instance;
        IntPtr node = igDockBuilderGetCentralNode(dockSpaceId);
        if (node == IntPtr.Zero)
        {
            engine.SceneViewport = null;
            _workspaceStrip = null;
            return;
        }
        var pos = *(Vector2*)((byte*)node + DockNodePosOffset);
        var size = *(Vector2*)((byte*)node + DockNodeSizeOffset);
        int x = (int)MathF.Round(pos.X), y = (int)MathF.Round(pos.Y);
        int w = (int)MathF.Round(size.X), h = (int)MathF.Round(size.Y);
        int tab = (int)stripHeight;
        bool sane = w >= 16 && h >= 16 + tab && x >= 0 && y >= 0 && x + w <= engine.Width + 1 && y + h <= engine.Height + 1;
        bool full = x == 0 && y == 0 && w == engine.Width && h == engine.Height;
        w = Math.Min(w, engine.Width - x);
        _workspaceStrip = sane && !full ? new Vector3(x, y, w) : null;
        engine.SceneViewport = sane && !full ? (x, y + tab, w, Math.Min(h, engine.Height - y) - tab) : null;
    }

	private bool WorldToScreen(System.Numerics.Vector3 world, int sw, int sh, out System.Numerics.Vector2 screen)
	{
		screen = System.Numerics.Vector2.Zero;
		if (_camera == null)
		{
			return false;
		}
		System.Numerics.Vector4 vector = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(world, 1f), _camera.View * _camera.Projection);
		if (vector.W <= 0f)
		{
			return false;
		}
		float num = vector.X / vector.W;
		float num2 = vector.Y / vector.W;
		if (num < -1f || num > 1f || num2 < -1f || num2 > 1f)
		{
			return false;
		}
		screen = ViewOrigin + new System.Numerics.Vector2((num + 1f) * 0.5f * (float)sw, (1f - num2) * 0.5f * (float)sh); // Amedo
		return true;
	}

	private Entity? HitTestCameraIcons(System.Numerics.Vector2 mouse, int sw, int sh)
	{
		if (_camerasRoot is not { Active: true } camerasRoot) return null;
		if (_cameraIconModel is not null || _playerCameraModel is not null) return HitTestCameraIcon3D(mouse, sw, sh); // Amedo 2026-11-15
		foreach (var child in camerasRoot.Children)
		{
			if (!WorldToScreen(child.Transform.World.Translation, sw, sh, out var screen)) continue;
			var hitMin = _cameraIconTexture is not null ? CameraIconMin : new System.Numerics.Vector2(-11f, -12.5f); // Amedo 2026-10-04
			var hitMax = _cameraIconTexture is not null ? CameraIconMax : new System.Numerics.Vector2(11f, 7.5f); // Amedo 2026-10-04
			if (mouse.X >= screen.X + hitMin.X && mouse.X <= screen.X + hitMax.X &&
			    mouse.Y >= screen.Y + hitMin.Y && mouse.Y <= screen.Y + hitMax.Y)
				return child;
		}
		return null;
	}

	private int _undoCountSeen = -1;

	private void KeepHeldPictureFresh()
	{
		var io = ImGui.GetIO();
		bool touched = ImGui.IsAnyItemActive() || io.WantTextInput || _undoStack.Count != _undoCountSeen
		               || ImGui.IsMouseDown(ImGuiMouseButton.Left) || ImGui.IsMouseDown(ImGuiMouseButton.Right) || ImGui.IsMouseDown(ImGuiMouseButton.Middle)
		               || io.MouseWheel != 0f;
		for (var key = ImGuiKey.NamedKey_BEGIN; !touched && key < ImGuiKey.NamedKey_END; key++)
			touched = ImGui.IsKeyDown(key);
		_undoCountSeen = _undoStack.Count;
		if (touched) SceneTarget.DrawFresh(3);
	}
}
