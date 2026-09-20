using CrashEngine.Core;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using Silk.NET.OpenGL;
using SysVec3 = System.Numerics.Vector3;
using System.Numerics;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;

namespace CrashEngine.Importer;

public static class ChunkImporter
{
    private const float INV_SCALE = 1f;

    public static Entity LoadChunk(GL gl, PackageReader pkg,
                                   string rm2Path, string sm2Path,
                                   out ScriptDumper.ChunkScripts scripts)
    {
        var root = new Entity(System.IO.Path.GetFileNameWithoutExtension(rm2Path));
        root.Transform.Scale = new SysVec3(-1f, 1f, 1f);

        var rm2 = new PS2AnyTwinsanityRM2();
        using (var stream = pkg.OpenByPath(rm2Path)
               ?? throw new FileNotFoundException($"RM2 not found: {rm2Path}"))
        using (var reader = new BinaryReader(stream))
            rm2.Read(reader, (int)stream.Length);

        PS2AnyTwinsanitySM2? sm2 = null;
        var sm2Stream = pkg.OpenByPath(sm2Path);
        if (sm2Stream is not null)
        {
            sm2 = new PS2AnyTwinsanitySM2();
            using var reader = new BinaryReader(sm2Stream);
            sm2.Read(reader, (int)sm2Stream.Length);
        }

        var texCache = BuildTextures(gl, rm2);

        PS2AnyTwinsanityRM2? globalRm2 = null;
        var globalTexCache = new Dictionary<uint, Texture2D>();
        var defStream = pkg.OpenByPath(@"Startup\Default.rm2");
        if (defStream is not null)
        {
            var def = new PS2Default();
            using (var reader = new BinaryReader(defStream))
                def.Read(reader, (int)defStream.Length);
            globalRm2 = def;
            globalTexCache = BuildTextures(gl, def);
            foreach (var kv in globalTexCache)
                texCache[kv.Key] = kv.Value;
        }

        var pathMap = ExtractPaths(rm2);
        ImportInstances(rm2, root, pathMap);

        var instRoot = root.Children.FirstOrDefault(c => c.Name == "Instances");
        object? meshTables = null;
        if (instRoot is not null)
            meshTables = MeshDecoder.BuildInstanceMeshes(gl, rm2, instRoot, texCache, globalRm2);

        object? sceneryTables = null;
        var sm2Tex = new Dictionary<uint, Texture2D>();
        if (sm2 is not null)
        {
            var sceneryEnt = new Entity("Scenery");
            root.AddChild(sceneryEnt);
            sm2Tex = BuildSm2Textures(gl, sm2);
            sceneryTables = MeshDecoder.BuildSceneryMeshes(gl, sm2, sceneryEnt, sm2Tex);

            BuildLinkedScenery(gl, pkg, sm2, root);
        }

        var collData = rm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData>(
            (uint)Constants.LEVEL_COLLISION_ITEM);
        var surfaceTypes = MeshDecoder.BuildSurfaceTypeLookup(rm2, globalRm2);
        if (collData is not null && MeshDecoder.DecodeCollision(gl, collData, surfaceTypes) is { } collision)
        {
            var collRoot = new Entity("Collision") { Active = false };
            collRoot.Add(new CollisionMesh());
            root.AddChild(collRoot);
            var collChild = new Entity("collision_mesh");
            collChild.Add(new MeshRenderer { Mesh = collision.Mesh, Material = collision.Mat });
            collRoot.AddChild(collChild);
        }

        scripts = ScriptDumper.DumpChunk(rm2);

        root.Add(new ChunkSource
        {
            Rm2        = rm2,
            Sm2        = sm2,
            Rm2Path    = rm2Path,
            Sm2Path    = sm2Path,
            GlobalRm2     = globalRm2,
            TexCache      = texCache,
            GlobalTexCache = globalTexCache,
            SceneryTexCache = sm2Tex,
            MeshTables    = meshTables,
            SceneryTables = sceneryTables,
        });

        return root;
    }

