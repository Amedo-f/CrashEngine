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
    private void DrawInspector(Entity e)
    {
        DrawInspectorHeader(e); // Amedo
        DrawReceiveShadows(e); // Amedo
        DrawShadowShape(e); // Amedo

        DrawInspectorAddToAssets(e);

        DrawInspectorTransform(e);

        DrawInspectorWorldLighting(e);

        DrawInspectorChunkTransform(e);

        DrawCollisionSection(e);
        DrawArraySection(e);

        var mr = e.Get<CrashEngine.Importer.MeshRenderer>();
        DrawInspectorMeshRenderer(e, mr);

        DrawInspectorRemoveImportedModel(e);

        var rmr = e.Get<CrashEngine.Renderer.MeshRenderer>();
        DrawInspectorRendererMesh(e, rmr);

        // Amedo 2026-09-19
        var dcr = e.Get<DirectCubeRenderer>();
        DrawInspectorCube(e, dcr);

        var rp = e.Get<RenderPipeline>();
        DrawInspectorRenderPipeline(e, rp);

        var cam = e.Get<CameraComponent>();
        DrawInspectorCamera(e, cam);

        DrawInspectorLinkGroup(e);

        var linkedScenery = e.Get<CrashEngine.Importer.LinkedSceneryLink>();
        DrawInspectorLinkedScenery(e, linkedScenery);

        DrawInspectorSkydome(e);

        var loadWall = e.Get<CrashEngine.Importer.LoadWallMarker>();
        DrawInspectorLoadWall(e, loadWall);

        DrawInspectorPreLoadBox(e);

        DrawInspectorCameraData(e);

        DrawInspectorPosition(e);

        DrawInspectorAiPosition(e);

        DrawInspectorSceneryLight(e);

        DrawInspectorTrigger(e);

        var se = e.Get<StealthEnemy>();
        DrawInspectorStealthEnemy(e, se);

        var inst = e.Get<InstanceData>();
        DrawInspectorInstance(e, inst);

        var anim = e.Get<CrashEngine.Importer.AnimatedObject>();
        DrawInspectorAnimations(e, anim, inst);

        DrawInspectorAgentLabState(e);

        var dynAnim = e.Get<CrashEngine.Importer.DynamicSceneryAnimator>(); // Amedo 2026-09-26
        DrawInspectorDynamicSceneryAnimation(e, dynAnim);

        bool hasSpecific = mr is not null || rmr is not null || dcr is not null || rp is not null
                        || cam is not null || se is not null || inst is not null || anim is not null
                        || linkedScenery is not null || loadWall is not null || dynAnim is not null; // Amedo 2026-09-26
        if (!hasSpecific && e.Components.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled("Components:");
            foreach (var c in e.Components)
                ImGui.TextDisabled($"  {c.GetType().Name}");
        }
    }
}
