using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using SysVec3 = System.Numerics.Vector3;
using SysVec4 = System.Numerics.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinSceneryBaseType = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryBaseType;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace CrashEngine.Importer;

public static partial class MeshDecoder
{
    private static (bool HadMesh, bool HadAnim, bool HadAlt) BuildOneInstanceMesh(
        GL gl, RmTables tables, Entity e, InstanceData data)
    {
        if (!tables.Objects.TryGetValue(data.ObjectId, out var obj)) return (false, false, false);

        string instBaseName = $"Inst_{data.Source.GetID():X4}";
        e.Name = string.IsNullOrWhiteSpace(obj.Name)
            ? instBaseName
            : $"{obj.Name.Replace("|", "_")}_{instBaseName}";

        var defaultOgi = ResolveDefaultOgi(tables, obj, out var defaultParts);
        bool hadMesh = defaultParts.Count > 0;

        var slotSpecs = new List<(int Index, ushort AnimId, PS2AnyAnimation Anim, PS2AnyOGI Ogi)>();
        for (int i = 0; i < obj.AnimationSlots.Count; i++)
        {
            var animId = obj.AnimationSlots[i];
            if (animId == 0xFFFF) continue;
            if (!tables.Animations.TryGetValue(animId, out var animItem) || !animItem.HasAnimationData) continue;
            ushort ogiSlotId = i < obj.OGISlots.Count ? obj.OGISlots[i] : (ushort)0xFFFF;
            if (ogiSlotId == 0xFFFF || !tables.OGIs.TryGetValue(ogiSlotId, out var slotOgi)) continue;
            slotSpecs.Add((i, animId, animItem, slotOgi));
        }

        var modelSpecs = new List<(int Index, PS2AnyOGI Ogi)>(); // Amedo 2026-09-28
        for (int i = 0; i < obj.OGISlots.Count; i++)
        {
            if (slotSpecs.Any(s => s.Index == i)) continue;
            ushort modelOgiId = obj.OGISlots[i];
            if (modelOgiId == 0xFFFF || !tables.OGIs.TryGetValue(modelOgiId, out var modelOgi)) continue;
            modelSpecs.Add((i, modelOgi));
        }
        bool anyOtherModel = modelSpecs.Any(m => !ReferenceEquals(m.Ogi, defaultOgi));

        AnimatedObject? animObj = slotSpecs.Count > 0 || anyOtherModel ? e.Add(new AnimatedObject { ReactSlots = obj.ReactJointAmount }) : null; // Amedo
        bool hadAnim = animObj is not null;

        AnimatedObject.SlotGroup? defaultGroup = null;
        if (defaultOgi is not null)
        {
            var parent = animObj is null ? e : NewChild(e, $"{e.Name}_default");
            var (attached, morphs) = EmitOgiMeshEntities(gl, tables, parent, defaultParts, defaultOgi, animObj);
            if (animObj is not null)
            {
                defaultGroup = new AnimatedObject.SlotGroup { Ogi = defaultOgi, MeshRoot = parent };
                defaultGroup.AttachedRigidParts.AddRange(attached);
                defaultGroup.MorphMeshes.AddRange(morphs);
            }
        }
        if (animObj is null) return (hadMesh, hadAnim, false);
        animObj.DefaultGroup = defaultGroup;

        bool anyAlt = false;
        foreach (var (index, animId, animItem, slotOgi) in slotSpecs)
        {
            AnimatedObject.SlotGroup group;
            if (defaultGroup is not null && ReferenceEquals(slotOgi, defaultOgi))
            {
                group = new AnimatedObject.SlotGroup { Ogi = slotOgi, MeshRoot = defaultGroup.MeshRoot };
                group.AttachedRigidParts.AddRange(defaultGroup.AttachedRigidParts);
                group.MorphMeshes.AddRange(defaultGroup.MorphMeshes);
            }
            else
            {
                anyAlt = true;
                var altRoot = NewChild(e, $"{e.Name}_slot{index}");
                altRoot.Active = false;
                var altParts = BuildOgiMeshes(tables, slotOgi);
                var (attached, morphs) = EmitOgiMeshEntities(gl, tables, altRoot, altParts, slotOgi, animObj);
                group = new AnimatedObject.SlotGroup { Ogi = slotOgi, MeshRoot = altRoot };
                group.AttachedRigidParts.AddRange(attached);
                group.MorphMeshes.AddRange(morphs);
            }
            group.SlotIndex = (ushort)index;
            group.AnimId    = animId;
            group.Anim      = animItem;
            animObj.Groups.Add(group);
        }
        foreach (var (index, modelOgi) in modelSpecs) // Amedo 2026-09-28
        {
            AnimatedObject.SlotGroup model;
            if (defaultGroup is not null && ReferenceEquals(modelOgi, defaultOgi))
                model = new AnimatedObject.SlotGroup { Ogi = modelOgi, MeshRoot = defaultGroup.MeshRoot };
            else
            {
                anyAlt = true;
                var modelRoot = NewChild(e, $"{e.Name}_model{index}");
                modelRoot.Active = false;
                var (attached, morphs) = EmitOgiMeshEntities(gl, tables, modelRoot, BuildOgiMeshes(tables, modelOgi), modelOgi, animObj);
                model = new AnimatedObject.SlotGroup { Ogi = modelOgi, MeshRoot = modelRoot };
                model.AttachedRigidParts.AddRange(attached);
                model.MorphMeshes.AddRange(morphs);
            }
            model.SlotIndex = (ushort)index;
            animObj.ModelGroups.Add(model);
        }
        return (hadMesh, hadAnim, anyAlt);
    }

