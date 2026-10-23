using System.Numerics;
using CrashEngine.Runtime.Screen.Oleg;

namespace CrashEngine.Runtime.Screen;

// Amedo
public static class ScreenFit
{
    private const float OffsetX = 256f / 2560f, OffsetY = 32f / 448f;

    public static float VirtualAspect => OverlayQueue.Widescreen_30A744 ? 16f / 9f : 4f / 3f;

    // Amedo
    public static Vector2 Frame_309744(string discLanguage) => new(512f, discLanguage == PlaySettings.AmericanLanguage ? 448f : 512f);

    // Amedo
    public static (Vector2 Origin, Vector2 Size) Rect(Vector2 origin, Vector2 size, Vector2 screenOffset)
    {
        float av = VirtualAspect, aw = size.X / size.Y;
        var inner = aw >= av ? new Vector2(size.Y * av, size.Y) : new Vector2(size.X, size.X / av);
        var o = origin + (size - inner) * 0.5f;
        o += new Vector2(screenOffset.X * OffsetX, screenOffset.Y * OffsetY) * inner;
        return (o, inner);
    }

    // Amedo
    public static float FovDegrees(float fovDegrees, float windowAspect)
    {
        float av = VirtualAspect;
        if (windowAspect >= av) return fovDegrees;
        float t = MathF.Tan(fovDegrees * MathF.PI / 360f) * av / windowAspect;
        return MathF.Atan(t) * 360f / MathF.PI;
    }

    // Amedo
    public static Vector2 ClipShift(Vector2 size, Vector2 screenOffset)
    {
        var (origin, inner) = Rect(Vector2.Zero, size, screenOffset);
        var centre = origin + inner * 0.5f - size * 0.5f;
        return new Vector2(centre.X * 2f / size.X, -centre.Y * 2f / size.Y);
    }
}
