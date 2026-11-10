using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;

namespace CrashLauncher;

public sealed partial class GameScene
{
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
}
