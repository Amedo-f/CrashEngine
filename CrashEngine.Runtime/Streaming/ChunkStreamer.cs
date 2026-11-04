using System.Numerics;
using CrashEngine.Runtime.Physics;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;

namespace CrashEngine.Runtime.Streaming;

// Amedo 2026-09-28
public sealed class StreamLink
{
    public const uint TargetPresent = 0x20000;
    public const uint TargetReady = 0x40000;

    public TwinChunkLink Source { get; }
    public string Path { get; }
    public uint Flags { get; set; }
    public StreamResource? Target { get; set; }
    public IReadOnlyList<ConvexHull> Boxes { get; }
    public WallHull? Wall { get; }
    public bool ViewerInside { get; set; }

    public StreamLink(TwinChunkLink source)
    {
        Source = source;
        Path   = Normalize(source.Path);
        uint file = (source.IsRendered ? 1u : 0u) | ((uint)(source.UnkNum & 0x3F) << 1) | (source.KeepLoaded ? 0x80u : 0u)
                  | (source.IsLoadWallActive ? 0x100u : 0u) | (source.LoadingWall is not null ? 0x80000u : 0u);
        Flags  = file | 0x10000;
        Boxes  = source.ChunkLinksCollisionData.Select(b => ConvexHull.From(b.BondingBoxBuilder)).ToArray();
        if (source.LoadingWall is { } lw)
        {
            static Vector3 C(Twinsanity.TwinsanityInterchange.Common.Vector4 v) => new(v.X, v.Y, v.Z);
            Wall = WallHull.Build_1EBCE0(C(lw.Column1), C(lw.Column2), C(lw.Column3), C(lw.Column4));
        }
    }

    public int Field => (int)((Flags >> 7) & 0x7F);

    public int RenderMode => (int)(Flags & 0x7F); // Amedo 2026-10-06

    // Amedo 2026-10-06
    public int Visibility
    {
        get => (int)((Flags >> 14) & 3);
        set => Flags = (Flags & ~0xC000u) | ((uint)(value & 3) << 14);
    }

    public static string Normalize(string path) => path.Replace('/', '\\').Trim().ToLowerInvariant();

    public static bool Inside(ConvexHull hull, Vector3 p)
    {
        foreach (var f in hull.Faces)
        {
            float d = f.X * p.X + f.Y * p.Y + f.Z * p.Z + f.W;
            if ((d < -5e-5f || 5e-5f < d) && 0f < d) return false;
        }
        return true;
    }
}

// Amedo 2026-09-28
public sealed class StreamResource
{
    public const uint LinksLoaded = 0x400; // Amedo 2026-09-30
    public const uint LinksQueued = 0x800; // Amedo

    public string Name { get; }
    public uint Flags4 { get; set; } = 0xFF;
    public int Frame { get; set; } = -1;
    public List<StreamLink>? Links { get; set; }
    public bool SceneryLoaded { get; set; }
    public bool ObjectsLoaded { get; set; }
    public object? Tag { get; set; }

    public StreamResource(string name) => Name = name;

    public int DepthA => (int)((Flags4 >> 4) & 0xF);
    public int DepthB => (int)(Flags4 & 0xF);
    public bool WantsScenery => DepthB < 2;
    public bool WantsObjects => DepthA < 2;

    public const int WaitNone = 0, WaitLoad = 1, WaitUnload = 2;
    private const long LoadDelay = 9000, UnloadDelay = LoadDelay * 4, UnreachedDelay = LoadDelay / 10;

    private int _waitKind;
    private long _waitStart, _waitTicks;

    public bool Waited_2AA020(int kind, long now)
    {
        if (kind == WaitNone)
        {
            _waitKind = WaitNone;
            return true;
        }
        if (kind != _waitKind)
        {
            _waitKind = kind;
            _waitStart = now;
            _waitTicks = kind == WaitLoad ? LoadDelay : (Flags4 & 0xFF) == 0xFF ? UnreachedDelay : UnloadDelay;
            return false;
        }
        return now - _waitStart > _waitTicks;
    }

    public override string ToString() => $"{Name} A{DepthA} B{DepthB}";
}

// Amedo 2026-09-28
public sealed class ChunkStreamer
{
    public List<StreamResource> Resources { get; } = new();
    public StreamResource? Root { get; set; }
    public int Frame { get; private set; }
    public int Mode { get; set; } = 5;
    public Vector3 Viewer { get; set; }
    public Action<string>? Trace { get; set; }
    public Func<StreamResource, bool>? Loading { get; set; } // Amedo
    public Action<StreamResource>? Removed { get; set; } // Amedo

