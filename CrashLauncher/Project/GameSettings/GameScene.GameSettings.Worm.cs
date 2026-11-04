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
    private const uint EarthWormSquashLaunchId = 0x5E9;

    private static List<ITwinBehaviourCommand> FindAllWormCommands(BaseTwinSection? behSec)
    {
        var graph = behSec?.GetItem<PS2BehaviourGraph>(EarthWormSquashLaunchId);
        return FindApplyVelocityCommands(graph);
    }

    private List<float>? _wormOriginalValues;
    private float _wormLaunchMultiplier = 1f;
    private bool _wormMultiplierActive;

    private void DrawWormLaunchPower(BaseTwinSection? behSec)
    {
        var cmds = FindAllWormCommands(behSec);
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find the Earth Worm squash-launch behaviour (0x5E9) in this level.");
            return;
        }

        if (_wormOriginalValues is null || _wormOriginalValues.Count != cmds.Count * 2)
        {
            _wormOriginalValues = LoadWormOriginalValues(cmds.Count)
                ?? cmds.SelectMany(c => new[] { GetArgFloat(c, SpringVelocityArgIndex1), GetArgFloat(c, SpringVelocityArgIndex2) }).ToList();
        }

        if (!_wormMultiplierActive && _wormOriginalValues.Count > 0 && _wormOriginalValues[0] != 0f)
            _wormLaunchMultiplier = GetArgFloat(cmds[0], SpringVelocityArgIndex1) / _wormOriginalValues[0];

        ImGui.Text("Earth Worm Squash Launch Power (not yet live-tested)");
        bool multChanged = ImGui.SliderFloat("Launch Multiplier##wormlaunch", ref _wormLaunchMultiplier, 0.2f, 4f, "%.2fx");
        _wormMultiplierActive = ImGui.IsItemActive();
        if (multChanged)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex1, _wormOriginalValues[i * 2] * _wormLaunchMultiplier);
                SetArgFloat(cmds[i], SpringVelocityArgIndex2, _wormOriginalValues[i * 2 + 1] * _wormLaunchMultiplier);
            }
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("Same command/argument as Iron Spring Crate Bounce Height (cross-\n" +
                         "validated, not just a guess) but built for this specific creature\n" +
                         "just now — not yet confirmed live in PCSX2. 1.00x = original (~10.0).\n" +
                         "Save Chunk to persist, then Build ISO + test.");
        }

        bool anyChanged = false;
        for (int i = 0; i < cmds.Count; i++)
        {
            float v1 = GetArgFloat(cmds[i], SpringVelocityArgIndex1);
            if (ImGui.InputFloat($"Launch Power {i}##wormpower{i}", ref v1, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex1, v1);
                anyChanged = true;
            }
            float v2 = GetArgFloat(cmds[i], SpringVelocityArgIndex2);
            if (ImGui.InputFloat($"Launch Power {i} (secondary)##wormpower2_{i}", ref v2, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], SpringVelocityArgIndex2, v2);
                anyChanged = true;
            }
        }
        _ = anyChanged;

        if (ImGui.Button("Reset##wormreset"))
        {
            _wormLaunchMultiplier = 1f;
            ResetWormToOriginal(behSec);
        }
    }

    private List<float>? LoadWormOriginalValues(int expectedCount)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllWormCommands(freshBehSec);
            var values = freshCmds.SelectMany(c => new[] { GetArgFloat(c, SpringVelocityArgIndex1), GetArgFloat(c, SpringVelocityArgIndex2) }).ToList();
            return values.Count == expectedCount * 2 ? values : null;
        }
        catch { return null; }
    }

    private void ResetWormToOriginal(BaseTwinSection? behSec)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshCmds = FindAllWormCommands(freshBehSec);
            var liveCmds = FindAllWormCommands(behSec);
            for (int i = 0; i < liveCmds.Count && i < freshCmds.Count; i++)
            {
                SetArgFloat(liveCmds[i], SpringVelocityArgIndex1, GetArgFloat(freshCmds[i], SpringVelocityArgIndex1));
                SetArgFloat(liveCmds[i], SpringVelocityArgIndex2, GetArgFloat(freshCmds[i], SpringVelocityArgIndex2));
            }
            _browser.Log("Reset Earth Worm Squash Launch Power to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Earth Worm Squash Launch Power failed: {ex.Message}"); }
    }
}
