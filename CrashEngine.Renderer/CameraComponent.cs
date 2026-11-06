using CrashEngine.Core;
using ImGuiNET;
using Silk.NET.Input;
using System.Numerics;

namespace CrashEngine.Renderer;

public sealed class CameraComponent : Component
{
    public float Fov  { get; set; } = 60f;
    public float Near { get; set; } = 0.1f;
    public float Far  { get; set; } = 20000f;

    public Matrix4x4 View       { get; private set; } = Matrix4x4.Identity;
    public Matrix4x4 Projection { get; private set; } = Matrix4x4.Identity;

    public Vector3 Forward { get; private set; } = -Vector3.UnitZ;

    public float Speed => _speed;

    private float   _yaw   = 180f;
    private float   _pitch = -10f;
    private Vector3 _pos   = new(0f, 8f, 30f);
    private float   _speed = 20f;
    private int     _w = 1280, _h = 720;

    public override void OnResize(int w, int h) { _w = w; _h = h; }

    public void FocusOn(Vector3 target, float distance = 5f)
    {
        _pos   = target + new Vector3(0f, distance * 0.4f, distance);
        var dir = GameMath.Normalize(target - _pos);
        _pitch  = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f)) * 180f / MathF.PI;
        _yaw    = MathF.Atan2(dir.X, dir.Z)               * 180f / MathF.PI;
    }

    // Amedo 2026-10-04
    public Vector3? OrbitTarget { get; set; }
    public float OrbitDistance { get; set; } = 5f;
    public (Vector3 Pos, float Yaw, float Pitch, float Speed) SaveState() => (_pos, _yaw, _pitch, _speed);
    public void RestoreState((Vector3 Pos, float Yaw, float Pitch, float Speed) s) => (_pos, _yaw, _pitch, _speed) = s;

    public void OrbitAround(Vector3 target, float distance, float yaw = 180f, float pitch = -10f)
    {
        OrbitTarget = target;
        OrbitDistance = distance;
        _yaw = yaw;
        _pitch = pitch;
    }

    public bool External { get; set; } // Amedo 2026-09-28
    public float Aspect => _h > 0 ? (float)_w / _h : 16f / 9f; // Amedo 2026-10-06

    // Amedo 2026-09-28
    // Amedo
    public static Matrix4x4 ShiftClip(Matrix4x4 p, Vector2 s)
    {
        if (s == Vector2.Zero) return p;
        p.M11 += s.X * p.M14; p.M12 += s.Y * p.M14;
        p.M21 += s.X * p.M24; p.M22 += s.Y * p.M24;
        p.M31 += s.X * p.M34; p.M32 += s.Y * p.M34;
        p.M41 += s.X * p.M44; p.M42 += s.Y * p.M44;
        return p;
    }

    // Amedo
    public void SetExternal(in Vector3 eye, in Vector3 forward, in Vector3 up, float fovDegrees, float near, float far, in Vector2 clipShift)
    {
        SetExternal(eye, forward, up, fovDegrees, near, far);
        Projection = ShiftClip(Projection, clipShift);
        SkyProjection = ShiftClip(SkyProjection, clipShift);
    }

    public void SetExternal(in Vector3 eye, in Vector3 forward, in Vector3 up, float fovDegrees, float near, float far)
    {
        External   = true;
        _pos       = eye;
        _pitch     = MathF.Asin(Math.Clamp(forward.Y, -1f, 1f)) * 180f / MathF.PI;
        _yaw       = MathF.Atan2(forward.X, forward.Z) * 180f / MathF.PI;
        Forward    = forward;
        Transform.Position = eye;
        View       = CreateLookAtRH(eye, eye + forward, up);
        Projection = CreatePerspectiveRH(fovDegrees * MathF.PI / 180f, (float)_w / _h, near, far);
        SkyProjection = CreatePerspectiveRH(fovDegrees * MathF.PI / 180f, (float)_w / _h, near, MathF.Max(far, SkyFar)); // Amedo 2026-09-29
    }

    // Amedo 2026-09-29
    public const float SkyFar = 1e6f;
    public Matrix4x4 SkyProjection { get; private set; } = Matrix4x4.Identity;

    // Amedo 2026-10-04
    public bool Orthographic { get; set; }
    public float OrthoHeight { get; set; } = 10f;
    public Vector3 Right { get; private set; } = Vector3.UnitX;
    public Vector3 Up { get; private set; } = Vector3.UnitY;
    public float Yaw => _yaw;
    public float Pitch => _pitch;

    public float WorldPerPixel(float distance, int screenHeight) =>
        Orthographic ? OrthoHeight / screenHeight : distance * 2f * MathF.Tan(Fov * MathF.PI / 360f) / screenHeight;

    public void Rotate(float yawDelta, float pitchDelta)
    {
        _yaw += yawDelta;
        _pitch = Math.Clamp(_pitch + pitchDelta, -89f, 89f);
        Orthographic = false;
    }

    public void SnapTo(Vector3 forward, Vector3 pivot)
    {
        forward = GameMath.Normalize(forward);
        float dist = OrbitTarget is not null ? OrbitDistance : MathF.Max(1f, GameMath.Distance(_pos, pivot));
        if (!Orthographic) OrthoHeight = MathF.Max(0.5f, 2f * dist * MathF.Tan(Fov * MathF.PI / 360f));
        _pitch = MathF.Asin(Math.Clamp(forward.Y, -1f, 1f)) * 180f / MathF.PI;
        _yaw = MathF.Abs(forward.Y) < 0.999f ? MathF.Atan2(forward.X, forward.Z) * 180f / MathF.PI : 180f;
        Orthographic = true;
        if (OrbitTarget is null) _pos = pivot - forward * dist;
    }

    private (Vector3 Forward, Vector3 Right, Vector3 Up) Basis()
    {
        float yr = _yaw * MathF.PI / 180f, pr = _pitch * MathF.PI / 180f;
        var forward = GameMath.Normalize(new Vector3(MathF.Cos(pr) * MathF.Sin(yr), MathF.Sin(pr), MathF.Cos(pr) * MathF.Cos(yr)));
        var right = GameMath.Normalize(Vector3.Cross(new Vector3(MathF.Sin(yr), 0f, MathF.Cos(yr)), Vector3.UnitY));
        return (forward, right, Vector3.Cross(right, forward));
    }

    private void SetProjection()
    {
        Projection = Orthographic
            ? CreateOrthographicRH(OrthoHeight, (float)_w / _h, -Far * 0.5f, Far * 0.5f)
            : CreatePerspectiveRH(Fov * MathF.PI / 180f, (float)_w / _h, Near, Far);
        SkyProjection = CreatePerspectiveRH(Fov * MathF.PI / 180f, (float)_w / _h, Near, MathF.Max(Far, SkyFar)); // Amedo 2026-09-29
    }

    public override void OnUpdate()
    {
        if (External) return; // Amedo 2026-09-28
        float dt = EngineTime.Delta;

        if (Input.Mouse(MouseButton.Right))
        {
            _yaw   -= Input.MouseDelta.X * 0.2f;
            _pitch -= Input.MouseDelta.Y * 0.2f;
            _pitch  = Math.Clamp(_pitch, -89f, 89f);
            if (Input.MouseDelta != Vector2.Zero) Orthographic = false; // Amedo 2026-10-04
        }

        bool scrolled = Input.ScrollDelta != 0f && !ImGui.GetIO().WantCaptureMouse; // Amedo 2026-10-04
        if (scrolled && Orthographic) OrthoHeight = Math.Clamp(OrthoHeight * (1f - Input.ScrollDelta * 0.1f), 0.2f, 5000f);

        if (OrbitTarget is { } orbit) // Amedo 2026-10-04
        {
            if (scrolled && !Orthographic)
                OrbitDistance = Math.Clamp(OrbitDistance * (1f - Input.ScrollDelta * 0.1f), 0.3f, 500f);
            var (of, orr, ou) = Basis();
            if (Input.Mouse(MouseButton.Middle))
            {
                float pan = Orthographic ? OrthoHeight * 0.0015f : OrbitDistance * 0.0015f;
                orbit += (-orr * Input.MouseDelta.X + ou * Input.MouseDelta.Y) * pan;
                OrbitTarget = orbit;
            }
            Forward = of;
            Right = orr;
            Up = ou;
            _pos = orbit - of * OrbitDistance;
            Transform.Position = _pos;
            View = CreateLookAtRH(_pos, orbit, ou);
            SetProjection();
            return;
        }

        if (scrolled && !Orthographic)
        {
            _speed += Input.ScrollDelta * _speed * 0.15f;
            _speed  = Math.Clamp(_speed, 0.5f, 500f);
        }

        var (forward, right, up) = Basis(); // Amedo 2026-10-04
        Forward = forward;
        Right = right;
        Up = up;

        float spd = _speed * ((Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight)) ? 5f : 1f);
        spd *= dt;

        bool ctrlHeld = Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight);
        if (!ctrlHeld)
        {
            if (Input.KeyHeld(Key.W)) _pos += forward      * spd;
            if (Input.KeyHeld(Key.S)) _pos -= forward      * spd;
            if (Input.KeyHeld(Key.A)) _pos -= right        * spd;
            if (Input.KeyHeld(Key.D)) _pos += right        * spd;
            if (Input.KeyHeld(Key.E)) _pos += Vector3.UnitY * spd;
            if (Input.KeyHeld(Key.Q)) _pos -= Vector3.UnitY * spd;
        }

        Transform.Position = _pos;

        View = CreateLookAtRH(_pos, _pos + forward, up); // Amedo 2026-10-04
        SetProjection();
    }

    // Amedo 2026-10-04
    public static Matrix4x4 CreateOrthographicRH(float height, float aspect, float near, float far)
    {
        float w = height * aspect;
        return new Matrix4x4(
            2f / w, 0f,          0f,                             0f,
            0f,     2f / height, 0f,                             0f,
            0f,     0f,          -2f / (far - near),             0f,
            0f,     0f,          -(far + near) / (far - near),   1f);
    }

    public static Matrix4x4 CreateLookAtRH(in Vector3 eye, in Vector3 target, in Vector3 up)
    {
        var f = GameMath.Normalize(target - eye);
        var s = GameMath.Normalize(Vector3.Cross(f, up));
        var u = Vector3.Cross(s, f);

        return new Matrix4x4(
            s.X, u.X, -f.X, 0f,
            s.Y, u.Y, -f.Y, 0f,
            s.Z, u.Z, -f.Z, 0f,
            -GameMath.Dot(s, eye), -GameMath.Dot(u, eye), GameMath.Dot(f, eye), 1f);
    }

    public static Matrix4x4 CreatePerspectiveRH(float fovY, float aspect, float near, float far)
    {
        float t = MathF.Tan(fovY * 0.5f);
        return new Matrix4x4(
            1f / (aspect * t), 0f,     0f,                              0f,
            0f,                1f / t, 0f,                              0f,
            0f,                0f,     -(far + near) / (far - near),   -1f,
            0f,                0f,     -(2f * far * near) / (far - near), 0f);
    }
}
