using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character.Vehicles;

// Amedo
public interface ISkateSounds
{
    void Surface_1199D8(int index, CollisionWorld.Surface surface, Vector3 velocity);
    void Land_119C70(float impact);
    void Lean_122148(float lean);
}

// Amedo
public sealed class Humiliskate : Vehicle
{
    private const float Eps = 2.4999998e-09f;
    private const int NoEvent = 0x6F;

    public float Height;
    public override float Value_D0 => Height; // Amedo
    public Vector3 Radii;
    public float Reach;
    public float Gravity;
    public float Seat;
    public Matrix4x4 Board = Matrix4x4.Identity;
    public Vector3 Vel, PrevVel;
    public Vector3 NormalSum;
    public Vector3 TargetUp;
    public bool Grounded, WasGrounded;
    public int State;
    public float StateTimer;
    public bool Crouch, LeanLeft, LeanRight, Fakie;
    public float Squash, SquashVel;
    public CollisionWorld.Surface GroundSurface;
    public int GroundSurfaceIndex = -1;
    public int RailCount;
    public int CachedTriangles => _cache.Triangles.Count;
    public float AirTime;
    public float Spin, Flip, SpinTarget, FlipTarget;
    public bool Jumped;
    public int LastEvent, LastPartnerEvent;
    public bool Grinding;
    public Vector3 RailStart, RailEnd;
    public float RailCooldown;
    public bool Contact;
    public float CrouchSpeed, MaxSpeed;
    public float JumpCooldown, JumpReleased;
    public bool JumpedFromRail;
    public Matrix4x4 CrashMatrix = Matrix4x4.Identity;
    public Matrix4x4 PartnerMatrix = Matrix4x4.Identity;

    private readonly TriangleCache _cache = new(0x10);
    private readonly List<Vector3> _railStarts = new(), _railEnds = new();
    private readonly List<int> _candidates = new();
    private readonly List<(Vector3 Min, Vector3 Max)> _candidateBoxes = new();
    private readonly HullBuilder _builder = new();
    private readonly ConvexHull _railHull = new();
    public readonly SkidMark Marks1 = new(), Marks2 = new();

    public Humiliskate(CharacterController owner, CharacterController? other) : base(owner, other, true)
    {
        Start_158C18();
        Owner.SkidMarks?.Invoke(Marks1.Ring, true); // Amedo
        Owner.SkidMarks?.Invoke(Marks2.Ring, true);
    }

    public override int Kind => Humiliskate;
    public override bool AllowsLook => true;
    public override (Vector3 Min, Vector3 Max) Box => (new Vector3(-1f), new Vector3(1f));
    public override Vector3? Velocity => Vel;
    public Vector3 Position => Board.Translation;

    public override void SetVelocity(Vector3 velocity)
    {
        PrevVel = velocity;
        Vel = velocity;
    }

    private void Start_158C18()
    {
        Radii = new Vector3(0.6f, 0.6f, 0.8f);
        Seat = 0.4f;
        Reach = 1f;
        Gravity = 35f;
        Board = Owner.GameMatrix;
        Board.M42 += Radii.Y;
        var node = Owner.GameMatrix;
        var v = new Vector3(node.M31 * 10f, node.M32 * 10f, node.M33 * 10f);
        PrevVel = v;
        Vel = v;
        TargetUp = new Vector3(0f, 1f, 0f);
        Grounded = true;
        WasGrounded = false;
        State = 0;
        StateTimer = 0f;
        Squash = 0f;
        SquashVel = 0f;
        Crouch = LeanLeft = LeanRight = Fakie = false;
        CrouchSpeed = 35f;
        JumpReleased = 1f;
        AirTime = 0f;
        Spin = Flip = SpinTarget = FlipTarget = 0f;
        Jumped = false;
        Grinding = false;
        RailCooldown = 0f;
        Contact = false;
        JumpCooldown = 0f;
        JumpedFromRail = false;
        LastPartnerEvent = NoEvent;
        MaxSpeed = 25f;
        LastEvent = NoEvent;
        Marks1.Clear_161C68(); // Amedo
        Marks2.Clear_161C68();
        RailStart = Vector3.Zero;
        RailEnd = Vector3.Zero;
        GroundSurfaceIndex = -1;
        Start_14EF90();
    }

    private void Start_14EF90()
    {
        Frame = Matrix4x4.Identity;
        CrashMatrix = Matrix4x4.Identity;
        PartnerMatrix = Matrix4x4.Identity;
        Height = 0f;
        Owner.Body5 = null;
        UpdateFrames();
    }

    public override bool UpdateFrames() => Place_159AB8();

    public override void Step(float dt) => Frame_1597C0(dt);

