using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Runtime.AgentLab;
using CrashEngine.Runtime.Streaming; // Amedo 2026-09-28
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace CrashEngine.Runtime;

// Amedo 2026-09-26
public sealed partial class SceneSimulation
{
    private sealed record Snapshot(Entity Entity, Matrix4x4? LocalMatrix, in Vector3 Position, in Quaternion Rotation,
                                   in Vector3 Scale, bool Active, AnimatedObject.SlotGroup? Group, float Frame,
                                   bool Playing, bool Loop)
    {
        public WorldChunk? Chunk { get; init; } // Amedo 2026-09-28
        public bool Skipped { get; set; } // Amedo 2026-09-29
    }

    private readonly List<Snapshot> _snapshot = new();
    private Entity? _gameSpace;
    private Audio.IAudioOutput? _audio; // Amedo 2026-10-26

    public AgentLabWorld World { get; }
    public Character.CharacterPad Pad { get; } = new(); // Amedo 2026-09-27
    public Camera.RuntimeGameCamera? GameCamera { get; private set; } // Amedo 2026-09-28

    private readonly bool _playerPhysics; // Amedo 2026-09-28
    private readonly uint _jumpRules;
    private readonly Action<string>? _log;

    private SceneSimulation(AgentLabWorld world, bool playerPhysics, uint jumpRules, Action<string>? log)
    {
        World          = world;
        GraphicsSettings.GameAspect = () => Screen.ScreenFit.VirtualAspect; // Amedo
        world.Pad1     = Pad; // Amedo
        _playerPhysics = playerPhysics;
        _jumpRules     = jumpRules;
        _log           = log;
        Latest         = this;
    }

    public static SceneSimulation? Latest { get; private set; }

    // Amedo 2026-10-04
    public static SceneSimulation? Start(Entity chunkRoot, Action<string>? log, PlaySettings play,
                                         Func<Entity, PS2AnyInstance, BaseTwinSection, Entity?>? spawnHost = null,
                                         Func<Entity, ushort, ushort, AnimatedObject.SlotGroup?, MeshDecoder.ForeignGroupBuild?>? groupHost = null,
                                         ChunkHost? chunkHost = null, string? traceFilter = null, bool continuesLoading = false) // Amedo 2026-11-16
    {
        log?.Invoke($"[play] settings: jump rules 0x{play.JumpRules:X}, classic Aku Aku {play.ClassicAku}, golden {play.GoldenSeconds:0.##} s, " +
                    $"touch breaks {play.AkuTouchBreak}, golden music {play.GoldenMusic} at {play.GoldenMusicVolume:0.##}, language {play.LanguageDirectory ?? "disc"}, disc language {play.DiscLanguage}"); // Amedo
        return Start(chunkRoot, log, traceFilter, spawnHost, jumpRules: play.JumpRules, musicDirectory: play.MusicDirectory, groupHost: groupHost,
                     chunkHost: chunkHost, languageDirectory: play.LanguageDirectory, classicAku: play.ClassicAku, goldenSeconds: play.GoldenSeconds,
                     akuTouchBreak: play.AkuTouchBreak, goldenMusic: play.GoldenMusic, goldenMusicVolume: play.GoldenMusicVolume, discLanguage: play.DiscLanguage,
                     savesDirectory: play.SavesDirectory, continuesLoading: continuesLoading); // Amedo
    }

