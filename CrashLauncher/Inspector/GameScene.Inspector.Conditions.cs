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
    private void DrawInspectorConditions(Entity e, InstanceData inst)
    {
        ImGui.Separator();
        if (ImGui.CollapsingHeader("Conditions##cond", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.IsItemHovered())
                MaybeTooltip("Other Triggers/Instances in this level that must fire before\n" +
                             "this one activates (they message it once their own condition\n" +
                             "is met). Empty = no dependency -- this object runs its own\n" +
                             "default behaviour on its own (e.g. opens on proximity).");
            uint selfInstId = inst.Source.GetID();
            var condSources = GetConditionSources(selfInstId, e);
            if (condSources.Count == 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f),
                    "No condition set — activates on its own, unconditionally.");
            }
            else
            {
                for (int ci = 0; ci < condSources.Count; ci++)
                {
                    var (srcEnt, kind, srcId) = condSources[ci];
                    ImGui.PushID(ci);
                    if (ImGui.Selectable($"[{kind}] {srcEnt.Name}  (0x{srcId:X4})", false,
                            ImGuiSelectableFlags.AllowOverlap))
                    { _selected = srcEnt; _revealSelectionInTree = true; }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Remove##condrm"))
                    {
                        if (kind == "Instance") srcEnt.Get<InstanceData>()?.Source.Instances.Remove((ushort)selfInstId);
                        else srcEnt.Get<CrashEngine.Importer.TriggerMarker>()?.Source.Trigger.Instances.Remove((ushort)selfInstId);
                    }
                    ImGui.PopID();
                }
            }
            if (ImGui.Button("+ Add Condition...##addcond"))
            {
                _conditionPickerTargetEntity = e;
                _conditionPickerFilter = "";
                _showConditionPicker = true;
            }

            if (inst.Source.ParamList3.Count > 2)
            {
                int required = unchecked((int)inst.Source.ParamList3[2]);
                ImGui.SetNextItemWidth(80f);
                if (ImGui.InputInt("Required count##condRequiredCount", ref required))
                    inst.Source.ParamList3[2] = unchecked((uint)Math.Max(0, required));
                if (ImGui.IsItemHovered())
                    MaybeTooltip("How many of the conditions above must fire before this\n" +
                                 "object activates (ParamList3[2]) -- confirmed live: must\n" +
                                 "match the real number exactly, extra linked conditions\n" +
                                 "beyond this count are optional/alternative, not required.");

                if (inst.ObjectId == 0x0000)
                {
                    int startingMasks = Math.Max(0, required - 1);
                    ImGui.SetNextItemWidth(80f);
                    if (ImGui.InputInt("Starting Aku Aku Masks##crashStartMasks", ref startingMasks))
                        inst.Source.ParamList3[2] = unchecked((uint)Math.Max(1, startingMasks + 1));
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Crash-specific meaning of the SAME raw field above\n" +
                                     "(ParamList3[2] = this value + 1) -- confirmed live by\n" +
                                     "direct testing: 0 = no masks, dies in one hit; 1 = starts\n" +
                                     "with 1 Aku Aku mask; etc.");
                }
            }
        }
    }
}
