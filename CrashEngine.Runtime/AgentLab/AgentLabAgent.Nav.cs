using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-10-05
public sealed partial class AgentLabAgent
{
    private void FocusNearNav_113150(uint arg0)
    {
        int code = (int)(arg0 & 0xFF);
        var target = code == SourceNone ? World.Player : Participant(code);
        if (target is null) return;
        var pos = target.GamePosition;
        var node = target.Chunk.Nav.Nearest_23BC80(pos, 0, 0x10);
        if (node is null) pos.Z += 0.1f;
        else if ((node.Flags & 4) != 0 || Vector3.Distance(node.Position, pos) < 30f) pos = node.Position + new Vector3(0f, 0.5f, 0f);
        FocusAt(pos);
    }

    private void PointToward_214CD0(uint w, float distance)
    {
        int dst = (int)(w & 0xFF), from = (int)((w >> 8) & 0xFF), to = (int)((w >> 16) & 0xFF);
        Vector3 a;
        if (CodeObject(from) is { } fromObj) a = fromObj.GamePosition;
        else if (!CodePosition(from, out a) && ((w >> 24) & 0xF) == 2) a = GamePosition;
        Vector3 b;
        if (CodeObject(to) is { } toObj) b = toObj.GamePosition;
        else CodePosition(to, out b);
        var d = b - a;
        var p = a + d * (InverseLength(d) * distance);
        Trace($"point toward 0x{to:X2} from 0x{from:X2}: {a} -> {p} into 0x{dst:X2}");
        SetCodePosition(dst, p);
    }

    private float NavDistance_225450()
    {
        var pos = GamePosition;
        if (Chunk.Nav.Nearest_23BAF8(pos) is not { } node) return 1e30f;
        float d = Vector3.Distance(pos, node.Position) - node.Radius;
        Trace($"nav distance² {d * d:0.##} (node {node.Id} at {node.Position} r {node.Radius:0.##}, {Chunk.Nav.Nodes.Count} nodes)");
        return d * d;
    }

    private bool PartnerDead_128C00()
    {
        if (World.PlayerSlot_570(1)?.OwnCharacter is not { Dead: true }) return false;
        int slot = (int)(World.Progression >> 8) & 0xF;
        return slot != 6 && World.PlayerSlot_570(slot)?.OwnCharacter is { Dead: false };
    }

    // Amedo
    private void LinkToNearestPoint_219BE8()
    {
        var me = GamePosition;
        if (Chunk.Nav?.Nearest_23BAF8(me) is not { } node) return;
        float dx = me.X - node.Position.X, dy = me.Y - node.Position.Y, dz = me.Z - node.Position.Z;
        if (!(dx * dx + dy * dy + dz * dz < 100f)) return;
        AttachmentsOf_196E20(); // Amedo
        if (AttachToAiPosition_196008(node)) Trace($"AI position {node.Index} rides on me");
    }

    private void ReleaseCharacter_122610() => (OwnCharacter ?? World.PlayerCharacter)?.Release_132FB0();

    // Amedo 2026-10-06
    private void DismissCharacter_1229F8(uint slot)
    {
        if (slot == 6 || World.PlayerSlot_570((int)slot) is not { Destroyed: false } who || who.Places is not { } places) return; // Amedo
        Trace($"dismiss character {who.Entity.Name} (slot {slot}): asleep (0x198288)");
        places.KeepLoaded_197E98(World);
        who.Sleep_197508();
    }

    private void Revive_122D08()
    {
        if ((OwnCharacter ?? World.PlayerCharacter) is { } c) c.State70 &= ~Character.CharacterController.DeadFlag;
    }

    private void WarpToPlayerChunk_11A928()
    {
        if (OwnCharacter is null || World.Player is not { } player || ReferenceEquals(Chunk, player.Chunk)) return;
        if (World.ChunkLink_1F1CC0?.Invoke(Chunk, player.Chunk) is not { Wall: { } wall } link || (link.Flags & Streaming.StreamLink.TargetReady) == 0) return;
        var center = (wall.Corners[0] + wall.Corners[1] + wall.Corners[2] + wall.Corners[3]) * 0.25f;
        var pos = center + new Vector3(wall.Plane.X, wall.Plane.Y, wall.Plane.Z) * 0.5f;
        if (pos != GamePosition) SetLocalPosition(pos);
        KeepWarp(pos);
        Trace($"warp to the {player.Chunk.Name} wall at {pos}");
    }
}