    public static SceneSimulation? Start(Entity chunkRoot, Action<string>? log, string? traceFilter = null,
                                         Func<Entity, PS2AnyInstance, BaseTwinSection, Entity?>? spawnHost = null,
                                         bool playerPhysics = true, uint jumpRules = 0x44, string? musicDirectory = null,
                                         Func<Entity, ushort, ushort, AnimatedObject.SlotGroup?, MeshDecoder.ForeignGroupBuild?>? groupHost = null, // Amedo 2026-09-28
                                         ChunkHost? chunkHost = null, string? languageDirectory = null, // Amedo 2026-10-01
                                         bool classicAku = false, float goldenSeconds = 8f, // Amedo 2026-10-04
                                         bool akuTouchBreak = false, int goldenMusic = -1, float goldenMusicVolume = 0.75f, // Amedo 2026-10-04
                                         string? discLanguage = null, string? savesDirectory = null, bool continuesLoading = false) // Amedo
    {
        discLanguage ??= PlaySettings.DetectDiscLanguage(musicDirectory); // Amedo
        Region.RegionProfile.Current = Region.RegionProfile.For(discLanguage); // Amedo 2026-11-11
        LogicTicks = Region.RegionProfile.Current.LogicTicks; // Amedo 2026-11-11
        ParticleWorld.Dt = LogicTicks / 9000f;
        var src = chunkRoot.Get<ChunkSource>();
        if (src?.Rm2 is null) return null;

        var timer = System.Diagnostics.Stopwatch.StartNew(); // Amedo 2026-10-02
        AnimatedObject.LogicNowTicks = 0;
        AnimatedObject.LogicStepTicks = 0;
        var times = new List<string>();
        void Mark(string step)
        {
            times.Add($"{step} {timer.Elapsed.TotalMilliseconds:0}");
            timer.Restart();
        }
        var tree = new SceneryTree(src.Sm2);
        Mark("tree");
        var collision = Physics.CollisionWorld.FromLevel(src.Rm2, src.GlobalRm2);
        Mark("collision");
        var world = new AgentLabWorld(src.Rm2, src.GlobalRm2, StreamLink.Normalize(Path.ChangeExtension(src.Rm2Path, null) ?? src.Rm2Path)) // Amedo 2026-09-28
        {
            Log = log, TraceFilter = traceFilter, SpawnHost = spawnHost, Tree = tree, // Amedo 2026-10-02
            Collision = collision,
            ForeignGroupHost = groupHost, // Amedo 2026-09-28
            ClassicAku = classicAku, GoldenSeconds = goldenSeconds, AkuTouchBreak = akuTouchBreak, // Amedo 2026-10-04
            SavesDirectory = savesDirectory, // Amedo
        };
        Mark("world");
        var sim   = new SceneSimulation(world, playerPhysics, jumpRules, log); // Amedo 2026-09-28

        foreach (var e in All(chunkRoot))
        {
            var inst = e.Get<InstanceData>();
            if (inst?.Source is null) continue;
            var anim = e.Get<AnimatedObject>();
            sim._snapshot.Add(new Snapshot(e, e.Transform.LocalMatrix, e.Transform.Position, e.Transform.Rotation,
                                           e.Transform.Scale, e.Active, anim?.CurrentGroup, anim?.Frame ?? 0f,
                                           anim?.Playing ?? false, anim?.Loop ?? true));
            sim._gameSpace ??= e.Parent; // Amedo 2026-09-27
        }
        Mark("snapshot"); // Amedo 2026-10-02
        sim.CreateDynamicScenery_1FE5E8(chunkRoot, world.RootChunk); // Amedo 2026-10-06
        sim.CreateAgents(); // Amedo 2026-09-28
        world.LoadChunkTriggers_26DDE8(); // Amedo
        Mark("agents"); // Amedo 2026-10-02
        sim.InitStreaming(chunkRoot, src, chunkHost);
        Mark("streaming"); // Amedo 2026-10-02

        if (All(chunkRoot).Select(e => e.Get<ParticleEffectsRenderer>()).FirstOrDefault(r => r is not null) is { } fx) // Amedo 2026-09-27
        {
            world.Particles = new RuntimeParticles(fx, src.GlobalRm2, src.Rm2) { Unimplemented = world.Unimplemented, RootToAnchor = () => world.RootChunk.ToAnchor }; // Amedo 2026-09-29
            sim._anchorBank = world.Particles.FirstBank;
            var anchorChunk = world.RootChunk; // Amedo
            world.Particles.AnchorDrawn = () => anchorChunk.DrawFrame == world.Frame;
            world.Particles.ClockStopped = () => !world.ChunkClocksRunning; // Amedo
            world.Particles.LogicDriven = FixedLogicStep; // Amedo
            world.Particles.BoxInView = sim.ParticleBoxInView; // Amedo
        }
        CrashEngine.Renderer.RenderPipeline.ShaderClockStopped = () => !world.ChunkClocksRunning; // Amedo
        Engine.SceneHeld = () => world.RunState_309718 == 3;
        Mark("particles"); // Amedo 2026-10-02
        world.Sounds = new RuntimeSounds(src.GlobalRm2, src.Rm2, new Audio.SoundMixer()) { Clock = () => world.Now, Language = () => world.AudioTrack_309754, Held = world.HeldSound }; // Amedo 2026-09-28
        world.Sounds.ReverbKey_22EA78 = sim.ReverbKey_22EA78; // Amedo 2026-11-11
        Mark("sounds"); // Amedo 2026-10-02
        world.Music = new RuntimeMusic(world.Sounds.Mixer, musicDirectory, discLanguage) { Clock = () => world.Now, Unimplemented = world.Unimplemented, Listener = world.Sounds, // Amedo
            GoldenTrack = goldenMusic, GoldenVolume = goldenMusicVolume, GoldenInvincible = () => world.PlayerCharacter?.Invincible == true }; // Amedo 2026-10-04
        Mark("music"); // Amedo 2026-10-02
        world.MoviePlayer_309720 = new Video.MoviePlayer { Mixer = world.Sounds.Mixer, Log = log, Resolve = name => ResolveMovie(musicDirectory, name) }; // Amedo
        world.Screen = Screen.GameScreen.Load(musicDirectory, log, languageDirectory, discLanguage, src.GlobalRm2); // Amedo
        Mark("screen"); // Amedo 2026-10-02
        foreach (var agent in world.Agents) agent.Begin();
        Mark("begin"); // Amedo 2026-10-02
        if (sim.BindPlayer() is { } character) // Amedo 2026-09-28
        {
            Mark("characters"); // Amedo 2026-10-02
            if (continuesLoading) sim._streamFocus = false; // Amedo 2026-11-16
            sim.StepStreaming(block: true, takeFocus: !continuesLoading); // Amedo 2026-09-28
            Mark("linked chunks"); // Amedo 2026-10-02
            if (chunkHost is not null && sim.Streamer is { Root: { } streamRoot } streamer)
            {
                chunkHost.StartupChunks.Clear();
                chunkHost.StartupChunks.AddRange(streamer.Resources.Where(r => (r.ObjectsLoaded || r.SceneryLoaded) && !ReferenceEquals(r, streamRoot)).Select(r => r.Name)); // Amedo 2026-10-06
            }
            sim.GameCamera = Camera.RuntimeGameCamera.Create(world);
            Mark("camera"); // Amedo 2026-10-02
            sim.UpdateChunkDraw_1ECB58(); // Amedo 2026-10-06
        }
        log?.Invoke($"[play] startup ms: {string.Join(", ", times)}; linked: {string.Join(",", chunkHost?.StartupChunks ?? [])}"); // Amedo 2026-10-02
        return sim;
    }

    // Amedo 2026-10-06
    public static bool DynamicSceneryCollision { get; set; } = true;

    // Amedo 2026-10-06
    private void CreateDynamicScenery_1FE5E8(Entity? host, WorldChunk chunk)
    {
        if (!DynamicSceneryCollision) return;
        var root = host?.Children.FirstOrDefault(c => c.Name == "Scenery")?.Children.FirstOrDefault(c => c.Name == "DynamicScenery");
        if (root is null) return;
        foreach (var e in root.Children)
            if (e.Get<DynamicSceneryAnimator>() is { Model: { } model } animator)
            {
                var d = new DynamicSceneryInstance(e, animator, model, chunk);
                World.DynamicScenery.Add(d);
                d.ChunkAddInstance_1F1940(); // Amedo
            }
    }

    // Amedo 2026-09-28
    private void CreateAgents()
    {
        foreach (var s in _snapshot)
        {
            if (s.Skipped || s.Chunk is null && _anchorUnloaded) continue; // Amedo 2026-11-10
            if (s.Chunk is not null && InstancesOf(s.Chunk) is { } home && !Under(s.Entity, home))
            {
                RememberHome(s.Entity);
                s.Entity.Parent?.RemoveChild(s.Entity);
                home.AddChild(s.Entity);
            }
            CreateAgent(s.Entity, s.Entity.Get<InstanceData>()!, s.Chunk ?? World.RootChunk); // Amedo 2026-09-29
        }
    }