    private static void BuildLinkedScenery(GL gl, PackageReader pkg, PS2AnyTwinsanitySM2 sm2, Entity root)
    {
        var linkItem = sm2.GetItem<PS2AnyLink>((uint)Constants.SCENERY_LINK_ITEM);
        if (linkItem is null) return;

        var linksRoot = new Entity("LinkedScenery");
        root.AddChild(linksRoot);
        if (linkItem.LinksList.Count == 0) return;

        int placed = 0;
        foreach (var link in linkItem.LinksList)
        {
            var linkEnt = new Entity($"Link_{System.IO.Path.GetFileName(link.Path)}");
            linkEnt.Transform.LocalMatrix = MeshDecoder.TwinMatToSys(link.ChunkMatrix);
            linkEnt.Add(new LinkedSceneryLink { Source = link });
            linksRoot.AddChild(linkEnt);

            if (!link.IsRendered) continue;

            using var linkedStream = pkg.OpenByPath($"{link.Path}.sm2");
            if (linkedStream is null)
            {
                Console.WriteLine($"[ChunkImporter] Linked chunk not found: {link.Path}.sm2");
                continue;
            }

            var linkedSm2 = new PS2AnyTwinsanitySM2();
            using (var reader = new BinaryReader(linkedStream))
                linkedSm2.Read(reader, (int)linkedStream.Length);

            var linkedTex = BuildSm2Textures(gl, linkedSm2);
            MeshDecoder.BuildSceneryMeshes(gl, linkedSm2, linkEnt, linkedTex, applyGlobalEffects: false);
            placed++;
        }
        Console.WriteLine($"[ChunkImporter] Linked scenery previews placed: {placed}/{linkItem.LinksList.Count} (all {linkItem.LinksList.Count} selectable in Hierarchy for retargeting)");
    }

    public static bool RetargetLinkedScenery(GL gl, PackageReader pkg, Entity linkEnt, string newPath, out string log)
    {
        var link = linkEnt.Get<LinkedSceneryLink>()?.Source;
        if (link is null) { log = "entity has no LinkedSceneryLink"; return false; }

        using var stream = pkg.OpenByPath($"{newPath}.sm2");
        if (stream is null) { log = $"'{newPath}.sm2' not found in the package"; return false; }

        var newSm2 = new PS2AnyTwinsanitySM2();
        using (var reader = new BinaryReader(stream))
            newSm2.Read(reader, (int)stream.Length);

        foreach (var child in linkEnt.Children.ToList())
            linkEnt.RemoveChild(child);

        var tex = BuildSm2Textures(gl, newSm2);
        MeshDecoder.BuildSceneryMeshes(gl, newSm2, linkEnt, tex, applyGlobalEffects: false);

        var oldPath = link.Path;
        link.Path = newPath;
        linkEnt.Name = $"Link_{System.IO.Path.GetFileName(newPath)}";

        log = $"Retargeted linked scenery '{oldPath}' -> '{newPath}'.";
        return true;
    }

    public static PS2AnyTwinsanityRM2 ParseRm2Only(PackageReader pkg, string rm2Path)
    {
        var rm2 = new PS2AnyTwinsanityRM2();
        using var stream = pkg.OpenByPath(rm2Path)
            ?? throw new FileNotFoundException($"RM2 not found: {rm2Path}");
        using var reader = new BinaryReader(stream);
        rm2.Read(reader, (int)stream.Length);
        return rm2;
    }

    private static Dictionary<uint, Texture2D> BuildTextures(GL gl, PS2AnyTwinsanityRM2 rm2)
    {
        var map = new Dictionary<uint, Texture2D>();

        var gfx = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        if (gfx is null) return map;

        var texSec = gfx.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (texSec is null) return map;

        for (int i = 0; i < texSec.GetItemsAmount(); i++)
        {
            if (texSec.GetItem(i) is PS2AnyTexture tex)
            {
                var t = DecodeTexture(gl, tex);
                if (t is not null) map[tex.GetID()] = t;
            }
        }
        return map;
    }

