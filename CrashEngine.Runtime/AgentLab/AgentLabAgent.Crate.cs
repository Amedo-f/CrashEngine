using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabAgent
{
    private const float CrateGravity = 50f;

    private int _crateMode;
    private int _crateRequest = 3;
    private float _crateVy;
    private bool _crateFalling;
    private bool _crateOnGround;
    private readonly List<AgentLabAgent> _crateHits = new();
    private int _crateWumpa; // Amedo

    internal bool HasCrateNode => (ComponentMask & (1u << 13)) != 0; // Amedo
    internal bool HasPickupNode => (ComponentMask & (1u << 14)) != 0; // Amedo

    // Amedo
    private void SetCrate_122EE8(uint arg1)
    {
        if ((int)arg1 < 0 || !HasCrateNode) return;
        _crateWumpa = (int)(arg1 & 0xFF);
        Trace($"crate wumpa {_crateWumpa} (SetCrate)");
    }

    // Amedo
    internal void TakeCrateWumpa_122550(int n)
    {
        if (HasPickupNode || !HasCrateNode) return;
        _crateWumpa = (uint)_crateWumpa < (uint)n ? 0 : (_crateWumpa - n) & 0xFF;
        Trace($"crate wumpa {_crateWumpa} (-{n})");
    }

    private void CrateStep(float dt)
    {
        if (_crateRequest != 3)
        {
            _crateMode = _crateRequest;
            _crateRequest = 3;
        }
        int result = -1;
        switch (_crateMode)
        {
            case 0:
                CrateSettle();
                result = 1;
                break;
            case 2:
                bool falling = CrateFall(dt);
                if (!falling)
                {
                    Trace($"crate landed at {GamePosition} -> slot 12");
                    TriggerEventSlot(12, null);
                    result = 1;
                }
                AgentFlags = falling ? AgentFlags | 0x1000 : AgentFlags & ~0x1000u;
                break;
        }
        if (result != -1) _crateRequest = result & 0xF;
    }

    private void CrateSettle()
    {
        var from = GamePosition + new Vector3(0f, 0.9f, 0f);
        var move = new Vector3(0f, -1f, 0f);
        bool hit = RayMove(from, ref move, 0x40, 0x2000, 0x10, 1, _crateHits, 1);
        _crateOnGround = hit && _crateHits.Count == 0;
        if (hit) AgentFlags &= ~0x1000u; // Amedo
    }

    // Amedo
    private void CrateLaunch_140790(Vector3 v)
    {
        _crateVy = v.Y;
        _crateRequest = 2;
        Trace($"crate launched vy {v.Y} (0x140790)");
    }

    internal void StartCrateFall()
    {
        if (_crateOnGround || _crateFalling) return;
        _crateVy = 0f;
        _crateRequest = 2;
        Trace("crate starts falling (0x140668 -> slot 11)");
        TriggerEventSlot(11, null);
    }

    private bool CrateFall(float dt)
    {
        var pos = GamePosition;
        var move = new Vector3(0f, _crateVy * dt, 0f);
        bool hit = false;
        if (0f <= _crateVy) _crateFalling = true;
        else
        {
            hit = RayMove(pos, ref move, 0x40, 0x2000, 0x10, 1, _crateHits, 1);
            if (_crateHits.Count > 0)
            {
                var below = _crateHits[0];
                if ((below.Instance.Source.StateFlags & 0x400) != 0)
                {
                    Trace($"crate lands on {below.Entity.Name} -> its slot 5");
                    below.TriggerEventSlot(5, this);
                    hit = true;
                }
                else
                {
                    move = new Vector3(0f, _crateVy * dt, 0f);
                    Trace($"crate crushes {below.Entity.Name} -> its slot 7");
                    below.TriggerEventSlot(7, this);
                    hit = false;
                }
            }
            else if (hit) _crateOnGround = true;
            _crateFalling = !hit;
        }
        _crateVy -= CrateGravity * dt;
        var next = pos + move;
        if (next != pos) SetLocalPosition(next);
        return !hit;
    }

    private bool RayMove(Vector3 pos, ref Vector3 move, uint worldMask, uint mask, uint require, uint exclude, List<AgentLabAgent> hits, int cap)
    {
        hits.Clear();
        if (Tiny(move))
        {
            move = Vector3.Zero;
            return false;
        }
        bool hit = false;
        if (Chunk.Collision is { } world && world.SegmentCast(pos, pos + move, worldMask, out var h)) // Amedo 2026-09-28
        {
            move = h.Point - pos;
            hit = true;
        }
        if (Tiny(move))
        {
            move = Vector3.Zero;
            return hit;
        }
        float t = SegmentQuery(pos, pos + move, mask, require, exclude, hits, cap);
        if (0f <= t && t <= 1f)
        {
            move *= t;
            hit = true;
        }
        return hit;
    }

    private static bool Tiny(Vector3 v) => MathF.Abs(v.X) <= 5e-5f && MathF.Abs(v.Y) <= 5e-5f && MathF.Abs(v.Z) <= 5e-5f;
}
