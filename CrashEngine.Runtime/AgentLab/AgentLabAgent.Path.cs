using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private readonly List<PathSpline?> _paths = new();
    private int _pathIndex;
    private float _pathAlong;
    private Vector3 _pathDirection;

    public PathSpline? CurrentPath => _pathIndex < _paths.Count ? _paths[_pathIndex] : null;
    public float PathAlong => _pathAlong;

    private void LoadPaths()
    {
        _paths.Clear();
        foreach (var id in Instance.Source.Paths) _paths.Add(World.Path(Instance.Section, id));
        _pathIndex     = 0;
        _pathAlong     = 0f;
        _pathDirection = Vector3.Zero;
    }

    // Amedo 2026-09-28
    private void InheritPaths(AgentLabAgent from)
    {
        _paths.Clear();
        _paths.AddRange(from._paths);
    }

    private bool PathTarget(bool hasRaw, Vector3 raw, out Vector3 target)
    {
        target = default;
        if (CurrentPath is not { } path) return false;
        float along = path.Closest(GamePosition, out var closest);
        along = along < 0f ? 0f : 1f < along ? 1f : along;
        var forward = path.DirectionAt(along);
        _pathAlong     = along;
        _pathDirection = forward;
        target = closest;
        if (!hasRaw) return true;
        target += Vector3.TransformNormal(raw, Along_185A40(forward)); // Amedo
        return true;
    }
}