    // Amedo 2026-09-29
    private static int CharacterSlot(InstanceData inst) => inst.Source.ParamList3 is { Count: > 0 } p3 ? (int)p3[0] : 6;

    private AgentLabAgent? CharacterSlotTaken(InstanceData inst)
    {
        if (World.GameObject(inst.ObjectId) is not { } go || (int)go.Type != 0) return null;
        int slot = CharacterSlot(inst);
        return slot == 6 ? null : World.PlayerSlot_570(slot) is { Destroyed: false } alive ? alive : null;
    }

    // Amedo 2026-09-28
    private AgentLabAgent CreateAgent(Entity entity, InstanceData inst, WorldChunk chunk)
    {
        var agent = new AgentLabAgent(World, inst, World.GameObject(inst.ObjectId), entity.Get<AnimatedObject>()) { Chunk = chunk };
        entity.Add(agent);
        World.Register(agent);
        agent.ChunkAddInstance_1F1940(); // Amedo
        MakePlaces_12DF70(agent, chunk); // Amedo
        return agent;
    }

    // Amedo 2026-09-28
    private Character.CharacterController? BindPlayer()
    {
        var world = World;
        if (!_playerPhysics || world.Collision is not { } collision) return null; // Amedo 2026-09-27
        if ((EnableCharacters_17C0C8(false) ?? world.Player) is not { } player) return null; // Amedo
        var character = BindCharacter(player, player.Chunk.Collision ?? collision); // Amedo
        character.Pad = Pad;
        world.SetPlayer_30A5A8(player, character); // Amedo
        BindCharacters(world.Agents);
        return character;
    }

    // Amedo
    private AgentLabAgent? EnableCharacters_17C0C8(bool switching)
    {
        int primary = (int)(World.Progression >> 8) & 0xF, second = (int)(World.Progression >> 12) & 0xF;
        var who = primary != 6 && World.PlayerSlot_570(primary) is { Destroyed: false } kept ? kept : SwitchToCharacterInFocus_1770A0();
        if (!switching) return who;
        SwitchCharacter_177A90((int)(World.Progression >> 8) & 0xF, true);
        SwitchCharacter_177A90(second, false);
        return who;
    }

    // Amedo
    private AgentLabAgent? SwitchToCharacterInFocus_1770A0()
    {
        int slot = (int)(World.Progression >> 8) & 0xF;
        for (int i = 0; i < 6; i++)
        {
            if (i != 0 && slot != 6 && World.PlayerSlot_570(slot) is { Destroyed: false } who && ReferenceEquals(who.Chunk, World.RootChunk))
            {
                World.Progression = (World.Progression & ~0xF00u) | ((uint)slot << 8);
                _log?.Invoke($"[AgentLab] player = slot {slot} ({who.Entity.Name}), first character in the focus chunk (0x1770A0)");
                return who;
            }
            slot++;
            if (6 <= slot) slot = 0;
        }
        return null;
    }

    // Amedo 2026-09-29
    private void BindCharacters(IEnumerable<AgentLabAgent> agents)
    {
        if (!_playerPhysics) return;
        foreach (var agent in agents)
        {
            if (agent.OwnCharacter is not null || ReferenceEquals(agent, World.Player)) continue; // Amedo 2026-11-09
            if (!agent.IsCharacterObject || agent.Chunk.Collision is not { } collision) continue;
            var npc = BindCharacter(agent, collision);
            npc.ActivePlayer = false;
            _log?.Invoke($"[character] {agent.Entity.Name}: controller (kind {npc.Bones.Kind}, run {npc.RunSpeed}, walk {npc.WalkSpeed}, hp {npc.HitPoints})");
        }
    }

