using System.Numerics;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabAgent
{
    private const int MaxDamageHits = 0x40;

    private readonly List<AgentLabAgent> _damageHits = new();
    private uint _damageKind;

    public int CreatureHitPoints { get; private set; }

    private void InitCreatureHitPoints() => CreatureHitPoints = IntVariable((2u << 3) | 1u) & 0xFF;

    private void ReduceHitPoints(int amount)
    {
        if (ControllerType != 15)
        {
            World.Unimplemented("command", 528, "ReduceHitPoints on a non-creature");
            return;
        }
        int hp = CreatureHitPoints - amount;
        CreatureHitPoints = hp > -1 ? hp & 0xFF : 0;
        Trace($"ReduceHitPoints {amount} -> hp {CreatureHitPoints}");
    }

    public uint LastDamageKind => OwnCharacter is { } c ? c.LastDamage.Kind : _damageKind; // Amedo 2026-09-29

    // Amedo
    private void PushBodiesInSphere_2457F0(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        var m = LocalMatrix;
        var center = m.Translation;
        var offset = new Vector3(F(1), F(2), F(3));
        if (!(MathF.Abs(offset.X) <= 5e-5f && MathF.Abs(offset.Y) <= 5e-5f && MathF.Abs(offset.Z) <= 5e-5f))
            center += Vector3.TransformNormal(offset, m);
        float radius = F(4);
        bool lift = (Arg(7) & 0xFF) != 0;
        int found = 0;
        foreach (var o in World.QuerySphere(center, radius, 0x20, 1, this, boxes: false)) // Amedo
        {
            if (found++ >= 0x40) break;
            if (o is not AgentLabAgent hit || hit.Component5 is not { } body) continue;
            var d = hit.LocalMatrix.Translation - center;
            var j = d * InverseLength(d) * F(5);
            if (lift) j += d * ((F(6) - F(5)) / radius);
            body.ApplyImpulse_289CC0(j, lift ? new Vector3(0f, 3f, 0f) : Vector3.Zero);
        }
    }

    private void CreateDamage(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint flags = Arg(5);
        float radius = FloatValueOf(Arg(10));
        uint mask = (flags & 8) != 0 ? 0x5E000u : (flags & 0x10) != 0 ? 0x1000u : 0x5F000u;
        var m = LocalMatrix;
        Vector3 pos;
        if ((flags & 0x1FE0) != 0x1FE0)
        {
            if (!JointPosition_22DA00((int)((flags >> 5) & 0xFF), out pos)) pos = m.Translation; // Amedo
        }
        else
        {
            pos = m.Translation;
            if ((flags & 4) != 0) pos += Vector3.TransformNormal(new Vector3(F(1), F(2), F(3)), m);
        }
        uint shape = Arg(9);
        _damageHits.Clear();
        if ((shape & 0xF0) != 0xF0) World.Unimplemented("command", 514, "CreateDamage target selector (0x141658)");
        else
            switch (shape & 0xF)
            {
                case 1:
                    foreach (var o in World.QuerySphere(pos, radius, mask, 1, this))
                        if (o is AgentLabAgent hit && _damageHits.Count < MaxDamageHits) _damageHits.Add(hit);
                    break;
                case 2: DamagePoints(pos, radius, 0f, mask); break;
                case 3: DamagePoints(pos, radius, FloatValueOf(Arg(11)) * 0.5f, mask); break;
            }
        int amount = ValueOf(Arg(7)) & 0xFF;
        uint kind = Arg(6) | ((flags & 2) != 0 ? 0x400u : 0u);
        float strength = (flags & 1) != 0 ? FloatValueOf(Arg(8)) : 1f;
        Trace($"CreateDamage at {pos} r {radius} shape {shape & 0xF} mask 0x{mask:X} kind 0x{kind:X} amount {amount} -> {_damageHits.Count} hit(s) [{string.Join(", ", _damageHits.Select(h => h.Entity.Name))}]");
        if ((flags & 0x2000) != 0)
        {
            AgentLabAgent? nearest = null;
            float best = 1e30f;
            foreach (var h in _damageHits)
            {
                var (min, max) = h.TreeBox;
                float d = Vector3.DistanceSquared((min + max) * 0.5f, pos);
                if (d < best) { best = d; nearest = h; }
            }
            nearest?.ReceiveDamage(kind, amount, strength, this, true);
            return;
        }
        foreach (var h in _damageHits.ToList()) h.ReceiveDamage(kind, amount, strength, this, true);
    }

    private void DamagePoints(Vector3 pos, float radius, float halfHeight, uint mask)
    {
        if (halfHeight <= 0f) // Amedo
        {
            foreach (var o in World.QuerySphere(pos, radius, mask, 1, this, boxes: false))
                if (o is AgentLabAgent hit && _damageHits.Count < MaxDamageHits) _damageHits.Add(hit);
            return;
        }
        _damageHits.AddRange(World.QueryCylinder_1E9018(pos, radius, halfHeight, mask, 1, this, MaxDamageHits)); // Amedo
    }

    // Amedo
    private void HitInstancesInBoxes_10EE60(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        uint flags = Arg(0);
        uint mask = (flags & 2) != 0 ? 0x1000u : (flags & 1) != 0 ? 0x1A000u : 0x1B000u;
        if (Body() is not { Parts.Count: > 0 } body) return;
        var own = body.Parts.ToList();
        foreach (var (hull, m) in own)
        {
            var (min, max) = hull.Bounds(m);
            int found = 0;
            foreach (var other in World.Agents.ToList())
            {
                if (found >= 0x14) break;
                if (other.Destroyed || !other.Entity.Active || !ReferenceEquals(other.Chunk, Chunk)) continue;
                if ((other.AgentFlags & 1) != 0 || (other.ComponentMask & mask) == 0 || (other.AgentFlags & 8) != 8) continue;
                var (omin, omax) = other.TreeBox;
                if (!CollisionWorld.Overlap(min, max, omin, omax)) continue;
                if (other.Body() is not { } ob || !ob.Parts.Any(p => ConvexHull.Overlaps_200370(hull, m, p.Hull, p.World))) continue;
                found++;
                Trace($"HitInstancesInBoxes {other.Entity.Name} {((flags & 4) != 0 ? $"message 0x{Arg(2) & 0xFFFF:X}" : $"hit 0x{Arg(1):X}")}");
                if ((flags & 4) != 0) other.Deliver((ushort)(Arg(2) & 0xFFFF), this);
                else other.ReceiveDamage(Arg(1), 1, 1f, this, false);
            }
        }
    }

    internal void ReceiveDamage(uint kind, int amount, float strength, AgentLabAgent sender, bool created)
    {
        switch (ControllerType)
        {
            case 12:
                if (OwnCharacter is { } c) c.Damage(sender.GamePosition, kind, amount, sender); // Amedo 2026-09-29
                else World.Unimplemented("damage", 0x1375A0, "damage on a character without a controller");
                return;
            case 14:
                RecordDamage(kind, amount, sender);
                return;
            case 15:
                if (created && (AgentFlags & 0x8000) != 0) Touched_22FC98(0f, sender); // Amedo 2026-10-06
                if ((Motion10 & 0x400) == 0) RecordDamage(kind, amount, sender);
                return;
            case 13 or 16 or 17 or 18 or 19 or 20:
                if (created && (AgentFlags & 0x8000) != 0) Touched_22FC98(strength, sender); // Amedo 2026-10-05
                if (amount != 0) RecordDamage(kind, amount, sender);
                return;
        }
    }

    // Amedo
    private void MakeNoise_2549E0(float radius, float loudness)
    {
        var (min, max) = TreeBox;
        MakeNoise_209BC8(radius, loudness, (min + max) * 0.5f);
    }

    // Amedo
    internal void MakeNoise_209BC8(float radius, float loudness, Vector3 center)
    {
        var hits = World.QuerySphere(center, radius, NoiseMask_309D80, 1, this); // Amedo
        Trace($"noise r {radius} loudness {loudness} -> {hits.Count} hit(s)");
        for (int i = 0; i < hits.Count && i < 0x80; i++)
            if (hits[i] is AgentLabAgent hit) World.PostNoise_209900(hit, loudness, this);
    }

    // Amedo
    private ulong _node150;
    internal const uint NoiseMask_309D80 = 0x9000; // Amedo

    // Amedo
    private void SetNode150Fields_254B48(uint w)
    {
        if ((w & 0xFFFF) != 0xFFFF)
        {
            w |= 0x10000;
            _node150 = (_node150 & ~(0xFFFFUL << 42)) | ((ulong)(w & 0xFFFF) << 42);
        }
        _node150 = (_node150 & ~(1UL << 40)) | ((ulong)((w >> 16) & 1) << 40);
    }

    // Amedo
    internal void NoiseEvent_209A30(float loudness, AgentLabAgent? source)
    {
        if (_lookAt is { } look && (look.D0 & 0x20) == 0 && (look.D0 & 2) != 0 && source is not null) look.HearNoise_237380(loudness, source);
        if (((_node150 >> 40) & 1) != 0) World.PostMessage_197A30(this, (ushort)((_node150 >> 42) & 0xFFFF), source); // Amedo
    }

    private bool BoxAboveIsOverlapped()
    {
        var m = LocalMatrix;
        m.Translation += new Vector3(0f, _boxMax.Y + 0.01f - _boxMin.Y, 0f);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var c = new Vector3((i & 1) != 0 ? _boxMax.X : _boxMin.X, (i & 2) != 0 ? _boxMax.Y : _boxMin.Y, (i & 4) != 0 ? _boxMax.Z : _boxMin.Z);
            var w = Vector3.Transform(c, m);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
        foreach (var a in World.Agents)
        {
            if (ReferenceEquals(a, this) || a.Destroyed || !a.Entity.Active || (a.AgentFlags & 1) != 0) continue;
            var (bmin, bmax) = a.TreeBox;
            if (!CrashEngine.Runtime.Physics.CollisionWorld.Overlap(min, max, bmin, bmax)) continue;
            Trace($"box above {min}..{max} overlaps {a.Entity.Name} {bmin}..{bmax}");
            return true;
        }
        return false;
    }

    private void TriggerLinkedObjects(uint arg)
    {
        var sender = (arg & 1) != 0 ? Requester : this;
        int n = 0;
        foreach (var link in Links.ToList())
        {
            if (n++ >= 0x1F) break;
            if (link is null) continue;
            Trace($"TriggerLinkedObjects -> {link.Entity.Name} slot 1");
            link.TriggerEventSlot(1, sender);
        }
    }

    private void TriggerAllNitroCrates()
    {
        foreach (var o in World.QueryKinds_1F16B0(Chunk, 0x2000, 1, 0, true, 0x80)) // Amedo
            if (o is AgentLabAgent a) a.TriggerEventSlot(13, null);
    }

    private void TriggerBalancedCrateFalling()
    {
        var pos = GamePosition;
        var from = pos + new Vector3(0f, 0.1f, 0f);
        var top = pos.Y + 0.1f + 3.8f;
        var to = new Vector3(pos.X, top, pos.Z);
        _damageHits.Clear();
        float t = SegmentQuery(from, to, 0x2000, 0x10, 0x41, _damageHits, 0x20);
        Trace($"TriggerBalancedCrateFalling column -> {_damageHits.Count} crate(s), t {t}");
        if (0f <= t && t <= 1f)
        {
            float lowest = 1e30f;
            foreach (var h in _damageHits) lowest = MathF.Min(lowest, h.GamePosition.Y - top);
            lowest += 0.5f;
            foreach (var h in _damageHits.ToList())
            {
                if (!(h.GamePosition.Y - top < lowest)) continue;
                Trace($"  {h.Entity.Name} falls (0x140668)");
                h.StartCrateFall();
                h.AgentFlags |= 0x200;
            }
        }
        TouchAround_22E888(0f); // Amedo 2026-10-05
    }

    // Amedo 2026-10-05
    private void TouchAround_22E888(float half)
    {
        var (min, max) = TreeBox;
        if (0f < half)
        {
            var c = (min + max) * 0.5f;
            min = c - new Vector3(half);
            max = c + new Vector3(half);
        }
        else
        {
            min -= new Vector3(0.5f);
            max += new Vector3(0.5f);
        }
        foreach (var a in World.QueryBox_1ECFC0(min, max, PairMask_309D78, 1, 0x8010, true, this, 0x40))
            if (a.Pickup is null) a.Touched_22FC98(0f, this);
    }

    private float SegmentQuery(Vector3 from, Vector3 to, uint mask, uint require, uint exclude, List<AgentLabAgent> hits, int cap)
    {
        float best = 1e30f;
        var d = to - from;
        foreach (var a in World.SegmentCandidates_1E9168(from, to, Chunk)) // Amedo
        {
            if (hits.Count >= cap) break;
            if (ReferenceEquals(a, this) || a.Destroyed || !a.Entity.Active) continue;
            if ((a.AgentFlags & exclude) != 0 || (a.ComponentMask & mask) == 0 || (a.AgentFlags & require) != require) continue;
            var (bmin, bmax) = (a.Cell.BoxMin_40, a.Cell.BoxMax_50); // Amedo
            if (!SegmentBox(from, d, bmin, bmax, out float t)) continue;
            if (a.Body() is { Parts.Count: > 0 } body)
            {
                t = 1e30f;
                foreach (var (hull, m) in body.Parts)
                    if (SegmentHull(hull, m, from, to, out float th) && th < t) t = th;
                if (1f < t) continue;
            }
            hits.Add(a);
            if (t < best) best = t;
        }
        return best;
    }

    private static bool SegmentBox(Vector3 p, Vector3 d, Vector3 min, Vector3 max, out float t)
    {
        float t0 = 0f, t1 = 1f;
        t = 0f;
        for (int k = 0; k < 3; k++)
        {
            float o = k == 0 ? p.X : k == 1 ? p.Y : p.Z;
            float v = k == 0 ? d.X : k == 1 ? d.Y : d.Z;
            float lo = k == 0 ? min.X : k == 1 ? min.Y : min.Z;
            float hi = k == 0 ? max.X : k == 1 ? max.Y : max.Z;
            if (MathF.Abs(v) < 1e-8f)
            {
                if (o < lo || hi < o) return false;
                continue;
            }
            float a = (lo - o) / v, b = (hi - o) / v;
            if (b < a) (a, b) = (b, a);
            t0 = MathF.Max(t0, a);
            t1 = MathF.Min(t1, b);
            if (t1 < t0) return false;
        }
        t = t0;
        return true;
    }

    private static bool SegmentHull(CrashEngine.Runtime.Physics.ConvexHull hull, Matrix4x4 m, Vector3 from, Vector3 to, out float t)
    {
        t = 0f;
        if (!Matrix4x4.Invert(m, out var inv)) return false;
        var p = Vector3.Transform(from, inv);
        var d = Vector3.Transform(to, inv) - p;
        float t0 = 0f, t1 = 1f;
        foreach (var f in hull.Faces)
        {
            var n = new Vector3(f.X, f.Y, f.Z);
            float num = -(Vector3.Dot(n, p) + f.W);
            float den = Vector3.Dot(n, d);
            if (MathF.Abs(den) < 1e-8f)
            {
                if (num < 0f) return false;
                continue;
            }
            float s = num / den;
            if (den < 0f) t0 = MathF.Max(t0, s);
            else t1 = MathF.Min(t1, s);
            if (t1 < t0) return false;
        }
        t = t0;
        return true;
    }

    // Amedo 2026-09-30
    private void ClearDamage_220B18()
    {
        if (OwnCharacter is { } c)
        {
            c.LastDamage.Kind = 0;
            c.LastDamage.Amount = 0;
            c.LastDamage.Source = new Vector4(0f, 0f, 0f, c.LastDamage.Source.W);
            return;
        }
        _damageKind = 0;
    }

    // Amedo 2026-09-30
    private void SurfaceEvent_28B918(CollisionWorld.Surface s)
    {
        if (_physics is not { } ctrl) return;
        Trace($"surface event {s.Id} (kind 0x{s.HitKind:X}, damage {s.HitAmount})");
        ReceiveDamage(s.HitKind, s.HitAmount, 1f, this, false);
        ctrl.State |= PhysicsController.StateSurfaceEvent;
    }

    private void RecordDamage(uint kind, int amount, AgentLabAgent sender)
    {
        _damageKind = kind;
        Trace($"damaged by {sender.Entity.Name} (kind 0x{kind:X}, amount {amount}) -> slot 2");
        TriggerEventSlot(2, sender);
    }
}
