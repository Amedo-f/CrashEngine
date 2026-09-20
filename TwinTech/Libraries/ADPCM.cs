using System;
using System.Collections.Generic;
using System.IO;

namespace Twinsanity.Libraries
{
    [Flags]
    public enum SampleLineFlags : byte
    {
        None = 0,
        LoopEnd = 1,
        Unknown = 2,
        LoopStart = 4
    }
    public class ADPCM
    {
        // Based on code by bITmASTER and nextvolume
        // https://github.com/simias/psxsdk/blob/master/tools/vag2wav.c
        // https://github.com/simias/psxsdk/blob/master/tools/wav2vag.c

        private static readonly int BUFFER_SIZE = 128 * 28;
        private static readonly List<KeyValuePair<float, float>> F = new List<KeyValuePair<float, float>>
        {
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.9375f, 0.0f),
            new KeyValuePair<float, float>(1.796875f, -0.8125f),
            new KeyValuePair<float, float>(1.53125f, -0.859375f),
            new KeyValuePair<float, float>(1.90625f, -0.9375f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f),
            new KeyValuePair<float, float>(0.0f, 0.0f)
        };

        // Amedo 2026-08-22 -- integer fixed-point form of the F[] coefficients, for the libpsxav encoder.
        private static readonly int[] FilterK1 = { 0, 60, 115, 98, 122 };
        private static readonly int[] FilterK2 = { 0, 0, -52, -55, -60 };

        // Amedo 2026-08-22 -- clamp before the (short) cast: unclamped values wrapped on overflow
        // (harsh noise on overdriven audio); real PS2 hardware saturates instead.
        private short SampleToPCM(int sample, int factor, int predict, ref float s0, ref float s1)
        {
            sample <<= 12;
            sample = (short)sample;
            sample >>= factor;
            float value = sample;
            value += s0 * F[predict].Key;
            value += s1 * F[predict].Value;
            value = Math.Clamp(value, short.MinValue, short.MaxValue);
            s1 = s0;
            s0 = value;
            return (short)Math.Round(value);
        }
        private SampleLineFlags LineToPCM(BinaryReader reader, BinaryWriter writer, ref float s0, ref float s1)
        {
            byte[] o = new byte[28 * 2];
            Byte startByte = reader.ReadByte();
            SampleLineFlags flags = (SampleLineFlags)reader.ReadByte();
            int factor = startByte & 0xF;
            int predict = (startByte >> 4) & 0xF;
            if ((flags & SampleLineFlags.LoopEnd) == 0)
            {
                for (int i = 0; i < 14; i++)
                {
                    Byte src = reader.ReadByte();
                    int low = src & 0xF;
                    int high = (src & 0xF0) >> 4;
                    short l = SampleToPCM(low, factor, predict, ref s0, ref s1);
                    short h = SampleToPCM(high, factor, predict, ref s0, ref s1);
                    writer.Write(l);
                    writer.Write(h);
                }
            }
            return flags;
        }
        // Amedo 2026-08-22 -- encoder replaced with a port of libpsxav (psxavenc): continuous
        // predictor state across the stream, exhaustive filter/shift search by round-trip error,
        // no dithering. Filter coefficients (F[] above) unchanged.
        private const int ShiftRange4Bps = 12;
        private const int SpuAdpcmFilterCount = 5;

        // Mirrors libpsxav's psx_audio_encoder_channel_state_t — prev1/prev2 are the last two
        // RECONSTRUCTED (decoded) samples, threaded continuously for one whole channel's stream.
        private sealed class SpuEncodeState
        {
            public int Prev1, Prev2;
            public long Mse;
            public void CopyFrom(SpuEncodeState other) { Prev1 = other.Prev1; Prev2 = other.Prev2; }
        }

