using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-10-05
public sealed partial class CharacterController
{
    public Vector3 Override1A0;
    public Func<object, Vector3?>? MotionVelocityOf;
    private readonly List<CharacterBody> _linkBodies = new();

    private float CollideLinked_13A500(Vector3 disp, float turn, float dt, bool groundedIn)
    {
        const int steps = 2;
        if (Link?.Follower_15FE98()?.Link is not { } fl) return Collide(disp, turn, 0f, dt, groundedIn);
        object? followerSelf = fl.Owner.Self;
        float dtSub = dt / steps;
        var dispSub = disp * (1f / steps);
        float turnSub = turn / steps;
        var p = Position + new Vector3(0f, CenterOffset, 0f);
        var hull = (Crouch.LowShape && (MotionFlagsHigh & 1u) != 0) || InAirState ? LowHull : StandHull;
        var ext = new Vector3(0.5f, 2f, 0.5f);
        var grow = new Vector3(GameMath.Length(disp) + 0.1f);
        var boxMin = Vector3.Min(p - ext, fl.Target1F0 - ext) - grow;
        var boxMax = Vector3.Max(p + ext, fl.Target1F0 + ext) + grow;
        Collision.Begin();
        Collision.GatherBox_283F98(_world, boxMin, boxMax, hull);
        if (Bodies is { } bodies)
        {
            var (qmin, qmax) = CharacterCollision.LocalBounds(hull);
            var mid = (p + fl.Target1F0) * 0.5f;
            qmin += mid - new Vector3(1.1f);
            qmax += mid + new Vector3(1.1f);
            _linkBodies.Clear();
            foreach (var b in bodies(qmin, qmax, 0x15B010u))
                if (!ReferenceEquals(b.Owner, followerSelf)) _linkBodies.Add(b);
            Collision.GatherBodies(_linkBodies, qmin, qmax, hull);
        }
        float turnOut = turnSub;
        for (int s = 0; s < steps; s++)
        {
            Collision.ResetFlags_287F88();
            var m = _getMatrix();
            p = m.Translation + new Vector3(0f, CenterOffset, 0f);
            var row2 = new Vector3(m.M31, m.M32, m.M33);
            var pStart = p;
            var followerPush = Vector3.Zero;
            if (Collision.PointTest(fl.Target1F0, 0, 0f))
            {
                followerPush = Collision.PushOut_2849C8(fl.Target1F0, true, true, false, null, out float followerDepth); // Amedo
                float followerLen = MathF.Sqrt(followerPush.X * followerPush.X + followerPush.Y * followerPush.Y + followerPush.Z * followerPush.Z); // Amedo
                if (1f < followerDepth && 0.25f < followerDepth - followerLen && CrushedAgents_136478()) Crushed_1367D0(); // Amedo
                if (followerPush != Vector3.Zero) followerPush.Y = 0f;
            }
            var mv = Limbs.Push(_world, Collision, StandHull, m, p, dispSub, dtSub, this);
            var plat = Vector3.Zero;
            if (Platform_1357C0(out var carried, out float platTurn))
            {
                plat = carried;
                mv += plat;
                turnOut = turnSub + platTurn;
            }
            else turnOut = turnSub;
            LastPlatform = plat;
            if (5e-5f < GameMath.Length(Override1A0)) mv = Override1A0 + followerPush;
            else mv += followerPush;
            Rotate(turnOut);
            if (StateFlagsOf is { } flagsOf) Collision.ClassFilter(ShapeClass(), flagsOf, MotionStateOf); // Amedo
            if (StandingOn is { } support && (State70 & 0xF00) == 0x300) Collision.MarkPart_287E90(support, StandingPart, 8);
            var push = Collision.PushOut_2849C8(p + plat, true, false, true, BodyOf, out float depth) * 1.001f; // Amedo
            float pushSq = push.X * push.X + push.Y * push.Y + push.Z * push.Z; // Amedo
            if (1f < depth && 0.25f < depth - pushSq && CrushedAgents_136478()) Crushed_1367D0(); // Amedo
            if (HullRadius * HullRadius < pushSq)
            {
                push = push * GameMath.InverseLength(push) * HullRadius;
                pushSq = HullRadius * HullRadius;
            }
            var start = p;
            if (pushSq != 0f)
            {
                var pn = push * GameMath.InverseLength(push);
                float into = GameMath.Dot(mv, pn);
                if (into < 0f) mv -= pn * into;
                Collision.Slide(p, push, out start, out _, false);
            }
            int hit = Collision.Slide(start, mv, out var newPos, out var normal, groundedIn);
            LastHit = hit;
            LastMove = mv;
            SetPosition(newPos - new Vector3(0f, CenterOffset, 0f));
            AfterSlide(hit, disp, normal, newPos);
            var moved = newPos - p;
            if (Collision.StepUp(newPos, mv, ref moved, groundedIn) &&
                !(MathF.Abs(moved.X) <= 5e-5f && MathF.Abs(moved.Y) <= 5e-5f && MathF.Abs(moved.Z) <= 5e-5f))
                SetPosition(Position + moved);
            DispatchTouches();
            if (!CutsceneMode)
            {
                SurfaceHazards();
                NonSolidContacts();
            }
            Push_134810(dtSub);
            FollowerTarget_13A500(fl, pStart, row2, start, dtSub, followerPush);
            if (groundedIn) dispSub.Y = 0f;
        }
        MotionFlagsHigh &= ~(1u << 23);
        return turnOut;
    }

