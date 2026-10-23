using System.Numerics;
using CrashEngine.Runtime.Camera;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-10-05
public sealed partial class AgentLabAgent
{
    private const int MaxSensors = 8;
    private const float SensorEps = 2.4999998e-09f;

    private sealed class Sensor_23F718
    {
        public required IReadOnlyList<uint> Args;
        public float Value;
        public bool Enabled = true;
        public int Last;
    }

    private List<Sensor_23F718>? _sensors110;
    private Vector3 _sensorDir170;
    private readonly List<AgentLabAgent> _sensorNear = new();
    internal float Influence08;

    private static uint SensorArg(IReadOnlyList<uint> a, int i) => i < a.Count ? a[i] : 0u;
    private static float SensorF(IReadOnlyList<uint> a, int i) => BitConverter.UInt32BitsToSingle(SensorArg(a, i));
    private static float Clamp_18C2A0(float v, float min, float max) => v < min ? min : max < v ? max : v;

    private void AddSensor_23D9E8(IReadOnlyList<uint> a)
    {
        _sensors110 ??= new List<Sensor_23F718>(MaxSensors);
        if (_sensors110.Count == MaxSensors || FindSensor_23F910((int)(SensorArg(a, 0) & 7)) is not null) return;
        _sensors110.Add(new Sensor_23F718 { Args = a });
    }

    // Amedo
    private void ReleasePerception_23DAA8() => _sensors110 = null;

    private Sensor_23F718? FindSensor_23F910(int type)
    {
        if (_sensors110 is not { } list) return null;
        foreach (var s in list)
            if ((SensorArg(s.Args, 0) & 7) == type) return s;
        return null;
    }

    private float SensorValue_22B140() => FindSensor_23F910(0)?.Value ?? 0f;

    public float? SensorValue(int type) => FindSensor_23F910(type)?.Value;

    private void SetSensor_223CB0(IReadOnlyList<uint> a)
    {
        if (FindSensor_23F910((int)(SensorArg(a, 0) & 0xFF)) is { } s) s.Value = Clamp_18C2A0(SensorF(a, 1), 0f, 1f);
    }

    private void SetInfluence_223DB0(IReadOnlyList<uint> a) => Influence08 = Clamp_18C2A0(SensorF(a, 0), -1f, 1f);

    private void UpdateSensors_2378C0()
    {
        if (_sensors110 is not { Count: > 0 } list) return;
        int now = World.NowTicks;
        foreach (var s in list)
        {
            if (!s.Enabled || !(SensorF(s.Args, 11) <= (now - s.Last) * CamMath.TickToSec)) continue;
            s.Last = now;
            switch (SensorArg(s.Args, 0) & 7)
            {
                case 0: Flee_234338(s); break;
                case 1: Decay_23E810(s); break;
                case 2: Speed_23E730(s); break;
            }
        }
    }

    private static void Decay_23E810(Sensor_23F718 s) =>
        s.Value = Clamp_18C2A0(s.Value + SensorF(s.Args, 10) * 0.3f, SensorF(s.Args, 12), SensorF(s.Args, 13));

    private void Speed_23E730(Sensor_23F718 s)
    {
        var v = OwnCharacter?.Velocity ?? Component5?.Velocity ?? _velocity;
        s.Value = Clamp_18C2A0(v.Length() * SensorF(s.Args, 14), SensorF(s.Args, 12), SensorF(s.Args, 13));
    }

    private void Flee_234338(Sensor_23F718 s)
    {
        var a = s.Args;
        float min = SensorF(a, 12), max = SensorF(a, 13);
        var dir = Vector3.Zero;
        float sum = 0f;
        var near = Near_234230(a);
        if (near.Count > 0)
        {
            var p = GamePosition;
            foreach (var other in near)
            {
                var q = other.GamePosition;
                float d2 = Vector3.DistanceSquared(p, q);
                float w = other.Influence08 + SensorF(a, 15) * Facing_2346E0(other, p, q);
                w *= 1f - Clamp_18C2A0(d2 / SensorF(a, 9), min, max);
                var away = p - q;
                away *= CamMath.InvLengthGuarded_11C030(SensorEps, new Vector4(away, 0f));
                sum += w / SensorF(a, 1);
                dir += away * w;
            }
            sum = Clamp_18C2A0(sum, min, max);
            dir *= CamMath.InvLengthGuarded_11C030(SensorEps, new Vector4(dir, 0f));
        }
        _sensorDir170 = dir;
        s.Value = s.Value <= sum ? sum : Clamp_18C2A0(s.Value + SensorF(a, 10) * 0.3f, min, max);
    }

    private List<AgentLabAgent> Near_234230(IReadOnlyList<uint> a)
    {
        _sensorNear.Clear();
        uint arg0 = SensorArg(a, 0);
        int count = (int)(SensorArg(a, 7) & 0xFF);
        foreach (var o in World.QuerySphere(GamePosition, SensorF(a, 8), SensorArg(a, 2), 1, this))
        {
            if (o is not AgentLabAgent other || (other.AgentFlags & 0x10) == 0) continue;
            int id = other.Instance.ObjectId & 0x7FFF;
            for (int k = 0; k < count; k++)
            {
                uint word = SensorArg(a, 3 + k / 2);
                if (((k & 1) == 0 ? word & 0xFFFF : word >> 16) != id) continue;
                if (_sensorNear.Count < 0x38) _sensorNear.Add(other);
                break;
            }
        }
        if ((arg0 & 8) != 0 && World.Player is { } player) _sensorNear.Add(player);
        return _sensorNear;
    }

    private static float Facing_2346E0(AgentLabAgent other, Vector3 p, Vector3 q)
    {
        if (other.FindSensor_23F910(2) is not { } speed) return 0f;
        var d = p - q;
        d *= CamMath.InvLengthGuarded_11C030(SensorEps, new Vector4(d, 0f));
        var m = other.GameMatrix;
        float v = speed.Value * (m.M31 * d.X + m.M32 * d.Y + m.M33 * d.Z);
        return v + v;
    }
}
