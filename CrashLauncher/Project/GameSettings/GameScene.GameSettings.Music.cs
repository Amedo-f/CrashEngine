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
}
