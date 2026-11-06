using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;

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
        Path.Combine(SavedChunksDir, "MusicNames.json");

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

    // Amedo 2026-09-25
    private readonly object _previewSoundLock = new();
    private GCHandle _previewSoundPin;

    private bool PlayPreviewWav(byte[] wav)
    {
        var scaled = ApplyPreviewVolume(wav, _previewVolume);
        lock (_previewSoundLock) // Amedo 2026-09-25
        {
            PlaySound(null, IntPtr.Zero, 0);
            if (_previewSoundPin.IsAllocated) _previewSoundPin.Free();
            _previewSoundPin = GCHandle.Alloc(scaled, GCHandleType.Pinned);
            return PlaySound(scaled, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
        }
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
            if (chunkId == 0x61746164)
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
        lock (_previewSoundLock) // Amedo 2026-09-25
        {
            try { PlaySound(null, IntPtr.Zero, 0); } catch {  }
            if (_previewSoundPin.IsAllocated) _previewSoundPin.Free();
        }
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
        var backupDir = DiscBackup.DirFor(_scriptOut, _extractedRoot);
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
            var backupDir = DiscBackup.DirFor(_scriptOut, _extractedRoot);
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
                var backupDir = DiscBackup.DirFor(_scriptOut, _extractedRoot);
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
}
