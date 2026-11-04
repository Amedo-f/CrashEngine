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
    private static readonly uint[] CrateLandedOnBehaviourIds =
    {
        0x3D,
        0x45,
        0x4B,
        0x53,
        0x6B,
        0x6F,
        0x73,
        0x489,
        0xD49,
        0x1AE5,
        0x1AE9,
    };
    private static readonly Dictionary<uint, string> CrateLandedOnNames = new()
    {
        [0x3D] = "TNT Crate", [0x45] = "Nitro Switch Crate", [0x4B] = "Iron Switch Crate",
        [0x53] = "Multiple-Hit Crate", [0x6B] = "Surprise Crate", [0x6F] = "Basic Crate",
        [0x73] = "Extra Life Crate", [0x489] = "Aku Aku Crate",
        [0xD49] = "Breakable Nitro Switch Crate", [0x1AE5] = "Cortex Life Crate",
        [0x1AE9] = "Nina Life Crate",
    };
    private const int CrateBounceArgIndex = 5;

    private readonly Dictionary<uint, float> _crateBounceOriginals = new();
    private readonly Dictionary<uint, float> _crateBounceMultipliers = new();
    private uint? _crateBounceActiveId;

    private void DrawCrateLandingBounceForId(ChunkSource chunkSource, BaseTwinSection? behSec, uint id)
    {
        var cmds = FindApplyVelocityCommands(behSec?.GetItem<PS2BehaviourGraph>(id));
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("Couldn't find this crate's landing-bounce command in Startup\\Default.rm2.");
            return;
        }
        var cmd = cmds[0];
        string label = CrateLandedOnNames.TryGetValue(id, out var n) ? n : $"0x{id:X}";

        if (!_crateBounceOriginals.TryGetValue(id, out var orig))
        {
            orig = LoadCrateBounceOriginal(id) ?? GetArgFloat(cmd, CrateBounceArgIndex);
            _crateBounceOriginals[id] = orig;
        }
        bool isActive = _crateBounceActiveId == id;
        float mult = _crateBounceMultipliers.TryGetValue(id, out var m) ? m : 1f;
        if (!isActive && orig != 0f)
            mult = GetArgFloat(cmd, CrateBounceArgIndex) / orig;

        ImGui.Text($"{label} Landing Bounce");
        ImGui.TextDisabled("Independent per crate type — doesn't affect any other crate.");
        bool changed = ImGui.SliderFloat($"Bounce Multiplier##cratebounce_{id:X}", ref mult, 0.2f, 4f, "%.2fx");
        if (ImGui.IsItemActive()) _crateBounceActiveId = id;
        else if (isActive) _crateBounceActiveId = null;
        _crateBounceMultipliers[id] = mult;
        if (changed)
        {
            SetArgFloat(cmd, CrateBounceArgIndex, orig * mult);
            chunkSource.GlobalRm2Dirty = true;
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip($"Not yet live-tested — only affects {label}, every other crate type has\n" +
                         "its own separate value. Original ≈ " + orig.ToString("0.###") + ".\n" +
                         "Watch for a hard ceiling like the Cannon/Enemy Stomp had — going too\n" +
                         "high may silently kill the bounce instead of making it bigger.\n" +
                         "Save Chunk to persist, then Build ISO + test.");
        }
        if (ImGui.Button($"Reset##cratebouncereset_{id:X}"))
        {
            _crateBounceMultipliers[id] = 1f;
            ResetCrateBounceForId(chunkSource, behSec, id);
        }
    }

    private float? LoadCrateBounceOriginal(uint id)
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
            var freshCmds = FindApplyVelocityCommands(freshBehSec?.GetItem<PS2BehaviourGraph>(id));
            return freshCmds.Count > 0 ? GetArgFloat(freshCmds[0], CrateBounceArgIndex) : null;
        }
        catch { return null; }
    }

    private void ResetCrateBounceForId(ChunkSource chunkSource, BaseTwinSection? behSec, uint id)
    {
        var orig = LoadCrateBounceOriginal(id);
        if (orig is null) { _browser.Log("Reset Crate Landing Bounce failed: couldn't find the original value."); return; }

        var cmds = FindApplyVelocityCommands(behSec?.GetItem<PS2BehaviourGraph>(id));
        if (cmds.Count == 0) { _browser.Log("Reset Crate Landing Bounce failed: live command not found."); return; }

        SetArgFloat(cmds[0], CrateBounceArgIndex, orig.Value);
        _crateBounceOriginals[id] = orig.Value;
        chunkSource.GlobalRm2Dirty = true;
        _browser.Log($"Reset {(CrateLandedOnNames.TryGetValue(id, out var n) ? n : $"0x{id:X}")} Landing Bounce to its original value (in-memory). Save Chunk to persist.");
    }
}
