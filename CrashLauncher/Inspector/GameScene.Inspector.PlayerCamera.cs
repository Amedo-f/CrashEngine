using CrashEngine.Core;
using ImGuiNET;

namespace CrashLauncher;

// Amedo 2026-11-15
public sealed partial class GameScene : Scene
{
    private bool _playerCameraAddresses;

    private void DrawInspectorPlayerCamera(Entity e)
    {
        if (!e.Has<PlayerCameraMarker>()) return;
        ImGui.Separator();
        if (!ImGui.CollapsingHeader("Player Camera##pcam", ImGuiTreeNodeFlags.DefaultOpen)) return;
        ImGui.TextWrapped("The camera the game builds behind Crash when the level starts, before any camera zone. " +
                          "The values come from the game's own camera code and are the same in every level. Read only.");
        if (!_playerCameraSettled)
        {
            ImGui.TextDisabled("No Crash in this scene.");
            return;
        }
        if (_playerCameraMoved)
        {
            ImGui.TextWrapped("Moved in the editor only. When Play starts, the game puts it behind Crash.");
            if (ImGui.Button("Snap Behind Crash##pcam", new System.Numerics.Vector2(-1f, 0f))) SnapPlayerCameraBehindCrash();
        }
        else ImGui.TextDisabled("Shown where it starts. Move it freely; it still starts behind Crash in Play.");
        ImGui.Checkbox("Show game addresses##pcam", ref _playerCameraAddresses);

        var s = _playerCameraStart;
        var m = s.Memory;
        uint d = s.Director, y = s.Eyes, x = s.Aim, c9 = s.CameraComponent;
        string Deg(uint a) => $"{PlayerCameraStart.Degrees(m.I(a)):0.##}°";
        string DegPerSec(uint a) => $"{PlayerCameraStart.Degrees(m.I(a)):0.##}°/s";
        string Num(uint a) => $"{m.F(a):0.###}";

        void Group(string title, params (string Label, string Value, string Address)[] rows)
        {
            ImGui.SeparatorText(title);
            int columns = _playerCameraAddresses ? 3 : 2;
            if (!ImGui.BeginTable($"##pcam{title}", columns, ImGuiTableFlags.SizingStretchProp)) return;
            foreach (var (label, value, address) in rows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(label);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(value);
                if (columns == 3)
                {
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(address);
                }
            }
            ImGui.EndTable();
        }

        Group("View",
            ("Field of view", Deg(c9 + 0x1C), "comp9+0x1C"),
            ("Aspect", $"{Num(c9 + 0x20)} (1.0 on PAL)", "comp9+0x20"),
            ("Near clip", Num(c9 + 0x24), "comp9+0x24"),
            ("Far clip", Num(c9 + 0x28), "comp9+0x28"));

        Group("Look-at point",
            ("Height above Crash", Num(x + 0xA4), "X+0xA4"),
            ("Low offset", Num(x + 0xF4), "X+0xF4 (0x30B7B0)"),
            ("High offset", Num(x + 0x104), "X+0x104 (0x30B7C0)"));

        Group("Distance",
            ("Distance", Num(y + 0x158), "Y+0x158"),
            ("Default", Num(d + 0x440), "D+0x440"),
            ("Closest", Num(y + 0x178), "Y+0x178 (D+0x468)"),
            ("Farthest", Num(y + 0x17C), "Y+0x17C (D+0x46C)"),
            ("First frame", Num(y + 0x180), "Y+0x180"),
            ("Return speed", $"{Num(y + 0x168)}/s", "Y+0x168"),
            ("Zoom speed (stick)", $"{Num(y + 0x16C)}/s", "Y+0x16C"));

        Group("Pitch",
            ("Pitch", Deg(y + 0x68), "Y+0x68"),
            ("Lowest", Deg(y + 0x88), "Y+0x88 (D+0x378)"),
            ("Highest", Deg(y + 0x8C), "Y+0x8C (D+0x37C)"),
            ("Return speed", DegPerSec(y + 0x78), "Y+0x78"),
            ("Stick speed", DegPerSec(y + 0x7C), "Y+0x7C"));

        Group("Direction",
            ("Starts", "behind Crash", "0x2711B8"),
            ("Stick speed", DegPerSec(y + 0x11C), "Y+0x11C"));

        Group("Follow",
            ("Eye follow rate", Num(y + 0x390), "Y+0x390"),
            ("Turn follow rate", Num(y + 0x394), "Y+0x394"));

        Group("Obstacles",
            ("Collision radius", Num(y + 0x3C0), "Y+0x3C0"),
            ("Dodge speed (pitch)", DegPerSec(y + 0x1CC), "Y+0x1CC"),
            ("Dodge speed (direction)", DegPerSec(y + 0x1D0), "Y+0x1D0"),
            ("Blocked view", "0.2 s, then 0.7 s before the eye moves in front", "0x27BE70"));
    }
}
