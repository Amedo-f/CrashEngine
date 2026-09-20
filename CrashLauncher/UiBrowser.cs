using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using PS2PSM = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSM;
using PS2PSF = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSF;
using PS2PTC = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PTC;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private string _uiCategory = "Loading Screens";
    private string _uiLanguage = "English";
    private readonly Dictionary<string, List<Texture2D>> _uiTextureCache = new();
    private readonly Dictionary<string, List<string>>    _uiLangFileCache = new();

    private Texture2D? _uiZoomTex;
    private bool       _uiZoomFlip180;
    private bool       _uiZoomFlipV;

    private bool    _fmvConverting;
    private string? _fmvConvertTarget;
    private string? _fmvConvertLog;

    private void DrawUiBrowser()
    {
        DrawUiZoomOverlay();
        if (!_showUiBrowser) return;

        ImGui.SetNextWindowSize(new Vector2(720f, 520f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("UI Browser##uibrowser", ref _showUiBrowser))
        {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled("Real assets decoded straight from the disc — same pieces the game");
        ImGui.TextDisabled("itself shows. No on-screen layout data exists in these files (see");
        ImGui.TextDisabled("tooltip below), so this is the individual art, not a live mockup.");
        ImGui.Separator();

        ImGui.BeginChild("##uiCatList", new Vector2(170f, -1f), ImGuiChildFlags.Border);
        string[] categories = { "Boot Sequence", "Legal / Copyright", "Loading Screens", "Level Titles", "Icons", "Decal", "Fonts", "FMV / Cutscenes", "Effects" };
        foreach (var cat in categories)
            if (ImGui.Selectable(cat, _uiCategory == cat))
                _uiCategory = cat;
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##uiContent", Vector2.Zero, ImGuiChildFlags.Border);
        switch (_uiCategory)
        {
            case "Boot Sequence":
                DrawUiBootSequence();
                break;
            case "Legal / Copyright":
                DrawUiLegal();
                break;
            case "Loading Screens":
                DrawUiPsmGroup(new[]
                {
                    @"Language\Loading\Loading1.psm",
                    @"Language\Loading\Loading2.psm",
                    @"Language\Loading\Loading3.psm",
                }, kind: UiAssetKind.SplitFrames, flipV: true);
                break;
            case "Level Titles":
                DrawUiLevelTitles();
                break;
            case "Icons":
                DrawUiPsmGroup(new[] { @"Startup\Icons.psm" }, flipV: true);
                break;
            case "Decal":
                DrawUiPtc(@"Startup\Decal.ptc", flipV: true);
                break;
            case "Fonts":
                DrawUiPsfGroup(new[] { @"Startup\Fonts\Arial.psf", @"Startup\Fonts\Crash.psf" });
                break;
            case "FMV / Cutscenes":
                DrawUiFmvList();
                break;
            case "Effects":
                DrawEffectsBrowser();
                break;
        }
        ImGui.EndChild();

        ImGui.End();
    }

    private void DrawUiBootSequence()
    {
        ImGui.TextWrapped("Real boot order, reconstructed from what's actually on disc:");
        ImGui.Separator();

        DrawUiBootPatch();
        ImGui.Separator();

        ImGui.Text("1. Legal / Copyright screen");
        DrawUiPsmGroup(new[] { $@"Language\Legal\{_uiLanguage}.psm" }, kind: UiAssetKind.SplitFrames, flip180: true);

        ImGui.Spacing();
        ImGui.Text("2. Vivendi Universal splash (video)");
        DrawUiVideoPlaceholder(@"FMV\VIVENDI.PSS");

        ImGui.Spacing();
        ImGui.Text("3. Traveller's Tales logo (video)");
        DrawUiVideoPlaceholder(@"FMV\TTIDENT.PSS");

        ImGui.Spacing();
        ImGui.Text("4. Loading screen");
        DrawUiPsmGroup(new[] { @"Language\Loading\Loading1.psm" }, kind: UiAssetKind.SplitFrames);
    }

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

        var seedProj = GetGameProject();
        if (seedProj is { IsNewGame: true } && seedProj.ExcludedCutscenes.Count == 0)
        {
            foreach (var f in files)
                if (BuildIsoToPS2.IsStoryCutscene(System.IO.Path.GetFileName(f)))
                    seedProj.ExcludedCutscenes.Add(System.IO.Path.GetRelativePath(fmvRoot, f));
            if (seedProj.ExcludedCutscenes.Count > 0) seedProj.Save();
        }

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
            if (ImGui.IsItemHovered())
                MaybeTooltip("Copies this .PSS to Temp as a .mpg and opens it with your default video\n" +
                             "player — it's a real, standard MPEG-1/2 stream, no PS2-specific decoding\n" +
                             "needed. Use this to see exactly which real clip each file is.");
            ImGui.SameLine();
            if (ImGui.Button("Import...##fmvimport"))
                ImportFmv(full, rel);
            if (ImGui.IsItemHovered())
                MaybeTooltip("Copies a file straight in, byte-for-byte — only works if it's already\n" +
                             "a real MPEG-1/2 Program Stream (.pss/.mpg/.mpeg). Anything else (mp4,\n" +
                             "avi, ...) won't decode — use \"Convert & Import...\" for those instead.");
            ImGui.SameLine();
            ImGui.BeginDisabled(_fmvConverting);
            if (ImGui.Button("Convert & Import...##fmvconvert"))
                ConvertAndImportFmv(full, rel);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                MaybeTooltip("Pick ANY video (mp4, avi, mov, ...) — transcodes it with FFmpeg to match\n" +
                             "this file's real MPEG-2 spec (resolution/fps/bitrate probed from the\n" +
                             "current file), then imports the result. Needs FFmpeg installed.");
            ImGui.SameLine();
            ImGui.BeginDisabled(!hasBackup);
            if (ImGui.Button("Restore Original##fmvrestore"))
                RestoreFmvOriginal(full, rel, backupPath);
            ImGui.EndDisabled();

            if (_fmvConverting && _fmvConvertTarget == full)
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "  Converting with FFmpeg — this can take a while for longer clips...");

            var proj = GetGameProject();
            if (proj is { IsNewGame: true })
            {
                bool excluded = proj.ExcludedCutscenes.Contains(rel);
                if (ImGui.Checkbox("Exclude from New Game build##fmvexclude", ref excluded))
                {
                    if (excluded) proj.ExcludedCutscenes.Add(rel);
                    else proj.ExcludedCutscenes.RemoveAll(p => string.Equals(p, rel, StringComparison.OrdinalIgnoreCase));
                    proj.Save();
                }
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Only affects Build ISO in New Game mode — this exact file is moved\n" +
                                 "out for that build only, restored right after. Never touches the\n" +
                                 "boot-required intro videos unless you check them yourself here.");
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

    private readonly record struct PatchEdit(int FileOffset, byte[] NewBytes);
    private readonly record struct BootPatchDef(string ExeName, string Name, string Description, PatchEdit[] Edits, string AppliedLog);

    private static readonly BootPatchDef[] BootPatches =
    {
        new(
            "SLES_525.68",
            "Skip intro videos (Vivendi + TTIdent)",
            "Replaces the actual PlayMovie call in state 4 (Vivendi, vaddr 0x001774DC) and state " +
            "5 (TTIdent, vaddr 0x00177504) with a fake \"succeeded\" result (li v0,1) — everything " +
            "else those two states do (resource/texture setup, timers, etc.) still runs exactly " +
            "as original, only the real video-start call is skipped. Legal is untouched.",
            new[]
            {
                new PatchEdit(0x784DC, new byte[] { 0x01, 0x00, 0x02, 0x34 }),
                new PatchEdit(0x78504, new byte[] { 0x01, 0x00, 0x02, 0x34 }),
            },
            "both PlayMovie calls (Vivendi + TTIdent) now return a fake success instantly, " +
            "skipping the videos while every other side effect those states have still runs"),
        new(
            "SLES_525.68",
            "[KNOWN ISSUE] Also skip Legal / Copyright screen",
            "NOPs out state 1's single call to the \"show static screen\" helper with index 0 " +
            "(Legal) — vaddr 0x00173B98. CONFIRMED BY REAL TEST to have a wider side effect than " +
            "intended: this state machine appears to be reused for ordinary per-level loading " +
            "screens too, not just the one-time boot intro, so this removed a small image from " +
            "IN-GAME loading screens rather than (or in addition to) the boot-time Legal splash. " +
            "Not recommended to apply until a boot-only guard is found — kept here for reference/" +
            "reverting, see project_crashengine_bootpatch memory.",
            new[] { new PatchEdit(0x74B98, new byte[] { 0x00, 0x00, 0x00, 0x00 }) },
            "state 1's Legal-display call removed — Legal screen no longer shown"),

        new(
            "SLUS_209.09",
            "Skip intro videos (Vivendi + TTIdent)",
            "Replaces the actual movie-start call in state 4 (Vivendi, vaddr 0x001773CC) and " +
            "state 5 (TTIdent, vaddr 0x001773F4) with a fake \"succeeded\" result (li v0,1) — " +
            "everything else those two states do still runs exactly as original, only the real " +
            "video-start call is skipped. Legal is untouched. Found via Ghidra (movie filename " +
            "table + FUN_00172b60), not ported from SLES's own addresses (those don't line up).",
            new[]
            {
                new PatchEdit(0x783CC, new byte[] { 0x01, 0x00, 0x02, 0x34 }),
                new PatchEdit(0x783F4, new byte[] { 0x01, 0x00, 0x02, 0x34 }),
            },
            "both movie-start calls (Vivendi + TTIdent) now return a fake success instantly, " +
            "skipping the videos while every other side effect those states have still runs"),
    };

    private readonly record struct BootRegionDef(string ExeName, int StartingChunkOffset, int StartingChunkSize);
    private static readonly BootRegionDef[] BootRegions =
    {
        new("SLES_525.68", 0x1F6708, 56),
        new("SLUS_209.09", 0x1F63A8, 56),
    };

    private static string? ReadStartingChunk(string exePath, int offset, int size)
    {
        try
        {
            using var fs = System.IO.File.OpenRead(exePath);
            fs.Seek(offset, System.IO.SeekOrigin.Begin);
            var buf = new byte[size];
            if (fs.Read(buf, 0, buf.Length) != buf.Length) return null;
            int nul = Array.IndexOf(buf, (byte)0);
            int len = nul < 0 ? buf.Length : nul;
            return System.Text.Encoding.ASCII.GetString(buf, 0, len);
        }
        catch { return null; }
    }

    private void ApplyStartingChunk(string exePath, string backupPath, string newLevelPath, int offset, int size)
    {
        try
        {
            if (newLevelPath.Length > size - 1)
            {
                _browser.Log($"Starting Level: '{newLevelPath}' is {newLevelPath.Length} chars — " +
                              $"max {size - 1} fits safely in this field, skipped.");
                return;
            }
            if (!System.IO.File.Exists(backupPath))
            {
                System.IO.File.Copy(exePath, backupPath);
                _browser.Log($"Boot Patch: backed up original {System.IO.Path.GetFileName(exePath)} -> {System.IO.Path.GetFileName(backupPath)}");
            }
            using var fs = new System.IO.FileStream(exePath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite);
            fs.Seek(offset, System.IO.SeekOrigin.Begin);
            var buf = new byte[size];
            var bytes = System.Text.Encoding.ASCII.GetBytes(newLevelPath);
            Array.Copy(bytes, buf, bytes.Length);
            fs.Write(buf, 0, buf.Length);
            _browser.Log($"Starting Level: the game now boots straight into '{newLevelPath}'. Real " +
                          "change to the disc content — Build ISO will include it. \"Restore Original\" " +
                          "below undoes this along with any other boot patch.");
        }
        catch (Exception ex) { _browser.Log($"Starting Level patch failed: {ex.Message}"); }
    }

    private void DrawUiBootPatch()
    {
        BootRegionDef? region = null;
        string exePath = "";
        foreach (var r in BootRegions)
        {
            var candidate = System.IO.Path.Combine(_extractedRoot, r.ExeName);
            if (System.IO.File.Exists(candidate)) { region = r; exePath = candidate; break; }
        }
        if (region is null)
        {
            ImGui.TextDisabled("No known game executable (SLES_525.68 / SLUS_209.09) found at " +
                                "this disc path — patches unavailable.");
            return;
        }
        var reg = region.Value;

        var backupPath = System.IO.Path.Combine(_extractedRoot, reg.ExeName + ".orig_backup");
        bool isPal = reg.ExeName == "SLES_525.68";

        ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), "Boot-sequence patches");
        ImGui.TextDisabled($"Detected region: {reg.ExeName} ({(isPal ? "PAL/Europe" : "NTSC-U/America")})");
        ImGui.TextWrapped("Real, reverse-engineered patches to the game's own boot executable — " +
                           "not substitute files, this changes what the game itself does. Both " +
                           "share one backup of the pristine executable, taken automatically " +
                           "before the very first patch.");

        ImGui.Separator();
        ImGui.Text("Starting Level");
        var currentStart = ReadStartingChunk(exePath, reg.StartingChunkOffset, reg.StartingChunkSize);
        ImGui.TextDisabled($"Currently set: {currentStart ?? "(unreadable)"}");
        var currentLevelPath = _rm2.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? _rm2[..^4] : _rm2;
        bool fitsField = currentLevelPath.Length <= reg.StartingChunkSize - 1;
        bool alreadySet = string.Equals(currentStart, currentLevelPath, StringComparison.OrdinalIgnoreCase);
        ImGui.BeginDisabled(!fitsField || alreadySet);
        if (ImGui.Button($"Set currently-loaded level ('{currentLevelPath}') as Starting Level##setstart"))
            ApplyStartingChunk(exePath, backupPath, currentLevelPath, reg.StartingChunkOffset, reg.StartingChunkSize);
        ImGui.EndDisabled();
        if (!fitsField)
            ImGui.TextColored(new Vector4(1f, 0.5f, 0.3f, 1f),
                $"This path is {currentLevelPath.Length} chars — too long (max {reg.StartingChunkSize - 1}) to patch safely.");
        else if (alreadySet)
            ImGui.TextDisabled("(already the starting level)");

        var patchesForThisExe = BootPatches.Where(d => d.ExeName == reg.ExeName).ToList();
        if (patchesForThisExe.Count > 0)
        {
            foreach (var def in patchesForThisExe)
            {
                ImGui.PushID(def.Name);
                bool isPatched = IsBootPatchApplied(exePath, def);
                ImGui.Separator();
                ImGui.Text(def.Name);
                ImGui.TextWrapped(def.Description);
                if (isPatched) ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "Status: PATCHED");
                else ImGui.TextDisabled("Status: original (unpatched)");

                ImGui.BeginDisabled(isPatched);
                if (ImGui.Button("Apply Patch##apply"))
                    ApplyBootPatch(exePath, backupPath, def);
                ImGui.EndDisabled();
                ImGui.PopID();
            }
        }
        else
        {
            ImGui.Separator();
            ImGui.TextDisabled($"No boot patches known yet for {reg.ExeName}.");
        }

        ImGui.Separator();
        ImGui.BeginDisabled(!System.IO.File.Exists(backupPath));
        if (ImGui.Button("Restore Original (undoes ALL boot patches)##bootpatchrestore"))
            RestoreBootPatch(exePath, backupPath);
        ImGui.EndDisabled();
    }

    private static bool IsBootPatchApplied(string exePath, BootPatchDef def)
    {
        try
        {
            using var fs = System.IO.File.OpenRead(exePath);
            foreach (var edit in def.Edits)
            {
                fs.Seek(edit.FileOffset, System.IO.SeekOrigin.Begin);
                var buf = new byte[edit.NewBytes.Length];
                int read = fs.Read(buf, 0, buf.Length);
                if (read != buf.Length || !buf.AsSpan().SequenceEqual(edit.NewBytes)) return false;
            }
            return true;
        }
        catch { return false; }
    }

    private void ApplyBootPatch(string exePath, string backupPath, BootPatchDef def)
    {
        try
        {
            if (!System.IO.File.Exists(backupPath))
            {
                System.IO.File.Copy(exePath, backupPath);
                _browser.Log($"Boot Patch: backed up original {System.IO.Path.GetFileName(exePath)} -> {System.IO.Path.GetFileName(backupPath)}");
            }
            using (var fs = new System.IO.FileStream(exePath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite))
            {
                foreach (var edit in def.Edits)
                {
                    fs.Seek(edit.FileOffset, System.IO.SeekOrigin.Begin);
                    fs.Write(edit.NewBytes, 0, edit.NewBytes.Length);
                }
            }
            _browser.Log($"Boot Patch: applied '{def.Name}' — {def.AppliedLog}. Real change to " +
                         "the disc content — Build ISO will include it. \"Restore Original\" to undo.");
        }
        catch (Exception ex) { _browser.Log($"Boot Patch failed: {ex.Message}"); }
    }

    private void RestoreBootPatch(string exePath, string backupPath)
    {
        try
        {
            System.IO.File.Copy(backupPath, exePath, overwrite: true);
            _browser.Log($"Boot Patch: restored original {System.IO.Path.GetFileName(exePath)} from backup (all patches undone).");
        }
        catch (Exception ex) { _browser.Log($"Boot Patch restore failed: {ex.Message}"); }
    }

    private void DrawUiVideoPlaceholder(string relativePath)
    {
        var full = System.IO.Path.Combine(_extractedRoot, relativePath);
        if (System.IO.File.Exists(full))
        {
            var info = new System.IO.FileInfo(full);
            ImGui.TextDisabled($"  {relativePath}  ({info.Length / 1024.0 / 1024.0:F1} MB) — real .PSS video file on disc.");
            if (ImGui.Button($"Play (external player)##bootplay_{relativePath}"))
                PlayFmvExternal(full);
            if (ImGui.IsItemHovered())
                MaybeTooltip("Opens the real, current file on disc with your default video player\n" +
                             "(it's a standard MPEG-1/2 stream) — see exactly which clip this is.");
        }
        else
        {
            ImGui.TextDisabled($"  {relativePath} — not found at this disc path.");
        }
    }

    private void DrawUiLegal()
    {
        string[] langs = { "English", "French", "German", "Italian", "Spanish" };
        ImGui.SetNextItemWidth(160f);
        if (ImGui.BeginCombo("##uiLegalLang", _uiLanguage))
        {
            foreach (var l in langs)
                if (ImGui.Selectable(l, l == _uiLanguage))
                    _uiLanguage = l;
            ImGui.EndCombo();
        }
        ImGui.Separator();
        DrawUiPsmGroup(new[] { $@"Language\Legal\{_uiLanguage}.psm" }, kind: UiAssetKind.SplitFrames, flip180: true);
    }

    private void DrawUiLevelTitles()
    {
        string[] langs = { "English", "French", "German", "Italian", "Spanish" };
        ImGui.SetNextItemWidth(160f);
        if (ImGui.BeginCombo("##uiLang", _uiLanguage))
        {
            foreach (var l in langs)
                if (ImGui.Selectable(l, l == _uiLanguage))
                    _uiLanguage = l;
            ImGui.EndCombo();
        }
        ImGui.Separator();

        var files = GetUiLanguageFiles(_uiLanguage);
        if (files is null) { ImGui.TextDisabled("(couldn't list files)"); return; }
        if (files.Count == 0) { ImGui.TextDisabled("(no title cards found for this language)"); return; }

        foreach (var path in files)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (ImGui.CollapsingHeader(name + "##uititle"))
                DrawUiPsmGroup(new[] { path }, flip180: true);
        }
    }

    private List<string>? GetUiLanguageFiles(string lang)
    {
        if (_uiLangFileCache.TryGetValue(lang, out var cached)) return cached;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            var prefix = $@"Language\Titles\{lang}\";
            var list = pkg.Records
                .Select(r => r.Path)
                .Where(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _uiLangFileCache[lang] = list;
            return list;
        }
        catch (Exception ex) { _browser.Log($"UI Browser: {ex.Message}"); return null; }
    }

    private void DrawUiPsmGroup(IReadOnlyList<string> paths, UiAssetKind kind = UiAssetKind.Psm, bool flip180 = false, bool flipV = false)
    {
        foreach (var path in paths)
        {
            ImGui.Text(System.IO.Path.GetFileName(path));
            var textures = GetOrDecodeUiAsset(path, kind);
            if (textures is null) { ImGui.TextDisabled("  (failed to decode — see Output log)"); continue; }
            DrawUiTextureGrid(textures, flip180, flipV);
            ImGui.Separator();
        }
    }

    private void DrawUiPtc(string path, bool flip180 = false, bool flipV = false)
    {
        ImGui.Text(System.IO.Path.GetFileName(path));
        var textures = GetOrDecodeUiAsset(path, UiAssetKind.Ptc);
        if (textures is null) { ImGui.TextDisabled("  (failed to decode — see Output log)"); return; }
        DrawUiTextureGrid(textures, flip180, flipV);
    }

    private void DrawUiPsfGroup(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            ImGui.Text(System.IO.Path.GetFileName(path));
            var textures = GetOrDecodeUiAsset(path, UiAssetKind.Psf);
            if (textures is null) { ImGui.TextDisabled("  (failed to decode — see Output log)"); continue; }
            DrawUiTextureGrid(textures, flipV: true);
            ImGui.Separator();
        }
    }

    private enum UiAssetKind { Psm, SplitFrames, Ptc, Psf }

    private List<Texture2D>? GetOrDecodeUiAsset(string path, UiAssetKind kind)
    {
        var cacheKey = $"{kind}:{path}";
        if (_uiTextureCache.TryGetValue(cacheKey, out var cached)) return cached;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath(path);
            if (stream is null) { _browser.Log($"UI Browser: '{path}' not found in the disc archive."); return null; }
            using var reader = new BinaryReader(stream);

            List<Texture2D> textures = kind switch
            {
                UiAssetKind.Psf         => DecodePsfFile(reader, (int)stream.Length),
                UiAssetKind.Ptc         => DecodePtcFile(reader, (int)stream.Length),
                UiAssetKind.SplitFrames => DecodeSplitFramesFile(reader, (int)stream.Length),
                _                       => DecodePsmFile(reader, (int)stream.Length),
            };
            _uiTextureCache[cacheKey] = textures;
            return textures;
        }
        catch (Exception ex)
        {
            _browser.Log($"UI Browser: failed to decode '{path}': {ex.Message}");
            return null;
        }
    }

    private List<Texture2D> DecodePsmFile(BinaryReader reader, int length)
    {
        var psm = new PS2PSM();
        psm.Read(reader, length);
        return MeshDecoder.DecodePsm(Engine.Instance.GL, psm);
    }

    private List<Texture2D> DecodeSplitFramesFile(BinaryReader reader, int length)
    {
        var psm = new PS2PSM();
        psm.Read(reader, length);
        return MeshDecoder.DecodeSplitFrames(Engine.Instance.GL, psm);
    }

    private List<Texture2D> DecodePtcFile(BinaryReader reader, int length)
    {
        var ptc = new PS2PTC();
        ptc.Read(reader, length);
        var tex = MeshDecoder.DecodeUiTexture(Engine.Instance.GL, ptc);
        return tex is null ? new List<Texture2D>() : new List<Texture2D> { tex };
    }

    private List<Texture2D> DecodePsfFile(BinaryReader reader, int length)
    {
        var psf = new PS2PSF();
        psf.Read(reader, length);
        return MeshDecoder.DecodePsf(Engine.Instance.GL, psf);
    }

    private void DrawUiTextureGrid(List<Texture2D> textures, bool flip180 = false, bool flipV = false)
    {
        if (textures.Count == 0) { ImGui.TextDisabled("  (no decodable textures)"); return; }

        const float cell = 150f;
        float avail  = ImGui.GetContentRegionAvail().X;
        int   perRow = Math.Max(1, (int)(avail / (cell + 12f)));
        var   uv0    = flip180 ? new Vector2(1f, 1f) : flipV ? new Vector2(0f, 1f) : Vector2.Zero;
        var   uv1    = flip180 ? new Vector2(0f, 0f) : flipV ? new Vector2(1f, 0f) : Vector2.One;

        for (int i = 0; i < textures.Count; i++)
        {
            var tex     = textures[i];
            float aspect = tex.Height > 0 ? (float)tex.Width / tex.Height : 1f;
            float th     = MathF.Min(cell / MathF.Max(aspect, 0.01f), cell);
            float tw     = th * aspect;

            ImGui.BeginGroup();
            ImGui.Image((nint)tex.GlId, new Vector2(tw, th), uv0, uv1);
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                _uiZoomTex = tex;
                _uiZoomFlip180 = flip180;
                _uiZoomFlipV = flipV;
            }
            if (ImGui.IsItemHovered()) MaybeTooltip("Double-click to view full-size");
            ImGui.TextDisabled($"{tex.Width}x{tex.Height}");
            ImGui.EndGroup();

            if ((i + 1) % perRow != 0 && i != textures.Count - 1) ImGui.SameLine();
        }
    }

    private void DrawUiZoomOverlay()
    {
        if (_uiZoomTex is null) return;
        var tex = _uiZoomTex;

        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.Pos);
        ImGui.SetNextWindowSize(vp.Size);
        ImGui.SetNextWindowBgAlpha(0.92f);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings
                  | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoNav;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.Begin("##uiZoomOverlay", flags);

        ImGui.Text($"{tex.Width}x{tex.Height}  —  double-click, click the background, or press Esc to close");

        var avail  = ImGui.GetContentRegionAvail();
        float aspect = tex.Height > 0 ? (float)tex.Width / tex.Height : 1f;
        float dh = avail.Y;
        float dw = dh * aspect;
        if (dw > avail.X) { dw = avail.X; dh = dw / aspect; }

        var cursor = ImGui.GetCursorPos();
        ImGui.SetCursorPos(new Vector2(cursor.X + MathF.Max(0f, (avail.X - dw) * 0.5f), cursor.Y));
        var uv0 = _uiZoomFlip180 ? new Vector2(1f, 1f) : _uiZoomFlipV ? new Vector2(0f, 1f) : Vector2.Zero;
        var uv1 = _uiZoomFlip180 ? new Vector2(0f, 0f) : _uiZoomFlipV ? new Vector2(1f, 0f) : Vector2.One;
        ImGui.Image((nint)tex.GlId, new Vector2(dw, dh), uv0, uv1);
        bool clickedImage = ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);

        bool clickedBackground = ImGui.IsWindowHovered() && !ImGui.IsItemHovered() &&
                                  ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        if (clickedImage || clickedBackground || ImGui.IsKeyPressed(ImGuiKey.Escape))
            _uiZoomTex = null;

        ImGui.End();
        ImGui.PopStyleVar();
    }
}
