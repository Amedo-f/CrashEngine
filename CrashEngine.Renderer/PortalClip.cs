using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Renderer;

public sealed class PortalClip : Component
{
    public static bool Enabled { get; set; }

    private readonly Vector4[] _planes = new Vector4[4];

    public bool Active { get; private set; }

    public void Set(ReadOnlySpan<Vector4> planes)
    {
        planes[..4].CopyTo(_planes);
        Active = true;
    }

    public void Clear() => Active = false;

    public static PortalClip? Of(Entity entity)
    {
        for (var e = entity; e is not null; e = e.Parent)
            if (e.Get<PortalClip>() is { } clip) return clip;
        return null;
    }

    public bool Hides(in Matrix4x4 world, in Vector3 min, in Vector3 max)
    {
        if (!Active) return false;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
            corners[i] = Vector3.Transform(new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z), world);
        foreach (var plane in _planes)
        {
            bool behind = true;
            foreach (var c in corners)
                if (0f <= plane.X * c.X + plane.Y * c.Y + plane.Z * c.Z + plane.W)
                {
                    behind = false;
                    break;
                }
            if (behind) return true;
        }
        return false;
    }
}
