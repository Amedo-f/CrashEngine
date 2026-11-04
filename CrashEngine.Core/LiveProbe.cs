namespace CrashEngine.Core;

public static class LiveProbe
{
    private static readonly List<(string Kind, string? Label, WeakReference Ref)> Tracked = new();

    public static void Track(string kind, object o, string? label = null)
    {
        lock (Tracked) Tracked.Add((kind, label, new WeakReference(o)));
    }

    public static string Report()
    {
        var counts = new SortedDictionary<string, int>();
        var labels = new SortedDictionary<string, int>();
        lock (Tracked)
        {
            Tracked.RemoveAll(t => !t.Ref.IsAlive);
            foreach (var (kind, label, _) in Tracked)
            {
                counts[kind] = counts.GetValueOrDefault(kind) + 1;
                if (label is not null) labels[$"{kind} {label}"] = labels.GetValueOrDefault($"{kind} {label}") + 1;
            }
        }
        return string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}")) + " | " + string.Join(", ", labels.Select(l => l.Value > 1 ? $"{l.Key} x{l.Value}" : l.Key));
    }
}
