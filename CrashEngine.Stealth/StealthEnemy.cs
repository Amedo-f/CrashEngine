using CrashEngine.Core;
using System.Numerics;

namespace CrashEngine.Stealth;

public sealed class StealthEnemy : Component
{
    public PatrolPath?  Patrol         { get; set; }
    public PlayerProxy? Player         { get; set; }

    public float DetectionRange  { get; set; } = 18f;
    public float AlertRange      { get; set; } = 8f;
    public float ChaseRange      { get; set; } = 30f;
    public float FovDegrees      { get; set; } = 110f;
    public float WalkSpeed       { get; set; } = 3.5f;
    public float RunSpeed        { get; set; } = 7f;
    public float SearchDuration  { get; set; } = 5f;
    public float DetectionRate   { get; set; } = 25f;
    public float CooldownRate    { get; set; } = 10f;

    public EnemyState State         { get; private set; } = EnemyState.Patrol;
    public float      DetectionPct  { get; private set; } = 0f;

    private int     _waypointIdx   = 0;
    private Vector3 _lastKnownPos  = Vector3.Zero;
    private float   _searchTimer   = 0f;
    private float   _searchAngle   = 0f;
    private Vector3 _facingDir     = Vector3.UnitZ;

    public override void OnFixedUpdate()
    {
        if (Player is null) return;

        bool canSee = ComputeVisibility(out float sqrDist);
        UpdateDetection(canSee, sqrDist);
        RunFSM(canSee, sqrDist);
    }

    private bool ComputeVisibility(out float sqrDist)
    {
        var playerPos = Player!.Transform.Position;
        var myPos     = Transform.Position;
        var delta     = playerPos - myPos;
        sqrDist = delta.LengthSquared();

        if (sqrDist > DetectionRange * DetectionRange) return false;

        float halfFov = FovDegrees * 0.5f;
        if (Player.IsCrouching) halfFov -= 40f;
        if (Player.IsRunning)   halfFov += 30f;
        halfFov = Math.Clamp(halfFov, 10f, 180f);

        float angleDeg = Vector3.Dot(Vector3.Normalize(delta), _facingDir)
                         is float cosA
                       ? MathF.Acos(Math.Clamp(cosA, -1f, 1f)) * 180f / MathF.PI
                       : 180f;

        return angleDeg <= halfFov;
    }

    private void UpdateDetection(bool canSee, float sqrDist)
    {
        if (canSee)
        {
            float rate = DetectionRate * EngineTime.FixedDelta;
            if (Player!.IsCrouching) rate *= 0.45f;
            if (Player.IsRunning)    rate *= 1.8f;
            DetectionPct = Math.Clamp(DetectionPct + rate, 0f, 100f);

            if (sqrDist < AlertRange * AlertRange) DetectionPct = 100f;
        }
        else
        {
            DetectionPct = Math.Clamp(
                DetectionPct - CooldownRate * EngineTime.FixedDelta, 0f, 100f);
        }
    }

    private void RunFSM(bool canSee, float sqrDist)
    {
        switch (State)
        {
            case EnemyState.Patrol:
                DoPatrol();
                if (DetectionPct >= 50f)  { EnterAlert(); return; }
                if (DetectionPct >= 100f) { EnterChase(); return; }
                break;

            case EnemyState.Alert:
                FacePlayer();
                if (DetectionPct >= 100f) { EnterChase(); return; }
                if (DetectionPct <= 0f)   { EnterPatrol(); return; }
                break;

            case EnemyState.Chase:
                DoChase(sqrDist);
                if (!canSee && sqrDist > ChaseRange * ChaseRange)
                    EnterSearch();
                break;

            case EnemyState.Search:
                DoSearch();
                break;

            case EnemyState.Return:
                DoReturn();
                break;
        }
    }

    private void DoPatrol()
    {
        if (Patrol is null || Patrol.Count == 0) return;
        var target = Patrol.Get(_waypointIdx);
        MoveToward(target, WalkSpeed);
        if ((target - Transform.Position).LengthSquared() < 0.5f * 0.5f)
            _waypointIdx = (_waypointIdx + 1) % Patrol.Count;
    }

    private void FacePlayer()
    {
        var delta = Player!.Transform.Position - Transform.Position;
        if (delta != Vector3.Zero) _facingDir = Vector3.Normalize(delta);
    }

    private void DoChase(float sqrDist)
    {
        _lastKnownPos = Player!.Transform.Position;
        MoveToward(_lastKnownPos, RunSpeed);
        FacePlayer();
    }

    private void DoSearch()
    {
        _searchTimer += EngineTime.FixedDelta;
        _searchAngle += 90f * EngineTime.FixedDelta;
        float rad    = _searchAngle * MathF.PI / 180f;
        _facingDir   = new Vector3(MathF.Sin(rad), 0, MathF.Cos(rad));

        var circleTarget = _lastKnownPos +
            new Vector3(MathF.Cos(rad) * 3f, 0f, MathF.Sin(rad) * 3f);
        MoveToward(circleTarget, WalkSpeed * 0.6f);

        if (_searchTimer >= SearchDuration) EnterReturn();
        if (DetectionPct >= 50f)            EnterChase();
    }

    private void DoReturn()
    {
        if (Patrol is null || Patrol.Count == 0) { EnterPatrol(); return; }
        var home = Patrol.Get(_waypointIdx);
        MoveToward(home, WalkSpeed);
        if ((home - Transform.Position).LengthSquared() < 1f)
            EnterPatrol();
    }

    private void EnterPatrol()  { State = EnemyState.Patrol;  }
    private void EnterAlert()   { State = EnemyState.Alert;   }
    private void EnterChase()
    {
        State = EnemyState.Chase;
        _lastKnownPos = Player!.Transform.Position;
    }
    private void EnterSearch()
    {
        State        = EnemyState.Search;
        _searchTimer = 0f;
        _searchAngle = 0f;
    }
    private void EnterReturn()  { State = EnemyState.Return; }

    private void MoveToward(Vector3 target, float speed)
    {
        var delta = target - Transform.Position;
        delta.Y = 0f;
        if (delta == Vector3.Zero) return;
        _facingDir             = Vector3.Normalize(delta);
        float step             = speed * EngineTime.FixedDelta;
        float dist             = delta.Length();
        Transform.Position    += _facingDir * Math.Min(step, dist);
    }
}

public enum EnemyState { Patrol, Alert, Chase, Search, Return }
