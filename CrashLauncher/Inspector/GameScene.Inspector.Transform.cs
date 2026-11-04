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
    private void DrawInspectorTransform(Entity e)
    {
        ImGui.Separator();
        if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var lmForPos = e.Transform.LocalMatrix;
            var pos = lmForPos is { } m0 ? new Vector3(m0.M41, m0.M42, m0.M43) : e.Transform.Position;
            ImGui.SetNextItemWidth(-1f);
            bool posChanged = ImGui.DragFloat3("Pos##t", ref pos, 0.05f);
            if (posChanged)
            {
                if (lmForPos is { } m1)
                {
                    m1.M41 = pos.X; m1.M42 = pos.Y; m1.M43 = pos.Z;
                    e.Transform.LocalMatrix = m1;
                }
                else e.Transform.Position = pos;
            }

            var lmForRot = e.Transform.LocalMatrix;
            bool haveRot = true;
            Vector3 scaleForRot = Vector3.One, posForRot = Vector3.Zero;
            Vector3 eulerDeg;
            if (lmForRot is { } mRot)
            {
                if (Matrix4x4.Decompose(mRot, out scaleForRot, out var rotQ, out posForRot))
                    eulerDeg = MatrixToEulerXYZ(Matrix4x4.CreateFromQuaternion(rotQ)) * (180f / MathF.PI);
                else { haveRot = false; eulerDeg = Vector3.Zero; }
            }
            else
            {
                eulerDeg = MatrixToEulerXYZ(Matrix4x4.CreateFromQuaternion(e.Transform.Rotation)) * (180f / MathF.PI);
            }

            ImGui.BeginDisabled(!haveRot);
            ImGui.SetNextItemWidth(-1f);
            bool rotChanged = ImGui.DragFloat3("Rot##t", ref eulerDeg, 0.5f);
            if (!haveRot && ImGui.IsItemHovered())
                MaybeTooltip("This object's transform is a mirrored/degenerate matrix — rotation\n" +
                             "can't be cleanly separated from its scale to show as angles.");
            if (rotChanged)
            {
                var newQuat = EulerXYZToQuaternion(eulerDeg * (MathF.PI / 180f));
                if (lmForRot is not null)
                {
                    e.Transform.LocalMatrix = Matrix4x4.CreateScale(scaleForRot)
                                             * Matrix4x4.CreateFromQuaternion(newQuat)
                                             * Matrix4x4.CreateTranslation(posForRot);
                }
                else e.Transform.Rotation = newQuat;
            }
            ImGui.EndDisabled();

            var scale = lmForRot is not null ? scaleForRot : e.Transform.Scale;

            void ApplyScale(Vector3 newScale)
            {
                if (lmForRot is not null)
                {
                    var rotQ2 = Matrix4x4.Decompose(lmForRot.Value, out _, out var q2, out _) ? q2 : Quaternion.Identity;
                    e.Transform.LocalMatrix = Matrix4x4.CreateScale(newScale)
                                             * Matrix4x4.CreateFromQuaternion(rotQ2)
                                             * Matrix4x4.CreateTranslation(posForRot);
                }
                else e.Transform.Scale = newScale;
            }

            float uniform = (scale.X + scale.Y + scale.Z) / 3f;
            ImGui.SetNextItemWidth(-1f);
            bool uniformChanged = ImGui.DragFloat("Uniform Scale##t", ref uniform, 0.01f, 0.001f, 1000f);
            if (uniformChanged) { scale = new Vector3(uniform, uniform, uniform); ApplyScale(scale); }

            ImGui.SetNextItemWidth(-1f);
            bool scaleChanged = ImGui.DragFloat3("Scale##t", ref scale, 0.01f, 0.001f, 1000f);
            if (scaleChanged) ApplyScale(scale);

            var wp = e.Transform.World.Translation;
            ImGui.TextDisabled($"World  {wp.X:F2}  {wp.Y:F2}  {wp.Z:F2}");
        }
    }

    private void DrawInspectorChunkTransform(Entity e)
    {
        if (e.Has<ChunkSource>())
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Whole-Chunk Transform##chunktf", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool hasOffset = e.Transform.Position.LengthSquared() > 1e-8f ||
                                  Quaternion.Dot(e.Transform.Rotation, Quaternion.Identity) < 0.999999f;
                ImGui.BeginDisabled(!hasOffset);
                if (ImGui.Button("Apply Chunk Transform (bake + save + reload)##applychunktf", new Vector2(-1f, 0f)))
                    ApplyChunkTransform();
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered()) MaybeTooltip(
                    "Move/rotate the level using the Pos/Rot fields above (or the gizmo), then\n" +
                    "click this to make it REAL: bakes the offset into every Instance, Scenery\n" +
                    "tile, Collision vertex/trigger, layout Trigger/Camera/Position/AI Position\n" +
                    "in the chunk, resets this Transform back to zero, Saves Chunk, and reloads.\n" +
                    "NOT on the Undo stack (touches too many independent systems at once) and\n" +
                    "writes to disk immediately — this is a one-way bake, like Blender's Apply\n" +
                    "Transform, not a live/re-editable offset. Cameras/Triggers' own internal\n" +
                    "facing/rotation fields are NOT adjusted (undecoded) — exact for a pure move,\n" +
                    "an approximation if you also rotated.");
                if (!hasOffset) ImGui.TextDisabled("(move or rotate this chunk above to enable)");
            }
        }
    }
}