    // Amedo 2026-09-29
    private Character.CharacterController BindCharacter(AgentLabAgent agent, Physics.CollisionWorld collision)
    {
        var world = World;
        var p = agent.Instance.Source;
        var floats = p.ParamList2?.ToArray() ?? [];
        agent.FloatParams = floats;
        var c = new Character.CharacterController(collision,
            () => agent.GameMatrix, m => agent.GameMatrix = m, slot => agent.TriggerEventSlot(slot),
            floats, p.ParamList1 ?? [], _jumpRules);
        c.Limbs.Anim = agent.Anim;
        c.Limbs.ExitPoint_293B70 = agent.ExitPoint_293B70; // Amedo
        c.SetVideoModel_29E988 = () => world.SetVideoModelInstance_29E988((ushort)((agent.GameObject is { OGISlots.Count: > 0 } go ? go.OGISlots[0] : 0xFFFF) & 0x7FFF), agent); // Amedo
        c.Bodies = (min, max, mask) => world.QueryBodies(min, max, mask, 1, 0x18, agent);
        c.SolveSkip_139964 = () => c.InstanceInt_2619D8?.Invoke(0) == 1 ? world.PlayerSlot_570(0) : null;
        c.LinkedTo = (owner, to) => to is AgentLabAgent t && owner is AgentLabAgent o && t.Links.Contains(o); // Amedo 2026-10-05
        c.Touch = (owner, type) => { if (owner is AgentLabAgent touched) world.PostTouch_13E0A0(touched, type, agent); }; // Amedo 2026-10-06
        c.CheckTriggers_131320 = () => world.CheckTriggers_131320(c); // Amedo
        c.StateFlagsOf = owner => owner is AgentLabAgent a ? a.Instance.Source.StateFlags : uint.MaxValue; // Amedo 2026-10-06
        c.MotionStateOf = owner => owner is AgentLabAgent { OwnCharacter: { } oc } ? oc.MotionState : -1; // Amedo
        c.InstanceFlagsOf = owner => owner switch { AgentLabAgent a => a.AgentFlags, DynamicSceneryInstance d => d.AgentFlags, _ => 0u }; // Amedo 2026-10-06
        c.ModelOf = owner => owner switch { AgentLabAgent a => a.CollisionModel, DynamicSceneryInstance d => d.Model, _ => null }; // Amedo 2026-10-06
        c.HullSurfaceOf = (owner, part) => owner is AgentLabAgent a ? a.HullSurfaceIndex_1F07D8(part) : 0; // Amedo
        c.PartMatrix = (owner, part) => owner is DynamicSceneryInstance d ? d.GameMatrix : ((AgentLabAgent)owner).PartMatrix_1F0538(part); // Amedo 2026-10-06
        c.LimbBodies = (min, max) => world.QueryBodies(min, max, 0x5A010, 1, 0x10, agent, requireAll: true);
        c.StrafeTarget_124 = () => agent.Keys.Count > 0 ? agent.Keys[0] : null; // Amedo
        c.GroundObjects = (a, b) => Physics.HullQueries.SegmentBodies_1F1740(
            world.QueryBodies(Vector3.Min(a, b), Vector3.Max(a, b), 0x15B010, 1, 0x10, agent, requireAll: true), a, b);
        c.OverlapQuery = (hull, m, mask) => world.QueryOverlap(hull, m, mask, 1, agent); // Amedo 2026-10-06
        c.SphereQuery = (at, r, mask) => world.QuerySphere(at, r, mask, 1, agent);
        c.InstanceBox = () => agent.TreeBox;
        c.Collect = owner => { if (owner is AgentLabAgent picked) picked.CollectBy(agent); }; // Amedo 2026-10-06
        c.TriggerFrom = (slot, o) => agent.TriggerEventSlot(slot, o is Character.CharacterController ? agent : o as AgentLabAgent);
        c.SetVisible = on => agent.SetRendered(on);
        c.SpawnParticle = (pid, at) => world.Particles?.Spawn(pid, 1, true, at);
        c.AddDecal = (at, n, d, type, group, flags, key) => world.Particles?.AddDecal_1ADC18(at, n, d, type, group, flags, key); // Amedo 2026-09-30
        c.SkidMarks = (ring, on) => world.Particles?.SkidMarks_13FEB8(ring, on); // Amedo
        c.PlaySound = (id, at, volume) => world.Sounds?.PlayAt(id, at, volume, -1f, $"{agent.Entity.Name} surface (0x2248C8)");
        c.Log = _log;
        c.AutoDisplacement = () => agent.CharacterDisplacement;
        c.MotionPaused = () => agent.CharacterPaused;
        c.SetCollidable = agent.SetCollidable; // Amedo 2026-09-29
        c.SetInstanceBox = agent.SetBoxOverride_1F0168;
        c.TriggerForced = slot => agent.TriggerEventSlot(slot, null, true);
        c.TriggerOnChannel = (slot, channel) => agent.TriggerEventSlot(slot, null, false, channel);
        c.CameraImpulse = (strength, falloff, at) => world.CameraImpulse?.Invoke(strength, falloff, at);
        c.SetBump154 = agent.SetBump_154; // Amedo
        c.SnowCloud = agent.SnowCloud_150AD0;
        c.Self = agent;
        c.Alive = () => !agent.Destroyed; // Amedo 2026-10-05
        c.ClearInstanceFlags = (owner, bits) => { if (owner is AgentLabAgent a) a.ClearAgentFlags(bits); else if (owner is DynamicSceneryInstance d) d.AgentFlags &= ~bits; }; // Amedo 2026-10-06
        c.SetInstanceFlags = (owner, bits) => { if (owner is AgentLabAgent a) a.SetAgentFlags(bits); else if (owner is DynamicSceneryInstance d) d.AgentFlags |= bits; }; // Amedo 2026-10-06
        c.HeldMatrix_11C = () => agent.Held11C is { } held ? held.GameMatrix : null;
        c.SetHeldMatrix_11C = m => { if (agent.Held11C is { } held) held.GameMatrix = m; };
        c.LookTarget = agent.CharacterLookTarget_130958; // Amedo 2026-10-02
        c.BodyOf = owner => (owner as AgentLabAgent)?.Component5; // Amedo 2026-10-06
        c.WakeBody = owner => (owner as AgentLabAgent)?.Wake_291308(); // Amedo 2026-10-06
        c.TouchHook = owner => (owner as AgentLabAgent)?.Touched_22FC98(0f, agent); // Amedo 2026-10-06
        c.TouchBack = owner => { if (owner is AgentLabAgent back) world.PostTouch_13E0A0(agent, 3, back); }; // Amedo 2026-10-06
        c.ComponentMaskOf = owner => owner switch { AgentLabAgent a => a.ComponentMask, DynamicSceneryInstance d => d.ComponentMask, _ => 0u }; // Amedo 2026-10-06
        c.MotionVelocityOf = owner => owner switch { AgentLabAgent a => a.MotionVelocity_192438(), DynamicSceneryInstance d => d.MotionVelocity_192438(), _ => null }; // Amedo 2026-10-06
        var ballBodies = new List<Character.CharacterBody>();
        c.BallBodies_1ECFC0 = (min, max, partner) =>
        {
            ballBodies.Clear();
            var cortex = partner as AgentLabAgent;
            foreach (var b in world.QueryBodies(min, max, 0x5B010, 1, 0x8, agent, requireAll: true))
            {
                if (cortex is not null && b.Owner is AgentLabAgent o && (ReferenceEquals(o, cortex) || cortex.Links.Contains(o))) continue;
                if (ballBodies.Count < 0x14) ballBodies.Add(b);
            }
            return ballBodies;
        };
        var skateBodies = new List<Character.CharacterBody>(); // Amedo
        c.SkateBodies_157FC0 = (min, max, partner) =>
        {
            skateBodies.Clear();
            var cortex = partner as AgentLabAgent;
            var hits = world.QueryBodies(min, max, 0x5B010, 1, 0x18, agent, requireAll: false, skipLinks: false);
            for (int i = 0; i < hits.Count && i < 0x14; i++)
            {
                if (hits[i].Owner is AgentLabAgent o &&
                    (agent.Links.Contains(o) || cortex is not null && (ReferenceEquals(o, cortex) || cortex.Links.Contains(o)))) continue;
                skateBodies.Add(hits[i]);
            }
            return skateBodies;
        };
        c.BodyHit_23D1E0 = (target, sender, point, force) => target is not AgentLabAgent t || t.BodyHit_23D1E0(sender as AgentLabAgent, point, force); // Amedo
        c.ParticleHit = (point, radius, yScale) => world.Particles?.TestCollisionSpheres_1B7738(agent.RootPoint(point), radius, yScale) ?? false; // Amedo
        c.TrailLevel = agent.SetTrailLevel_64; // Amedo
        c.SetNodeOf = (owner, m) => { if (owner is AgentLabAgent a) a.GameMatrix = m; }; // Amedo
        c.SetAttachedTo_28 = holder => agent.SetAttachedTo_28(holder as AgentLabAgent); // Amedo
        c.PositionOf = owner => owner is AgentLabAgent a ? a.GamePosition : null; // Amedo
        c.AliveOf = owner => owner is AgentLabAgent a && !a.Destroyed; // Amedo
        c.TowCandidates_1ECFC0 = (min, max) => world.QueryBox_1ECFC0(min, max, 0x20u, 1, 0, true, agent, 0x20); // Amedo
        c.HoverRay_281408 = (from, to, extra) => // Amedo
        {
            var end = to;
            bool hit = c.CollisionWorld.SegmentCast(from, to, 0x40, out var h);
            if (hit)
            {
                if (!(0f < h.T)) return h.Point; // Amedo
                end = from * h.T + to * (1f - h.T); // Amedo
            }
            var bodies = world.QueryBodies(Vector3.Min(from, end), Vector3.Max(from, end), 0x5A010, 1, 0x10, agent, requireAll: true)
                .Where(b => !ReferenceEquals(b.Owner, extra) && !ReferenceEquals(b.Owner, agent.Parent28)); // Amedo
            float t = Physics.HullQueries.SegmentBodies_1F1740(bodies, from, end);
            if (0f <= t && t <= 1f) return from * (1f - t) + end * t;
            return hit ? h.Point : null;
        };
        c.SkateSounds = () => agent.SkateSounds; // Amedo
        c.InstanceInt_2619D8 = i => InstanceInt(p, agent.GameObject?.InstIntegers, i); // Amedo
        c.ClearTriggers_110 = () => world.ClearTriggers_110(c); // Amedo
        c.IdleSlot_211D30 = () => agent.PlaySlot(8, 0.2f, true); // Amedo
        c.SnapInstanceToGround = agent.SnapInstanceToGround_13D428; // Amedo
        c.Bones.Setup(InstanceInt(p, agent.GameObject?.InstIntegers, 0));
        c.IsActivePlayer = () => (int)(world.Progression >> 8 & 0xF) is var slot && slot != 6 && ReferenceEquals(world.PlayerSlot_570(slot), agent); // Amedo
        c.GunHolder = () => world.GunHolder_30A5B0 = agent; // Amedo
        c.HullHits = agent.HullHits_136B70; // Amedo
        c.Progression = () => world.Progression; // Amedo
        c.Clock = () => world.NowTicks; // Amedo
        c.KindSetUp_134F18(); // Amedo
        c.SetKindBoxes_13C758(InstanceInt(p, agent.GameObject?.InstIntegers, 0)); // Amedo 2026-10-03
        if (agent.Anim is { } anim) anim.Controllers = c.Bones;
        c.GoldenCheck_2C8260 = world.GoldenCheck_2C8260; // Amedo
        c.GoldenSeconds = world.GoldenSeconds; // Amedo 2026-10-04
        c.InvincibilityEnded = () => { if (ReferenceEquals(world.PlayerCharacter, c)) world.Music?.GoldenEnd_269890(); }; // Amedo 2026-10-04
        c.CutsceneMode = world.CutsceneActive;
        c.ApplyState_131B58(); // Amedo
        agent.OwnCharacter = c;
        return c;
    }

