using System.Numerics;
using System.Runtime.InteropServices;
using CrashEngine.Core;

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
    private static unsafe void UpdateSceneViewport(uint dockSpaceId)
    {
        var engine = Engine.Instance;
        IntPtr node = igDockBuilderGetCentralNode(dockSpaceId);
        if (node == IntPtr.Zero)
        {
            engine.SceneViewport = null;
            return;
        }
        var pos = *(Vector2*)((byte*)node + DockNodePosOffset);
        var size = *(Vector2*)((byte*)node + DockNodeSizeOffset);
        int x = (int)MathF.Round(pos.X), y = (int)MathF.Round(pos.Y);
        int w = (int)MathF.Round(size.X), h = (int)MathF.Round(size.Y);
        bool sane = w >= 16 && h >= 16 && x >= 0 && y >= 0 && x + w <= engine.Width + 1 && y + h <= engine.Height + 1;
        bool full = x == 0 && y == 0 && w == engine.Width && h == engine.Height;
        engine.SceneViewport = sane && !full ? (x, y, Math.Min(w, engine.Width - x), Math.Min(h, engine.Height - y)) : null;
    }
}
