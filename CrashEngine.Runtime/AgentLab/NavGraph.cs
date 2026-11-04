using System.Numerics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed class NavNode
{
    public int Layout { get; init; }
    public uint Id { get; init; }
    public Vector3 Position { get; set; } // Amedo
    public float Radius { get; init; }
    public ushort Flags { get; set; } // Amedo
    public const ushort Attached_30A71C = 0x20; // Amedo
    public int Index { get; init; } // Amedo
    public int Stamp; // Amedo
    public byte Occupancy; // Amedo
    public ushort BackNav_18, BackPos_1A = 0xFF; // Amedo
    public List<NavPath> Paths { get; } = new(); // Amedo

    // Amedo
    public NavPath? PathTo_23C710(ushort nav, ushort pos)
    {
        foreach (var path in Paths)
            if (path.NavA == nav && path.A == pos || path.NavB == nav && path.B == pos) return path;
        return null;
    }
}

// Amedo
public sealed class NavPath
{
    public ushort A { get; init; }
    public ushort B { get; init; }
    public ushort Flags { get; init; }
    public ushort NavA, NavB;
}

// Amedo 2026-09-27
public sealed class NavGraph
{
    public List<NavNode> Nodes { get; } = new();
    public List<NavPath> AiPaths { get; } = new(); // Amedo
    public int Index { get; set; } = -1; // Amedo
    public int EdgeCount => AiPaths.Count; // Amedo

    public NavGraph(PS2AnyTwinsanityRM2 rm2)
    {
        var byId = new Dictionary<(int, uint), NavNode>();
        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
            var positions = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_AI_POSITIONS_SECTION);
            if (positions is null) continue;
            for (int i = 0; i < positions.GetItemsAmount(); i++)
            {
                if (positions.GetItem(i) is not PS2AnyAIPosition p) continue;
                var node = new NavNode
                {
                    Layout   = lid,
                    Id       = p.GetID(),
                    Position = new Vector3(p.Position.X, p.Position.Y, p.Position.Z),
                    Radius   = p.Position.W,
                    Flags    = p.UnkShort, // Amedo 2026-10-05
                    Index    = Nodes.Count, // Amedo
                };
                Nodes.Add(node);
                byId[(lid, node.Id)] = node;
            }
            var paths = layout!.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_AI_PATHS_SECTION);
            if (paths is null) continue;
            for (int i = 0; i < paths.GetItemsAmount(); i++)
            {
                if (paths.GetItem(i) is not PS2AnyAIPath e || e.Args.Length < 2) continue;
                if (!byId.TryGetValue((lid, e.Args[0]), out var a) || !byId.TryGetValue((lid, e.Args[1]), out var b)) continue;
                AiPaths.Add(new NavPath { A = (ushort)a.Index, B = (ushort)b.Index, Flags = e.Args.Length > 2 ? e.Args[2] : (ushort)0 }); // Amedo
            }
        }
    }

    public NavNode? Nearest_23BAF8(Vector3 point) // Amedo
    {
        NavNode? best = null;
        float bestSq = 1e30f;
        foreach (var n in Nodes)
        {
            float x = point.X - n.Position.X, y = point.Y - n.Position.Y, z = point.Z - n.Position.Z;
            float dSq = x * x + y * y + z * z;
            if (dSq < bestSq) { bestSq = dSq; best = n; }
        }
        return best;
    }

    // Amedo 2026-10-05
    public NavNode? Nearest_23BC80(Vector3 point, ushort require, ushort exclude)
    {
        NavNode? best = null;
        float bestSq = 1e30f;
        foreach (var n in Nodes)
        {
            if (require != 0 && (n.Flags & require) == 0) continue;
            if ((n.Flags & exclude) != 0) continue;
            float dSq = GameMath.DistanceSq(point, n.Position);
            if (dSq < bestSq) { bestSq = dSq; best = n; }
        }
        return best;
    }
}
