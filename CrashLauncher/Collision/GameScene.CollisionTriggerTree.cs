using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private sealed class TriggerTreeNode
    {
        public bool IsLeaf;
        public TriggerTreeNode? Left;
        public TriggerTreeNode? Right;
        public int Id;
        public int LeafPtr;
    }

    internal static List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTrigger> BuildTriggerTree(
        List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation> groups,
        List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle> tris,
        List<TwinVec4> verts)
    {
        var result = new List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTrigger>();
        int n = groups.Count;
        if (n == 0) return result;

        var root = new TriggerTreeNode { IsLeaf = true };
        var leaves = new List<TriggerTreeNode> { root };
        int fullLevels = (int)Math.Truncate(Math.Log(n, 2));
        for (int lvl = 0; lvl < fullLevels; lvl++)
        {
            var next = new List<TriggerTreeNode>(leaves.Count * 2);
            foreach (var leaf in leaves)
            {
                leaf.IsLeaf = false;
                leaf.Left  = new TriggerTreeNode { IsLeaf = true };
                leaf.Right = new TriggerTreeNode { IsLeaf = true };
                next.Add(leaf.Left);
                next.Add(leaf.Right);
            }
            leaves = next;
        }
        int endings = n - (int)Math.Pow(2, fullLevels);
        for (int i = 0; i < endings; i++)
        {
            var leaf = leaves[i];
            leaf.IsLeaf = false;
            leaf.Left  = new TriggerTreeNode { IsLeaf = true };
            leaf.Right = new TriggerTreeNode { IsLeaf = true };
        }

        int leafPtr = 1;
        int AssignIds(TriggerTreeNode node, int id)
        {
            node.Id = id;
            if (node.IsLeaf) { node.LeafPtr = leafPtr++; return 1; }
            int leftSize = AssignIds(node.Left!, id + 1);
            int rightSize = AssignIds(node.Right!, id + 1 + leftSize);
            return 1 + leftSize + rightSize;
        }
        AssignIds(root, 0);

        var flat = new Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTrigger[2 * n - 1];
        void Flatten(TriggerTreeNode node)
        {
            var trig = new Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTrigger();
            if (node.IsLeaf)
            {
                trig.MinTriggerIndex = -node.LeafPtr;
                trig.MaxTriggerIndex = -node.LeafPtr;
            }
            else
            {
                trig.MinTriggerIndex = node.Left!.Id;
                trig.MaxTriggerIndex = node.Right!.Id;
            }
            flat[node.Id] = trig;
            if (!node.IsLeaf) { Flatten(node.Left!); Flatten(node.Right!); }
        }
        Flatten(root);

        void Recalculate(int idx)
        {
            var t = flat[idx];
            if (t.MinTriggerIndex >= 0)
            {
                Recalculate(t.MinTriggerIndex);
                Recalculate(t.MaxTriggerIndex);
                var l = flat[t.MinTriggerIndex]; var r = flat[t.MaxTriggerIndex];
                t.V1 = new Twinsanity.TwinsanityInterchange.Common.Vector3(MathF.Min(l.V1.X, r.V1.X), MathF.Min(l.V1.Y, r.V1.Y), MathF.Min(l.V1.Z, r.V1.Z));
                t.V2 = new Twinsanity.TwinsanityInterchange.Common.Vector3(MathF.Max(l.V2.X, r.V2.X), MathF.Max(l.V2.Y, r.V2.Y), MathF.Max(l.V2.Z, r.V2.Z));
            }
            else
            {
                int groupIdx = -t.MinTriggerIndex - 1;
                var g = groups[groupIdx];
                float x1 = float.MaxValue, y1 = float.MaxValue, z1 = float.MaxValue;
                float x2 = float.MinValue, y2 = float.MinValue, z2 = float.MinValue;
                for (uint k = g.Offset; k < g.Offset + g.Size; k++)
                {
                    var tr = tris[(int)k];
                    foreach (var vi in new[] { tr.Vector1Index, tr.Vector2Index, tr.Vector3Index })
                    {
                        var v = verts[vi];
                        x1 = MathF.Min(x1, v.X); y1 = MathF.Min(y1, v.Y); z1 = MathF.Min(z1, v.Z);
                        x2 = MathF.Max(x2, v.X); y2 = MathF.Max(y2, v.Y); z2 = MathF.Max(z2, v.Z);
                    }
                }
                t.V1 = new Twinsanity.TwinsanityInterchange.Common.Vector3(x1, y1, z1);
                t.V2 = new Twinsanity.TwinsanityInterchange.Common.Vector3(x2, y2, z2);
            }
        }
        Recalculate(0);

        result.AddRange(flat);
        return result;
    }
}
