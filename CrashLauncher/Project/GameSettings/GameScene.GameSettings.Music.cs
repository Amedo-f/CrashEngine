using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene
{

    private const uint DjDefaultBehaviourId = 0x4CF;

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


        ImGui.BeginDisabled(_musicReplaceInProgress);
        if (ImGui.Button("Import From External WAV/MP3...##musicReplace", new Vector2(-1f, 0f)))
            ReplaceMusicTrackAudio(currentId, currentLabel, applyNewId);
        ImGui.EndDisabled();
        if (_musicReplaceInProgress)
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f),
                "Working — reading/writing the ~200MB MUSIC.MB archive, this can take several seconds...");

        ImGui.BeginDisabled(_musicReplaceInProgress);
        if (ImGui.Button("Reset to Original##musicReset", new Vector2(-1f, 0f)))
            resetToOriginal();
        ImGui.EndDisabled();

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
        }

        ImGui.SetNextItemWidth(120f);
        ImGui.InputTextWithHint("##musicIdRaw", "raw id", ref _musicIdTextInput, 8, ImGuiInputTextFlags.CharsDecimal);
        ImGui.SameLine();
        if (ImGui.Button("Apply id##musicApplyRaw") && uint.TryParse(_musicIdTextInput, out var rawId))
            applyNewId(rawId);
    }

    private void ResetDjMusicParamToOriginal(InstanceData data)
    {
        try
        {
            var freshRm2 = ReadPristineRm2(_rm2);

            var original = FindLayoutInstance(freshRm2, c => c.GetID() == data.Source.GetID() && c.ObjectId == data.ObjectId);
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
}
