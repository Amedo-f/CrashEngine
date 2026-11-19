using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Runtime.AgentLab;
using CrashEngine.Runtime.Streaming;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;

namespace CrashEngine.Runtime;

// Amedo 2026-09-28
public sealed partial class SceneSimulation
{
    private sealed class StreamedChunk
    {
        public required Entity Entity;
        public required WorldChunk Chunk;
        public Entity? Preview;
        public Renderer.GpuUploadQueue? Uploads; // Amedo 2026-09-29
        public int ParticleBank = -1;
    }

    private readonly Dictionary<string, (Entity Root, Renderer.GpuUploadQueue? Uploads)> _built = new(); // Amedo 2026-09-29
    private readonly List<string> _builtOrder = new();
    private readonly Renderer.GpuUploadQueue _stillUsed = new();

    public static int KeepBuiltScenes { get; set; } = 0;

    public string StreamCounts => $"built {_built.Count}, streamed {_streamed.Count}, scenery-only {_sceneryOnly.Count}, parsing {_parsing.Count}, dropped {_dropped.Count}";

    private HashSet<object> UsedGpuResources()
    {
        var used = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<Entity>();
        if (_chunkRoot?.Scene is { } scene) foreach (var e in scene.Roots) stack.Push(e);
        foreach (var (_, kept) in _built) stack.Push(kept.Root);
        foreach (var agent in World.Agents) if (!agent.Destroyed) stack.Push(agent.Entity); // Amedo 2026-11-10
        foreach (var (mesh, material) in Renderer.RenderPipeline.SkydomeDraws) AddUsed(used, mesh, material);
        if (Renderer.RenderPipeline.SkydomeOverride is { } sky) foreach (var (mesh, material) in sky) AddUsed(used, mesh, material);
        while (stack.Count > 0)
        {
            var e = stack.Pop();
            foreach (var c in e.Components)
                switch (c)
                {
                    case MeshRenderer m: AddUsed(used, m.Mesh, m.Material); break;
                    case Renderer.MeshRenderer m: AddUsed(used, m.Mesh, m.Material); break;
                }
            foreach (var child in e.Children) stack.Push(child);
        }
        return used;
    }

    private static void AddUsed(HashSet<object> used, object? mesh, Renderer.Material? material)
    {
        if (mesh is not null) used.Add(mesh);
        if (material is null) return;
        if (material.Albedo is { } texture) used.Add(texture);
        if (material.Layers is { } layers) foreach (var layer in layers) AddUsed(used, null, layer);
    }

    private void KeepBuilt(string name, (Entity Root, Renderer.GpuUploadQueue? Uploads) built)
    {
        _built.TryGetValue(name, out var replaced);
        _built[name] = built;
        _builtOrder.Remove(name);
        _builtOrder.Add(name);
        if (replaced.Root is not null && !ReferenceEquals(replaced.Root, built.Root)) FreeBuilt(name, replaced);
        while (_built.Count > KeepBuiltScenes && _builtOrder.Count > 0)
        {
            string oldest = _builtOrder[0];
            _builtOrder.RemoveAt(0);
            if (_built.Remove(oldest, out var freed)) FreeBuilt(oldest, freed);
        }
    }

    private readonly Queue<(string Name, (Entity Root, Renderer.GpuUploadQueue? Uploads) Built)> _freeLater = new(); // Amedo 2026-11-14
    private readonly Renderer.GpuUploadQueue _disposeLater = new();
    private const double DisposeBudgetMs = 0.5;

    private void FreeBuilt(string name, (Entity Root, Renderer.GpuUploadQueue? Uploads) freed) => _freeLater.Enqueue((name, freed)); // Amedo 2026-11-14

    // Amedo 2026-11-14
    private void FreeBuiltNow(string name, (Entity Root, Renderer.GpuUploadQueue? Uploads) freed)
    {
        if (freed.Uploads is { } uploads)
        {
            long walk = System.Diagnostics.Stopwatch.GetTimestamp();
            int count = uploads.Count, done = uploads.MoveUnused(UsedGpuResources(), _stillUsed, _disposeLater);
            _log?.Invoke($"[stream] freed the built {name} ({done} of {count} GPU resources, the rest still used) to keep {KeepBuiltScenes} left scene(s): used-walk {System.Diagnostics.Stopwatch.GetElapsedTime(walk).TotalMilliseconds:0.0} ms, {_disposeLater.Count} waiting to be deleted");
        }
        CutTree(freed.Root);
    }

    // Amedo 2026-11-14
    private void FreeLaterStep()
    {
        if (_freeLater.Count > 0)
        {
            var (name, built) = _freeLater.Dequeue();
            FreeBuiltNow(name, built);
            return;
        }
        if (_disposeLater.Count > 0) _disposeLater.DisposeSome(DisposeBudgetMs);
    }

