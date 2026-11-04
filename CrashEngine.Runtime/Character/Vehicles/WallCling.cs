using System.Numerics;
using CrashEngine.Runtime.Camera;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character.Vehicles;

// Amedo
public sealed class WallCling : Vehicle
{
    private Matrix4x4 _place;
    private Vector3 _velocity;
    public Vector3 WallNormal;
    private Matrix4x4 _driver = Matrix4x4.Identity, _partner = Matrix4x4.Identity;
    private readonly ConvexHull _box = CharacterController.Box(1.8f, 0.29f);

    public WallCling(CharacterController owner) : base(owner, null, true) => Start_160880();

    public override int Kind => Kind7;
    public override bool AllowsLook => true;
    public override Vector3? Velocity => _velocity;

    private void Start_160880()
    {
        _place = Owner.GameMatrix;
        _velocity = Vector3.Zero;
        Start_14EF90();
    }

    private void Start_14EF90()
    {
        Frame = Matrix4x4.Identity;
        _driver = Matrix4x4.Identity;
        _partner = Matrix4x4.Identity;
        Owner.Body5 = null;
        UpdateFrames();
    }

    public override bool UpdateFrames() => Place_160A70();

    private bool Place_160A70()
    {
        _partner = _place;
        _driver = _place;
        Frame = _place;
        return true;
    }

    public override void Step(float dt) => Frame_160968(dt);

    private void Frame_160968(float dt)
    {
        Move_150358(dt);
        bool done = Collide_150540(dt);
        FaceWall_150910();
        if (UpdateFrames()) Owner.SetNode_18AF40(_driver);
        if (done) Owner.ExitVehicle_13BCF0(false);
        else Owner.Trigger(0x22);
    }

    private void Move_150358(float dt)
    {
        float vy = _velocity.Y - dt * 4.5f;
        _velocity.Y = vy < -4.5f ? -4.5f : vy;
        var p = _place.Translation;
        p.X += _velocity.X * dt;
        p.Y += _velocity.Y * dt;
        p.Z += _velocity.Z * dt;
        _place.Translation = p;
        var x = new Vector3(_place.M11, _place.M12, _place.M13);
        float want = (Owner.MoveX * x.X + Owner.MoveZ * x.Z) * 6f;
        float have = x.X * _velocity.X + x.Y * _velocity.Y + x.Z * _velocity.Z;
        float d = want - have;
        if (3f < d) d = 3f;
        else if (d < -3f) d = -3f;
        var k = x * d;
        _velocity += k * dt * 25f;
    }

    private bool Collide_150540(float dt)
    {
        var collision = Owner.Collision;
        collision.Begin();
        float speed = MathF.Sqrt(_velocity.X * _velocity.X + _velocity.Y * _velocity.Y + _velocity.Z * _velocity.Z);
        var mv = new Vector3(0f, 0f, speed * dt + 0.5f);
        var p = _place.Translation;
        collision.Gather(Owner.CollisionWorld, p, ref mv, _box);
        Owner.GatherAgents_2846E8(p, mv, _box, 0x5B010u);
        var push = collision.PushOut_2849C8(p, true, true, true, Owner.BodyOf, out _);
        p += push;
        _place.Translation = p;
        if (0.001f < MathF.Sqrt(push.X * push.X + push.Y * push.Y + push.Z * push.Z))
        {
            var n = push * GameMath.InverseLength(push);
            if (0.707f < n.Y) return true;
        }
        var z = new Vector3(_place.M31, _place.M32, _place.M33);
        if (!collision.PointTest(p + z * 0.5f, 0x800, 0f)) return true;
        var near = p + z * 0.02f;
        if (!collision.PointTest(near, 0x800, 0f)) _place.Translation = near;
        return false;
    }

    private void FaceWall_150910()
    {
        var z = new Vector3(_place.M31, _place.M32, _place.M33);
        var n = WallNormal;
        var axis = new Vector3(z.Y * n.Z - z.Z * n.Y, z.Z * n.X - z.X * n.Z, z.X * n.Y - z.Y * n.X);
        z *= GameMath.InverseLength(z);
        if (!(5e-5f < axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z)) return;
        axis *= GameMath.InverseLength(axis);
        float dot = z.X * n.X + z.Y * n.Y + z.Z * n.Z;
        var r = CrashEngine.Runtime.Character.Vehicles.Humiliskate.AxisAngle_18CEA8(axis, GameMath.Acos_18B738(dot));
        var p = _place.Translation;
        _place *= r;
        _place.Translation = p;
    }

    public override void Destroy() => Owner.Body5 = null;
}