    private static Entity NewChild(Entity parent, string name)
    {
        var child = new Entity(name);
        parent.AddChild(child);
        return child;
    }

    public static List<SurfaceType>? BuildSurfaceTypeLookup(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2 = null)
    {
        List<SurfaceType>? TryFind(PS2AnyTwinsanityRM2 source)
        {
            for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = source.GetItem<BaseTwinSection>((uint)lid);
                var surfSec = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_SURFACES_SECTION);
                if (surfSec is null || surfSec.GetItemsAmount() == 0) continue;

                var list = new List<SurfaceType>(surfSec.GetItemsAmount());
                for (int i = 0; i < surfSec.GetItemsAmount(); i++)
                    list.Add(surfSec.GetItem(i) is PS2AnyCollisionSurface s ? s.SurfaceId : SurfaceType.SURF_DEFAULT);
                return list;
            }
            return null;
        }

        return TryFind(rm2) ?? (globalRm2 is not null ? TryFind(globalRm2) : null);
    }

    private static readonly SysVec4 CollisionFallbackColor = new(1f, 0.15f, 0.85f, 0.92f);

    public static SysVec4 SurfaceDebugColor(SurfaceType st) => st switch
    {
        SurfaceType.SURF_NORMAL_SAND  => new SysVec4(1f, 0.85f, 0.1f, 0.92f),
        SurfaceType.SURF_NORMAL_GRASS => new SysVec4(0.15f, 0.8f, 0.2f, 0.92f),
        SurfaceType.SURF_NORMAL_WATER => new SysVec4(0.15f, 0.4f, 0.95f, 0.92f),
        SurfaceType.SURF_NORMAL_WOOD  => new SysVec4(0.72f, 0.52f, 0.3f, 0.92f),
        SurfaceType.SURF_NORMAL_METAL or SurfaceType.SURF_SLIPPY_METAL
            => new SysVec4(0.22f, 0.22f, 0.26f, 0.92f),
        SurfaceType.SURF_NORMAL_ROCK or SurfaceType.SURF_SLIPPY_ROCK
            or SurfaceType.SURF_NORMAL_STONE_TILES
            => new SysVec4(0.32f, 0.20f, 0.10f, 0.92f),
        SurfaceType.SURF_NORMAL_MUD   => new SysVec4(0.35f, 0.25f, 0.12f, 0.92f),
        SurfaceType.SURF_NORMAL_SNOW or SurfaceType.SURF_STICKY_SNOW
            => new SysVec4(0.9f, 0.9f, 0.95f, 0.92f),
        SurfaceType.SURF_ICE or SurfaceType.SURF_ICE_LOW_SLIPPY
            => new SysVec4(0.6f, 0.85f, 0.95f, 0.92f),
        SurfaceType.SURF_BLOCK_PLAYER or SurfaceType.SURF_BLOCK_AI_ONLY
            or SurfaceType.SURF_CAMERA_BLOCKING
            or SurfaceType.SURF_GENERIC_MEDIUM_SLIPPY_RIGID_ONLY
            => new SysVec4(0.55f, 0.55f, 0.55f, 0.92f),
        SurfaceType.SURF_GLASS_WALL
            => new SysVec4(0.5f, 0.3f, 0.12f, 0.92f),
        SurfaceType.SURF_LAVA or SurfaceType.SURF_GENERIC_INSTANT_DEATH
            or SurfaceType.SURF_FALL_THRU_DEATH or SurfaceType.SURF_DROWNING_PLANE
            or SurfaceType.SURF_NONSOLID_ELECTRIC_DEATH
            => new SysVec4(1f, 0.1f, 0.1f, 0.92f),
        _ => CollisionFallbackColor,
    };

    // Amedo 2026-11-20
    private static readonly SysVec3 CollisionLightDir = GameMath.Normalize(new SysVec3(0.35f, 1f, 0.25f));
    private const float CollisionEdgeLift = -0.1f;
    private const float CollisionEdgeDarken = 0.35f;

    // Amedo 2026-11-20
    private static SysVec4 ShadeCollision(SysVec4 color, SysVec3 normal)
    {
        float light = 0.5f + 0.5f * MathF.Abs(GameMath.Dot(normal, CollisionLightDir));
        return new SysVec4(color.X * light, color.Y * light, color.Z * light, 1f);
    }

    public sealed record CollisionView(GpuMesh Mesh, Material Mat, GpuMesh? Edges, Material? EdgeMat); // Amedo 2026-11-20

    public static CollisionView? DecodeCollision(GL gl,
        Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData coll,
        IReadOnlyList<SurfaceType>? surfaceTypes = null)
    {
        if (coll.Triangles.Count == 0 || coll.Vectors.Count == 0) return null;

        var verts = new List<GpuMesh.Vertex>(coll.Triangles.Count * 3);
        var edges = new Dictionary<(int, int), (SysVec3 Normal, SysVec4 Color)>(); // Amedo 2026-11-20

        foreach (var tri in coll.Triangles)
        {
            if (tri.Vector1Index < 0 || tri.Vector2Index < 0 || tri.Vector3Index < 0 ||
                tri.Vector1Index >= coll.Vectors.Count ||
                tri.Vector2Index >= coll.Vectors.Count ||
                tri.Vector3Index >= coll.Vectors.Count)
                continue;

            var debugColor = CollisionFallbackColor;
            if (surfaceTypes is not null && tri.SurfaceIndex >= 0 && tri.SurfaceIndex < surfaceTypes.Count)
                debugColor = SurfaceDebugColor(surfaceTypes[tri.SurfaceIndex]);

            var a = coll.Vectors[tri.Vector1Index];
            var b = coll.Vectors[tri.Vector2Index];
            var c = coll.Vectors[tri.Vector3Index];
            var pa = new SysVec3(a.X, a.Y, a.Z);
            var pb = new SysVec3(b.X, b.Y, b.Z);
            var pc = new SysVec3(c.X, c.Y, c.Z);

            var normal = SysVec3.Cross(pb - pa, pc - pa);
            normal = GameMath.LengthSquared(normal) > 1e-12f ? SysVec3.Normalize(normal) : SysVec3.UnitY;

            var shaded = ShadeCollision(debugColor, normal); // Amedo 2026-11-20
            verts.Add(new GpuMesh.Vertex { Position = pa, Normal = normal, Color = shaded });
            verts.Add(new GpuMesh.Vertex { Position = pb, Normal = normal, Color = shaded });
            verts.Add(new GpuMesh.Vertex { Position = pc, Normal = normal, Color = shaded });
            AddCollisionEdge(edges, tri.Vector1Index, tri.Vector2Index, normal, shaded); // Amedo 2026-11-20
            AddCollisionEdge(edges, tri.Vector2Index, tri.Vector3Index, normal, shaded);
            AddCollisionEdge(edges, tri.Vector3Index, tri.Vector1Index, normal, shaded);
        }
        if (verts.Count < 3) return null;

        var idx  = Enumerable.Range(0, verts.Count).Select(i => (uint)i).ToArray();
        var mesh = GpuMesh.FromOwned(gl, verts.ToArray(), idx); // Amedo 2026-09-29

        var localCenter = verts.Aggregate(SysVec3.Zero, (acc, v) => acc + v.Position) / verts.Count;
        float radius = verts.Max(v => GameMath.Distance(v.Position, localCenter)); // Amedo 2026-11-20
        var mat = new Material
        {
            Culling         = Material.CullMode.Both,
            DoubleColor     = 1f,
            AlphaBlend      = false, // Amedo 2026-11-20
            DepthWrite      = true, // Amedo 2026-11-20
            AlwaysOnTop     = true, // Amedo 2026-11-20
            FogEnabled      = false,
            ShaderType      = "StandardUnlit",
            LocalCenter     = localCenter,
            BoundingRadius  = radius,
        };

        // Amedo 2026-11-20
        var lines = new GpuMesh.Vertex[edges.Count * 2];
        int n = 0;
        foreach (var ((i, j), (sum, color)) in edges)
        {
            var lift = (GameMath.LengthSquared(sum) > 1e-12f ? GameMath.Normalize(sum) : SysVec3.UnitY) * CollisionEdgeLift;
            var edgeColor = new SysVec4(color.X * CollisionEdgeDarken, color.Y * CollisionEdgeDarken, color.Z * CollisionEdgeDarken, 1f);
            var p = coll.Vectors[i];
            var q = coll.Vectors[j];
            lines[n++] = new GpuMesh.Vertex { Position = new SysVec3(p.X, p.Y, p.Z) + lift, Normal = SysVec3.UnitY, Color = edgeColor };
            lines[n++] = new GpuMesh.Vertex { Position = new SysVec3(q.X, q.Y, q.Z) + lift, Normal = SysVec3.UnitY, Color = edgeColor };
        }
        GpuMesh? edgeMesh = n > 0 ? new GpuMesh(gl, lines, PrimitiveType.Lines) : null;
        var edgeMat = edgeMesh is null ? null : new Material
        {
            Culling        = Material.CullMode.Both,
            DoubleColor    = 1f,
            AlwaysOnTop    = true,
            FogEnabled     = false,
            ShaderType     = "StandardUnlit",
            LocalCenter    = localCenter,
            BoundingRadius = radius,
        };
        return new CollisionView(mesh, mat, edgeMesh, edgeMat);
    }

    // Amedo 2026-11-20
    private static void AddCollisionEdge(Dictionary<(int, int), (SysVec3 Normal, SysVec4 Color)> edges, int a, int b, SysVec3 normal, SysVec4 color)
    {
        var key = a < b ? (a, b) : (b, a);
        edges[key] = edges.TryGetValue(key, out var e) ? (e.Normal + normal, e.Color) : (normal, color);
    }

    public static object BuildSceneryMeshes(GL gl,
                                          PS2AnyTwinsanitySM2 sm2,
                                          Entity sceneryEnt,
                                          Dictionary<uint, Texture2D> texCache,
                                          bool applyGlobalEffects = true)
    {
        var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        Console.WriteLine($"[MeshDecoder] SM2 gfx section: {(gfx is null ? "NULL" : "OK")}");
        if (gfx is null) return new Dictionary<uint, List<(GpuMesh, Material)>>();

        var modelById = BuildModelLookup(gfx);
        Console.WriteLine($"[MeshDecoder] SM2 models: {modelById.Count}");
        var meshById  = BuildMeshLookup(gfx, modelById, gl, texCache);
        Console.WriteLine($"[MeshDecoder] SM2 decoded gpu meshes: {meshById.Count}");

        if (applyGlobalEffects)
        {
            SetGlobalLook(sm2); // Amedo 2026-11-09
            RenderPipeline.SkydomeDraws.Clear();
            AddSkydomeDraws(gfx, meshById);
        }
        else if (gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKYDOMES_SECTION) is { } ownSky) // Amedo 2026-09-29
        {
            var set = sceneryEnt.Get<SkydomeSet>() ?? sceneryEnt.Add(new SkydomeSet());
            set.Draws.Clear();
            for (int i = 0; i < ownSky.GetItemsAmount(); i++)
            {
                if (ownSky.GetItem(i) is not PS2AnySkydome sky) continue;
                var draws = new List<(GpuMesh, Material)>();
                foreach (var meshId in sky.Meshes)
                {
                    if (!meshById.TryGetValue(meshId, out var entry)) continue;
                    foreach (var (gpuMesh, mat) in entry)
                    {
                        mat.FogEnabled = true;
                        mat.DepthWrite = false;
                        draws.Add((gpuMesh, mat));
                    }
                }
                set.Draws[sky.GetID()] = draws;
            }
        }

        var dyn =sm2.GetItem<PS2AnyDynamicScenery>((uint)Constants.SCENERY_DYNAMIC_SECENERY_ITEM);
        if (dyn is not null)
        {
            var dynEnt = new Entity("DynamicScenery");
            int placed = 0;
            foreach (var model in dyn.DynamicModels)
            {
                if (!meshById.TryGetValue(model.MeshID, out var entry)) continue;
                var ent = new Entity($"dyn_{model.MeshID:X8}");
                var animator = new DynamicSceneryAnimator(model.Animation, model.AnimatedFrames) { Model = model }; // Amedo 2026-10-06
                ent.Add(animator);
                animator.Apply();
                foreach (var (gpuMesh, mat) in entry)
                {
                    var child = new Entity($"dynm_{model.MeshID:X8}");
                    child.Add(new MeshRenderer { Mesh = gpuMesh, Material = mat });
                    ent.AddChild(child);
                }
                dynEnt.AddChild(ent);
                placed++;
            }
            sceneryEnt.AddChild(dynEnt);
            Console.WriteLine($"[MeshDecoder] Dynamic scenery: {placed}/{dyn.DynamicModels.Count} models placed");
        }

        var lodById = new Dictionary<uint, PS2AnyLOD>();
        var lodSec  = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_LODS_SECTION);
        if (lodSec is not null)
            for (int i = 0; i < lodSec.GetItemsAmount(); i++)
                if (lodSec.GetItem(i) is PS2AnyLOD lod && lodSec.GetItem(i) is BaseTwinItem bi)
                    lodById[bi.GetID()] = lod;

        var scenery = sm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        Console.WriteLine($"[MeshDecoder] SM2 scenery: {(scenery is null ? "NULL" : $"{scenery.Sceneries.Count} nodes")}  LODs: {lodById.Count}");
        if (scenery is null) return meshById;

        int idx = 0, lodPlaced = 0;

        void Place(List<(GpuMesh, Material)> entry, TwinMat4 mat, string src, uint srcId,
                   TwinSceneryBaseType node, bool isLod, PS2AnyLOD? lod = null)
        {
            var meshEnt = new Entity($"scenery_{idx++}_{src}{srcId:X8}");
            meshEnt.Add(new SceneryTile { Source = mat, Node = node, SourceId = srcId, IsLod = isLod });
            meshEnt.Transform.LocalMatrix = TwinMatToSys(mat);
            sceneryEnt.AddChild(meshEnt);
            if (lod is not null && SceneryLod.Original)
            {
                SceneryLod.Attach(meshEnt, lod, meshById);
                return;
            }
            foreach (var (gpuMesh, m) in entry)
            {
                var child = new Entity($"sm_{idx}");
                child.Add(new MeshRenderer { Mesh = gpuMesh, Material = m });
                meshEnt.AddChild(child);
            }
        }

        foreach (var node in scenery.Sceneries)
        {
            for (int i = 0; i < node.MeshIDs.Count; i++)
                if (meshById.TryGetValue(node.MeshIDs[i], out var entry))
                    Place(entry, node.MeshModelMatrices[i], "mesh", node.MeshIDs[i], node, isLod: false);

            for (int i = 0; i < node.LodIDs.Count; i++)
            {
                if (!lodById.TryGetValue(node.LodIDs[i], out var lod) || lod.Meshes.Count == 0) continue;
                if (!meshById.TryGetValue(lod.Meshes[0], out var entry)) continue;
                Place(entry, node.LodModelMatrices[i], "lod", node.LodIDs[i], node, isLod: true, lod);
                lodPlaced++;
            }
        }
        Console.WriteLine($"[MeshDecoder] Scenery placed: {idx} meshes ({lodPlaced} via LODs)");
        return new SceneryTables { MeshById = meshById, LodById = lodById };
    }

    private sealed class SceneryTables
    {
        public required Dictionary<uint, List<(GpuMesh, Material)>> MeshById;
        public required Dictionary<uint, PS2AnyLOD> LodById;
    }

    public static uint? ResolveSceneryMeshId(object tablesHandle, SceneryTile tile)
    {
        if (tablesHandle is not SceneryTables tables) return null;
        if (!tile.IsLod) return tile.SourceId;
        return tables.LodById.TryGetValue(tile.SourceId, out var lod) && lod.Meshes.Count > 0 ? lod.Meshes[0] : null;
    }

    public static void BuildMeshForSceneryTile(GL gl, object tablesHandle, Entity e)
    {
        if (tablesHandle is not SceneryTables tables) return;
        var tile = e.Get<SceneryTile>();
        if (tile is null) return;

        uint meshId = tile.SourceId;
        if (tile.IsLod)
        {
            if (!tables.LodById.TryGetValue(tile.SourceId, out var lod) || lod.Meshes.Count == 0) return;
            meshId = lod.Meshes[0];
        }
        if (!tables.MeshById.TryGetValue(meshId, out var entry)) return;

        int i = 0;
        foreach (var (gpuMesh, m) in entry)
        {
            var child = new Entity($"{e.Name}_sm_{i++}");
            child.Add(new MeshRenderer { Mesh = gpuMesh, Material = m });
            e.AddChild(child);
        }
    }


    private sealed class RmTables
    {
        public Dictionary<uint, PS2AnyObject>      Objects      = new();
        public Dictionary<uint, PS2AnyOGI>         OGIs         = new();
        public Dictionary<uint, PS2AnyRigidModel>  RigidModels  = new();
        public Dictionary<uint, PS2AnyAnimation>   Animations   = new();
        public Dictionary<uint, ITwinModel>        Models       = new();
        public Dictionary<uint, List<(GpuMesh, Material)>> ModelGpu     = new();
        public Dictionary<uint, List<(GpuMesh, Material)>> SkinGpu      = new();
        public Dictionary<uint, List<(GpuMesh, Material)>> BlendSkinGpu = new();

        public Dictionary<uint, (Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin Blend,
            List<(Texture2D? Tex, TwinShader? Shader)> SubMats)> BlendSkinRaw = new();

        public RmTables? GlobalPart; // Amedo 2026-09-29
        public PS2AnyTwinsanityRM2? GlobalSource;
        public HashSet<uint>? GlobalMissingTextures;
        public bool GlobalShareable;
    }

    private static RmTables BuildLookupTables(GL gl,
                                              PS2AnyTwinsanityRM2 rm2,
                                              Dictionary<uint, Texture2D> texCache,
                                              PS2AnyTwinsanityRM2? globalRm2 = null,
                                              ICollection<uint>? globalTextureIds = null, // Amedo 2026-09-29
                                              RmTables? shared = null)
    {
        var tables = new RmTables();
        if (globalRm2 is not null) // Amedo 2026-09-29
        {
            if (shared is { GlobalPart: { } part, GlobalShareable: true, GlobalMissingTextures: { } missing } &&
                ReferenceEquals(shared.GlobalSource, globalRm2) && !missing.Overlaps(texCache.Keys))
            {
                Console.WriteLine("[MeshDecoder] Default.rm2 models: shared (decoded once)");
            }
            else
            {
                part = new RmTables();
                AddRm2ToTables(gl, globalRm2, texCache, part);
                missing = ReferencedTextures(globalRm2);
                if (globalTextureIds is not null) missing.ExceptWith(globalTextureIds);
                bool shareable = globalTextureIds is not null && !missing.Overlaps(texCache.Keys);
                Console.WriteLine($"[MeshDecoder] Default.rm2 models: decoded ({missing.Count} texture ids missing from Default.rm2, shareable {shareable})");
                shared = new RmTables { GlobalPart = part, GlobalSource = globalRm2, GlobalMissingTextures = missing, GlobalShareable = shareable };
            }
            tables.GlobalPart = shared.GlobalPart;
            tables.GlobalSource = shared.GlobalSource;
            tables.GlobalMissingTextures = shared.GlobalMissingTextures;
            tables.GlobalShareable = shared.GlobalShareable;
            CopyTables(part, tables);
        }
        AddRm2ToTables(gl, rm2, texCache, tables);
        Console.WriteLine($"[MeshDecoder] Skins decoded: {tables.SkinGpu.Count}");
        return tables;
    }

    // Amedo 2026-09-29
    private static void CopyTables(RmTables from, RmTables to)
    {
        foreach (var kv in from.Objects) to.Objects[kv.Key] = kv.Value;
        foreach (var kv in from.OGIs) to.OGIs[kv.Key] = kv.Value;
        foreach (var kv in from.RigidModels) to.RigidModels[kv.Key] = kv.Value;
        foreach (var kv in from.Animations) to.Animations[kv.Key] = kv.Value;
        foreach (var kv in from.Models) to.Models[kv.Key] = kv.Value;
        foreach (var kv in from.ModelGpu) to.ModelGpu[kv.Key] = kv.Value;
        foreach (var kv in from.SkinGpu) to.SkinGpu[kv.Key] = kv.Value;
        foreach (var kv in from.BlendSkinGpu) to.BlendSkinGpu[kv.Key] = kv.Value;
        foreach (var kv in from.BlendSkinRaw) to.BlendSkinRaw[kv.Key] = kv.Value;
    }

    // Amedo 2026-09-29
    private static HashSet<uint> ReferencedTextures(PS2AnyTwinsanityRM2 rm2)
    {
        var ids = new HashSet<uint>();
        if (rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION) is { } gfx)
            foreach (var mat in BuildMaterialLookup(gfx).Values)
                foreach (var sh in mat.Shaders) ids.Add(sh.TextureId);
        return ids;
    }

    private static void AddRm2ToTables(GL gl, PS2AnyTwinsanityRM2 rm2,
                                       Dictionary<uint, Texture2D> texCache, RmTables tables)
    {
        var code = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        if (code is not null)
        {
            FillSection(code, (uint)Constants.CODE_GAME_OBJECTS_SECTION,
                        tables.Objects,  i => i is PS2AnyObject o ? o : null);
            FillSection(code, (uint)Constants.CODE_OGIS_SECTION,
                        tables.OGIs, i => i is PS2AnyOGI o ? o : null);
            FillSection(code, (uint)Constants.CODE_ANIMATIONS_SECTION,
                        tables.Animations, i => i is PS2AnyAnimation a ? a : null);
        }

        var gfx = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        if (gfx is not null)
        {
            FillSection(gfx, (uint)Constants.GRAPHICS_RIGID_MODELS_SECTION,
                        tables.RigidModels, i => i is PS2AnyRigidModel r ? r : null);

            var modelById = BuildModelLookup(gfx);
            foreach (var kv in modelById) tables.Models[kv.Key] = kv.Value;
            foreach (var kv in BuildMeshLookup(gfx, modelById, gl, texCache, tables.GlobalSource)) tables.ModelGpu[kv.Key]     = kv.Value; // Amedo
            foreach (var kv in BuildSkinLookup(gfx, gl, texCache))            tables.SkinGpu[kv.Key]      = kv.Value;
            foreach (var kv in BuildBlendSkinLookup(gfx, gl, texCache, tables.BlendSkinRaw)) tables.BlendSkinGpu[kv.Key] = kv.Value;
        }
    }

    private static Dictionary<uint, List<(GpuMesh, Material)>> BuildBlendSkinLookup(
        PS2AnyGraphicsSection gfx, GL gl, Dictionary<uint, Texture2D> texCache,
        Dictionary<uint, (Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin Blend,
            List<(Texture2D? Tex, TwinShader? Shader)> SubMats)> rawOut)
    {
        var result        = new Dictionary<uint, List<(GpuMesh, Material)>>();
        var materialsById = BuildMaterialLookup(gfx);

        var sec = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        if (sec is null) return result;

        for (int i = 0; i < sec.GetItemsAmount(); i++)
        {
            if (sec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin blend) continue;
            if (sec.GetItem(i) is not BaseTwinItem bi) continue;

            var subMats = new List<(Texture2D?, TwinShader?)>();
            foreach (var sb in blend.SubBlends)
                subMats.Add(ResolveMaterial(sb.Material, materialsById, texCache));

            var gpu = DecodeBlendSkin(gl, blend, subMats);
            if (gpu.Count > 0) result[bi.GetID()] = gpu;
            rawOut[bi.GetID()] = (blend, subMats);
        }
        return result;
    }

    private static Dictionary<uint, List<(GpuMesh, Material)>> BuildSkinLookup(
        PS2AnyGraphicsSection gfx, GL gl, Dictionary<uint, Texture2D> texCache)
    {
        var result        = new Dictionary<uint, List<(GpuMesh, Material)>>();
        var materialsById = BuildMaterialLookup(gfx);

        var skinSec = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        if (skinSec is null) return result;

        for (int i = 0; i < skinSec.GetItemsAmount(); i++)
        {
            if (skinSec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinSkin skin) continue;
            if (skinSec.GetItem(i) is not BaseTwinItem bi) continue;

            var subMats = new List<(Texture2D?, TwinShader?)>();
            foreach (var ss in skin.SubSkins)
                subMats.Add(ResolveMaterial(ss.Material, materialsById, texCache));

            var gpu = DecodeSkin(gl, skin, subMats);
            if (gpu.Count > 0) result[bi.GetID()] = gpu;
        }
        return result;
    }

    private static (Texture2D?, TwinShader?) ResolveMaterial(
        uint matId, Dictionary<uint, PS2AnyMaterial> materialsById, Dictionary<uint, Texture2D> texCache)
    {
        Texture2D?  tex    = null;
        TwinShader? shader = null;
        if (materialsById.TryGetValue(matId, out var mat))
        {
            foreach (var sh in mat.Shaders)
            {
                shader ??= sh;
                if (texCache.TryGetValue(sh.TextureId, out tex) && tex is not null) break;
            }
            RegisterUpperLayers(mat, texCache); // Amedo 2026-09-26
        }
        return (tex, shader);
    }

    private static void FillSection<T>(BaseTwinSection parent, uint subId,
                                        Dictionary<uint, T> dict,
                                        Func<object, T?> cast) where T : class
    {
        var sec = parent.GetItem<BaseTwinSection>(subId);
        if (sec is null) return;
        for (int i = 0; i < sec.GetItemsAmount(); i++)
        {
            var item = sec.GetItem(i);
            var t    = cast(item);
            if (t is not null && item is BaseTwinItem bi)
                dict[bi.GetID()] = t;
        }
    }


    private static Dictionary<uint, ITwinModel> BuildModelLookup(PS2AnyGraphicsSection gfx)
    {
        var dict = new Dictionary<uint, ITwinModel>();
        var sec  = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        if (sec is null) return dict;
        for (int i = 0; i < sec.GetItemsAmount(); i++)
        {
            if (sec.GetItem(i) is ITwinModel m && sec.GetItem(i) is BaseTwinItem bi)
                dict[bi.GetID()] = m;
        }
        return dict;
    }

    private static Dictionary<uint, PS2AnyMaterial> BuildMaterialLookup(PS2AnyGraphicsSection gfx)
    {
        var dict = new Dictionary<uint, PS2AnyMaterial>();
        FillSection(gfx, (uint)Constants.GRAPHICS_MATERIALS_SECTION,
                    dict, i => i is PS2AnyMaterial m ? m : null);
        Console.WriteLine($"[MeshDecoder] Materials: {dict.Count}");
        return dict;
    }

    private static List<(Texture2D? Tex, TwinShader? Shader)> SubMaterials(List<uint> materialIds,
        Dictionary<uint, PS2AnyMaterial> materialsById, Dictionary<uint, Texture2D> texCache)
    {
        var subMats = new List<(Texture2D? Tex, TwinShader? Shader)>();
        foreach (var matId in materialIds)
        {
            Texture2D? tex = null;
            TwinShader? shader = null;
            if (materialsById.TryGetValue(matId, out var mat))
            {
                foreach (var sh in mat.Shaders)
                {
                    shader ??= sh;
                    if (texCache.TryGetValue(sh.TextureId, out tex) && tex is not null)
                        break;
                }
                RegisterUpperLayers(mat, texCache);
            }
            subMats.Add((tex, shader));
        }
        return subMats;
    }

    private static Dictionary<uint, List<(GpuMesh, Material)>> BuildMeshLookup(
        PS2AnyGraphicsSection gfx,
        Dictionary<uint, ITwinModel> modelById,
        GL gl,
        Dictionary<uint, Texture2D> texCache,
        PS2AnyTwinsanityRM2? globalRm2 = null) // Amedo
    {
        var result = new Dictionary<uint, List<(GpuMesh, Material)>>();
        var materialsById = BuildMaterialLookup(gfx);
        if (globalRm2?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION) is { } globalGfx && !ReferenceEquals(globalGfx, gfx)) // Amedo
            foreach (var kv in BuildMaterialLookup(globalGfx)) materialsById.TryAdd(kv.Key, kv.Value);

        DecodeMeshSection(gfx, (uint)Constants.GRAPHICS_MESHES_SECTION,
                          modelById, gl, texCache, materialsById, result);

        DecodeMeshSection(gfx, (uint)Constants.GRAPHICS_RIGID_MODELS_SECTION,
                          modelById, gl, texCache, materialsById, result);

        return result;
    }

    private static void DecodeMeshSection(PS2AnyGraphicsSection gfx,
                                           uint sectionId,
                                           Dictionary<uint, ITwinModel> modelById,
                                           GL gl,
                                           Dictionary<uint, Texture2D> texCache,
                                           Dictionary<uint, PS2AnyMaterial> materialsById,
                                           Dictionary<uint, List<(GpuMesh, Material)>> result)
    {
        var sec = gfx.GetItem<BaseTwinSection>(sectionId);
        if (sec is null) return;

        for (int i = 0; i < sec.GetItemsAmount(); i++)
        {
            if (sec.GetItem(i) is not PS2AnyRigidModel rm) continue;
            if (sec.GetItem(i) is not BaseTwinItem bi) continue;

            if (!modelById.TryGetValue(rm.Model, out var model)) continue;

            var subMats = SubMaterials(rm.Materials, materialsById, texCache);

            var gpuList = DecodeModel(gl, model, subMats);
            if (gpuList.Count > 0)
                result[bi.GetID()] = gpuList;
        }
    }


    private static List<(GpuMesh Mesh, Material Mat, Matrix4x4 JointLocal, bool IsSkinned, bool IsBlendSkin, int JointIndex)> BuildOgiMeshes(
        RmTables tables, PS2AnyOGI ogi)
    {
        var result = new List<(GpuMesh, Material, Matrix4x4, bool, bool, int)>();

        var jointWorld = ComputeJointWorlds(ogi);

        for (int i = 0; i < ogi.RigidModelIds.Count; i++)
        {
            var rmId = ogi.RigidModelIds[i];
            if (!tables.ModelGpu.TryGetValue(rmId, out var meshes)) continue;

            int jointIdx = i < ogi.JointIndices.Count ? ogi.JointIndices[i] : 0;
            var m = jointWorld.TryGetValue(jointIdx, out var jw) ? jw : Matrix4x4.Identity;

            foreach (var (mesh, mat) in meshes)
                result.Add((mesh, mat, m, false, false, jointIdx));
        }

        if (ogi.SkinID != 0 && tables.SkinGpu.TryGetValue(ogi.SkinID, out var skinMeshes))
            foreach (var (mesh, mat) in skinMeshes)
                result.Add((mesh, mat, Matrix4x4.Identity, true, false, -1));

        if (ogi.BlendSkinID != 0 && tables.BlendSkinGpu.TryGetValue(ogi.BlendSkinID, out var blendMeshes))
            foreach (var (mesh, mat) in blendMeshes)
                result.Add((mesh, mat, Matrix4x4.Identity, true, true, -1)); // Amedo

        return result;
    }

    private static PS2AnyOGI? ResolveDefaultOgi(RmTables tables, PS2AnyObject obj,
        out List<(GpuMesh, Material, Matrix4x4, bool, bool, int)> parts)
    {
        foreach (var ogiSlot in obj.OGISlots)
        {
            if (!tables.OGIs.TryGetValue(ogiSlot, out var ogi)) continue;
            var built = BuildOgiMeshes(tables, ogi);
            if (built.Count > 0) { parts = built; return ogi; }
        }
        parts = new();
        return null;
    }

    private static (List<(Entity Child, int JointIndex, Matrix4x4 BindLocal)> AttachedRigidParts,
                    List<MorphMesh> MorphMeshes) EmitOgiMeshEntities(
        GL gl, RmTables tables, Entity parent,
        List<(GpuMesh Mesh, Material Mat, Matrix4x4 JointLocal, bool IsSkinned, bool IsBlendSkin, int JointIndex)> parts,
        PS2AnyOGI ogi, AnimatedObject? animObj)
    {
        var attached = new List<(Entity, int, Matrix4x4)>();
        var morphs   = new List<MorphMesh>();

        foreach (var (gpuMesh, mat, jointLocal, isSkinned, isBlendSkin, jointIndex) in parts)
        {
            if (isBlendSkin && animObj is not null) continue;

            var child = new Entity($"{parent.Name}_mesh");
            child.Transform.LocalMatrix = jointLocal;
            var mr = child.Add(new MeshRenderer { Mesh = gpuMesh, Material = mat, IsSkinned = isSkinned });
            if (isSkinned) mr.Skeleton = animObj;
            else if (animObj is not null && jointIndex >= 0)
                attached.Add((child, jointIndex, jointLocal));
            parent.AddChild(child);
        }

        if (animObj is not null && ogi.BlendSkinID != 0 &&
            tables.BlendSkinRaw.TryGetValue(ogi.BlendSkinID, out var raw))
        {
            foreach (var mm in DecodeBlendSkinMorph(gl, raw.Blend, raw.SubMats))
            {
                morphs.Add(mm); // Amedo

                var child = new Entity($"{parent.Name}_face");
                var mr = child.Add(new MeshRenderer { Mesh = mm.Mesh, Material = mm.Mat, IsSkinned = true });
                mr.Skeleton = animObj;
                parent.AddChild(child);
            }
        }

        return (attached, morphs);
    }
}
