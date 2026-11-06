using System.Numerics;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed class SceneryTree
{
    public sealed class Node
    {
        public Vector3 Min;
        public Vector3 Max;
        public readonly Node?[] Kids = new Node?[8];
        public readonly List<AgentLabAgent> Bodies = new();
    }

    public Node? Root { get; }
    public int NodeCount { get; }

    public SceneryTree(PS2AnyTwinsanitySM2? sm2)
    {
        if (sm2?.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM) is not { Sceneries.Count: > 0 } scenery) return;
        int index = 0, count = 0;
        Node Build()
        {
            var src = scenery.Sceneries[index++];
            var node = new Node
            {
                Min = new Vector3(src.UnkVec2.X, src.UnkVec2.Y, src.UnkVec2.Z),
                Max = new Vector3(src.UnkVec3.X, src.UnkVec3.Y, src.UnkVec3.Z),
            };
            count++;
            if (src is TwinSceneryNode branch)
                for (int i = 0; i < 8 && i < branch.SceneryTypes.Length; i++)
                    if (branch.SceneryTypes[i] is ITwinScenery.SceneryType.Node or ITwinScenery.SceneryType.Leaf && index < scenery.Sceneries.Count)
                        node.Kids[i] = Build();
            return node;
        }
        Root      = Build();
        NodeCount = count;
    }

    public static int Contain(Node node, Vector3 min, Vector3 max, float eps = 4.99999987e-05f) // Amedo
    {
        bool full = true;
        for (int k = 0; k < 3; k++)
        {
            float size = max[k] - min[k];
            float overlap = min[k] < node.Max[k] && node.Min[k] < max[k]
                ? MathF.Abs(MathF.Min(min[k], node.Min[k]) - MathF.Max(max[k], node.Max[k]))
                : 0f;
            if (overlap <= eps * size) return 0;
            if (size * (1f - eps) > overlap) full = false;
        }
        return full ? 1 : 2;
    }

    // Amedo
    public static bool BoxContainsRegion_1FF950(Node node, Vector3 min, Vector3 max)
    {
        for (int k = 0; k < 3; k++)
            if (min[k] <= node.Min[k] || node.Max[k] <= max[k]) return false;
        return true;
    }

    public Node? Find(Vector3 min, Vector3 max)
    {
        if (Root is null || Contain(Root, min, max) == 0) return null;
        var node   = Root;
        var center = (min + max) * 0.5f;
        while (true)
        {
            Node? best = null;
            float bestDistance = 1e11f; // Amedo
            foreach (var kid in node.Kids)
            {
                if (kid is null || !BoxContainsRegion_1FF950(kid, min, max)) continue; // Amedo
                var c = (kid.Min + kid.Max) * 0.5f;
                float dx = c.X - center.X, dy = c.Y - center.Y, dz = c.Z - center.Z;
                float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                if (!(distance < bestDistance)) continue;
                best = kid;
                bestDistance = distance;
            }
            if (best is null) return node;
            node = best;
        }
    }

    internal static int Test(Vector4[] planes, Node node) // Amedo
    {
        var c = (node.Min + node.Max) * 0.5f;
        var h = (node.Max - node.Min) * 0.5f;
        bool inside = true;
        foreach (var p in planes)
        {
            float dist = p.X * c.X + p.Y * c.Y + p.Z * c.Z + p.W;
            float r = MathF.Abs(p.X) * h.X + MathF.Abs(p.Y) * h.Y + MathF.Abs(p.Z) * h.Z;
            if (dist + r < 0f) return 0;
            if (dist - r < 0f) inside = false;
        }
        return inside ? 1 : 2;
    }
}
