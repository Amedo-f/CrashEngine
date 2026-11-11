using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Runtime.Screen;

namespace CrashEngine.Runtime.Region;

// Amedo 2026-11-11
public sealed class RegionProfile
{
    public GameRegion Region { get; private init; }
    public int LogicTicks { get; private init; }
    public float FrameHeight_309744 { get; private init; }
    public string FontPath { get; private init; } = "";
    public Vector2 FontSize { get; private init; }
    public int[] FrameLimits { get; private init; } = [];

    public static readonly RegionProfile Ntsc = new()
    {
        Region = GameRegion.Ntsc,
        LogicTicks = SceneSimulation.NtscLogicTicks,
        FrameHeight_309744 = 448f,
        FontPath = GameScreen.FontPath,
        FontSize = new(GameFont.GlyphWidth, GameFont.GlyphHeight),
        FrameLimits = GraphicsSettings.PcFrameLimits,
    };

    public static readonly RegionProfile Pal = new()
    {
        Region = GameRegion.Pal,
        LogicTicks = SceneSimulation.PalLogicTicks,
        FrameHeight_309744 = 512f,
        FontPath = GameScreen.PalFontPath,
        FontSize = new(GameFont.PalGlyphWidth, GameFont.PalGlyphHeight),
        FrameLimits = GraphicsSettings.PalFrameLimits,
    };

    public static RegionProfile Current { get; set; } = Ntsc;

    public static RegionProfile For(string discLanguage) => discLanguage == PlaySettings.AmericanLanguage ? Ntsc : Pal;

    public static RegionProfile ForDisc(string disc) => PlaySettings.IsPalDisc(disc) ? Pal : Ntsc;
}
