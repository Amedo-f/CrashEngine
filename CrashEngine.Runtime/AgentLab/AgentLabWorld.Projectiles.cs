using System.Numerics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-10-06
public sealed partial class AgentLabWorld
{
    private readonly Dictionary<int, PS2BehaviourCommandsSequence?> _projectileSequences = new();
    private readonly Dictionary<int, ProjectileAgent.Settings> _projectileSettings = new();

    private PS2BehaviourCommandsSequence? ProjectileSequence(int global)
    {
        if (_projectileSequences.TryGetValue(global, out var found)) return found;
        found = null;
        foreach (var rm in AllRm2())
        {
            var sec = rm?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                        ?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);
            for (int i = 0; sec is not null && i < sec.GetItemsAmount(); i++)
                if (sec.GetItem(i) is PS2BehaviourCommandsSequence s && s.Key == ITwinBehaviourCommandsSequence.InstanceType.Projectile
                    && s.IndexInGlobalStorage == global)
                {
                    found = s;
                    break;
                }
            if (found is not null) break;
        }
        _projectileSequences[global] = found;
        return found;
    }

    public ProjectileAgent.Settings ProjectileSettings_10C448(int global)
    {
        if (_projectileSettings.TryGetValue(global, out var s)) return s;
        float speed = 0f, time = 0f, turnH = 0f, turnV = 0f, gravity = 0f;
        uint flags = 0;
        if (ProjectileSequence(global) is { } seq)
            foreach (var c in seq.Commands)
            {
                if (c is not PS2BehaviourCommand cmd || cmd.CommandIndex != 540) continue;
                var a = cmd.Arguments;
                uint W(int i) => i < a.Count ? a[i] : 0u;
                float F(int i) => BitConverter.UInt32BitsToSingle(W(i));
                uint f = W(0);
                speed = F(1);
                if ((f & 0x20) != 0)
                {
                    uint g = W(5);
                    gravity = ((g >> 1) & 3) == 2 && (g & 1) == 0 ? BitConverter.Int32BitsToSingle((int)(g & ~7u)) : 0f;
                    flags |= ProjectileAgent.Gravity;
                }
                if ((f & 0x10) != 0)
                {
                    turnH = F(2);
                    turnV = F(3);
                    flags |= ProjectileAgent.Homing;
                }
                if ((f & 0x40) != 0)
                {
                    time = F(4);
                    flags |= ProjectileAgent.HomingTimed;
                }
                if ((f & 0x80) != 0) flags |= ProjectileAgent.CharacterGun;
            }
        s = new(10f, speed, time, turnH, turnV, gravity, flags);
        _projectileSettings[global] = s;
        return s;
    }

    public IReadOnlyList<ITwinBehaviourCommand>? ProjectilePack(int global, int state)
    {
        if (ProjectileSequence(global) is not { } seq) return null;
        ushort id = (ushort)(ProjectileAgent.FirstId + state * 2);
        foreach (var kv in seq.BehaviourPacks)
            if (kv.Key == id) return kv.Value.Commands;
        return null;
    }

    public AgentLabAgent? SegmentAgents_1E9168(Vector3 a, Vector3 b, uint mask, AgentLabAgent self, AgentLabAgent? parent, ref float best)
    {
        var qmin = Vector3.Min(a, b);
        var qmax = Vector3.Max(a, b);
        var chunk = ChunkOf(self);
        AgentLabAgent? hit = null;
        foreach (var o in Agents)
        {
            if (ReferenceEquals(o, self) || ReferenceEquals(o, parent) || o.Destroyed || !o.Entity.Active || !ReferenceEquals(o.Chunk, chunk)) continue;
            if ((o.AgentFlags & 1) != 0 || (o.ComponentMask & mask) == 0 || (o.AgentFlags & 0x10) == 0) continue;
            var (bmin, bmax) = o.TreeBox;
            if (!Physics.CollisionWorld.Overlap(qmin, qmax, bmin, bmax) || o.Body() is not { } body) continue;
            float near = 1e30f;
            foreach (var (hull, m) in body.Parts)
            {
                if (!Matrix4x4.Invert(m, out var inv)) continue;
                if (Physics.HullQueries.SegmentHull_1FC470(hull, Vector3.Transform(a, inv), Vector3.Transform(b, inv), out float t) && t < near) near = t;
            }
            if (near == 1e30f || best <= near) continue;
            best = near;
            hit = o;
        }
        return hit;
    }
}
