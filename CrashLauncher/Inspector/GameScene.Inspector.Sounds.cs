using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnySound = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnySound;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PointLight = Twinsanity.TwinsanityInterchange.Common.Lights.PointLight;
using NegativeLight = Twinsanity.TwinsanityInterchange.Common.Lights.NegativeLight;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using ITwinCamera = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.ITwinCamera;

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
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Editor-only listening level for the Play buttons here and Music\n" +
                                      "Preview — scales the previewed audio down before playback so it's\n" +
                                      "comfortable on top of your system volume (does NOT change the\n" +
                                      "sound's real in-game Volume/gain below). Lower it if previews are\n" +
                                      "too loud.");

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
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("Digital gain applied to this sound's own waveform before re-encoding — " +
                                         "always computed from the ORIGINAL audio (cached the first time you touch " +
                                         "this slider this session), never compounds. Save Chunk to persist.");
                    }
                    ImGui.PopID();
                }
                if (_musicLastError is not null)
                    ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), $"Error: {_musicLastError}");
            }
        }
    }
}
