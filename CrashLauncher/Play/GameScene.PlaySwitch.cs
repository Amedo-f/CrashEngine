using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Runtime;
using CrashEngine.Runtime.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;

namespace CrashLauncher;

// Amedo
public sealed partial class GameScene
{
    private Entity? _switchRoot;
    private Entity? _switchHidden;
    private string? _switchTo;
    private (System.Numerics.Vector3 Color, int? Index)? _switchFog;

    // Amedo
    private SceneSimulation? StartSimOn(Entity root, PlaySettings settings, ChunkSource? source)
    {
        var sim = SceneSimulation.Start(root, _browser.Log, settings, spawnHost: (parent, inst, section) =>
        {
            var entity = ChunkImporter.ImportInstance(inst, parent, new Dictionary<uint, CrashEngine.Stealth.PatrolPath>(), section);
            if (SourceOf(parent)?.MeshTables is { } tables)
                MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, tables, entity);
            return entity;
        }, groupHost: (e, ogi, anim, same) => SourceOf(e)?.MeshTables is { } tables
               ? MeshDecoder.BuildForeignGroup(Engine.Instance.GL, tables, e, ogi, anim, same) : null,
           chunkHost: PlayHost(source));
        if (sim is not null) sim.World.LevelSwitch = (chunk, _) => _switchTo = chunk;
        return sim;
    }

    // Amedo
    private void ApplyLevelSwitch()
    {
        if (_switchTo is not { } next || _simulation is not { } old) return;
        _switchTo = null;
        _browser.Log($"Play Scene: the game loads {next}: opening it for play (the level you are editing is kept as it is).");
        InputTape.Current?.Note('w', next); // Amedo
        var from = old.World;
        _simulation = null;
        old.Stop();
        DropSwitchRoot();
        Entity root;
        try
        {
            var gl = Engine.Instance.GL;
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            var parsed = ChunkImporter.ParseChunk(pkg, next + ".rm2", next + ".sm2");
            root = ChunkImporter.BuildChunk(gl, pkg, parsed, out _, editorExtras: false);
        }
        catch (Exception ex)
        {
            _browser.Log($"Play Scene: could not open {next}: {ex.Message}");
            PrepareForPlay();
            return;
        }
        if (RenderPipeline.Instance is { } pipe)
        {
            _switchFog ??= (pipe.FogColor, pipe.FogIndex);
            if (MeshDecoder.LastFogColor is { } fog) pipe.FogColor = fog;
            if (MeshDecoder.LastFogColorIndex is { } fogIndex) pipe.FogIndex = fogIndex;
        }
        var source = root.Get<ChunkSource>();
        root.Add(new GameLightingEnvironment { Scenery = source?.Sm2?.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM) });
        root.Add(new GameShadowCasters());
        AddSwitchParticles(root, source);
        _switchHidden ??= Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (_switchHidden is not null) _switchHidden.Active = false;
        LoadAndAddRoot(root);
        _switchRoot = root;
        _simulation = StartSimOn(root, CurrentPlaySettings(source), source);
        if (_simulation is null)
        {
            _browser.Log($"Play Scene: {next} has no level to play.");
            DropSwitchRoot();
            PrepareForPlay();
            return;
        }
        _simulation.World.ContinueLevelSwitch(from);
        _simulation.Volume = _simVolume;
        _simulation.Muted = !_simSound;
        if (_simulation.StartAudio() is { } audioError) _browser.Log("Play Scene: sound output unavailable: " + audioError);
    }

    // Amedo
    private void AddSwitchParticles(Entity root, ChunkSource? source)
    {
        var fxRoot = new Entity("ParticleEffects");
        var effects = fxRoot.Add(new ParticleEffectsRenderer { DistanceGating = _particleDistanceGating });
        EnsureFxTexturePagesLoaded();
        effects.Pages = _fxTexturePages;
        effects.Decals = _fxDecals;
        root.AddChild(fxRoot);
        var markers = new Entity("ParticleEmitters") { Active = false };
        root.AddChild(markers);
        if (source?.Rm2?.GetItem<PS2AnyParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM) is not { } data) return;
        foreach (var em in data.ParticleEmitters)
        {
            var marker = new Entity($"ParticleEmitter_{ParticleNames.Clean(em.Name)}");
            marker.Transform.Position = new System.Numerics.Vector3(em.Position.X, em.Position.Y, em.Position.Z);
            markers.AddChild(marker);
            AddParticleEffect(effects, marker, em, source);
        }
    }

    // Amedo
    private void DropSwitchRoot()
    {
        if (_switchRoot is { } root)
        {
            DropPlayHost();
            RemoveRoot(root);
            _switchRoot = null;
        }
    }

    // Amedo
    private void EndLevelSwitch()
    {
        _switchTo = null;
        if (_switchRoot is null && _switchHidden is null) return;
        DropSwitchRoot();
        if (_switchHidden is { } hidden) hidden.Active = true;
        _switchHidden = null;
        if (_switchFog is { } fog && RenderPipeline.Instance is { } pipe)
        {
            pipe.FogColor = fog.Color;
            if (fog.Index is { } index) pipe.FogIndex = index;
        }
        _switchFog = null;
        _browser.Log("Play Scene: back to the level you are editing.");
    }
}
