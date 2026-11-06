using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;

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
