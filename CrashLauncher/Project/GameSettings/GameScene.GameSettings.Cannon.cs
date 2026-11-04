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
    private const uint RigidCannonActivatedId = 0xF33;
    private const uint AUnknownCannonCommandIndex = 96;
    private const int  CannonPowerArgIndex1 = 41;
    private const int  CannonPowerArgIndex2 = 43;

    private static List<ITwinBehaviourCommand> FindAllCannonCommands(BaseTwinSection? behSec)
    {
        var found = new List<ITwinBehaviourCommand>();
        var graph = behSec?.GetItem<PS2BehaviourGraph>(RigidCannonActivatedId);
        if (graph is null) return found;
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == AUnknownCannonCommandIndex && cmd.Arguments.Count > CannonPowerArgIndex2)
                        found.Add(cmd);
        return found;
    }

    private List<float>? _cannonOriginalValues;
    private float _cannonPowerMultiplier = 1f;
    private bool _cannonMultiplierActive;

    private void DrawCannonShotPower(BaseTwinSection? behSec)
    {
        var cmds = FindAllCannonCommands(behSec);
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find the Rigid Cannon fire behaviour (0xF33) in this level.");
            return;
        }

        if (_cannonOriginalValues is null || _cannonOriginalValues.Count != cmds.Count * 2)
        {
            _cannonOriginalValues = LoadCannonOriginalValues(cmds.Count)
                ?? cmds.SelectMany(c => new[] { GetArgFloat(c, CannonPowerArgIndex1), GetArgFloat(c, CannonPowerArgIndex2) }).ToList();
        }

        if (!_cannonMultiplierActive && _cannonOriginalValues.Count > 0 && _cannonOriginalValues[0] != 0f)
            _cannonPowerMultiplier = GetArgFloat(cmds[0], CannonPowerArgIndex1) / _cannonOriginalValues[0];

        ImGui.Text("Cannon Shot Power — CONFIRMED WORKING LIVE");
        ImGui.TextDisabled("Higher = fires farther. Too high and the shot comes out jammed/stuck instead.");
        bool multChanged = ImGui.SliderFloat("Power Multiplier##cannonpower", ref _cannonPowerMultiplier, 0.2f, 4f, "%.2fx");
        _cannonMultiplierActive = ImGui.IsItemActive();
        if (multChanged)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                SetArgFloat(cmds[i], CannonPowerArgIndex1, _cannonOriginalValues[i * 2] * _cannonPowerMultiplier);
                SetArgFloat(cmds[i], CannonPowerArgIndex2, _cannonOriginalValues[i * 2 + 1] * _cannonPowerMultiplier);
            }
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("CONFIRMED live in PCSX2 (2026-08-11) — was a genuine guess (argument\n" +
                         "index 41/43 of the undocumented AUnknown_96 command) but the user\n" +
                         "confirmed live: bodyslamming the cannon's button fires it farther as\n" +
                         "this goes up. Known quirk: pushed too high, the shot comes out\n" +
                         "jammed/stuck instead of a clean launch — keep it moderate.\n" +
                         "1.00x = original. Save Chunk to persist, then Build ISO + test.");
        }

        for (int i = 0; i < cmds.Count; i++)
        {
            float v1 = GetArgFloat(cmds[i], CannonPowerArgIndex1);
            if (ImGui.InputFloat($"Shot {i} Power##cannonpower1_{i}", ref v1, 0.5f, 5f, "%.3f"))
                SetArgFloat(cmds[i], CannonPowerArgIndex1, v1);
            float v2 = GetArgFloat(cmds[i], CannonPowerArgIndex2);
            if (ImGui.InputFloat($"Shot {i} Power (secondary)##cannonpower2_{i}", ref v2, 0.5f, 5f, "%.3f"))
                SetArgFloat(cmds[i], CannonPowerArgIndex2, v2);
        }

        if (ImGui.Button("Reset##cannonreset"))
        {
            _cannonPowerMultiplier = 1f;
            ResetCannonToOriginal(behSec);
        }
    }

    private List<float>? LoadCannonOriginalValues(int expectedCount)
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
            var freshCmds = FindAllCannonCommands(freshBehSec);
            var values = freshCmds.SelectMany(c => new[] { GetArgFloat(c, CannonPowerArgIndex1), GetArgFloat(c, CannonPowerArgIndex2) }).ToList();
            return values.Count == expectedCount * 2 ? values : null;
        }
        catch { return null; }
    }

    private void ResetCannonToOriginal(BaseTwinSection? behSec)
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
            var freshCmds = FindAllCannonCommands(freshBehSec);
            var liveCmds = FindAllCannonCommands(behSec);
            for (int i = 0; i < liveCmds.Count && i < freshCmds.Count; i++)
            {
                SetArgFloat(liveCmds[i], CannonPowerArgIndex1, GetArgFloat(freshCmds[i], CannonPowerArgIndex1));
                SetArgFloat(liveCmds[i], CannonPowerArgIndex2, GetArgFloat(freshCmds[i], CannonPowerArgIndex2));
            }
            _browser.Log("Reset Cannon Shot Power to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Cannon Shot Power failed: {ex.Message}"); }
    }
}
