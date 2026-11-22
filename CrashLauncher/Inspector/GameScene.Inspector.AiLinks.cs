using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-19
public sealed partial class GameScene
{
    private static readonly Vector4 AiLinkNoteColor = new(1f, 0.6f, 0.2f, 1f);

    private void DrawInspectorAiLinks(Entity e)
    {
        if (e.Get<AiPositionMarker>() is null) return;
        ImGui.Separator();
        if (!ImGui.CollapsingHeader("AI Links##ailinks", ImGuiTreeNodeFlags.DefaultOpen)) return;
        DrawAiLayoutWarning();
        var chain = SelectedAiPositions();
        if (chain.Count >= 2) DrawAiChain(chain);
        else DrawAiPointLinks(e);
    }

    private bool _aiLinkWorm; // Amedo 2026-11-21

    private void DrawAiChain(List<Entity> chain)
    {
        var linked = new HashSet<(Entity, Entity)>();
        var flagsOf = new Dictionary<(Entity, Entity), ushort>(); // Amedo 2026-11-21
        foreach (var (_, path, a, b) in AiLinks()) { linked.Add((a, b)); linked.Add((b, a)); flagsOf[(a, b)] = flagsOf[(b, a)] = path.Args[2]; }
        int toMake = 0, existing = 0, worm = 0;
        for (int i = 0; i < chain.Count; i++)
        {
            ImGui.TextUnformatted($"{i + 1}. {chain[i].Name}");
            if (i == 0) continue;
            string? note = null;
            if (flagsOf.TryGetValue((chain[i - 1], chain[i]), out var f)) // Amedo 2026-11-21
            {
                existing++;
                if ((f & AiLinkWormTunnel) != 0) worm++;
                note = f == 0 ? "already linked to the one before" : $"already linked to the one before, flags 0x{f:X}";
            }
            else if (!ReferenceEquals(AiLayoutOf(chain[i - 1].Get<AiPositionMarker>()!.Source), AiLayoutOf(chain[i].Get<AiPositionMarker>()!.Source))) note = "in another layout";
            else toMake++;
            if (note is null) continue;
            ImGui.SameLine();
            ImGui.TextColored(AiLinkNoteColor, $"({note})");
        }
        ImGui.Checkbox("Worm tunnel (0x40)##ailinkworm", ref _aiLinkWorm); // Amedo 2026-11-21
        ImGui.SameLine();
        ImGui.TextDisabled("worms move underground only along these");
        ImGui.BeginDisabled(toMake == 0);
        if (ImGui.Button(chain.Count == 2 ? "Link##ailinkgo" : $"Link in this order ({toMake})##ailinkgo", new Vector2(-1f, 0f))) LinkAiChain(chain, _aiLinkWorm ? AiLinkWormTunnel : (ushort)0);
        ImGui.EndDisabled();
        if (existing > 0) // Amedo 2026-11-21
        {
            if (worm < existing && ImGui.Button($"Make the {existing - worm} existing link(s) worm tunnels##ailinkwormset", new Vector2(-1f, 0f))) SetAiChainFlag(chain, AiLinkWormTunnel, true);
            if (worm > 0 && ImGui.Button($"Make the {worm} worm tunnel link(s) normal##ailinkwormclr", new Vector2(-1f, 0f))) SetAiChainFlag(chain, AiLinkWormTunnel, false);
        }
    }

    private void DrawAiPointLinks(Entity e)
    {
        int n = 0;
        foreach (var (section, path, a, b) in AiLinks().ToList())
        {
            var other = ReferenceEquals(a, e) ? b : ReferenceEquals(b, e) ? a : null;
            if (other is null) continue;
            n++;
            ImGui.TextUnformatted($"- {other.Name}" + (path.Args[2] != 0 ? $"  (flags 0x{path.Args[2]:X})" : ""));
            ImGui.SameLine();
            bool isWorm = (path.Args[2] & AiLinkWormTunnel) != 0; // Amedo 2026-11-21
            if (ImGui.Checkbox($"worm##ailinkw{n}", ref isWorm))
            {
                var rec = new LayoutEditRecorder();
                SetAiLinkFlag(rec, path, AiLinkWormTunnel, isWorm);
                _undoStack.Push(rec.Finish(e));
                _browser.Log($"AI link {e.Name} - {other.Name}: {(isWorm ? "worm tunnel" : "normal")} (flags 0x{path.Args[2]:X}). Save Chunk to keep.");
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"Select##ailinksel{n}")) SelectClicked(other, false);
            ImGui.SameLine();
            if (ImGui.SmallButton($"Delete##ailinkdel{n}"))
            {
                var rec = new LayoutEditRecorder();
                DeleteAiPath(rec, section, path);
                _undoStack.Push(rec.Finish(e));
                _browser.Log($"AI link {e.Name} - {other.Name} deleted. Save Chunk to keep.");
            }
        }
        if (n == 0) ImGui.TextDisabled("No links.");
        ImGui.TextDisabled("Shift+click more AI positions to link them in order.");
    }

    // Amedo 2026-11-15
    private void DrawAiLayoutWarning()
    {
        if (Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2 is not { } rm2) return;
        var inUse = AiLayoutsInUse(rm2);
        if (inUse.Count < 2) return;
        ImGui.TextColored(AiLinkNoteColor, $"AI points are split over {inUse.Count} layouts: the game only reads one network.");
        if (ImGui.SmallButton("Merge them into the main network"))
        {
            var rec = new LayoutEditRecorder();
            int moved = MergeAiLayouts(rec);
            _undoStack.Push(rec.Finish(null));
            _browser.Log($"AI: moved {moved} AI point(s) and their links into the main network layout. Link them to it, then Save Chunk.");
        }
    }
}
