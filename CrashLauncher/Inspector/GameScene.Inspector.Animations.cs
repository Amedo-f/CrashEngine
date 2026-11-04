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
    private void DrawInspectorAnimations(Entity e, CrashEngine.Importer.AnimatedObject? anim, InstanceData? inst)
    {
        if (anim is not null && anim.Groups.Count > 0)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Animations##anim", ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var group in anim.Groups)
                {
                    bool isCurrent = anim.CurrentGroup == group;
                    bool altState  = anim.DefaultGroup is not null && group.MeshRoot != anim.DefaultGroup.MeshRoot;
                    ImGui.PushID(group.SlotIndex);
                    if (isCurrent) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 1f, 0.6f, 1f));
                    var label = $"Slot {group.SlotIndex}  —  Anim {group.AnimId:X4}  ({group.Anim.TotalFrames} frames)"
                              + (altState ? "  [alt state]" : "");
                    if (ImGui.Selectable(label, isCurrent))
                        anim.Play(group);
                    if (isCurrent) ImGui.PopStyleColor();

                    ImGui.Indent();
                    if (ImGui.SmallButton("Swap animation##swapbtn")) ImGui.OpenPopup("##swapAnim");
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Replace this slot's animation clip. Slots are called by\n" +
                                          "NUMBER from scripts, not by animation id — this only changes\n" +
                                          "WHICH clip plays for this slot, so scripts keep working.\n" +
                                          "Changes the shared GameObject definition — every instance of\n" +
                                          "this object in the level gets it, not just this one. Not\n" +
                                          "undoable.");
                    ImGui.Unindent();

                    if (ImGui.BeginPopup("##swapAnim"))
                    {
                        var swapChunkRoot2 = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                        var animTables     = swapChunkRoot2?.Get<ChunkSource>()?.MeshTables;
                        if (animTables is null)
                        {
                            ImGui.TextDisabled("(chunk not loaded)");
                        }
                        else
                        {
                            ImGui.SetNextItemWidth(220f);
                            ImGui.InputTextWithHint("##animSwapFilter", "Search id...", ref _animSwapFilter, 64);
                            ImGui.BeginChild("##animSwapList", new Vector2(240f, 180f), ImGuiChildFlags.Border);
                            int myJoints = group.Anim.MainAnimation.JointSettings.Count;
                            foreach (var (animId, frames, joints) in MeshDecoder.GetAnimationCatalog(animTables))
                            {
                                if (!string.IsNullOrWhiteSpace(_animSwapFilter) &&
                                    !$"{animId:X4}".Contains(_animSwapFilter, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                bool mismatch = joints != myJoints;
                                var itemLabel = $"Anim {animId:X4}  ({frames}f, {joints}j)" + (mismatch ? "  !" : "");
                                if (mismatch) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.2f, 1f));
                                bool picked = ImGui.Selectable($"{itemLabel}##pick{animId:X4}", animId == group.AnimId);
                                if (mismatch) ImGui.PopStyleColor();
                                if (mismatch && ImGui.IsItemHovered())
                                    MaybeTooltip($"Joint count differs from this slot's current clip\n" +
                                                      $"({joints} vs {myJoints}) — may look wrong or not match\n" +
                                                      "the object's actual skeleton. Not tested against a real\n" +
                                                      "PCSX2 run — verify in-game before trusting this.");
                                if (picked)
                                {
                                    SwapAnimationSlot(e, inst, group.SlotIndex, animId);
                                    ImGui.CloseCurrentPopup();
                                }
                            }
                            ImGui.EndChild();
                        }
                        ImGui.EndPopup();
                    }
                    ImGui.PopID();
                }

                if (anim.Current is not null)
                {
                    ImGui.Separator();
                    if (ImGui.Button(anim.Playing ? "Pause##anim" : "Play##anim")) anim.Playing = !anim.Playing;
                    ImGui.SameLine();
                    bool loop = anim.Loop;
                    if (ImGui.Checkbox("Loop##anim", ref loop)) anim.Loop = loop;
                    ImGui.SameLine();
                    if (ImGui.Button("Stop##anim")) anim.Stop();

                    if (anim.Current is not null)
                    {
                        int lastFrame = anim.Current.MainAnimation.AnimatedTransformations.Count - 1;
                        ImGui.Text($"Frame {anim.Frame:F1} / {MathF.Max(0, lastFrame)}");
                        float f = anim.Frame;
                        ImGui.SetNextItemWidth(-1f);
                        if (ImGui.SliderFloat("##animtimeline", ref f, 0f, MathF.Max(0, lastFrame)))
                        {
                            anim.Frame   = f;
                            anim.Playing = false;
                        }
                    }
                }
            }
        }
    }

    private void DrawInspectorDynamicSceneryAnimation(Entity e, CrashEngine.Importer.DynamicSceneryAnimator? dynAnim)
    {
        if (dynAnim is not null && dynAnim.FrameCount > 1)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Dynamic Scenery Animation##dynanim", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.Button(dynAnim.Playing ? "Pause##dynanim" : "Play##dynanim")) dynAnim.Playing = !dynAnim.Playing;
                ImGui.SameLine();
                ImGui.TextDisabled($"{dynAnim.FrameCount} frames @ {CrashEngine.Importer.DynamicSceneryAnimator.Fps:0} fps, loop");
                float df = dynAnim.Frame;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("##dynanimtimeline", ref df, 0f, dynAnim.FrameCount, "Frame %.2f"))
                {
                    dynAnim.Frame   = df;
                    dynAnim.Playing = false;
                }
            }
        }
    }

    private void SwapAnimationSlot(Entity e, InstanceData? data, int slotIndex, uint newAnimId)
    {
        if (data is null) return;
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.MeshTables is null) return;

        if (!MeshDecoder.SetAnimationSlot(chunkSource.MeshTables, data.ObjectId, slotIndex, newAnimId))
        {
            _browser.Log($"Swap Animation: slot {slotIndex} not found on ObjectId 0x{data.ObjectId:X4}.");
            return;
        }
        MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, chunkSource.MeshTables, e);
        _browser.Log($"Swapped Slot {slotIndex} -> Anim {newAnimId:X4} on ObjectId 0x{data.ObjectId:X4} " +
                      "(every instance of this object in the level shares this — not undoable). " +
                      "Save Chunk + Build ISO, then test in an emulator.");
    }
}
