using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private string _fxFilter = "";
    private string? _fxSelected;
    private List<(string Name, string Level, int TexturePage)>? _fxGlobalCatalog;
    private bool _fxGlobalLoading;
    private int _fxGlobalLoadProgress;
    private int _fxGlobalLoadTotal;
    private string? _fxSelectedLevel;
    private readonly Dictionary<(string Level, string Name), Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem?> _fxResolvedCache = new();
    private MeshDecoder.ParticleTexturePage[]? _fxTexturePages;
    private bool _fxTexturePagesLoading;

    private List<(string Name, string Level, int Count)>? _emitterGlobalCatalog;
    private bool _emitterGlobalLoading;
    private string _emitterFilter = "";

    private static GpuMesh BuildParticleQuadMesh(GL gl, Vector2 uv0, Vector2 uv1)
    {
        var white = new Vector4(1f, 1f, 1f, 1f);
        GpuMesh.Vertex V(float x, float y, float u, float v) => new()
        {
            Position = new Vector3(x, y, 0f), Normal = Vector3.UnitZ, UV = new Vector2(u, v), Color = white
        };
        var v0 = V(-0.5f, -0.5f, uv0.X, uv1.Y);
        var v1 = V( 0.5f, -0.5f, uv1.X, uv1.Y);
        var v2 = V( 0.5f,  0.5f, uv1.X, uv0.Y);
        var v3 = V(-0.5f,  0.5f, uv0.X, uv0.Y);
        return new GpuMesh(gl, new[] { v0, v1, v2, v0, v2, v3 }, PrimitiveType.Triangles);
    }

    private void BuildParticleEmitterPreview(Entity markerEnt, string name, PS2AnyTwinsanityRM2 rm2)
    {
        var sys = MeshDecoder.FindParticleSystem(rm2, name);
        if (sys is null) return;

        EnsureFxTexturePagesLoaded();
        MeshDecoder.ParticleTexturePage? page = _fxTexturePages is not null && sys.UnkInt is >= 0 and < 3
            ? _fxTexturePages[sys.UnkInt] : null;
        (int X, int Y, int W, int H, float FillRatio)? icon = page is { Icons.Count: > 0 } pg
            ? pg.Icons.OrderByDescending(i => i.FillRatio).First() : null;

        var uv0 = Vector2.Zero;
        var uv1 = Vector2.One;
        Texture2D? pageTex = null;
        if (page is { } pgv && icon is { } ic)
        {
            uv0 = new Vector2(ic.X / (float)pgv.Width, ic.Y / (float)pgv.Height);
            uv1 = new Vector2((ic.X + ic.W) / (float)pgv.Width, (ic.Y + ic.H) / (float)pgv.Height);
            pageTex = pgv.Tex;
        }

        var gl = Engine.Instance.GL;
        var quadMesh = BuildParticleQuadMesh(gl, uv0, uv1);
        int particleCount = Math.Clamp((int)sys.UnkUShort2, 10, 200);

        var preview = markerEnt.Add(new ParticleEmitterPreview());
        preview.Members.Add(sys);
        for (int i = 0; i < particleCount; i++)
        {
            var pEnt = new Entity($"particle_{i}");
            var billboard = pEnt.Add(new ParticleBillboardRenderer { Mesh = quadMesh });
            if (pageTex is not null) billboard.Mat.Albedo = pageTex;
            preview.Particles.Add(billboard);
            markerEnt.AddChild(pEnt);
        }
    }

    private void LoadGlobalFxCatalog()
    {
        _fxGlobalLoading = true;
        Task.Run(() =>
        {
            var result = new List<(string, string, int)>();
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
                            foreach (var (name, texPage) in MeshDecoder.GetParticleSystemCatalog(def))
                                result.Add((name, @"Startup\Default", texPage));
                        }
                        catch (Exception ex) { _browser.Log($"Effects: Startup\\Default.rm2 particle scan failed: {ex.Message}"); }
                    }
                }

                var levels = new List<string>();
                using (var lvlStream = pkg.OpenByPath(@"Startup\LevelSelect.txt"))
                {
                    if (lvlStream is not null)
                    {
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
                    }
                }

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
                        foreach (var (name, texPage) in MeshDecoder.GetParticleSystemCatalog(rm2))
                            result.Add((name, levelPath, texPage));
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

                var levels = new List<string>();
                using (var lvlStream = pkg.OpenByPath(@"Startup\LevelSelect.txt"))
                {
                    if (lvlStream is not null)
                    {
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
                    }
                }

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
                                     .Select(em => new string(em.Name).TrimEnd('\0', ' '))
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
                _fxTexturePages = MeshDecoder.GetParticleTexturePages(Engine.Instance.GL, def);
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
        ImGui.TextDisabled($"{_fxGlobalCatalog.Count} particle system(s) across every level — real color/count/size, approximate motion (see tooltip).");
        if (ImGui.IsItemHovered())
            MaybeTooltip("Color-over-lifetime, particle count, and the grow/hold/shrink size\n" +
                         "curve are decoded from real data. Spawn shape/velocity/texture aren't\n" +
                         "confirmed yet — the preview's MOTION (drift direction) is still a\n" +
                         "generic approximation, not the real effect's actual spawn shape.");

        ImGui.BeginChild("##fxList", new Vector2(-1f, 160f), ImGuiChildFlags.Border);
        foreach (var (name, level, texPage) in _fxGlobalCatalog)
        {
            if (!string.IsNullOrWhiteSpace(_fxFilter) && !name.Contains(_fxFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            var swatchColor = MeshDecoder.GuessParticleTint(name);
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

            ImGui.TextDisabled(resolvedMembers.Count > 0
                ? $"Preview: {familyKey}*  ({resolvedMembers.Count} system(s) in this family, real color+count+size+direction+texture page)"
                : $"Preview: {_fxSelected}  (system not found)");
            if (ImGui.IsItemHovered()) MaybeTooltip(
                "Sprite shapes are the REAL per-system texture page (0/1/2, from sys.UnkInt) — 3 " +
                "real sprite atlases decoded from Startup\\Default.rm2. Which exact icon on that " +
                "page a system uses could not be identified despite a thorough data + Ghidra " +
                "investigation (see project_crashengine_particles memory) — shown icon defaults " +
                "to the page's largest shape, a fallback, not decoded data.");

            var avail = ImGui.GetContentRegionAvail().X;
            var boxSize = new Vector2(MathF.Min(avail, 220f), 110f);
            var p0 = ImGui.GetCursorScreenPos();
            var dl2 = ImGui.GetWindowDrawList();
            dl2.AddRectFilled(p0, p0 + boxSize, ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.08f, 0.08f, 1f)), 4f);

            var center = p0 + boxSize * 0.5f;
            const float baseRadius = 5f;
            float cycle = 1.6f;

            Vector4 peakColor = default;
            float peakBrightness = -1f;
            foreach (var (_, s) in resolvedMembers)
            {
                if (s is null) continue;
                foreach (var (_, c) in MeshDecoder.GetColorGradient(s))
                {
                    float b = c.X + c.Y + c.Z;
                    if (b > peakBrightness) { peakBrightness = b; peakColor = c; }
                }
            }
            if (peakBrightness > 0f)
            {
                float flashPhase = (EngineTime.Total / cycle) % 1f;
                float flashAlpha = MathF.Max(0f, 1f - flashPhase * 3.5f);
                float flashRadius = boxSize.Y * (0.12f + flashPhase * 0.35f);
                dl2.AddCircleFilled(center, flashRadius,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(peakColor.X, peakColor.Y, peakColor.Z, flashAlpha * 0.55f)), 24);
            }

            foreach (var (memberName, sys) in resolvedMembers)
            {
                if (sys is null) continue;
                var gradient = MeshDecoder.GetColorGradient(sys);
                var sizeCurve = MeshDecoder.GetSizeGradient(sys);
                int previewCount = Math.Clamp((int)sys.UnkUShort2, 8, 32);
                int nameHash = memberName.GetHashCode();

                float yawCenter = sys.UnkVec2.Z;
                float yawSpread = MathF.Abs(sys.UnkVec1.Z);
                float pitchSpread = MathF.Abs(sys.UnkVec1.Y);
                float posMag = MathF.Max(0.15f, MathF.Abs(sys.UnkVec2.X));
                float travelUnit = MathF.Min(posMag * 18f, MathF.Min(boxSize.X, boxSize.Y) * 0.48f);
                float spreadPx = MathF.Min(posMag * 6f, boxSize.X * 0.15f);

                MeshDecoder.ParticleTexturePage? page = _fxTexturePages is not null && sys.UnkInt is >= 0 and < 3
                    ? _fxTexturePages[sys.UnkInt] : null;
                (int X, int Y, int W, int H, float FillRatio)? memberIcon = page is { Icons.Count: > 0 } pgForIcon
                    ? pgForIcon.Icons.OrderByDescending(i => i.FillRatio).First()
                    : null;

                for (int i = 0; i < previewCount; i++)
                {
                    float phase = (EngineTime.Total / cycle + (i * 0.6180339887f + (nameHash & 0xFF) / 255f)) % 1f;
                    float jitterYaw = ((i + nameHash) * 12.9898f) % 1f;
                    float jitterPitch = ((i + nameHash) * 78.233f) % 1f;

                    float yaw = yawCenter + (jitterYaw * 2f - 1f) * yawSpread;
                    float pitch = (jitterPitch * 2f - 1f) * pitchSpread;
                    float cosPitch = MathF.Cos(pitch);
                    var coneDir = new Vector2(MathF.Cos(yaw) * cosPitch, -MathF.Sin(yaw));
                    if (coneDir.LengthSquared() < 1e-6f) coneDir = new Vector2(1f, 0f);
                    else coneDir = Vector2.Normalize(coneDir);
                    var perp = new Vector2(-coneDir.Y, coneDir.X);
                    float lateral = (jitterPitch - 0.5f) * spreadPx;
                    var pos = center + coneDir * (phase * travelUnit) + perp * lateral;

                    var col = MeshDecoder.SampleColorGradient(gradient, phase);
                    float alpha = MathF.Sin(phase * MathF.PI);
                    float sizeMul = MeshDecoder.SampleSizeGradient(sizeCurve, phase);
                    float radius = MathF.Max(0.5f, baseRadius * sizeMul);
                    uint tintU32 = ImGui.ColorConvertFloat4ToU32(new Vector4(col.X, col.Y, col.Z, alpha));

                    if (page is { } pg && pg.Tex is not null && memberIcon is { } icon)
                    {
                        var uv0 = new Vector2(icon.X / (float)pg.Width, icon.Y / (float)pg.Height);
                        var uv1 = new Vector2((icon.X + icon.W) / (float)pg.Width, (icon.Y + icon.H) / (float)pg.Height);
                        float iconAspect = icon.H > 0 ? icon.W / (float)icon.H : 1f;
                        float drawH = radius * 2.4f;
                        float drawW = drawH * iconAspect;
                        var half = new Vector2(drawW, drawH) * 0.5f;
                        dl2.AddImage((nint)pg.Tex.GlId, pos - half, pos + half, uv0, uv1, tintU32);
                    }
                    else
                    {
                        dl2.AddCircleFilled(pos, radius, tintU32, 10);
                    }
                }
            }
            ImGui.Dummy(boxSize);

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
            if (ImGui.IsItemHovered()) MaybeTooltip(
                "Copies this effect's real particle system definition(s) into the CURRENTLY\n" +
                "loaded level's own data, so a script here CAN reference it by name (AgentLab's\n" +
                "DoParticle). Does NOT itself make anything trigger it — no reliable link from\n" +
                "object/script to particle system name was ever found (see the note above).");
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
            if (ImGui.IsItemHovered()) MaybeTooltip(
                "Adds ONE placed instance of this ambient effect to the currently-loaded\n" +
                "level (at the camera's current position — drag it afterward with the normal\n" +
                "gizmo, see \"Show Particle Emitters\" in the Scene panel). Also copies its\n" +
                "underlying particle system definition if this level doesn't already have\n" +
                "one by that name. This one really is always-on — no script needed.");
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

            var destData = chunkSource.Rm2.GetItem<PS2AnyParticleData>((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
            if (destData is null)
            {
                destData = new PS2AnyParticleData();
                destData.SetID((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
                chunkSource.Rm2.AddItem(destData);
            }

            static TwinParticleSystem CloneParticleSystem(TwinParticleSystem src)
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

            int added = 0, alreadyPresent = 0, missing = 0;
            foreach (var memberName in memberNames)
            {
                var srcSys = MeshDecoder.FindParticleSystem(sourceRm2, memberName);
                if (srcSys is null) { missing++; continue; }

                bool exists = destData.ParticleSystems.Any(s =>
                    string.Equals(new string(s.Name).TrimEnd('\0', ' '), memberName, StringComparison.OrdinalIgnoreCase));
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
                string.Equals(new string(em.Name).TrimEnd('\0', ' '), name, StringComparison.OrdinalIgnoreCase));
            if (srcEmitter is null) { _browser.Log($"Add Ambient Effect: '{name}' not found in '{sourceLevel}'."); return; }

            var destData = chunkSource.Rm2.GetItem<PS2AnyParticleData>((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
            if (destData is null)
            {
                destData = new PS2AnyParticleData();
                destData.SetID((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
                chunkSource.Rm2.AddItem(destData);
            }

            static TwinParticleSystem CloneParticleSystem(TwinParticleSystem src)
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
            static TwinParticleEmitter CloneEmitter(TwinParticleEmitter src)
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

            bool templateExists = destData.ParticleSystems.Any(s =>
                string.Equals(new string(s.Name).TrimEnd('\0', ' '), name, StringComparison.OrdinalIgnoreCase));
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
                BuildParticleEmitterPreview(e, name, chunkSource.Rm2);
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
