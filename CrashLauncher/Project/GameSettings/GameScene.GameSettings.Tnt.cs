using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private const uint TntCountdownBehaviourId = 0x3F;
    private const int  ControlPacketDelayIndex = (int)TwinBehaviourControlPacket.ControlPacketData.Delay;

    private static float? GetDelay(TwinBehaviourControlPacket packet)
    {
        if (packet.Bytes.Count <= ControlPacketDelayIndex) return null;
        var floatIdx = packet.Bytes[ControlPacketDelayIndex];
        if (floatIdx == 0xFF || floatIdx >= 0x80 || floatIdx >= packet.Floats.Count) return null;
        return BitConverter.UInt32BitsToSingle(packet.Floats[floatIdx]);
    }

    private static void SetDelay(TwinBehaviourControlPacket packet, float value)
    {
        if (packet.Bytes.Count <= ControlPacketDelayIndex) return;
        var floatIdx = packet.Bytes[ControlPacketDelayIndex];
        if (floatIdx == 0xFF || floatIdx >= 0x80 || floatIdx >= packet.Floats.Count) return;
        packet.Floats[floatIdx] = BitConverter.SingleToUInt32Bits(value);
    }

    private static List<TwinBehaviourControlPacket> FindTntDelayPackets(PS2BehaviourGraph graph)
    {
        var found = new List<TwinBehaviourControlPacket>();
        foreach (var state in graph.ScriptStates)
            if (state.ControlPacket is { } cp && GetDelay(cp) is not null)
                found.Add(cp);
        return found;
    }

    private List<float>? _tntOriginalDelays;
    private float _tntSpeedMultiplier = 1f;
    private bool _tntMultiplierActive;

    private void DrawTntCountdownSpeed(Entity e, ChunkSource chunkSource, Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject obj) // Amedo
    {
        var tntGraph = ObjectGraph(chunkSource, obj, TntCountdownBehaviourId, out bool own);
        if (tntGraph is null)
        {
            ImGui.TextDisabled("Couldn't find the TNT countdown behaviour (0x3F) in Startup\\Default.rm2.");
            return;
        }

        var packets = FindTntDelayPackets(tntGraph);
        if (packets.Count == 0)
        {
            ImGui.TextDisabled("No Delay-bearing ControlPackets found in COM_TNT_CRATE_COUNTDOWN.");
            return;
        }

        if (_tntOriginalDelays is null || _tntOriginalDelays.Count != packets.Count)
        {
            _tntOriginalDelays = LoadTntOriginalDelays(packets.Count) ?? packets.Select(p => GetDelay(p) ?? 0f).ToList();
        }

        if (!_tntMultiplierActive && _tntOriginalDelays.Count > 0 &&
            GetDelay(packets[0]) is { } liveDelay0 && liveDelay0 != 0f)
            _tntSpeedMultiplier = _tntOriginalDelays[0] / liveDelay0;

        ImGui.Text("TNT Countdown Speed");
        if (DrawScriptScope(e, own, "every TNT crate")) return; // Amedo
        bool changed = ImGui.SliderFloat("Speed Multiplier##tntspeed", ref _tntSpeedMultiplier, 0.1f, 5f, "%.2fx");
        _tntMultiplierActive = ImGui.IsItemActive();
        if (changed)
        {
            for (int i = 0; i < packets.Count; i++)
                SetDelay(packets[i], _tntOriginalDelays[i] / _tntSpeedMultiplier);
            if (!own) chunkSource.GlobalRm2Dirty = true; // Amedo
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset##tntreset"))
        {
            _tntSpeedMultiplier = 1f;
            ResetTntCountdownToOriginal(chunkSource, tntGraph);
        }

        if (ImGui.TreeNode("Individual delays (advanced)##tntdelays"))
        {
            for (int i = 0; i < packets.Count; i++)
            {
                float d = GetDelay(packets[i]) ?? 0f;
                if (ImGui.InputFloat($"Delay {i}##tntdelay{i}", ref d, 0.01f, 0.1f))
                    SetDelay(packets[i], MathF.Max(0f, d));
            }
            ImGui.TreePop();
        }
    }

    private List<float>? LoadTntOriginalDelays(int expectedCount)
    {
        try
        {
            var freshBehSec = MeshDecoder.CodeSection(ReadPristineRm2(GlobalRm2Path), TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshGraph = freshBehSec?.GetItem<PS2BehaviourGraph>(TntCountdownBehaviourId);
            if (freshGraph is null) return null;

            var delays = FindTntDelayPackets(freshGraph).Select(p => GetDelay(p) ?? 0f).ToList();
            return delays.Count == expectedCount ? delays : null;
        }
        catch { return null; }
    }

    private void ResetTntCountdownToOriginal(ChunkSource chunkSource, PS2BehaviourGraph liveGraph)
    {
        try
        {
            var freshBehSec = MeshDecoder.CodeSection(ReadPristineRm2(GlobalRm2Path), TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshGraph = freshBehSec?.GetItem<PS2BehaviourGraph>(TntCountdownBehaviourId);
            if (freshGraph is null)
            {
                _browser.Log("Reset TNT Countdown Speed: couldn't find the original behaviour on disc.");
                return;
            }

            var liveStates = liveGraph.ScriptStates;
            var freshStates = freshGraph.ScriptStates;
            for (int i = 0; i < liveStates.Count && i < freshStates.Count; i++)
            {
                if (liveStates[i].ControlPacket is not { } liveCp) continue;
                if (freshStates[i].ControlPacket is not { } freshCp) continue;
                if (GetDelay(freshCp) is { } origDelay)
                    SetDelay(liveCp, origDelay);
            }
            chunkSource.GlobalRm2Dirty = true;
            _browser.Log("Reset TNT Countdown Speed to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset TNT Countdown Speed failed: {ex.Message}"); }
    }
}
