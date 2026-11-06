using System.Numerics;

namespace CrashEngine.Core;

public class Transform
{
    public Vector3    Position = Vector3.Zero;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3    Scale    = Vector3.One;

    public Transform? Parent { get; internal set; }

    public Matrix4x4? LocalMatrix;

    public Matrix4x4 Local =>
        LocalMatrix ??
        (Matrix4x4.CreateScale(Scale)
        * Matrix4x4.CreateFromQuaternion(Rotation)
        * Matrix4x4.CreateTranslation(Position));

    public Matrix4x4 World
    {
        get
        {
            if (_cacheEpoch != 0 && _worldEpoch == _cacheEpoch && Environment.CurrentManagedThreadId == _cacheThread) return _world; // Amedo
            var world = Parent is null ? Local : Local * Parent.World;
            if (_cacheEpoch != 0 && Environment.CurrentManagedThreadId == _cacheThread) // Amedo
            {
                _world = world;
                _worldEpoch = _cacheEpoch;
            }
            return world;
        }
    }

    // Amedo
    private static int _cacheEpoch, _nextEpoch, _cacheThread;
    private int _worldEpoch;
    private Matrix4x4 _world;

    public static bool CacheWhileRendering { get; set; } = true; // Amedo

    // Amedo
    internal static void BeginRenderCache()
    {
        if (!CacheWhileRendering) return;
        _cacheThread = Environment.CurrentManagedThreadId;
        _cacheEpoch = ++_nextEpoch;
    }

    internal static void EndRenderCache() => _cacheEpoch = 0; // Amedo

    public Vector3 Forward => GameMath.Normalize(Vector3.Transform(-Vector3.UnitZ, Rotation));
    public Vector3 Right   => GameMath.Normalize(Vector3.Transform( Vector3.UnitX, Rotation));
    public Vector3 Up      => GameMath.Normalize(Vector3.Transform( Vector3.UnitY, Rotation));
}