        // Finds the tightest shift that avoids the block's samples clipping, by directly
        // simulating the residual (not a proxy/heuristic) — mirrors libpsxav's find_min_shift.
        private static int FindMinShift(SpuEncodeState state, short[] samples, int offset, int sampleLimit, int pitch, int filter, int shiftRange)
        {
            int prev1 = state.Prev1, prev2 = state.Prev2;
            int k1 = FilterK1[filter], k2 = FilterK2[filter];
            int rightShift = 0;
            int sMin = 0, sMax = 0;
            for (int i = 0; i < 28; i++)
            {
                int rawSample = (i >= sampleLimit) ? 0 : samples[offset + i * pitch];
                int previousValues = (k1 * prev1 + k2 * prev2 + (1 << 5)) >> 6;
                int sample = rawSample - previousValues;
                if (sample < sMin) sMin = sample;
                if (sample > sMax) sMax = sample;
                prev2 = prev1;
                prev1 = rawSample;
            }
            while (rightShift < shiftRange && (sMax >> rightShift) > (0x7FFF >> shiftRange)) rightShift++;
            while (rightShift < shiftRange && (sMin >> rightShift) < (-0x8000 >> shiftRange)) rightShift++;
            return shiftRange - rightShift;
        }

        // Actually encodes one block with a specific (filter, shift) candidate, writing one nibble
        // VALUE per output byte (packed to real nibbles by the caller) and updating outState —
        // mirrors libpsxav's attempt_to_encode. Real per-sample simulated decode + squared error
        // (outState.Mse) is what the caller uses to judge this candidate against others.
        private static byte AttemptToEncode(SpuEncodeState outState, SpuEncodeState inState, short[] samples, int offset, int sampleLimit, int pitch, byte[] data, int filter, int sampleShift, int shiftRange)
        {
            int sampleMask = 0xFFFF >> shiftRange;
            int k1 = FilterK1[filter], k2 = FilterK2[filter];
            byte hdr = (byte)((sampleShift & 0x0F) | (filter << 4));

            if (!ReferenceEquals(outState, inState)) outState.CopyFrom(inState);
            outState.Mse = 0;

            for (int i = 0; i < 28; i++)
            {
                int sample = (i >= sampleLimit) ? 0 : samples[offset + i * pitch];
                int previousValues = (k1 * outState.Prev1 + k2 * outState.Prev2 + (1 << 5)) >> 6;
                int sampleEnc = sample - previousValues;
                sampleEnc <<= sampleShift;
                sampleEnc += (1 << (shiftRange - 1));
                sampleEnc >>= shiftRange;
                int lo = -0x8000 >> shiftRange, hi = 0x7FFF >> shiftRange;
                if (sampleEnc < lo) sampleEnc = lo;
                if (sampleEnc > hi) sampleEnc = hi;
                sampleEnc &= sampleMask;

                int sampleDec = (short)((sampleEnc & sampleMask) << shiftRange);
                sampleDec >>= sampleShift;
                sampleDec += previousValues;
                if (sampleDec > short.MaxValue) sampleDec = short.MaxValue;
                if (sampleDec < short.MinValue) sampleDec = short.MinValue;
                long sampleError = sampleDec - sample;

                data[i] = (byte)sampleEnc;
                outState.Mse += sampleError * sampleError;

                outState.Prev2 = outState.Prev1;
                outState.Prev1 = sampleDec;
            }
            return hdr;
        }

