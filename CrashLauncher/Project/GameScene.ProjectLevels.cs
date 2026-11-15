using CrashEngine.Core;
using CrashEngine.Importer;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private int _browserRevision = -1;

    // Amedo 2026-11-12
    private void SyncWithBrowser()
    {
        if (_browserRevision == _browser.Revision) return;
        _browserRevision = _browser.Revision;
        _gameProjectLoaded = false;
        _swapLevelList = null;
        _sceneFiles = null;
    }

    private CrashProject? GetGameProject()
    {
        SyncWithBrowser(); // Amedo 2026-11-12
        if (_gameProjectLoaded) return _gameProject;
        _gameProjectLoaded = true;
        var projectRoot = Path.GetDirectoryName(_scriptOut) ?? _extractedRoot;
        _gameProject = CrashProject.Open(projectRoot);
        return _gameProject;
    }

    // Amedo 2026-09-22
    private string ModeDir(string name) =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot,
            GetGameProject() is { IsNewGame: true } ? name + "_NewGame" : name);

    private string SavedChunksDir => ModeDir("SavedChunks");

    private List<string> GetSwapLevelList()
    {
        SyncWithBrowser(); // Amedo 2026-11-12
        if (_swapLevelList is not null) return _swapLevelList;
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            foreach (var rec in pkg.GetByExtension(".rm2"))
            {
                var rel = rec.Path.Replace('/', '\\');
                if (string.Equals(rel, _rm2, StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.EndsWith(@"Startup\Default.rm2", StringComparison.OrdinalIgnoreCase)) continue;
                if (seen.Add(rel)) list.Add(rel);
            }
        }
        catch (Exception ex) { _browser.Log($"Swap Object: level scan failed: {ex.Message}"); }

        try
        {
            var savedChunksDir = SavedChunksDir;
            if (Directory.Exists(savedChunksDir))
            {
                foreach (var file in Directory.GetFiles(savedChunksDir, "*.rm2", SearchOption.AllDirectories))
                {
                    var rel = GamePaths.FromDisk(Path.GetRelativePath(savedChunksDir, file));
                    if (string.Equals(rel, _rm2, StringComparison.OrdinalIgnoreCase)) continue;
                    if (seen.Add(rel)) list.Add(rel);
                }
            }
        }
        catch (Exception ex) { _browser.Log($"Swap Object: SavedChunks scan failed: {ex.Message}"); }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        _swapLevelList = list;
        return list;
    }

    private PS2AnyTwinsanityRM2? GetForeignRm2(string rm2Path)
    {
        if (_foreignRm2Cache.TryGetValue(rm2Path, out var cached)) return cached;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            var rm2 = ChunkImporter.ParseRm2Only(pkg, rm2Path);
            _foreignRm2Cache[rm2Path] = rm2;
            return rm2;
        }
        catch (Exception ex)
        {
            _browser.Log($"Swap Object: failed to open {rm2Path}: {ex.Message}");
            return null;
        }
    }
}