    private static void CutTree(Entity root)
    {
        GameLightingDraw.ForgetChunk(root);
        root.RemoveComponent<ChunkSource>();
        var stack = new Stack<Entity>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var e = stack.Pop();
            if (e.Has<AgentLabAgent>()) continue;
            foreach (var child in e.Children) stack.Push(child);
            e.ForgetChildren();
        }
    }

    // Amedo 2026-10-06
    private sealed class SceneryOnlyChunk
    {
        public required Entity Entity;
        public WorldChunk? Chunk;
        public Renderer.GpuUploadQueue? Uploads;
        public readonly List<(Entity Entity, bool Active)> Hidden = new();
    }

    private readonly Dictionary<StreamResource, SceneryOnlyChunk> _sceneryOnly = new(); // Amedo 2026-10-06

    private ChunkHost? _chunkHost;
    private readonly Dictionary<string, Task<object?>> _parsing = new();
    private readonly Dictionary<string, Task<object?>> _dropped = new(); // Amedo

    // Amedo 2026-09-29
    private sealed class PreparedChunk
    {
        public Entity? Root;
        public Renderer.GpuUploadQueue Uploads { get; init; } = new();
        public bool Cached { get; init; }
    }

    private static Task _lane = Task.CompletedTask; // Amedo 2026-09-29
    private static readonly object LaneLock = new();
    private Entity? _chunkRoot;
    private WorldChunk? _anchorChunk;
    private StreamResource? _anchorResource;
    private readonly List<(Entity Entity, Entity Home, int Index)> _moved = new(); // Amedo

    // Amedo
    private bool Moved(Entity e)
    {
        foreach (var x in _moved)
            if (ReferenceEquals(x.Entity, e)) return true;
        return false;
    }

    // Amedo
    private void RememberHome(Entity e)
    {
        if (Moved(e) || e.Parent is not { } parent) return;
        int index = 0;
        while (index < parent.Children.Count && !ReferenceEquals(parent.Children[index], e)) index++;
        _moved.Add((e, parent, index));
    }

    private readonly Dictionary<string, uint> _lastDepths = new();

    // Amedo 2026-09-29
    private PS2AnyTwinsanitySM2? _anchorSm2;
    private bool _anchorUnloaded;
    private readonly List<(Entity Entity, bool Active)> _anchorHidden = new();
    private static readonly string[] AnchorContent = { "Scenery", "Instances", "LinkedScenery" };
    private int _anchorBank = -1;
    private bool _crossedThisTick;

    private static Entity? EmittersOf(Entity? root) => root?.Children.FirstOrDefault(c => c.Name == "ParticleEmitters");

    public Matrix4x4 RootToWorld => World.RootChunk.ToAnchor * (_gameSpace?.Transform.World ?? Matrix4x4.Identity);
    private readonly Dictionary<StreamResource, StreamedChunk> _streamed = new();

    public ChunkStreamer? Streamer { get; private set; }

    private static List<StreamLink> LinksOf(PS2AnyTwinsanitySM2? sm2) =>
        sm2?.GetItem<PS2AnyLink>((uint)Constants.SCENERY_LINK_ITEM)?.LinksList.Select(l => new StreamLink(l)).ToList() ?? new();

    private void InitStreaming(Entity chunkRoot, ChunkSource src, ChunkHost? chunkHost)
    {
        _chunkRoot = chunkRoot;
        _chunkHost = chunkHost;
        string rootName = StreamLink.Normalize(Path.ChangeExtension(src.Rm2Path, null) ?? src.Rm2Path);
        Streamer = new ChunkStreamer { Trace = _log, Loading = r => _parsing.ContainsKey(r.Name), Removed = DropParse }; // Amedo
        var root = Streamer.Create_2AA590(rootName, 0);
        root.SceneryLoaded = root.ObjectsLoaded = true;
        root.Links = LinksOf(src.Sm2);
        root.Tag = World.RootChunk;
        _anchorChunk = World.RootChunk;
        _anchorResource = root;
        _anchorSm2 = src.Sm2; // Amedo 2026-09-29
        ReadSceneryShaders_1CCF80(src.Sm2); // Amedo
        _anchorUnloaded = false;
        _anchorHidden.Clear();
        World.RootChunk.Host = chunkRoot;
        World.ChunkLink_1F1CC0 = (from, to) => ResourceOf(from)?.Links?.FirstOrDefault(l => ReferenceEquals(l.Target?.Tag, to)); // Amedo 2026-10-05
        World.InCameraFrustum_227380 = p => GameCamera is { } cam && cam.InFrustum_227380(p); // Amedo 2026-10-05
        World.CameraObjectPosition = () => GameCamera is { } cam ? cam.Pose.Translation : null; // Amedo
        World.CameraObjectMatrix = () => GameCamera is { } cam ? cam.Pose : null; // Amedo
        InitChunkDraw(); // Amedo 2026-10-06
        InitPlaces(); // Amedo
    }

    private Entity? InstancesOf(WorldChunk chunk) =>
        ReferenceEquals(chunk, _anchorChunk) ? _gameSpace : chunk.Host?.Children.FirstOrDefault(c => c.Name == "Instances");

    // Amedo
    private StreamResource? ResourceOf(WorldChunk chunk)
    {
        if (Streamer is not { } streamer) return null;
        foreach (var r in streamer.Resources)
            if (ReferenceEquals(r.Tag, chunk)) return r;
        return null;
    }

    private readonly List<StreamResource> _stepResources = new(); // Amedo

    private void CheckCrossing_1ED690()
    {
        if (Streamer is null || World.Player is not { Destroyed: false } player || World.PlayerCharacter is not { } character) return;
        if (ResourceOf(player.Chunk)?.Links is not { } links) return;
        var best = Select_1ED690(links, player.Chunk, player.GamePosition); // Amedo 2026-09-29
        if (best?.Target is { ObjectsLoaded: false } pending && _parsing.ContainsKey(pending.Name)) FinishNow(pending); // Amedo 2026-09-29
        if (best?.Target is { ObjectsLoaded: true, Tag: WorldChunk target } && !ReferenceEquals(target, player.Chunk))
            Cross_197650(player, character, best, target);
        CheckCharacterCrossings_1979A8(); // Amedo 2026-09-29
    }

    // Amedo 2026-10-02
    private bool WillCross_1ED690()
    {
        if (Streamer is null || World.Player is not { Destroyed: false } player || World.PlayerCharacter is null) return false;
        if (ResourceOf(player.Chunk)?.Links is not { } links) return false;
        return Select_1ED690(links, player.Chunk, player.GamePosition)?.Target is { Tag: WorldChunk target } t
            && !ReferenceEquals(target, player.Chunk) && (t.ObjectsLoaded || _parsing.ContainsKey(t.Name));
    }

    // Amedo 2026-09-29
    private static StreamLink? Select_1ED690(List<StreamLink> links, WorldChunk chunk, Vector3 pos)
    {
        StreamLink? best = null;
        int bestField = 1;
        foreach (var l in links)
            if (bestField < l.Field && RegionTest_1E9B48(l, pos))
            {
                best = l;
                bestField = l.Field;
            }
        if (best is null && !InsideRoot(chunk, pos))
            foreach (var l in links)
                if (RegionTest_1E9B48(l, pos)) return l;
        return best;
    }

    // Amedo 2026-09-29
    private void CheckCharacterCrossings_1979A8()
    {
        var agents = World.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            if (agent.Destroyed || ReferenceEquals(agent, World.Player)) continue; // Amedo
            if (!agent.InChunk_A0) continue; // Amedo 2026-11-17
            if ((agent.AgentFlags & 0x20000) != 0x20000) continue; // Amedo 2026-10-05
            if (ResourceOf(agent.Chunk)?.Links is not { } links) continue;
            var best = Select_1ED690(links, agent.Chunk, agent.GamePosition);
            if (best?.Target is not { ObjectsLoaded: true, Tag: WorldChunk target } || ReferenceEquals(target, agent.Chunk)) continue;
            if (InstancesOf(target) is not { } home) continue;
            var from = agent.Chunk;
            var moving = new List<AgentLabAgent>();
            Transfer_197650(agent, MeshDecoder.TwinMatToSys(best.Source.ObjectMatrix), target, home, moving);
            _log?.Invoke($"[stream] character crossed {from.Name} -> {target.Name} at {agent.GamePosition} ({string.Join(", ", moving.Select(a => a.Entity.Name.Split("_act_").Last()))})");
        }
    }

    private static bool InsideRoot(WorldChunk chunk, Vector3 p) =>
        chunk.Tree?.Root is not { } n || (n.Min.X <= p.X && p.X <= n.Max.X && n.Min.Y <= p.Y && p.Y <= n.Max.Y && n.Min.Z <= p.Z && p.Z <= n.Max.Z);

    private static bool RegionTest_1E9B48(StreamLink link, Vector3 p)
    {
        if (link.Wall is { } wall) return wall.Contains_1EC0C8(p);
        if ((link.Flags & StreamLink.TargetReady) == 0 || (link.Flags & 0x3F80) == 0 || link.Target?.Tag is not WorldChunk target) return false;
        return InsideRoot(target, Vector3.Transform(p, MeshDecoder.TwinMatToSys(link.Source.ObjectMatrix)));
    }

    private void Cross_197650(AgentLabAgent player, Character.CharacterController character, StreamLink link, WorldChunk target)
    {
        if (InstancesOf(target) is null) return;
        var from = player.Chunk;
        var obj = MeshDecoder.TwinMatToSys(link.Source.ObjectMatrix);
        var moving = new List<AgentLabAgent>(); // Amedo 2026-09-29
        AdoptPlayerChunk(player, character, obj, from, target, moving);
    }

    // Amedo 2026-09-29
    private void AdoptPlayerChunk(AgentLabAgent player, Character.CharacterController character, Matrix4x4 obj, WorldChunk from, WorldChunk target, List<AgentLabAgent> moving)
    {
        if (InstancesOf(target) is not { } home) return;
        Transfer_197650(player, obj, target, home, moving);
        World.SetRoot(target);
        _crossedThisTick = true; // Amedo 2026-09-29
        if (target.Collision is { } collision) character.CollisionWorld = collision;
        if (ResourceOf(target) is { } r) Streamer!.Root = r;
        PlaceLinked_1E9C18(); // Amedo
        if (GameCamera is { } cam)
        {
            cam.Warp(obj);
            cam.Camera.Collision = target.Collision;
            cam.ReloadZones();
        }
        _log?.Invoke($"[stream] crossed {from.Name} -> {target.Name} at {player.GamePosition} ({moving.Count} object(s) moved: {string.Join(", ", moving.Select(a => a.Entity.Name.Split("_act_").Last()))})"); // Amedo 2026-09-29
    }

    // Amedo 2026-09-29
    private void Transfer_197650(AgentLabAgent agent, Matrix4x4 obj, WorldChunk target, Entity home, List<AgentLabAgent> moving)
    {
        if (moving.Contains(agent)) return;
        moving.Add(agent);
        foreach (var linked in agent.Links.ToList())
            if (linked is not null) Transfer_197650(linked, obj, target, home, moving);
        Transfer(agent, obj, target, home, moving);
    }

    private void Transfer(AgentLabAgent agent, Matrix4x4 obj, WorldChunk target, Entity home, List<AgentLabAgent> moving)
    {
        var from = agent.Chunk; // Amedo 2026-09-29
        var m = agent.GameMatrix * obj;
        var e = agent.Entity;
        RememberHome(e); // Amedo
        e.Parent?.RemoveChild(e);
        home.AddChild(e);
        agent.GameMatrix = m;
        CarryDisplay(e, obj); // Amedo 2026-11-14
        agent.CanChangeChunk_23D060(target); // Amedo
        agent.Projectile?.CanChangeChunk_11EFB8(obj); // Amedo
        agent.Chunk = target;
        if (agent.Component5 is { } body && !ReferenceEquals(body, agent.OwnCharacter?.Body5)) body.Transfer_28D698(obj); // Amedo 2026-11-03
        if (agent.OwnCharacter is not { } c) return; // Amedo 2026-09-29
        c.Body5?.Transfer_28D698(obj);
        c.Transfer_13BEA0(obj);
        if (c.Link is { IsLeader: true } link && link.Follower_15FE98() is { } fc // Amedo 2026-10-05
            && World.Agents.FirstOrDefault(a => ReferenceEquals(a.OwnCharacter, fc)) is { } follower && ReferenceEquals(follower.Chunk, from))
            Transfer_197650(follower, obj, target, home, moving);
        if (c.Vehicle is Character.Vehicles.Hoverboard { Board: AgentLabAgent board } && ReferenceEquals(board.Chunk, from)) // Amedo
            Transfer_197650(board, obj, target, home, moving);
        c.Vehicle?.UpdateFrames(); // Amedo 2026-09-29
        if (!ReferenceEquals(agent, World.Player) && target.Collision is { } collision) c.CollisionWorld = collision;
        if (c.Vehicle is Character.Vehicles.PassengerVehicle { Other: { } driverC } && World.Agents.FirstOrDefault(a => ReferenceEquals(a.OwnCharacter, driverC)) is { } driver
            && ReferenceEquals(driver.Chunk, from))
        {
            if (ReferenceEquals(driver, World.Player)) AdoptPlayerChunk(driver, World.PlayerCharacter!, obj, from, target, moving);
            else Transfer_197650(driver, obj, target, home, moving);
        }
    }

    // Amedo
    private void PlaceLinked_1E9C18()
    {
        if (Streamer?.Root?.Links is not { } links) return;
        var root = World.RootChunk;
        foreach (var l in links)
        {
            if (l.Target is not { } t || links.First(x => ReferenceEquals(x.Target, t)) != l) continue;
            if (ReferenceEquals(t, _anchorResource) || t.Name == _anchorResource?.Name) // Amedo
            {
                var wants = MeshDecoder.TwinMatToSys(l.Source.ChunkMatrix) * root.ToAnchor;
                if (_anchorChunk is { } a && !ReferenceEquals(a, root) && !Near(a.ToAnchor, wants)) MoveAroundAnchor(MeshDecoder.TwinMatToSys(l.Source.ChunkMatrix), root, wants); // Amedo
                continue;
            }
            Entity? e;
            WorldChunk? chunk = null;
            int bank = -1;
            if (_streamed.TryGetValue(t, out var s)) (e, chunk, bank) = (s.Entity, s.Chunk, s.ParticleBank);
            else if (_sceneryOnly.TryGetValue(t, out var held)) e = held.Entity;
            else continue;
            if (ReferenceEquals(chunk, root)) continue;
            var toAnchor = MeshDecoder.TwinMatToSys(l.Source.ChunkMatrix) * root.ToAnchor;
            var was = chunk?.ToAnchor ?? e.Transform.LocalMatrix ?? Matrix4x4.Identity;
            if (Near(was, toAnchor)) continue;
            e.Transform.LocalMatrix = toAnchor;
            if (chunk is not null)
            {
                chunk.ToAnchor = toAnchor;
                if (bank >= 0) World.Particles?.MoveBank(bank, was, toAnchor);
            }
            _log?.Invoke($"[stream] {t.Name} placed by the link of {root.Name}: {was.Translation} -> {toAnchor.Translation}");
        }
        World.SetRoot(root);
    }

    // Amedo
    private void MoveAroundAnchor(Matrix4x4 link, WorldChunk root, Matrix4x4 wants)
    {
        if (!Matrix4x4.Invert(link, out var rootAt) || !Matrix4x4.Invert(root.ToAnchor, out var fromRoot)) return;
        var move = fromRoot * rootAt;
        foreach (var s in _streamed.Values)
        {
            var was = s.Chunk.ToAnchor;
            s.Chunk.ToAnchor = was * move;
            s.Entity.Transform.LocalMatrix = s.Chunk.ToAnchor;
            if (s.ParticleBank >= 0) World.Particles?.MoveBank(s.ParticleBank, was, s.Chunk.ToAnchor);
        }
        foreach (var held in _sceneryOnly.Values)
            if (held.Entity.Transform.LocalMatrix is { } m) held.Entity.Transform.LocalMatrix = m * move;
        World.MoveRespawn(move, _anchorChunk?.Name);
        SnapDisplay();
        _log?.Invoke($"[stream] the link of {root.Name} puts the editor's scene at {wants.Translation}: moved the {_streamed.Count + _sceneryOnly.Count} other scene(s) by {move.Translation} so it stays where it is");
    }

    private static bool Near(Matrix4x4 a, Matrix4x4 b)
    {
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                if (MathF.Abs(a[i, j] - b[i, j]) > 1e-3f) return false;
        return true;
    }

    private void ReturnToAnchor(bool reviveUnloaded = true)
    {
        if (_anchorChunk is null) return;
        bool revive = reviveUnloaded || !_anchorUnloaded; // Amedo 2026-11-10
        if (revive) ShowAnchor(); // Amedo 2026-09-29
        for (int i = _moved.Count - 1; i >= 0; i--) // Amedo
        {
            var (e, homeParent, index) = _moved[i];
            if (e.Parent is null && e.Get<AgentLabAgent>() is { SpawnedBy: not null } gone && !World.Spawned.Contains(gone)) continue; // Amedo
            e.Parent?.RemoveChild(e);
            homeParent.InsertChild(index, e); // Amedo
        }
        _moved.Clear();
        SnapDisplay(); // Amedo 2026-09-30
        if (!revive) return; // Amedo 2026-11-10
        World.SetRoot(_anchorChunk);
        if (Streamer is not null && _anchorResource is not null) Streamer.Root = _anchorResource; // Amedo
        PlaceLinked_1E9C18(); // Amedo
        foreach (var a in World.Agents)
            if (a.Entity.Parent is { } p && ReferenceEquals(p, _gameSpace)) { a.CanChangeChunk_23D060(_anchorChunk); a.Chunk = _anchorChunk; } // Amedo
    }

    private bool _streamFocus = true; // Amedo 2026-11-16

    private void StepStreaming(bool block = false, bool takeFocus = true)
    {
        if (Streamer is not { Root: { } root } streamer || World.Player is not { } player) return;
        FreeLaterStep(); // Amedo 2026-11-14
        if (World.TakeStreamFocusRelease_2AA3E8()) _streamFocus = false; // Amedo 2026-11-16
        if (takeFocus && World.GameMode is AgentLabWorld.ModePlay or AgentLabWorld.ModeCutscene) _streamFocus = true; // Amedo 2026-11-16
        streamer.HasViewer = _streamFocus; // Amedo 2026-11-16
        streamer.Viewer = player.GamePosition;
        streamer.Update_2AA8D0();
        if (_dropped.Count > 0) KeepDropped(); // Amedo
        World.RootLinksLoaded = (root.Flags4 & StreamResource.LinksLoaded) != 0; // Amedo 2026-09-30
        World.RootLinksQueued = (root.Flags4 & StreamResource.LinksQueued) != 0; // Amedo
        bool holdUnload = _crossedThisTick; // Amedo 2026-09-29
        _crossedThisTick = false;
        _stepResources.Clear(); // Amedo
        _stepResources.AddRange(streamer.Resources);
        foreach (var r in _stepResources)
        {
            if (holdUnload && !r.WantsObjects && r.ObjectsLoaded) continue; // Amedo 2026-09-29
            if (!_lastDepths.TryGetValue(r.Name, out uint was) || was != (r.Flags4 & 0xFF))
            {
                _lastDepths[r.Name] = r.Flags4 & 0xFF;
                _log?.Invoke($"[stream] {r.Name} A{r.DepthA} B{r.DepthB} at {World.Now:0.00}s (viewer {player.GamePosition}, root {root.Name})");
            }
            if (ReferenceEquals(r, root))
            {
                r.Waited_2AA020(StreamResource.WaitNone, World.ClockTicks);
                continue;
            }
            if (r.Name == _anchorResource?.Name) // Amedo 2026-09-29
            {
                if (_anchorUnloaded) _anchorResource = r; // Amedo
                int anchorWait = !r.WantsObjects && !_anchorUnloaded ? StreamResource.WaitUnload
                               : r.WantsObjects && _anchorUnloaded ? StreamResource.WaitLoad : StreamResource.WaitNone;
                if (r.Waited_2AA020(anchorWait, World.ClockTicks) || block)
                {
                    if (!r.WantsObjects && !_anchorUnloaded) UnloadAnchor(r);
                    else if (r.WantsObjects && _anchorUnloaded) RestoreAnchor(r);
                }
                if (_anchorUnloaded) r.SceneryLoaded = r.WantsScenery; // Amedo 2026-10-06
                continue;
            }
            int wait = r.WantsObjects && !r.ObjectsLoaded ? StreamResource.WaitLoad
                     : !r.WantsObjects && r.ObjectsLoaded ? StreamResource.WaitUnload
                     : r.WantsScenery && !r.SceneryLoaded ? StreamResource.WaitLoad
                     : !r.WantsScenery && _sceneryOnly.ContainsKey(r) ? StreamResource.WaitUnload : StreamResource.WaitNone;
            if (!r.Waited_2AA020(wait, World.ClockTicks) && !block) continue;
            if (r.WantsObjects && !r.ObjectsLoaded) // Amedo 2026-10-06
            {
                if (_sceneryOnly.ContainsKey(r))
                {
                    if (ObjectsWaitForUnload(r, root) is null && _sceneryOnly.Remove(r, out var held)) UpgradeScenery(r, root, held); // Amedo 2026-11-10
                }
                else RequestParse(r);
            }
            else if (!r.WantsObjects && r.ObjectsLoaded) UnloadObjects(r, r.WantsScenery); // Amedo 2026-10-06
            else if (r.WantsScenery && !r.SceneryLoaded) RequestParse(r); // Amedo 2026-10-06
            else if (!r.WantsScenery && _sceneryOnly.ContainsKey(r)) DetachScenery(r); // Amedo 2026-10-06
        }
        if (block) Task.WaitAll(_parsing.Values.ToArray());
        var budget = System.Diagnostics.Stopwatch.StartNew(); // Amedo 2026-09-29
        double budgetMs = block ? double.PositiveInfinity : Math.Min(_chunkHost?.UploadBudgetMs ?? 3.0, CrashEngine.Core.PerfStats.FrameBudgetMs * 0.25); // Amedo 2026-11-14
        _stepResources.Clear(); // Amedo
        _stepResources.AddRange(streamer.Resources);
        if (!block && !_tapeLoadsFree && InputTape.Current is { Replaying: true } tape) // Amedo
        {
            foreach (var name in tape.FinishesThisStep)
            {
                var r = _stepResources.FirstOrDefault(x => x.Name == name);
                if (r is null || !_parsing.TryGetValue(name, out var planned))
                {
                    _log?.Invoke($"[tape] step {tape.Step}: recorded load of {name} is not pending here (the replay left the recording: loads finish on their own from now on)");
                    _tapeLoadsFree = true; // Amedo
                    continue;
                }
                planned.Wait();
                TryFinish(r, root, planned, double.PositiveInfinity);
            }
            return;
        }
        foreach (var r in _stepResources)
        {
            if (ReferenceEquals(r, root) || r.ObjectsLoaded || !_parsing.TryGetValue(r.Name, out var task) || !task.IsCompleted) continue;
            if (TryFinish(r, root, task, budgetMs - budget.Elapsed.TotalMilliseconds) && !block) InputTape.Current?.Finished(r.Name); // Amedo
        }
    }

    private bool _tapeLoadsFree; // Amedo

    // Amedo 2026-11-10
    private StreamResource? ObjectsWaitForUnload(StreamResource r, StreamResource root)
    {
        if (Streamer is not { } streamer) return null;
        foreach (var x in streamer.Resources)
            if (!ReferenceEquals(x, r) && !ReferenceEquals(x, root) && x.ObjectsLoaded && !x.WantsObjects) return x;
        return null;
    }

    // Amedo 2026-09-29
    private bool TryFinish(StreamResource r, StreamResource root, Task<object?> task, double budgetMs)
    {
        var result = task.IsCompletedSuccessfully ? task.Result : null;
        if (!r.WantsObjects && !(r.WantsScenery && !r.SceneryLoaded)) // Amedo 2026-10-06
        {
            _parsing.Remove(r.Name);
            if (result is PreparedChunk { Root: { } unwanted } kept) KeepBuilt(r.Name, (unwanted, kept.Uploads)); // Amedo 2026-09-29
            return false;
        }
        var heldBy = r.WantsObjects ? ObjectsWaitForUnload(r, root) : null; // Amedo 2026-11-10
        string waits = heldBy is null ? "" : $"; its objects wait until {heldBy.Name} unloads"; // Amedo 2026-11-10
        if (!r.WantsObjects || heldBy is not null) // Amedo 2026-10-06
        {
            if (result is PreparedChunk scenery)
            {
                if (budgetMs <= 0 && scenery.Uploads.Done < scenery.Uploads.Count) return false;
                if (!scenery.Uploads.Upload(budgetMs)) return false;
                _parsing.Remove(r.Name);
                AttachScenery(r, root, scenery.Root, scenery.Uploads, (scenery.Cached ? "from the cache" : "background build") + waits);
                return true;
            }
            _parsing.Remove(r.Name);
            AttachScenery(r, root, result is null || _chunkHost is null ? null : _chunkHost.Build(r.Name, result), null, "built" + waits);
            return true;
        }
        if (result is PreparedChunk prepared)
        {
            if (budgetMs <= 0 && prepared.Uploads.Done < prepared.Uploads.Count) return false; // Amedo 2026-09-29
            if (!prepared.Uploads.Upload(budgetMs)) return false;
            _parsing.Remove(r.Name);
            LoadObjects(r, root, prepared.Root, prepared.Cached ? "from the cache"
                            : $"background build, {prepared.Uploads.Count} GPU uploads in {prepared.Uploads.UploadMs:0} ms over {prepared.Uploads.Slices} step(s)", // Amedo 2026-09-30
                        prepared.Uploads);
            return true;
        }
        _parsing.Remove(r.Name);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var built = result is null || _chunkHost is null ? null : _chunkHost.Build(r.Name, result);
        LoadObjects(r, root, built, $"built {sw.ElapsedMilliseconds} ms", null);
        return true;
    }

    // Amedo 2026-09-29
    private bool FinishNow(StreamResource r)
    {
        if (Streamer?.Root is not { } root || !_parsing.TryGetValue(r.Name, out var task)) return false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        task.Wait();
        bool done = TryFinish(r, root, task, double.PositiveInfinity);
        _log?.Invoke($"[stream] reached the wall of {r.Name} before it finished loading: waited {sw.ElapsedMilliseconds} ms");
        return done;
    }

    // Amedo
    private void DropParse(StreamResource r)
    {
        if (_parsing.Remove(r.Name, out var task)) _dropped[r.Name] = task;
    }

    // Amedo
    private void KeepDropped()
    {
        _droppedDone.Clear();
        foreach (var (name, task) in _dropped)
            if (task.IsCompleted) _droppedDone.Add(name);
        foreach (var name in _droppedDone)
        {
            _dropped.Remove(name, out var task);
            if (task!.IsCompletedSuccessfully && task.Result is PreparedChunk { Root: { } kept } p && !_built.ContainsKey(name)) KeepBuilt(name, (kept, p.Uploads));
        }
    }

    private readonly List<string> _droppedDone = new(); // Amedo

    private void RequestParse(StreamResource r)
    {
        if (_chunkHost is not { } host || _parsing.ContainsKey(r.Name)) return;
        string name = r.Name;
        _log?.Invoke($"[stream] {name}: load started at {World.Now:0.00}s");
        if (_dropped.Remove(name, out var resumed)) // Amedo
        {
            _parsing[name] = resumed;
            return;
        }
        if (_built.Remove(name, out var cached)) // Amedo 2026-09-29
        {
            _parsing[name] = Task.FromResult<object?>(new PreparedChunk { Root = cached.Root, Uploads = cached.Uploads ?? new(), Cached = true });
            return;
        }
        lock (host.Prepared) // Amedo 2026-10-02
            if (host.Prepared.Remove(name, out var early))
            {
                _parsing[name] = early;
                _log?.Invoke($"[stream] {name}: using the copy prepared before Play ({(early.IsCompleted ? "ready" : "still preparing")})");
                return;
            }
        if (host.BuildInBackground) // Amedo 2026-09-29
        {
            var read = Task.Run(() => ReadInBackground(host, name));
            lock (LaneLock)
            {
                var job = Task.WhenAll(_lane, read).ContinueWith(_ => Prepare(host, name, read.Result), CancellationToken.None,
                                                                  TaskContinuationOptions.None, TaskScheduler.Default);
                _lane = job;
                _parsing[name] = job;
            }
            return;
        }
        _parsing[name] = Task.Run(() =>
        {
            try { return host.Parse(name); }
            catch (Exception ex) { _log?.Invoke($"[stream] could not read {name}: {ex.Message}"); return null; }
        });
    }

    private (object? Parsed, long Ms) ReadInBackground(ChunkHost host, string name) => Read(host, name, _log); // Amedo 2026-10-02

    // Amedo 2026-09-29
    private static (object? Parsed, long Ms) Read(ChunkHost host, string name, Action<string>? log)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long bytes = GC.GetAllocatedBytesForCurrentThread(); // Amedo 2026-09-29
        try
        {
            var parsed = host.Parse(name);
            log?.Invoke($"[stream] read {name}: {sw.ElapsedMilliseconds} ms, allocated {(GC.GetAllocatedBytesForCurrentThread() - bytes) / 1048576.0:0} MB");
            return (parsed, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[stream] could not read {name}: {ex.Message}");
            return (null, sw.ElapsedMilliseconds);
        }
    }

    private object? Prepare(ChunkHost host, string name, (object? Parsed, long Ms) read) => Prepare(host, name, read, _log, $"at {World.Now:0.00}s"); // Amedo 2026-10-02

    // Amedo 2026-09-29
    private static object? Prepare(ChunkHost host, string name, (object? Parsed, long Ms) read, Action<string>? log, string when)
    {
        try
        {
            if (read.Parsed is not { } parsed) return null;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long bytes = GC.GetAllocatedBytesForCurrentThread(); // Amedo 2026-09-29
            var prepared = new PreparedChunk();
            using (prepared.Uploads.Begin()) prepared.Root = host.Build(name, parsed);
            prepared.Uploads.Warm();
            log?.Invoke($"[stream] prepared {name} in the background ({when}): parse {read.Ms} ms, build {sw.ElapsedMilliseconds} ms " +
                         $"(allocated {(GC.GetAllocatedBytesForCurrentThread() - bytes) / 1048576.0:0} MB), {prepared.Uploads.Count} GPU resources");
            return prepared;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[stream] could not prepare {name}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // Amedo 2026-11-09
    public static Task<T?> OnBuildLane<T>(Func<T?> work) where T : class
    {
        lock (LaneLock)
        {
            var job = _lane.ContinueWith(_ => work(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            _lane = job;
            return job;
        }
    }

    // Amedo 2026-10-02
    public static void PrepareBeforePlay(ChunkHost host, IEnumerable<string> names, Action<string>? log)
    {
        foreach (var name in names)
        {
            lock (host.Prepared)
                if (host.Prepared.ContainsKey(name)) continue;
            var read = Task.Run(() => Read(host, name, log));
            Task<object?> job;
            lock (LaneLock)
            {
                job = Task.WhenAll(_lane, read).ContinueWith(_ => Prepare(host, name, read.Result, log, "before Play"), CancellationToken.None,
                                                             TaskContinuationOptions.None, TaskScheduler.Default);
                _lane = job;
            }
            lock (host.Prepared) host.Prepared[name] = job;
        }
    }

    // Amedo 2026-10-02
    public static void DropPrepared(ChunkHost host)
    {
        List<Task<object?>> jobs;
        lock (host.Prepared)
        {
            jobs = host.Prepared.Values.ToList();
            host.Prepared.Clear();
        }
        foreach (var job in jobs)
            job.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result is PreparedChunk p) p.Uploads.DisposeAll();
            }, TaskScheduler.Default);
    }

    private void LoadObjects(StreamResource r, StreamResource root, Entity? e, string how, Renderer.GpuUploadQueue? uploads) // Amedo 2026-09-29
    {
        if (_chunkHost is null || _chunkRoot is null) return;
        var link = root.Links?.FirstOrDefault(l => ReferenceEquals(l.Target, r));
        if (link is null) return;
        var toRoot = MeshDecoder.TwinMatToSys(link.Source.ChunkMatrix);
        LoadObjectsAt(r, e, toRoot * World.RootChunk.ToAnchor, how, uploads); // Amedo 2026-09-29
    }

    // Amedo 2026-09-29
    private WorldChunk? LoadObjectsAt(StreamResource r, Entity? e, Matrix4x4 toAnchor, string how, Renderer.GpuUploadQueue? uploads)
    {
        if (_chunkRoot is null) return null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (e?.Get<ChunkSource>() is not { Rm2: { } rm2 } src)
        {
            _log?.Invoke($"[stream] could not load {r.Name}");
            r.ObjectsLoaded = true;
            return null;
        }
        Matrix4x4.Invert(World.RootChunk.ToAnchor, out var anchorToRoot);
        var toRoot = toAnchor * anchorToRoot;
        e.Transform.Scale = Vector3.One;
        e.Transform.LocalMatrix = toAnchor;
        if (!e.Has<GameLightingEnvironment>())
            e.Add(new GameLightingEnvironment { Scenery = src.Sm2?.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM) });
        if (!e.Has<GameShadowCasters>()) e.Add(new GameShadowCasters());
        if (!ReferenceEquals(e.Parent, _chunkRoot)) _chunkRoot.AddChild(e); // Amedo 2026-10-06
        var preview = _chunkRoot.Children.FirstOrDefault(c => c.Name == "LinkedScenery")?.Children // Amedo 2026-09-29
            .FirstOrDefault(c => c.Get<LinkedSceneryLink>()?.Source is { } ls && StreamLink.Normalize(ls.Path) == r.Name);
        if (preview is { Active: true }) preview.Active = false;
        else preview = null;
        var chunk = new WorldChunk(r.Name, rm2, src.Sm2)
        {
            Tree = new SceneryTree(src.Sm2),
            Collision = Physics.CollisionWorld.FromLevel(rm2, src.GlobalRm2 ?? World.GlobalRm2),
            ToRoot = toRoot,
            ToAnchor = toAnchor,
            Host = e,
        };
        World.AddChunk(chunk);
        ReadSceneryShaders_1CCF80(src.Sm2); // Amedo
        CreateDynamicScenery_1FE5E8(e, chunk); // Amedo 2026-10-06
        World.Sounds?.AddLevel(rm2); // Amedo 2026-09-29
        int bank = World.Particles?.AddBank_1B8910(rm2, toAnchor, () => chunk.DrawFrame == World.Frame) ?? -1; // Amedo
        _streamed[r] = new StreamedChunk { Entity = e, Chunk = chunk, Preview = preview, Uploads = uploads, ParticleBank = bank }; // Amedo 2026-09-29
        r.SceneryLoaded = r.ObjectsLoaded = true;
        r.Links = LinksOf(src.Sm2);
        r.Tag = chunk;
        int before = World.Agents.Count;
        var fresh = new List<AgentLabAgent>();
        foreach (var ent in All(e))
        {
            var inst = ent.Get<InstanceData>();
            if (inst?.Source is null) continue;
            var taken = CharacterSlotTaken(inst); // Amedo 2026-11-09
            var anim = ent.Get<AnimatedObject>();
            _snapshot.Add(new Snapshot(ent, ent.Transform.LocalMatrix, ent.Transform.Position, ent.Transform.Rotation,
                                       ent.Transform.Scale, ent.Active, anim?.CurrentGroup, anim?.Frame ?? 0f,
                                       anim?.Playing ?? false, anim?.Loop ?? true) { Chunk = chunk, Skipped = taken is not null }); // Amedo 2026-09-29
            if (taken is not null) // Amedo 2026-09-29
            {
                AddStandIn_12DF70(taken, ent, chunk); // Amedo
                ent.Active = false;
                _log?.Invoke($"[stream] {r.Name}: {ent.Name} not created (character slot {CharacterSlot(inst)} already alive: {taken.Entity.Name})");
                continue;
            }
            fresh.Add(CreateAgent(ent, inst, chunk));
        }
        World.LoadChunkTriggers_26DDE8(chunk); // Amedo
        foreach (var agent in fresh) agent.Begin();
        if (World.PlayerCharacter is not null) BindCharacters(fresh); // Amedo 2026-09-29
        _log?.Invoke($"[stream] loaded {r.Name} at {World.Now:0.00}s (A{r.DepthA} B{r.DepthB}): {World.Agents.Count - before} agents, {sw.ElapsedMilliseconds} ms ({how})"); // Amedo 2026-09-29
        return chunk;
    }

    // Amedo 2026-10-06
    private void AttachScenery(StreamResource r, StreamResource root, Entity? e, Renderer.GpuUploadQueue? uploads, string how)
    {
        if (_chunkRoot is null || e?.Get<ChunkSource>() is not { } src || root.Links?.FirstOrDefault(l => ReferenceEquals(l.Target, r)) is not { } link)
        {
            if (e is not null) KeepBuilt(r.Name, (e, uploads));
            return;
        }
        var toAnchor = MeshDecoder.TwinMatToSys(link.Source.ChunkMatrix) * World.RootChunk.ToAnchor;
        e.Transform.Scale = Vector3.One;
        e.Transform.LocalMatrix = toAnchor;
        if (!e.Has<GameLightingEnvironment>())
            e.Add(new GameLightingEnvironment { Scenery = src.Sm2?.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM) });
        if (!e.Has<GameShadowCasters>()) e.Add(new GameShadowCasters());
        var held = new SceneryOnlyChunk { Entity = e, Uploads = uploads };
        foreach (var c in e.Children)
        {
            if (c.Name == "Scenery") continue;
            held.Hidden.Add((c, c.Active));
            c.Active = false;
        }
        if (!ReferenceEquals(e.Parent, _chunkRoot)) _chunkRoot.AddChild(e);
        _sceneryOnly[r] = held;
        ReadSceneryShaders_1CCF80(src.Sm2); // Amedo
        r.SceneryLoaded = true;
        r.Links = LinksOf(src.Sm2);
        _log?.Invoke($"[stream] loaded the scenery of {r.Name} at {World.Now:0.00}s (A{r.DepthA} B{r.DepthB}): scenery only, no objects ({how})");
    }

    // Amedo 2026-10-06
    private void UpgradeScenery(StreamResource r, StreamResource root, SceneryOnlyChunk held)
    {
        foreach (var (c, active) in held.Hidden) c.Active = active;
        held.Entity.Active = true;
        LoadObjects(r, root, held.Entity, "its scenery was already loaded", held.Uploads);
    }

    // Amedo 2026-10-06
    private void DetachScenery(StreamResource r)
    {
        if (!_sceneryOnly.Remove(r, out var held)) return;
        foreach (var (c, active) in held.Hidden) c.Active = active;
        held.Entity.Active = true;
        held.Entity.Parent?.RemoveChild(held.Entity);
        ReleaseSceneryShaders(held.Entity.Get<ChunkSource>()?.Sm2); // Amedo
        KeepBuilt(r.Name, (held.Entity, held.Uploads));
        if (held.Chunk is { } chunk) ReleaseChunkData(chunk);
        r.SceneryLoaded = false;
        r.Links = null;
        _log?.Invoke($"[stream] unloaded the scenery of {r.Name} at {World.Now:0.00}s (kept built for the rest of this Play)");
    }

    private void ReleaseChunkData(WorldChunk chunk)
    {
        foreach (var agent in World.Agents)
            if (ReferenceEquals(agent.Chunk, chunk)) return;
        chunk.ReleaseData();
    }

    private void UnloadObjects(StreamResource r) => UnloadObjects(r, false); // Amedo 2026-10-06

    private void UnloadObjects(StreamResource r, bool keepScenery) // Amedo 2026-10-06
    {
        if (!_streamed.Remove(r, out var s))
        {
            r.ObjectsLoaded = false;
            return;
        }
        long unloadStart = System.Diagnostics.Stopwatch.GetTimestamp(); // Amedo 2026-11-14
        foreach (var agent in World.Agents.Where(a => ReferenceEquals(a.Chunk, s.Chunk)).ToList()) // Amedo 2026-09-29
            if (!ReleaseOnUnload_197570(agent, s.Chunk)) ReleaseAgent(agent); // Amedo
        World.ReleaseSpawnedUnder(s.Entity); // Amedo 2026-09-29
        World.UndoForeignGroupsUnder(s.Entity);
        foreach (var agent in World.Agents) agent.ForgetReleased();
        foreach (var snap in _snapshot.Where(x => ReferenceEquals(x.Chunk, s.Chunk)))
            if (snap.Entity.Get<AgentLabAgent>() is not { } alive || ReferenceEquals(alive.Chunk, s.Chunk)) Restore(snap); // Amedo 2026-09-29
        KeepSlotCharacters(s.Chunk);
        _snapshot.RemoveAll(x => ReferenceEquals(x.Chunk, s.Chunk));
        World.RemoveChunk(s.Chunk);
        World.Sounds?.RemoveLevel(s.Chunk.Rm2); // Amedo 2026-09-29
        World.Particles?.RemoveBank_1B7AE0(s.ParticleBank);
        if (s.Preview is not null) s.Preview.Active = true;
        r.ObjectsLoaded = false;
        r.Tag = null;
        if (keepScenery) // Amedo 2026-10-06
        {
            var held = new SceneryOnlyChunk { Entity = s.Entity, Chunk = s.Chunk, Uploads = s.Uploads };
            foreach (var c in s.Entity.Children)
            {
                if (c.Name == "Scenery") continue;
                held.Hidden.Add((c, c.Active));
                c.Active = false;
            }
            _sceneryOnly[r] = held;
            _log?.Invoke($"[stream] unloaded the objects of {r.Name} at {World.Now:0.00}s (A{r.DepthA} B{r.DepthB}): its scenery stays");
            return;
        }
        s.Entity.Parent?.RemoveChild(s.Entity);
        ReleaseSceneryShaders(s.Chunk.Sm2); // Amedo
        KeepBuilt(r.Name, (s.Entity, s.Uploads)); // Amedo 2026-09-29
        ReleaseChunkData(s.Chunk);
        r.SceneryLoaded = false;
        r.Links = null;
        _log?.Invoke($"[stream] unloaded {r.Name} (kept built for the rest of this Play) in {System.Diagnostics.Stopwatch.GetElapsedTime(unloadStart).TotalMilliseconds:0.0} ms"); // Amedo 2026-11-14
    }

    // Amedo 2026-09-29
    private WorldChunk? RespawnChunk_175618()
    {
        if (World.RespawnChunkName is not { } name || World.RespawnMatrix is null || _anchorChunk is null) return null;
        if (name == _anchorChunk.Name) return _anchorChunk;
        if (_streamed.Values.FirstOrDefault(s => s.Chunk.Name == name) is { } loaded) return loaded.Chunk;
        return LoadForRespawn(name, World.RespawnToAnchor);
    }

    // Amedo 2026-09-29
    private WorldChunk? LoadForRespawn(string name, Matrix4x4 toAnchor)
    {
        if (Streamer is null || _chunkHost is null) return null;
        var r = Streamer.Find_2AA7F8(name) ?? Streamer.Create_2AA590(name, 0xF);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RequestParse(r);
        if (!_parsing.Remove(name, out var task)) return null;
        task.Wait();
        var result = task.IsCompletedSuccessfully ? task.Result : null;
        Entity? e;
        Renderer.GpuUploadQueue? uploads = null;
        string how;
        if (result is PreparedChunk prepared)
        {
            prepared.Uploads.Upload(double.PositiveInfinity);
            e = prepared.Root;
            uploads = prepared.Uploads;
            how = prepared.Cached ? "from the cache" : "background build";
        }
        else
        {
            e = result is null ? null : _chunkHost.Build(name, result);
            how = "built";
        }
        _log?.Invoke($"[stream] the respawn point is in {name}, which is not loaded: loading it first (0x175618 mode 9), waited {sw.ElapsedMilliseconds} ms");
        return LoadObjectsAt(r, e, toAnchor, $"{how}, for the respawn", uploads);
    }

    // Amedo 2026-09-29
    private void MovePlayer_1954A8(WorldChunk target)
    {
        if (World.Player is not { } player || InstancesOf(target) is not { } home) return;
        if (!ReferenceEquals(player.Chunk, target))
        {
            var e = player.Entity;
            RememberHome(e); // Amedo
            e.Parent?.RemoveChild(e);
            home.AddChild(e);
            player.CanChangeChunk_23D060(target); // Amedo
            player.Chunk = target;
        }
        if (!ReferenceEquals(World.RootChunk, target))
        {
            World.SetRoot(target);
            if (Streamer is not null && ResourceOf(target) is { } r) Streamer.Root = r;
            PlaceLinked_1E9C18(); // Amedo
        }
        if (target.Collision is { } collision && World.PlayerCharacter is { } character) character.CollisionWorld = collision;
        _log?.Invoke($"[stream] respawn: {player.Entity.Name} goes to {target.Name} (0x1954A8 -> 0x1ED188), root {World.RootChunk.Name}");
    }

    private void KeepSlotCharacters(WorldChunk unloading, bool anchor = false)
    {
        for (int i = 0; i < _snapshot.Count; i++)
        {
            var snap = _snapshot[i];
            if (!(anchor ? snap.Chunk is null : ReferenceEquals(snap.Chunk, unloading)) || snap.Skipped || snap.Entity.Get<AgentLabAgent>() is not { Destroyed: false } kept) continue; // Amedo 2026-11-10
            int slot = CharacterSlot(kept.Instance);
            if (slot == 6 || ReferenceEquals(kept.Chunk, unloading) || !ReferenceEquals(World.PlayerSlot_570(slot), kept)) continue;
            _snapshot[i] = snap with { Chunk = kept.Chunk };
            _log?.Invoke($"[stream] {unloading.Name} unloads: {kept.Entity.Name} (slot {slot}) lives on in {kept.Chunk.Name} and is kept for a reset there");
        }
    }

    // Amedo 2026-09-29
    private void PlaceSecond_171E90(WorldChunk target)
    {
        if (World.ResetKind != AgentLabWorld.ResetKindDeath || World.RespawnPlayerMode is not { } saved || World.RespawnMatrix is not { } m) return;
        int slot = (int)(saved >> 8) & 0xF;
        if (slot == 6) return;
        var agent = World.PlayerSlot_570(slot) ?? CreateSkippedCharacter(slot, target);
        if (agent is null)
        {
            _log?.Invoke($"[stream] respawn: no character for slot {slot} (0x171E90)");
            return;
        }
        if (InstancesOf(target) is { } home && (!ReferenceEquals(agent.Chunk, target) || !Under(agent.Entity, home)))
        {
            var e = agent.Entity;
            RememberHome(e); // Amedo
            e.Parent?.RemoveChild(e);
            home.AddChild(e);
            agent.CanChangeChunk_23D060(target); // Amedo
            agent.Chunk = target;
        }
        if (agent.Global) agent.LeaveGlobal(); // Amedo 2026-11-10
        agent.GameMatrix = m;
        agent.SetComebackPlacement_23CD80(m); // Amedo
        if (agent.OwnCharacter is { } c && target.Collision is { } collision) c.CollisionWorld = collision; // Amedo
        _log?.Invoke($"[stream] respawn: second character {agent.Entity.Name} (slot {slot}) goes with the player to {target.Name} at {m.Translation} (0x171E90 -> 0x195218 + 0x1954A8)");
    }

    // Amedo 2026-09-29
    private AgentLabAgent? CreateSkippedCharacter(int slot, WorldChunk prefer)
    {
        Snapshot? pick = null;
        foreach (var s in _snapshot)
        {
            if (!s.Skipped || s.Entity.Get<InstanceData>() is not { } inst || CharacterSlot(inst) != slot) continue;
            if (World.GameObject(inst.ObjectId) is not { } go || (int)go.Type != 0) continue;
            pick ??= s;
            if (ReferenceEquals(s.Chunk, prefer))
            {
                pick = s;
                break;
            }
        }
        if (pick is null || pick.Chunk is null && _anchorChunk is null) return null;
        pick.Skipped = false;
        pick.Entity.Active = pick.Active;
        var agent = CreateAgent(pick.Entity, pick.Entity.Get<InstanceData>()!, pick.Chunk ?? _anchorChunk!);
        agent.Begin();
        if (World.PlayerCharacter is not null) BindCharacters(new[] { agent });
        _log?.Invoke($"[stream] respawn: slot {slot} had no character left (its chunk was unloaded) -> {agent.Entity.Name} built instead");
        return agent;
    }

    // Amedo 2026-09-29
    private void ReleaseAgent(AgentLabAgent agent)
    {
        agent.ReleaseParts_23CF38();
        if (!agent.Rendered) agent.SetRendered(true);
        agent.Instance.ShadowOverride = null;
        agent.Instance.RuntimeShadowSlots = null; // Amedo
        agent.Instance.RuntimeFlags = null; // Amedo 2026-10-01
        if (agent.Anim is { } anim) { anim.Controllers = null; anim.LogicDriven = false; } // Amedo 2026-10-01
        agent.Entity.RemoveComponent<AgentLabAgent>();
        agent.Released = true; // Amedo 2026-09-30
        agent.LeaveTree(); // Amedo
        agent.InChunk_A0 = false;
        World.Agents.Remove(agent);
        World.Unregister(agent);
    }

    // Amedo 2026-09-29
    private static bool Under(Entity? e, Entity? root)
    {
        if (root is null) return false;
        for (; e is not null; e = e.Parent)
            if (ReferenceEquals(e, root)) return true;
        return false;
    }

    // Amedo 2026-09-29
    private void UnloadAnchor(StreamResource r)
    {
        if (_anchorChunk is not { } chunk || _chunkRoot is null || ReferenceEquals(chunk, World.RootChunk)) return;
        var agents = World.Agents.Where(a => ReferenceEquals(a.Chunk, chunk)).ToList();
        foreach (var agent in agents)
            if (!ReleaseOnUnload_197570(agent, chunk)) ReleaseAgent(agent); // Amedo
        KeepSlotCharacters(chunk, anchor: true); // Amedo 2026-11-10
        if (_gameSpace is not null)
        {
            World.ReleaseSpawnedUnder(_gameSpace);
            World.UndoForeignGroupsUnder(_gameSpace);
        }
        foreach (var agent in World.Agents) agent.ForgetReleased();
        foreach (var snap in _snapshot.Where(x => x.Chunk is null && Under(x.Entity, _gameSpace))) Restore(snap);
        World.RemoveChunk(chunk);
        World.Sounds?.RemoveLevel(chunk.Rm2);
        World.Particles?.RemoveBank_1B7AE0(_anchorBank);
        _anchorBank = -1;
        if (EmittersOf(_chunkRoot) is { } emitters) World.Particles?.PauseMarkers(emitters, true);
        foreach (var c in _chunkRoot.Children)
        {
            if (Array.IndexOf(AnchorContent, c.Name) < 0 || c.Name == "Scenery") continue; // Amedo 2026-10-06
            _anchorHidden.Add((c, c.Active));
            c.Active = false;
        }
        _anchorUnloaded = true;
        r.ObjectsLoaded = r.SceneryLoaded = false;
        r.Links = null;
        r.Tag = null;
        _log?.Invoke($"[stream] unloaded {r.Name} at {World.Now:0.00}s (the editor's scene: {agents.Count} agents removed, instances hidden)");
    }

    // Amedo 2026-09-29
    private void ShowAnchor()
    {
        if (!_anchorUnloaded || _anchorChunk is not { } chunk) return;
        foreach (var (e, active) in _anchorHidden) e.Active = active;
        _anchorHidden.Clear();
        if (!World.Chunks.Contains(chunk)) World.Chunks.Insert(0, chunk);
        World.AddChunk(chunk);
        World.Sounds?.AddLevel(chunk.Rm2);
        if (World.Particles is { } fx)
        {
            _anchorBank = fx.AddBank(chunk.Rm2);
            if (EmittersOf(_chunkRoot) is { } emitters) fx.PauseMarkers(emitters, false);
        }
        Matrix4x4.Invert(World.RootChunk.ToAnchor, out var anchorToRoot);
        chunk.ToRoot = chunk.ToAnchor * anchorToRoot;
        _anchorUnloaded = false;
        if (Streamer is not null && _anchorResource is { } r)
        {
            if (Streamer.Find_2AA7F8(r.Name) is { } listed) _anchorResource = r = listed;
            else Streamer.Resources.Add(r);
            r.SceneryLoaded = r.ObjectsLoaded = true;
            r.Links = LinksOf(_anchorSm2);
            r.Tag = chunk;
        }
    }

    // Amedo 2026-09-29
    private void RestoreAnchor(StreamResource r)
    {
        if (_anchorChunk is not { } chunk) return;
        _anchorResource = r;
        ShowAnchor();
        var fresh = new List<AgentLabAgent>();
        foreach (var s in _snapshot.Where(x => x.Chunk is null && Under(x.Entity, _gameSpace) && !x.Entity.Has<AgentLabAgent>()))
            fresh.Add(CreateAgent(s.Entity, s.Entity.Get<InstanceData>()!, chunk));
        World.LoadChunkTriggers_26DDE8(chunk); // Amedo
        foreach (var agent in fresh) agent.Begin();
        if (World.PlayerCharacter is not null) BindCharacters(fresh); // Amedo 2026-09-29
        _log?.Invoke($"[stream] loaded {r.Name} again at {World.Now:0.00}s (A{r.DepthA} B{r.DepthB}; the editor's scene: {fresh.Count} agents)");
    }

    // Amedo 2026-11-09
    public void UnloadForLevelSwitch()
    {
        StopStreaming();
        ResetLook();
        Renderer.RenderPipeline.SkydomeDraws.Clear();
        _log?.Invoke("[stream] level switch: every streamed scene unloaded before the next level loads (0x2AA3E8 UnloadEverything)");
    }

    private void StopStreaming()
    {
        ReturnToAnchor();
        foreach (var (name, task) in _parsing.Concat(_dropped)) // Amedo 2026-09-29
            if (task.IsCompletedSuccessfully && task.Result is PreparedChunk { Root: { } pending } p && !_built.ContainsKey(name))
                KeepBuilt(name, (pending, p.Uploads));
        _parsing.Clear();
        _dropped.Clear(); // Amedo
        foreach (var r in _streamed.Keys.ToList()) UnloadObjects(r);
        foreach (var r in _sceneryOnly.Keys.ToList()) DetachScenery(r); // Amedo 2026-10-06
        StopChunkDraw(); // Amedo 2026-10-06
        int freed = 0; // Amedo 2026-09-29
        foreach (var (_, c) in _built)
        {
            freed += c.Uploads?.Count ?? 0;
            c.Uploads?.DisposeAll();
        }
        if (_built.Count > 0) _log?.Invoke($"[stream] freed {freed} GPU resources of {_built.Count} streamed scene(s)");
        _built.Clear();
        _builtOrder.Clear();
        while (_freeLater.TryDequeue(out var pending)) // Amedo 2026-11-14
        {
            pending.Built.Uploads?.DisposeAll();
            CutTree(pending.Built.Root);
        }
        _disposeLater.DisposeAll();
        _stillUsed.DisposeAll();
        Streamer = null;
    }
}
