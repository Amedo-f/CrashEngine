using System.Runtime.InteropServices;
using CrashEngine.Platform;
using ImGuiNET;

namespace CrashLauncher;

// Amedo 2026-11-12
public static class EditorFonts
{
    public static readonly ushort[] UiRanges = { 0x0020, 0x00FF, 0x0600, 0x06FF, 0x0750, 0x077F, 0x2010, 0x2027, 0x2030, 0x2044, 0xFB50, 0xFDFF, 0xFE70, 0xFEFF, 0 };

    public static string? UiFontPath() => SystemFonts.Find("segoeui.ttf", "arial.ttf", "DejaVuSans.ttf", "NotoSans-Regular.ttf"); // Amedo 2026-11-15

    public static string? MonoFontPath() => SystemFonts.Find("consola.ttf", "DejaVuSansMono.ttf", "NotoSansMono-Regular.ttf"); // Amedo 2026-11-15

    public static unsafe IntPtr Ranges(ushort[] ranges)
    {
        var ptr = (ushort*)Marshal.AllocHGlobal(ranges.Length * sizeof(ushort));
        for (int i = 0; i < ranges.Length; i++) ptr[i] = ranges[i];
        return (IntPtr)ptr;
    }

    public const float UiSize = 17f; // Amedo 2026-11-14

    public static string? AddUiFont(ImFontAtlasPtr atlas, float size = UiSize)
    {
        var font = UiFontPath();
        if (font is not null) atlas.AddFontFromFileTTF(font, size, SharpConfig(), Ranges(UiRanges)); // Amedo 2026-11-14
        return font;
    }

    // Amedo 2026-11-14
    public static unsafe ImFontConfigPtr SharpConfig()
    {
        var config = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig());
        config.OversampleH = 2;
        config.OversampleV = 1;
        config.PixelSnapH = true;
        config.RasterizerMultiply = 1.15f;
        return config;
    }
}