        // Tries every filter and, per libpsxav's own comment ("the optimal shift can be off the
        // true minimum shift by 1 in *either* direction" when not using dither), the 3 shifts
        // around FindMinShift's result — picks whichever real (filter, shift) combo minimizes
        // actual simulated squared error, then commits it for real (updating `state`, the
        // persistent per-channel state) — mirrors libpsxav's encode().
        private static byte EncodeBlock(SpuEncodeState state, short[] samples, int offset, int sampleLimit, int pitch, byte[] data, int shiftRange)
        {
            var proposed = new SpuEncodeState();
            var scratch = new byte[28];
            long bestMse = long.MaxValue;
            int bestFilter = 0, bestShift = 0;

            for (int filter = 0; filter < SpuAdpcmFilterCount; filter++)
            {
                int trueMinShift = FindMinShift(state, samples, offset, sampleLimit, pitch, filter, shiftRange);
                int minShift = Math.Max(0, trueMinShift - 1);
                int maxShift = Math.Min(shiftRange, trueMinShift + 1);
                for (int sampleShift = minShift; sampleShift <= maxShift; sampleShift++)
                {
                    AttemptToEncode(proposed, state, samples, offset, sampleLimit, pitch, scratch, filter, sampleShift, shiftRange);
                    if (bestMse > proposed.Mse)
                    {
                        bestMse = proposed.Mse;
                        bestFilter = filter;
                        bestShift = sampleShift;
                    }
                }
            }
            return AttemptToEncode(state, state, samples, offset, sampleLimit, pitch, data, bestFilter, bestShift, shiftRange);
        }

        // Packs 28 raw nibble-value bytes (from EncodeBlock's `data`, one value 0-15 per byte)
        // into the real on-disk 14-byte layout, same bit order LineToPCM's own decode expects.
        private static void PackNibbles(byte[] nibbleValues, BinaryWriter writer)
        {
            for (int k = 0; k < 28; k += 2)
                writer.Write((byte)((nibbleValues[k + 1] << 4) | (nibbleValues[k] & 0xF)));
        }

        // Amedo 2026-08-22 -- keep this terminator block (known-working state; a removal experiment
        // didn't fix a loop glitch and was reverted).
        public void ToADPCMMono(BinaryReader reader, BinaryWriter writer)
        {
            var pcm = new List<short>();
            while (reader.BaseStream.Position + 2 <= reader.BaseStream.Length)
                pcm.Add(reader.ReadInt16());
            var samples = pcm.ToArray();

            var state = new SpuEncodeState();
            var block = new byte[28];
            int lastHdr = 0;
            for (int i = 0; i < samples.Length; i += 28)
            {
                int sampleLimit = Math.Min(28, samples.Length - i);
                lastHdr = EncodeBlock(state, samples, i, sampleLimit, 1, block, ShiftRange4Bps);
                writer.Write((byte)lastHdr);
                writer.Write((byte)0);
                PackNibbles(block, writer);
            }
            writer.Write((byte)lastHdr);
            writer.Write((byte)7);
            for (int i = 0; i < 14; i++) writer.Write((byte)0);
        }

        // Amedo 2026-08-12 -- encodes a stereo WAV back into MUSIC.MB format (de-interleave -> encode
        // each channel via EncodeBlock -> interleave L/R chunks) for "Replace Track Audio".
        public void ToADPCMStereo(BinaryReader reader, BinaryWriter writer, int interleave)
        {
            // Amedo 2026-08-22 -- reject interleave <= 0 or non-multiple-of-16 (0 slipped through and
            // hung the write loop, since `pos += interleave` never advanced).
            if (interleave <= 0 || (interleave % 16) != 0)
                throw new ArgumentException($"Stereo interleave must be a positive multiple of 16 (got {interleave}).");

            var lSamples = new List<short>();
            var rSamples = new List<short>();
            while (reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
            {
                lSamples.Add(reader.ReadInt16());
                rSamples.Add(reader.ReadInt16());
            }

            byte[] EncodeChannel(List<short> samples)
            {
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);
                var state = new SpuEncodeState();
                var block = new byte[28];
                var arr = samples.ToArray();
                int lastHdr = 0;
                for (int i = 0; i < arr.Length; i += 28)
                {
                    int sampleLimit = Math.Min(28, arr.Length - i);
                    lastHdr = EncodeBlock(state, arr, i, sampleLimit, 1, block, ShiftRange4Bps);
                    bw.Write((byte)lastHdr);
                    bw.Write((byte)0);
                    PackNibbles(block, bw);
                }
                // Amedo 2026-08-22 -- flags=7 terminator block (kept; removing it didn't fix a loop glitch).
                bw.Write((byte)lastHdr);
                bw.Write((byte)7);
                for (int k = 0; k < 14; k++) bw.Write((byte)0);
                bw.Flush();
                return ms.ToArray();
            }

            var lEncoded = EncodeChannel(lSamples);
            var rEncoded = EncodeChannel(rSamples);

            // Amedo 2026-08-23 -- pad each channel up to the `interleave` boundary (real MUSIC.MB
            // tracks are interleave-aligned); the zero padding sits after the terminator and is
            // never decoded.
            int Align(int len) => ((len + interleave - 1) / interleave) * interleave;
            int lAligned = Align(lEncoded.Length);
            if (lAligned != lEncoded.Length) Array.Resize(ref lEncoded, lAligned);
            int rAligned = Align(rEncoded.Length);
            if (rAligned != rEncoded.Length) Array.Resize(ref rEncoded, rAligned);

            int pos = 0;
            int maxLen = Math.Max(lEncoded.Length, rEncoded.Length);
            while (pos < maxLen)
            {
                int lChunk = Math.Min(interleave, lEncoded.Length - pos);
                if (lChunk > 0) writer.Write(lEncoded, pos, lChunk);
                int rChunk = Math.Min(interleave, rEncoded.Length - pos);
                if (rChunk > 0) writer.Write(rEncoded, pos, rChunk);
                pos += interleave;
            }
        }

