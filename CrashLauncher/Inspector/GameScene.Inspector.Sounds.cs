using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawInspectorObjectSounds(PS2AnyObject? thisObj, ChunkSource? scriptChunkSrc)
    {
        if (thisObj is not null && (thisObj.RefSounds.Count > 0 || thisObj.SoundSlots.Any(s => s != 0xFFFF)))
        {
            if (ImGui.CollapsingHeader("Sounds##soundshdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Amedo 2026-09-21
                float previewVolPct = _previewVolume * 100f;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("Preview Volume##previewvol", ref previewVolPct, 0f, 100f, "%.0f%%"))
                    _previewVolume = Math.Clamp(previewVolPct / 100f, 0f, 1f);

                var codeSecLive = scriptChunkSrc?.Rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION);
                var soundIds = thisObj.RefSounds.Concat(thisObj.SoundSlots)
                    .Where(sid => sid != 0xFFFF).Distinct().ToList();
                if (soundIds.Count == 0)
                {
                    ImGui.TextDisabled("Referenced slot(s) are all empty (0xFFFF) — nothing to show.");
                }
                foreach (var sid in soundIds)
                {
                    var (snd, sectionId, label) = FindSoundAnySection(codeSecLive, sid);
                    ImGui.PushID((int)sid);
                    if (snd is null)
                    {
                        ImGui.TextDisabled($"Sound id 0x{sid:X} — not found in this level's own sound section (checked SFX + all language/dialogue sections).");
                    }
                    else
                    {
                        double durationSeconds = snd.Sound.Length / 16.0 * 28.0 / snd.GetFreq();
                        string tag = label == "SFX" ? "" : $" [{label} dialogue]";
                        ImGui.Text($"Sound 0x{sid:X} (ID {sid}){tag} — {snd.GetFreq()}Hz, {FormatMinSec(durationSeconds)}");
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Play##sndplay")) PlaySoundEffect(snd);
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Replace...##sndreplace")) ReplaceSoundEffectAudio(snd, sid, _rm2, sectionId);
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Reset Original##sndreset")) ResetSoundEffectToOriginal(snd, sid, _rm2, sectionId);

                        float gain = GetPersistedSoundEffectGain(snd, sid, _rm2, sectionId);
                        ImGui.SetNextItemWidth(150f);
                        if (ImGui.SliderFloat("Volume (gain)##sndgain", ref gain, 0.1f, 3f, "%.2fx"))
                            ApplySoundEffectGain(snd, sid, gain, _rm2, sectionId);
                    }
                    ImGui.PopID();
                }
                if (_musicLastError is not null)
                    ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), $"Error: {_musicLastError}");
            }
        }
    }
}