    private void Frame_1597C0(float dt)
    {
        if (!Suspended)
        {
            float q = dt * 0.25f;
            for (int i = 3; 0 <= i; i--)
            {
                Steer_155690(q);
                Contact = false;
                Grounded = false;
                Collide_155A08(q);
                CollideInstances_157FC0(q);
                if (!Contact)
                {
                    TargetUp.Y += q * 2.5f;
                    float k = GameMath.InvLength_11C030(Eps, new Vector4(TargetUp, 1f));
                    TargetUp = new Vector3(TargetUp.X * k, TargetUp.Y * k, TargetUp.Z * k);
                }
                if (Grounded)
                {
                    if (!WasGrounded) Jumped = false;
                    if (Grounded && !WasGrounded)
                    {
                        Flip = FlipTarget;
                        Spin = SpinTarget;
                    }
                }
                LimitSpeed_157068(q);
                AlignUp_156DB0(TargetUp);
                Suspension_158E48(q);
                if (!Grinding)
                {
                    float sx = Vel.X * q, sy = Vel.Y * q, sz = Vel.Z * q;
                    Board.M41 += sx;
                    Board.M42 += sy;
                    Board.M43 += sz;
                    Vel.Y += -Gravity * q;
                }
                WasGrounded = Grounded;
                PrevVel = Vel;
                if (!Grounded) AirTime += q;
                else AirTime = 0f;
                Tricks_159450(q);
                Jump_155520(q);
            }
            SendEvents_158F88(dt);
            Owner.TrailLevel?.Invoke(Grounded ? 1f : 0f);
            LaySkidMarks_158850();
        }
        if (!UpdateFrames()) return;
        Owner.SetNode_18AF40(CrashMatrix);
        Other?.SetNode_18AF40(PartnerMatrix);
    }

    private ISkateSounds? PartnerSounds() => Other?.SkateSounds?.Invoke();

    private void Steer_155690(float dt)
    {
        var sounds = PartnerSounds();
        var inX = Owner.MoveX;
        var inZ = Owner.MoveZ;
        float lean = inX * Board.M11 + inZ * Board.M13;
        float sign = 0f < lean ? 1f : -1f;
        float turn = sign * (MathF.Sqrt(MathF.Abs(lean)) * 1.2f);
        if (turn < -1f) turn = -1f;
        else if (1f < turn) turn = 1f;
        float rate;
        if (Grounded)
        {
            float wobble = Owner.Rng.GetRandFloat__18C130(1.4f); // Amedo
            float speed = MathF.Sqrt(Vel.X * Vel.X + Vel.Y * Vel.Y + Vel.Z * Vel.Z);
            turn += wobble * speed * 0.03125f;
            rate = turn * (Crouch ? 1.3f : 4f);
        }
        else rate = turn * 2f;
        var r = GameMath.RotY_18BBC0(GameMath.FromValue_11BFA0(rate * dt, 0));
        var t = Board.Translation;
        Board = r * Board;
        Board.Translation = t;
        Crouch = Owner.Circle != 0f;
        if (LeanLeft)
        {
            if (!(lean < -0.3f)) LeanLeft = false;
        }
        else if (lean < -0.6f)
        {
            if (sounds is not null && Grounded) sounds.Lean_122148(lean);
            LeanRight = false;
            LeanLeft = true;
        }
        if (LeanRight)
        {
            if (!(0.3f < lean)) LeanRight = false;
        }
        else if (0.6f < lean)
        {
            if (sounds is not null && Grounded) sounds.Lean_122148(lean);
            LeanLeft = false;
            LeanRight = true;
        }
    }

    private void Jump_155520(float dt)
    {
        float cross = Owner.Cross;
        if (cross != 0f && (Grounded || Grinding) && JumpCooldown == 0f && JumpReleased != 0f)
        {
            bool rail = Grinding;
            Vel.Y += Board.M22 * 13f;
            Grounded = false;
            State = 0;
            Grinding = false;
            Jumped = true;
            JumpedFromRail = rail;
            JumpCooldown = 0.1f;
            JumpReleased = 0f;
            RailCooldown = 0.2f;
            Owner.Trigger(0x6D);
            Other?.Trigger(0x6D);
        }
        else if (cross == 0f) JumpReleased = 1f;
        float left = JumpCooldown - dt;
        JumpCooldown = left;
        if (left < 0f) JumpCooldown = 0f;
    }