    public static Texture2D? DecodeTexture(GL gl, PS2AnyTexture tex)
    {
        try
        {
            tex.CalculateData();
            if (tex.Colors.Count == 0) return null;

            int w = tex.ImageWidthPower  > 0 ? (1 << tex.ImageWidthPower)  : 1;
            int h = tex.ImageHeightPower > 0 ? (1 << tex.ImageHeightPower) : 1;

            var rgba = new byte[Math.Max(tex.Colors.Count, w * h) * 4];
            int idx  = 0;
            foreach (TwinColor c in tex.Colors)
            {
                rgba[idx++] = c.R;
                rgba[idx++] = c.G;
                rgba[idx++] = c.B;
                rgba[idx++] = c.A;
            }

            return new Texture2D(gl, rgba, (uint)w, (uint)h);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChunkImporter] Texture {tex.GetID():X8} decode failed: {ex.Message}");
            return null;
        }
    }

    private static Dictionary<uint, Texture2D> BuildSm2Textures(GL gl, PS2AnyTwinsanitySM2 sm2)
    {
        var map = new Dictionary<uint, Texture2D>();
        var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        if (gfx is null) return map;

        var texSec = gfx.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (texSec is null) return map;

        for (int i = 0; i < texSec.GetItemsAmount(); i++)
        {
            if (texSec.GetItem(i) is PS2AnyTexture tex)
            {
                var t = DecodeTexture(gl, tex);
                if (t is not null) map[tex.GetID()] = t;
            }
        }
        return map;
    }

    private static Dictionary<uint, PatrolPath> ExtractPaths(PS2AnyTwinsanityRM2 rm2)
    {
        var map = new Dictionary<uint, PatrolPath>();

        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION;
             lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout  = rm2.GetItem<BaseTwinSection>((uint)lid);
            var pathSec = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_PATHS_SECTION);
            if (pathSec is null) continue;

            for (int i = 0; i < pathSec.GetItemsAmount(); i++)
            {
                if (pathSec.GetItem(i) is PS2AnyPath path)
                {
                    var patrol = new PatrolPath();
                    patrol.Points = path.PointList
                        .Select(v => new SysVec3(v.X, v.Y, v.Z) * INV_SCALE)
                        .ToList();
                    map[path.GetID()] = patrol;
                }
            }
        }
        return map;
    }

    private static void ImportInstances(PS2AnyTwinsanityRM2 rm2,
                                        Entity root,
                                        Dictionary<uint, PatrolPath> pathMap)
    {
        var instRoot = new Entity("Instances");
        root.AddChild(instRoot);

        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION;
             lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout  = rm2.GetItem<BaseTwinSection>((uint)lid);
            var instSec = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION);
            if (instSec is null) continue;

            for (int i = 0; i < instSec.GetItemsAmount(); i++)
            {
                if (instSec.GetItem(i) is not PS2AnyInstance inst) continue;

                if (inst.StateFlags == 0) continue;

                ImportInstance(inst, instRoot, pathMap, instSec);
            }
        }
    }

    public static Entity ImportInstance(PS2AnyInstance inst, Entity parent,
                                       Dictionary<uint, PatrolPath> pathMap,
                                       BaseTwinSection section,
                                       bool isUserAdded = false)
    {
        var e = new Entity($"Inst_{inst.GetID():X4}");

        float rx = inst.RotationX.GetRotation() * MathF.PI / 180f;
        float ry = inst.RotationY.GetRotation() * MathF.PI / 180f;
        float rz = inst.RotationZ.GetRotation() * MathF.PI / 180f;
        var pos = new SysVec3(inst.Position.X, inst.Position.Y, inst.Position.Z) * INV_SCALE;
        e.Transform.Position = pos;
        e.Transform.Scale    = SysVec3.One;

        e.Transform.LocalMatrix =
            Matrix4x4.CreateScale(e.Transform.Scale) *
            Matrix4x4.CreateRotationX(rx) *
            Matrix4x4.CreateRotationY(ry) *
            Matrix4x4.CreateRotationZ(rz) *
            Matrix4x4.CreateTranslation(pos);

        parent.AddChild(e);

        if (inst.Paths.Count > 0)
        {
            var firstPathId = (uint)inst.Paths[0];
            if (pathMap.TryGetValue(firstPathId, out var patrol) && patrol.Count >= 2)
            {
                var enemy = e.Add(new StealthEnemy());
                enemy.Patrol         = patrol;
                enemy.DetectionRange = 18f;
                enemy.AlertRange     = 6f;
                enemy.ChaseRange     = 35f;
            }
        }

        e.Add(new InstanceData
        {
            ObjectId         = inst.ObjectId,
            StateFlags       = inst.StateFlags,
            OnSpawnScriptId  = inst.OnSpawnHeaderScriptID,
            LinkedPaths      = inst.Paths.Select(p => (uint)p).ToList(),
            LinkedInstances  = inst.Instances.Select(p => (uint)p).ToList(),
            Source           = inst,
            Section          = section,
            IsUserAdded      = isUserAdded,
        });

        return e;
    }
}


