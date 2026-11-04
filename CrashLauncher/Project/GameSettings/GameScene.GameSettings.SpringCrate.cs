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
    private const uint IronSpringCrateLandedOnId   = 0x5F;
    private const uint WoodenSpringCrateLandedOnId = 0x67;

    private static List<ITwinBehaviourCommand> FindAllSpringCommands(BaseTwinSection? behSec)
    {
        var ironGraph = behSec?.GetItem<PS2BehaviourGraph>(IronSpringCrateLandedOnId);
        return FindApplyVelocityCommands(ironGraph);
    }

    private List<float>? _springOriginalValues;
    private float _springBounceMultiplier = 1f;
    private bool _springMultiplierActive;

    private void DrawSpringCrateBounce(ChunkSource chunkSource, BaseTwinSection? behSec)
    {
        var cmds = FindAllSpringCommands(behSec);
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find the Iron Spring Crate bounce behaviour (0x5F) in Startup\\Default.rm2.");
            return;
        }

        if (_springOriginalValues is null || _springOriginalValues.Count != cmds.Count * 2)
        {
            _springOriginalValues = LoadSpringOriginalValues(cmds.Count)
                ?? cmds.SelectMany(c => new[] { GetArgFloat(c, SpringVelocityArgIndex1), GetArgFloat(c, SpringVelocityArgIndex2) }).ToList();
        }

        if (!_springMultiplierActive && _springOriginalValues.Count > 0 && _springOriginalValues[0] != 0f)
            _springBounceMultiplier = GetArgFloat(cmds[0], SpringVelocityArgIndex1) / _springOriginalValues[0];

        ImGui.Text("Iron Spring Crate Bounce Height — CONFIRMED WORKING LIVE");

        bool multChanged = ImGui.SliderFloat("Bounce Multiplier##springbounce", ref _springBounceMultiplier, 0.2f, 4f, "%.2fx");
        _springMultiplierActive = ImGui.IsItemActive();
        if (multChanged)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex1, _springOriginalValues[i * 2] * _springBounceMultiplier);
                SetArgFloat(cmds[i], SpringVelocityArgIndex2, _springOriginalValues[i * 2 + 1] * _springBounceMultiplier);
            }
            chunkSource.GlobalRm2Dirty = true;
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("CONFIRMED live in PCSX2 (2026-08-11) — scales both launch-power numbers\n" +
                         "below relative to the true original. 1.00x = original bounce.\n" +
                         "Save Chunk to persist, then Build ISO + test.");
        }

        bool anyChanged = false;
        for (int i = 0; i < cmds.Count; i++)
        {
            float v1 = GetArgFloat(cmds[i], SpringVelocityArgIndex1);
            if (ImGui.InputFloat($"Launch Power {i}##springpower{i}", ref v1, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex1, v1);
                anyChanged = true;
            }
            float v2 = GetArgFloat(cmds[i], SpringVelocityArgIndex2);
            if (ImGui.InputFloat($"Launch Power {i} (secondary)##springpower2_{i}", ref v2, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex2, v2);
                anyChanged = true;
            }
        }
        if (anyChanged) chunkSource.GlobalRm2Dirty = true;

        if (ImGui.Button("Reset##springreset"))
        {
            _springBounceMultiplier = 1f;
            ResetSpringToOriginal(chunkSource, behSec);
        }
    }

    private List<float>? LoadSpringOriginalValues(int expectedCount)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                ?? throw new FileNotFoundException("Couldn't reopen Startup\\Default.rm2 from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshDefault = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
            freshDefault.Read(reader, (int)stream.Length);

            var freshBehSec = freshDefault.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllSpringCommands(freshBehSec);
            var values = freshCmds.SelectMany(c => new[] { GetArgFloat(c, SpringVelocityArgIndex1), GetArgFloat(c, SpringVelocityArgIndex2) }).ToList();
            return values.Count == expectedCount * 2 ? values : null;
        }
        catch { return null; }
    }

    private void ResetSpringToOriginal(ChunkSource chunkSource, BaseTwinSection? behSec)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                ?? throw new FileNotFoundException("Couldn't reopen Startup\\Default.rm2 from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshDefault = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
            freshDefault.Read(reader, (int)stream.Length);

            var freshBehSec = freshDefault.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllSpringCommands(freshBehSec);
            var liveCmds = FindAllSpringCommands(behSec);
            for (int i = 0; i < liveCmds.Count && i < freshCmds.Count; i++)
            {
                SetArgFloat(liveCmds[i], SpringVelocityArgIndex1, GetArgFloat(freshCmds[i], SpringVelocityArgIndex1));
                SetArgFloat(liveCmds[i], SpringVelocityArgIndex2, GetArgFloat(freshCmds[i], SpringVelocityArgIndex2));
            }
            chunkSource.GlobalRm2Dirty = true;
            _browser.Log("Reset Spring Crate Bounce Height to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Spring Crate Bounce Height failed: {ex.Message}"); }
    }
}
