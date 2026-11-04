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
    private string _trigInstanceAddBuf = "";

    private void DrawInspectorTrigger(Entity e)
    {
        var trigMarker = e.Get<CrashEngine.Importer.TriggerMarker>();
        if (trigMarker is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Trigger Settings##trig", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("Position/Rotation/Scale: see Transform above.");

                var twinTrig = trigMarker.Source.Trigger;
                if (ImGui.TreeNode("Activated By##trigActivator"))
                {
                    foreach (var flag in Enum.GetValues<Twinsanity.TwinsanityInterchange.Enumerations.Enums.TriggerActivatorObjects>())
                    {
                        bool on = twinTrig.ObjectActivatorMask.HasFlag(flag);
                        if (ImGui.Checkbox($"{flag}##trigActivator_{flag}", ref on))
                        {
                            twinTrig.ObjectActivatorMask = on
                                ? twinTrig.ObjectActivatorMask | flag
                                : twinTrig.ObjectActivatorMask & ~flag;
                        }
                    }
                    ImGui.TreePop();
                }

                ImGui.SetNextItemWidth(-1f);
                float trigUnkFloat = twinTrig.UnkFloat;
                if (ImGui.DragFloat("Unk Float##trigUnkFloat", ref trigUnkFloat, 0.05f)) twinTrig.UnkFloat = trigUnkFloat;

                if (ImGui.TreeNode($"Linked Instances ({twinTrig.Instances.Count})##trigInstances"))
                {
                    int removeIdx = -1;
                    for (int i = 0; i < twinTrig.Instances.Count; i++)
                    {
                        ImGui.TextDisabled($"0x{twinTrig.Instances[i]:X4}");
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"Remove##trigInstRemove{i}")) removeIdx = i;
                    }
                    if (removeIdx >= 0) twinTrig.Instances.RemoveAt(removeIdx);

                    ImGui.SetNextItemWidth(120f);
                    ImGui.InputText("Instance id (hex)##trigInstAdd", ref _trigInstanceAddBuf, 8);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Add##trigInstAddBtn") &&
                        ushort.TryParse(_trigInstanceAddBuf, System.Globalization.NumberStyles.HexNumber, null, out var newInstId))
                    {
                        twinTrig.Instances.Add(newInstId);
                        _trigInstanceAddBuf = "";
                    }
                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("AgentLab Messages (sent on trigger)##trigMessages"))
                {
                    for (int i = 0; i < trigMarker.Source.TriggerMessages.Length; i++)
                    {
                        ImGui.SetNextItemWidth(120f);
                        int msg = trigMarker.Source.TriggerMessages[i];
                        if (ImGui.DragInt($"Message {i}##trigMsg{i}", ref msg, 1f, 0, ushort.MaxValue))
                            trigMarker.Source.TriggerMessages[i] = (ushort)Math.Clamp(msg, 0, ushort.MaxValue);
                        DrawTriggerMessageTargets(trigMarker.Source, trigMarker.Source.TriggerMessages[i]); // Amedo 2026-09-25
                    }
                    ImGui.TreePop();
                }
            }
        }
    }
}
