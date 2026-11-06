using CrashEngine.Core;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void ExecuteGlue(Entity source, Entity target)
    {
        if (!TryGetWorldAABB(source, out var sMin, out var sMax)) return;
        if (!TryGetWorldAABB(target, out var tMin, out var tMax)) return;

        var sCenter  = (sMin + sMax) * 0.5f;
        var tCenter  = (tMin + tMax) * 0.5f;
        var toTarget = tCenter - sCenter;

        int axis;
        if (_glueAxisLock >= 0) axis = _glueAxisLock;
        else
        {
            var abs = new Vector3(MathF.Abs(toTarget.X), MathF.Abs(toTarget.Y), MathF.Abs(toTarget.Z));
            axis = abs.X >= abs.Y && abs.X >= abs.Z ? 0 : (abs.Y >= abs.Z ? 1 : 2);
        }

        float delta = axis switch
        {
            0 => toTarget.X >= 0 ? tMin.X - sMax.X : tMax.X - sMin.X,
            1 => toTarget.Y >= 0 ? tMin.Y - sMax.Y : tMax.Y - sMin.Y,
            _ => toTarget.Z >= 0 ? tMin.Z - sMax.Z : tMax.Z - sMin.Z,
        };
        var worldDelta = axis switch
        {
            0 => new Vector3(delta, 0f, 0f),
            1 => new Vector3(0f, delta, 0f),
            _ => new Vector3(0f, 0f, delta),
        };

        var before = new TransformSnapshot(source.Transform);
        ApplyWorldDelta(source, worldDelta);
        var after = new TransformSnapshot(source.Transform);
        if (!after.Equals(before))
            _undoStack.Push(new TransformEditAction { Entity = source, Before = before, After = after });
    }
}
