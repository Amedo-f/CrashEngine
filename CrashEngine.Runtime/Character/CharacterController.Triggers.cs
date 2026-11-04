using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-28
public sealed partial class CharacterController
{
    private const int TriggerBoxPoints = 11;

    public ConvexHull TriggerHull { get; private set; } = ConvexHull.Box(new Vector3(-1f), new Vector3(1f));

    // Amedo
    private void MakeBoxOfExitPoints_1395A0()
    {
        if (InstanceInt_2619D8?.Invoke(0) == 5) return;
        var node = _getMatrix();
        var inv = GameMath.RigidInverse_18E808(node);
        var min = new Vector3(Far);
        var max = new Vector3(-Far);
        bool any = false;
        for (int exit = 0; exit < TriggerBoxPoints; exit++)
        {
            if (Limbs.ExitPosition(exit, 0f, node) is not { } p) continue;
            var local = Vector3.Transform(p, inv);
            if (local.X < min.X) min.X = local.X;
            if (local.Y < min.Y) min.Y = local.Y;
            if (local.Z < min.Z) min.Z = local.Z;
            if (max.X < local.X) max.X = local.X;
            if (max.Y < local.Y) max.Y = local.Y;
            if (max.Z < local.Z) max.Z = local.Z;
            any = true;
        }
        if (any) InstanceBox_1F0168((min, max));
    }

    private ConvexHull? _shapeBeforeVehicle; // Amedo 2026-09-29

    private void AltShape_1F0168((Vector3 Min, Vector3 Max)? box)
    {
        if (box is { } b)
        {
            _shapeBeforeVehicle ??= TriggerHull;
            TriggerHull = ConvexHull.Box(b.Min, b.Max);
        }
        else if (_shapeBeforeVehicle is { } saved)
        {
            TriggerHull = saved;
            _shapeBeforeVehicle = null;
        }
    }
}
