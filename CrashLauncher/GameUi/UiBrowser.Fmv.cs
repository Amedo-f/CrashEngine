using ImGuiNET;
using System.Diagnostics;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private bool    _fmvConverting;
    private string? _fmvConvertTarget;
    private string? _fmvConvertLog;

    private void DrawUiFmvList()
    {
        ImGui.TextWrapped("Real .PSS video files straight from the disc (FMV\\ folder, outside the " +
                           "main archive). Playback isn't implemented here (see Boot Sequence tab) — " +
                           "but you can swap one out for a different file to test how the game reacts " +
                           "(e.g. a shorter clip to see if a boot video can be skipped). Always backed " +
                           "up automatically before the first swap.");
        ImGui.Separator();

        var fmvRoot = System.IO.Path.Combine(_extractedRoot, "FMV");
        if (!System.IO.Directory.Exists(fmvRoot)) { ImGui.TextDisabled("(no FMV\\ folder found on this disc)"); return; }

        List<string> files;
        try
        {
            files = System.IO.Directory.EnumerateFiles(fmvRoot, "*.pss", System.IO.SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) { ImGui.TextDisabled($"(couldn't list FMV\\: {ex.Message})"); return; }

        // Amedo 2026-09-20
        foreach (var full in files)
        {
            var rel = System.IO.Path.GetRelativePath(fmvRoot, full);
            var info = new System.IO.FileInfo(full);
            var backupPath = GetFmvBackupPath(rel);
            bool hasBackup = System.IO.File.Exists(backupPath);

            ImGui.PushID(rel);
            ImGui.Text(rel);
            ImGui.SameLine();
            ImGui.TextDisabled($"({info.Length / 1024.0 / 1024.0:F1} MB)");
            if (hasBackup)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), "[swapped]");
            }

            if (ImGui.Button("Play (external player)##fmvplay"))
                PlayFmvExternal(full);
            ImGui.SameLine();
            if (ImGui.Button("Import...##fmvimport"))
                ImportFmv(full, rel);
            ImGui.SameLine();
            ImGui.BeginDisabled(_fmvConverting);
            if (ImGui.Button("Convert & Import...##fmvconvert"))
                ConvertAndImportFmv(full, rel);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(!hasBackup);
            if (ImGui.Button("Restore Original##fmvrestore"))
                RestoreFmvOriginal(full, rel, backupPath);
            ImGui.EndDisabled();

            if (_fmvConverting && _fmvConvertTarget == full)
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "  Converting with FFmpeg — this can take a while for longer clips...");

            var proj = GetGameProject();
            if (proj is not null) // Amedo 2026-09-20
            {
                bool excluded = proj.ExcludedCutscenes.Contains(rel);
                if (ImGui.Checkbox("Exclude & skip in-game##fmvexclude", ref excluded))
                {
                    if (excluded) proj.ExcludedCutscenes.Add(rel);
                    else proj.ExcludedCutscenes.RemoveAll(p => string.Equals(p, rel, StringComparison.OrdinalIgnoreCase));
                    proj.Save();
                }
            }

            ImGui.PopID();
            ImGui.Separator();
        }

        if (!string.IsNullOrEmpty(_fmvConvertLog))
        {
            ImGui.Separator();
            ImGui.TextWrapped(_fmvConvertLog);
        }
    }

    private void PlayFmvExternal(string realPath)
    {
        try
        {
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                System.IO.Path.GetFileNameWithoutExtension(realPath) + ".mpg");
            System.IO.File.Copy(realPath, tempPath, overwrite: true);
            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            _browser.Log($"FMV: playing '{System.IO.Path.GetFileName(realPath)}' via default video player.");
        }
        catch (Exception ex)
        {
            _browser.Log($"FMV playback failed: {ex.Message} " +
                         "(no default .mpg player set? try VLC or Windows Media Player).");
        }
    }

    private string GetFmvBackupPath(string relativePath) =>
        System.IO.Path.Combine(_extractedRoot, "FMV_Backups", relativePath);

    private void ImportFmv(string realPath, string relativePath)
    {
        ShowOpenFileDialog("Import Video (replaces this file on disc)",
            "PSS Video\0*.pss;*.PSS\0All Files\0*.*\0\0", picked =>
        {
            if (picked is null) return;

            try
            {
                var backupPath = GetFmvBackupPath(relativePath);
                if (!System.IO.File.Exists(backupPath))
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backupPath)!);
                    System.IO.File.Copy(realPath, backupPath);
                    _browser.Log($"FMV: backed up original '{relativePath}' -> FMV_Backups\\{relativePath}");
                }
                System.IO.File.Copy(picked, realPath, overwrite: true);
                _browser.Log($"FMV: replaced '{relativePath}' with '{System.IO.Path.GetFileName(picked)}'. " +
                              "This is a REAL change to the disc content — Build ISO will include it. " +
                              "Use \"Restore Original\" to undo.");
            }
            catch (Exception ex) { _browser.Log($"FMV import failed: {ex.Message}"); }
        });
    }

    private void RestoreFmvOriginal(string realPath, string relativePath, string backupPath)
    {
        try
        {
            System.IO.File.Copy(backupPath, realPath, overwrite: true);
            _browser.Log($"FMV: restored original '{relativePath}' from backup.");
        }
        catch (Exception ex) { _browser.Log($"FMV restore failed: {ex.Message}"); }
    }

    private void ConvertAndImportFmv(string realPath, string relativePath)
    {
        ShowOpenFileDialog("Convert & Import Video (any format)",
            "Video Files\0*.mp4;*.avi;*.mov;*.mkv;*.webm;*.wmv;*.flv;*.mpg;*.mpeg\0All Files\0*.*\0\0", picked =>
        {
        if (picked is null) return;

        var ffmpeg = FindFfmpeg();
        if (ffmpeg is null)
        {
            _browser.Log("Convert & Import: FFmpeg not found. Install it (winget install Gyan.FFmpeg) and try again.");
            return;
        }

        _fmvConverting = true;
        _fmvConvertTarget = realPath;
        _fmvConvertLog = $"Converting '{System.IO.Path.GetFileName(picked)}' -> '{relativePath}'...";

        Task.Run(() =>
        {
            string? tempOut = null;
            try
            {
                var probeSource = System.IO.File.Exists(GetFmvBackupPath(relativePath))
                    ? GetFmvBackupPath(relativePath) : realPath;
                var spec = ProbeVideoSpec(ffmpeg, probeSource) ?? new FfmpegSpec(640, 480, 25, 9000, false);
                tempOut = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"fmvconvert_{Guid.NewGuid():N}.pss");

                var audioArgs = spec.HasAudio ? "-c:a mp2 -b:a 224k -ar 48000 -ac 2 " : "-an ";
                var args = $"-y -i \"{picked}\" " +
                           $"-c:v mpeg2video -b:v {spec.BitrateKbps}k -maxrate {spec.BitrateKbps}k " +
                           $"-minrate {spec.BitrateKbps}k -bufsize {spec.BitrateKbps * 200} " +
                           $"-s {spec.Width}x{spec.Height} -r {spec.Fps} -aspect 4:3 -pix_fmt yuv420p " +
                           $"-flags +ilme+ildct -top 0 " +
                           $"-color_primaries bt470bg -color_trc bt470m -colorspace bt470bg " +
                           audioArgs +
                           $"-f mpeg \"{tempOut}\"";

                var psi = new ProcessStartInfo(ffmpeg, args)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(psi)!;
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (proc.ExitCode != 0 || !System.IO.File.Exists(tempOut) || new System.IO.FileInfo(tempOut).Length == 0)
                {
                    var tail = stderr.Length > 800 ? stderr[^800..] : stderr;
                    _browser.Log($"Convert & Import: FFmpeg failed (exit {proc.ExitCode}).\n{tail}");
                    return;
                }

                var rawBytes = System.IO.File.ReadAllBytes(tempOut);
                var aligned = AlignMpegPsToSectors(rawBytes);
                System.IO.File.WriteAllBytes(tempOut, aligned);

                var backupPath = GetFmvBackupPath(relativePath);
                if (!System.IO.File.Exists(backupPath))
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backupPath)!);
                    System.IO.File.Copy(realPath, backupPath);
                    _browser.Log($"FMV: backed up original '{relativePath}' -> FMV_Backups\\{relativePath}");
                }
                System.IO.File.Copy(tempOut, realPath, overwrite: true);
                _browser.Log($"Convert & Import: '{System.IO.Path.GetFileName(picked)}' -> '{relativePath}' " +
                             $"({spec.Width}x{spec.Height}@{spec.Fps}fps, {spec.BitrateKbps}kb/s, " +
                             "sector-aligned to match the disc's real stream layout). " +
                             "Real change to the disc content — Build ISO will include it. \"Restore Original\" to undo.");
            }
            catch (Exception ex)
            {
                _browser.Log($"Convert & Import failed: {ex.Message}");
            }
            finally
            {
                if (tempOut is not null) { try { System.IO.File.Delete(tempOut); } catch { } }
                _fmvConverting = false;
                _fmvConvertTarget = null;
                _fmvConvertLog = null;
            }
        });
        });
    }

    private static byte[] AlignMpegPsToSectors(byte[] data)
    {
        const int Sector = 16384;
        var packMarker = new byte[] { 0x00, 0x00, 0x01, 0xBA };

        var offsets = new List<int>();
        int idx = 0;
        while (true)
        {
            idx = IndexOf(data, packMarker, idx);
            if (idx < 0) break;
            offsets.Add(idx);
            idx += 4;
        }
        offsets.Add(data.Length);

        using var outStream = new System.IO.MemoryStream(data.Length + data.Length / 8 + 4096);
        for (int i = 0; i < offsets.Count - 1; i++)
        {
            int packStart = offsets[i], packEnd = offsets[i + 1];
            outStream.Write(data, packStart, packEnd - packStart);

            long pos = outStream.Position;
            int remainder = (int)(pos % Sector);
            if (remainder == 0) continue;

            int gap = Sector - remainder;
            if (gap < 6) gap += Sector;
            int padLen = gap - 6;

            outStream.WriteByte(0x00); outStream.WriteByte(0x00);
            outStream.WriteByte(0x01); outStream.WriteByte(0xBE);
            outStream.WriteByte((byte)(padLen >> 8));
            outStream.WriteByte((byte)(padLen & 0xFF));
            for (int p = 0; p < padLen; p++) outStream.WriteByte(0xFF);
        }
        return outStream.ToArray();
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (int i = start; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    private readonly record struct FfmpegSpec(int Width, int Height, int Fps, int BitrateKbps, bool HasAudio);

    private static FfmpegSpec? ProbeVideoSpec(string ffmpegPath, string videoPath)
    {
        try
        {
            var ffprobePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ffmpegPath) ?? "", "ffprobe.exe");
            if (!System.IO.File.Exists(ffprobePath)) return null;

            var psi = new ProcessStartInfo(ffprobePath,
                $"-v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate,bit_rate " +
                $"-of csv=p=0 \"{videoPath}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            string outp = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            var parts = outp.Split(',');
            if (parts.Length < 3) return null;
            int w = int.Parse(parts[0]);
            int h = int.Parse(parts[1]);
            int fps = 25;
            var fpsParts = parts[2].Split('/');
            if (fpsParts.Length == 2 && double.TryParse(fpsParts[1], out var den) && den > 0 &&
                double.TryParse(fpsParts[0], out var num))
                fps = (int)Math.Round(num / den);
            int bitrateKbps = 9000;
            if (parts.Length > 3 && long.TryParse(parts[3], out var br) && br > 0)
                bitrateKbps = (int)(br / 1000);

            bool hasAudio = ProbeHasAudioStream(ffprobePath, videoPath);

            return new FfmpegSpec(w, h, fps, bitrateKbps, hasAudio);
        }
        catch { return null; }
    }

    private static bool ProbeHasAudioStream(string ffprobePath, string videoPath)
    {
        try
        {
            var psi = new ProcessStartInfo(ffprobePath,
                $"-v error -select_streams a -show_entries stream=index -of csv=p=0 \"{videoPath}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            string outp = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);
            return outp.Length > 0;
        }
        catch { return false; }
    }

    private static string? FindFfmpeg()
    {
        try
        {
            var psi = new ProcessStartInfo("ffmpeg", "-version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(3000);
            if (p is not null && p.ExitCode == 0) return "ffmpeg";
        }
        catch { }

        try
        {
            var pkgRoot = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WinGet", "Packages");
            if (System.IO.Directory.Exists(pkgRoot))
            {
                var found = System.IO.Directory.EnumerateFiles(pkgRoot, "ffmpeg.exe", System.IO.SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (found is not null) return found;
            }
        }
        catch { }

        return null;
    }
}
