using ImGuiNET;
using System.Numerics;

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
        ImGui.SameLine();
        if (ImGui.Checkbox("Infinite##akuinfinite", ref infinite))
            SetAkuAkuInvincibility(proj, infinite ? BuildIsoToPS2.AkuAkuInfinite : BuildIsoToPS2.AkuAkuDefaultSeconds);

        bool touchBreak = proj.AkuAkuTouchBreak;
        if (ImGui.Checkbox("Golden Aku Aku breaks crates and enemies on touch##akutouchbreak", ref touchBreak))
        {
            proj.AkuAkuTouchBreak = touchBreak;
            proj.Save();
        }

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

            int volume = proj.AkuAkuGoldenMusicVolume ?? BuildIsoToPS2.AkuAkuMusicDefaultVolume;
            ImGui.SetNextItemWidth(200f);
            if (ImGui.SliderInt("Golden music volume (%)##akumusicvol", ref volume, 1, 100))
                proj.AkuAkuGoldenMusicVolume = volume == BuildIsoToPS2.AkuAkuMusicDefaultVolume ? null : volume;
            if (ImGui.IsItemDeactivatedAfterEdit()) proj.Save();
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
