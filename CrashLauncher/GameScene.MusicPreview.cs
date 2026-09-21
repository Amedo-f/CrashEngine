using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;
using PS2AnySound = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnySound;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private sealed class MusicTrack
    {
        public int    Type;
        public int    Size;
        public uint   Offset;
        public int    SampleRate;
    }

    private List<MusicTrack>? _musicBankTracks;

    private readonly Dictionary<uint, string> _customMusicNames = new();
    private bool _customMusicNamesLoaded;
    private string?           _musicBankMbPath;
    private int                _musicBankInterleave;

    private readonly Dictionary<(string Rm2, int SectionId, uint Sid), byte[]> _soundEffectBaselinePcm = new();
    private readonly Dictionary<(string Rm2, int SectionId, uint Sid), float>  _soundEffectGain = new();
    private byte[]?            _lastPreviewWav;

    private bool     _musicPreviewPlaying;
    private string   _musicPreviewLabel = "";
    private double   _musicPreviewDurationSeconds;
    private Stopwatch? _musicPreviewWatch;

    private string? _musicLastError;

    private void DrawMusicPreviewStatus()
    {
        if (!_musicPreviewPlaying || _musicPreviewWatch is null) return;

        double elapsed = _musicPreviewWatch.Elapsed.TotalSeconds;
        if (elapsed >= _musicPreviewDurationSeconds)
        {
            _musicPreviewPlaying = false;
            return;
        }

        float frac = _musicPreviewDurationSeconds > 0
            ? (float)Math.Clamp(elapsed / _musicPreviewDurationSeconds, 0.0, 1.0) : 0f;
        ImGui.Text($"Now Playing: {_musicPreviewLabel}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Stop##musicPreviewStatusStop"))
            StopMusicPreview();
        ImGui.ProgressBar(frac, new Vector2(-1f, 0f),
            $"{FormatMinSec(elapsed)} / {FormatMinSec(_musicPreviewDurationSeconds)}");
    }

    private string CustomMusicNamesPath =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "SavedChunks", "MusicNames.json");

    private void LoadCustomMusicNames()
    {
        if (_customMusicNamesLoaded) return;
        _customMusicNamesLoaded = true;
        try
        {
            var path = CustomMusicNamesPath;
            if (!File.Exists(path)) return;
            var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<uint, string>>(File.ReadAllText(path));
            if (loaded is null) return;
            foreach (var (id, name) in loaded) _customMusicNames[id] = name;
        }
        catch (Exception ex) { _browser.Log($"Music Preview: failed to load MusicNames.json: {ex.Message}"); }
    }

    private void SaveCustomMusicNames()
    {
        try
        {
            var path = CustomMusicNamesPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(_customMusicNames));
        }
        catch (Exception ex) { _browser.Log($"Music Preview: failed to save MusicNames.json: {ex.Message}"); }
    }

    private void EnsureMusicBankLoaded()
    {
        LoadCustomMusicNames();
        if (_musicBankTracks is not null) return;

        _musicBankTracks = new List<MusicTrack>();
        try
        {
            var mhPath = Path.Combine(_extractedRoot, "Crash6", "MUSIC.MH");
            _musicBankMbPath = Path.Combine(_extractedRoot, "Crash6", "MUSIC.MB");
            if (!File.Exists(mhPath) || !File.Exists(_musicBankMbPath))
            {
                _browser.Log($"Music Preview: MUSIC.MH/MB not found under {_extractedRoot}\\Crash6 — preview unavailable.");
                return;
            }

            using var mh = new BinaryReader(File.OpenRead(mhPath));
            int count = mh.ReadInt32();
            _musicBankInterleave = mh.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                _musicBankTracks.Add(new MusicTrack
                {
                    Type       = mh.ReadInt32(),
                    Size       = mh.ReadInt32(),
                    Offset     = mh.ReadUInt32(),
                    SampleRate = mh.ReadInt32(),
                });
                mh.ReadInt32();
            }
            _browser.Log($"Music Preview: loaded MUSIC.MH ({count} tracks, interleave {_musicBankInterleave}).");
        }
        catch (Exception ex)
        {
            _musicLastError = $"MUSIC.MH load failed: {ex.GetType().Name}: {ex.Message}";
            _browser.Log($"Music Preview: failed to load MUSIC.MH: {ex.Message}");
            _musicBankTracks = new List<MusicTrack>();
        }
    }

    private static double ComputeTrackDurationSeconds(MusicTrack t) =>
        t.Type == 0
            ? (double)((t.Size - 0x30) / 0x10 * 28) / t.SampleRate
            : (double)(t.Size / 0x20 * 28) / t.SampleRate;

    private (byte[] Wav, double DurationSeconds)? DecodeMusicTrackToWav(uint id)
    {
        _musicLastError = null;
        EnsureMusicBankLoaded();
        if (_musicBankTracks is null || _musicBankTracks.Count == 0 || _musicBankMbPath is null)
        { _musicLastError = "MUSIC.MH/MB not loaded — see log for why."; return null; }
        if (id >= _musicBankTracks.Count)
        { _musicLastError = $"id {id} is out of range (MUSIC.MH has {_musicBankTracks.Count} tracks)."; _browser.Log($"Music Preview: {_musicLastError}"); return null; }

        var t = _musicBankTracks[(int)id];
        if (t.Type == 2)
        { _musicLastError = $"id {id} is a real empty/null slot in MUSIC.MH — nothing to play."; _browser.Log($"Music Preview: {_musicLastError}"); return null; }
        if (t.Type != 0 && t.Type != 1)
        { _musicLastError = $"id {id} has an unrecognized track type ({t.Type})."; _browser.Log($"Music Preview: {_musicLastError}"); return null; }

        try
        {
            using var mb = new BinaryReader(File.OpenRead(_musicBankMbPath));
            mb.BaseStream.Position = t.Offset + (t.Type == 0 ? 0x30 : 0);
            var rawAdpcm = mb.ReadBytes(t.Type == 0 ? t.Size - 0x30 : t.Size);

            using var adpcmStream = new MemoryStream(rawAdpcm);
            using var adpcmReader = new BinaryReader(adpcmStream);
            using var pcmStream = new MemoryStream();
            using (var pcmWriter = new BinaryWriter(pcmStream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var adpcm = new ADPCM();
                try
                {
                    if (t.Type == 0) adpcm.ToPCMMono(adpcmReader, pcmWriter);
                    else              adpcm.ToPCMStereo(adpcmReader, pcmWriter, _musicBankInterleave);
                }
                catch (EndOfStreamException)
                {
                    _browser.Log($"Music Preview: id {id} decode ran out of data slightly before finding its own end marker — using what decoded so far (real audio, just possibly missing the very last instant).");
                }
            }

            using var wavStream = new MemoryStream();
            using (var wavWriter = new BinaryWriter(wavStream))
            {
                short channels = (short)(t.Type == 0 ? 1 : 2);
                uint sampleRate = (uint)t.SampleRate;
                RIFF.SaveRiff(wavWriter, pcmStream.ToArray(), ref channels, ref sampleRate);
            }
            return (wavStream.ToArray(), ComputeTrackDurationSeconds(t));
        }
        catch (Exception ex)
        {
            _musicLastError = $"decode failed for id {id}: {ex.GetType().Name}: {ex.Message}";
            _browser.Log($"Music Preview: {_musicLastError}\n{ex.StackTrace}");
            return null;
        }
    }

    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool PlaySound(byte[]? data, IntPtr hMod, uint flags);

    private const uint SND_MEMORY    = 0x0004;
    private const uint SND_ASYNC     = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;

    // Amedo 2026-09-21
    private float _previewVolume = 0.5f;

    private bool PlayPreviewWav(byte[] wav)
    {
        var scaled = ApplyPreviewVolume(wav, _previewVolume);
        return PlaySound(scaled, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
    }

    private static byte[] ApplyPreviewVolume(byte[] wav, float factor)
    {
        if (factor >= 0.999f || wav.Length < 12) return wav;
        factor = Math.Clamp(factor, 0f, 1f);
        var outp = (byte[])wav.Clone();
        int i = 12;
        while (i + 8 <= outp.Length)
        {
            int chunkId = BitConverter.ToInt32(outp, i);
            int chunkSize = BitConverter.ToInt32(outp, i + 4);
            if (chunkId == 0x61746164) // "data"
            {
                int start = i + 8;
                int end = Math.Min(start + chunkSize, outp.Length);
                for (int p = start; p + 1 < end; p += 2)
                {
                    short s = (short)(outp[p] | (outp[p + 1] << 8));
                    int v = Math.Clamp((int)MathF.Round(s * factor), short.MinValue, short.MaxValue);
                    outp[p] = (byte)(v & 0xFF);
                    outp[p + 1] = (byte)((v >> 8) & 0xFF);
                }
                break;
            }
            if (chunkSize < 0) break;
            i += 8 + chunkSize + (chunkSize & 1);
        }
        return outp;
    }

    private void PreviewMusicTrack(uint id, string label)
    {
        var decoded = DecodeMusicTrackToWav(id);
        if (decoded is not { } d) return;

        try
        {
            _lastPreviewWav = d.Wav;
            bool ok = PlayPreviewWav(d.Wav); // Amedo 2026-09-21
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                _musicLastError = $"winmm PlaySound returned false (Win32 error {err}).";
                _browser.Log($"Music Preview: {_musicLastError}");
                return;
            }

            _musicPreviewLabel = label;
            _musicPreviewDurationSeconds = d.DurationSeconds;
            _musicPreviewWatch = Stopwatch.StartNew();
            _musicPreviewPlaying = true;
            _browser.Log($"Music Preview: playing id {id} ({d.Wav.Length} byte WAV, {d.DurationSeconds:F1}s).");
        }
        catch (Exception ex)
        {
            _musicLastError = $"PlaySound call failed: {ex.GetType().Name}: {ex.Message}";
            _browser.Log($"Music Preview: {_musicLastError}");
        }
    }

    private void StopMusicPreview()
    {
        try { PlaySound(null, IntPtr.Zero, 0); } catch {  }
        _musicPreviewPlaying = false;
    }

    private void SaveMusicTrackAsWav(uint id, string label)
    {
        var decoded = DecodeMusicTrackToWav(id);
        if (decoded is not { } d) return;

        ShowSaveFileDialog("Save track as WAV", "WAV Audio\0*.wav\0All Files\0*.*\0\0",
            $"{label.Replace(' ', '_')}.wav", path =>
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;

                File.WriteAllBytes(path, d.Wav);
                _browser.Log($"Music Preview: saved id {id} → {path} ({d.Wav.Length} bytes).");
            }
            catch (Exception ex)
            {
                _musicLastError = $"Save As WAV failed: {ex.GetType().Name}: {ex.Message}";
                _browser.Log($"Music Preview: {_musicLastError}");
            }
        });
    }

    private bool _musicReplaceInProgress;

    private void EnsureMusicBankBackup(string mhPath, string mbPath)
    {
        var backupDir = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build",
                                       "original_backup_" + Path.GetFileName(_extractedRoot.TrimEnd('\\', '/')));
        var mhBackup = Path.Combine(backupDir, "MUSIC.MH");
        var mbBackup = Path.Combine(backupDir, "MUSIC.MB");
        if (!File.Exists(mhBackup) || !File.Exists(mbBackup))
        {
            Directory.CreateDirectory(backupDir);
            File.Copy(mhPath, mhBackup, overwrite: false);
            File.Copy(mbPath, mbBackup, overwrite: false);
            _browser.Log("Music Preview: backed up original MUSIC.MB/MH (first edit only, kept for every future reset/recovery).");
        }
    }

    private int GetOriginalMusicTrackCount()
    {
        try
        {
            var backupDir = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build",
                                           "original_backup_" + Path.GetFileName(_extractedRoot.TrimEnd('\\', '/')));
            var mhBackup = Path.Combine(backupDir, "MUSIC.MH");
            if (File.Exists(mhBackup))
            {
                using var r = new BinaryReader(File.OpenRead(mhBackup));
                return r.ReadInt32();
            }
        }
        catch (Exception ex) { _browser.Log($"Music Preview: couldn't read original MUSIC.MH backup: {ex.Message}"); }
        return _musicBankTracks?.Count ?? 0;
    }

    private void RestoreMusicTrackContentFromBackup(uint id, string label)
    {
        if (_musicReplaceInProgress) return;
        _musicLastError = null;
        _musicReplaceInProgress = true;
        _browser.Log($"Music Preview: restoring id {id} ({label})'s audio from the pristine backup — " +
                      "reading/writing the full ~200MB MUSIC.MB archive (several seconds, running in the background)...");

        Task.Run(() =>
        {
            try
            {
                var backupDir = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build",
                                              "original_backup_" + Path.GetFileName(_extractedRoot.TrimEnd('\\', '/')));
                var mhBackupPath = Path.Combine(backupDir, "MUSIC.MH");
                var mbBackupPath = Path.Combine(backupDir, "MUSIC.MB");
                if (!File.Exists(mhBackupPath) || !File.Exists(mbBackupPath))
                { _musicLastError = "No pristine MUSIC.MB/MH backup exists yet (none taken before this project's first music edit) — nothing to restore from."; _browser.Log($"Music Preview: {_musicLastError}"); return; }

                var mhPath = Path.Combine(_extractedRoot, "Crash6", "MUSIC.MH");
                var mbPath = Path.Combine(_extractedRoot, "Crash6", "MUSIC.MB");
                var mhTemp = mhPath + ".tmp";
                var mbTemp = mbPath + ".tmp";

                var backupMb = new PS2MB(mhBackupPath, mhBackupPath + ".unused");
                using (var backupReadStream = File.OpenRead(mbBackupPath))
                using (var backupReader = new BinaryReader(backupReadStream))
                    backupMb.Read(backupReader, (int)backupReadStream.Length);

                var src = backupMb.GetRecordData((int)id);
                if (src.Type == PS2MB.RecordType.NULL || src.Data.Length == 0)
                { _musicLastError = $"id {id} is a real empty/null slot in the backup — nothing to restore."; _browser.Log($"Music Preview: {_musicLastError}"); return; }

                var mb = new PS2MB(mhPath, mhTemp);
                using (var mbReadStream = File.OpenRead(mbPath))
                using (var mbReader = new BinaryReader(mbReadStream))
                    mb.Read(mbReader, (int)mbReadStream.Length);

                mb.ReplaceRecord((int)id, src.Type, (src.Name ?? "").TrimEnd('\0'), src.Data, src.SampleRate);

                using (var mbWriteStream = File.Create(mbTemp))
                using (var mbWriter = new BinaryWriter(mbWriteStream))
                    mb.Write(mbWriter);

                File.Move(mbTemp, mbPath, overwrite: true);
                File.Move(mhTemp, mhPath, overwrite: true);

                _musicBankTracks = null;

                _browser.Log($"Music Preview: restored id {id} ({label})'s audio from the pristine backup " +
                              "— lossless raw copy. Takes effect immediately — Preview it above to confirm.");
            }
            catch (Exception ex)
            {
                _musicLastError = $"Restore from backup failed: {ex.GetType().Name}: {ex.Message}";
                _browser.Log($"Music Preview: {_musicLastError}\n{ex.StackTrace}");
            }
            finally { _musicReplaceInProgress = false; }
        });
    }

    private static bool DecodeMp3ToPcm(string mp3Path, out byte[] pcm16, out short channels, out uint sampleRate, out string? error)
    {
        pcm16 = Array.Empty<byte>(); channels = 0; sampleRate = 0; error = null;
        try
        {
            using var mpegFile = new NLayer.MpegFile(mp3Path);
            channels = (short)mpegFile.Channels;
            sampleRate = (uint)mpegFile.SampleRate;

            var floatBuffer = new float[mpegFile.Channels * 4096];
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            int samplesRead;
            while ((samplesRead = mpegFile.ReadSamples(floatBuffer, 0, floatBuffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    float f = Math.Clamp(floatBuffer[i], -1f, 1f);
                    bw.Write((short)Math.Round(f * short.MaxValue));
                }
            }
            bw.Flush();
            pcm16 = ms.ToArray();
            if (pcm16.Length == 0) { error = "NLayer decoded zero samples — file may be empty, corrupt, or an unsupported MPEG variant."; return false; }
            return true;
        }
        catch (Exception ex)
        {
            error = $"MP3 decode failed: {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static byte[] ResamplePcm16(byte[] pcm16, int channels, int fromRate, int toRate)
    {
        if (fromRate == toRate) return pcm16;
        int frameCount = pcm16.Length / (2 * channels);
        int outFrameCount = (int)((long)frameCount * toRate / fromRate);
        var input = new short[frameCount * channels];
        Buffer.BlockCopy(pcm16, 0, input, 0, pcm16.Length);
        var output = new short[outFrameCount * channels];

        for (int outFrame = 0; outFrame < outFrameCount; outFrame++)
        {
            double srcPos = (double)outFrame * fromRate / toRate;
            int i0 = (int)srcPos;
            int i1 = Math.Min(i0 + 1, frameCount - 1);
            double frac = srcPos - i0;
            for (int c = 0; c < channels; c++)
            {
                short s0 = input[Math.Min(i0, frameCount - 1) * channels + c];
                short s1 = input[i1 * channels + c];
                output[outFrame * channels + c] = (short)Math.Round(s0 + (s1 - s0) * frac);
            }
        }
        var outBytes = new byte[output.Length * 2];
        Buffer.BlockCopy(output, 0, outBytes, 0, outBytes.Length);
        return outBytes;
    }

    private void ReplaceMusicTrackAudio(uint id, string label, Action<uint> applyNewId)
    {
        if (_musicReplaceInProgress) return;
        _musicLastError = null;

        EnsureMusicBankLoaded();

        ShowOpenFileDialog("Replace track audio — pick a WAV or MP3 file",
            "Audio Files\0*.wav;*.mp3\0WAV Audio\0*.wav\0MP3 Audio\0*.mp3\0All Files\0*.*\0\0", pickedPath =>
        {
        if (string.IsNullOrEmpty(pickedPath)) return;

        _musicReplaceInProgress = true;
        _browser.Log($"Music Preview: replacing id {id}'s audio — reading/writing the full ~200MB " +
                      "MUSIC.MB archive (several seconds, running in the background — the editor stays usable).");

        Task.Run(() =>
        {
            try
            {
                byte[] pcm;
                short channels;
                uint sampleRate;

                bool isMp3 = string.Equals(Path.GetExtension(pickedPath), ".mp3", StringComparison.OrdinalIgnoreCase);
                if (isMp3)
                {
                    _browser.Log($"Music Preview: decoding '{Path.GetFileName(pickedPath)}' via NLayer (fully managed, no external tools)...");
                    if (!DecodeMp3ToPcm(pickedPath, out pcm, out channels, out uint mp3Rate, out var decodeError))
                    {
                        _musicLastError = $"MP3 decode failed: {decodeError}";
                        _browser.Log($"Music Preview: {_musicLastError}");
                        return;
                    }
                    sampleRate = mp3Rate;
                    if (sampleRate != 32000)
                    {
                        _browser.Log($"Music Preview: resampling from {sampleRate}Hz to 32000Hz (real game convention)...");
                        pcm = ResamplePcm16(pcm, channels, (int)sampleRate, 32000);
                        sampleRate = 32000;
                    }
                }
                else
                {
                    pcm = Array.Empty<byte>();
                    channels = 0;
                    sampleRate = 0;
                    using var wavStream = File.OpenRead(pickedPath);
                    using var wavReader = new BinaryReader(wavStream);
                    RIFF.LoadRiff(wavReader, ref pcm, ref channels, ref sampleRate);
                }

                if (channels != 1 && channels != 2)
                { _musicLastError = $"Audio has {channels} channel(s) — only mono or stereo is supported."; _browser.Log($"Music Preview: {_musicLastError}"); return; }

                var recordType = channels == 1 ? PS2MB.RecordType.MONO : PS2MB.RecordType.STEREO;

                byte[] encoded;
                using (var pcmStream = new MemoryStream(pcm))
                using (var pcmReader = new BinaryReader(pcmStream))
                using (var encStream = new MemoryStream())
                using (var encWriter = new BinaryWriter(encStream))
                {
                    var adpcm = new ADPCM();
                    if (channels == 1) adpcm.ToADPCMMono(pcmReader, encWriter);
                    else               adpcm.ToADPCMStereo(pcmReader, encWriter, _musicBankInterleave);
                    encWriter.Flush();
                    encoded = encStream.ToArray();
                }

                var mhPath = Path.Combine(_extractedRoot, "Crash6", "MUSIC.MH");
                var mbPath = Path.Combine(_extractedRoot, "Crash6", "MUSIC.MB");
                var mhTemp = mhPath + ".tmp";
                var mbTemp = mbPath + ".tmp";

                EnsureMusicBankBackup(mhPath, mbPath);

                var mb = new PS2MB(mhPath, mhTemp);
                using (var mbReadStream = File.OpenRead(mbPath))
                using (var mbReader = new BinaryReader(mbReadStream))
                    mb.Read(mbReader, (int)mbReadStream.Length);

                string trackName = Path.GetFileNameWithoutExtension(pickedPath);
                int newId = mb.AddRecord(recordType, trackName, encoded, (int)sampleRate);

                using (var mbWriteStream = File.Create(mbTemp))
                using (var mbWriter = new BinaryWriter(mbWriteStream))
                    mb.Write(mbWriter);

                File.Move(mbTemp, mbPath, overwrite: true);
                File.Move(mhTemp, mhPath, overwrite: true);

                _musicBankTracks = null;

                _customMusicNames[(uint)newId] = trackName;
                SaveCustomMusicNames();

                applyNewId((uint)newId);

                _browser.Log($"Music Preview: imported '{trackName}' as a brand-new track id {newId} " +
                              $"({channels} ch, {sampleRate}Hz, {encoded.Length} encoded bytes) and repointed " +
                              $"this reference from id {id} ({label}) to it — the original id's audio was " +
                              "never touched. Save Chunk to persist the new id reference; Preview it above to confirm.");
            }
            catch (Exception ex)
            {
                _musicLastError = $"Replace Track Audio failed: {ex.GetType().Name}: {ex.Message}";
                _browser.Log($"Music Preview: {_musicLastError}\n{ex.StackTrace}");
            }
            finally { _musicReplaceInProgress = false; }
        });
        });
    }

    private static string FormatMinSec(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0;
        int total = (int)Math.Round(seconds);
        return $"{total / 60}:{total % 60:00}";
    }

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
            _lastPreviewWav = wav;

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

    private static Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2 LoadFreshPristineRm2(BinaryReader reader, int length, string rm2RelPath)
    {
        Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2 rm2 =
            rm2RelPath.Equals(@"Startup\Default.rm2", StringComparison.OrdinalIgnoreCase)
                ? new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default()
                : new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
        rm2.Read(reader, length);
        return rm2;
    }

    private void ResetSoundEffectToOriginal(PS2AnySound snd, uint sid, string rm2Scope,
        int sectionId = TwinConstants.CODE_SOUND_EFFECTS_SECTION)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(rm2Scope)
                ?? throw new FileNotFoundException($"Couldn't reopen {rm2Scope} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = LoadFreshPristineRm2(reader, (int)stream.Length, rm2Scope);

            var freshSndSec = freshRm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection>(
                    (uint)Twinsanity.TwinsanityInterchange.Enumerations.Constants.LEVEL_CODE_SECTION)
                ?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection>((uint)sectionId);
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
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "SavedChunks", "_SoundGainBaselines");

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
                var sndSec = scopeRm2
                    ?.GetItem<BaseTwinSection>((uint)Twinsanity.TwinsanityInterchange.Enumerations.Constants.LEVEL_CODE_SECTION)
                    ?.GetItem<BaseTwinSection>((uint)sectionId);
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
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(rm2Scope);
            if (stream is not null)
            {
                using var reader = new BinaryReader(stream);
                var freshRm2 = LoadFreshPristineRm2(reader, (int)stream.Length, rm2Scope);
                section = freshRm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection>(
                        (uint)Twinsanity.TwinsanityInterchange.Enumerations.Constants.LEVEL_CODE_SECTION)
                    ?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection>((uint)sectionId);
            }
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
