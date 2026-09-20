using System.Numerics;

namespace CrashEngine.Core;

public class Transform
{
    public Vector3    Position = Vector3.Zero;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3    Scale    = Vector3.One;

    public Transform? Parent { get; internal set; }

    public Matrix4x4? LocalMatrix;

    public Matrix4x4 Local =>
        LocalMatrix ??
        (Matrix4x4.CreateScale(Scale)
        * Matrix4x4.CreateFromQuaternion(Rotation)
        * Matrix4x4.CreateTranslation(Position));

    public Matrix4x4 World =>
        Parent is null ? Local : Local * Parent.World;

    public Vector3 Forward => Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, Rotation));
    public Vector3 Right   => Vector3.Normalize(Vector3.Transform( Vector3.UnitX, Rotation));
    public Vector3 Up      => Vector3.Normalize(Vector3.Transform( Vector3.UnitY, Rotation));

    public void LookAt(Vector3 target)
    {
        var dir = Vector3.Normalize(target - Position);
        if (dir == Vector3.Zero) return;
        Rotation = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateLookAt(Position, Position + dir, Vector3.UnitY) is var m
                ? Matrix4x4.Transpose(m) : Matrix4x4.Identity);
    }
}
