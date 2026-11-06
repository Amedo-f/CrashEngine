using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private enum TextureSource { Rm2, GlobalRm2, Sm2 }

    private const int Ps2MaxSide = 256; // Amedo 2026-10-02

    private void ImportTextureOverMaterial(Material mat)
    {
        if (mat.Albedo is null) { _browser.Log("Import Texture: this material has no texture to replace."); return; }

        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource is null) return;

        if (!TraceTextureId(chunkSource, mat.Albedo, out uint id, out bool isScenery, out bool isGlobal))
        { _browser.Log("Import Texture: couldn't trace this texture back to a source id."); return; }

        var texObj = FindTextureById(chunkSource, id, isScenery, isGlobal, out var source);
        bool fromGlobal = source == TextureSource.GlobalRm2;
        if (texObj is null) { _browser.Log($"Import Texture: source PS2 texture {id:X8} not found in the chunk graph."); return; }

        ShowOpenFileDialog("Import Texture", "Images\0*.png;*.jpg;*.jpeg;*.bmp;*.tga\0All Files\0*.*\0\0", path =>
        {
        if (path is null) return;

        try
        {
            using var stream = File.OpenRead(path);
            var img = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);

            int dw, dh;
            if (texObj.TextureFormat == Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture.TexturePixelFormat.PSMT8)
            {
                double aspect = Math.Log2((double)img.Width / img.Height); // Amedo 2026-10-02
                var best = PS2AnyTexture.RegisteredPsmt8Sizes
                    .OrderBy(s => Math.Abs(Math.Log2((double)s.Width / s.Height) - aspect))
                    .ThenBy(s => Math.Abs(s.Width - img.Width) + Math.Abs(s.Height - img.Height))
                    .First();
                (dw, dh) = best;
            }
            else
            {
                dw = Math.Min(NearestPowerOfTwo(img.Width), Ps2MaxSide); // Amedo 2026-10-02
                dh = Math.Min(NearestPowerOfTwo(img.Height), Ps2MaxSide);
            }
            var rgba = (dw == img.Width && dh == img.Height)
                ? img.Data
                : dw <= img.Width && dh <= img.Height ? ResizeBox(img.Data, img.Width, img.Height, dw, dh) // Amedo 2026-10-02
                : ResizeNearest(img.Data, img.Width, img.Height, dw, dh);

            var colors = new List<TwinColor>(dw * dh);
            for (int i = 0; i < dw * dh; i++)
                colors.Add(new TwinColor(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]));

            texObj.FromBitmap(colors, dw, texObj.TexFun, texObj.TextureFormat);

            if (fromGlobal) chunkSource.GlobalRm2Dirty = true;

            string? key = ChunkImporter.SaveHd(texObj, img.Data, img.Width, img.Height); // Amedo
            if (key is not null) mat.Albedo.UpdateHighRes(img.Data, (uint)img.Width, (uint)img.Height, (uint)dw, (uint)dh);
            else mat.Albedo.UpdateData(rgba, (uint)dw, (uint)dh);

            _browser.Log($"Imported texture → Texture {id:X8} ({img.Width}x{img.Height}): PS2 {dw}x{dh}, PC {(key is not null ? $"{img.Width}x{img.Height}" : $"{dw}x{dh}")}, format {texObj.TextureFormat}" + // Amedo 2026-10-02
                         (fromGlobal ? "  [shared/global object — will also write Startup\\Default.rm2 on Save Chunk]" : ""));
        }
        catch (Exception ex) { _browser.Log($"Import Texture failed: {ex.Message}"); }
        });
    }

    private void ResetTextureToOriginal(Material mat)
    {
        if (mat.Albedo is null) { _browser.Log("Reset Texture: nothing to reset (no texture bound)."); return; }

        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource is null) return;

        if (!TraceTextureId(chunkSource, mat.Albedo, out uint id, out bool isScenery, out bool isGlobal))
        { _browser.Log("Reset Texture: couldn't trace this texture back to a source id."); return; }

        var texObj = FindTextureById(chunkSource, id, isScenery, isGlobal, out var source);
        bool fromGlobal = source == TextureSource.GlobalRm2;
        if (texObj is null) { _browser.Log($"Reset Texture: source PS2 texture {id:X8} not found in the chunk graph."); return; }

        try
        {
            PS2AnyTexture? freshTex;
            if (source == TextureSource.Sm2)
            {
                using var pkg = PackageReader.Open(ResolvePristinePackageSource());
                var path = _sm2;
                using var stream = pkg.OpenByPath(path)
                    ?? throw new FileNotFoundException($"Couldn't reopen {path} from the disc archive.");
                using var reader = new BinaryReader(stream);
                var freshSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
                freshSm2.Read(reader, (int)stream.Length);
                var freshGfx    = freshSm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
                var freshTexSec = freshGfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
                freshTex = freshTexSec?.GetItem<PS2AnyTexture>(id);
            }
            else
            {
                var freshRm2 = ReadPristineRm2(fromGlobal ? GlobalRm2Path : _rm2);
                var freshGfx    = freshRm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.LEVEL_GRAPHICS_SECTION);
                var freshTexSec = freshGfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
                freshTex = freshTexSec?.GetItem<PS2AnyTexture>(id);
            }
            if (freshTex is null) { _browser.Log($"Reset Texture: original texture {id:X8} not found on disc."); return; }

            using (var buf = new MemoryStream())
            {
                using (var bw = new BinaryWriter(buf, System.Text.Encoding.Default, leaveOpen: true))
                    freshTex.Write(bw);
                buf.Position = 0;
                using var br = new BinaryReader(buf);
                texObj.Read(br, (int)buf.Length);
            }

            texObj.CalculateData();
            int w = texObj.ImageWidthPower  > 0 ? (1 << texObj.ImageWidthPower)  : 1;
            int h = texObj.ImageHeightPower > 0 ? (1 << texObj.ImageHeightPower) : 1;
            var rgba = new byte[Math.Max(texObj.Colors.Count, w * h) * 4];
            int idx = 0;
            foreach (TwinColor c in texObj.Colors)
            {
                rgba[idx++] = c.R; rgba[idx++] = c.G; rgba[idx++] = c.B; rgba[idx++] = c.A;
            }
            mat.Albedo.UpdateData(rgba, (uint)w, (uint)h);

            if (fromGlobal) chunkSource.GlobalRm2Dirty = true;
            _browser.Log($"Reset texture {id:X8} to its original disc data ({w}x{h}).");
        }
        catch (Exception ex) { _browser.Log($"Reset Texture failed: {ex.Message}"); }
    }

    private static bool TraceTextureId(ChunkSource src, Texture2D albedo, out uint id, out bool isScenery, out bool isGlobal)
    {
        foreach (var kv in src.GlobalTexCache)
            if (ReferenceEquals(kv.Value, albedo)) { id = kv.Key; isScenery = false; isGlobal = true; return true; }
        foreach (var kv in src.TexCache)
            if (ReferenceEquals(kv.Value, albedo)) { id = kv.Key; isScenery = false; isGlobal = false; return true; }
        foreach (var kv in src.SceneryTexCache)
            if (ReferenceEquals(kv.Value, albedo)) { id = kv.Key; isScenery = true; isGlobal = false; return true; }
        id = 0; isScenery = false; isGlobal = false;
        return false;
    }

    private static PS2AnyTexture? FindTextureById(ChunkSource src, uint id, bool isScenery, bool isGlobal, out TextureSource source)
    {
        if (isScenery)
        {
            var gfx    = src.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
            var texSec = gfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
            source = TextureSource.Sm2;
            return texSec?.GetItem<PS2AnyTexture>(id);
        }

        PS2AnyTexture? Lookup(Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2? rm2)
        {
            var gfx    = rm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.LEVEL_GRAPHICS_SECTION);
            var texSec = gfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
            return texSec?.GetItem<PS2AnyTexture>(id);
        }
        source = isGlobal ? TextureSource.GlobalRm2 : TextureSource.Rm2;
        return isGlobal ? Lookup(src.GlobalRm2) : Lookup(src.Rm2);
    }

    private static int NearestPowerOfTwo(int v)
    {
        v = Math.Clamp(v, 4, 1024);
        int lower = 1 << (int)Math.Floor(Math.Log2(v));
        int upper = lower << 1;
        return (v - lower) < (upper - v) ? lower : upper;
    }

    // Amedo 2026-10-02
    private static byte[] ResizeBox(byte[] src, int sw, int sh, int dw, int dh)
    {
        var dst = new byte[dw * dh * 4];
        for (int y = 0; y < dh; y++)
        {
            int y0 = y * sh / dh, y1 = Math.Max(y0 + 1, (y + 1) * sh / dh);
            for (int x = 0; x < dw; x++)
            {
                int x0 = x * sw / dw, x1 = Math.Max(x0 + 1, (x + 1) * sw / dw);
                int r = 0, g = 0, b = 0, a = 0, n = (x1 - x0) * (y1 - y0);
                for (int yy = y0; yy < y1; yy++)
                    for (int xx = x0; xx < x1; xx++)
                    {
                        int si = (yy * sw + xx) * 4;
                        r += src[si]; g += src[si + 1]; b += src[si + 2]; a += src[si + 3];
                    }
                int di = (y * dw + x) * 4;
                dst[di] = (byte)(r / n); dst[di + 1] = (byte)(g / n); dst[di + 2] = (byte)(b / n); dst[di + 3] = (byte)(a / n);
            }
        }
        return dst;
    }

    private static byte[] ResizeNearest(byte[] src, int sw, int sh, int dw, int dh)
    {
        var dst = new byte[dw * dh * 4];
        for (int y = 0; y < dh; y++)
        {
            int sy = Math.Min(sh - 1, y * sh / dh);
            for (int x = 0; x < dw; x++)
            {
                int sx = Math.Min(sw - 1, x * sw / dw);
                int si = (sy * sw + sx) * 4;
                int di = (y * dw + x) * 4;
                dst[di] = src[si]; dst[di + 1] = src[si + 1]; dst[di + 2] = src[si + 2]; dst[di + 3] = src[si + 3];
            }
        }
        return dst;
    }
}
