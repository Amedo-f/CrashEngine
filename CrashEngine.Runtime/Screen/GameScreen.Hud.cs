using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;

namespace CrashEngine.Runtime.Screen;

// Amedo 2026-10-01
public sealed partial class GameScreen
{
    public const string IconsPath = @"StartUp\Icons.psm";
    public const int IconSlots = 46, FirstIconSlot = 6;

    public GameFont.Page?[] Icons { get; private init; } = new GameFont.Page?[IconSlots];
    public GameFont.Page? ParticlePage_0 { get; private init; } // Amedo

    // Amedo
    private static void ReadIcons(Stream stream, GameFont.Page?[] icons)
    {
        var psm = new PS2PSM();
        psm.Read(new BinaryReader(stream), (int)stream.Length);
        for (int i = 0; i < psm.PTCs.Count && FirstIconSlot + i < icons.Length; i++)
            if (psm.PTCs[i].Texture is PS2AnyTexture tex) icons[FirstIconSlot + i] = GameFont.Decode(tex);
    }

    // Amedo
    private static GameFont.Page? ReadParticlePage(PS2AnyTwinsanityRM2? globalRm2, int page)
    {
        var data = globalRm2?.GetItem<PS2DefaultParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        var textures = globalRm2?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION)?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (data is null || textures is null) return null;
        uint id = data.TextureIDs[page];
        return textures.ContainsItem(id) && textures.GetItem<PS2AnyTexture>(id) is { } tex ? GameFont.Decode(tex) : null;
    }
}