        public void ToPCMMono(BinaryReader reader, BinaryWriter writer)
        {
            float s0 = 0.0f;
            float s1 = 0.0f;
            SampleLineFlags flag = 0;
            while ((flag & SampleLineFlags.LoopEnd) == 0)
            {
                flag = LineToPCM(reader, writer, ref s0, ref s1);
            }
        }

        public void ToPCMStereo(BinaryReader reader, BinaryWriter writer, int interleave)
        {
            // Amedo 2026-08-12 -- output must be sample-interleaved [L,R,L,R]; the old [L..,R..]
            // layout played stereo at ~double speed. Decode each channel then interleave per sample.
            if ((interleave % 16) != 0)
                throw new ArgumentException("Stereo interleave is not a multiple of 16.");
            int blocks = interleave /= 16;
            float s0_l = 0;
            float s1_l = 0;
            float s0_r = 0;
            float s1_r = 0;
            SampleLineFlags flag_l = 0;
            SampleLineFlags flag_r = 0;
            using var lStream = new MemoryStream();
            using var rStream = new MemoryStream();
            using var lWriter = new BinaryWriter(lStream);
            using var rWriter = new BinaryWriter(rStream);
            try
            {
                while ((flag_l & SampleLineFlags.LoopEnd) == 0 && (flag_r & SampleLineFlags.LoopEnd) == 0)
                {
                    for (var i = 0; i < blocks; ++i)
                    {
                        flag_l = LineToPCM(reader, lWriter, ref s0_l, ref s1_l);
                        if ((flag_l & SampleLineFlags.LoopEnd) != 0)
                        {
                            break;
                        }
                    }
                    for (var i = 0; i < blocks; ++i)
                    {
                        flag_r = LineToPCM(reader, rWriter, ref s0_r, ref s1_r);
                        if ((flag_r & SampleLineFlags.LoopEnd) != 0)
                        {
                            break;
                        }
                    }
                }
            }
            catch (EndOfStreamException)
            {
                // Amedo 2026-08-12 -- if the declared size runs out before a LoopEnd, still write
                // the audio already decoded (letting the exception escape made previews silent).
            }

            lWriter.Flush();
            rWriter.Flush();
            var lBytes = lStream.ToArray();
            var rBytes = rStream.ToArray();
            int sampleCount = Math.Min(lBytes.Length, rBytes.Length) / 2; // 2 bytes/sample (16-bit PCM)
            for (int k = 0; k < sampleCount; k++)
            {
                writer.Write(BitConverter.ToInt16(lBytes, k * 2));
                writer.Write(BitConverter.ToInt16(rBytes, k * 2));
            }
        }
    }
}