    // Amedo 2026-09-27
    private static int InstanceInt(PS2AnyInstance inst, IReadOnlyList<uint>? defaults, int index)
    {
        int count = inst.ParamList3?.Count ?? 0;
        if (index < count) return (int)inst.ParamList3![index];
        int k = index - count;
        return defaults is not null && k < defaults.Count ? (int)defaults[k] : 0;
    }

    private uint _cameraTicks; // Amedo 2026-10-02
    private bool _cameraDeferred;

    // Amedo 2026-10-02
    private void StepCamera()
    {
        if (!World.ChunkClocksRunning) // Amedo
        {
            GameCamera?.Hold();
            return;
        }
        if (WillCross_1ED690())
        {
            _cameraDeferred = true;
            return;
        }
        using (PerfStats.Measure(PerfSection.Camera)) GameCamera?.Step(Pad, _cameraTicks);
    }

    private Character.CharacterController? _heldBeforeGame;

    private void HoldPlayerBeforeGame()
    {
        bool beforeGame = World.GameMode <= AgentLabWorld.ModeMainMenu;
        if (beforeGame && World.PlayerCharacter is { } c && ReferenceEquals(c.Pad, Pad))
        {
            c.Pad = null;
            c.Move = Vector3.Zero;
            _heldBeforeGame = c;
        }
        else if (!beforeGame && _heldBeforeGame is { } held)
        {
            if (held.Pad is null && ReferenceEquals(World.PlayerCharacter, held)) held.Pad = Pad;
            _heldBeforeGame = null;
        }
    }

