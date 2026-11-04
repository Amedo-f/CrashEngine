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
    private void DrawInspectorPosition(Entity e)
    {
        var posMarker = e.Get<CrashEngine.Importer.PositionMarker>();
        if (posMarker is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Position Data##pos", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("XYZ: see Transform above.");
                ImGui.SetNextItemWidth(-1f);
                float w = posMarker.Source.Position.W;
                if (ImGui.DragFloat("W (unknown)##posData", ref w, 0.05f)) { var pp = posMarker.Source.Position; pp.W = w; posMarker.Source.Position = pp; } // Amedo
            }
        }
    }

    private void DrawInspectorAiPosition(Entity e)
    {
        var aiPosMarker = e.Get<CrashEngine.Importer.AiPositionMarker>();
        if (aiPosMarker is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("AI Position Data##aipos", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("XYZ: see Transform above.");
                ImGui.SetNextItemWidth(-1f);
                float w = aiPosMarker.Source.Position.W;
                if (ImGui.DragFloat("W (unknown)##aiPosData", ref w, 0.05f)) { var ap = aiPosMarker.Source.Position; ap.W = w; aiPosMarker.Source.Position = ap; } // Amedo
                ImGui.SetNextItemWidth(-1f);
                int unkShort = aiPosMarker.Source.UnkShort;
                if (ImGui.DragInt("Unk Short##aiPosData", ref unkShort, 1f, 0, ushort.MaxValue))
                    aiPosMarker.Source.UnkShort = (ushort)unkShort;
            }
        }
    }

    private void DrawInspectorSceneryLight(Entity e)
    {
        // Amedo 2026-09-19
        var lightMarker = e.Get<CrashEngine.Importer.SceneryLightMarker>();
        if (lightMarker is not null && lightMarker.Source is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader((lightMarker.IsNegative ? "Negative Light Data##lightdata" : "Point Light Data##lightdata"), ImGuiTreeNodeFlags.DefaultOpen))
            {
                var lt = lightMarker.Source;
                ImGui.TextDisabled("Position: drag the gizmo above to move (saved on Save Chunk).");
                var col = new Vector3(lt.Color.X, lt.Color.Y, lt.Color.Z);
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.ColorEdit3("Color##lmc", ref col)) { lt.Color.X = col.X; lt.Color.Y = col.Y; lt.Color.Z = col.Z; }
                float r = lt.Radius;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.DragFloat("Radius##lmr", ref r, 0.05f, 0f, float.MaxValue)) lt.Radius = r;
                if (lightMarker.IsNegative && lt is NegativeLight nlt)
                {
                    ImGui.SetNextItemWidth(-1f);
                    float nf1 = nlt.UnkFloat1;
                    if (ImGui.DragFloat("Unk Float 1##lmn1", ref nf1, 0.05f)) nlt.UnkFloat1 = nf1;
                    ImGui.SetNextItemWidth(-1f);
                    float nf2 = nlt.UnkFloat2;
                    if (ImGui.DragFloat("Unk Float 2##lmn2", ref nf2, 0.05f)) nlt.UnkFloat2 = nf2;
                }
                ImGui.TextDisabled("Affects the real game only (editor preview is ambient+directional).");
            }
        }
    }
}