public sealed class SceneryMarker : CrashEngine.Core.Component
{
    public Dictionary<uint, Texture2D> TextureCache { get; set; } = new();
}

public sealed class CollisionMesh : CrashEngine.Core.Component
{
}

public sealed class SceneryTile : CrashEngine.Core.Component
{
    public Twinsanity.TwinsanityInterchange.Common.Matrix4 Source { get; set; } = null!;

    public Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryBaseType Node { get; set; } = null!;
    public uint SourceId { get; set; }
    public bool IsLod { get; set; }
}

public sealed class LinkedSceneryLink : CrashEngine.Core.Component
{
    public Twinsanity.TwinsanityInterchange.Common.TwinChunkLink Source { get; set; } = null!;
}

public sealed class LoadWallMarker : CrashEngine.Core.Component
{
    public Twinsanity.TwinsanityInterchange.Common.TwinChunkLink Source { get; set; } = null!;
}

public sealed class LoadZoneBoxMarker : CrashEngine.Core.Component
{
    public Twinsanity.TwinsanityInterchange.Common.TwinChunkLink Link { get; set; } = null!;
    public Twinsanity.TwinsanityInterchange.Common.TwinChunkLinkBoundingBoxBuilder Box { get; set; } = null!;
}

public sealed class SkydomeMarker : CrashEngine.Core.Component
{
    public float Brightness = 1f;
    public Dictionary<object, List<Twinsanity.TwinsanityInterchange.Common.Vector4>>? BaseColors;
}

public sealed class ParticleEmitterMarker : CrashEngine.Core.Component
{
    public Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter Source { get; set; } = null!;
}

public sealed class TriggerMarker : CrashEngine.Core.Component
{
    public PS2AnyTrigger Source { get; set; } = null!;
}
public sealed class CameraMarker : CrashEngine.Core.Component
{
    public PS2AnyCamera Source { get; set; } = null!;
}

public sealed class PositionMarker : CrashEngine.Core.Component
{
    public PS2AnyPosition Source { get; set; } = null!;
}
public sealed class AiPositionMarker : CrashEngine.Core.Component
{
    public PS2AnyAIPosition Source { get; set; } = null!;
}
// Amedo 2026-09-19
public sealed class SceneryLightMarker : CrashEngine.Core.Component
{
    public Twinsanity.TwinsanityInterchange.Common.Lights.Light Source { get; set; } = null!;
    public bool IsNegative { get; set; }
}

public sealed class InstanceData : CrashEngine.Core.Component
{
    public ushort     ObjectId        { get; set; }
    public uint       StateFlags      { get; set; }
    public ushort     OnSpawnScriptId { get; set; }
    public List<uint> LinkedPaths     { get; set; } = new();
    public List<uint> LinkedInstances { get; set; } = new();

    public bool        IsUserAdded    { get; set; }

    public Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance Source { get; set; } = null!;

    public Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection Section { get; set; } = null!;

    
}

public sealed class ChunkSource : CrashEngine.Core.Component
{
    public Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2  Rm2 { get; set; } = null!;
    public Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2? Sm2 { get; set; }
    public string  Rm2Path { get; set; } = "";
    public string? Sm2Path { get; set; }

    public Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2? GlobalRm2 { get; set; }

    public Dictionary<uint, Texture2D> GlobalTexCache { get; set; } = new();

    public bool GlobalRm2Dirty { get; set; }

    public Dictionary<uint, Texture2D> TexCache { get; set; } = new();

    public Dictionary<uint, Texture2D> SceneryTexCache { get; set; } = new();

    public object? MeshTables { get; set; }

    public object? SceneryTables { get; set; }

    public Dictionary<uint, TransplantRecord> Transplants { get; } = new();

    public Dictionary<uint, MeshDecoder.FullTransplantRecord> FullTransplants { get; } = new();

    public Dictionary<uint, MeshDecoder.SceneryBakeRecord> SceneryBakes { get; } = new();
}
