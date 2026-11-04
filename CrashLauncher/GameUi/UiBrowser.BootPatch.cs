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
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private readonly record struct PatchEdit(int FileOffset, byte[] NewBytes);
    private readonly record struct BootPatchDef(string ExeName, string Name, string Description, PatchEdit[] Edits, string AppliedLog)
    {
        public string[]? Supersedes { get; init; } // Amedo 2026-09-24
    }

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

        // Amedo 2026-09-24
        new(
            "SLUS_209.09",
            "Boot straight into the game (skip title screen + main menu)",
            "After the TTIdent state (boot state 5) the game checks whether a level name is already " +
            "set: empty -> load the hub for the title screen (state 6 -> 7 -> menu 8); set -> go " +
            "straight to Loading (state 9) with New Game data, exactly the path the menu's New Game " +
            "uses. This patch hooks the TTIdent movie call (vaddr 0x001773F4) into a 7-instruction " +
            "code cave (vaddr 0x002D680C, inter-function nop padding) that assigns the Starting Level " +
            "string (the same table the Starting Level patch edits) via the game's own string-assign " +
            "and returns 1, so boot goes Legal -> memory card check -> Loading -> gameplay. Confirmed " +
            "live in PCSX2 (state 5 -> 9 -> 11 -> 12). Includes the intro-video skip (both videos).",
            new[]
            {
                new PatchEdit(0x783CC, new byte[] { 0x01, 0x00, 0x02, 0x34 }),
                new PatchEdit(0x783F4, new byte[] { 0x03, 0x5A, 0x0B, 0x0C }),
                new PatchEdit(0x1D780C, new byte[]
                {
                    0x2D, 0x90, 0xE0, 0x03,
                    0x00, 0x05, 0x24, 0x26,
                    0x31, 0x00, 0x05, 0x3C,
                    0xE2, 0x0F, 0x08, 0x0C,
                    0x00, 0xBB, 0xA5, 0x8C,
                    0x08, 0x00, 0x40, 0x02,
                    0x01, 0x00, 0x02, 0x24,
                }),
            },
            "boot now goes Legal -> memory card check -> Loading -> gameplay in the Starting Level " +
            "(title screen and main menu skipped; intro videos skipped too)")
        { Supersedes = new[] { "Skip intro videos (Vivendi + TTIdent)" } },

        // Amedo 2026-09-24
        new(
            "SLUS_209.09",
            "Hide + don't wait on the memory-card check screen (check still runs in the background)",
            "Boot state 3 starts the memory-card check and then waits until the save manager reports " +
            "done (2 s minimum hold + fades = ~4.1 s measured). This NOPs only that wait (vaddr " +
            "0x00173CCC, bne -> nop) so the boot moves on after 2 frames; the check itself still " +
            "starts and finishes normally in the background (final manager status identical to the " +
            "unpatched boot, confirmed live). Changing the 2 s hold value instead stalls the check " +
            "(tested: 0, 1 and 30 all hang until an internal timeout), so this is the safe variant. " +
            "Also removes the 'Checking memory card' panel itself (the save manager's show-panel call " +
            "in FUN_0029F5C0, vaddr 0x0029F67C) — otherwise it stays on top of the loading screen once " +
            "the intro videos are skipped too. Confirmed live: no panel, check still completes. Side " +
            "effect: the same call shows the manager's 'busy' panel for in-game save/load too, so that " +
            "panel won't appear either (saving itself is unaffected). Note: with NO memory card inserted " +
            "the 'No memory card / CONTINUE / RETRY' prompt shows up over loading/gameplay instead.",
            new[]
            {
                new PatchEdit(0x74CCC, new byte[] { 0x00, 0x00, 0x00, 0x00 }),
                new PatchEdit(0x1A067C, new byte[] { 0x00, 0x00, 0x00, 0x00 }),
            },
            "boot no longer waits for or shows the memory-card check screen (check runs in the background)"),

        // Amedo
        new(
            "SLES_525.68",
            "Boot straight into the game (skip title screen + main menu)",
            "PAL version of the NTSC patch: the TTIdent movie call (vaddr 0x00177504) is hooked into the " +
            "same 7-instruction code cave (vaddr 0x002D6B8C, the PAL copy of the NTSC nop padding) that assigns " +
            "the Starting Level string (pointer 0x0030BE80) to the game controller's level name (+0x510, the PAL " +
            "layout of NTSC +0x500) with the game's own string-assign (0x00204718) and returns 1, so boot goes " +
            "Legal -> memory card check -> Loading -> gameplay. Includes the intro-video skip (both videos).",
            new[]
            {
                new PatchEdit(0x784DC, new byte[] { 0x01, 0x00, 0x02, 0x34 }),
                new PatchEdit(0x78504, new byte[] { 0xE3, 0x5A, 0x0B, 0x0C }),
                new PatchEdit(0x1D7B8C, new byte[]
                {
                    0x2D, 0x90, 0xE0, 0x03,
                    0x10, 0x05, 0x24, 0x26,
                    0x31, 0x00, 0x05, 0x3C,
                    0xC6, 0x11, 0x08, 0x0C,
                    0x80, 0xBE, 0xA5, 0x8C,
                    0x08, 0x00, 0x40, 0x02,
                    0x01, 0x00, 0x02, 0x24,
                }),
            },
            "boot now goes Legal -> memory card check -> Loading -> gameplay in the Starting Level " +
            "(title screen and main menu skipped; intro videos skipped too)")
        { Supersedes = new[] { "Skip intro videos (Vivendi + TTIdent)" } },

        // Amedo
        new(
            "SLES_525.68",
            "Hide + don't wait on the memory-card check screen (check still runs in the background)",
            "PAL version of the NTSC patch: NOPs the boot state 3 wait on the save manager (vaddr 0x00173CF4, " +
            "bne -> nop) and the save manager's show-panel call in SaveManager::Ask (vaddr 0x0029FED4). Same " +
            "behaviour and side effects as the NTSC patch (the busy panel for in-game save/load won't show; " +
            "with no memory card the 'No memory card' prompt shows over loading/gameplay).",
            new[]
            {
                new PatchEdit(0x74CF4, new byte[] { 0x00, 0x00, 0x00, 0x00 }),
                new PatchEdit(0x1A0ED4, new byte[] { 0x00, 0x00, 0x00, 0x00 }),
            },
            "boot no longer waits for or shows the memory-card check screen (check runs in the background)"),
    };

    private void DrawUiBootPatch()
    {
        BuildIsoToPS2.BootRegionDef? region = null;
        string exePath = "";
        foreach (var r in BuildIsoToPS2.BootRegions)
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
        ImGui.TextDisabled("Set automatically on Build ISO: the game starts in the level you build from, like Build to PC.");

        var patchesForThisExe = BootPatches.Where(d => d.ExeName == reg.ExeName).ToList();
        if (patchesForThisExe.Count > 0)
        {
            foreach (var def in patchesForThisExe)
            {
                ImGui.PushID(def.Name);
                bool isPatched = IsBootPatchApplied(exePath, def);
                // Amedo 2026-09-24
                var includedBy = patchesForThisExe.FirstOrDefault(o =>
                    o.Supersedes != null && o.Supersedes.Contains(def.Name) && IsBootPatchApplied(exePath, o));
                bool isIncluded = !isPatched && includedBy.Name != null;
                ImGui.Separator();
                ImGui.Text(def.Name);
                ImGui.TextWrapped(def.Description);
                if (isPatched) ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "Status: PATCHED");
                else if (isIncluded) ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), $"Status: included by '{includedBy.Name}'");
                else ImGui.TextDisabled("Status: original (unpatched)");

                ImGui.BeginDisabled(isPatched || isIncluded);
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
                // Amedo 2026-09-24
                foreach (var edit in def.Edits)
                {
                    if (edit.NewBytes.Length <= 4) continue;
                    fs.Seek(edit.FileOffset, System.IO.SeekOrigin.Begin);
                    var cur = new byte[edit.NewBytes.Length];
                    if (fs.Read(cur, 0, cur.Length) != cur.Length) throw new Exception($"exe too short for cave at 0x{edit.FileOffset:X}");
                    bool free = cur.All(b => b == 0) || cur.AsSpan().SequenceEqual(edit.NewBytes);
                    if (!free) throw new Exception($"code cave at 0x{edit.FileOffset:X} is not free — patch NOT applied");
                }
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
}