    private void LogicStep(double dt) // Amedo 2026-09-30
    {
        InputTape.Current?.BeginStep(this); // Amedo
        Pad.UpdateButtons_2B2348((int)Math.Round(dt * 9000.0) * (1f / 9000f), World.PadFlags_30971C); // Amedo
        UseLogicalCamera(); // Amedo 2026-09-30
        _cameraTicks = (uint)Math.Round(dt * 9000.0); // Amedo 2026-10-02
        World.CameraGroup_172A48 ??= StepCamera;
        HoldPlayerBeforeGame();
        using (PerfStats.Measure(PerfSection.Sim)) World.Step(dt); // Amedo 2026-09-28
        if (World.ResetPending) // Amedo 2026-09-28
        {
            _cameraDeferred = false; // Amedo 2026-10-02
            ResetScene();
        }
        else
        {
            using (PerfStats.Measure(PerfSection.Streaming)) // Amedo 2026-09-29
            {
                CheckCrossing_1ED690(); // Amedo 2026-09-28
                if (_cameraDeferred) // Amedo 2026-10-02
                {
                    _cameraDeferred = false;
                    using (PerfStats.Measure(PerfSection.Camera)) GameCamera?.Step(Pad, _cameraTicks);
                }
                StepStreaming();
                UpdateLook_19B280(); // Amedo 2026-09-29
                UpdateChunkDraw_1ECB58(); // Amedo 2026-10-06
            }
            World.DrawScene_1F11D8(GameCamera); // Amedo
        }
        StepAnimations_19FE38((int)Math.Round(World.Dt * 9000.0)); // Amedo
        using (PerfStats.Measure(PerfSection.Particles))
        {
            World.Particles?.Update(); // Amedo 2026-09-27
            if (World.Particles is { LogicDriven: true } stepped) stepped.StepWithLogic(GameCameraEye()); // Amedo
        }
        bool soundPaused = World.RunState_309718 == 3; // Amedo
        if (!soundPaused && !World.MoviePlaying) // Amedo
            using (PerfStats.Measure(PerfSection.Sounds)) World.Sounds?.Update(); // Amedo 2026-09-28
        if (!World.MoviePlaying) // Amedo
            using (PerfStats.Measure(PerfSection.Music)) World.Music?.Update((float)dt, soundPaused); // Amedo
        if (World.Sounds is { } groupsOwner && !World.MoviePlaying) // Amedo 2026-10-06
        {
            float soundDt = (int)Math.Round(dt * 9000.0) * (1f / 9000f);
            groupsOwner.Groups.UpdateFade_1DFFD8(soundDt); // Amedo
            groupsOwner.Groups.Update_1DEEE0(soundDt);
        }
        RecordStep(); // Amedo 2026-10-01
        InputTape.Current?.EndStep(this); // Amedo
    }

    // Amedo 2026-09-28
    public string? StartAudio()
    {
        if (World.Sounds is not { } sounds || _audio is not null) return null;
        if (Audio.AudioOutput.Create(sounds.Mixer) is not { } player) return null; // Amedo 2026-10-26
        if (player.Error is { } error)
        {
            player.Dispose();
            return error;
        }
        _audio = player;
        return null;
    }

    public int AudioBuffersWritten => _audio?.BuffersWritten ?? 0; // Amedo 2026-10-26

    public float Volume
    {
        get => World.Sounds?.Mixer.MasterVolume ?? 1f;
        set { if (World.Sounds is { } s) s.Mixer.MasterVolume = value; }
    }

    public bool Muted
    {
        get => World.Sounds?.Mixer.Muted ?? false;
        set { if (World.Sounds is { } s) s.Mixer.Muted = value; }
    }

    // Amedo 2026-09-27
    public void SetViewer(Matrix4x4 viewProjection, Vector3 cameraPosition)
    {
        if (InputTape.Current is { Replaying: true }) return; // Amedo
        HostViewer = (viewProjection, cameraPosition);
        SetViewerCore(viewProjection, cameraPosition);
    }

    // Amedo
    internal (Matrix4x4 ViewProjection, Vector3 Position)? HostViewer { get; private set; }
    internal (Vector3 Right, Vector3 Up, Vector3 Forward)? HostCamera { get; private set; }
    internal (Vector3 Right, Vector3 Up, Vector3 Forward)? ControlBasis => _controlBasis;
    internal void ApplyTapeViewer(Matrix4x4 viewProjection, Vector3 position) => SetViewerCore(viewProjection, position);
    internal void ApplyTapeCamera(in Vector3 right, in Vector3 up, in Vector3 forward, bool? hostShowsGameCamera = null)
    {
        if (hostShowsGameCamera is { } shows) _hostShowsGameCamera = shows; // Amedo
        else NoteHostCamera(forward);
        ApplyCameraBasis(right, up, forward);
    }
    internal bool HostShowsGameCamera => _hostShowsGameCamera; // Amedo
    internal void ApplyTapeControl((Vector3 Right, Vector3 Up, Vector3 Forward)? basis) => _controlBasis = basis;

    // Amedo 2026-11-11
    private int ReverbKey_22EA78()
    {
        if (World.Player?.Chunk is not { } chunk || World.Sounds is not { } sounds) return 0;
        var slot = chunk.Sounds[0];
        bool b = chunk.SoundBoxes_198.Count != 0 && Matrix4x4.Invert(chunk.ToRoot, out var toChunk)
            && chunk.InSoundBox_1F1E68(Vector3.Transform(World.ViewerPosition, toChunk));
        if (b) slot = chunk.Sounds[1];
        if (slot.Kind >= 0xFF) return 0;
        var mixer = sounds.Mixer;
        var (m0, m1) = (mixer.Bank(0), mixer.Bank(1));
        int key = mixer.ClaimBank_1DFBD8(Audio.SpuReverb.ModeOf_2FAFF0(slot.Kind), (int)(slot.C * 32767f));
        for (int core = 0; core < 2; core++)
            if (mixer.Bank(core) != (core == 0 ? m0 : m1))
                _log?.Invoke($"[sound {World.Now:0.00}s] reverb core {core}: mode {mixer.Bank(core).Mode} evol 0x{mixer.Bank(core).Evol:X4} ({chunk.Name} slot {(b ? "B" : "A")} kind {slot.Kind})");
        return key;
    }

    private void SetViewerCore(Matrix4x4 viewProjection, Vector3 cameraPosition) // Amedo
    {
        var gameToWorld = RootToWorld; // Amedo 2026-09-28
        World.ViewProjection = gameToWorld * viewProjection;
        World.ViewerPosition = Matrix4x4.Invert(gameToWorld, out var worldToGame)
            ? Vector3.Transform(cameraPosition, worldToGame)
            : cameraPosition;
        if (World.Sounds is { } sounds) sounds.ListenerPosition = World.ViewerPosition; // Amedo 2026-09-28
    }

    // Amedo 2026-09-27
    public void SetCamera(in Vector3 right, in Vector3 up, in Vector3 forward)
    {
        if (InputTape.Current is { Replaying: true }) return; // Amedo
        HostCamera = (right, up, forward);
        NoteHostCamera(forward); // Amedo 2026-09-30
        ApplyCameraBasis(right, up, forward);
    }

