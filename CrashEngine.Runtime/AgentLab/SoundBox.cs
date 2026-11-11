using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed class SoundBox
{
    public PS2AnyTrigger Source { get; }
    private readonly Vector3 _center, _half;
    private readonly float _radius2;
    private readonly Matrix4x4 _toLocal;

    // Amedo
    public SoundBox(PS2AnyTrigger source)
    {
        Source = source;
        var t = source.Trigger; // Amedo 2026-11-11
        _center = new Vector3(t.Position.X, t.Position.Y, t.Position.Z);
        _half = new Vector3(t.Scale.X, t.Scale.Y, t.Scale.Z);
        _radius2 = _half.X * _half.X + _half.Y * _half.Y + _half.Z * _half.Z;
        var m = Matrix4x4.CreateFromQuaternion(new Quaternion(t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Rotation.W))
              * Matrix4x4.CreateTranslation(_center);
        Matrix4x4.Invert(m, out _toLocal);
    }

    // Amedo 2026-11-11
    public bool Contains_1E4BE0(Vector3 p)
    {
        var d = p - _center;
        if (_radius2 < d.X * d.X + d.Y * d.Y + d.Z * d.Z) return false;
        var l = Vector3.Transform(p, _toLocal);
        return -_half.X <= l.X && l.X <= _half.X && -_half.Y <= l.Y && l.Y <= _half.Y && -_half.Z <= l.Z && l.Z <= _half.Z;
    }
}
