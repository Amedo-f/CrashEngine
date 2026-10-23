using CrashEngine.Core;
using CrashEngine.Runtime.Streaming;

namespace CrashEngine.Runtime;

// Amedo 2026-10-06
public sealed partial class SceneSimulation
{
    public bool DrawLinkedChunks_309C52 { get; set; } = true;

    private readonly List<(Entity Entity, bool Active)> _drawSaved = new();
    private readonly HashSet<string> _drawn = new();

    private void InitChunkDraw()
    {
        _drawSaved.Clear();
        if (_chunkRoot is null) return;
        foreach (var c in _chunkRoot.Children)
            if (c.Name is "Scenery" or "Instances" or "LinkedScenery") _drawSaved.Add((c, c.Active));
        if (AnchorPart("LinkedScenery") is { } previews) previews.Active = false;
    }

    private void StopChunkDraw()
    {
        foreach (var (e, active) in _drawSaved) e.Active = active;
        _drawSaved.Clear();
        _drawn.Clear();
    }

    private Entity? AnchorPart(string name) => _chunkRoot?.Children.FirstOrDefault(c => c.Name == name);

    private void UpdateChunkDraw_1ECB58()
    {
        if (Streamer?.Root is not { } root)
        {
            World.RootChunk.DrawFrame = World.Frame; // Amedo
            return;
        }
        _drawn.Clear();
        _drawn.Add(root.Name);
        if (DrawLinkedChunks_309C52 && root.Links is { } links)
        {
            Portals_1EC9F8(links);
            foreach (var l in links)
                if (l.Target is { } t && l.RenderMode != 0 && (l.Flags & StreamLink.TargetPresent) != 0 && l.Visibility != 0) _drawn.Add(t.Name);
        }
        foreach (var c in World.Chunks) // Amedo
            if (_drawn.Contains(c.Name)) c.DrawFrame = World.Frame;
        if (_anchorResource is { } anchor)
        {
            bool shown = _drawn.Contains(anchor.Name) && (ReferenceEquals(anchor, root) || !_anchorUnloaded || anchor.SceneryLoaded);
            if (AnchorPart("Scenery") is { } scenery && scenery.Active != shown)
            {
                scenery.Active = shown;
                _log?.Invoke($"[draw {World.Now:0.00}s] the editor's scene {anchor.Name}: scenery {(shown ? "shown" : "hidden")} (in the drawn set {_drawn.Contains(anchor.Name)}, unloaded {_anchorUnloaded}, scenery loaded {anchor.SceneryLoaded})"); // Amedo
            }
            if (!_anchorUnloaded && AnchorPart("Instances") is { } instances && instances.Active != _drawn.Contains(anchor.Name))
                instances.Active = _drawn.Contains(anchor.Name);
        }
        foreach (var (r, s) in _streamed)
        {
            bool shown = _drawn.Contains(r.Name);
            if (s.Entity.Active != shown) s.Entity.Active = shown;
        }
        foreach (var (r, s) in _sceneryOnly)
        {
            bool shown = _drawn.Contains(r.Name);
            if (s.Entity.Active != shown) s.Entity.Active = shown;
        }
        if (!DrawKeysChanged(root)) return; // Amedo
        string summary = string.Join(", ", (root.Links ?? new List<StreamLink>()).Select(l =>
            $"{l.Path.Split('\\').Last()} m{l.RenderMode} v{l.Visibility}{((l.Flags & StreamLink.TargetPresent) == 0 ? " -" : "")}{(l.Target is { } t && _drawn.Contains(t.Name) ? " drawn" : "")}"));
        if (summary != _drawSummary || root.Name != _drawRoot)
        {
            _drawSummary = summary;
            _drawRoot = root.Name;
            _log?.Invoke($"[draw {World.Now:0.00}s] {root.Name.Split('\\').Last()}: {summary}");
        }
    }

    private string? _drawSummary, _drawRoot;
    private readonly List<int> _drawKeys = new(); // Amedo
    private List<StreamLink>? _drawLinks;
    private string? _drawKeysRoot;

    // Amedo
    private bool DrawKeysChanged(StreamResource root)
    {
        var links = root.Links;
        int count = links?.Count ?? 0;
        bool changed = !ReferenceEquals(root.Name, _drawKeysRoot) || !ReferenceEquals(links, _drawLinks) || count != _drawKeys.Count;
        if (count < _drawKeys.Count) _drawKeys.RemoveRange(count, _drawKeys.Count - count);
        for (int i = 0; i < count; i++)
        {
            var l = links![i];
            int key = l.RenderMode | (l.Visibility << 7) | ((l.Flags & StreamLink.TargetPresent) != 0 ? 1 << 9 : 0)
                    | (l.Target is { } t && _drawn.Contains(t.Name) ? 1 << 10 : 0);
            if (i == _drawKeys.Count) _drawKeys.Add(key);
            else if (_drawKeys[i] != key) _drawKeys[i] = key;
            else continue;
            changed = true;
        }
        _drawKeysRoot = root.Name;
        _drawLinks = links;
        return changed;
    }

    private void Portals_1EC9F8(List<StreamLink> links)
    {
        foreach (var l in links)
        {
            if (l.Wall is not { } wall || l.RenderMode == 1)
            {
                l.Visibility = 1;
                continue;
            }
            var cam = GameCamera;
            if (cam is null && PortalVisibilityOverride is null)
            {
                l.Visibility = 2;
                continue;
            }
            var min = wall.Corners[0];
            var max = wall.Corners[0];
            for (int i = 1; i < 4; i++)
            {
                min = System.Numerics.Vector3.Min(min, wall.Corners[i]);
                max = System.Numerics.Vector3.Max(max, wall.Corners[i]);
            }
            l.Visibility = PortalVisibilityOverride?.Invoke(min, max) ?? cam!.BoxVisibility_1F5F90(min, max);
        }
    }

    public Func<System.Numerics.Vector3, System.Numerics.Vector3, int>? PortalVisibilityOverride { get; set; } // Amedo

    public IEnumerable<(string Target, int Mode, int Visibility, bool Present, bool Drawn)> DrawState() // Amedo
    {
        if (Streamer?.Root?.Links is not { } links) yield break;
        foreach (var l in links)
            yield return (l.Path.Split('\\').Last(), l.RenderMode, l.Visibility, (l.Flags & StreamLink.TargetPresent) != 0, l.Target is { } t && _drawn.Contains(t.Name));
    }
}
