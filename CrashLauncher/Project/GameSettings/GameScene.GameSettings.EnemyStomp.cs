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
    private const uint GenericCreatureDamagedJumpedOnId = 0x709;
    private const uint ReduceHitPointsCommandIndex = 528;
    private const int  StompDamageArgIndex = 0;
    private const int  StompBounceArgIndex = 2;

    private static List<ITwinBehaviourCommand> FindStompDamageCommands(BaseTwinSection? behSec)
    {
        var found = new List<ITwinBehaviourCommand>();
        var graph = behSec?.GetItem<PS2BehaviourGraph>(GenericCreatureDamagedJumpedOnId);
        if (graph is null) return found;
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == ReduceHitPointsCommandIndex && cmd.Arguments.Count > StompDamageArgIndex)
                        found.Add(cmd);
        return found;
    }

    private static List<ITwinBehaviourCommand> FindStompBounceCommands(BaseTwinSection? behSec)
    {
        var graph = behSec?.GetItem<PS2BehaviourGraph>(GenericCreatureDamagedJumpedOnId);
        return FindApplyVelocityCommands(graph);
    }

    private uint? _stompOriginalDamage;
    private float? _stompOriginalBounce;
    private float _stompDamageMultiplier = 1f;
    private bool _stompDamageMultiplierActive;

    private void DrawEnemyStomp(BaseTwinSection? behSec)
    {
        var dmgCmds = FindStompDamageCommands(behSec);
        var bounceCmds = FindStompBounceCommands(behSec);
        if (dmgCmds.Count == 0 && bounceCmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find the enemy-stomp behaviour (0x709) in this level.");
            return;
        }

        if (_stompOriginalDamage is null && dmgCmds.Count > 0)
            _stompOriginalDamage = LoadStompOriginals(out _stompOriginalBounce) ?? dmgCmds[0].Arguments[StompDamageArgIndex];

        ImGui.Text("Enemy Stomp Damage");
        if (dmgCmds.Count > 0 && _stompOriginalDamage is { } origDmg)
        {
            if (!_stompDamageMultiplierActive && origDmg != 0)
                _stompDamageMultiplier = dmgCmds[0].Arguments[StompDamageArgIndex] / (float)origDmg;

            bool multChanged = ImGui.SliderFloat("Damage Multiplier##stompdmg", ref _stompDamageMultiplier, 0.2f, 5f, "%.2fx");
            _stompDamageMultiplierActive = ImGui.IsItemActive();
            if (multChanged)
                foreach (var cmd in dmgCmds)
                    cmd.Arguments[StompDamageArgIndex] = (uint)Math.Max(0, MathF.Round(origDmg * _stompDamageMultiplier));
            if (ImGui.IsItemHovered())
            {
                MaybeTooltip("Confident, not just a guess — ReduceHitPoints has exactly ONE plain\n" +
                             "whole-number argument (the enemy hit points removed per stomp, no\n" +
                             "float decoding involved). Original = " + origDmg + ".\n" +
                             "Save Chunk to persist, then Build ISO + test.");
            }

            int rawDmg = (int)dmgCmds[0].Arguments[StompDamageArgIndex];
            if (ImGui.InputInt("Damage (raw)##stompdmgraw", ref rawDmg))
            {
                uint clamped = (uint)Math.Max(0, rawDmg);
                foreach (var cmd in dmgCmds) cmd.Arguments[StompDamageArgIndex] = clamped;
            }
        }

        if (bounceCmds.Count > 0)
        {
            ImGui.Text("Enemy Stomp Bounce-Back Power");
            float v = GetArgFloat(bounceCmds[0], StompBounceArgIndex);
            if (ImGui.InputFloat("Bounce Power##stompbounce", ref v, 0.5f, 5f, "%.3f"))
                foreach (var cmd in bounceCmds) SetArgFloat(cmd, StompBounceArgIndex, v);
            if (v >= 16f)
                ImGui.TextColored(new Vector4(1f, 0.6f, 0.2f, 1f),
                    "16.0+ found to kill the bounce entirely (not just clamp it) — stay under 16.");
        }

        if (ImGui.Button("Reset##stompreset"))
        {
            _stompDamageMultiplier = 1f;
            ResetStompToOriginal(behSec);
        }
    }

    private uint? LoadStompOriginals(out float? origBounce)
    {
        origBounce = null;
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
            var freshDmgCmds = FindStompDamageCommands(freshBehSec);
            var freshBounceCmds = FindStompBounceCommands(freshBehSec);
            if (freshBounceCmds.Count > 0) origBounce = GetArgFloat(freshBounceCmds[0], StompBounceArgIndex);
            return freshDmgCmds.Count > 0 ? freshDmgCmds[0].Arguments[StompDamageArgIndex] : null;
        }
        catch { return null; }
    }

    private void ResetStompToOriginal(BaseTwinSection? behSec)
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
            var freshDmgCmds = FindStompDamageCommands(freshBehSec);
            var liveDmgCmds = FindStompDamageCommands(behSec);
            for (int i = 0; i < liveDmgCmds.Count && i < freshDmgCmds.Count; i++)
                liveDmgCmds[i].Arguments[StompDamageArgIndex] = freshDmgCmds[i].Arguments[StompDamageArgIndex];

            var freshBounceCmds = FindStompBounceCommands(freshBehSec);
            var liveBounceCmds = FindStompBounceCommands(behSec);
            for (int i = 0; i < liveBounceCmds.Count && i < freshBounceCmds.Count; i++)
                SetArgFloat(liveBounceCmds[i], StompBounceArgIndex, GetArgFloat(freshBounceCmds[i], StompBounceArgIndex));

            _browser.Log("Reset Enemy Stomp to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Enemy Stomp failed: {ex.Message}"); }
    }
}