    // Amedo
    public (float FovDegrees, Vector2 ClipShift) PresentGameView(float fovDegrees, float windowAspect) =>
        (Screen.ScreenFit.FovDegrees(fovDegrees, windowAspect), Screen.ScreenFit.ClipShift(new Vector2(windowAspect, 1f), World.ScreenOffset_309728));

    // Amedo 2026-10-06
    public bool ViewFromGameCamera(float aspect)
    {
        if (GameCamera is not { } cam || !GameCameraView(out var eye, out var forward, out var up, out float fov)) return false;
        var view = CrashEngine.Renderer.CameraComponent.CreateLookAtRH(eye, eye + forward, up);
        var (shownFov, shift) = PresentGameView(fov, aspect); // Amedo
        var projection = CrashEngine.Renderer.CameraComponent.ShiftClip(CrashEngine.Renderer.CameraComponent.CreatePerspectiveRH(shownFov * MathF.PI / 180f, aspect, cam.Near, cam.Far), shift);
        SetViewer(view * projection, eye);
        SetCamera(new Vector3(view.M11, view.M21, view.M31), new Vector3(view.M12, view.M22, view.M32), -new Vector3(view.M13, view.M23, view.M33));
        return true;
    }

    // Amedo 2026-09-30
    private void ApplyCameraBasis(in Vector3 rightIn, in Vector3 upIn, in Vector3 forwardIn)
    {
        var right = rightIn; // Amedo 2026-10-27
        var up = upIn;
        var forward = forwardIn;
        var gameToWorld = RootToWorld; // Amedo 2026-09-28
        if (!Matrix4x4.Invert(gameToWorld, out var worldToGame)) return;
        static Vector3 Unit(Vector3 v) => GameMath.LengthSquared(v) > 0f ? GameMath.Normalize(v) : v;
        if (World.Sounds is { } sounds) // Amedo 2026-09-28
        {
            sounds.ListenerRight   = Unit(Vector3.TransformNormal(right, worldToGame));
            sounds.ListenerUp      = Unit(Vector3.TransformNormal(up, worldToGame));
            sounds.ListenerForward = Unit(Vector3.TransformNormal(forward, worldToGame));
        }
        if (World.PlayerCharacter is not { } character) return;
        if (_controlBasis is { } control) (right, up, forward) = control; // Amedo 2026-10-06
        character.CameraRight   = Unit(Vector3.TransformNormal(right, worldToGame));
        character.CameraUp      = Unit(Vector3.TransformNormal(up, worldToGame));
        character.CameraForward = Unit(Vector3.TransformNormal(forward, worldToGame));
    }

    // Amedo 2026-10-06
    private (Vector3 Right, Vector3 Up, Vector3 Forward)? _controlBasis;

    // Amedo 2026-10-06
    public void SetControlCamera(in Vector3 right, in Vector3 up, in Vector3 forward)
    {
        if (InputTape.Current is { Replaying: true }) return; // Amedo
        _controlBasis = (right, up, forward);
    }

    // Amedo 2026-10-06
    public void ClearControlCamera()
    {
        if (InputTape.Current is { Replaying: true }) return; // Amedo
        _controlBasis = null;
    }

    private readonly Screen.ScreenDraw _screenDraw = new(); // Amedo 2026-09-30

    // Amedo 2026-09-30
    public void DrawScreen(ImGuiNET.ImDrawListPtr list, Vector2 origin, Vector2 size)
    {
        var player = World.MoviePlayer_309720 is { Frame: not null } p ? p : null; // Amedo
        if (player is not null || !World.WorldShown) Screen.ScreenDraw.ClearFrame_19F1C8(list, origin, size); // Amedo
        if (World.Screen is { } screen)
            _screenDraw.Draw(screen, list, origin, size, World.Frame, SimulationClock.Active ? SimulationClock.Alpha : 1f, World.ScreenOffset_309728, GameViewBars); // Amedo
        if (player is { Frame: { } movie }) // Amedo
            _screenDraw.DrawMovie_2AEB30(list, origin, size, World.ScreenOffset_309728, movie, player.FrameWidth, player.FrameHeight, player.FrameVersion, player.Wide,
                Screen.ScreenFit.Frame_309744(World.Screen?.DiscLanguage ?? PlaySettings.AmericanLanguage));
    }

    // Amedo
    private static string? ResolveMovie(string? musicDirectory, string name)
    {
        if (musicDirectory is null || Directory.GetParent(musicDirectory) is not { } disc) return null;
        string folder = GamePaths.Folder(name); // Amedo 2026-11-20
        string? dir = folder.Length == 0 ? disc.FullName : GamePaths.FindOnDisk(disc.FullName, folder.Split(GamePaths.Separator)); // Amedo 2026-11-20
        if (dir is null || !Directory.Exists(dir)) return null;
        string file = GamePaths.FileName(name);
        foreach (string ext in MovieExtensions)
            foreach (string candidate in Directory.EnumerateFiles(dir))
                if (string.Equals(Path.GetFileName(candidate), file + ext, StringComparison.OrdinalIgnoreCase)) return candidate;
        return null;
    }

    private static readonly string[] MovieExtensions = [".PSS", ".mpg", ".mpeg", ".m2v"]; // Amedo

    public bool GameViewBars { get; set; } = true; // Amedo

    public void Stop()
    {
        StopRecording(); // Amedo 2026-10-01
        _screenDraw.Dispose(); // Amedo 2026-09-30
        World.Screen?.Dispose(); // Amedo 2026-10-01
        Screen.Oleg.MenuPage.StaticInit_2597C0();
        Screen.Oleg.LevelWidget.ClearSbss();
        ResetLook(); // Amedo 2026-09-29
        World.Particles?.Clear(); // Amedo 2026-09-27
        if (World.Particles is { } particles)
        {
            particles.AnchorDrawn = null; // Amedo
            particles.ClockStopped = null; // Amedo
            particles.LogicDriven = false; // Amedo
            particles.BoxInView = null; // Amedo
        }
        CrashEngine.Renderer.RenderPipeline.ShaderClockStopped = null; // Amedo
        CrashEngine.Renderer.SunShadowPass.FocusEntity = null;
        if (ReferenceEquals(Latest, this)) Latest = null;
        Engine.SceneHeld = null;
        World.Music?.StopAll(); // Amedo 2026-09-28
        World.Sounds?.StopAll();
        Pad.StopRumble(); // Amedo
        World.MoviePlayer_309720?.Dispose(); // Amedo
        _audio?.Dispose();
        _audio = null;
        StopStreaming(); // Amedo 2026-09-28
        World.RemoveDynamicScenery(null); // Amedo 2026-10-06
        RemoveAgents(); // Amedo 2026-09-28
        RestoreSnapshot();
        _snapshot.Clear();
        GameLightingDraw.Forget();
    }

