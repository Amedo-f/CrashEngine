namespace CrashLauncher;

// Amedo 2026-11-18
public sealed partial class GameScene
{
    private const int BehaviourSaveSettleMs = 500;
    private FileSystemWatcher? _behaviourWatcher;
    private long _behaviourCodeTouched;
    private long _behaviourWatchCheckedAt;
    private Task<BehaviourCodeBuild>? _behaviourBuild;

    private void UpdateBehaviourWatch()
    {
        long now = Environment.TickCount64;
        while (BehaviourIde.Messages.TryDequeue(out var message)) _browser.Log("Behaviour code: " + message);
        if (now - _behaviourWatchCheckedAt >= 1000)
        {
            _behaviourWatchCheckedAt = now;
            WatchBehaviourCode();
        }
        if (Simulating) return;
        if (_behaviourBuild is { IsCompleted: true } build)
        {
            _behaviourBuild = null;
            if (build.IsFaulted) _browser.Log("Behaviour code: " + build.Exception!.GetBaseException().Message);
            else if (BehaviourBuildCurrent(build.Result.Work)) FinishBehaviourCode(build.Result, fromWatch: true);
            else BehaviourCodeTouched();
        }
        long touched = Interlocked.Read(ref _behaviourCodeTouched);
        if (_behaviourBuild is not null || touched == 0 || now - touched < BehaviourSaveSettleMs) return;
        Interlocked.CompareExchange(ref _behaviourCodeTouched, 0, touched);
        try
        {
            if (PendingBehaviourCode() is { } work) _behaviourBuild = Task.Run(() => CompileBehaviourCode(work));
        }
        catch (IOException)
        {
            BehaviourCodeTouched();
        }
    }

    private bool BehaviourBuildCurrent(BehaviourCodeWork work)
    {
        var src = CodeSource;
        if (!ReferenceEquals(work.Rm2, src?.Rm2) || !ReferenceEquals(work.GlobalRm2, src?.GlobalRm2)) return false;
        var times = BehaviourCodeTimes();
        return times.Count == work.Times.Count && times.All(t => work.Times.TryGetValue(t.Key, out var at) && at == t.Value)
            && !BehaviourCodeApplied(times, src?.Rm2, src?.GlobalRm2);
    }

    private void WatchBehaviourCode()
    {
        string root = BehaviourCodeRoot;
        string dir = Path.Combine(root, "Behaviours");
        bool wanted = CodeSource?.Rm2 is not null && Path.IsPathRooted(root) && Directory.Exists(dir);
        if (_behaviourWatcher is { } old && (!wanted || !old.Path.Equals(dir, StringComparison.OrdinalIgnoreCase)))
        {
            old.Dispose();
            _behaviourWatcher = null;
        }
        if (!wanted || _behaviourWatcher is not null) return;
        var watcher = new FileSystemWatcher(dir, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
        };
        watcher.Changed += (_, _) => BehaviourCodeTouched();
        watcher.Created += (_, _) => BehaviourCodeTouched();
        watcher.Deleted += (_, _) => BehaviourCodeTouched();
        watcher.Renamed += (_, _) => BehaviourCodeTouched();
        watcher.Error += (_, _) => BehaviourCodeTouched();
        watcher.EnableRaisingEvents = true;
        _behaviourWatcher = watcher;
        BehaviourCodeTouched();
    }

    private void BehaviourCodeTouched()
    {
        Interlocked.Exchange(ref _behaviourCodeTouched, Environment.TickCount64);
        _behaviourAssetsDirty = true;
    }
}
