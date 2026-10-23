using CrashEngine.Core;

namespace CrashEngine.Runtime.Streaming;

// Amedo 2026-09-28
public sealed class ChunkHost
{
    public required Func<string, object?> Parse { get; init; }
    public required Func<string, object, Entity?> Build { get; init; }
    public bool BuildInBackground { get; init; } // Amedo 2026-09-29
    public double UploadBudgetMs { get; init; } = 3.0;
    public Dictionary<string, Task<object?>> Prepared { get; } = new(); // Amedo 2026-10-02
    public List<string> StartupChunks { get; } = new();
}
