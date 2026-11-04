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
    private const uint CreateDamageCommandIndex = 514;
    private const uint TriggerBalancedCrateFallingCommandIndex = 519;
    private const int  DamageAmountArgIndex     = 7;
    private const int  DamageTypeArgIndex       = 8;
    private const uint VanillaBlastAmount       = 0x00000320;
    private const uint ClassicBlastAmount       = 0x00000008;
    private const uint PristineDamageType       = 0x0000000D;
    private const uint CrateExplodeBehaviourId  = 0x002F;

    private static ITwinBehaviourCommand? FindBigBlastCommand(PS2BehaviourGraph graph) => CrashEngine.Runtime.PlaySettings.FindBigBlast(graph); // Amedo 2026-10-04

    // Amedo 2026-09-25
    private bool? ReadClassicAkuAku() =>
        CrashEngine.Runtime.PlaySettings.ClassicFromData(Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.GlobalRm2); // Amedo 2026-10-04

    private static List<ITwinBehaviourCommand> FindAllCreateDamageCommands(PS2BehaviourGraph graph)
    {
        var found = new List<ITwinBehaviourCommand>();
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == CreateDamageCommandIndex && cmd.Arguments.Count > DamageTypeArgIndex)
                        found.Add(cmd);
        return found;
    }

    private void DrawGameSettings()
    {
        if (!_showGameSettings) return;

        ImGui.SetNextWindowSize(new Vector2(480f, 200f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Game Settings##gamesettings", ref _showGameSettings))
        {
            ImGui.End();
            return;
        }

        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var behSec = chunkSource?.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
        var explodeGraph = behSec?.GetItem<PS2BehaviourGraph>(CrateExplodeBehaviourId);

        if (chunkSource is null)
        {
            ImGui.TextDisabled("No level currently loaded.");
        }
        else if (explodeGraph is null)
        {
            ImGui.TextDisabled("Couldn't find the Nitro/TNT explosion behaviour (0x002F) in Startup\\Default.rm2.");
        }
        else
        {
            var bigBlast = FindBigBlastCommand(explodeGraph);
            if (bigBlast is null)
            {
                ImGui.TextDisabled("Couldn't find the initial-blast CreateDamage call.");
            }
            else
            {
                bool isClassic = bigBlast.Arguments[DamageAmountArgIndex] == ClassicBlastAmount;
                bool isVanilla = bigBlast.Arguments[DamageAmountArgIndex] == VanillaBlastAmount;
                if (GetGameProject() is { } classicProj && classicProj.ClassicAkuAku != isClassic) // Amedo 2026-09-25
                {
                    classicProj.ClassicAkuAku = isClassic;
                    classicProj.Save();
                }

                bool classicAkuAku = isClassic;
                if (ImGui.Checkbox("Classic Aku Aku##classicakuaku", ref classicAkuAku))
                {
                    bigBlast.Arguments[DamageAmountArgIndex] = classicAkuAku ? ClassicBlastAmount : VanillaBlastAmount;
                    foreach (var cmd in FindAllCreateDamageCommands(explodeGraph))
                        cmd.Arguments[DamageTypeArgIndex] = PristineDamageType;
                    chunkSource.GlobalRm2Dirty = true;
                }
                if (ImGui.IsItemHovered())
                {
                    MaybeTooltip("Classic-games Aku Aku: getting hit by a Nitro or TNT Crate only\n" +
                                 "kills you if you have NO Aku Aku masks — same as any other damage\n" +
                                 "source, instead of always killing you outright regardless of masks.\n" +
                                 "Shrinks the crate explosion's initial blast damage from 800 to 8\n" +
                                 "(matching TheBetaM/CrateModLoader's real, decompiled fix — small\n" +
                                 "enough for Aku Aku to actually absorb). Edits the shared Nitro/TNT\n" +
                                 "explosion script (Startup\\Default.rm2) — affects every level.\n" +
                                 "Also fixes the golden Aku Aku glow disappearing after Crash walks\n" +
                                 "into another level (the glow now follows him), and the golden\n" +
                                 "invincibility ending after an in-game cutscene (its timer now keeps\n" +
                                 "running during the cutscene: the rest continues afterwards, or it\n" +
                                 "ends on time mid-cutscene). Both applied on Build ISO.\n" +
                                 "Save Chunk to persist, then Build ISO + test.");
                }

                if (!isClassic && !isVanilla)
                    ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1f),
                        "Mixed/unexpected state — the initial-blast damage amount doesn't match\neither known value; check manually before relying on this checkbox.");
            }
        }

        DrawAkuAkuInvincibility(); // Amedo 2026-09-25

        ImGui.Separator();
        DrawDoubleJumpRules(); // Amedo 2026-09-25

        if (chunkSource is not null) // Amedo
        {
            ImGui.Separator();
            DrawCratesReceiveShadows(chunkSource);
        }

        ImGui.Separator(); // Amedo 2026-10-01
        DrawLanguage();

        ImGui.End();
    }
}
