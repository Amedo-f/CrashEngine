using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

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
                if (!hasOffset) ImGui.TextDisabled("(move or rotate this chunk above to enable)");
            }
        }
    }
}
