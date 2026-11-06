using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private string _fxFilter = "";
    private string? _fxSelected;
    private List<(string Name, string Level, int TexturePage, Vector4 Swatch)>? _fxGlobalCatalog; // Amedo 2026-09-26
    private bool _fxGlobalLoading;
    private int _fxGlobalLoadProgress;
    private int _fxGlobalLoadTotal;
    private string? _fxSelectedLevel;
    private readonly Dictionary<(string Level, string Name), Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem?> _fxResolvedCache = new();
    private MeshDecoder.ParticleTexturePage[]? _fxTexturePages;
    private DecalAssets? _fxDecals; // Amedo 2026-09-30
    private bool _fxTexturePagesLoading;

    private List<(string Name, string Level, int Count)>? _emitterGlobalCatalog;
    private bool _emitterGlobalLoading;
    private string _emitterFilter = "";

    // Amedo 2026-09-26
    private PreviewFbo? _fxPrevFbo;
    private ParticleWorld? _fxPrevWorld;
    private string? _fxPrevKey;
    private bool _fxPrevBurst;
    private bool _fxPrevFramed;
    private float _fxPrevYaw = 35f, _fxPrevPitch = 15f, _fxPrevZoom = 1f;
    private Vector3 _fxPrevCenter;
    private float _fxPrevRadius = 1f;
    private float _fxPrevRestartIn = -1f;
    private static readonly Matrix4x4 FxPreviewToEditor = Matrix4x4.CreateScale(-1f, 1f, 1f);

    // Amedo 2026-09-26
    private static void AddParticleEffect(ParticleEffectsRenderer? effects, Entity markerEnt, TwinParticleEmitter em, ChunkSource? src)
    {
        if (effects is null || src?.Rm2 is null) return;
        var name = ParticleNames.Clean(em.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        var sys = MeshDecoder.FindParticleSystem(src.Rm2, name)
                  ?? (src.GlobalRm2 is { } def ? MeshDecoder.FindParticleSystem(def, name) : null);
        if (sys is null) return;
        effects.AddEmitter(markerEnt, em, sys);
    }

    // Amedo 2026-09-26
    private void UpdateFxPreview(List<TwinParticleSystem> systems, string key)
    {
        if (_fxPrevWorld is null || _fxPrevKey != key)
        {
            _fxPrevWorld = new ParticleWorld();
            _fxPrevWorld.Rng = _fxPrevWorld.Decals.Rng = new CrashEngine.Core.GameRandom(); // Amedo
            foreach (var sys in systems)
            {
                var def = ParticleFx.DefFor(sys);
                _fxPrevWorld.Emitters.Add(new ParticleEmitterSim(def, _fxPrevWorld, def.Phase, _fxPrevBurst ? 1 : 0));
            }
            _fxPrevKey = key;
            _fxPrevFramed = false;
            _fxPrevRestartIn = -1f;
        }

        _fxPrevWorld.Advance(EngineTime.Delta, null, distanceGating: false);
        if (_fxPrevBurst)
        {
            float now = _fxPrevWorld.RenderTime;
            if (_fxPrevWorld.Emitters.All(e => e.Finished && !e.AnyAlive(now)))
            {
                if (_fxPrevRestartIn < 0f) _fxPrevRestartIn = 0.6f;
                _fxPrevRestartIn -= EngineTime.Delta;
                if (_fxPrevRestartIn <= 0f) { _fxPrevWorld.Reset(); _fxPrevRestartIn = -1f; }
            }
        }

        if (ParticleFx.LiveBounds(_fxPrevWorld, FxPreviewToEditor, out var mn, out var mx))
        {
            var c = (mn + mx) * 0.5f;
            float r = MathF.Max((mx - mn).Length() * 0.5f, 0.25f);
            if (!_fxPrevFramed) { _fxPrevCenter = c; _fxPrevRadius = r; _fxPrevFramed = true; }
            else
            {
                float k = 1f - MathF.Exp(-EngineTime.Delta * 2f);
                _fxPrevCenter = Vector3.Lerp(_fxPrevCenter, c, k);
                _fxPrevRadius += (r - _fxPrevRadius) * (r > _fxPrevRadius ? k : k * 0.15f);
            }
        }
    }

    // Amedo 2026-09-26
    private void RenderFxPreview(int w, int h)
    {
        var gl = Engine.Instance.GL;
        _fxPrevFbo ??= new PreviewFbo(gl, w, h);
        _fxPrevFbo.Resize(w, h);
        _fxPrevFbo.Begin(new Vector3(0.08f, 0.08f, 0.1f));
        if (_fxPrevWorld is not null && _fxTexturePages is not null)
        {
            float fov = 50f * MathF.PI / 180f;
            float dist = _fxPrevRadius / MathF.Sin(fov * 0.5f) * _fxPrevZoom;
            float yr = _fxPrevYaw * MathF.PI / 180f, pr = _fxPrevPitch * MathF.PI / 180f;
            var eye = _fxPrevCenter + new Vector3(MathF.Cos(pr) * MathF.Sin(yr), MathF.Sin(pr), MathF.Cos(pr) * MathF.Cos(yr)) * dist;
            var view = CameraComponent.CreateLookAtRH(eye, _fxPrevCenter, Vector3.UnitY);
            var proj = CameraComponent.CreatePerspectiveRH(fov, (float)w / h, 0.02f, 5000f);
            ParticleFx.Draw(gl, _fxPrevWorld, view, proj, FxPreviewToEditor, _fxTexturePages, drawDistanceCull: false);
        }
        _fxPrevFbo.End(Engine.Instance.Width, Engine.Instance.Height);
        RenderPipeline.Instance?.Shader.Use();
    }

    private static readonly string[] FxBlendNames = { "Add", "Subtract", "Mix", "Opaque" }; // Amedo 2026-09-26

    private void LoadGlobalFxCatalog()
    {
        _fxGlobalLoading = true;
        Task.Run(() =>
        {
            var result = new List<(string, string, int, Vector4)>(); // Amedo 2026-09-26
            try
            {
                using var pkg = PackageReader.Open(_extractedRoot);

                using (var defStream = pkg.OpenByPath(@"Startup\Default.rm2"))
                {
                    if (defStream is not null)
                    {
                        try
                        {
                            var def = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
                            using var reader = new BinaryReader(defStream);
                            def.Read(reader, (int)defStream.Length);
                            foreach (var (name, texPage, swatch) in MeshDecoder.GetParticleSystemCatalog(def)) // Amedo 2026-09-26
                                result.Add((name, @"Startup\Default", texPage, swatch));
                        }
                        catch (Exception ex) { _browser.Log($"Effects: Startup\\Default.rm2 particle scan failed: {ex.Message}"); }
                    }
                }

                var levels = ReadLevelSelectPaths(pkg);

                _fxGlobalLoadTotal = levels.Count;
                for (int li = 0; li < levels.Count; li++)
                {
                    var levelPath = levels[li];
                    _fxGlobalLoadProgress = li + 1;
                    try
                    {
                        using var stream = pkg.OpenByPath($"{levelPath}.rm2");
                        if (stream is null) continue;
                        var rm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
                        using var reader = new BinaryReader(stream);
                        rm2.Read(reader, (int)stream.Length);
                        foreach (var (name, texPage, swatch) in MeshDecoder.GetParticleSystemCatalog(rm2)) // Amedo 2026-09-26
                            result.Add((name, levelPath, texPage, swatch));
                    }
                    catch {  }
                }
            }
            catch (Exception ex) { _browser.Log($"Effects: failed to load global catalog: {ex.Message}"); }

            _fxGlobalCatalog = result.OrderBy(t => t.Item1, StringComparer.OrdinalIgnoreCase).ToList();
            _fxGlobalLoading = false;
            int sourceFileCount = result.Select(r => r.Item2).Distinct().Count();
            _browser.Log($"Effects: loaded {_fxGlobalCatalog.Count} particle system(s) across {sourceFileCount} source file(s).");
        });
    }

    private void LoadGlobalEmitterCatalog()
    {
        _emitterGlobalLoading = true;
        Task.Run(() =>
        {
            var result = new List<(string Name, string Level, int Count)>();
            try
            {
                using var pkg = PackageReader.Open(_extractedRoot);

                var levels = ReadLevelSelectPaths(pkg);

                foreach (var levelPath in levels)
                {
                    try
                    {
                        using var stream = pkg.OpenByPath($"{levelPath}.rm2");
                        if (stream is null) continue;
                        var rm2 = new PS2AnyTwinsanityRM2();
                        using var reader = new BinaryReader(stream);
                        rm2.Read(reader, (int)stream.Length);
                        var data = rm2.GetItem<PS2AnyParticleData>((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
                        if (data is null) continue;
                        foreach (var grp in data.ParticleEmitters
                                     .Select(em => ParticleNames.Clean(em.Name)) // Amedo 2026-09-26
                                     .Where(n => !string.IsNullOrWhiteSpace(n))
                                     .GroupBy(n => n, StringComparer.OrdinalIgnoreCase))
                            result.Add((grp.Key, levelPath, grp.Count()));
                    }
                    catch {  }
                }
            }
            catch (Exception ex) { _browser.Log($"Ambient Emitters: failed to load global catalog: {ex.Message}"); }

            _emitterGlobalCatalog = result.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
            _emitterGlobalLoading = false;
            _browser.Log($"Ambient Emitters: loaded {_emitterGlobalCatalog.Count} placed effect group(s) across every level.");
        });
    }

    private static List<string> ReadLevelSelectPaths(PackageReader pkg)
    {
        var levels = new List<string>();
        using var lvlStream = pkg.OpenByPath(@"Startup\LevelSelect.txt");
        if (lvlStream is null) return levels;
        using var sr = new StreamReader(lvlStream);
        foreach (var line in sr.ReadToEnd().Split('\n'))
        {
            var quoted = System.Text.RegularExpressions.Regex.Matches(line, "\"([^\"]*)\"");
            if (quoted.Count > 0)
            {
                var p = quoted[^1].Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(p)) levels.Add(p);
            }
        }
        return levels;
    }

    private static string GetFxFamilyKey(string name)
    {
        if (name.Length >= 2 && char.IsUpper(name[^1]) && char.IsDigit(name[^2]))
            return name[..^1];
        return name;
    }

    private void EnsureFxTexturePagesLoaded()
    {
        if (_fxTexturePages is not null || _fxTexturePagesLoading) return;
        _fxTexturePagesLoading = true;
        try
        {
            var chunkSrc = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
            var def = chunkSrc?.GlobalRm2;
            if (def is null)
            {
                using var pkg = PackageReader.Open(_extractedRoot);
                using var stream = pkg.OpenByPath(@"Startup\Default.rm2");
                if (stream is not null)
                {
                    var freshDef = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
                    using var reader = new BinaryReader(stream);
                    freshDef.Read(reader, (int)stream.Length);
                    def = freshDef;
                }
            }
            if (def is not null)
            {
                _fxTexturePages = MeshDecoder.GetParticleTexturePages(Engine.Instance.GL, def);
                _fxDecals = MeshDecoder.GetDecalAssets(Engine.Instance.GL, def); // Amedo 2026-09-30
            }
        }
        catch (Exception ex) { _browser.Log($"Effects: failed to load particle texture pages: {ex.Message}"); }
        finally { _fxTexturePagesLoading = false; }
    }

    private void DrawEffectsBrowser()
    {
        if (_fxGlobalCatalog is null && !_fxGlobalLoading) LoadGlobalFxCatalog();
        EnsureFxTexturePagesLoaded();

        if (_fxGlobalLoading)
        {
            ImGui.TextDisabled(_fxGlobalLoadTotal > 0
                ? $"Scanning level {_fxGlobalLoadProgress}/{_fxGlobalLoadTotal} for particle effects (one-time, cached)..."
                : "Scanning Startup\\Default.rm2 for particle effects...");
            return;
        }
        if (_fxGlobalCatalog is null || _fxGlobalCatalog.Count == 0)
        {
            ImGui.TextDisabled("(no particle effects found)");
            return;
        }

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##fxFilter", "Search effects (e.g. NITRO, EXP, MIST)...", ref _fxFilter, 128);
        ImGui.TextDisabled($"{_fxGlobalCatalog.Count} particle system(s) across every level — preview = the game's own particle math."); // Amedo 2026-09-26

        ImGui.BeginChild("##fxList", new Vector2(-1f, 160f), ImGuiChildFlags.Border);
        foreach (var (name, level, texPage, swatchColor) in _fxGlobalCatalog) // Amedo 2026-09-26
        {
            if (!string.IsNullOrWhiteSpace(_fxFilter) && !name.Contains(_fxFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            var cursor = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(cursor, cursor + new Vector2(14f, 14f),
                ImGui.ColorConvertFloat4ToU32(swatchColor), 3f);
            ImGui.Dummy(new Vector2(18f, 14f));
            ImGui.SameLine();
            var levelShort = level.Contains('\\') ? level[(level.LastIndexOf('\\') + 1)..] : level;
            if (ImGui.Selectable($"{name}  [{levelShort}, tex page {texPage}]##fx{name}{level}",
                    _fxSelected == name && _fxSelectedLevel == level))
            {
                _fxSelected = name;
                _fxSelectedLevel = level;
                _fxPrevBurst = _emitterGlobalCatalog is not null && !_emitterGlobalCatalog.Any(e => // Amedo 2026-09-26
                    string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            }
        }
        ImGui.EndChild();

        if (_fxSelected is not null && _fxSelectedLevel is not null)
        {
            var fxChunkSrc = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();

            string familyKey = GetFxFamilyKey(_fxSelected);
            var family = _fxGlobalCatalog!
                .Where(e => e.Level == _fxSelectedLevel && GetFxFamilyKey(e.Name) == familyKey)
                .Select(e => e.Name)
                .Distinct()
                .ToList();

            var resolvedMembers = family
                .Select(n => (Name: n, Sys: ResolveGlobalParticleSystem(_fxSelectedLevel, n, fxChunkSrc)))
                .Where(m => m.Sys is not null)
                .ToList();

            ImGui.TextDisabled(resolvedMembers.Count > 0 // Amedo 2026-09-26
                ? $"Preview: {familyKey}*  ({resolvedMembers.Count} system(s) in this family, drag = rotate, wheel = zoom)"
                : $"Preview: {_fxSelected}  (system not found)");

            // Amedo 2026-09-26
            if (resolvedMembers.Count > 0)
            {
                var systems = resolvedMembers.Select(m => m.Sys!).ToList();
                UpdateFxPreview(systems, $"{_fxSelectedLevel}|{familyKey}|{_fxPrevBurst}");

                int pw = (int)MathF.Max(64f, ImGui.GetContentRegionAvail().X), ph = 200;
                RenderFxPreview(pw, ph);
                ImGui.Image((nint)_fxPrevFbo!.ColorTexture, new Vector2(pw, ph), new Vector2(0, 1), new Vector2(1, 0));
                if (ImGui.IsItemHovered())
                {
                    var io = ImGui.GetIO();
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                    {
                        _fxPrevYaw -= io.MouseDelta.X * 0.4f;
                        _fxPrevPitch = Math.Clamp(_fxPrevPitch + io.MouseDelta.Y * 0.4f, -89f, 89f);
                    }
                    if (io.MouseWheel != 0) _fxPrevZoom = Math.Clamp(_fxPrevZoom * (1f - io.MouseWheel * 0.12f), 0.1f, 20f);
                }

                if (ImGui.Checkbox("Burst##fxburst", ref _fxPrevBurst)) _fxPrevKey = null;
                ImGui.SameLine();
                if (ImGui.SmallButton("Restart##fxrestart")) _fxPrevWorld?.Reset();

                foreach (var (memberName, sys) in resolvedMembers)
                {
                    var def = ParticleFx.DefFor(sys!);
                    string blend = def.Blend is >= 0 and < 4 ? FxBlendNames[def.Blend] : $"blend {def.Blend}";
                    string rate = def.Rate >= 0 ? $"{def.Rate}/frame" : $"1 every {-def.Rate} frames";
                    ImGui.TextDisabled($"{memberName}: shape {def.SpawnClass}, {blend}, life {def.Life:0.##}s, {rate}, " +
                                       $"on {def.OnFrames}/off {def.OffFrames}, max {def.MaxAlive}, page {def.Page}");
                }
            }

            var currentChunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
            bool isSameLevel = currentChunkRoot is not null &&
                string.Equals(_rm2.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? _rm2[..^4] : _rm2,
                               _fxSelectedLevel, StringComparison.OrdinalIgnoreCase);
            ImGui.BeginDisabled(currentChunkRoot is null || isSameLevel || resolvedMembers.Count == 0);
            if (ImGui.Button($"Import '{familyKey}*' into the currently-loaded level##fximport", new Vector2(-1f, 0f)))
                ImportParticleFamilyIntoCurrentLevel(familyKey, _fxSelectedLevel, family);
            ImGui.EndDisabled();
            if (isSameLevel)
                ImGui.TextDisabled("(already this level's own effect)");
        }

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1f), "Ambient World Effects (always-on, placed)");
        if (_emitterGlobalCatalog is null && !_emitterGlobalLoading) LoadGlobalEmitterCatalog();
        if (_emitterGlobalLoading)
            ImGui.TextDisabled("Scanning every level for ambient placed effects (one-time, cached)...");
        else if (_emitterGlobalCatalog is null || _emitterGlobalCatalog.Count == 0)
            ImGui.TextDisabled("(no ambient placed effects found)");
        else
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##emitterFilter", "Search ambient effects (e.g. SNOW, MIST, BUBBLES)...", ref _emitterFilter, 128);
            ImGui.BeginChild("##emitterList", new Vector2(-1f, 140f), ImGuiChildFlags.Border);
            foreach (var (name, level, count) in _emitterGlobalCatalog)
            {
                if (!string.IsNullOrWhiteSpace(_emitterFilter) && !name.Contains(_emitterFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                var levelShort = level.Contains('\\') ? level[(level.LastIndexOf('\\') + 1)..] : level;
                ImGui.TextDisabled($"{name}  [{levelShort}, {count}x placed]");
                ImGui.SameLine();
                var currentChunkRoot2 = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                bool sameLvl = string.Equals(_rm2.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? _rm2[..^4] : _rm2,
                                              level, StringComparison.OrdinalIgnoreCase);
                ImGui.BeginDisabled(currentChunkRoot2 is null || sameLvl);
                if (ImGui.SmallButton($"+ Add here##emadd{name}{level}"))
                    ImportEmitterIntoCurrentLevel(name, level);
                ImGui.EndDisabled();
            }
            ImGui.EndChild();
        }
    }

    private void ImportParticleFamilyIntoCurrentLevel(string familyKey, string sourceLevel, List<string> memberNames)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.Rm2 is null) { _browser.Log("Import Effect: no level currently loaded."); return; }

        try
        {
            PS2AnyTwinsanityRM2 sourceRm2;
            using (var pkg = PackageReader.Open(_extractedRoot))
            {
                if (string.Equals(sourceLevel, @"Startup\Default", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = pkg.OpenByPath(@"Startup\Default.rm2")
                        ?? throw new FileNotFoundException("Startup\\Default.rm2 not found in the package.");
                    var def = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
                    using var reader = new BinaryReader(stream);
                    def.Read(reader, (int)stream.Length);
                    sourceRm2 = def;
                }
                else
                {
                    using var stream = pkg.OpenByPath($"{sourceLevel}.rm2")
                        ?? throw new FileNotFoundException($"'{sourceLevel}.rm2' not found in the package.");
                    var rm2 = new PS2AnyTwinsanityRM2();
                    using var reader = new BinaryReader(stream);
                    rm2.Read(reader, (int)stream.Length);
                    sourceRm2 = rm2;
                }
            }

            var destData = GetOrAddParticleData(chunkSource.Rm2);

            int added = 0, alreadyPresent = 0, missing = 0;
            foreach (var memberName in memberNames)
            {
                var srcSys = MeshDecoder.FindParticleSystem(sourceRm2, memberName);
                if (srcSys is null) { missing++; continue; }

                bool exists = destData.ParticleSystems.Any(s =>
                    string.Equals(ParticleNames.Clean(s.Name), memberName, StringComparison.OrdinalIgnoreCase)); // Amedo 2026-09-26
                if (exists) { alreadyPresent++; continue; }

                destData.ParticleSystems.Add(CloneParticleSystem(srcSys));
                added++;
            }

            _browser.Log($"Import Effect: '{familyKey}*' from '{sourceLevel}' — {added} added, " +
                          $"{alreadyPresent} already present, {missing} not found. Not undoable — Reload " +
                          "Level discards it. Save Chunk writes it into this level's own Rm2 (normal path, " +
                          "no dirty flag needed). This only makes the DEFINITION resolvable by name — it " +
                          "doesn't make anything in this level actually trigger it.");
        }
        catch (Exception ex) { _browser.Log($"Import Effect failed: {ex.Message}"); }
    }

    private static PS2AnyParticleData GetOrAddParticleData(PS2AnyTwinsanityRM2 rm2)
    {
        if (rm2.GetItem<PS2AnyParticleData>((uint)TwinConstants.LEVEL_PARTICLES_ITEM) is { } data) return data;
        data = new PS2AnyParticleData();
        data.SetID((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
        rm2.AddItem(data);
        return data;
    }

    private static TwinParticleSystem CloneParticleSystem(TwinParticleSystem src)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms)) src.Write(w);
        var bytes = ms.ToArray();
        var clone = new TwinParticleSystem(src.Version);
        using var rs = new MemoryStream(bytes);
        using var r = new BinaryReader(rs);
        clone.Read(r, bytes.Length);
        return clone;
    }

    private static TwinParticleEmitter CloneEmitter(TwinParticleEmitter src)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms)) src.Write(w);
        var bytes = ms.ToArray();
        var clone = new TwinParticleEmitter(src.Version);
        using var rs = new MemoryStream(bytes);
        using var r = new BinaryReader(rs);
        clone.Read(r, bytes.Length);
        return clone;
    }

    private void ImportEmitterIntoCurrentLevel(string name, string sourceLevel)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.Rm2 is null) { _browser.Log("Add Ambient Effect: no level currently loaded."); return; }

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{sourceLevel}.rm2")
                ?? throw new FileNotFoundException($"'{sourceLevel}.rm2' not found in the package.");
            var sourceRm2 = new PS2AnyTwinsanityRM2();
            using (var reader = new BinaryReader(stream))
                sourceRm2.Read(reader, (int)stream.Length);

            var srcData = sourceRm2.GetItem<PS2AnyParticleData>((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
            var srcEmitter = srcData?.ParticleEmitters.FirstOrDefault(em =>
                string.Equals(ParticleNames.Clean(em.Name), name, StringComparison.OrdinalIgnoreCase)); // Amedo 2026-09-26
            if (srcEmitter is null) { _browser.Log($"Add Ambient Effect: '{name}' not found in '{sourceLevel}'."); return; }

            var destData = GetOrAddParticleData(chunkSource.Rm2);

            bool templateExists = destData.ParticleSystems.Any(s =>
                string.Equals(ParticleNames.Clean(s.Name), name, StringComparison.OrdinalIgnoreCase)); // Amedo 2026-09-26
            bool templateAdded = false;
            if (!templateExists)
            {
                var srcSys = MeshDecoder.FindParticleSystem(sourceRm2, name);
                if (srcSys is not null)
                {
                    destData.ParticleSystems.Add(CloneParticleSystem(srcSys));
                    templateAdded = true;
                }
            }

            var newEmitter = CloneEmitter(srcEmitter);
            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            if (Matrix4x4.Invert(chunkRoot!.Transform.World, out var toGame)) spawnPos = Vector3.Transform(spawnPos, toGame); // Amedo 2026-09-26
            newEmitter.Position.X = spawnPos.X;
            newEmitter.Position.Y = spawnPos.Y;
            newEmitter.Position.Z = spawnPos.Z;
            destData.ParticleEmitters.Add(newEmitter);

            if (_particleEmittersRoot is not null)
            {
                var e = new Entity($"ParticleEmitter_{name}");
                e.Transform.Position = spawnPos;
                e.Transform.Scale    = new Vector3(0.4f, 0.4f, 0.4f);
                var rdr = e.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = new Vector4(0.6f, 0.85f, 1f, 0.7f) });
                rdr.Mat.AlphaBlend = true;
                e.Add(new CrashEngine.Importer.ParticleEmitterMarker { Source = newEmitter });
                _particleEmittersRoot.AddChild(e);
                AddParticleEffect(_particleEffects, e, newEmitter, chunkSource); // Amedo 2026-09-26
                _particleEmittersRoot.Active = true;
                _showParticleEmitters = true;
            }

            _browser.Log($"Add Ambient Effect: placed '{name}' from '{sourceLevel}' at the camera position" +
                          (templateAdded ? " (+ its particle system template)" : "") +
                          ". Not undoable — Reload Level discards it. Drag it with the normal gizmo, then " +
                          "Save Chunk to keep it (\"Show Particle Emitters\" in the Scene panel to see it).");
        }
        catch (Exception ex) { _browser.Log($"Add Ambient Effect failed: {ex.Message}"); }
    }

    private Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem? ResolveGlobalParticleSystem(
        string level, string name, ChunkSource? currentChunkSrc)
    {
        var key = (level, name);
        if (_fxResolvedCache.TryGetValue(key, out var cached)) return cached;

        Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem? found = null;
        try
        {
            if (level == @"Startup\Default")
            {
                if (currentChunkSrc?.GlobalRm2 is not null)
                    found = MeshDecoder.FindParticleSystem(currentChunkSrc.GlobalRm2, name);
                else
                {
                    using var pkg = PackageReader.Open(_extractedRoot);
                    using var stream = pkg.OpenByPath(@"Startup\Default.rm2");
                    if (stream is not null)
                    {
                        var def = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default();
                        using var reader = new BinaryReader(stream);
                        def.Read(reader, (int)stream.Length);
                        found = MeshDecoder.FindParticleSystem(def, name);
                    }
                }
            }
            else if (currentChunkSrc is not null &&
                     string.Equals(System.IO.Path.ChangeExtension(currentChunkSrc.Rm2Path, null), level, StringComparison.OrdinalIgnoreCase))
            {
                found = MeshDecoder.FindParticleSystem(currentChunkSrc.Rm2, name);
            }
            else
            {
                using var pkg = PackageReader.Open(_extractedRoot);
                using var stream = pkg.OpenByPath($"{level}.rm2");
                if (stream is not null)
                {
                    var rm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
                    using var reader = new BinaryReader(stream);
                    rm2.Read(reader, (int)stream.Length);
                    found = MeshDecoder.FindParticleSystem(rm2, name);
                }
            }
        }
        catch (Exception ex) { _browser.Log($"Effects: failed to resolve '{name}' from '{level}': {ex.Message}"); }

        _fxResolvedCache[key] = found;
        return found;
    }
}
