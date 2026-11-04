using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed class SoundBox
{
    public Vector3 Min_00 { get; }
    public Vector3 Max_10 { get; }
    public Vector3 Center_20 { get; }
    public Matrix4x4 Matrix_30 { get; }
    public Matrix4x4 Inverse_70 { get; }
    public float Radius2_B0 { get; }
    public PS2AnyTrigger Source { get; }

    // Amedo
    public SoundBox(PS2AnyTrigger source)
    {
        Source = source;
        var t = source.Trigger;
        Max_10 = new Vector3(t.Scale.X, t.Scale.Y, t.Scale.Z);
        Min_00 = -Max_10;
        Center_20 = new Vector3(t.Position.X, t.Position.Y, t.Position.Z);
        Matrix_30 = Matrix4x4.CreateFromQuaternion(new Quaternion(t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Rotation.W)) * Matrix4x4.CreateTranslation(Center_20);
        Inverse_70 = GameMath.RigidInverse_18E808(Matrix_30);
        var h = (Min_00 - Max_10) * 0.5f;
        float r = MathF.Sqrt(h.X * h.X + h.Y * h.Y + h.Z * h.Z);
        Radius2_B0 = r * r;
    }

    // Amedo
    public bool Contains_1E4BE0(Vector3 p)
    {
        var d = p - Center_20;
        if (Radius2_B0 < d.X * d.X + d.Y * d.Y + d.Z * d.Z) return false;
        var l = Vector3.Transform(p, Inverse_70);
        return Min_00.X <= l.X && l.X <= Max_10.X && Min_00.Y <= l.Y && l.Y <= Max_10.Y && Min_00.Z <= l.Z && l.Z <= Max_10.Z;
    }
}
