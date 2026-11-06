using System.Numerics;

namespace CrashEngine.Runtime.Character.Vehicles;

// Amedo 2026-09-29
public abstract class Vehicle
{
    public const int Rollerbrawl = 1, Humiliskate = 3, Hoverboard = 5, Wrestle = 6, Kind7 = 7, Passenger = 8;

    protected Vehicle(CharacterController owner, CharacterController? other, bool driver)
    {
        Owner = owner;
        Other = other;
        Driver = driver;
    }

    public CharacterController Owner { get; }
    public CharacterController? Other { get; }
    public bool Driver { get; }
    public Matrix4x4 Frame = Matrix4x4.Identity;
    public bool Suspended; // Amedo

    public abstract int Kind { get; }
    public virtual bool HasBody => false;
    public virtual bool AllowsLook => false;
    public virtual bool KeepsGun => false; // Amedo
    public virtual (Vector3 Min, Vector3 Max) Box => (new Vector3(-1f), new Vector3(1f));
    public virtual Vector3? Velocity => null;
    public virtual float Value_D0 => 0f; // Amedo

    public virtual void SetPosition(Vector3 position) { }
    public virtual void SetVelocity(Vector3 velocity) { }

    // Amedo
    public virtual void Push_160168(Vector3 velocity, Matrix4x4? sourceNode)
    {
        if (sourceNode is not { } node || !HasBody || Velocity is not { } v) return; // Amedo
        SetVelocity(v + Vector3.TransformNormal(velocity, node));
    }
    public virtual bool UpdateFrames() => false;
    public virtual void Step(float dt) { }
    public virtual void Transfer(Matrix4x4 obj) { }
    public virtual void Destroy() { }
    public virtual void SplashSphere_160278(out Vector3 position, out float radius) => Owner.SplashPoint_13F4C0(out position, out radius); // Amedo
}

// Amedo 2026-09-29
public sealed class PassengerVehicle : Vehicle
{
    public PassengerVehicle(int kind, CharacterController owner, CharacterController driver) : base(owner, driver, false) => Kind = kind;

    public override int Kind { get; }
    public override bool AllowsLook => true; // Amedo

    // Amedo
    public override void SplashSphere_160278(out Vector3 position, out float radius) =>
        (Other ?? Owner).SplashPoint_13F4C0(out position, out radius);
}
