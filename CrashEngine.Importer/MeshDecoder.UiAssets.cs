using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using SysVec2 = System.Numerics.Vector2;
using SysVec3 = System.Numerics.Vector3;
using SysVec4 = System.Numerics.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinSceneryBaseType = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryBaseType;
using TwinSceneryLeaf = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryLeaf;
using TwinSceneryNode = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryNode;
using TwinSceneryRoot = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryRoot;
using ITwinScenery = Twinsanity.TwinsanityInterchange.Interfaces.Items.SM.ITwinScenery;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using TwinMorphAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinMorphAnimation;
using TwinJointSettings = Twinsanity.TwinsanityInterchange.Common.Animation.JointSettings;
using TwinTransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace CrashEngine.Importer;

public static partial class MeshDecoder
{
    public static Texture2D? DecodeUiTexture(GL gl, Twinsanity.TwinsanityInterchange.Interfaces.ITwinPTC ptc) =>
        ptc.Texture is Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture tex
            ? ChunkImporter.DecodeTexture(gl, tex, false) // Amedo
            : null;

    public static List<Texture2D> DecodePsm(GL gl, Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSM psm) =>
        psm.PTCs.Select(p => DecodeUiTexture(gl, p)).Where(t => t is not null).Select(t => t!).ToList();

    public static List<Texture2D> DecodeSplitFrames(GL gl, Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSM psm)
    {
        var raw = psm.PTCs
            .Select(p => p.Texture as Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture)
            .Where(t => t is not null)
            .Select(t => DecodeRaw(t!))
            .ToList();

        if (raw.Count < 2 || raw.Count % 2 != 0 || raw.Any(r => r is null) ||
            raw.Any(r => r!.Value.w != raw[0]!.Value.w || r.Value.h != raw[0]!.Value.h))
            return DecodePsm(gl, psm);

        int tw = raw[0]!.Value.w, th = raw[0]!.Value.h, half = th / 2;
        if (half == 0) return DecodePsm(gl, psm);

        var topGroup = new List<byte[]>();
        var bottomGroup = new List<byte[]>();
        foreach (var r in raw)
        {
            var (w, h, rgba) = r!.Value;
            bool isTop = LuminanceVariance(rgba, w, 0, half) >= LuminanceVariance(rgba, w, half, half);
            var cropped = new byte[w * half * 4];
            int srcRowStart = isTop ? 0 : half;
            for (int y = 0; y < half; y++)
                Array.Copy(rgba, ((srcRowStart + y) * w) * 4, cropped, (y * w) * 4, w * 4);
            (isTop ? topGroup : bottomGroup).Add(cropped);
        }

        List<Texture2D> frames = new();
        foreach (var group in new[] { topGroup, bottomGroup })
        {
            if (group.Count == 0) continue;
            int W = tw * group.Count;
            var frame = new byte[W * half * 4];
            for (int c = 0; c < group.Count; c++)
            for (int y = 0; y < half; y++)
                Array.Copy(group[c], (y * tw) * 4, frame, (y * W + c * tw) * 4, tw * 4);
            frames.Add(new Texture2D(gl, frame, (uint)W, (uint)half));
        }
        return frames;
    }

    private static double LuminanceVariance(byte[] rgba, int width, int rowStart, int rowCount)
    {
        int n = width * rowCount;
        if (n == 0) return 0;
        var lum = new double[n];
        double sum = 0;
        for (int y = 0; y < rowCount; y++)
        for (int x = 0; x < width; x++)
        {
            int i = ((rowStart + y) * width + x) * 4;
            double l = 0.299 * rgba[i] + 0.587 * rgba[i + 1] + 0.114 * rgba[i + 2];
            lum[y * width + x] = l;
            sum += l;
        }
        double mean = sum / n, varSum = 0;
        foreach (var l in lum) varSum += (l - mean) * (l - mean);
        return varSum / n;
    }

    private static (int w, int h, byte[] rgba)? DecodeRaw(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture tex)
    {
        tex.CalculateData();
        if (tex.Colors.Count == 0) return null;
        int w = tex.ImageWidthPower  > 0 ? (1 << tex.ImageWidthPower)  : 1;
        int h = tex.ImageHeightPower > 0 ? (1 << tex.ImageHeightPower) : 1;
        var rgba = new byte[Math.Max(tex.Colors.Count, w * h) * 4];
        int idx = 0;
        foreach (Twinsanity.TwinsanityInterchange.Common.Color c in tex.Colors)
        {
            if (idx >= rgba.Length) break;
            rgba[idx++] = c.R;
            rgba[idx++] = c.G;
            rgba[idx++] = c.B;
            rgba[idx++] = c.A;
        }
        return (w, h, rgba);
    }

