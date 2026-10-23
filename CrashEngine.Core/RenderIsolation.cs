namespace CrashEngine.Core;

// Amedo 2026-10-04
public sealed class RenderIsolation
{
    public Entity Target { get; }
    internal readonly HashSet<Entity> Path = new(ReferenceEqualityComparer.Instance);
    internal readonly HashSet<Entity> Keep = new(ReferenceEqualityComparer.Instance);

    public RenderIsolation(Entity target, IEnumerable<Entity>? keep)
    {
        Target = target;
        for (var p = target.Parent; p is not null; p = p.Parent) Path.Add(p);
        if (keep is not null) foreach (var k in keep) Keep.Add(k);
    }
}
