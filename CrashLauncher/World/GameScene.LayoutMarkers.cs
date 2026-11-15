using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinBoundingBoxBuilder;
using TwinChunkLinkBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinChunkLinkBoundingBoxBuilder;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinTrigger = Twinsanity.TwinsanityInterchange.Common.TwinTrigger;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void AddLoadZoneBox(Entity wallEntity, LoadWallMarker wall)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null || wallEntity.Transform.LocalMatrix is not { } wallLm) return;

        var right = new Vector3(wallLm.M11, wallLm.M12, wallLm.M13);
        var up    = new Vector3(wallLm.M21, wallLm.M22, wallLm.M23);
        float halfW = right.Length() > 1e-6f ? right.Length() : 1f;
        float halfH = up.Length()    > 1e-6f ? up.Length()    : 1f;
        var wallCenter = new Vector3(wallLm.M41, wallLm.M42, wallLm.M43);
        var normal = right.LengthSquared() > 1e-6f && up.LengthSquared() > 1e-6f
            ? Vector3.Normalize(Vector3.Cross(Vector3.Normalize(right), Vector3.Normalize(up)))
            : Vector3.UnitZ;

        float depth = MathF.Min(halfW, halfH) * 0.5f;
        var center = wallCenter + normal * depth;
        var boxLm = new Matrix4x4(
            halfW, 0f, 0f, 0f,
            0f, halfH, 0f, 0f,
            0f, 0f, depth, 0f,
            center.X, center.Y, center.Z, 1f);

        var newBox = new TwinBoundingBoxBuilder();
        ChunkExporter.RecomputeBoundingBox(newBox, boxLm);
        var linkBox = new TwinChunkLinkBoundingBoxBuilder { Type = 0, BondingBoxBuilder = newBox };
        wall.Source.ChunkLinksCollisionData.Add(linkBox);

        EnsureBoxZoneMesh(Engine.Instance.GL);
        var e = new Entity($"LoadZoneBox_{System.IO.Path.GetFileName(wall.Source.Path)}_{wall.Source.ChunkLinksCollisionData.Count}");
        e.Transform.LocalMatrix = boxLm;
        e.Add(new LoadZoneBoxMarker { Link = wall.Source, Box = linkBox });
        var rdr = e.Add(new DirectCubeRenderer { Mesh = _boxZoneMesh, Color = new Vector4(1f, 0.65f, 0.1f, 0.35f) });
        rdr.Mat.AlphaBlend = true;
        wallEntity.Parent?.AddChild(e);

        // Amedo 2026-09-20
        PushAddWithSelectionRestore(
            new LoadZoneBoxAddAction { Parent = wallEntity.Parent!, Entity = e, Link = wall.Source, Box = linkBox },
            e);

        _browser.Log($"Added a pre-load bounding box for '{wall.Source.Path}' (box {wall.Source.ChunkLinksCollisionData.Count} on this link). " +
                      "Drag it into place (Move gizmo) and use Size X/Y/Z to resize -- click Add Bounding Box again to chain more into an L/U shape. Save Chunk to persist.");
    }

    private Entity MakeQuadMarker(Entity parent, string name, TwinChunkLink link, Vector4 color)
    {
        var wall = link.LoadingWall!;
        var p1 = new Vector3(wall.Column1.X, wall.Column1.Y, wall.Column1.Z);
        var p2 = new Vector3(wall.Column2.X, wall.Column2.Y, wall.Column2.Z);
        var p3 = new Vector3(wall.Column3.X, wall.Column3.Y, wall.Column3.Z);
        var p4 = new Vector3(wall.Column4.X, wall.Column4.Y, wall.Column4.Z);

        var center    = (p1 + p2 + p3 + p4) * 0.25f;
        var edgeRight = p2 - p1;
        var edgeUp    = p4 - p1;
        var halfRight = edgeRight.Length() * 0.5f;
        var halfUp    = edgeUp.Length() * 0.5f;
        var right     = halfRight > 1e-6f ? edgeRight / edgeRight.Length() : Vector3.UnitX;
        var up        = halfUp    > 1e-6f ? edgeUp    / edgeUp.Length()    : Vector3.UnitY;
        var normal    = Vector3.Cross(right, up);
        if (normal.LengthSquared() < 1e-6f) normal = Vector3.UnitZ; else normal = Vector3.Normalize(normal);

        var lm = new Matrix4x4(
            right.X * halfRight, right.Y * halfRight, right.Z * halfRight, 0f,
            up.X    * halfUp,    up.Y    * halfUp,    up.Z    * halfUp,    0f,
            normal.X,             normal.Y,            normal.Z,            0f,
            center.X,             center.Y,            center.Z,            1f);

        var e = new Entity(name);
        e.Transform.LocalMatrix = lm;
        e.Add(new LoadWallMarker { Source = link });
        var rdr = e.Add(new DirectCubeRenderer { Mesh = _quadMesh, Color = color });
        rdr.Mat.AlphaBlend = true;
        parent.AddChild(e);
        return e;
    }

    private Entity MakePointMarker(Entity parent, string name, TwinVec4 pos, Vector4 color)
    {
        var e = new Entity(name);
        e.Transform.Position = new Vector3(pos.X, pos.Y, pos.Z);
        e.Transform.Scale    = new Vector3(0.3f, 0.3f, 0.3f);

        var fillEnt = new Entity($"{name}_fill");
        var fillRdr = fillEnt.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = color });
        fillRdr.Mat.AlphaBlend = true;
        fillRdr.Mat.DepthWrite = false;
        fillRdr.Mat.IgnoreDepthTest = true;
        e.AddChild(fillEnt);

        var edgeEnt = new Entity($"{name}_edge");
        var edgeRdr = edgeEnt.Add(new DirectCubeRenderer { Mesh = _cubeWireMesh, Color = new Vector4(color.X, color.Y, color.Z, 1f) });
        edgeRdr.Mat.DepthWrite = false;
        edgeRdr.Mat.IgnoreDepthTest = true;
        e.AddChild(edgeEnt);

        parent.AddChild(e);
        return e;
    }

    private void EnsurePositionMarkersExist(Entity chunkRoot, PS2AnyTwinsanityRM2 rm2, IEnumerable<ushort> ids)
    {
        if (_positionsRoot is null) return;
        var existing = new HashSet<uint>(
            AllEntities(chunkRoot).Where(x => x.Has<CrashEngine.Importer.PositionMarker>())
                .Select(x => x.Get<CrashEngine.Importer.PositionMarker>()!.Source.GetID()));

        foreach (var id in ids.Distinct())
        {
            if (existing.Contains(id)) continue;
            var p = Layouts(rm2).SelectMany(l => LayoutSectionItems<PS2AnyPosition>(l, TwinConstants.LAYOUT_POSITIONS_SECTION))
                                .FirstOrDefault(x => x.GetID() == id);
            if (p is not null)
            {
                var posEnt = MakePositionMarker(_positionsRoot, $"Position_{p.GetID():X4}", p.Position); // Amedo 2026-11-15
                posEnt.Add(new CrashEngine.Importer.PositionMarker { Source = p });
            }
        }
    }

    private void BuildLayoutMarkers(Entity chunkRoot)
    {
        var rm2 = chunkRoot.Get<ChunkSource>()?.Rm2;
        if (rm2 is null) return;

        var triggersRoot  = new Entity("Triggers")    { Active = false };
        var camerasRoot   = new Entity("Cameras")     { Active = false };
        var positionsRoot = new Entity("Positions")   { Active = false };
        var aiPosRoot     = new Entity("AiPositions") { Active = false };
        var loadScenesRoot = new Entity("LoadScenes") { Active = false };
        var emittersRoot   = new Entity("ParticleEmitters") { Active = false };
        var lightsRoot     = new Entity("Lights") { Active = _showLights }; // Amedo 2026-09-19
        var effectsRoot    = new Entity("ParticleEffects") { Active = _showParticleEffects }; // Amedo 2026-09-26
        var effects        = effectsRoot.Add(new ParticleEffectsRenderer { DistanceGating = _particleDistanceGating }); // Amedo 2026-09-26
        chunkRoot.AddChild(triggersRoot);
        chunkRoot.AddChild(camerasRoot);
        chunkRoot.AddChild(positionsRoot);
        chunkRoot.AddChild(aiPosRoot);
        chunkRoot.AddChild(loadScenesRoot);
        chunkRoot.AddChild(emittersRoot);
        chunkRoot.AddChild(lightsRoot);
        chunkRoot.AddChild(effectsRoot); // Amedo 2026-09-26
        AddObjectCollisionRoot(chunkRoot); // Amedo 2026-10-03
        AddGameCameraOverlay(chunkRoot); // Amedo 2026-10-06
        _triggersRoot = triggersRoot; _camerasRoot = camerasRoot;
        _positionsRoot = positionsRoot; _aiPosRoot = aiPosRoot;
        _loadScenesRoot = loadScenesRoot; _particleEmittersRoot = emittersRoot;
        _lightsRoot = lightsRoot;
        _particleEffectsRoot = effectsRoot; _particleEffects = effects; // Amedo 2026-09-26
        EnsureFxTexturePagesLoaded(); // Amedo 2026-09-26
        effects.Pages = _fxTexturePages; // Amedo 2026-09-26
        effects.Decals = _fxDecals; // Amedo 2026-09-30

        Entity MakeBoxMarker(Entity parent, string name, TwinVec4 pos, TwinVec4 scale, Vector4 color, Texture2D? icon = null, TwinVec4? rotation = null)
        {
            var e = new Entity(name);
            e.Transform.Position = new Vector3(pos.X, pos.Y, pos.Z);
            var sc = new Vector3(MathF.Max(scale.X, 0.05f), MathF.Max(scale.Y, 0.05f), MathF.Max(scale.Z, 0.05f));
            e.Transform.Scale = sc;
            if (rotation is not null)
            {
                TwinTrigger.DecodeAxisAngle(rotation.Value, out var axis, out var angleRad); // Amedo
                if (angleRad != 0f)
                    e.Transform.Rotation = Quaternion.CreateFromAxisAngle(new Vector3(axis.X, axis.Y, axis.Z), angleRad);
            }

            var fillEnt = new Entity($"{name}_fill");
            var fillRdr = fillEnt.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = color });
            fillRdr.Mat.AlphaBlend = true;
            fillRdr.Mat.DepthWrite = false;
            fillRdr.Mat.IgnoreDepthTest = true;
            e.AddChild(fillEnt);

            var edgeEnt = new Entity($"{name}_edge");
            var edgeRdr = edgeEnt.Add(new DirectCubeRenderer { Mesh = _cubeWireMesh, Color = new Vector4(color.X, color.Y, color.Z, 1f) });
            edgeRdr.Mat.DepthWrite = false;
            edgeRdr.Mat.IgnoreDepthTest = true;
            e.AddChild(edgeEnt);

            if (icon is not null)
            {
                var iconEnt = new Entity($"{name}_icon");
                iconEnt.Transform.Position = new Vector3(pos.X, pos.Y + sc.Y, pos.Z);
                const float iconWorldSize = 0.9f;
                iconEnt.Transform.Scale = new Vector3(iconWorldSize, iconWorldSize, iconWorldSize);
                var iconRdr = iconEnt.Add(new DirectCubeRenderer { Mesh = _quadMesh, Color = Vector4.One });
                iconRdr.Mat.Albedo          = icon;
                iconRdr.Mat.AlphaBlend      = true;
                iconRdr.Mat.BillboardRender = true;
                iconRdr.Mat.Culling         = Material.CullMode.Both;
                iconRdr.Mat.DepthWrite      = false;
                iconRdr.Mat.IgnoreDepthTest = true;
                parent.AddChild(iconEnt);
            }

            parent.AddChild(e);
            return e;
        }

        int triggers = 0, cameras = 0, positions = 0, aiPositions = 0;
        foreach (var layout in Layouts(rm2))
        {
            foreach (var t in LayoutSectionItems<PS2AnyTrigger>(layout, TwinConstants.LAYOUT_TRIGGERS_SECTION))
            {
                var trigEnt = MakeBoxMarker(triggersRoot, $"Trigger_{t.GetID():X4}", t.Trigger.Position, t.Trigger.Scale,
                              new Vector4(0.2f, 0.85f, 0.3f, 0.35f), rotation: t.Trigger.Rotation);
                trigEnt.Add(new CrashEngine.Importer.TriggerMarker { Source = t });
                triggers++;
            }

            foreach (var c in LayoutSectionItems<PS2AnyCamera>(layout, TwinConstants.LAYOUT_CAMERAS_SECTION))
            {
                var camEnt = MakeBoxMarker(camerasRoot, $"Camera_{c.GetID():X4}", c.CamTrigger.Position, c.CamTrigger.Scale,
                              new Vector4(0.15f, 0.4f, 0.9f, 0.35f), rotation: c.CamTrigger.Rotation);
                camEnt.Add(new CrashEngine.Importer.CameraMarker { Source = c });
                cameras++;
            }

            foreach (var p in LayoutSectionItems<PS2AnyPosition>(layout, TwinConstants.LAYOUT_POSITIONS_SECTION))
            {
                var posEnt = MakePositionMarker(positionsRoot, $"Position_{p.GetID():X4}", p.Position); // Amedo 2026-11-15
                posEnt.Add(new CrashEngine.Importer.PositionMarker { Source = p });
                positions++;
            }

            foreach (var ap in LayoutSectionItems<PS2AnyAIPosition>(layout, TwinConstants.LAYOUT_AI_POSITIONS_SECTION))
            {
                var aiPosEnt = MakeAiPositionMarker(aiPosRoot, $"AiPosition_{ap.GetID():X4}", ap.Position); // Amedo
                aiPosEnt.Add(new CrashEngine.Importer.AiPositionMarker { Source = ap });
                aiPositions++;
            }
        }

        // Amedo 2026-09-19
        var sceneryForLights = chunkRoot.Get<ChunkSource>()?.Sm2
            ?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);
        if (sceneryForLights is not null) BuildSceneryLightMarkers(sceneryForLights);

        int loadScenes = 0;
        foreach (var linkEnt in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.LinkedSceneryLink>()))
        {
            var link = linkEnt.Get<CrashEngine.Importer.LinkedSceneryLink>()!.Source;
            if (link.LoadingWall is null) continue;
            MakeQuadMarker(loadScenesRoot, $"LoadScene_{System.IO.Path.GetFileName(link.Path)}",
                link, new Vector4(1f, 0.4f, 0.1f, 0.6f));
            loadScenes++;
        }

        int emitters = 0;
        var particleData = rm2.GetItem<PS2AnyParticleData>((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
        if (particleData is not null)
        {
            foreach (var em in particleData.ParticleEmitters)
            {
                var name = ParticleNames.Clean(em.Name); // Amedo 2026-09-26
                var e = new Entity($"ParticleEmitter_{(string.IsNullOrWhiteSpace(name) ? "unnamed" : name)}");
                e.Transform.Position = new Vector3(em.Position.X, em.Position.Y, em.Position.Z);
                e.Transform.Scale    = new Vector3(0.4f, 0.4f, 0.4f);
                var rdr = e.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = new Vector4(0.6f, 0.85f, 1f, 0.7f) });
                rdr.Mat.AlphaBlend = true;
                e.Add(new CrashEngine.Importer.ParticleEmitterMarker { Source = em });
                emittersRoot.AddChild(e);
                AddParticleEffect(effects, e, em, chunkRoot.Get<ChunkSource>()); // Amedo 2026-09-26
                emitters++;
            }
        }

        _browser.Log($"Layout markers: {triggers} triggers, {cameras} cameras, {positions} positions, " +
                      $"{aiPositions} AI positions, {loadScenes} load scenes, {emitters} particle emitters " +
                      "(all hidden by default, see Scene panel toggles).");
    }

    private Entity? _triggersRoot, _camerasRoot, _positionsRoot, _aiPosRoot, _loadScenesRoot, _particleEmittersRoot;
    private Entity? _lightsRoot; // Amedo 2026-09-19
    private bool _showTriggers, _showCameras, _showPositions, _showAiPositions, _showLoadScenes, _showParticleEmitters;
    private bool _showLights = true; // Amedo 2026-09-19
    private Entity? _particleEffectsRoot; // Amedo 2026-09-26
    private ParticleEffectsRenderer? _particleEffects; // Amedo 2026-09-26
    private bool _showParticleEffects = true, _particleDistanceGating = true; // Amedo 2026-09-26

	private void UpdatePositionChainVisual()
	{
		if (_positionChainRenderer is null)
		{
			var host = new Entity("__PositionChainViz");
			_positionChainRenderer = host.Add(new CrashEngine.Renderer.PositionChainRenderer());
			LoadAndAddRoot(host);
		}

		var points = _positionChainRenderer.Points;
		points.Clear();

		var chainInst = _selected?.Get<InstanceData>();
		if (chainInst is not null && chainInst.Source.Positions.Count > 0)
		{
			points.Add(_selected!.Transform.World.Translation);
			foreach (var (_, _, marker) in PatrolPoints(chainInst.Source)) // Amedo 2026-11-15
				if (marker is not null) points.Add(marker.Transform.World.Translation);
		}
	}

    private static IEnumerable<BaseTwinSection> Layouts(PS2AnyTwinsanityRM2 rm2)
    {
        for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
            if (rm2.GetItem<BaseTwinSection>((uint)lid) is { } layout) yield return layout;
    }

    private static IEnumerable<T> LayoutSectionItems<T>(BaseTwinSection layout, int sectionId) where T : class
    {
        var section = layout.GetItem<BaseTwinSection>((uint)sectionId);
        if (section is null) yield break;
        for (int i = 0; i < section.GetItemsAmount(); i++)
            if (section.GetItem(i) is T item) yield return item;
    }
}