    // Amedo 2026-09-28
    private void ResetScene()
    {
        SnapDisplay(); // Amedo 2026-09-30
        ReturnToAnchor(reviveUnloaded: false); // Amedo 2026-11-10
        World.StopFilteredObjectNodes_263C30(); // Amedo
        World.Particles?.KillParticles_1B9AB0(); // Amedo
        World.Music?.StopAll();
        World.Sounds?.StopAll();
        World.Sounds?.Groups.StopFade_1DFDD0(); // Amedo
        RemoveAgents();
        RestoreSnapshot();
        HideSkippedCharacters(); // Amedo 2026-09-30
        World.ClearForReset();
        CreateAgents();
        World.LoadChunkTriggers_26DDE8(); // Amedo
        foreach (var agent in World.Agents) agent.Begin();
        BindPlayer();
        ApplyInformation_173040(); // Amedo
        if (RespawnChunk_175618() is { } respawnChunk) // Amedo 2026-09-29
        {
            MovePlayer_1954A8(respawnChunk);
            PlaceSecond_171E90(respawnChunk);
        }
        World.FinishReset();
        GameCamera = Camera.RuntimeGameCamera.Create(World);
        _lookInstant = true; // Amedo 2026-09-29
        GameLightingDraw.SkipFades(); // Amedo 2026-09-30
        UpdateLook_19B280();
        UpdateChunkDraw_1ECB58(); // Amedo 2026-10-06
    }

    // Amedo 2026-09-28
    public void ResetGameCamera() => GameCamera = Camera.RuntimeGameCamera.Create(World);

    // Amedo 2026-09-28
    // Amedo
    private bool ParticleBoxInView(Vector3 centre, Vector3 half)
    {
        if (GameCamera is not { HasPose: true } cam || !Matrix4x4.Invert(World.RootChunk.ToAnchor, out var toRoot)) return true;
        var c = Vector3.Transform(centre, toRoot);
        return cam.BoxVisibility_1F5F90(c - half, c + half) != 0;
    }

    // Amedo
    private Vector3? GameCameraEye() => GameCamera is { HasPose: true } cam ? Vector3.Transform(cam.Pose.Translation, RootToWorld) : null;

    public bool GameCameraView(out Vector3 eye, out Vector3 forward, out Vector3 up, out float fovDegrees)
    {
        eye = forward = up = default;
        fovDegrees = 45f;
        if (GameCamera is not { } cam) return false;
        var gameToWorld = RootToWorld; // Amedo 2026-09-28
        var pose = cam.ShownPose; // Amedo 2026-09-30
        eye     = Vector3.Transform(new Vector3(pose.M41, pose.M42, pose.M43), gameToWorld);
        forward = Vector3.TransformNormal(new Vector3(pose.M31, pose.M32, pose.M33), gameToWorld);
        up      = Vector3.TransformNormal(new Vector3(pose.M21, pose.M22, pose.M23), gameToWorld);
        if (GameMath.LengthSquared(forward) <= 0f) return false;
        forward = GameMath.Normalize(forward);
        up      = GameMath.LengthSquared(up) > 0f ? GameMath.Normalize(up) : Vector3.UnitY;
        fovDegrees = cam.ShownFov; // Amedo 2026-09-30
        return true;
    }

    // Amedo 2026-09-28
    private void RemoveAgents()
    {
        foreach (var agent in World.Agents)
        {
            if (!agent.Rendered) agent.SetRendered(true); // Amedo 2026-09-27
            agent.Instance.ShadowOverride = null; // Amedo 2026-09-28
            agent.Instance.RuntimeShadowSlots = null; // Amedo
            agent.Instance.RuntimeFlags = null; // Amedo 2026-10-01
            if (agent.Anim is { } anim) { anim.Controllers = null; anim.LogicDriven = false; } // Amedo 2026-10-01
            agent.Entity.RemoveComponent<AgentLabAgent>();
        }
        foreach (var spawned in World.Spawned)
            spawned.Entity.Parent?.RemoveChild(spawned.Entity);
        World.UndoForeignGroups(); // Amedo 2026-09-28
    }

    // Amedo 2026-09-28
    private void RestoreSnapshot()
    {
        foreach (var s in _snapshot) Restore(s); // Amedo 2026-09-29
    }

    // Amedo 2026-09-30
    private void HideSkippedCharacters()
    {
        int hidden = 0;
        foreach (var s in _snapshot)
        {
            if (!s.Skipped || !s.Entity.Active) continue;
            s.Entity.Active = false;
            hidden++;
        }
        if (hidden > 0) _log?.Invoke($"[stream] reset: {hidden} duplicate character(s) kept hidden (slot already taken by the player)");
    }

    // Amedo 2026-09-29
    private static void Restore(Snapshot s)
    {
        s.Entity.Transform.LocalMatrix = s.LocalMatrix;
        s.Entity.Transform.Position    = s.Position;
        s.Entity.Transform.Rotation    = s.Rotation;
        s.Entity.Transform.Scale       = s.Scale;
        s.Entity.Active                = s.Active;
        if (s.Entity.Get<AnimatedObject>() is not { } anim) return;
        anim.Stop();
        if (s.Group is null) return;
        anim.Play(s.Group);
        anim.Frame   = s.Frame;
        anim.Playing = s.Playing;
        anim.Loop    = s.Loop;
    }

    public string UnhandledSummary(int top = 25) =>
        string.Join(", ", World.Unhandled.OrderByDescending(kv => kv.Value).Take(top).Select(kv => $"{kv.Key} x{kv.Value}"));

    private static IEnumerable<Entity> All(Entity e)
    {
        yield return e;
        foreach (var c in e.Children)
            foreach (var s in All(c))
                yield return s;
    }
}
