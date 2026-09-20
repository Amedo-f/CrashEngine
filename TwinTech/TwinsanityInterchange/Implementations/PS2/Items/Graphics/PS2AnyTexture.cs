using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Twinsanity.Libraries;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics
{
    public class PS2AnyTexture : BaseTwinItem, ITwinTexture
    {
        static Dictionary<string, TextureDescriptor> TextureDescriptorHelper;
        public List<Color> Colors { get; set; } = new List<Color>();
        public UInt32 HeaderSignature { get; set; }
        public UInt16 ImageWidthPower { get; set; }
        public UInt16 ImageHeightPower { get; set; }
        public Byte MipLevels { get; set; }
        public ITwinTexture.TexturePixelFormat TextureFormat { get; set; }
        public ITwinTexture.TexturePixelFormat DestinationTextureFormat { get; set; }
        public ITwinTexture.TextureColorComponent ColorComponent { get; set; }
        public Byte UnkByte { get; set; }
        public ITwinTexture.TextureFunction TexFun { get; set; }
        public Byte[] UnkBytes1 { get; set; }
        public Int32 TextureBasePointer { get; set; }
        public Int32[] MipLevelsTBP { get; set; }
        public Int32 TextureBufferWidth { get; set; }
        public Int32[] MipLevelsTBW { get; set; }
        public Int32 ClutBufferBasePointer { get; set; }
        public Byte[] UnkBytes2 { get; set; }
        public Byte[] UnkBytes3 { get; set; }
        public Byte[] UnusedMetadata { get; set; }
        public Byte[] TextureData { get; set; }

        // Amedo 2026-09-11 -- expose TextureDescriptorHelper read-only: it's a fixed 15-size
        // whitelist (max 128x256), and a PSMT8 bake for an unlisted size throws; callers snap first.
        private static void EnsureTextureDescriptorHelperLoaded()
        {
            if (TextureDescriptorHelper != null) return;
            string codeBase = Assembly.GetExecutingAssembly().Location;
            UriBuilder uri = new(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            using FileStream stream = new(Path.Combine(Path.GetDirectoryName(path), @"TextureDescriptionHelper.json"), FileMode.Open, FileAccess.Read);
            using StreamReader reader = new(stream);
            TextureDescriptorHelper = JsonSerializer.Deserialize<Dictionary<string, TextureDescriptor>>(reader.ReadToEnd());
        }

        /// <summary>True only if "{width}x{height}" (EXACT order, not "{height}x{width}") is
        /// one of the 15 real, calibrated PSMT8 sizes. Use before calling FromBitmap with
        /// PSMT8 for an arbitrary/user-supplied size to avoid a raw KeyNotFoundException.</summary>
        public static bool IsRegisteredPsmt8Size(int width, int height)
        {
            EnsureTextureDescriptorHelperLoaded();
            return TextureDescriptorHelper.ContainsKey($"{width}x{height}");
        }

        /// <summary>Every real, calibrated (Width,Height) pair PSMT8 supports -- exact order as
        /// stored, e.g. (128,256) is registered but (256,128) is NOT (confirmed: the JSON has no
        /// reverse-order duplicate entries).</summary>
        public static IReadOnlyList<(int Width, int Height)> RegisteredPsmt8Sizes
        {
            get
            {
                EnsureTextureDescriptorHelperLoaded();
                var list = new List<(int, int)>();
                foreach (var key in TextureDescriptorHelper.Keys)
                {
                    var parts = key.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                        list.Add((w, h));
                }
                return list;
            }
        }

        public PS2AnyTexture()
        {
            EnsureTextureDescriptorHelperLoaded();
            UnusedMetadata = new byte[32];
            HeaderSignature = 0xbbcccdcd;
            DestinationTextureFormat = ITwinTexture.TexturePixelFormat.PSMCT32;
            ColorComponent = ITwinTexture.TextureColorComponent.RGBA;
            UnkByte = 0;
            TextureBasePointer = 0;
            MipLevelsTBP = new int[6];
            TextureBufferWidth = 4;
            MipLevelsTBW = new int[6];
            ClutBufferBasePointer = 0;
            UnkBytes1 = new byte[2];
            UnkBytes2 = new byte[4] { 224, 0, 2, 0 };
            UnkBytes3 = new byte[2] { 0, 2 };
            UnusedMetadata = new byte[32];
            UnusedMetadata[0] = 31;
            UnusedMetadata[16] = 64;
            UnusedMetadata[17] = 246;
            UnusedMetadata[18] = 89;
            UnusedMetadata[19] = 32;
        }

        public override Int32 GetLength()
        {
            return 4 + 96 + UnusedMetadata.Length + (TextureData != null ? TextureData.Length : 0);
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            int dataLen = reader.ReadInt32();
            HeaderSignature = reader.ReadUInt32();
            ImageWidthPower = reader.ReadUInt16();
            ImageHeightPower = reader.ReadUInt16();
            MipLevels = reader.ReadByte();
            TextureFormat = (ITwinTexture.TexturePixelFormat)reader.ReadByte();
            DestinationTextureFormat = (ITwinTexture.TexturePixelFormat)reader.ReadByte();
            ColorComponent = (ITwinTexture.TextureColorComponent)reader.ReadByte();
            UnkByte = reader.ReadByte();
            TexFun = (ITwinTexture.TextureFunction)reader.ReadByte();
            UnkBytes1 = reader.ReadBytes(2);
            TextureBasePointer = reader.ReadInt32();
            MipLevelsTBP = new int[6];
            for (var i = 0; i < 6; ++i)
            {
                MipLevelsTBP[i] = reader.ReadInt32();
            }
            TextureBufferWidth = reader.ReadInt32();
            MipLevelsTBW = new int[6];
            for (var i = 0; i < 6; ++i)
            {
                MipLevelsTBW[i] = reader.ReadInt32();
            }
            ClutBufferBasePointer = reader.ReadInt32();
            reader.ReadInt32(); // CLUT buffer width, always 1 meaning always 64
            UnkBytes2 = reader.ReadBytes(4);
            reader.ReadInt32(); // Reserved
            reader.ReadInt32(); // Reserved
            UnkBytes3 = reader.ReadBytes(2);
            reader.ReadBytes(2); // Reserved
            reader.Read(UnusedMetadata, 0, UnusedMetadata.Length);
            TextureData = reader.ReadBytes(dataLen - 96 - UnusedMetadata.Length);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(GetLength() - 4);
            writer.Write(HeaderSignature);
            writer.Write(ImageWidthPower);
            writer.Write(ImageHeightPower);
            writer.Write(MipLevels);
            writer.Write((Byte)TextureFormat);
            writer.Write((Byte)DestinationTextureFormat);
            writer.Write((Byte)ColorComponent);
            writer.Write(UnkByte);
            writer.Write((Byte)TexFun);
            writer.Write(UnkBytes1);
            writer.Write(TextureBasePointer);
            for (var i = 0; i < 6; ++i)
            {
                writer.Write(MipLevelsTBP[i]);
            }
            writer.Write(TextureBufferWidth);
            for (var i = 0; i < 6; ++i)
            {
                writer.Write(MipLevelsTBW[i]);
            }
            writer.Write(ClutBufferBasePointer);
            writer.Write(1); // CLUT buffer width
            writer.Write(UnkBytes2);
            writer.Write(0); // Reserved
            writer.Write(0); // Reserved
            writer.Write(UnkBytes3);
            writer.Write((Int16)0); // Reserved
            writer.Write(UnusedMetadata);
            writer.Write(TextureData);
        }

        public void CalculateData()
        {
            var interpreter = VIFInterpreter.InterpretCode(TextureData);
            var data = interpreter.GetGifMem();
            Colors.Clear();
            switch (TextureFormat)
            {
                case ITwinTexture.TexturePixelFormat.PSMCT32:
                    EzSwizzle.TagToColors(data[1], Colors);
                    foreach (var c in Colors)
                    {
                        c.ScaleAlphaUp();
                    }
                    break;
                case ITwinTexture.TexturePixelFormat.PSMT8:
                    byte[] gifData = EzSwizzle.TagToBytes(data[1]);
                    int RRW = (int)((data[0].Data[1].Output >> 0) & 0xFFFFFFFF);
                    int RRH = (int)((data[0].Data[1].Output >> 32) & 0xFFFFFFFF);
                    int Width = (int)(Math.Pow(2, ImageWidthPower));
                    int Height = (int)(Math.Pow(2, ImageHeightPower));
                    byte[] rawTextureData = EzSwizzle.writeTexPSMCT32(0, 1, 0, 0, RRW, RRH, gifData);
                    byte[] texData = EzSwizzle.readTexPSMT8(0, TextureBufferWidth, 0, 0, Width, Height, rawTextureData, false);
                    byte[] paletteData = EzSwizzle.readTexPSMCT32(ClutBufferBasePointer, 1, 0, 0, 16, 16, rawTextureData, false);
                    List<Color> palette = EzSwizzle.BytesToColors(paletteData);
                    for (int i = 0; i < 8; i++)
                    {
                        for (int j = 8; j < 16; j++)
                        {
                            Color tmp = palette[j + i * 32];
                            palette[j + i * 32] = palette[j + i * 32 + 8];
                            palette[j + i * 32 + 8] = tmp;
                        }
                    }
                    foreach (var c in palette)
                    {
                        c.ScaleAlphaUp();
                    }
                    int Pixels = Width * Height;
                    for (var i = 0; i < Pixels; ++i)
                    {
                        Colors.Add(palette[texData[i]]);
                    }
                    break;
            }
        }

        // Amedo 2026-09-19
        private static List<Color> MedianCutPalette(List<Color> image, int maxColors)
        {
            var counts = new Dictionary<uint, (int W, byte R, byte G, byte B, byte A)>();
            foreach (var c in image)
            {
                uint k = c.ToARGB();
                if (counts.TryGetValue(k, out var e)) counts[k] = (e.W + 1, e.R, e.G, e.B, e.A);
                else counts[k] = (1, c.R, c.G, c.B, c.A);
            }
            var all = new List<(byte R, byte G, byte B, byte A, int W)>(counts.Count);
            foreach (var e in counts.Values) all.Add((e.R, e.G, e.B, e.A, e.W));

            var boxes = new List<List<(byte R, byte G, byte B, byte A, int W)>> { all };
            while (boxes.Count < maxColors)
            {
                int pick = -1, bestRange = -1, bestAxis = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    var bx = boxes[i];
                    if (bx.Count < 2) continue;
                    byte rmin = 255, rmax = 0, gmin = 255, gmax = 0, bmin = 255, bmax = 0, amin = 255, amax = 0;
                    foreach (var p in bx)
                    {
                        if (p.R < rmin) rmin = p.R; if (p.R > rmax) rmax = p.R;
                        if (p.G < gmin) gmin = p.G; if (p.G > gmax) gmax = p.G;
                        if (p.B < bmin) bmin = p.B; if (p.B > bmax) bmax = p.B;
                        if (p.A < amin) amin = p.A; if (p.A > amax) amax = p.A;
                    }
                    int axis = 0, rng = rmax - rmin;
                    if (gmax - gmin > rng) { rng = gmax - gmin; axis = 1; }
                    if (bmax - bmin > rng) { rng = bmax - bmin; axis = 2; }
                    if (amax - amin > rng) { rng = amax - amin; axis = 3; }
                    if (rng > bestRange) { bestRange = rng; pick = i; bestAxis = axis; }
                }
                if (pick < 0) break;
                var box = boxes[pick];
                box.Sort((x, y) => ChannelOf(x, bestAxis) - ChannelOf(y, bestAxis));
                int mid = box.Count / 2;
                boxes[pick] = box.GetRange(0, mid);
                boxes.Add(box.GetRange(mid, box.Count - mid));
            }
            var pal = new List<Color>(boxes.Count);
            foreach (var bx in boxes)
            {
                long r = 0, g = 0, b = 0, a = 0, w = 0;
                foreach (var p in bx) { r += (long)p.R * p.W; g += (long)p.G * p.W; b += (long)p.B * p.W; a += (long)p.A * p.W; w += p.W; }
                if (w == 0) w = 1;
                pal.Add(new Color((byte)(r / w), (byte)(g / w), (byte)(b / w), (byte)(a / w)));
            }
            return pal;
        }

        private static int ChannelOf((byte R, byte G, byte B, byte A, int W) p, int axis)
            => axis == 0 ? p.R : axis == 1 ? p.G : axis == 2 ? p.B : p.A;

        public void FromBitmap(List<Color> image, Int32 width, ITwinTexture.TextureFunction fun, ITwinTexture.TexturePixelFormat format, bool generateMipmaps = false)
        {
            int height = image.Count / width;
            TexFun = fun;
            TextureFormat = format;
            TextureBufferWidth = (int)Math.Ceiling(width / 64.0f);
            ImageWidthPower = (ushort)Math.Log2(width);
            ImageHeightPower = (ushort)Math.Log2(height);
            if (width != 256 && generateMipmaps)
            {
                TextureDescriptor textureDescriptor = TextureDescriptorHelper[$"{width}x{height}"];
                ClutBufferBasePointer = textureDescriptor.CBP;
                MipLevelsTBP = textureDescriptor.MipTBP;
                MipLevelsTBW = textureDescriptor.MipTBW;
                MipLevels = (byte)textureDescriptor.MipLevels;
            }
            else
            {
                ClutBufferBasePointer = 0;
                MipLevelsTBP = new Int32[6];
                MipLevelsTBW = new Int32[6];
                MipLevels = 1;
            }

            if (format == ITwinTexture.TexturePixelFormat.PSMT8 && !generateMipmaps)
            {
                TextureDescriptor textureDescriptor = TextureDescriptorHelper[$"{width}x{height}"];
                ClutBufferBasePointer = textureDescriptor.CBP;
            }

            //this is probably not bytes but whatever
            UnkBytes2[1] = UnkBytes3[0] = (Byte)((width == 256) ? 0 : (byte)Math.Min(width, height));
            UnkBytes2[2] = UnkBytes3[1] = (Byte)((width == 256) ? 2 : 0);

            GIFTag headerTag = new GIFTag();
            headerTag.REGS = new REGSEnum[16];
            headerTag.REGS[0] = REGSEnum.ApD;
            headerTag.NLOOP = 3;
            headerTag.NREG = 1;
            headerTag.FLG = GIFModeEnum.PACKED;
            headerTag.Data = new List<RegOutput>();
            RegOutput head1 = new RegOutput();
            head1.REG = REGSEnum.ApD;
            head1.Address = 81;
            RegOutput head2 = new RegOutput();
            head2.REG = REGSEnum.ApD;
            head2.Address = 82;
            RegOutput head3 = new RegOutput();
            head3.REG = REGSEnum.ApD;
            head3.Address = 83;
            headerTag.Data.Add(head1);
            headerTag.Data.Add(head2);
            headerTag.Data.Add(head3);
            GIFTag tag;
            if (format == ITwinTexture.TexturePixelFormat.PSMCT32)
            {
                foreach (var c in image)
                {
                    c.ScaleAlphaDown();
                }
                tag = EzSwizzle.ColorsToTag(image);
                // Amedo 2026-09-16
                head2.Output = ((ulong)height << 32) | (ulong)width;
            }
            else
            {
                byte[] textureData = new byte[width * height];
                byte[] paletteData = new byte[256 * 4];
                List<Color> palette;
                var index = 0;
                // Amedo 2026-09-19
                var distinct = new List<Color>();
                var seenKeys = new HashSet<uint>();
                foreach (var c in image)
                    if (seenKeys.Add(c.ToARGB())) distinct.Add(c);
                if (distinct.Count <= 256)
                {
                    palette = distinct;
                    while (palette.Count < 256) palette.Add(new Color());
                    foreach (var c in image)
                    {
                        textureData[index] = (Byte)palette.IndexOf(c);
                        ++index;
                    }
                }
                else
                {
                    palette = MedianCutPalette(image, 256);
                    while (palette.Count < 256) palette.Add(new Color());
                    var nearestCache = new Dictionary<uint, byte>();
                    foreach (var c in image)
                    {
                        uint key = c.ToARGB();
                        if (!nearestCache.TryGetValue(key, out var pIdx))
                        {
                            int best = 0; long bestD = long.MaxValue;
                            for (int i = 0; i < 256; i++)
                            {
                                var pc = palette[i];
                                long dr = c.R - pc.R, dg = c.G - pc.G, db = c.B - pc.B, da = c.A - pc.A;
                                long d = dr * dr + dg * dg + db * db + da * da;
                                if (d < bestD) { bestD = d; best = i; if (d == 0) break; }
                            }
                            pIdx = (byte)best; nearestCache[key] = pIdx;
                        }
                        textureData[index] = pIdx;
                        ++index;
                    }
                }
                foreach (var c in palette)
                {
                    c.ScaleAlphaDown();
                }
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 8; j < 16; j++)
                    {
                        var srcIndex = j + i * 32 + 8;
                        var dstIndex = j + i * 32;
                        Color tmp = palette[srcIndex];
                        palette[srcIndex] = palette[dstIndex];
                        palette[dstIndex] = tmp;
                    }
                }
                index = 0;
                foreach (var c in palette)
                {
                    EzSwizzle.ColorsToByte(c, paletteData, index);
                    ++index;
                }

                TextureDescriptor textureDescriptor = TextureDescriptorHelper[$"{width}x{height}"];
                ulong high = (ulong)textureDescriptor.RRH;
                ulong low = (ulong)textureDescriptor.RRW;
                head2.Output = (high << 32) | (low);
                byte[] rawTextureData = new byte[textureDescriptor.RRH * 256];
                Array.Fill<byte>(rawTextureData, 0xFF);

                EzSwizzle.writeTexPSMT8To(0, TextureBufferWidth, 0, 0, width, height, textureData, rawTextureData);
                var prevData = textureData;
                var mipWidth = width;
                var mipHeight = height;
                for (var i = 1; i < MipLevels; ++i)
                {
                    mipWidth /= 2;
                    mipHeight /= 2;
                    var mipData = new byte[mipWidth * mipHeight];
                    for (var y = 0; y < mipHeight; ++y)
                    {
                        for (var x = 0; x < mipWidth; ++x)
                        {
                            var prevWidth = mipWidth * 2;
                            var srcX = x * 2;
                            var srcY = y * 2;
                            mipData[x + y * mipWidth] = prevData[srcX + srcY * prevWidth];
                        }
                    }
                    EzSwizzle.writeTexPSMT8To(MipLevelsTBP[i - 1], MipLevelsTBW[i - 1], 0, 0, mipWidth, mipHeight, mipData, rawTextureData);
                    prevData = mipData;
                }
                EzSwizzle.writeTexPSMCT32To(ClutBufferBasePointer, 1, 0, 0, 16, 16, paletteData, rawTextureData);
                byte[] gifData = EzSwizzle.readTexPSMCT32(0, 1, 0, 0, textureDescriptor.RRW, textureDescriptor.RRH, rawTextureData);
                tag = EzSwizzle.ColorsToTag(EzSwizzle.BytesToColors(gifData));
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            {
                var QWC = (UInt64)headerTag.GetLength() + (UInt64)tag.GetLength() + 2;
                UInt64 low = QWC;
                low |= (UInt64)6 << 28;
                writer.Write(low);
                VIFCode code1 = new VIFCode();
                code1.OP = VIFCodeEnum.NOP;
                code1.Write(writer);
                VIFCode code2 = new VIFCode();
                code2.OP = VIFCodeEnum.DIRECT;
                code2.Immediate = (ushort)QWC;
                code2.Write(writer);
                headerTag.Write(writer);
                tag.Write(writer);
                writer.Flush();
                TextureData = stream.ToArray();
            }
        }

        public override String GetName()
        {
            return $"Texture {id:X}";
        }

        public struct TextureDescriptor
        {
            public Int32 MipLevels { get; set; }
            public Int32 CBP { get; set; }
            public Int32 RRW { get; set; }
            public Int32 RRH { get; set; }
            public Int32[] MipTBP { get; set; }
            public Int32[] MipTBW { get; set; }

        }
    }
}