    private static readonly List<StreamLink> NoLinks = new(); // Amedo

    public StreamResource? Find_2AA7F8(string name)
    {
        name = StreamLink.Normalize(name);
        foreach (var r in Resources)
            if (r.Name == name) return r;
        return null;
    }

    public StreamResource Create_2AA590(string name, int depth)
    {
        var r = Find_2AA7F8(name);
        if (r is null)
        {
            r = new StreamResource(StreamLink.Normalize(name));
            Resources.Add(r);
        }
        uint d = (uint)depth & 0xF;
        r.Flags4 = (r.Flags4 & 0xFFFFFF00) | (d << 4) | d;
        if (depth == 0) Root = r;
        return r;
    }

    public void Update_2AA8D0()
    {
        Frame++;
        if (Root is not null) Walk_2A9898(Root, Frame, 0, 0);
        foreach (var r in Resources)
            if (r.Frame != Frame) r.Flags4 |= 0xFF;
        for (int i = Resources.Count - 1; i >= 0; i--) // Amedo
        {
            var r = Resources[i];
            if (r.Frame != Frame && !r.SceneryLoaded && !r.ObjectsLoaded && !ReferenceEquals(r, Root))
            {
                Resources.RemoveAt(i);
                Removed?.Invoke(r); // Amedo
            }
        }
    }

    public void Walk_2A9898(StreamResource r, int frame, int a, int b)
    {
        bool better = a < r.DepthA;
        bool first = r.Frame != frame;
        bool notWorse = r.DepthB <= b;
        if (!first && !better && notWorse) return;
        if (first)
        {
            r.Flags4 = (r.Flags4 & 0xFFFFFF00) | ((uint)(a & 0xF) << 4) | (uint)(b & 0xF);
            r.Frame = frame;
        }
        else
        {
            if (better) r.Flags4 = (r.Flags4 & 0xFFFFFF0F) | ((uint)(a & 0xF) << 4);
            if (!notWorse) r.Flags4 = (r.Flags4 & 0xFFFFFFF0) | (uint)(b & 0xF);
        }
        if (Mode == 0) return; // Amedo 2026-09-30
        bool loaded = r.Links is { Count: > 0 }; // Amedo 2026-09-30
        bool queued = loaded; // Amedo
        if (r.Links is not { } links) links = NoLinks; // Amedo
        foreach (var link in links)
        {
            int na = a != 0xF && link.Field != 0 ? a + 1 : 0xF;
            int nb = b != 0xF ? b + 1 : 0xF;
            var target = (link.Target is { } held && Resources.Contains(held) ? held : null)
                         ?? Find_2AA7F8(link.Path) ?? (Mode != 1 ? Create_2AA590(link.Path, 0xF) : null);
            link.Target = target;
            link.Flags = (link.Flags & ~StreamLink.TargetPresent) | (target?.SceneryLoaded == true ? StreamLink.TargetPresent : 0u);
            link.Flags = (link.Flags & ~StreamLink.TargetReady) | (target?.ObjectsLoaded == true ? StreamLink.TargetReady : 0u);
            if (target is null) { loaded = queued = false; continue; } // Amedo 2026-09-30
            if (link.Boxes.Count > 0)
            {
                bool inside = false; // Amedo
                for (int bi = 0; bi < link.Boxes.Count && !inside; bi++)
                    inside = StreamLink.Inside(link.Boxes[bi], Viewer);
                if (inside != link.ViewerInside)
                {
                    link.ViewerInside = inside;
                    Trace?.Invoke($"[stream] {(inside ? "entered" : "left")} the pre-load box of {r.Name} -> {link.Path} at {Viewer}");
                }
                if (inside) Walk_2A9898(target, frame, na, nb);
            }
            else Walk_2A9898(target, frame, na, nb);
            if (link.Field != 0 && !target.ObjectsLoaded) loaded = false; // Amedo 2026-09-30
            if (!(target.SceneryLoaded || target.WantsScenery)) loaded = false;
            bool busy = Loading?.Invoke(target) == true; // Amedo
            if (link.Field != 0 && !target.ObjectsLoaded && busy) queued = false; // Amedo
            if (!target.SceneryLoaded && busy) queued = false; // Amedo
        }
        r.Flags4 = (r.Flags4 & ~StreamResource.LinksLoaded) | (loaded ? StreamResource.LinksLoaded : 0u); // Amedo 2026-09-30
        r.Flags4 = (r.Flags4 & ~StreamResource.LinksQueued) | (queued ? StreamResource.LinksQueued : 0u); // Amedo
    }
}
