using CrashEngine.Core;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
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
        var dir = Vector3.Normalize(target - _pos);
        _pitch  = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f)) * 180f / MathF.PI;
        _yaw    = MathF.Atan2(dir.X, dir.Z)               * 180f / MathF.PI;
    }

    public override void OnUpdate()
    {
        float dt = EngineTime.Delta;

        if (Input.Mouse(MouseButton.Right))
        {
            _yaw   -= Input.MouseDelta.X * 0.2f;
            _pitch -= Input.MouseDelta.Y * 0.2f;
            _pitch  = Math.Clamp(_pitch, -89f, 89f);
        }

        if (Input.ScrollDelta != 0f && !ImGui.GetIO().WantCaptureMouse)
        {
            _speed += Input.ScrollDelta * _speed * 0.15f;
            _speed  = Math.Clamp(_speed, 0.5f, 500f);
        }

        float yr = _yaw   * MathF.PI / 180f;
        float pr = _pitch * MathF.PI / 180f;

        var forward = Vector3.Normalize(new Vector3(
            MathF.Cos(pr) * MathF.Sin(yr),
            MathF.Sin(pr),
            MathF.Cos(pr) * MathF.Cos(yr)));

        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Forward = forward;

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

        View = CreateLookAtRH(_pos, _pos + forward, Vector3.UnitY);
        Projection = CreatePerspectiveRH(
            Fov * MathF.PI / 180f, (float)_w / _h, Near, Far);
    }

    public static Matrix4x4 CreateLookAtRH(Vector3 eye, Vector3 target, Vector3 up)
    {
        var f = Vector3.Normalize(target - eye);
        var s = Vector3.Normalize(Vector3.Cross(f, up));
        var u = Vector3.Cross(s, f);

        return new Matrix4x4(
            s.X, u.X, -f.X, 0f,
            s.Y, u.Y, -f.Y, 0f,
            s.Z, u.Z, -f.Z, 0f,
            -Vector3.Dot(s, eye), -Vector3.Dot(u, eye), Vector3.Dot(f, eye), 1f);
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