    private void FollowerTarget_13A500(CharacterLink fl, in Vector3 pStart, in Vector3 row2, in Vector3 start, float dtSub, in Vector3 followerPush)
    {
        fl.Value210 = 1.3f;
        var desired = pStart - row2 * 1.3f;
        float probe = Collision.DownProbe(fl.Target1F0, 0.5f, 0.05f, out var hitPoint, out var hitNormal, out var hitOwner);
        var target = new Vector3(desired.X, fl.Target1F0.Y + fl.Vec200.Y * dtSub, desired.Z);
        var vel = fl.Vec200;
        if (probe == Far) vel.Y -= dtSub * 120f;
        else
        {
            target.Y = hitPoint.Y + 0.001f;
            vel.Y = 0f;
            if (hitOwner is not null && MotionVelocityOf?.Invoke(hitOwner) is { } ownerVelocity) vel.Y = ownerVelocity.Y;
        }
        if (target.Y < start.Y - 0.45f)
        {
            target.Y = start.Y - 0.45f;
            vel.Y = fl.Vec200.Y;
        }
        Override1A0 = Vector3.Zero;
        if (start.Y + 0.5f < target.Y)
        {
            if (0f < vel.Y) vel.Y = fl.Vec200.Y;
            if (probe != Far)
            {
                if (Velocity.Y < fl.Vec200.Y) Velocity.Y = fl.Vec200.Y;
                var o = hitNormal;
                if (o.Y < 0.99f) o.Y -= 1f / o.Y;
                else
                {
                    var m = _getMatrix();
                    o = new Vector3(m.M31, m.M32, m.M33);
                }
                Override1A0 = o * (dtSub * 15f);
            }
            target.Y = start.Y + 0.5f;
        }
        CheckLinkedCrush_136678(MathF.Sqrt(followerPush.X * followerPush.X + followerPush.Y * followerPush.Y + followerPush.Z * followerPush.Z), start); // Amedo
        fl.Target1F0 = target;
        fl.Vec200 = vel;
    }

    private void FollowerPlace_1302F8(CharacterLink link, float dt)
    {
        var pos = Position;
        var leader = link.Leader_15FE50();
        var m = leader is not null ? leader._getMatrix() : _getMatrix();
        m.Translation = link.Target1F0;
        _setMatrix(m);
        var vel = (link.Target1F0 - pos) * (1f / dt);
        FollowerAnim_14E138(link, vel, leader is not null ? leader.ForwardAxis : ForwardAxis);
    }

    private void FollowerAnim_14E138(CharacterLink link, Vector3 vel, Vector3 forward)
    {
        float speed = MathF.Sqrt(vel.X * vel.X + vel.Z * vel.Z);
        float dot = GameMath.Dot(vel * GameMath.InverseLength(vel), forward * GameMath.InverseLength(forward));
        void Set(int state) => link.Flags = (link.Flags & ~(0xFUL << 36)) | ((ulong)state << 36);
        switch ((int)((link.Flags >> 36) & 0xF))
        {
            case 0:
                _trigger(0xC);
                Set(1);
                return;
            case 1:
                if (2f < speed) { Set(2); _trigger(0xE); return; }
                if (dot < 0.6f && 0.2f < speed) { Set(4); _trigger(0xD); return; }
                _trigger(0xC);
                return;
            case 2:
                if (dot < 0.6f && 0.2f < speed) { Set(4); _trigger(0xD); return; }
                if (4f < speed) { Set(3); _trigger(0xF); return; }
                if (speed < 0.5f) { Set(1); _trigger(0xC); return; }
                _trigger(0xE);
                return;
            case 3:
                if (3f <= speed) { _trigger(0xF); return; }
                Set(2);
                _trigger(0xE);
                return;
            case 4:
                if (dot <= 0.8f && speed <= 2f)
                {
                    if (0.2f <= speed) { _trigger(0xD); return; }
                    Set(1);
                    _trigger(0xC);
                    return;
                }
                Set(2);
                _trigger(0xE);
                return;
        }
    }
}
