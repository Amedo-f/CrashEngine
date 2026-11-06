using CrashEngine.Core;
using ImGuiNET;
using System.Numerics;
using NegativeLight = Twinsanity.TwinsanityInterchange.Common.Lights.NegativeLight;

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
