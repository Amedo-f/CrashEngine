using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private const uint CreateDamageCommandIndex = 514;
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
        if (!_windowChrome.Begin("Game Settings##gamesettings", ref _showGameSettings)) return;

        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var behSec = MeshDecoder.CodeSection(chunkSource?.GlobalRm2, TwinConstants.CODE_BEHAVIOURS_SECTION);
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
