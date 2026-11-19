using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-18
public sealed partial class GameScene : Scene
{
    private string _modernClipFilter = "";

    private void DrawInspectorModernCharacter(Entity e)
    {
        if ((e.Get<ModernCharacterRenderer>() ?? e.Children.Select(c => c.Get<ModernCharacterRenderer>()).FirstOrDefault(r => r is not null)) is not { } mc) return;
        var data = mc.Data;
        ImGui.Separator();
        if (!ImGui.CollapsingHeader("Animations##modern", ImGuiTreeNodeFlags.DefaultOpen)) return;
        ImGui.TextDisabled($"Modern look (PC only): {Path.GetFileName(mc.SourcePath)}. Build ISO uses the still model.");
        ImGui.TextDisabled($"{data.Joints.Count} bones · {data.VertexCount:N0} vertices · {data.TriangleCount:N0} triangles · {data.Materials.Count} materials");
        if (data.Notes.Count > 0 && ImGui.TreeNode($"Import notes ({data.Notes.Count})##modernnotes"))
        {
            foreach (var note in data.Notes) ImGui.TextWrapped(note);
            ImGui.TreePop();
        }

        if (ImGui.Button(mc.Playing ? "Pause##modern" : "Play##modern"))
        {
            if (mc.Clip < 0) mc.Play(0);
            else mc.Playing = !mc.Playing;
        }
        ImGui.SameLine();
        bool loop = mc.Loop;
        if (ImGui.Checkbox("Loop##modern", ref loop)) mc.Loop = loop;
        ImGui.SameLine();
        if (ImGui.Button("Rest pose##modern")) mc.Play(-1);
        float scale = mc.Scale;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.DragFloat("##modernscale", ref scale, 0.005f, 0.01f, 100f, "look scale %.3f")) mc.Scale = scale;
        if (ImGui.IsItemDeactivatedAfterEdit()) SaveModernLook(mc);
        float speed = mc.Speed;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.SliderFloat("##modernspeed", ref speed, 0.1f, 2f, "speed %.2f")) mc.Speed = speed;
        if (mc.Clip >= 0)
        {
            var clip = data.Clips[mc.Clip];
            float t = mc.Time;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.SliderFloat("##moderntime", ref t, 0f, MathF.Max(clip.Duration, 1e-3f), $"{clip.Name}  %.2f / {clip.Duration:0.00} s"))
            {
                mc.Time = t;
                mc.Playing = false;
            }
        }

        DrawModernSlots(mc);

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##modernfilter", $"Search {data.Clips.Count} animations...", ref _modernClipFilter, 64);
        if (ImGui.BeginChild("##modernclips", new Vector2(0f, 200f), ImGuiChildFlags.Border))
        {
            for (int i = 0; i < data.Clips.Count; i++)
            {
                var clip = data.Clips[i];
                if (_modernClipFilter.Length > 0 && !clip.Name.Contains(_modernClipFilter, StringComparison.OrdinalIgnoreCase)) continue;
                ImGui.PushID(i);
                int slot = mc.Slots.IndexOf(i);
                if (slot < 0 && mc.Slots.Count < ModernCharacterRenderer.MaxSlots)
                {
                    if (ImGui.SmallButton("+ slot")) { mc.Slots.Add(i); SaveModernLook(mc); }
                    ImGui.SameLine();
                }
                string tag = slot >= 0 ? $"[slot {slot}]" : "";
                if (ImGui.Selectable($"{clip.Name}   {clip.Duration:0.00} s   {tag}##modernclip", i == mc.Clip)) mc.Play(i);
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
    }

    private void DrawModernSlots(ModernCharacterRenderer mc)
    {
        if (!ImGui.TreeNodeEx($"Slots ({mc.Slots.Count} of {ModernCharacterRenderer.MaxSlots})##modernslots", ImGuiTreeNodeFlags.DefaultOpen)) return;
        ImGui.TextDisabled("A behaviour plays a slot with Do Animation and its number.");
        int move = -1, remove = -1, delta = 0;
        if (ImGui.BeginChild("##modernslotlist", new Vector2(0f, MathF.Min(200f, 8f + mc.Slots.Count * ImGui.GetFrameHeightWithSpacing())), ImGuiChildFlags.Border))
        {
            for (int s = 0; s < mc.Slots.Count; s++)
            {
                ImGui.PushID(s);
                if (ImGui.ArrowButton("##up", ImGuiDir.Up) && s > 0) (move, delta) = (s, -1);
                ImGui.SameLine();
                if (ImGui.ArrowButton("##down", ImGuiDir.Down) && s + 1 < mc.Slots.Count) (move, delta) = (s, 1);
                ImGui.SameLine();
                if (ImGui.SmallButton("x")) remove = s;
                ImGui.SameLine();
                int clip = mc.Slots[s];
                string name = clip >= 0 ? mc.Data.Clips[clip].Name : "(missing)";
                if (ImGui.Selectable($"Slot {s,3}   {name}##modernslot", clip >= 0 && clip == mc.Clip) && clip >= 0) mc.Play(clip);
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
        ImGui.TreePop();
        if (move >= 0) { (mc.Slots[move], mc.Slots[move + delta]) = (mc.Slots[move + delta], mc.Slots[move]); SaveModernLook(mc); }
        if (remove >= 0) { mc.Slots.RemoveAt(remove); SaveModernLook(mc); }
    }
}
