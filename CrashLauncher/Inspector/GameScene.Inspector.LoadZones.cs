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
    private void DrawInspectorLoadWall(Entity e, CrashEngine.Importer.LoadWallMarker? loadWall)
    {
        if (loadWall is not null && e.Transform.LocalMatrix is { } wallLm)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Load Wall##loadwall", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled($"Target: {loadWall.Source.Path}");
                if (!IsLinkTargetKeptInNewGame(loadWall.Source.Path))
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0.2f, 1f),
                        "⚠ Not claimed for New Game -- this wall will be DISABLED\n(neutered) in the built ISO, even though it's still live here.");

                var right = new Vector3(wallLm.M11, wallLm.M12, wallLm.M13);
                var up    = new Vector3(wallLm.M21, wallLm.M22, wallLm.M23);
                float width  = right.Length() * 2f;
                float height = up.Length() * 2f;

                ImGui.SetNextItemWidth(-1f);
                bool widthChanged  = ImGui.DragFloat("Width##loadwall",  ref width,  0.1f, 0.1f, 2000f);
                ImGui.SetNextItemWidth(-1f);
                bool heightChanged = ImGui.DragFloat("Height##loadwall", ref height, 0.1f, 0.1f, 2000f);
                if (widthChanged || heightChanged)
                {
                    var newRight = (right.LengthSquared() > 1e-9f ? Vector3.Normalize(right) : Vector3.UnitX) * (width * 0.5f);
                    var newUp    = (up.LengthSquared()    > 1e-9f ? Vector3.Normalize(up)    : Vector3.UnitY) * (height * 0.5f);
                    wallLm.M11 = newRight.X; wallLm.M12 = newRight.Y; wallLm.M13 = newRight.Z;
                    wallLm.M21 = newUp.X;    wallLm.M22 = newUp.Y;    wallLm.M23 = newUp.Z;
                    e.Transform.LocalMatrix = wallLm;
                }
                ImGui.TextDisabled("Move it with the gizmo. Saved with Save Chunk. Ctrl+D to duplicate\n" +
                                    "(a new independent entry/exit point to the same target level).");

                ImGui.Separator();
                if (ImGui.Button("Flip Load Wall Normal##flipwall", new Vector2(-1f, 0f)))
                {
                    wallLm.M11 = -wallLm.M11; wallLm.M12 = -wallLm.M12; wallLm.M13 = -wallLm.M13;
                    e.Transform.LocalMatrix = wallLm;
                    _browser.Log($"Flipped Load Wall normal for '{loadWall.Source.Path}'. Rectangle unchanged, only its facing reversed. Save Chunk + Build ISO to test the crossing.");
                }
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Reverses which way the wall FACES (its normal). The game uses the\n" +
                                 "facing to decide crossing direction -- a wrong-facing wall freezes\n" +
                                 "you in place with no control. If a generated/added wall won't\n" +
                                 "transition, flip it. TEST: flip a REAL working wall -- if that\n" +
                                 "freezes too, the normal is confirmed as the cause. Save Chunk + Build.");

                ImGui.Separator();
                if (ImGui.Button("Add Bounding Box##addloadzonebox", new Vector2(-1f, 0f)))
                    AddLoadZoneBox(e, loadWall);
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Adds a real pre-load trigger volume (separate from the wall\n" +
                                 "crossing plane above) to THIS SAME link — no new link entry, so\n" +
                                 "this is safe to add several of (chain into an L/U shape by\n" +
                                 "dragging each one with the Move gizmo). Save Chunk to persist.");
            }
        }
    }

    private void DrawInspectorPreLoadBox(Entity e)
    {
        var zoneBox = e.Get<CrashEngine.Importer.LoadZoneBoxMarker>();
        if (zoneBox is not null && e.Transform.LocalMatrix is { } boxLm)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Pre-Load Box##loadzonebox", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var bRight = new Vector3(boxLm.M11, boxLm.M12, boxLm.M13);
                var bUp    = new Vector3(boxLm.M21, boxLm.M22, boxLm.M23);
                var bFwd   = new Vector3(boxLm.M31, boxLm.M32, boxLm.M33);
                float sx = bRight.Length() * 2f, sy = bUp.Length() * 2f, sz = bFwd.Length() * 2f;

                ImGui.SetNextItemWidth(-1f);
                bool sxChanged = ImGui.DragFloat("Size X##loadzonebox", ref sx, 0.1f, 0.1f, 2000f);
                ImGui.SetNextItemWidth(-1f);
                bool syChanged = ImGui.DragFloat("Size Y##loadzonebox", ref sy, 0.1f, 0.1f, 2000f);
                ImGui.SetNextItemWidth(-1f);
                bool szChanged = ImGui.DragFloat("Size Z##loadzonebox", ref sz, 0.1f, 0.1f, 2000f);
                if (sxChanged || syChanged || szChanged)
                {
                    var nr = (bRight.LengthSquared() > 1e-9f ? Vector3.Normalize(bRight) : Vector3.UnitX) * (sx * 0.5f);
                    var nu = (bUp.LengthSquared()    > 1e-9f ? Vector3.Normalize(bUp)    : Vector3.UnitY) * (sy * 0.5f);
                    var nf = (bFwd.LengthSquared()   > 1e-9f ? Vector3.Normalize(bFwd)   : Vector3.UnitZ) * (sz * 0.5f);
                    boxLm.M11 = nr.X; boxLm.M12 = nr.Y; boxLm.M13 = nr.Z;
                    boxLm.M21 = nu.X; boxLm.M22 = nu.Y; boxLm.M23 = nu.Z;
                    boxLm.M31 = nf.X; boxLm.M32 = nf.Y; boxLm.M33 = nf.Z;
                    e.Transform.LocalMatrix = boxLm;
                }
                ImGui.TextDisabled("Move it with the gizmo. Saved with Save Chunk.");

                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Delete Box##delloadzonebox", new Vector2(-1f, 0f)))
                {
                    zoneBox.Link.ChunkLinksCollisionData.Remove(zoneBox.Box);
                    if (e.Parent is not null) e.Parent.RemoveChild(e);
                    _selectedSet.Remove(e);
                    if (_selected == e) _selected = null;
                }
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Removes this box from the link's pre-load zone. Not on the Undo\n" +
                                 "stack (unlike Add) — Reload Level from Disc undoes it before Save.");
            }
        }
    }
}