    public static List<Texture2D> DecodePsf(GL gl, Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSF psf) =>
        psf.FontPages.Select(p => DecodeUiTexture(gl, p)).Where(t => t is not null).Select(t => t!).ToList();

    public readonly record struct PsfGlyph(int Codepoint, int PageIndex, int X, int Y, int W, int H);

    public static (List<(int W, int H, byte[] Rgba)> Pages, List<PsfGlyph> Glyphs)? GetPsfRawGlyphs(
        Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSF psf)
    {
        if (psf.FontPages.Count != 1) return null;

        if (psf.FontPages[0].Texture is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture tex)
            return null;
        var raw = DecodeRaw(tex);
        if (raw is null) return null;

        var glyphs = new List<PsfGlyph>();
        for (int i = 0; i < psf.UnkVecs.Count; i++)
        {
            var v = psf.UnkVecs[i];
            int x = (int)v.X, y = (int)v.Y, w = (int)v.Z, h = (int)v.W;
            if (w <= 0 || h <= 0) continue;
            glyphs.Add(new PsfGlyph(psf.UnkInt + i, 0, x, y, w, h));
        }
        return (new List<(int, int, byte[])> { raw.Value }, glyphs);
    }


    // Amedo 2026-09-26
    public static List<(string Name, int TexturePage, SysVec4 Swatch)> GetParticleSystemCatalog(PS2AnyTwinsanityRM2 rm2)
    {
        var result = new List<(string, int, SysVec4)>();
        var data = rm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        if (data is null) return result;
        foreach (var sys in data.ParticleSystems)
        {
            var name = ParticleNames.Clean(sys.Name);
            if (string.IsNullOrWhiteSpace(name)) name = "(unnamed)";
            result.Add((name, sys.UnkInt, ParticleFx.DefFor(sys).Swatch));
        }
        return result.OrderBy(t => t.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem? FindParticleSystem(PS2AnyTwinsanityRM2 rm2, string name)
    {
        var data = rm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        if (data is null) return null;
        foreach (var sys in data.ParticleSystems)
            if (string.Equals(ParticleNames.Clean(sys.Name), name, StringComparison.OrdinalIgnoreCase)) // Amedo 2026-09-26
                return sys;
        return null;
    }

    public readonly record struct ParticleTexturePage(Texture2D? Tex, int Width, int Height); // Amedo 2026-09-26

    public static ParticleTexturePage[] GetParticleTexturePages(GL gl, PS2AnyTwinsanityRM2 defaultRm2)
    {
        var result = new ParticleTexturePage[3];
        var data = defaultRm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2DefaultParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        if (data is null) return result;
        var gfx = defaultRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var texSec = gfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (texSec is null) return result;
        for (int i = 0; i < texSec.GetItemsAmount(); i++)
        {
            if (texSec.GetItem(i) is not PS2AnyTexture tex) continue;
            for (int p = 0; p < 3; p++)
            {
                if (tex.GetID() != data.TextureIDs[p]) continue;
                var gpuTex = ChunkImporter.DecodeTexture(gl, tex, false); // Amedo
                result[p] = new ParticleTexturePage(gpuTex, (int)(gpuTex?.Width ?? 0), (int)(gpuTex?.Height ?? 0));
            }
        }
        return result;
    }


    // Amedo 2026-09-30
    public static DecalAssets? GetDecalAssets(GL gl, PS2AnyTwinsanityRM2 defaultRm2)
    {
        var data = defaultRm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2DefaultParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        if (data is null) return null;
        var assets = DecalAssets.From(data);
        var texSec = defaultRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION)
                               ?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (texSec is not null && texSec.ContainsItem(data.DecalTextureID) && texSec.GetItem<PS2AnyTexture>(data.DecalTextureID) is { } tex)
            assets.Texture = ChunkImporter.DecodeTexture(gl, tex, false); // Amedo
        return assets;
    }
}
