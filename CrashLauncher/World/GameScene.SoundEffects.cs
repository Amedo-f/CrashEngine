using System.Runtime.InteropServices;
using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.Libraries;
using PS2AnySound = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnySound;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private readonly Dictionary<(string Rm2, int SectionId, uint Sid), byte[]> _soundEffectBaselinePcm = new();
    private readonly Dictionary<(string Rm2, int SectionId, uint Sid), float>  _soundEffectGain = new();

    private static readonly (int Id, string Label)[] SoundSectionCandidates =
    {
        (TwinConstants.CODE_SOUND_EFFECTS_SECTION, "SFX"),
        (TwinConstants.CODE_LANG_ENG_SECTION, "ENG"),
        (TwinConstants.CODE_LANG_FRE_SECTION, "FRE"),
        (TwinConstants.CODE_LANG_GER_SECTION, "GER"),
        (TwinConstants.CODE_LANG_SPA_SECTION, "SPA"),
        (TwinConstants.CODE_LANG_ITA_SECTION, "ITA"),
        (TwinConstants.CODE_LANG_JPN_SECTION, "JPN"),
    };

    private static (PS2AnySound? Snd, int SectionId, string Label) FindSoundAnySection(BaseTwinSection? codeSec, uint sid)
    {
        foreach (var (id, label) in SoundSectionCandidates)
        {
            var item = codeSec?.GetItem<BaseTwinSection>((uint)id)?.GetItem<PS2AnySound>(sid);
            if (item is not null) return (item, id, label);
        }
        return (null, TwinConstants.CODE_SOUND_EFFECTS_SECTION, "SFX");
    }

    private void PlaySoundEffect(PS2AnySound snd)
    {
        try
        {
            var pcm = snd.ToPCM();
            short ch = 1;
            uint sr = snd.GetFreq();
            using var wavStream = new MemoryStream();
            using (var wavWriter = new BinaryWriter(wavStream))
                RIFF.SaveRiff(wavWriter, pcm, ref ch, ref sr);
            var wav = wavStream.ToArray();

            Task.Delay(50).ContinueWith(_ =>
            {
                bool ok = PlayPreviewWav(wav); // Amedo 2026-09-21
                if (!ok)
                {
                    int err = Marshal.GetLastWin32Error();
                    _musicLastError = $"winmm PlaySound returned false (Win32 error {err}).";
                    _browser.Log($"Sound Effect: {_musicLastError}");
                }
            });
        }
        catch (Exception ex)
        {
            _musicLastError = $"Sound Effect preview failed: {ex.GetType().Name}: {ex.Message}";
            _browser.Log($"Sound Effect: {_musicLastError}");
        }
    }

    private void ApplySoundEffectGain(PS2AnySound snd, uint sid, float gain, string rm2Scope,
        int sectionId = TwinConstants.CODE_SOUND_EFFECTS_SECTION)
    {
        try
        {
            var key = (rm2Scope, sectionId, sid);
            if (!_soundEffectBaselinePcm.TryGetValue(key, out var baseline))
            {
                baseline = LoadSoundGainBaseline(sid, rm2Scope, sectionId);
                if (baseline is null)
                {
                    var currentPcm = snd.ToPCM();
                    var pristinePcm = GetPristineSoundSection(rm2Scope, sectionId)?.GetItem<PS2AnySound>(sid)?.ToPCM();
                    baseline = (pristinePcm is not null && LooksLikeGainedPristine(currentPcm, pristinePcm))
                        ? pristinePcm
                        : currentPcm;
                }
                _soundEffectBaselinePcm[key] = baseline;
            }
            _soundEffectGain[key] = gain;

            var scaled = new byte[baseline.Length];
            for (int i = 0; i + 1 < baseline.Length; i += 2)
            {
                short s = BitConverter.ToInt16(baseline, i);
                int scaledSample = (int)Math.Round(s * gain);
                scaledSample = Math.Clamp(scaledSample, short.MinValue, short.MaxValue);
                BitConverter.GetBytes((short)scaledSample).CopyTo(scaled, i);
            }

            snd.SetDataFromPCM(scaled);
            _browser.Log($"Sound Effect 0x{sid:X}: gain set to {gain:F2}x. Save Chunk to persist.");
        }
        catch (Exception ex)
        {
            _musicLastError = $"Apply gain failed: {ex.GetType().Name}: {ex.Message}";
            _browser.Log($"Sound Effect: {_musicLastError}");
        }
    }

    private static bool LooksLikeGainedPristine(byte[] currentPcm, byte[] pristinePcm)
    {
        if (pristinePcm.Length == 0) return false;
        double diff = Math.Abs(currentPcm.Length - pristinePcm.Length) / (double)pristinePcm.Length;
        return diff < 0.05;
    }

    private void ResetSoundEffectToOriginal(PS2AnySound snd, uint sid, string rm2Scope,
        int sectionId = TwinConstants.CODE_SOUND_EFFECTS_SECTION)
    {
        try
        {
            var freshRm2 = ReadPristineRm2(rm2Scope);

            var freshSndSec = MeshDecoder.CodeSection(freshRm2, sectionId);
            var original = freshSndSec?.GetItem<PS2AnySound>(sid);
            if (original is null)
            {
                _musicLastError = $"Sound 0x{sid:X}: no original found on disc for this level — nothing to reset to.";
                _browser.Log($"Sound Effect: {_musicLastError}");
                return;
            }

            snd.Header = original.Header;
            snd.UnkFlag = original.UnkFlag;
            snd.FreqFac = original.FreqFac;
            snd.Param1 = original.Param1;
            snd.Param2 = original.Param2;
            snd.Param3 = original.Param3;
            snd.Param4 = original.Param4;
            snd.Sound = (byte[])original.Sound.Clone();

            var key = (rm2Scope, sectionId, sid);
            _soundEffectBaselinePcm.Remove(key);
            _soundEffectGain.Remove(key);
            try { File.Delete(SoundGainBaselinePath(sid, rm2Scope, sectionId)); } catch {  }
            _browser.Log($"Sound Effect 0x{sid:X}: restored to original (disc) audio. Save Chunk to persist.");
        }
        catch (Exception ex)
        {
            _musicLastError = $"Reset Sound Effect failed: {ex.GetType().Name}: {ex.Message}";
            _browser.Log($"Sound Effect: {_musicLastError}");
        }
    }

    private readonly Dictionary<(string Rm2, int SectionId), BaseTwinSection?> _pristineSoundSecCache = new();

    private string SoundGainBaselineDir =>
        Path.Combine(SavedChunksDir, "_SoundGainBaselines");

    private string SoundGainBaselinePath(uint sid, string rm2Scope,
        int sectionId = TwinConstants.CODE_SOUND_EFFECTS_SECTION)
    {
        string safeScope = rm2Scope.Replace('\\', '_').Replace('/', '_').Replace(':', '_');
        string suffix = sectionId == TwinConstants.CODE_SOUND_EFFECTS_SECTION ? "" : $"_sec{sectionId}";
        return Path.Combine(SoundGainBaselineDir, $"{safeScope}__{sid:X4}{suffix}.wav");
    }

    private void SaveSoundGainBaselines(Entity chunkRoot)
    {
        if (_soundEffectBaselinePcm.Count == 0) return;
        try
        {
            var chunkSrc = chunkRoot.Get<ChunkSource>();
            Directory.CreateDirectory(SoundGainBaselineDir);
            foreach (var ((rm2Scope, sectionId, sid), pcm) in _soundEffectBaselinePcm)
            {
                var scopeRm2 = rm2Scope.Equals(@"Startup\Default.rm2", StringComparison.OrdinalIgnoreCase)
                    ? chunkSrc?.GlobalRm2 : chunkSrc?.Rm2;
                var sndSec = MeshDecoder.CodeSection(scopeRm2, sectionId);
                short channels = 1;
                uint sampleRate = sndSec?.GetItem<PS2AnySound>(sid)?.GetFreq() ?? 32000u;
                using var fs = File.Create(SoundGainBaselinePath(sid, rm2Scope, sectionId));
                using var writer = new BinaryWriter(fs);
                RIFF.SaveRiff(writer, pcm, ref channels, ref sampleRate);
            }
        }
        catch (Exception ex)
        {
            _browser.Log($"Sound Effect: failed to save gain baselines: {ex.Message}");
        }
    }

    private byte[]? LoadSoundGainBaseline(uint sid, string rm2Scope,
        int sectionId = TwinConstants.CODE_SOUND_EFFECTS_SECTION)
    {
        try
        {
            var path = SoundGainBaselinePath(sid, rm2Scope, sectionId);
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs);
            byte[] pcm = Array.Empty<byte>();
            short channels = 0;
            uint sampleRate = 0;
            RIFF.LoadRiff(reader, ref pcm, ref channels, ref sampleRate);
            return pcm;
        }
        catch
        {
            return null;
        }
    }

    private BaseTwinSection? GetPristineSoundSection(string rm2Scope,
        int sectionId = TwinConstants.CODE_SOUND_EFFECTS_SECTION)
    {
        var cacheKey = (rm2Scope, sectionId);
        if (_pristineSoundSecCache.TryGetValue(cacheKey, out var cached)) return cached;

        BaseTwinSection? section = null;
        try
        {
            section = MeshDecoder.CodeSection(TryReadPristineRm2(rm2Scope), sectionId);
        }
        catch
        {
        }
        _pristineSoundSecCache[cacheKey] = section;
        return section;
    }

    private float GetPersistedSoundEffectGain(PS2AnySound snd, uint sid, string rm2Scope,
        int sectionId = TwinConstants.CODE_SOUND_EFFECTS_SECTION)
    {
        var key = (rm2Scope, sectionId, sid);
        if (_soundEffectGain.TryGetValue(key, out var cached)) return cached;

        float gain = 1f;
        try
        {
            var currentPcm = snd.ToPCM();
            var savedBaseline = LoadSoundGainBaseline(sid, rm2Scope, sectionId);
            byte[]? referencePcm = savedBaseline;
            if (referencePcm is null)
            {
                var pristinePcm = GetPristineSoundSection(rm2Scope, sectionId)?.GetItem<PS2AnySound>(sid)?.ToPCM();
                if (pristinePcm is not null && LooksLikeGainedPristine(currentPcm, pristinePcm))
                    referencePcm = pristinePcm;
            }
            if (referencePcm is not null)
            {
                double currentRms = RmsOf(currentPcm);
                double referenceRms = RmsOf(referencePcm);
                if (referenceRms > 1e-6)
                    gain = (float)Math.Clamp(currentRms / referenceRms, 0.1, 3.0);
            }
        }
        catch
        {
        }

        _soundEffectGain[key] = gain;
        return gain;
    }

    private static double RmsOf(byte[] pcm16)
    {
        if (pcm16.Length < 2) return 0.0;
        double sumSquares = 0;
        int count = 0;
        for (int i = 0; i + 1 < pcm16.Length; i += 2)
        {
            short s = BitConverter.ToInt16(pcm16, i);
            sumSquares += (double)s * s;
            count++;
        }
        return count > 0 ? Math.Sqrt(sumSquares / count) : 0.0;
    }

    private static byte[] StereoToMono(byte[] pcm16Stereo)
    {
        int frames = pcm16Stereo.Length / 4;
        var mono = new byte[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            short l = BitConverter.ToInt16(pcm16Stereo, i * 4);
            short r = BitConverter.ToInt16(pcm16Stereo, i * 4 + 2);
            short avg = (short)(((int)l + r) / 2);
            BitConverter.GetBytes(avg).CopyTo(mono, i * 2);
        }
        return mono;
    }

    private void ReplaceSoundEffectAudio(PS2AnySound snd, uint sid, string rm2Scope,
        int sectionId = TwinConstants.CODE_SOUND_EFFECTS_SECTION)
    {
        _musicLastError = null;
        ShowOpenFileDialog("Replace sound effect — pick a WAV or MP3 file",
            "Audio Files\0*.wav;*.mp3\0WAV Audio\0*.wav\0MP3 Audio\0*.mp3\0All Files\0*.*\0\0", path =>
        {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            byte[] pcm;
            short channels;
            uint sampleRate;

            if (string.Equals(Path.GetExtension(path), ".mp3", StringComparison.OrdinalIgnoreCase))
            {
                if (!DecodeMp3ToPcm(path, out pcm, out channels, out sampleRate, out var decodeError))
                {
                    _musicLastError = $"MP3 decode failed: {decodeError}";
                    _browser.Log($"Sound Effect: {_musicLastError}");
                    return;
                }
            }
            else
            {
                pcm = Array.Empty<byte>(); channels = 0; sampleRate = 0;
                using var wavStream = File.OpenRead(path);
                using var wavReader = new BinaryReader(wavStream);
                RIFF.LoadRiff(wavReader, ref pcm, ref channels, ref sampleRate);
            }

            if (channels == 2)
            {
                pcm = StereoToMono(pcm);
                channels = 1;
            }
            else if (channels != 1)
            {
                _musicLastError = $"Audio has {channels} channel(s) — only mono or stereo (auto-downmixed) is supported.";
                _browser.Log($"Sound Effect: {_musicLastError}");
                return;
            }

            uint targetFreq = snd.GetFreq();
            if (sampleRate != targetFreq)
                pcm = ResamplePcm16(pcm, 1, (int)sampleRate, (int)targetFreq);

            snd.SetDataFromPCM(pcm);
            _soundEffectBaselinePcm[(rm2Scope, sectionId, sid)] = pcm;
            _soundEffectGain[(rm2Scope, sectionId, sid)] = 1f;
            _browser.Log($"Sound Effect 0x{sid:X}: replaced with '{Path.GetFileNameWithoutExtension(path)}' " +
                          $"({targetFreq}Hz mono, {snd.Sound.Length} encoded bytes). Save Chunk to persist.");
        }
        catch (Exception ex)
        {
            _musicLastError = $"Replace Sound Effect failed: {ex.GetType().Name}: {ex.Message}";
            _browser.Log($"Sound Effect: {_musicLastError}\n{ex.StackTrace}");
        }
        });
    }
}
