namespace CrashEngine.Renderer;

public static class GpuOwner
{
    private static readonly Dictionary<object, HashSet<IGpuResource>> Owned = new(ReferenceEqualityComparer.Instance);

    public static object? Current { get; set; }

    static GpuOwner() => CrashEngine.Core.Engine.GpuContextStarted += Owned.Clear;

    public readonly struct Scope : IDisposable
    {
        private readonly object? _previous;
        internal Scope(object? previous) => _previous = previous;
        public void Dispose() => Current = _previous;
    }

    public static Scope Shared()
    {
        var previous = Current;
        Current = null;
        return new Scope(previous);
    }

    internal static object? Track(IGpuResource r)
    {
        if (Current is not { } owner) return null;
        if (!Owned.TryGetValue(owner, out var set)) Owned[owner] = set = new(ReferenceEqualityComparer.Instance);
        set.Add(r);
        return owner;
    }

    internal static void Forget(object? owner, IGpuResource r)
    {
        if (owner is not null && Owned.TryGetValue(owner, out var set)) set.Remove(r);
    }

    public static int Release(object owner)
    {
        if (!Owned.Remove(owner, out var set)) return 0;
        foreach (var r in set.ToArray()) r.Dispose();
        if (ReferenceEquals(Current, owner)) Current = null;
        return set.Count;
    }
}
