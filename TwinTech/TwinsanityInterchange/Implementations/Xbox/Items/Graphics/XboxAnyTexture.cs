using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics
{
    public class XboxAnyTexture : BaseTwinItem, ITwinTexture
    {
        static Dictionary<string, TextureDescriptor> TextureDescriptorHelper;

        UInt32 textureType;

        public List<Color> Colors { get; set; }
        public UInt32 HeaderSignature { get; set; }
        public UInt16 ImageWidthPower { get; set; }
        public UInt16 ImageHeightPower { get; set; }
        public Byte MipLevels { get; set; }

        private ITwinTexture.TexturePixelFormat ps2TextureFormat;
        public ITwinTexture.TexturePixelFormat TextureFormat
        {
            get
            {
                if (textureType == 2)
                {
                    return ITwinTexture.TexturePixelFormat.DXT5;
                }

                return ITwinTexture.TexturePixelFormat.Raw;
            }
            set
            {
                if (ITwinTexture.TexturePixelFormat.DXT5 == value)
                {
                    textureType = 2;
                    return;
                }

                textureType = 0;
            }
        }
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

        public XboxAnyTexture()
        {
            if (TextureDescriptorHelper == null)
            {
                string codeBase = Assembly.GetExecutingAssembly().Location;
                UriBuilder uri = new(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                using FileStream stream = new(Path.Combine(Path.GetDirectoryName(path), @"TextureDescriptionHelper.json"), FileMode.Open, FileAccess.Read);
                using StreamReader reader = new(stream);
                TextureDescriptorHelper = JsonSerializer.Deserialize<Dictionary<string, TextureDescriptor>>(reader.ReadToEnd());
            }
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
            UnkBytes2 = new byte[8] { 0, 0, 0, 0, 224, 0, 2, 0 };
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
            return 4 + 100 + UnusedMetadata.Length + (TextureData != null ? TextureData.Length : 0);
        }

        public override String GetName()
        {
            return $"Texture {id:X}";
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            int dataLen = reader.ReadInt32();
            HeaderSignature = reader.ReadUInt32();
            ImageWidthPower = reader.ReadUInt16();
            ImageHeightPower = reader.ReadUInt16();
            MipLevels = reader.ReadByte();
            ps2TextureFormat = (ITwinTexture.TexturePixelFormat)reader.ReadByte();
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
            UnkBytes2 = reader.ReadBytes(8);
            reader.ReadInt32(); // Reserved
            reader.ReadInt32(); // Reserved
            UnkBytes3 = reader.ReadBytes(2);
            reader.ReadBytes(2); // Reserved
            reader.Read(UnusedMetadata, 0, UnusedMetadata.Length);

            // XBox specific
            textureType = reader.ReadUInt32();
            TextureData = reader.ReadBytes(dataLen - 100 - UnusedMetadata.Length);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(GetLength() - 4);
            writer.Write(HeaderSignature);
            writer.Write(ImageWidthPower);
            writer.Write(ImageHeightPower);
            writer.Write(MipLevels);
            writer.Write((Byte)ps2TextureFormat);
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
            writer.Write(UnkBytes2);
            writer.Write(0); // Reserved
            writer.Write(0); // Reserved
            writer.Write(UnkBytes3);
            writer.Write((Int16)0); // Reserved
            writer.Write(UnusedMetadata);
            writer.Write(textureType);
            writer.Write(TextureData);
        }

        public void CalculateData()
        {
            using var ms = new MemoryStream(TextureData);
            using var reader = new BinaryReader(ms);
            var width = (1 << ImageWidthPower);
            var height = (1 << ImageHeightPower);
            Color[] rawData;
            byte[] imageData;
            if (textureType != 0)
            {
                // BC3/DXT5 compression
                imageData = reader.ReadBytes(width * height);
                byte[] decompressed = new byte[imageData.Length * 4];
                DecompressDXT5(imageData, width, height, decompressed);

                rawData = new Color[width * height];
                int b = 0;
                int c = 0;
                for (int y = height - 1; y >= 0; y--)
                {
                    c = (width * y);
                    for (int x = 0; x < width; x++)
                    {
                        rawData[c + x] = new Color(decompressed[b + 3], decompressed[b + 2], decompressed[b + 1], decompressed[b + 0]);
                        b += 4;
                    }
                }
            }
            else
            {
                // Uncompressed pixels (PSM)
                imageData = Array.Empty<Byte>();
                rawData = new Color[width * height];
                int c = 0;
                for (int y = height - 1; y >= 0; y--)
                {
                    c = (width * y);
                    for (int x = 0; x < width; x++)
                    {
                        byte[] clr = reader.ReadBytes(4);
                        rawData[c + x] = new Color(clr[3], clr[2], clr[1], clr[0]);
                    }
                }
            }

            Colors = new List<Color>(rawData);
        }

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
            //this is probably not bytes but whatever
            UnkBytes2[5] = UnkBytes3[0] = (Byte)((width == 256) ? 0 : (byte)Math.Min(width, height));
            UnkBytes2[6] = UnkBytes3[1] = (Byte)((width == 256) ? 2 : 0);

            if (textureType == 2)
            {
                Byte[] rawData = new Byte[image.Sum(_ => 4)];
                var byteIndex = 0;
                for (Int32 i = 0; i < image.Count; i++)
                {
                    rawData[byteIndex++] = image[i].A;
                    rawData[byteIndex++] = image[i].R;
                    rawData[byteIndex++] = image[i].G;
                    rawData[byteIndex++] = image[i].B;
                }

                CompressDXT5(rawData, (UInt32)width, (UInt32)height, out Byte[] compressedData);
                TextureData = compressedData;
            }
            else
            {
                Byte[] rawData = new Byte[image.Sum(_ => 4)];
                var byteIndex = 0;
                for (Int32 i = 0; i < image.Count; i++)
                {
                    rawData[byteIndex++] = image[i].A;
                    rawData[byteIndex++] = image[i].R;
                    rawData[byteIndex++] = image[i].G;
                    rawData[byteIndex++] = image[i].B;
                }

                TextureData = rawData;
            }
        }

        #region Compression
        public static void DecompressDXT1(byte[] input, int width, int height, byte[] output)
        {
            int offset = 0;
            int bcw = (width + 3) / 4;
            int bch = (height + 3) / 4;
            int clen_last = (width + 3) % 4 + 1;
            uint[] buffer = new uint[16];
            int[] colors = new int[4];
            for (int t = 0; t < bch; t++)
            {
                for (int s = 0; s < bcw; s++, offset += 8)
                {
                    int q0 = input[offset + 0] | input[offset + 1] << 8;
                    int q1 = input[offset + 2] | input[offset + 3] << 8;
                    Rgb565(q0, out Int32 r0, out Int32 g0, out Int32 b0);
                    Rgb565(q1, out Int32 r1, out Int32 g1, out Int32 b1);
                    colors[0] = TColor(r0, g0, b0, 255);
                    colors[1] = TColor(r1, g1, b1, 255);
                    if (q0 > q1)
                    {
                        colors[2] = TColor((r0 * 2 + r1) / 3, (g0 * 2 + g1) / 3, (b0 * 2 + b1) / 3, 255);
                        colors[3] = TColor((r0 + r1 * 2) / 3, (g0 + g1 * 2) / 3, (b0 + b1 * 2) / 3, 255);
                    }
                    else
                    {
                        colors[2] = TColor((r0 + r1) / 2, (g0 + g1) / 2, (b0 + b1) / 2, 255);
                    }

                    uint d = BitConverter.ToUInt32(input, offset + 4);
                    for (int i = 0; i < 16; i++, d >>= 2)
                    {
                        buffer[i] = unchecked((uint)colors[d & 3]);
                    }

                    int clen = (s < bcw - 1 ? 4 : clen_last) * 4;
                    for (int i = 0, y = t * 4; i < 4 && y < height; i++, y++)
                    {
                        Buffer.BlockCopy(buffer, i * 4 * 4, output, (y * width + s * 4) * 4, clen);
                    }
                }
            }
        }

        public static void DecompressDXT3(byte[] input, int width, int height, byte[] output)
        {
            int offset = 0;
            int bcw = (width + 3) / 4;
            int bch = (height + 3) / 4;
            int clen_last = (width + 3) % 4 + 1;
            uint[] buffer = new uint[16];
            int[] colors = new int[4];
            int[] alphas = new int[16];
            for (int t = 0; t < bch; t++)
            {
                for (int s = 0; s < bcw; s++, offset += 16)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int alpha = input[offset + i * 2] | input[offset + i * 2 + 1] << 8;
                        alphas[i * 4 + 0] = (((alpha >> 0) & 0xF) * 0x11) << 24;
                        alphas[i * 4 + 1] = (((alpha >> 4) & 0xF) * 0x11) << 24;
                        alphas[i * 4 + 2] = (((alpha >> 8) & 0xF) * 0x11) << 24;
                        alphas[i * 4 + 3] = (((alpha >> 12) & 0xF) * 0x11) << 24;
                    }

                    int q0 = input[offset + 8] | input[offset + 9] << 8;
                    int q1 = input[offset + 10] | input[offset + 11] << 8;
                    Rgb565(q0, out Int32 r0, out Int32 g0, out Int32 b0);
                    Rgb565(q1, out Int32 r1, out Int32 g1, out Int32 b1);
                    colors[0] = TColor(r0, g0, b0, 0);
                    colors[1] = TColor(r1, g1, b1, 0);
                    if (q0 > q1)
                    {
                        colors[2] = TColor((r0 * 2 + r1) / 3, (g0 * 2 + g1) / 3, (b0 * 2 + b1) / 3, 0);
                        colors[3] = TColor((r0 + r1 * 2) / 3, (g0 + g1 * 2) / 3, (b0 + b1 * 2) / 3, 0);
                    }
                    else
                    {
                        colors[2] = TColor((r0 + r1) / 2, (g0 + g1) / 2, (b0 + b1) / 2, 0);
                    }

                    uint d = BitConverter.ToUInt32(input, offset + 12);
                    for (int i = 0; i < 16; i++, d >>= 2)
                    {
                        buffer[i] = unchecked((uint)(colors[d & 3] | alphas[i]));
                    }

                    int clen = (s < bcw - 1 ? 4 : clen_last) * 4;
                    for (int i = 0, y = t * 4; i < 4 && y < height; i++, y++)
                    {
                        Buffer.BlockCopy(buffer, i * 4 * 4, output, (y * width + s * 4) * 4, clen);
                    }
                }
            }
        }

        // Implementation based on https://github.com/bhlzlx/DXT5-Compression
        public static void CompressDXT5(byte[] input, uint width, uint height, out byte[] output)
        {
            var totalBlockCols = ((width + 3) & ~(3)) / 4;
            var totalBlockRows = ((height + 3) & ~(3)) / 4;
            output = new byte[16 * totalBlockCols * totalBlockRows];
            DXT5Block[] blocks = new DXT5Block[totalBlockCols * totalBlockRows];
            var blockIndex = 0;
            for (UInt32 blockCol = 0; blockCol < totalBlockCols; blockCol++)
            {
                for (UInt32 blockRow = 0; blockRow < totalBlockRows; blockRow++)
                {
                    UInt32[] bitmapBlock = new UInt32[16];
                    for (UInt32 localPixelCol = 0; localPixelCol < 4; localPixelCol++)
                    {
                        UInt32 pixelCol = blockCol * 4 + localPixelCol;
                        for (UInt32 localPixelRow = 0; localPixelRow < 4; localPixelRow++)
                        {
                            UInt32 pixelRow = localPixelRow + blockRow * 4;
                            UInt32 localPixelIndex = localPixelRow + localPixelCol * 4;
                            UInt32 pixelIndex = pixelRow + pixelCol * width;
                            if (pixelCol >= height || pixelRow >= width)
                            {
                                bitmapBlock[localPixelRow + localPixelCol * 4] = bitmapBlock[0];
                            }
                            else
                            {
                                bitmapBlock[localPixelIndex] = BitConverter.ToUInt32(input, (Int32)pixelIndex * 4);
                            }
                        }
                    }
                    blocks[blockIndex++].CompressBitmap(bitmapBlock, 4, 4);
                }
            }

            for (Int32 i = 0; i < blocks.Length; i++)
            {
                var alphaBytes = BitConverter.GetBytes(blocks[i].Alpha);
                var colorBytes = BitConverter.GetBytes(blocks[i].Color);
                for (Int32 j = 0; j < 8; ++j)
                {
                    output[i * 16 + j] = alphaBytes[j];
                    output[i * 16 + j + 8] = colorBytes[j];
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct DXT5Block
        {
            public UInt64 Alpha;
            public UInt64 Color;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Byte CalculateAlphaLevel(Byte min, Byte max, Byte alpha)
            {
                if (min != max)
                {
                    Byte level = (Byte)(((max - alpha) * 0xFF / (max - min)) >> 5);
                    Byte[] map = { 0, 2, 3, 4, 5, 6, 7, 1 };
                    return map[level];
                }
                return 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static UInt32 CalculateRelativeWeight(UInt32 color1, UInt32 color2)
            {
                UInt32 weight = 0;
                for (Int32 i = 0; i < 3; i++)
                {
                    Byte channel1 = (Byte)((color1 >> i * 8) & 0xFF);
                    Byte channel2 = (Byte)((color2 >> i * 8) & 0xFF);
                    if (channel1 > channel2)
                    {
                        weight += (UInt32)channel1 - channel2;
                    }
                    else
                    {
                        weight += (UInt32)channel2 - channel1;
                    }
                }

                return weight;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Byte CalculateColorLevel(UInt32[] colorTable, UInt32 color)
            {
                Byte level = 0;
                UInt32 weight = UInt32.MaxValue;
                for (UInt32 i = 0; i < 4; i++)
                {
                    UInt32 currentWeight = CalculateRelativeWeight(colorTable[i], color);
                    if (weight > currentWeight)
                    {
                        weight = currentWeight;
                        level = (Byte)i;
                    }
                }

                return level;
            }

            public void CompressBitmap(UInt32[] bitmapData, UInt32 width, UInt32 height)
            {
                Alpha = Color = 0;
                UInt32 pixelAmount = width * height;
                Debug.Assert(pixelAmount == 16);

                UInt32 alphaMin = UInt32.MaxValue;
                UInt32 alphaMax = 0;
                UInt32 redMin = UInt32.MaxValue;
                UInt32 redMax = 0;
                UInt32 greenMin = UInt32.MaxValue;
                UInt32 greenMax = 0;
                UInt32 blueMin = UInt32.MaxValue;
                UInt32 blueMax = 0;
                for (UInt32 i = 0; i < pixelAmount; i++)
                {
                    UInt32 pixel = bitmapData[i];
                    if (alphaMin > (pixel & 0xFF000000)) alphaMin = (pixel & 0xFF000000);
                    if (alphaMax < (pixel & 0xFF000000)) alphaMax = (pixel & 0xFF000000);
                    if (blueMin > (pixel & 0x00FF0000)) blueMin = (pixel & 0x00FF0000);
                    if (blueMax < (pixel & 0x00FF0000)) blueMax = (pixel & 0x00FF0000);
                    if (greenMin > (pixel & 0x0000FF00)) greenMin = (pixel & 0x0000FF00);
                    if (greenMax < (pixel & 0x0000FF00)) greenMax = (pixel & 0x0000FF00);
                    if (redMin > (pixel & 0x000000FF)) redMin = (pixel & 0x000000FF);
                    if (redMax < (pixel & 0x000000FF)) redMax = (pixel & 0x000000FF);
                }
                alphaMin >>= 24;
                alphaMax >>= 24;
                blueMin >>= 16;
                blueMax >>= 16;
                greenMin >>= 8;
                greenMax >>= 8;
                UInt64 colorMax = blueMax >> 3 | ((greenMax >> 2) << 5) | ((redMax >> 3) << 11);
                UInt64 colorMin = blueMin >> 3 | ((greenMin << 2) << 5) | ((redMin >> 3) << 11);

                Color |= (colorMax | colorMin << 16);

                UInt32[] colorTable =
                {
                    redMax | greenMax << 8 | blueMax << 16,
                    redMin | greenMin << 8 | blueMin << 16,
                    (redMax - (redMax - redMin) / 3) | (greenMax - (greenMax - greenMin) / 3) << 8 | (blueMax - (blueMax - blueMin) / 3) << 16,
                    (redMin + (redMax - redMin) / 3) | (greenMin + (greenMax - greenMin) / 3) << 8 | (blueMin + (blueMax - blueMin) / 3) << 16,
                };

                Alpha |= alphaMax;
                Alpha |= (alphaMin << 8);

                for (UInt32 pixelIndex = 0; pixelIndex < 16; pixelIndex++)
                {
                    UInt16 alphaShift = (UInt16)(16 + (pixelIndex * 3));
                    UInt16 colorShift = (UInt16)(32 + (pixelIndex * 2));
                    UInt64 alphaLevel = CalculateAlphaLevel((Byte)alphaMin, (Byte)alphaMax, (Byte)(bitmapData[pixelAmount] >> 24));
                    UInt64 colorLevel = CalculateColorLevel(colorTable, bitmapData[pixelIndex]);
                    Alpha |= alphaLevel << alphaShift;
                    Color |= colorLevel << colorShift;
                }
            }
        }

        public static void DecompressDXT5(byte[] input, int width, int height, byte[] output)
        {
            int offset = 0;
            int bcw = (width + 3) / 4;
            int bch = (height + 3) / 4;
            int clen_last = (width + 3) % 4 + 1;
            uint[] buffer = new uint[16];
            int[] colors = new int[4];
            int[] alphas = new int[8];
            for (int t = 0; t < bch; t++)
            {
                for (int s = 0; s < bcw; s++, offset += 16)
                {
                    alphas[0] = input[offset + 0];
                    alphas[1] = input[offset + 1];
                    if (alphas[0] > alphas[1])
                    {
                        alphas[2] = (alphas[0] * 6 + alphas[1]) / 7;
                        alphas[3] = (alphas[0] * 5 + alphas[1] * 2) / 7;
                        alphas[4] = (alphas[0] * 4 + alphas[1] * 3) / 7;
                        alphas[5] = (alphas[0] * 3 + alphas[1] * 4) / 7;
                        alphas[6] = (alphas[0] * 2 + alphas[1] * 5) / 7;
                        alphas[7] = (alphas[0] + alphas[1] * 6) / 7;
                    }
                    else
                    {
                        alphas[2] = (alphas[0] * 4 + alphas[1]) / 5;
                        alphas[3] = (alphas[0] * 3 + alphas[1] * 2) / 5;
                        alphas[4] = (alphas[0] * 2 + alphas[1] * 3) / 5;
                        alphas[5] = (alphas[0] + alphas[1] * 4) / 5;
                        alphas[7] = 255;
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        alphas[i] <<= 24;
                    }

                    int q0 = input[offset + 8] | input[offset + 9] << 8;
                    int q1 = input[offset + 10] | input[offset + 11] << 8;
                    Rgb565(q0, out Int32 r0, out Int32 g0, out Int32 b0);
                    Rgb565(q1, out Int32 r1, out Int32 g1, out Int32 b1);
                    colors[0] = TColor(r0, g0, b0, 0);
                    colors[1] = TColor(r1, g1, b1, 0);
                    if (q0 > q1)
                    {
                        colors[2] = TColor((r0 * 2 + r1) / 3, (g0 * 2 + g1) / 3, (b0 * 2 + b1) / 3, 0);
                        colors[3] = TColor((r0 + r1 * 2) / 3, (g0 + g1 * 2) / 3, (b0 + b1 * 2) / 3, 0);
                    }
                    else
                    {
                        colors[2] = TColor((r0 + r1) / 2, (g0 + g1) / 2, (b0 + b1) / 2, 0);
                    }

                    ulong da = BitConverter.ToUInt64(input, offset) >> 16;
                    uint dc = BitConverter.ToUInt32(input, offset + 12);
                    for (int i = 0; i < 16; i++, da >>= 3, dc >>= 2)
                    {
                        buffer[i] = unchecked((uint)(alphas[da & 7] | colors[dc & 3]));
                    }

                    int clen = (s < bcw - 1 ? 4 : clen_last) * 4;
                    for (int i = 0, y = t * 4; i < 4 && y < height; i++, y++)
                    {
                        Buffer.BlockCopy(buffer, i * 4 * 4, output, (y * width + s * 4) * 4, clen);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Rgb565(int c, out int r, out int g, out int b)
        {
            r = (c & 0xf800) >> 8;
            g = (c & 0x07e0) >> 3;
            b = (c & 0x001f) << 3;
            r |= r >> 5;
            g |= g >> 6;
            b |= b >> 5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int TColor(int r, int g, int b, int a)
        {
            return r << 16 | g << 8 | b | a << 24;
        }
        #endregion
    }
}
