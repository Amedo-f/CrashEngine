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
}
