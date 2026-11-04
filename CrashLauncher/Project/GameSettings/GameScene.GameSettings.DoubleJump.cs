using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene
{
    // Amedo 2026-09-25
    private void DrawDoubleJumpRules()
    {
        if (!ImGui.CollapsingHeader("Jump Rules##doublejumprules")) return;
        var proj = GetGameProject();
        if (proj is null) { ImGui.TextDisabled("No project loaded."); return; }
        if (!BuildIsoToPS2.HasPatchableExe(_extractedRoot)) // Amedo
        {
            ImGui.TextDisabled("Only available for NTSC-U (SLUS_209.09) and PAL (SLES_525.68) discs.");
            return;
        }

        uint mask = proj.DoubleJumpMask ?? BuildIsoToPS2.DoubleJumpDefaultMask;

        bool single = (mask & 0x44) == 0;
        if (ImGui.Checkbox("Single jump only##djnormal", ref single))
        {
            mask = single ? mask & ~0x44u : mask | 0x44;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (the normal jump, standing or running, allows a double jump).\n" +
                         "Turn it on and the normal jump becomes a single jump.");

        bool slide = (mask & 0x20) != 0;
        if (ImGui.Checkbox("Allow double jump after slide jump##djslide", ref slide))
        {
            mask = slide ? mask | 0x20 : mask & ~0x20u;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (the slide jump is a single jump).\n" +
                         "The double jump after a slide jump also turns Crash to face the stick,\n" +
                         "like a normal double jump.");

        bool spin = (mask & BuildIsoToPS2.DoubleJumpSpinBit) != 0;
        if (ImGui.Checkbox("Allow double jump while spinning##djspin", ref spin))
        {
            mask = spin ? mask | BuildIsoToPS2.DoubleJumpSpinBit : mask & ~BuildIsoToPS2.DoubleJumpSpinBit;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (jumping while spinning is a single jump, and the spin keeps\n" +
                         "the double jump blocked until Crash lands). Turn it on to allow a double jump\n" +
                         "during and after the spin.");

        bool slideSpin = (mask & BuildIsoToPS2.SlideJumpSpinBit) != 0;
        if (ImGui.Checkbox("Allow spin during slide jump##spinslide", ref slideSpin))
        {
            mask = slideSpin ? mask | BuildIsoToPS2.SlideJumpSpinBit : mask & ~BuildIsoToPS2.SlideJumpSpinBit;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (the spin is cancelled during a slide jump).");

        bool slideSlam = (mask & BuildIsoToPS2.SlideJumpBodySlamBit) != 0;
        if (ImGui.Checkbox("Allow body slam during slide jump##bsslide", ref slideSlam))
        {
            mask = slideSlam ? mask | BuildIsoToPS2.SlideJumpBodySlamBit : mask & ~BuildIsoToPS2.SlideJumpBodySlamBit;
            SetDoubleJumpMask(proj, mask);
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Original game: OFF (the body slam button does nothing during a slide jump).");

        bool isDefault = mask == BuildIsoToPS2.DoubleJumpDefaultMask;
        ImGui.BeginDisabled(isDefault);
        if (ImGui.Button("Reset to original rules##djreset")) SetDoubleJumpMask(proj, BuildIsoToPS2.DoubleJumpDefaultMask);
        ImGui.EndDisabled();
    }

    private void SetDoubleJumpMask(CrashProject proj, uint mask)
    {
        proj.DoubleJumpMask = mask == BuildIsoToPS2.DoubleJumpDefaultMask ? null : mask;
        proj.Save();
        _browser.Log(proj.DoubleJumpMask is null
            ? "Jump rules: back to the original game rules (applied on next Build ISO)."
            : $"Jump rules: saved (mask 0x{mask:X2}) — applied on next Build ISO.");
    }
}
