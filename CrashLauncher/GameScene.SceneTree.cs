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
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawEntityNode(Entity e)
    {
        if (e.Name is "__Gizmo" or "Meter" or "Player") return;

        bool isSel    = _selectedSet.Contains(e);
        bool isSystem = e.Name is "RenderPipeline" or "Camera" or "Grid";

        string hint = e switch
        {
            _ when e.Has<StealthEnemy>()                              => " [npc]",
            _ when e.Has<DirectCubeRenderer>()                        => " [cube]",
            _ when e.Has<CrashEngine.Importer.AnimatedObject>()      => " [anim]",
            _ when e.Has<CrashEngine.Importer.MeshRenderer>()        => " [mesh]",
            _ when e.Has<CrashEngine.Renderer.MeshRenderer>()        => " [mesh]",
            _ when e.Has<InstanceData>()                              => " [inst]",
            _ when e.Has<CrashEngine.Importer.CollisionMesh>()        => " [collision]",
            _ when e.Has<CameraComponent>()                           => " [cam]",
            _ when e.Has<GridRenderer>()                              => " [grid]",
            _ when e.Has<RenderPipeline>()                            => " [rp]",
            _ => e.Components.Count > 0 ? " [•]" : "",
        };

        bool hasChildren = e.Children.Count > 0;
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (isSel)        flags |= ImGuiTreeNodeFlags.Selected;

        _treeVisibleOrder.Add(e);

        ImGui.PushID(RuntimeHelpers.GetHashCode(e));

        if (_revealSelectionInTree && hasChildren && _treeRevealAncestors.Contains(e))
            ImGui.SetNextItemOpen(true);

        if (isSystem) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1f));
        bool open = ImGui.TreeNodeEx(e.Name + hint, flags);
        if (isSystem) ImGui.PopStyleColor();

        if (_revealSelectionInTree && isSel)
            ImGui.SetScrollHereY(0.5f);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            bool shift = Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight);
            bool ctrl  = Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight);
            if (shift && ctrl && _rangeSelectAnchor is not null)
            {
                SelectRange(_rangeSelectAnchor, e);
            }
            else
            {
                SelectClicked(e, shift);
                _rangeSelectAnchor = e;
            }
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            _camera?.FocusOn(e.Transform.World.Translation);

        if (e.Get<SceneryTile>() is { } ctxTile && ImGui.BeginPopupContextItem())
        {
            if (ImGui.IsWindowAppearing())
                _editMeshIdBuf = ctxTile.SourceId.ToString("X8");

            ImGui.TextDisabled(ctxTile.IsLod ? "LOD group -- edits its Meshes[0] entry" : "Mesh ID");
            ImGui.SetNextItemWidth(140f);
            ImGui.InputText("##editmeshid", ref _editMeshIdBuf, 8, ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase);
            ImGui.SameLine();
            if (ImGui.Button("Apply##editmeshid"))
            {
                if (uint.TryParse(_editMeshIdBuf, System.Globalization.NumberStyles.HexNumber, null, out var newId))
                    ApplyManualMeshIdChange(e, ctxTile, newId);
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (open && hasChildren)
        {
            foreach (var child in e.Children)
                DrawEntityNode(child);
            ImGui.TreePop();
        }

        ImGui.PopID();
    }
}
