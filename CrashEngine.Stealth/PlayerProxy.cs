using CrashEngine.Core;

namespace CrashEngine.Stealth;

public sealed class PlayerProxy : Component
{
    public bool IsCrouching { get; set; } = false;
    public bool IsRunning   { get; set; } = false;
    public bool IsMoving    { get; set; } = false;

    public override void OnUpdate()
    {
        IsRunning   = Input.KeyHeld(Silk.NET.Input.Key.ShiftLeft) && Input.KeyHeld(Silk.NET.Input.Key.W);
        IsCrouching = Input.KeyHeld(Silk.NET.Input.Key.ControlLeft);
        IsMoving    = Input.KeyHeld(Silk.NET.Input.Key.W) || Input.KeyHeld(Silk.NET.Input.Key.A)
                   || Input.KeyHeld(Silk.NET.Input.Key.S) || Input.KeyHeld(Silk.NET.Input.Key.D);

        float speed = IsCrouching ? 1.5f : IsRunning ? 8f : 4f;
        var move    = System.Numerics.Vector3.Zero;
        if (Input.KeyHeld(Silk.NET.Input.Key.W)) move.Z -= 1f;
        if (Input.KeyHeld(Silk.NET.Input.Key.S)) move.Z += 1f;
        if (Input.KeyHeld(Silk.NET.Input.Key.A)) move.X -= 1f;
        if (Input.KeyHeld(Silk.NET.Input.Key.D)) move.X += 1f;
        if (move != System.Numerics.Vector3.Zero)
            move = System.Numerics.Vector3.Normalize(move);
        Transform.Position += move * speed * EngineTime.Delta;
    }
}
