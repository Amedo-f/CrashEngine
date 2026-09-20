using ImGuiNET;
using System;
using System.Linq;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private static readonly (string Name, string World, string Match, string Path)[] FixedSkydomes =
    {
        ("Skydome 1", "Earth",    @"Earth\Hub\beach",        @"Levels\Earth\Hub\beach.rm2"),
        ("Skydome 2", "Ice",      @"Ice\Hub\labext",         @"Levels\Ice\Hub\labext.rm2"),
        ("Skydome 3", "school",   @"school\Sch_Hub\sch_hub", @"Levels\school\Sch_Hub\sch_hub.rm2"),
        ("Skydome 4", "AltEarth", @"AltEarth\Hub\alta",      @"Levels\AltEarth\Hub\alta.rm2"),
    };

    private void DrawSkydomeUniquePicker()
    {
        ImGui.TextDisabled("Replace this level's sky — the game's 4 skydomes:");
        foreach (var s in FixedSkydomes)
        {
            if (ImGui.Selectable($"{s.Name}  —  {s.World}"))
            {
                var path = GetSwapLevelList()
                    .FirstOrDefault(l => l.Replace('/', '\\').Contains(s.Match, StringComparison.OrdinalIgnoreCase))
                    ?? s.Path;
                SwapSkydome(path);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Applies the {s.World} sky (from {s.Match})");
        }
    }
}