    private void Collide_155A08(float dt)
    {
        var world = Owner.CollisionWorld;
        var pos = Board.Translation;
        var cur = pos;
        float grow = Reach + 0.2f;
        _cache.Refresh(world, new Vector3(pos.X - grow, pos.Y - grow, pos.Z - grow), new Vector3(pos.X + grow, pos.Y + grow, pos.Z + grow));
        var tris = _cache.Triangles;
        int rails = FindRails_1581F0(tris);
        RailCount = rails;
        bool ended = false;
        if (Grinding)
        {
            var d = RailEnd - RailStart;
            float kd = GameMath.InvLength_11C030(Eps, new Vector4(d, 1f));
            d = new Vector3(d.X * kd, d.Y * kd, d.Z * kd);
            float t = d.X * pos.X + d.Y * pos.Y + d.Z * pos.Z - (d.X * RailStart.X + d.Y * RailStart.Y + d.Z * RailStart.Z);
            var onRail = new Vector3(RailStart.X + d.X * t, RailStart.Y + d.Y * t, RailStart.Z + d.Z * t);
            float limit = dt * 5f;
            var pull = new Vector3(onRail.X - pos.X, onRail.Y + 0.4f - pos.Y, onRail.Z - pos.Z);
            if (limit < MathF.Sqrt(pull.X * pull.X + pull.Y * pull.Y + pull.Z * pull.Z))
            {
                float kp = GameMath.InvLength_11C030(Eps, new Vector4(pull, 1f));
                pull = new Vector3(pull.X * kp * limit, pull.Y * kp * limit, pull.Z * kp * limit);
            }
            Board.M41 += pull.X;
            Board.M42 += pull.Y;
            Board.M43 += pull.Z;
            float along = d.X * Vel.X + d.Y * Vel.Y + d.Z * Vel.Z;
            var slide = new Vector3(d.X * along * dt, d.Y * along * dt, d.Z * along * dt);
            Board.M41 += slide.X;
            Board.M42 += slide.Y;
            Board.M43 += slide.Z;
            var e = RailEnd - RailStart;
            float ke = GameMath.InvLength_11C030(Eps, new Vector4(e, 1f));
            float slope = e.Y * ke * 3.5f;
            if (1f < slope) slope = 1f;
            else if (slope < -1f) slope = -1f;
            float speed = MathF.Sqrt(Vel.X * Vel.X + Vel.Y * Vel.Y + Vel.Z * Vel.Z);
            float accel = dt * 12f;
            float change = 25f - slope * 11f - speed;
            if (accel < change) change = accel;
            else if (change < -accel) change = -accel;
            float s = speed + change;
            Vel = new Vector3(d.X * s, d.Y * s, d.Z * s);
            var p = Board.Translation;
            if (d.X * RailEnd.X + d.Y * RailEnd.Y + d.Z * RailEnd.Z < d.X * p.X + d.Y * p.Y + d.Z * p.Z)
            {
                State = 6;
                ended = true;
                Grinding = false;
                StateTimer = 0f;
            }
            var up = new Vector3(0f, 1f, 0f);
            var right = new Vector3(d.Y * up.Z - d.Z * up.Y, d.Z * up.X - d.X * up.Z, d.X * up.Y - d.Y * up.X);
            float kr = GameMath.InvLength_11C030(Eps, new Vector4(right, 1f));
            right = new Vector3(right.X * kr, right.Y * kr, right.Z * kr);
            var n = new Vector3(right.Y * d.Z - right.Z * d.Y, right.Z * d.X - right.X * d.Z, right.X * d.Y - right.Y * d.X);
            float kn = GameMath.InvLength_11C030(Eps, new Vector4(n, 1f));
            n = new Vector3(n.X * kn, n.Y * kn, n.Z * kn);
            NormalSum = n;
            TargetUp = n;
            var e2 = RailEnd - RailStart;
            float ke2 = GameMath.InvLength_11C030(Eps, new Vector4(e2, 1f));
            e2 = new Vector3(e2.X * ke2, e2.Y * ke2, e2.Z * ke2);
            var fwd = new Vector3(Board.M31, Board.M32, Board.M33);
            var c = new Vector3(fwd.Y * e2.Z - fwd.Z * e2.Y, fwd.Z * e2.X - fwd.X * e2.Z, fwd.X * e2.Y - fwd.Y * e2.X);
            float yawLimit = dt * 150f;
            float yaw = MathF.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z);
            if (yawLimit < yaw) yaw = yawLimit;
            else if (yaw < -yawLimit) yaw = -yawLimit;
            float side = 0f <= c.X * Board.M21 + c.Y * Board.M22 + c.Z * Board.M23 ? 12f : -12f;
            TurnAboutUp(yaw * dt * side);
            if (Grinding)
            {
                GroundContacts(dt, tris, cur);
                return;
            }
        }
        if (0f < RailCooldown)
        {
            float left = RailCooldown - dt;
            RailCooldown = left;
            if (left < 0f) RailCooldown = 0f;
        }
        else if (!(Vel.X * Board.M31 + Vel.Y * Board.M32 + Vel.Z * Board.M33 < 0f))
        {
            bool caught = false;
            if (ended)
            {
                var fwd = new Vector3(Board.M31, Board.M32, Board.M33);
                for (int i = 0; i < rails; i++)
                {
                    var a = _railStarts[i];
                    var b = _railEnds[i];
                    bool ahead = a.X * fwd.X + a.Y * fwd.Y + a.Z * fwd.Z < b.X * fwd.X + b.Y * fwd.Y + b.Z * fwd.Z;
                    var first = ahead ? a : b;
                    var second = ahead ? b : a;
                    if (!Near(first, RailEnd)) continue;
                    CatchRail(first, second);
                    caught = true;
                    break;
                }
            }
            if (!caught && !Grinding) CatchNewRail(rails);
        }
        GroundContacts(dt, tris, cur);
    }

    private void CatchRail(Vector3 start, Vector3 end)
    {
        RailStart = start;
        RailEnd = end;
        Grinding = true;
        State = 5;
        Flip = FlipTarget;
        Spin = SpinTarget;
    }

    private void CatchNewRail(int rails)
    {
        int best = -1;
        float bestReach = -1e10f;
        Vector3 bestStart = default, bestEnd = default;
        var fwd = new Vector3(Board.M31, Board.M32, Board.M33);
        var radii = new Vector3(Radii.X * 1.2f, Radii.Y * 1.2f, Radii.Z * 1.2f);
        for (int i = 0; i < rails; i++)
        {
            var a = _railStarts[i];
            var b = _railEnds[i];
            if (Near(a, RailStart) && Near(b, RailEnd)) continue;
            if (Near(a, RailEnd) && Near(b, RailStart)) continue;
            var low = new Vector3(b.X, b.Y - 0.1f, b.Z);
            if (!_builder.BuildTriangle_1FBB20(new Vector4(a, 1f), new Vector4(b, 1f), new Vector4(low, 1f), _railHull)) continue;
            if (!HullQueries.EllipsoidHull_1FD178(_railHull, Board, radii, Matrix4x4.Identity, out _, out _)) continue;
            float da = a.X * fwd.X + a.Y * fwd.Y + a.Z * fwd.Z;
            float db = b.X * fwd.X + b.Y * fwd.Y + b.Z * fwd.Z;
            float reach = da < db ? db : da;
            if (!(bestReach < reach)) continue;
            bestReach = reach;
            best = i;
            if (da < db)
            {
                bestStart = a;
                bestEnd = b;
            }
            else
            {
                bestStart = b;
                bestEnd = a;
            }
        }
        if (best == -1) return;
        CatchRail(bestStart, bestEnd);
    }

    private static bool Near(Vector3 a, Vector3 b)
    {
        var d = a - b;
        return d.X * d.X + d.Y * d.Y + d.Z * d.Z < 0.01f;
    }

    private void TurnAboutUp(float radians)
    {
        var up = new Vector3(Board.M21, Board.M22, Board.M23);
        var r = GameMath.AxisAngle_18CEA8(up, GameMath.FromValue_11BFA0(radians, 0), true); // Amedo 2026-11-21
        var t = Board.Translation;
        Board = Board * r;
        NormalizeRows();
        Board.Translation = t;
    }

    private void NormalizeRows()
    {
        float k0 = GameMath.InvLength_11C030(Eps, new Vector4(Board.M11, Board.M12, Board.M13, 1f));
        Board.M11 *= k0; Board.M12 *= k0; Board.M13 *= k0;
        float k1 = GameMath.InvLength_11C030(Eps, new Vector4(Board.M21, Board.M22, Board.M23, 1f));
        Board.M21 *= k1; Board.M22 *= k1; Board.M23 *= k1;
        float k2 = GameMath.InvLength_11C030(Eps, new Vector4(Board.M31, Board.M32, Board.M33, 1f));
        Board.M31 *= k2; Board.M32 *= k2; Board.M33 *= k2;
    }

    private void GroundContacts(float dt, List<CollisionWorld.Triangle> tris, Vector3 cur)
    {
        var world = Owner.CollisionWorld;
        NormalSum = Vector3.Zero;
        float grip = 0f, topY = -1f;
        int contacts = 0;
        var scale = new Matrix4x4(1f / Radii.X, 0f, 0f, 0f, 0f, 1f / Radii.Y, 0f, 0f, 0f, 0f, 1f / Radii.Z, 0f, 0f, 0f, 0f, 1f);
        GroundSurfaceIndex = -1;
        foreach (var tri in tris)
        {
            var surface = world.SurfaceAt(tri.Surface);
            bool solid = ((surface.Mask >> 20) & 1) != 0;
            var plane = HullQueries.PlaneFrom3_18D0A0(tri.A, tri.B, tri.C);
            var down = new Vector3(-plane.X, -plane.Y, -plane.Z);
            var frame = Board;
            frame.Translation = cur;
            var local = Local(Local(tri, GameMath.RigidInverse_18E808(frame)), scale);
            if (!CollisionWorld.SphereTriangle(local, Vector3.Zero, 1f, out var pushed)) continue;
            if (solid)
            {
                var push = new Vector3(pushed.X * Radii.X, pushed.Y * Radii.Y, pushed.Z * Radii.Z);
                contacts++;
                var w = Vector3.TransformNormal(push, Board);
                cur += new Vector3(w.X * 0.25f, w.Y * 0.25f, w.Z * 0.25f);
                NormalSum += down;
                grip += surface.Grip;
                if (topY < down.Y)
                {
                    topY = down.Y;
                    GroundSurfaceIndex = tri.Surface;
                    GroundSurface = surface;
                }
            }
            Owner.SurfaceMessage_140958(surface);
        }
        if (!Grinding)
        {
            Board.Translation = cur;
            if (contacts != 0) GroundResponse_1572A0(NormalSum, grip / contacts, dt);
        }
        PartnerSounds()?.Surface_1199D8(GroundSurfaceIndex, GroundSurface, Vel);
    }

    private static CollisionWorld.Triangle Local(in CollisionWorld.Triangle tri, Matrix4x4 m)
    {
        var a = GameMath.Transform_18E4F0(m, new Vector4(tri.A, 1f));
        var b = GameMath.Transform_18E4F0(m, new Vector4(tri.B, 1f));
        var c = GameMath.Transform_18E4F0(m, new Vector4(tri.C, 1f));
        return new CollisionWorld.Triangle(new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(c.X, c.Y, c.Z), b.W, c.W, a.W, tri.Surface);
    }

    private int FindRails_1581F0(List<CollisionWorld.Triangle> tris)
    {
        _railStarts.Clear();
        _railEnds.Clear();
        _candidates.Clear();
        _candidateBoxes.Clear();
        var world = Owner.CollisionWorld;
        var inverse = GameMath.RigidInverse_18E808(Board);
        var scale = new Matrix4x4(1f / Radii.X, 0f, 0f, 0f, 0f, 1f / Radii.Y, 0f, 0f, 0f, 0f, 1f / Radii.Z, 0f, 0f, 0f, 0f, 1f);
        for (int i = 0; i < tris.Count; i++)
        {
            var tri = tris[i];
            if (((world.SurfaceAt(tri.Surface).Mask >> 20) & 1) == 0) continue;
            var plane = HullQueries.PlaneFrom3_18D0A0(tri.A, tri.B, tri.C);
            float ux = -plane.X, uy = -plane.Y, uz = -plane.Z;
            if (0.5f < uy || uy < -0.5f) continue;
            float facing = MathF.Abs(Board.M31 * ux + Board.M32 * uy + Board.M33 * uz);
            if (0.707f < facing) continue;
            var local = Local(Local(tri, inverse), scale);
            if (!CollisionWorld.SphereTriangle(local, Vector3.Zero, 1f, out _)) continue;
            var min = Vector3.Min(Vector3.Min(tri.A, tri.B), tri.C);
            var max = Vector3.Max(Vector3.Max(tri.A, tri.B), tri.C);
            var grow = new Vector3(0.01f);
            _candidateBoxes.Add((min - grow, max + grow));
            _candidates.Add(i);
        }
        Span<int> shared = stackalloc int[9];
        for (int a = 0; a < _candidates.Count; a++)
        {
            var ta = tris[_candidates[a]];
            var na = HullQueries.PlaneFrom3_18D0A0(ta.A, ta.B, ta.C);
            for (int b = a + 1; b < _candidates.Count; b++)
            {
                var (amin, amax) = _candidateBoxes[a];
                var (bmin, bmax) = _candidateBoxes[b];
                if (!CollisionWorld.Overlap(amin, amax, bmin, bmax)) continue;
                var tb = tris[_candidates[b]];
                var nb = HullQueries.PlaneFrom3_18D0A0(tb.A, tb.B, tb.C);
                float d = na.X * nb.X + na.Y * nb.Y + na.Z * nb.Z;
                if (!(-0.99f < d) || !(d < -0.5f)) continue;
                int count = 0;
                for (int j = 0; j < 9; j++)
                {
                    var va = Vertex(ta, j % 3);
                    var vb = Vertex(tb, j / 3);
                    var g = va - vb;
                    if (g.X * g.X + g.Y * g.Y + g.Z * g.Z < 0.01f) shared[count++] = j;
                }
                if (count != 2) continue;
                _railStarts.Add(Vertex(ta, shared[0] % 3));
                _railEnds.Add(Vertex(ta, shared[1] % 3));
            }
        }
        return _railStarts.Count;
    }

    private static Vector3 Vertex(in CollisionWorld.Triangle t, int i) => i switch { 0 => t.A, 1 => t.B, _ => t.C };

    private void CollideInstances_157FC0(float dt)
    {
        if (Owner.SkateBodies_157FC0 is not { } query) return;
        var p = Board.Translation;
        float r = Reach + 0.1f;
        var bodies = query(new Vector3(p.X - r, p.Y - r, p.Z - r), new Vector3(p.X + r, p.Y + r, p.Z + r), Other?.Self);
        for (int i = 0; i < bodies.Count; i++) CollideHulls_157D28(bodies[i], dt);
    }

    private void CollideHulls_157D28(CharacterBody target, float dt)
    {
        var board = Radii;
        var rider = new Vector3(0.45f, 0.6f, 0.45f);
        for (int i = 0; i < target.Parts.Count; i++)
        {
            var (hull, m) = target.Parts[i];
            if (TouchesHull_157950(Board, board, m, hull, out var push, out var contact, out var normal))
            {
                var surface = Owner.CollisionWorld.SurfaceAt(Owner.HullSurfaceOf?.Invoke(target.Owner, i) ?? 0);
                if (((surface.Mask >> 20) & 1) != 0) TouchedHull_157B18(contact, push, normal, dt, target.Owner, true);
                Owner.SurfaceMessage_140958(surface);
            }
            if (Crouch) continue;
            var seat = Board;
            seat.M41 += Board.M21 * 0.8f;
            seat.M42 += Board.M22 * 0.8f;
            seat.M43 += Board.M23 * 0.8f;
            if (TouchesHull_157950(seat, rider, m, hull, out push, out contact, out normal))
                TouchedHull_157B18(contact, push, normal, dt, target.Owner, false);
        }
    }

    private bool TouchesHull_157950(Matrix4x4 frame, Vector3 radii, Matrix4x4 hullWorld, ConvexHull hull, out Vector3 push, out Vector3 contact, out Vector3 normal)
    {
        contact = default;
        float r = Reach + 0.1f;
        var p = frame.Translation;
        var (hmin, hmax) = hull.Bounds(hullWorld);
        push = default;
        normal = default;
        if (!CollisionWorld.Overlap(new Vector3(p.X - r, p.Y - r, p.Z - r), new Vector3(p.X + r, p.Y + r, p.Z + r), hmin, hmax)) return false;
        if (!HullQueries.EllipsoidHull_1FD178(hull, frame, radii, hullWorld, out push, out normal)) return false;
        var local = Vector3.TransformNormal(push, GameMath.RigidInverse_18E808(frame));
        local *= GameMath.InvLength_11C030(Eps, new Vector4(local, 1f));
        local *= -radii.X;
        local *= -radii.Y;
        local *= -radii.Z;
        contact = local;
        return true;
    }

    private void TouchedHull_157B18(in Vector3 contact, in Vector3 push, in Vector3 normal, float dt, object target, bool respond)
    {
        var point = GameMath.Transform_18E4F0(Board, new Vector4(contact, 1f));
        var at = new Vector3(point.X, point.Y, point.Z);
        bool hit = Owner.BodyHit_23D1E0?.Invoke(target, Owner.Self, at, Vel) ?? true;
        if (Owner.Self is { } self)
        {
            Owner.TouchFrom_137858(target, Vel, -Vel);
            if (((Owner.InstanceFlagsOf?.Invoke(self) ?? 0u) & 0x8000) != 0) Owner.BodyHit_23D1E0?.Invoke(self, target, at, -Vel);
        }
        if (((Owner.InstanceFlagsOf?.Invoke(target) ?? 0u) & 0x10) == 0) hit = false;
        if (!respond || !hit) return;
        Board.M41 += push.X;
        Board.M42 += push.Y;
        Board.M43 += push.Z;
        GroundResponse_1572A0(normal, 1f, dt);
    }

    private void GroundResponse_1572A0(Vector3 sum, float grip, float dt)
    {
        float kn = GameMath.InvLength_11C030(Eps, new Vector4(sum, 1f));
        var n = new Vector3(sum.X * kn, sum.Y * kn, sum.Z * kn);
        TargetUp += new Vector3(n.X * 0.04f, n.Y * 0.04f, n.Z * 0.04f);
        float ku = GameMath.InvLength_11C030(Eps, new Vector4(TargetUp, 1f));
        TargetUp = new Vector3(TargetUp.X * ku, TargetUp.Y * ku, TargetUp.Z * ku);
        Contact = true;
        float into = n.X * Vel.X + n.Y * Vel.Y + n.Z * Vel.Z;
        if (into < -2f)
        {
            var half = new Vector3(n.X * into * 0.5f, n.Y * into * 0.5f, n.Z * into * 0.5f);
            float hard = MathF.Sqrt(half.X * half.X + half.Y * half.Y + half.Z * half.Z);
            var flat = new Vector3(Vel.X, 0f, Vel.Z);
            float kf = GameMath.InvLength_11C030(Eps, new Vector4(flat, 1f));
            flat = new Vector3(flat.X * kf, flat.Y * kf, flat.Z * kf);
            float facing = n.X * flat.X + n.Y * flat.Y + n.Z * flat.Z;
            if (3f < hard && facing < -0.5f)
            {
                Owner.Trigger(0x6E);
                Other?.Trigger(0x6E);
                Vel -= new Vector3(n.X * into * 1.3f, n.Y * into * 1.3f, n.Z * into * 1.3f);
            }
            else Vel -= half;
        }
        float lateral = Board.M11 * Vel.X + Board.M12 * Vel.Y + Board.M13 * Vel.Z;
        float kv = GameMath.InvLength_11C030(Eps, new Vector4(Vel, 1f));
        var heading = new Vector3(Vel.X * kv, Vel.Y * kv, Vel.Z * kv);
        float cos = Board.M31 * heading.X + Board.M32 * heading.Y + Board.M33 * heading.Z;
        float soft = 4f - cos * 3.2f;
        if (soft < 1f) soft = 1f;
        else if (4f < soft) soft = 4f;
        float limit = dt * (grip * 45f) / soft * 1.414f * TargetUp.Y;
        if (limit < lateral) lateral = limit;
        else if (lateral < -limit) lateral = -limit;
        Vel -= new Vector3(Board.M11 * lateral, Board.M12 * lateral, Board.M13 * lateral);
        float yawLimit = dt * 10f;
        float yaw = lateral * 0.1f;
        if (yawLimit < yaw) yaw = yawLimit;
        else if (yaw < -yawLimit) yaw = -yawLimit;
        TurnAboutUp(yaw);
        if (0.2f < sum.Y) Grounded = true;
    }

    private void LimitSpeed_157068(float dt)
    {
        float speed = MathF.Sqrt(Vel.X * Vel.X + Vel.Y * Vel.Y + Vel.Z * Vel.Z);
        float limit = Crouch ? CrouchSpeed : MaxSpeed;
        if (limit < speed)
        {
            float k = GameMath.InvLength_11C030(Eps, new Vector4(Vel, 1f));
            Vel = new Vector3(Vel.X * k, Vel.Y * k, Vel.Z * k);
            Vel = new Vector3(Vel.X * limit, Vel.Y * limit, Vel.Z * limit);
        }
        if (!(speed < 5f)) return;
        if (!(0f < Vel.X * Board.M31 + Vel.Y * Board.M32 + Vel.Z * Board.M33)) return;
        if (!(0.866f < TargetUp.Y)) return;
        float push = speed + dt * 10f;
        float kp = GameMath.InvLength_11C030(Eps, new Vector4(Vel, 1f));
        Vel = new Vector3(Vel.X * kp, Vel.Y * kp, Vel.Z * kp);
        Vel = new Vector3(Vel.X * push, Vel.Y * push, Vel.Z * push);
    }

    private void AlignUp_156DB0(Vector3 target)
    {
        var a = new Vector3(Board.M21, Board.M22, Board.M23);
        float ka = GameMath.InvLength_11C030(Eps, new Vector4(a, 1f));
        a = new Vector3(a.X * ka, a.Y * ka, a.Z * ka);
        float kb = GameMath.InvLength_11C030(Eps, new Vector4(target, 1f));
        var b = new Vector3(target.X * kb, target.Y * kb, target.Z * kb);
        var c = new Vector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        if (!(1e-4f < MathF.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z))) return;
        float kc = GameMath.InvLength_11C030(Eps, new Vector4(c, 1f));
        float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        c = new Vector3(c.X * kc, c.Y * kc, c.Z * kc);
        var r = GameMath.AxisAngle_18CEA8(c, GameMath.Acos_18B738(dot), true); // Amedo 2026-11-21
        var t = Board.Translation;
        Board = Board * r;
        NormalizeRows();
        Board.Translation = t;
    }

    private void Suspension_158E48(float dt)
    {
        if (Grounded && !WasGrounded)
        {
            float impact = Vel.Y - PrevVel.Y;
            float kick = impact * -1.6f;
            if (kick < SquashVel) SquashVel = kick;
            PartnerSounds()?.Land_119C70(impact);
        }
        float v = SquashVel;
        float x = Squash + v * dt;
        Squash = x;
        SquashVel = v + (x * -1.5f - v * 0.015f);
        if (0.5f < x)
        {
            Squash = 0.5f;
            SquashVel = 0f;
        }
        else if (x < -0.5f)
        {
            Squash = -0.5f;
            SquashVel = 0f;
        }
    }

    private void FallTime_159278(out float time, out float height)
    {
        if (Grounded)
        {
            time = 0f;
            height = 0f;
            return;
        }
        var start = Board.Translation;
        var end = new Vector3(start.X, start.Y + -10f, start.Z) + new Vector3(Board.M31 * 4f, Board.M32 * 4f, Board.M33 * 4f);
        if (Owner.CollisionWorld.SegmentCast(start, end, 0x10, out var hit))
        {
            var d = start - hit.Point;
            height = MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        }
        else height = 10f;
        float g = Gravity;
        time = (-Vel.Y - MathF.Sqrt(Vel.Y * Vel.Y + (g + g) * height)) / -g;
    }

    private void Tricks_159450(float dt)
    {
        FallTime_159278(out float time, out float height);
        Height = height;
        if (State == 0)
        {
            if (0.03f < AirTime && 3f < height && !JumpedFromRail && !Jumped)
            {
                if (0.51887906f < time)
                {
                    State = 2;
                    FlipTarget = Flip + 6.2831855f;
                }
                else if (0.36179939f < time)
                {
                    State = 1;
                    SpinTarget = Spin + 3.1415927f;
                }
            }
            if (State == 2)
            {
                Owner.Trigger(0x61);
                Other?.Trigger(0x61);
                LastPartnerEvent = LastEvent = NoEvent;
            }
            else if (State == 1)
            {
                Owner.Trigger(0x60);
                Other?.Trigger(0x60);
                LastPartnerEvent = LastEvent = NoEvent;
            }
        }
        if (State == 2)
        {
            float next = Flip + dt * ((FlipTarget - Flip) * 5.1f + 1.4f);
            if (FlipTarget < next)
            {
                if (time < 0.51887906f) State = 3;
                else FlipTarget += 6.2831855f;
            }
            else Flip = next;
        }
        else if (State == 1)
        {
            float next = Spin + ((SpinTarget - Spin) * 7.5f * dt + dt);
            if (SpinTarget < next)
            {
                Fakie = !Fakie;
                if (time < 0.36179939f) State = 3;
                else SpinTarget += 3.1415927f;
            }
            else Spin = next;
        }
        SpinTarget = GameMath.Trunc(SpinTarget * 0.31830987f + 0.5f) * 3.1415927f;
        FlipTarget = GameMath.Trunc(FlipTarget * 0.15915494f + 0.5f) * 6.2831855f;
    }

    private void SendEvents_158F88(float dt)
    {
        int crash = NoEvent, cortex = NoEvent;
        switch (State)
        {
            case 0:
                crash = Crouch ? LeanLeft ? 0x5E : LeanRight ? 0x5D : 0x5C : Pose();
                cortex = Pose();
                break;
            case 1:
            case 2:
                break;
            case 3:
                State = 4;
                StateTimer = 0f;
                Owner.Trigger(0x5F);
                Other?.Trigger(Pose());
                LastPartnerEvent = NoEvent;
                LastEvent = NoEvent;
                break;
            case 4:
                StateTimer += dt;
                if (0.5f < StateTimer) State = 0;
                break;
            case 5:
                crash = cortex = Fakie ? 0x63 : 0x62;
                break;
            case 6:
                StateTimer += dt;
                if (0.2f < StateTimer) State = 0;
                break;
        }
        LastEvent = crash;
        Owner.Trigger(crash);
        LastPartnerEvent = cortex;
        Other?.Trigger(cortex);
    }

    private int Pose() => LeanLeft ? Fakie ? 0x5A : 0x57 : LeanRight ? Fakie ? 0x5B : 0x58 : Fakie ? 0x59 : 0x56;

    private float Signed(float x) => Owner.Rng.GetRandFloat__18C130(x); // Amedo

    private void LaySkidMarks_158850()
    {
        if (Grounded && !Grinding)
        {
            var p = new Vector4(Board.Translation, 1f);
            p.Y -= Radii.Y * 0.866f;
            if (0.1f < MathF.Sqrt(Vel.X * Vel.X + Vel.Z * Vel.Z))
            {
                var flat = new Vector4(Vel.X, 0f, Vel.Z, 0f);
                float k = GameMath.InvLength_11C030(Eps, flat);
                var dir = new Vector4(flat.X * k, flat.Y * k, flat.Z * k, 0f);
                var side = new Vector4(dir.Y * 0f - dir.Z * 1f, dir.Z * 0f - dir.X * 0f, dir.X * 1f - dir.Y * 0f, 1f);
                float kv = GameMath.InvLength_11C030(Eps, new Vector4(Vel, 0f));
                float along = Vel.X * kv * Board.M31 + Vel.Y * kv * Board.M32 + Vel.Z * kv * Board.M33;
                p.X -= TargetUp.X * 0.5f;
                p.Z -= TargetUp.Z * 0.5f;
                float offset = MathF.Sqrt(1.001f - along * along) * 0.4f + 0.3f;
                var world = Owner.CollisionWorld;
                Marks1.Lay_15BDB0(p, side, _cache.Triangles, world, offset, Signed(0.01f) + 0.16f, Signed);
                Marks2.Lay_15BDB0(p, new Vector4(-side.X, -side.Y, -side.Z, side.W), _cache.Triangles, world, offset, Signed(0.01f) + 0.16f, Signed);
                return;
            }
        }
        Marks1.Idle_161C40();
        Marks2.Idle_161C40();
    }

    private bool Place_159AB8()
    {
        var b = GameMath.RotXY_182AD0(GameMath.FromValue_11BFA0(-Flip, 0), 0) * Board;
        var up = new Vector3(b.M21 * Seat, b.M22 * Seat, b.M23 * Seat);
        b.M41 -= up.X;
        b.M42 -= up.Y;
        b.M43 -= up.Z;
        PartnerMatrix = b;
        CrashMatrix = b;
        float s = -Squash;
        if (s < -0.5f) s = -0.5f;
        if (0.5f < s) s = 0.5f;
        var squash = Matrix4x4.Identity;
        squash.M22 = 1f - s;
        squash.M33 = s * 0.4f + 1f;
        squash.M11 = s * 0.4f + 1f;
        PartnerMatrix = squash * PartnerMatrix;
        PartnerMatrix = GameMath.RotY_18BBC0(GameMath.FromValue_11BFA0(Spin, 0)) * PartnerMatrix;
        CrashMatrix.M42 += Squash * 0.6f;
        var fwd = new Vector3(Board.M31, Board.M32, Board.M33);
        var u = new Vector3(0f, 1f, 0f);
        var right = new Vector3(fwd.Z, u.Z * fwd.X - u.X * fwd.Z, u.X * fwd.Y - u.Y * fwd.X);
        var ahead = new Vector3(right.Y * u.Z - right.Z * u.Y, right.Z * u.X - right.X * u.Z, right.X * u.Y - right.Y * u.X);
        var p = Board.Translation;
        Frame = new Matrix4x4(right.X, right.Y, right.Z, 0f, u.X, u.Y, u.Z, 0f, ahead.X, ahead.Y, ahead.Z, 0f, p.X, p.Y, p.Z, 1f);
        return true;
    }

    public override void Transfer(Matrix4x4 obj)
    {
        Board *= obj;
        Vel = Vector3.TransformNormal(Vel, obj);
        PrevVel = Vector3.TransformNormal(PrevVel, obj);
        NormalSum = Vector3.TransformNormal(NormalSum, obj);
        TargetUp = Vector3.TransformNormal(TargetUp, obj);
        RailStart = Vector3.Transform(RailStart, obj);
        RailEnd = Vector3.Transform(RailEnd, obj);
    }

    public override void Push_160168(Vector3 velocity, Matrix4x4? sourceNode) => Push_1615A8(velocity, sourceNode); // Amedo

    public void Push_1615A8(Vector3 v, Matrix4x4? frame)
    {
        var d = frame is { } m ? Vector3.TransformNormal(v, m) : v;
        Vel = new Vector3(Vel.X + d.X, Vel.Y + d.Y, Vel.Z + d.Z);
    }

    public override void Destroy()
    {
        Marks2.Destroy_161B98(); // Amedo
        Marks1.Destroy_161B98();
        Owner.SkidMarks?.Invoke(Marks2.Ring, false);
        Owner.SkidMarks?.Invoke(Marks1.Ring, false);
        Owner.Body5 = null;
    }
}
