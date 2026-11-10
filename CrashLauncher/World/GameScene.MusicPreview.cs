using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using CrashEngine.Platform.Windows;
using ImGuiNET;
using Twinsanity.Libraries;

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
            WinMmSound.Stop();
            if (_previewSoundPin.IsAllocated) _previewSoundPin.Free();
            _previewSoundPin = GCHandle.Alloc(scaled, GCHandleType.Pinned);
            return WinMmSound.PlayWav(scaled);
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
            try { WinMmSound.Stop(); } catch {  }
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

    private static string FormatMinSec(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0;
        int total = (int)Math.Round(seconds);
        return $"{total / 60}:{total % 60:00}";
    }
}
