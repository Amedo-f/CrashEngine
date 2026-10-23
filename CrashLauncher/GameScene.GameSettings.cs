using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private const uint CreateDamageCommandIndex = 514;
    private const uint TriggerBalancedCrateFallingCommandIndex = 519;
    private const int  DamageAmountArgIndex     = 7;
    private const int  DamageTypeArgIndex       = 8;
    private const uint VanillaBlastAmount       = 0x00000320;
    private const uint ClassicBlastAmount       = 0x00000008;
    private const uint PristineDamageType       = 0x0000000D;
    private const uint CrateExplodeBehaviourId  = 0x002F;

    private static ITwinBehaviourCommand? FindBigBlastCommand(PS2BehaviourGraph graph) => CrashEngine.Runtime.PlaySettings.FindBigBlast(graph); // Amedo 2026-10-04

    // Amedo 2026-09-25
    private bool? ReadClassicAkuAku() =>
        CrashEngine.Runtime.PlaySettings.ClassicFromData(Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.GlobalRm2); // Amedo 2026-10-04

    private static List<ITwinBehaviourCommand> FindAllCreateDamageCommands(PS2BehaviourGraph graph)
    {
        var found = new List<ITwinBehaviourCommand>();
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == CreateDamageCommandIndex && cmd.Arguments.Count > DamageTypeArgIndex)
                        found.Add(cmd);
        return found;
    }

    private void DrawGameSettings()
    {
        if (!_showGameSettings) return;

        ImGui.SetNextWindowSize(new Vector2(480f, 200f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Game Settings##gamesettings", ref _showGameSettings))
        {
            ImGui.End();
            return;
        }

        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var behSec = chunkSource?.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
        var explodeGraph = behSec?.GetItem<PS2BehaviourGraph>(CrateExplodeBehaviourId);

        if (chunkSource is null)
        {
            ImGui.TextDisabled("No level currently loaded.");
        }
        else if (explodeGraph is null)
        {
            ImGui.TextDisabled("Couldn't find the Nitro/TNT explosion behaviour (0x002F) in Startup\\Default.rm2.");
        }
        else
        {
            var bigBlast = FindBigBlastCommand(explodeGraph);
            if (bigBlast is null)
            {
                ImGui.TextDisabled("Couldn't find the initial-blast CreateDamage call.");
            }
            else
            {
                bool isClassic = bigBlast.Arguments[DamageAmountArgIndex] == ClassicBlastAmount;
                bool isVanilla = bigBlast.Arguments[DamageAmountArgIndex] == VanillaBlastAmount;
                if (GetGameProject() is { } classicProj && classicProj.ClassicAkuAku != isClassic) // Amedo 2026-09-25
                {
                    classicProj.ClassicAkuAku = isClassic;
                    classicProj.Save();
                }

                bool classicAkuAku = isClassic;
                if (ImGui.Checkbox("Classic Aku Aku##classicakuaku", ref classicAkuAku))
                {
                    bigBlast.Arguments[DamageAmountArgIndex] = classicAkuAku ? ClassicBlastAmount : VanillaBlastAmount;
                    foreach (var cmd in FindAllCreateDamageCommands(explodeGraph))
                        cmd.Arguments[DamageTypeArgIndex] = PristineDamageType;
                    chunkSource.GlobalRm2Dirty = true;
                }
                if (ImGui.IsItemHovered())
                {
                    MaybeTooltip("Classic-games Aku Aku: getting hit by a Nitro or TNT Crate only\n" +
                                 "kills you if you have NO Aku Aku masks — same as any other damage\n" +
                                 "source, instead of always killing you outright regardless of masks.\n" +
                                 "Shrinks the crate explosion's initial blast damage from 800 to 8\n" +
                                 "(matching TheBetaM/CrateModLoader's real, decompiled fix — small\n" +
                                 "enough for Aku Aku to actually absorb). Edits the shared Nitro/TNT\n" +
                                 "explosion script (Startup\\Default.rm2) — affects every level.\n" +
                                 "Also fixes the golden Aku Aku glow disappearing after Crash walks\n" +
                                 "into another level (the glow now follows him), and the golden\n" +
                                 "invincibility ending after an in-game cutscene (its timer now keeps\n" +
                                 "running during the cutscene: the rest continues afterwards, or it\n" +
                                 "ends on time mid-cutscene). Both applied on Build ISO.\n" +
                                 "Save Chunk to persist, then Build ISO + test.");
                }

                if (!isClassic && !isVanilla)
                    ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1f),
                        "Mixed/unexpected state — the initial-blast damage amount doesn't match\neither known value; check manually before relying on this checkbox.");
            }
        }

        DrawAkuAkuInvincibility(); // Amedo 2026-09-25

        ImGui.Separator();
        DrawDoubleJumpRules(); // Amedo 2026-09-25

        if (chunkSource is not null) // Amedo
        {
            ImGui.Separator();
            DrawCratesReceiveShadows(chunkSource);
        }

        ImGui.Separator(); // Amedo 2026-10-01
        DrawLanguage();

        ImGui.End();
    }

    // Amedo 2026-09-25
    private void DrawAkuAkuInvincibility()
    {
        var proj = GetGameProject();
        if (proj is null || !BuildIsoToPS2.HasPatchableExe(_extractedRoot)) return; // Amedo

        bool infinite = proj.AkuAkuInvincibilitySeconds == BuildIsoToPS2.AkuAkuInfinite;
        int seconds = infinite ? BuildIsoToPS2.AkuAkuDefaultSeconds
            : proj.AkuAkuInvincibilitySeconds ?? BuildIsoToPS2.AkuAkuDefaultSeconds;

        ImGui.BeginDisabled(infinite);
        ImGui.SetNextItemWidth(100f);
        if (ImGui.InputInt("Golden Aku Aku (seconds)##akusecs", ref seconds))
            SetAkuAkuInvincibility(proj, Math.Clamp(seconds, 1, BuildIsoToPS2.AkuAkuMaxSeconds));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            MaybeTooltip("Original game: 8 seconds. How long the golden invincibility lasts after\n" +
                         "collecting the 3rd Aku Aku mask (1-255).");
        ImGui.SameLine();
        if (ImGui.Checkbox("Infinite##akuinfinite", ref infinite))
            SetAkuAkuInvincibility(proj, infinite ? BuildIsoToPS2.AkuAkuInfinite : BuildIsoToPS2.AkuAkuDefaultSeconds);
        if (ImGui.IsItemHovered())
            MaybeTooltip("The golden invincibility never runs out.");

        bool touchBreak = proj.AkuAkuTouchBreak;
        if (ImGui.Checkbox("Golden Aku Aku breaks crates and enemies on touch##akutouchbreak", ref touchBreak))
        {
            proj.AkuAkuTouchBreak = touchBreak;
            proj.Save();
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF. While the golden invincibility lasts, touching a crate or an\n" +
                         "enemy counts as a spin (crates break, enemies get hit). Landing on top works as\n" +
                         "usual, and wumpa are still collected.");

        DrawAkuAkuGoldenMusic(proj); // Amedo 2026-09-25
    }

    // Amedo 2026-09-25
    private void DrawAkuAkuGoldenMusic(CrashProject proj)
    {
        LoadCustomMusicNames();
        uint? musicId = proj.AkuAkuGoldenMusicId;
        string musicLabel = musicId is not { } mid ? "None (original game)"
            : _customMusicNames.TryGetValue(mid, out var customName) ? $"{customName} (id {mid})"
            : MusicNames.TryGetValue(mid, out var name) ? $"{name} (id {mid})"
            : $"id {mid}";
        ImGui.TextDisabled($"Golden Aku Aku music: {musicLabel}");

        ImGui.BeginDisabled(_musicReplaceInProgress);
        if (ImGui.Button("Import WAV/MP3...##akumusicimport"))
            ReplaceMusicTrackAudio(musicId ?? 0, "Golden Aku Aku music", newId =>
            {
                proj.AkuAkuGoldenMusicId = newId;
                proj.Save();
            });
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: no music. Pick a WAV or MP3 to play while the golden invincibility\n" +
                         "lasts. It starts right away when the 3rd mask is collected and keeps playing if\n" +
                         "another mask is collected or Crash walks into another level. When the golden\n" +
                         "invincibility ends, the current level's music fades back in.\n" +
                         "Added to MUSIC.MB as a new track. MP3 is resampled to 32000Hz automatically; a WAV\n" +
                         "is used as-is, so save it at 32000Hz (the game's rate). Applied on the next Build ISO.");

        if (musicId is { } playId)
        {
            ImGui.SameLine();
            if (ImGui.Button("Preview##akumusicpreview")) PreviewMusicTrack(playId, musicLabel);
            ImGui.SameLine();
            if (ImGui.Button("Stop##akumusicstop")) StopMusicPreview();
            ImGui.SameLine();
            ImGui.BeginDisabled(_musicReplaceInProgress);
            if (ImGui.Button("Remove##akumusicremove"))
            {
                proj.AkuAkuGoldenMusicId = null;
                proj.Save();
            }
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                MaybeTooltip("Back to the original game: no music during the golden invincibility.");

            int volume = proj.AkuAkuGoldenMusicVolume ?? BuildIsoToPS2.AkuAkuMusicDefaultVolume;
            ImGui.SetNextItemWidth(200f);
            if (ImGui.SliderInt("Golden music volume (%)##akumusicvol", ref volume, 1, 100))
                proj.AkuAkuGoldenMusicVolume = volume == BuildIsoToPS2.AkuAkuMusicDefaultVolume ? null : volume;
            if (ImGui.IsItemDeactivatedAfterEdit()) proj.Save();
            if (ImGui.IsItemHovered())
                MaybeTooltip("The in-game volume of the golden music. 75% = the game's own music volume\n" +
                             "(most level tracks play at 75%). 100% = the loudest the PS2 can play a track.\n" +
                             "The audio itself isn't changed. Applied on the next Build ISO.");
        }

        if (_musicReplaceInProgress)
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f),
                "Working — reading/writing the ~200MB MUSIC.MB archive, this can take several seconds...");
    }

    private static void SetAkuAkuInvincibility(CrashProject proj, int seconds)
    {
        proj.AkuAkuInvincibilitySeconds = seconds == BuildIsoToPS2.AkuAkuDefaultSeconds ? null : seconds;
        proj.Save();
    }

    // Amedo 2026-09-25
    private void DrawDoubleJumpRules()
    {
        if (!ImGui.CollapsingHeader("Jump Rules##doublejumprules")) return;
        var proj = GetGameProject();
        if (proj is null) { ImGui.TextDisabled("No project loaded."); return; }
        if (!BuildIsoToPS2.HasPatchableExe(_extractedRoot)) // Amedo
        {
            ImGui.TextDisabled("Only available for NTSC-U (SLUS_209.09) and PAL (SLES_525.68) discs.");
            return;
        }

        uint mask = proj.DoubleJumpMask ?? BuildIsoToPS2.DoubleJumpDefaultMask;

        bool single = (mask & 0x44) == 0;
        if (ImGui.Checkbox("Single jump only##djnormal", ref single))
        {
            mask = single ? mask & ~0x44u : mask | 0x44;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (the normal jump, standing or running, allows a double jump).\n" +
                         "Turn it on and the normal jump becomes a single jump.");

        bool slide = (mask & 0x20) != 0;
        if (ImGui.Checkbox("Allow double jump after slide jump##djslide", ref slide))
        {
            mask = slide ? mask | 0x20 : mask & ~0x20u;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (the slide jump is a single jump).\n" +
                         "The double jump after a slide jump also turns Crash to face the stick,\n" +
                         "like a normal double jump.");

        bool spin = (mask & BuildIsoToPS2.DoubleJumpSpinBit) != 0;
        if (ImGui.Checkbox("Allow double jump while spinning##djspin", ref spin))
        {
            mask = spin ? mask | BuildIsoToPS2.DoubleJumpSpinBit : mask & ~BuildIsoToPS2.DoubleJumpSpinBit;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (jumping while spinning is a single jump, and the spin keeps\n" +
                         "the double jump blocked until Crash lands). Turn it on to allow a double jump\n" +
                         "during and after the spin.");

        bool slideSpin = (mask & BuildIsoToPS2.SlideJumpSpinBit) != 0;
        if (ImGui.Checkbox("Allow spin during slide jump##spinslide", ref slideSpin))
        {
            mask = slideSpin ? mask | BuildIsoToPS2.SlideJumpSpinBit : mask & ~BuildIsoToPS2.SlideJumpSpinBit;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (the spin is cancelled during a slide jump).");

        bool slideSlam = (mask & BuildIsoToPS2.SlideJumpBodySlamBit) != 0;
        if (ImGui.Checkbox("Allow body slam during slide jump##bsslide", ref slideSlam))
        {
            mask = slideSlam ? mask | BuildIsoToPS2.SlideJumpBodySlamBit : mask & ~BuildIsoToPS2.SlideJumpBodySlamBit;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (the body slam button does nothing during a slide jump).");

        bool isDefault = mask == BuildIsoToPS2.DoubleJumpDefaultMask;
        ImGui.BeginDisabled(isDefault);
        if (ImGui.Button("Reset to original rules##djreset")) SetDoubleJumpMask(proj, BuildIsoToPS2.DoubleJumpDefaultMask);
        ImGui.EndDisabled();
    }

    private void SetDoubleJumpMask(CrashProject proj, uint mask)
    {
        proj.DoubleJumpMask = mask == BuildIsoToPS2.DoubleJumpDefaultMask ? null : mask;
        proj.Save();
        _browser.Log(proj.DoubleJumpMask is null
            ? "Jump rules: back to the original game rules (applied on next Build ISO)."
            : $"Jump rules: saved (mask 0x{mask:X2}) — applied on next Build ISO.");
    }

    private const uint BeginMusicCommandId = 88;

    private const uint DjDefaultBehaviourId = 0x4CF;

    private const uint AmbientDefaultBehaviourId   = 0x190D;
    private const uint AmbientActivatedBehaviourId = 0x190F;

    private static List<ITwinBehaviourCommand> FindBeginMusicCommandsInGraph(PS2BehaviourGraph? graph)
    {
        var found = new List<ITwinBehaviourCommand>();
        if (graph is null) return found;
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == BeginMusicCommandId && cmd.Arguments.Count > 0)
                        found.Add(cmd);
        return found;
    }

    private string _musicIdTextInput = "";

    private void DrawLevelMusic(uint currentId, string countLabel, Action<uint> applyNewId, Action resetToOriginal)
    {
        bool hasName = MusicNames.TryGetValue(currentId, out var curName);
        string currentLabel = hasName ? $"{curName} (id {currentId})" : $"id {currentId}";
        ImGui.TextDisabled(hasName
            ? $"Current: {curName} (id {currentId})  —  {countLabel}"
            : $"Current: id {currentId} (undocumented)  —  {countLabel}");

        if (ImGui.Button("Preview##musicPreview"))
            PreviewMusicTrack(currentId, currentLabel);
        ImGui.SameLine();
        if (ImGui.Button("Stop##musicPreviewStop"))
            StopMusicPreview();
        ImGui.SameLine();
        if (ImGui.Button("Save As WAV...##musicSaveWav"))
            SaveMusicTrackAsWav(currentId, currentLabel);
        if (ImGui.IsItemHovered())
            MaybeTooltip("Decodes this track's real audio straight from MUSIC.MB and saves it as\n" +
                              "a plain, standalone .wav file — for listening outside the editor, or\n" +
                              "just as a sanity-check that the decode itself is correct.");


        ImGui.BeginDisabled(_musicReplaceInProgress);
        if (ImGui.Button("Import From External WAV/MP3...##musicReplace", new Vector2(-1f, 0f)))
            ReplaceMusicTrackAudio(currentId, currentLabel, applyNewId);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Pick a WAV or MP3 and add it as a BRAND-NEW track id, then repoint THIS\n" +
                              "reference at it — the original id's own audio is never touched\n" +
                              "(unlike the old behaviour, which overwrote it in place). CAUTION:\n" +
                              "the file's own sample rate is used (MP3 auto-resampled to 32000Hz; WAV\n" +
                              "used as-is, un-validated) — every real track in this game uses 32000Hz,\n" +
                              "so a WAV at a different rate will likely play at the wrong pitch/speed.\n" +
                              "Prefer the button above unless you specifically need external audio.\n" +
                              "Rewrites the whole ~200MB MUSIC.MB archive (a few seconds, runs in the\n" +
                              "background) — writes to temp files first, only swaps over the real files\n" +
                              "once the full write succeeds. Save Chunk to persist the new id reference.");
        if (_musicReplaceInProgress)
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f),
                "Working — reading/writing the ~200MB MUSIC.MB archive, this can take several seconds...");

        ImGui.BeginDisabled(_musicReplaceInProgress);
        if (ImGui.Button("Reset to Original##musicReset", new Vector2(-1f, 0f)))
            resetToOriginal();
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered())
            MaybeTooltip("Restores this level's ORIGINAL music id (read fresh from the disc\n" +
                              "archive, in-memory only — needs Save Chunk to persist, same as every\n" +
                              "other Reset button in this panel) AND restores that id's own audio in\n" +
                              "MUSIC.MB from the pristine backup (written straight to disc immediately\n" +
                              "— undoes a 'Replace'/'Copy Audio' edit for this id, shared across every\n" +
                              "level using it). Doesn't touch any other saved edit (colors, textures,\n" +
                              "etc.) in this level.");

        if (_musicLastError is not null)
            ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), $"Error: {_musicLastError}");

        DrawMusicPreviewStatus();

        ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1f),
            "An id that sounds \"weird\"/off as background music is usually a real short mono\n" +
            "sound asset (jingle/stinger), not a broken/empty slot — Preview it above to check\n" +
            "before building. A genuinely empty id logs \"nothing to play\" instead.");

        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##musicPickKnown", "Pick a documented track..."))
        {
            foreach (var kv in MusicNames.OrderBy(kv => kv.Key))
            {
                bool selected = kv.Key == currentId;
                if (ImGui.Selectable($"{kv.Value} (id {kv.Key})", selected))
                    applyNewId(kv.Key);
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Only the ~32 track ids this project has a real name for (from the\n" +
                              "independent twinsanity-editor-0.81 community tool's own MusicID\n" +
                              "enum) — plenty of real, valid ids exist outside this list. See\n" +
                              "'Pick an imported track...' below for anything this project added\n" +
                              "itself, or the raw id field for anything else.");

        EnsureMusicBankLoaded();
        int originalCount = GetOriginalMusicTrackCount();
        if (_musicBankTracks is not null && _musicBankTracks.Count > originalCount)
        {
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.BeginCombo("##musicPickImported", "Pick an imported track..."))
            {
                for (uint id = (uint)originalCount; id < _musicBankTracks.Count; id++)
                {
                    bool selected = id == currentId;
                    string label = _customMusicNames.TryGetValue(id, out var name) ? $"{name} (id {id})" : $"id {id}";
                    if (ImGui.Selectable(label, selected))
                        applyNewId(id);
                    if (selected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                MaybeTooltip("Every track id beyond the game's original count — i.e. everything\n" +
                                  "THIS project has ever added via Import, across every session\n" +
                                  "(labelled with its import filename when known, just its id\n" +
                                  "otherwise). Only shows up once at least one such track exists.");
        }

        ImGui.SetNextItemWidth(120f);
        ImGui.InputTextWithHint("##musicIdRaw", "raw id", ref _musicIdTextInput, 8, ImGuiInputTextFlags.CharsDecimal);
        ImGui.SameLine();
        if (ImGui.Button("Apply id##musicApplyRaw") && uint.TryParse(_musicIdTextInput, out var rawId))
            applyNewId(rawId);
        if (ImGui.IsItemHovered())
            MaybeTooltip("Set by raw numeric track id — no name lookup, for ids not in the\n" +
                              "documented list above. Save Chunk + Build ISO to hear it for real.");
    }

    private void ResetLevelMusicToOriginal(List<ITwinBehaviourCommand> liveCalls, params uint[] behaviourIds)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCalls = behaviourIds
                .SelectMany(id => FindBeginMusicCommandsInGraph(freshBehSec?.GetItem<PS2BehaviourGraph>(id)))
                .ToList();
            if (freshCalls.Count == 0)
            { _browser.Log("Reset Level Music: no original BeginMusic call found on disc — nothing to reset to."); return; }

            var tally = new Dictionary<uint, int>();
            foreach (var cmd in freshCalls) tally[cmd.Arguments[0]] = tally.GetValueOrDefault(cmd.Arguments[0]) + 1;
            uint originalId = tally.OrderByDescending(kv => kv.Value).First().Key;

            foreach (var cmd in liveCalls) cmd.Arguments[0] = originalId;

            _musicLabel = MusicNames.TryGetValue(originalId, out var name)
                ? $"{name} (id {originalId})" : $"id {originalId} (undocumented — not in the reference tool's own name table)";
            _browser.Log($"Reset Level Music: restored {liveCalls.Count} BeginMusic call(s) to the original " +
                          $"{_musicLabel} (in-memory). Save Chunk to persist.");

            RestoreMusicTrackContentFromBackup(originalId, _musicLabel);
        }
        catch (Exception ex)
        {
            _browser.Log($"Reset Level Music failed: {ex.Message}");
        }
    }

    private void ResetDjMusicParamToOriginal(InstanceData data)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            PS2AnyInstance? original = null;
            for (int lid = 0; lid <= 7 && original is null; lid++)
            {
                var layout = freshRm2.GetItem<BaseTwinSection>((uint)lid);
                var instSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (instSec is null) continue;
                for (int i = 0; i < instSec.GetItemsAmount(); i++)
                {
                    if (instSec.GetItem(i) is PS2AnyInstance cand &&
                        cand.GetID() == data.Source.GetID() && cand.ObjectId == data.ObjectId)
                    { original = cand; break; }
                }
            }
            if (original is null || original.ParamList3.Count == 0)
            {
                _browser.Log("Reset DJ Music: couldn't find this DJ instance's original ParamList3 on disc.");
                return;
            }

            data.Source.ParamList3.Clear();
            data.Source.ParamList3.AddRange(original.ParamList3);
            var originalId = original.ParamList3[0];
            _musicLabel = MusicNames.TryGetValue(originalId, out var name)
                ? $"{name} (id {originalId})" : $"id {originalId} (undocumented — not in the reference tool's own name table)";
            _browser.Log($"Reset DJ Music: restored this DJ instance's ParamList3 to its original " +
                          $"{_musicLabel} (in-memory). Save Chunk to persist.");

            RestoreMusicTrackContentFromBackup(originalId, _musicLabel);
        }
        catch (Exception ex)
        {
            _browser.Log($"Reset DJ Music failed: {ex.Message}");
        }
    }

    private const uint IronSpringCrateLandedOnId   = 0x5F;
    private const uint WoodenSpringCrateLandedOnId = 0x67;
    private const uint ApplyVelocityCommandIndex   = 523;
    private const int  SpringVelocityArgIndex1     = 5;
    private const int  SpringVelocityArgIndex2     = 6;

    private static float GetArgFloat(ITwinBehaviourCommand cmd, int idx) =>
        idx < cmd.Arguments.Count ? BitConverter.Int32BitsToSingle((int)cmd.Arguments[idx]) : 0f;

    private static void SetArgFloat(ITwinBehaviourCommand cmd, int idx, float value)
    {
        if (idx < cmd.Arguments.Count)
            cmd.Arguments[idx] = unchecked((uint)BitConverter.SingleToInt32Bits(value));
    }

    private static List<ITwinBehaviourCommand> FindApplyVelocityCommands(PS2BehaviourGraph? graph)
    {
        var found = new List<ITwinBehaviourCommand>();
        if (graph is null) return found;
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == ApplyVelocityCommandIndex && cmd.Arguments.Count > SpringVelocityArgIndex2)
                        found.Add(cmd);
        return found;
    }

    private static List<ITwinBehaviourCommand> FindAllSpringCommands(BaseTwinSection? behSec)
    {
        var ironGraph = behSec?.GetItem<PS2BehaviourGraph>(IronSpringCrateLandedOnId);
        return FindApplyVelocityCommands(ironGraph);
    }

    private List<float>? _springOriginalValues;
    private float _springBounceMultiplier = 1f;
    private bool _springMultiplierActive;

    private void DrawSpringCrateBounce(ChunkSource chunkSource, BaseTwinSection? behSec)
    {
        var cmds = FindAllSpringCommands(behSec);
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find the Iron Spring Crate bounce behaviour (0x5F) in Startup\\Default.rm2.");
            return;
        }

        if (_springOriginalValues is null || _springOriginalValues.Count != cmds.Count * 2)
        {
            _springOriginalValues = LoadSpringOriginalValues(cmds.Count)
                ?? cmds.SelectMany(c => new[] { GetArgFloat(c, SpringVelocityArgIndex1), GetArgFloat(c, SpringVelocityArgIndex2) }).ToList();
        }

        if (!_springMultiplierActive && _springOriginalValues.Count > 0 && _springOriginalValues[0] != 0f)
            _springBounceMultiplier = GetArgFloat(cmds[0], SpringVelocityArgIndex1) / _springOriginalValues[0];

        ImGui.Text("Iron Spring Crate Bounce Height — CONFIRMED WORKING LIVE");

        bool multChanged = ImGui.SliderFloat("Bounce Multiplier##springbounce", ref _springBounceMultiplier, 0.2f, 4f, "%.2fx");
        _springMultiplierActive = ImGui.IsItemActive();
        if (multChanged)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex1, _springOriginalValues[i * 2] * _springBounceMultiplier);
                SetArgFloat(cmds[i], SpringVelocityArgIndex2, _springOriginalValues[i * 2 + 1] * _springBounceMultiplier);
            }
            chunkSource.GlobalRm2Dirty = true;
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("CONFIRMED live in PCSX2 (2026-08-11) — scales both launch-power numbers\n" +
                         "below relative to the true original. 1.00x = original bounce.\n" +
                         "Save Chunk to persist, then Build ISO + test.");
        }

        bool anyChanged = false;
        for (int i = 0; i < cmds.Count; i++)
        {
            float v1 = GetArgFloat(cmds[i], SpringVelocityArgIndex1);
            if (ImGui.InputFloat($"Launch Power {i}##springpower{i}", ref v1, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex1, v1);
                anyChanged = true;
            }
            float v2 = GetArgFloat(cmds[i], SpringVelocityArgIndex2);
            if (ImGui.InputFloat($"Launch Power {i} (secondary)##springpower2_{i}", ref v2, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex2, v2);
                anyChanged = true;
            }
        }
        if (anyChanged) chunkSource.GlobalRm2Dirty = true;

        if (ImGui.Button("Reset##springreset"))
        {
            _springBounceMultiplier = 1f;
            ResetSpringToOriginal(chunkSource, behSec);
        }
    }

    private List<float>? LoadSpringOriginalValues(int expectedCount)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                ?? throw new FileNotFoundException("Couldn't reopen Startup\\Default.rm2 from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshDefault = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
            freshDefault.Read(reader, (int)stream.Length);

            var freshBehSec = freshDefault.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllSpringCommands(freshBehSec);
            var values = freshCmds.SelectMany(c => new[] { GetArgFloat(c, SpringVelocityArgIndex1), GetArgFloat(c, SpringVelocityArgIndex2) }).ToList();
            return values.Count == expectedCount * 2 ? values : null;
        }
        catch { return null; }
    }

    private void ResetSpringToOriginal(ChunkSource chunkSource, BaseTwinSection? behSec)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                ?? throw new FileNotFoundException("Couldn't reopen Startup\\Default.rm2 from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshDefault = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
            freshDefault.Read(reader, (int)stream.Length);

            var freshBehSec = freshDefault.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllSpringCommands(freshBehSec);
            var liveCmds = FindAllSpringCommands(behSec);
            for (int i = 0; i < liveCmds.Count && i < freshCmds.Count; i++)
            {
                SetArgFloat(liveCmds[i], SpringVelocityArgIndex1, GetArgFloat(freshCmds[i], SpringVelocityArgIndex1));
                SetArgFloat(liveCmds[i], SpringVelocityArgIndex2, GetArgFloat(freshCmds[i], SpringVelocityArgIndex2));
            }
            chunkSource.GlobalRm2Dirty = true;
            _browser.Log("Reset Spring Crate Bounce Height to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Spring Crate Bounce Height failed: {ex.Message}"); }
    }

    private const uint EarthWormSquashLaunchId = 0x5E9;

    private static List<ITwinBehaviourCommand> FindAllWormCommands(BaseTwinSection? behSec)
    {
        var graph = behSec?.GetItem<PS2BehaviourGraph>(EarthWormSquashLaunchId);
        return FindApplyVelocityCommands(graph);
    }

    private List<float>? _wormOriginalValues;
    private float _wormLaunchMultiplier = 1f;
    private bool _wormMultiplierActive;

    private void DrawWormLaunchPower(BaseTwinSection? behSec)
    {
        var cmds = FindAllWormCommands(behSec);
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find the Earth Worm squash-launch behaviour (0x5E9) in this level.");
            return;
        }

        if (_wormOriginalValues is null || _wormOriginalValues.Count != cmds.Count * 2)
        {
            _wormOriginalValues = LoadWormOriginalValues(cmds.Count)
                ?? cmds.SelectMany(c => new[] { GetArgFloat(c, SpringVelocityArgIndex1), GetArgFloat(c, SpringVelocityArgIndex2) }).ToList();
        }

        if (!_wormMultiplierActive && _wormOriginalValues.Count > 0 && _wormOriginalValues[0] != 0f)
            _wormLaunchMultiplier = GetArgFloat(cmds[0], SpringVelocityArgIndex1) / _wormOriginalValues[0];

        ImGui.Text("Earth Worm Squash Launch Power (not yet live-tested)");
        bool multChanged = ImGui.SliderFloat("Launch Multiplier##wormlaunch", ref _wormLaunchMultiplier, 0.2f, 4f, "%.2fx");
        _wormMultiplierActive = ImGui.IsItemActive();
        if (multChanged)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex1, _wormOriginalValues[i * 2] * _wormLaunchMultiplier);
                SetArgFloat(cmds[i], SpringVelocityArgIndex2, _wormOriginalValues[i * 2 + 1] * _wormLaunchMultiplier);
            }
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("Same command/argument as Iron Spring Crate Bounce Height (cross-\n" +
                         "validated, not just a guess) but built for this specific creature\n" +
                         "just now — not yet confirmed live in PCSX2. 1.00x = original (~10.0).\n" +
                         "Save Chunk to persist, then Build ISO + test.");
        }

        bool anyChanged = false;
        for (int i = 0; i < cmds.Count; i++)
        {
            float v1 = GetArgFloat(cmds[i], SpringVelocityArgIndex1);
            if (ImGui.InputFloat($"Launch Power {i}##wormpower{i}", ref v1, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex1, v1);
                anyChanged = true;
            }
            float v2 = GetArgFloat(cmds[i], SpringVelocityArgIndex2);
            if (ImGui.InputFloat($"Launch Power {i} (secondary)##wormpower2_{i}", ref v2, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex2, v2);
                anyChanged = true;
            }
        }
        _ = anyChanged;

        if (ImGui.Button("Reset##wormreset"))
        {
            _wormLaunchMultiplier = 1f;
            ResetWormToOriginal(behSec);
        }
    }

    private List<float>? LoadWormOriginalValues(int expectedCount)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllWormCommands(freshBehSec);
            var values = freshCmds.SelectMany(c => new[] { GetArgFloat(c, SpringVelocityArgIndex1), GetArgFloat(c, SpringVelocityArgIndex2) }).ToList();
            return values.Count == expectedCount * 2 ? values : null;
        }
        catch { return null; }
    }

    private void ResetWormToOriginal(BaseTwinSection? behSec)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllWormCommands(freshBehSec);
            var liveCmds = FindAllWormCommands(behSec);
            for (int i = 0; i < liveCmds.Count && i < freshCmds.Count; i++)
            {
                SetArgFloat(liveCmds[i], SpringVelocityArgIndex1, GetArgFloat(freshCmds[i], SpringVelocityArgIndex1));
                SetArgFloat(liveCmds[i], SpringVelocityArgIndex2, GetArgFloat(freshCmds[i], SpringVelocityArgIndex2));
            }
            _browser.Log("Reset Earth Worm Squash Launch Power to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Earth Worm Squash Launch Power failed: {ex.Message}"); }
    }

    private const uint RigidCannonActivatedId = 0xF33;
    private const uint AUnknownCannonCommandIndex = 96;
    private const int  CannonPowerArgIndex1 = 41;
    private const int  CannonPowerArgIndex2 = 43;

    private static List<ITwinBehaviourCommand> FindAllCannonCommands(BaseTwinSection? behSec)
    {
        var found = new List<ITwinBehaviourCommand>();
        var graph = behSec?.GetItem<PS2BehaviourGraph>(RigidCannonActivatedId);
        if (graph is null) return found;
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == AUnknownCannonCommandIndex && cmd.Arguments.Count > CannonPowerArgIndex2)
                        found.Add(cmd);
        return found;
    }

    private List<float>? _cannonOriginalValues;
    private float _cannonPowerMultiplier = 1f;
    private bool _cannonMultiplierActive;

    private void DrawCannonShotPower(BaseTwinSection? behSec)
    {
        var cmds = FindAllCannonCommands(behSec);
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find the Rigid Cannon fire behaviour (0xF33) in this level.");
            return;
        }

        if (_cannonOriginalValues is null || _cannonOriginalValues.Count != cmds.Count * 2)
        {
            _cannonOriginalValues = LoadCannonOriginalValues(cmds.Count)
                ?? cmds.SelectMany(c => new[] { GetArgFloat(c, CannonPowerArgIndex1), GetArgFloat(c, CannonPowerArgIndex2) }).ToList();
        }

        if (!_cannonMultiplierActive && _cannonOriginalValues.Count > 0 && _cannonOriginalValues[0] != 0f)
            _cannonPowerMultiplier = GetArgFloat(cmds[0], CannonPowerArgIndex1) / _cannonOriginalValues[0];

        ImGui.Text("Cannon Shot Power — CONFIRMED WORKING LIVE");
        ImGui.TextDisabled("Higher = fires farther. Too high and the shot comes out jammed/stuck instead.");
        bool multChanged = ImGui.SliderFloat("Power Multiplier##cannonpower", ref _cannonPowerMultiplier, 0.2f, 4f, "%.2fx");
        _cannonMultiplierActive = ImGui.IsItemActive();
        if (multChanged)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                SetArgFloat(cmds[i], CannonPowerArgIndex1, _cannonOriginalValues[i * 2] * _cannonPowerMultiplier);
                SetArgFloat(cmds[i], CannonPowerArgIndex2, _cannonOriginalValues[i * 2 + 1] * _cannonPowerMultiplier);
            }
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("CONFIRMED live in PCSX2 (2026-08-11) — was a genuine guess (argument\n" +
                         "index 41/43 of the undocumented AUnknown_96 command) but the user\n" +
                         "confirmed live: bodyslamming the cannon's button fires it farther as\n" +
                         "this goes up. Known quirk: pushed too high, the shot comes out\n" +
                         "jammed/stuck instead of a clean launch — keep it moderate.\n" +
                         "1.00x = original. Save Chunk to persist, then Build ISO + test.");
        }

        for (int i = 0; i < cmds.Count; i++)
        {
            float v1 = GetArgFloat(cmds[i], CannonPowerArgIndex1);
            if (ImGui.InputFloat($"Shot {i} Power##cannonpower1_{i}", ref v1, 0.5f, 5f, "%.3f"))
                SetArgFloat(cmds[i], CannonPowerArgIndex1, v1);
            float v2 = GetArgFloat(cmds[i], CannonPowerArgIndex2);
            if (ImGui.InputFloat($"Shot {i} Power (secondary)##cannonpower2_{i}", ref v2, 0.5f, 5f, "%.3f"))
                SetArgFloat(cmds[i], CannonPowerArgIndex2, v2);
        }

        if (ImGui.Button("Reset##cannonreset"))
        {
            _cannonPowerMultiplier = 1f;
            ResetCannonToOriginal(behSec);
        }
    }

    private List<float>? LoadCannonOriginalValues(int expectedCount)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllCannonCommands(freshBehSec);
            var values = freshCmds.SelectMany(c => new[] { GetArgFloat(c, CannonPowerArgIndex1), GetArgFloat(c, CannonPowerArgIndex2) }).ToList();
            return values.Count == expectedCount * 2 ? values : null;
        }
        catch { return null; }
    }

    private void ResetCannonToOriginal(BaseTwinSection? behSec)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllCannonCommands(freshBehSec);
            var liveCmds = FindAllCannonCommands(behSec);
            for (int i = 0; i < liveCmds.Count && i < freshCmds.Count; i++)
            {
                SetArgFloat(liveCmds[i], CannonPowerArgIndex1, GetArgFloat(freshCmds[i], CannonPowerArgIndex1));
                SetArgFloat(liveCmds[i], CannonPowerArgIndex2, GetArgFloat(freshCmds[i], CannonPowerArgIndex2));
            }
            _browser.Log("Reset Cannon Shot Power to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Cannon Shot Power failed: {ex.Message}"); }
    }

    private const uint GenericCreatureDamagedJumpedOnId = 0x709;
    private const uint ReduceHitPointsCommandIndex = 528;
    private const int  StompDamageArgIndex = 0;
    private const int  StompBounceArgIndex = 2;

    private static List<ITwinBehaviourCommand> FindStompDamageCommands(BaseTwinSection? behSec)
    {
        var found = new List<ITwinBehaviourCommand>();
        var graph = behSec?.GetItem<PS2BehaviourGraph>(GenericCreatureDamagedJumpedOnId);
        if (graph is null) return found;
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == ReduceHitPointsCommandIndex && cmd.Arguments.Count > StompDamageArgIndex)
                        found.Add(cmd);
        return found;
    }

    private static List<ITwinBehaviourCommand> FindStompBounceCommands(BaseTwinSection? behSec)
    {
        var graph = behSec?.GetItem<PS2BehaviourGraph>(GenericCreatureDamagedJumpedOnId);
        return FindApplyVelocityCommands(graph);
    }

    private uint? _stompOriginalDamage;
    private float? _stompOriginalBounce;
    private float _stompDamageMultiplier = 1f;
    private bool _stompDamageMultiplierActive;

    private void DrawEnemyStomp(BaseTwinSection? behSec)
    {
        var dmgCmds = FindStompDamageCommands(behSec);
        var bounceCmds = FindStompBounceCommands(behSec);
        if (dmgCmds.Count == 0 && bounceCmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find the enemy-stomp behaviour (0x709) in this level.");
            return;
        }

        if (_stompOriginalDamage is null && dmgCmds.Count > 0)
            _stompOriginalDamage = LoadStompOriginals(out _stompOriginalBounce) ?? dmgCmds[0].Arguments[StompDamageArgIndex];

        ImGui.Text("Enemy Stomp Damage");
        if (dmgCmds.Count > 0 && _stompOriginalDamage is { } origDmg)
        {
            if (!_stompDamageMultiplierActive && origDmg != 0)
                _stompDamageMultiplier = dmgCmds[0].Arguments[StompDamageArgIndex] / (float)origDmg;

            bool multChanged = ImGui.SliderFloat("Damage Multiplier##stompdmg", ref _stompDamageMultiplier, 0.2f, 5f, "%.2fx");
            _stompDamageMultiplierActive = ImGui.IsItemActive();
            if (multChanged)
                foreach (var cmd in dmgCmds)
                    cmd.Arguments[StompDamageArgIndex] = (uint)Math.Max(0, MathF.Round(origDmg * _stompDamageMultiplier));
            if (ImGui.IsItemHovered())
            {
                MaybeTooltip("Confident, not just a guess — ReduceHitPoints has exactly ONE plain\n" +
                             "whole-number argument (the enemy hit points removed per stomp, no\n" +
                             "float decoding involved). Original = " + origDmg + ".\n" +
                             "Save Chunk to persist, then Build ISO + test.");
            }

            int rawDmg = (int)dmgCmds[0].Arguments[StompDamageArgIndex];
            if (ImGui.InputInt("Damage (raw)##stompdmgraw", ref rawDmg))
            {
                uint clamped = (uint)Math.Max(0, rawDmg);
                foreach (var cmd in dmgCmds) cmd.Arguments[StompDamageArgIndex] = clamped;
            }
        }

        if (bounceCmds.Count > 0)
        {
            ImGui.Text("Enemy Stomp Bounce-Back Power");
            float v = GetArgFloat(bounceCmds[0], StompBounceArgIndex);
            if (ImGui.InputFloat("Bounce Power##stompbounce", ref v, 0.5f, 5f, "%.3f"))
                foreach (var cmd in bounceCmds) SetArgFloat(cmd, StompBounceArgIndex, v);
            if (v >= 16f)
                ImGui.TextColored(new Vector4(1f, 0.6f, 0.2f, 1f),
                    "16.0+ found to kill the bounce entirely (not just clamp it) — stay under 16.");
        }

        if (ImGui.Button("Reset##stompreset"))
        {
            _stompDamageMultiplier = 1f;
            ResetStompToOriginal(behSec);
        }
    }

    private uint? LoadStompOriginals(out float? origBounce)
    {
        origBounce = null;
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshDmgCmds = FindStompDamageCommands(freshBehSec);
            var freshBounceCmds = FindStompBounceCommands(freshBehSec);
            if (freshBounceCmds.Count > 0) origBounce = GetArgFloat(freshBounceCmds[0], StompBounceArgIndex);
            return freshDmgCmds.Count > 0 ? freshDmgCmds[0].Arguments[StompDamageArgIndex] : null;
        }
        catch { return null; }
    }

    private void ResetStompToOriginal(BaseTwinSection? behSec)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshDmgCmds = FindStompDamageCommands(freshBehSec);
            var liveDmgCmds = FindStompDamageCommands(behSec);
            for (int i = 0; i < liveDmgCmds.Count && i < freshDmgCmds.Count; i++)
                liveDmgCmds[i].Arguments[StompDamageArgIndex] = freshDmgCmds[i].Arguments[StompDamageArgIndex];

            var freshBounceCmds = FindStompBounceCommands(freshBehSec);
            var liveBounceCmds = FindStompBounceCommands(behSec);
            for (int i = 0; i < liveBounceCmds.Count && i < freshBounceCmds.Count; i++)
                SetArgFloat(liveBounceCmds[i], StompBounceArgIndex, GetArgFloat(freshBounceCmds[i], StompBounceArgIndex));

            _browser.Log("Reset Enemy Stomp to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Enemy Stomp failed: {ex.Message}"); }
    }

    private static readonly uint[] CrateLandedOnBehaviourIds =
    {
        0x3D,
        0x45,
        0x4B,
        0x53,
        0x6B,
        0x6F,
        0x73,
        0x489,
        0xD49,
        0x1AE5,
        0x1AE9,
    };
    private static readonly Dictionary<uint, string> CrateLandedOnNames = new()
    {
        [0x3D] = "TNT Crate", [0x45] = "Nitro Switch Crate", [0x4B] = "Iron Switch Crate",
        [0x53] = "Multiple-Hit Crate", [0x6B] = "Surprise Crate", [0x6F] = "Basic Crate",
        [0x73] = "Extra Life Crate", [0x489] = "Aku Aku Crate",
        [0xD49] = "Breakable Nitro Switch Crate", [0x1AE5] = "Cortex Life Crate",
        [0x1AE9] = "Nina Life Crate",
    };
    private const int CrateBounceArgIndex = 5;

    private readonly Dictionary<uint, float> _crateBounceOriginals = new();
    private readonly Dictionary<uint, float> _crateBounceMultipliers = new();
    private uint? _crateBounceActiveId;

    private void DrawCrateLandingBounceForId(ChunkSource chunkSource, BaseTwinSection? behSec, uint id)
    {
        var cmds = FindApplyVelocityCommands(behSec?.GetItem<PS2BehaviourGraph>(id));
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find this crate's landing-bounce command in Startup\\Default.rm2.");
            return;
        }
        var cmd = cmds[0];
        string label = CrateLandedOnNames.TryGetValue(id, out var n) ? n : $"0x{id:X}";

        if (!_crateBounceOriginals.TryGetValue(id, out var orig))
        {
            orig = LoadCrateBounceOriginal(id) ?? GetArgFloat(cmd, CrateBounceArgIndex);
            _crateBounceOriginals[id] = orig;
        }
        bool isActive = _crateBounceActiveId == id;
        float mult = _crateBounceMultipliers.TryGetValue(id, out var m) ? m : 1f;
        if (!isActive && orig != 0f)
            mult = GetArgFloat(cmd, CrateBounceArgIndex) / orig;

        ImGui.Text($"{label} Landing Bounce");
        ImGui.TextDisabled("Independent per crate type — doesn't affect any other crate.");
        bool changed = ImGui.SliderFloat($"Bounce Multiplier##cratebounce_{id:X}", ref mult, 0.2f, 4f, "%.2fx");
        if (ImGui.IsItemActive()) _crateBounceActiveId = id;
        else if (isActive) _crateBounceActiveId = null;
        _crateBounceMultipliers[id] = mult;
        if (changed)
        {
            SetArgFloat(cmd, CrateBounceArgIndex, orig * mult);
            chunkSource.GlobalRm2Dirty = true;
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip($"Not yet live-tested — only affects {label}, every other crate type has\n" +
                         "its own separate value. Original ≈ " + orig.ToString("0.###") + ".\n" +
                         "Watch for a hard ceiling like the Cannon/Enemy Stomp had — going too\n" +
                         "high may silently kill the bounce instead of making it bigger.\n" +
                         "Save Chunk to persist, then Build ISO + test.");
        }
        if (ImGui.Button($"Reset##cratebouncereset_{id:X}"))
        {
            _crateBounceMultipliers[id] = 1f;
            ResetCrateBounceForId(chunkSource, behSec, id);
        }
    }

    private float? LoadCrateBounceOriginal(uint id)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                ?? throw new FileNotFoundException("Couldn't reopen Startup\\Default.rm2 from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshDefault = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
            freshDefault.Read(reader, (int)stream.Length);

            var freshBehSec = freshDefault.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindApplyVelocityCommands(freshBehSec?.GetItem<PS2BehaviourGraph>(id));
            return freshCmds.Count > 0 ? GetArgFloat(freshCmds[0], CrateBounceArgIndex) : null;
        }
        catch { return null; }
    }

    private void ResetCrateBounceForId(ChunkSource chunkSource, BaseTwinSection? behSec, uint id)
    {
        var orig = LoadCrateBounceOriginal(id);
        if (orig is null) { _browser.Log("Reset Crate Landing Bounce failed: couldn't find the original value."); return; }

        var cmds = FindApplyVelocityCommands(behSec?.GetItem<PS2BehaviourGraph>(id));
        if (cmds.Count == 0) { _browser.Log("Reset Crate Landing Bounce failed: live command not found."); return; }

        SetArgFloat(cmds[0], CrateBounceArgIndex, orig.Value);
        _crateBounceOriginals[id] = orig.Value;
        chunkSource.GlobalRm2Dirty = true;
        _browser.Log($"Reset {(CrateLandedOnNames.TryGetValue(id, out var n) ? n : $"0x{id:X}")} Landing Bounce to its original value (in-memory). Save Chunk to persist.");
    }

    private const uint TntCountdownBehaviourId = 0x3F;
    private const int  ControlPacketDelayIndex = (int)TwinBehaviourControlPacket.ControlPacketData.Delay;

    private static float? GetDelay(TwinBehaviourControlPacket packet)
    {
        if (packet.Bytes.Count <= ControlPacketDelayIndex) return null;
        var floatIdx = packet.Bytes[ControlPacketDelayIndex];
        if (floatIdx == 0xFF || floatIdx >= 0x80 || floatIdx >= packet.Floats.Count) return null;
        return BitConverter.UInt32BitsToSingle(packet.Floats[floatIdx]);
    }

    private static void SetDelay(TwinBehaviourControlPacket packet, float value)
    {
        if (packet.Bytes.Count <= ControlPacketDelayIndex) return;
        var floatIdx = packet.Bytes[ControlPacketDelayIndex];
        if (floatIdx == 0xFF || floatIdx >= 0x80 || floatIdx >= packet.Floats.Count) return;
        packet.Floats[floatIdx] = BitConverter.SingleToUInt32Bits(value);
    }

    private static List<TwinBehaviourControlPacket> FindTntDelayPackets(PS2BehaviourGraph graph)
    {
        var found = new List<TwinBehaviourControlPacket>();
        foreach (var state in graph.ScriptStates)
            if (state.ControlPacket is { } cp && GetDelay(cp) is not null)
                found.Add(cp);
        return found;
    }

    private List<float>? _tntOriginalDelays;
    private float _tntSpeedMultiplier = 1f;
    private bool _tntMultiplierActive;

    private void DrawTntCountdownSpeed(Entity e, ChunkSource chunkSource, Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject obj) // Amedo
    {
        var tntGraph = ObjectGraph(chunkSource, obj, TntCountdownBehaviourId, out bool own);
        if (tntGraph is null)
        {
            ImGui.TextDisabled("Couldn't find the TNT countdown behaviour (0x3F) in Startup\\Default.rm2.");
            return;
        }

        var packets = FindTntDelayPackets(tntGraph);
        if (packets.Count == 0)
        {
            ImGui.TextDisabled("No Delay-bearing ControlPackets found in COM_TNT_CRATE_COUNTDOWN.");
            return;
        }

        if (_tntOriginalDelays is null || _tntOriginalDelays.Count != packets.Count)
        {
            _tntOriginalDelays = LoadTntOriginalDelays(packets.Count) ?? packets.Select(p => GetDelay(p) ?? 0f).ToList();
        }

        if (!_tntMultiplierActive && _tntOriginalDelays.Count > 0 &&
            GetDelay(packets[0]) is { } liveDelay0 && liveDelay0 != 0f)
            _tntSpeedMultiplier = _tntOriginalDelays[0] / liveDelay0;

        ImGui.Text("TNT Countdown Speed");
        if (DrawScriptScope(e, own, "every TNT crate")) return; // Amedo
        bool changed = ImGui.SliderFloat("Speed Multiplier##tntspeed", ref _tntSpeedMultiplier, 0.1f, 5f, "%.2fx");
        _tntMultiplierActive = ImGui.IsItemActive();
        if (changed)
        {
            for (int i = 0; i < packets.Count; i++)
                SetDelay(packets[i], _tntOriginalDelays[i] / _tntSpeedMultiplier);
            if (!own) chunkSource.GlobalRm2Dirty = true; // Amedo
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("Scales every ControlPacket Delay in the TNT fuse's beep-beep-beep\n" +
                         "countdown script (Startup\\Default.rm2, COM_TNT_CRATE_COUNTDOWN),\n" +
                         "relative to the TRUE original values (read once from the pristine\n" +
                         "disc, not whatever's currently live) — drag freely, no compounding.\n" +
                         "1.00x = original speed. Save Chunk to persist, then Build ISO + test.");
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset##tntreset"))
        {
            _tntSpeedMultiplier = 1f;
            ResetTntCountdownToOriginal(chunkSource, tntGraph);
        }

        if (ImGui.TreeNode("Individual delays (advanced)##tntdelays"))
        {
            for (int i = 0; i < packets.Count; i++)
            {
                float d = GetDelay(packets[i]) ?? 0f;
                if (ImGui.InputFloat($"Delay {i}##tntdelay{i}", ref d, 0.01f, 0.1f))
                    SetDelay(packets[i], MathF.Max(0f, d));
            }
            ImGui.TreePop();
        }
    }

    private List<float>? LoadTntOriginalDelays(int expectedCount)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                ?? throw new FileNotFoundException("Couldn't reopen Startup\\Default.rm2 from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshDefault = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
            freshDefault.Read(reader, (int)stream.Length);

            var freshBehSec = freshDefault.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshGraph = freshBehSec?.GetItem<PS2BehaviourGraph>(TntCountdownBehaviourId);
            if (freshGraph is null) return null;

            var delays = FindTntDelayPackets(freshGraph).Select(p => GetDelay(p) ?? 0f).ToList();
            return delays.Count == expectedCount ? delays : null;
        }
        catch { return null; }
    }

    private void ResetTntCountdownToOriginal(ChunkSource chunkSource, PS2BehaviourGraph liveGraph)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                ?? throw new FileNotFoundException("Couldn't reopen Startup\\Default.rm2 from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshDefault = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
            freshDefault.Read(reader, (int)stream.Length);

            var freshBehSec = freshDefault.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshGraph = freshBehSec?.GetItem<PS2BehaviourGraph>(TntCountdownBehaviourId);
            if (freshGraph is null)
            {
                _browser.Log("Reset TNT Countdown Speed: couldn't find the original behaviour on disc.");
                return;
            }

            var liveStates = liveGraph.ScriptStates;
            var freshStates = freshGraph.ScriptStates;
            for (int i = 0; i < liveStates.Count && i < freshStates.Count; i++)
            {
                if (liveStates[i].ControlPacket is not { } liveCp) continue;
                if (freshStates[i].ControlPacket is not { } freshCp) continue;
                if (GetDelay(freshCp) is { } origDelay)
                    SetDelay(liveCp, origDelay);
            }
            chunkSource.GlobalRm2Dirty = true;
            _browser.Log("Reset TNT Countdown Speed to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset TNT Countdown Speed failed: {ex.Message}"); }
    }

    private const uint NitroCrateBehaviourId       = 0x37;
    private const int  ApplyBlastRadiusCommandIndex = 117;

    private static PS2BehaviourCommand? FindBlastRadiusCommand(PS2BehaviourGraph graph)
    {
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == ApplyBlastRadiusCommandIndex && cmd.Arguments.Count > 0)
                        return (PS2BehaviourCommand)cmd;
        return null;
    }

    private float? _nitroOriginalBlastRadius;
    private float  _nitroBlastRadiusMultiplier = 1f;
    private bool   _nitroBlastRadiusActive;

    private void DrawNitroNoiseRadius(Entity e, ChunkSource chunkSource, Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject obj) // Amedo
    {
        var nitroGraph = ObjectGraph(chunkSource, obj, NitroCrateBehaviourId, out bool own);
        if (nitroGraph is null)
        {
            ImGui.TextDisabled("Couldn't find COM_NITRO_CRATE_DEFAULT (0x37) in Startup\\Default.rm2.");
            return;
        }

        var cmd = FindBlastRadiusCommand(nitroGraph);
        if (cmd is null)
        {
            ImGui.TextDisabled("No ApplyBlastRadius (117) command found in COM_NITRO_CRATE_DEFAULT.");
            return;
        }

        _nitroOriginalBlastRadius ??= LoadNitroOriginalBlastRadius() ?? BitConverter.UInt32BitsToSingle(cmd.Arguments[0]);

        if (!_nitroBlastRadiusActive && _nitroOriginalBlastRadius is { } orig0 && orig0 != 0f)
        {
            float liveVal = BitConverter.UInt32BitsToSingle(cmd.Arguments[0]);
            _nitroBlastRadiusMultiplier = liveVal / orig0;
        }

        ImGui.Text("Nitro Hop Noise Radius"); // Amedo
        if (DrawScriptScope(e, own, "every Nitro crate")) return;
        bool changed = ImGui.SliderFloat("Radius Multiplier##nitroblast", ref _nitroBlastRadiusMultiplier, 0.1f, 5f, "%.2fx");
        _nitroBlastRadiusActive = ImGui.IsItemActive();
        if (changed)
        {
            cmd.Arguments[0] = BitConverter.SingleToUInt32Bits((_nitroOriginalBlastRadius ?? 10f) * _nitroBlastRadiusMultiplier);
            if (!own) chunkSource.GlobalRm2Dirty = true; // Amedo
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("Scales command 117 (MakeNoise, 0x2549E0) in the idle hop of\n" + // Amedo
                         "COM_NITRO_CRATE_DEFAULT (Startup\\Default.rm2): the radius of the\n" +
                         "noise each hop makes. Agents inside it hear the crate (look at it /\n" +
                         "get their noise message). It does NOT damage anything; the kill\n" +
                         "radius is 'Crate Explosion Radius' below.\n" +
                         "Shared script = every Nitro Crate; own script = this object only.\n" +
                         "1.00x = original\n" +
                         "(10.0 units). Save Chunk to persist.");
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset##nitroblastreset"))
        {
            _nitroBlastRadiusMultiplier = 1f;
            cmd.Arguments[0] = BitConverter.SingleToUInt32Bits(_nitroOriginalBlastRadius ?? 10f);
            if (!own) chunkSource.GlobalRm2Dirty = true; // Amedo
        }

        float rawVal = BitConverter.UInt32BitsToSingle(cmd.Arguments[0]);
        ImGui.TextDisabled($"Current noise radius: {rawVal:F2} world units"); // Amedo
        DrawSelectedSphereRings(rawVal, new System.Numerics.Vector4(0.35f, 0.7f, 1f, 0.6f), 1.5f); // Amedo
    }

    // Amedo 2026-09-21
    private void DrawSelectedSphereRings(float radius, System.Numerics.Vector4 color, float thickness)
    {
        if (_selected is null || _camera is null || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup)) return;
        var center = _selected.Transform.World.Translation;
        int sw = ViewW, sh = ViewH; // Amedo
        var dl = ImGui.GetForegroundDrawList();
        uint ringColor = ImGui.ColorConvertFloat4ToU32(color);
        foreach (var axis in new[] { System.Numerics.Vector3.UnitX, System.Numerics.Vector3.UnitY, System.Numerics.Vector3.UnitZ })
        {
            var pts = RingPoints(center, axis, radius);
            System.Numerics.Vector2? prev = null;
            for (int i = 0; i <= pts.Length; i++)
            {
                if (!WorldToScreen(pts[i % pts.Length], sw, sh, out var sp)) { prev = null; continue; }
                if (prev is { } pv) dl.AddLine(pv, sp, ringColor, thickness);
                prev = sp;
            }
        }
    }

    // Amedo
    private List<float>? _crateExplodeOriginalRadii;
    private float _crateExplodeMultiplier = 1f;
    private bool  _crateExplodeActive;

    // Amedo
    private static bool IsLiteralFloatWord(uint w) => (w & 1) == 0 && ((w >> 1) & 3) == 2;
    private static float LiteralFloat(uint w) => BitConverter.Int32BitsToSingle((int)(w & ~7u));
    private static uint ToLiteralFloat(float f) => (BitConverter.SingleToUInt32Bits(f) & ~7u) | 4u;

    // Amedo
    private static List<PS2BehaviourCommand> FindCrateDamageCommands(PS2BehaviourGraph graph)
    {
        var list = new List<PS2BehaviourCommand>();
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex is 514 or 546 or 563 && cmd.Arguments.Count > 10 && IsLiteralFloatWord(cmd.Arguments[10]))
                        list.Add((PS2BehaviourCommand)cmd);
        return list;
    }

    // Amedo
    private void DrawCrateExplosionRadius(Entity e, ChunkSource chunkSource, Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject obj)
    {
        var graph = ObjectGraph(chunkSource, obj, CrateExplodeBehaviourId, out bool own);
        if (graph is null)
        {
            ImGui.TextDisabled("Couldn't find COM_GENERIC_CRATE_EXPLODE (47) in Startup\\Default.rm2.");
            return;
        }
        var cmds = FindCrateDamageCommands(graph);
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("No CreateDamage (514) command found in COM_GENERIC_CRATE_EXPLODE.");
            return;
        }

        if (_crateExplodeOriginalRadii is null || _crateExplodeOriginalRadii.Count != cmds.Count)
        {
            var orig = LoadCrateExplodeOriginalRadii();
            _crateExplodeOriginalRadii = orig is not null && orig.Count == cmds.Count
                ? orig
                : cmds.Select(c => LiteralFloat(c.Arguments[10])).ToList();
        }
        var origRadii = _crateExplodeOriginalRadii;

        int main = 0;
        for (int i = 1; i < origRadii.Count; i++) if (origRadii[i] > origRadii[main]) main = i;
        if (!_crateExplodeActive && origRadii[main] != 0f)
            _crateExplodeMultiplier = LiteralFloat(cmds[main].Arguments[10]) / origRadii[main];

        ImGui.Text("Crate Explosion Radius");
        if (DrawScriptScope(e, own, "every Nitro and TNT crate")) return;
        bool changed = ImGui.SliderFloat("Radius Multiplier##crateexplode", ref _crateExplodeMultiplier, 0.1f, 5f, "%.2fx");
        _crateExplodeActive = ImGui.IsItemActive();
        if (changed)
        {
            for (int i = 0; i < cmds.Count; i++)
                cmds[i].Arguments[10] = ToLiteralFloat(origRadii[i] * _crateExplodeMultiplier);
            if (!own) chunkSource.GlobalRm2Dirty = true;
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("Scales every CreateDamage (514) radius inside\n" +
                         "COM_GENERIC_CRATE_EXPLODE (47, Startup\\Default.rm2): the real\n" +
                         "area that kills Crash / breaks things when a crate explodes.\n" +
                         "Shared script = every Nitro and TNT crate; own script = this object only.\n" +
                         "1.00x = original.\n" +
                         "Save Chunk to persist.");
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset##crateexplodereset"))
        {
            _crateExplodeMultiplier = 1f;
            for (int i = 0; i < cmds.Count; i++) cmds[i].Arguments[10] = ToLiteralFloat(origRadii[i]);
            if (!own) chunkSource.GlobalRm2Dirty = true;
        }

        float mainVal = LiteralFloat(cmds[main].Arguments[10]);
        ImGui.TextDisabled($"Kill radius: {mainVal:F2} world units (original {origRadii[main]:F2})");
        DrawSelectedSphereRings(mainVal, new System.Numerics.Vector4(1f, 0.25f, 0.1f, 0.95f), 2.5f);
    }

    // Amedo
    private bool DrawScriptScope(Entity e, bool own, string sharedBy)
    {
        if (own)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f), "Applies to: this object only (own script)");
            return false;
        }
        ImGui.TextColored(new System.Numerics.Vector4(1f, 0.75f, 0.3f, 1f), $"Applies to: {sharedBy} (shared script)");
        ImGui.SameLine();
        if (!ImGui.SmallButton("Only this object##ownscriptscope")) return false;
        return MakeSelectionOwnScripts(e);
    }

    // Amedo
    private List<float>? LoadCrateExplodeOriginalRadii()
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                ?? throw new FileNotFoundException("Couldn't reopen Startup\\Default.rm2 from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshDefault = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
            freshDefault.Read(reader, (int)stream.Length);

            var freshGraph = freshDefault.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION)
                ?.GetItem<PS2BehaviourGraph>(CrateExplodeBehaviourId);
            return freshGraph is null ? null : FindCrateDamageCommands(freshGraph).Select(c => LiteralFloat(c.Arguments[10])).ToList();
        }
        catch { return null; }
    }

    private float? LoadNitroOriginalBlastRadius()
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                ?? throw new FileNotFoundException("Couldn't reopen Startup\\Default.rm2 from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshDefault = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
            freshDefault.Read(reader, (int)stream.Length);

            var freshBehSec = freshDefault.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshGraph = freshBehSec?.GetItem<PS2BehaviourGraph>(NitroCrateBehaviourId);
            if (freshGraph is null) return null;

            var freshCmd = FindBlastRadiusCommand(freshGraph);
            return freshCmd is null ? null : BitConverter.UInt32BitsToSingle(freshCmd.Arguments[0]);
        }
        catch { return null; }
    }
}
