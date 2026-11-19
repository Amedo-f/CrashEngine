using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using Silk.NET.OpenGL;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private string ResolvePristinePackageSource() => DiscBackup.PristineSource(_scriptOut, _extractedRoot);

	private static PS2AnyTwinsanityRM2 ParseRm2(BinaryReader reader, int length, string path)
	{
		PS2AnyTwinsanityRM2 rm2 = path.Equals(GlobalRm2Path, StringComparison.OrdinalIgnoreCase) ? new PS2Default() : new PS2AnyTwinsanityRM2();
		rm2.Read(reader, length);
		return rm2;
	}

	private PS2AnyTwinsanityRM2? TryReadPristineRm2(string path)
	{
		using var pkg = PackageReader.Open(ResolvePristinePackageSource());
		using var stream = pkg.OpenByPath(path);
		if (stream is null) return null;
		using var reader = new BinaryReader(stream);
		return ParseRm2(reader, (int)stream.Length, path);
	}

	private PS2AnyTwinsanityRM2 ReadPristineRm2(string path) =>
		TryReadPristineRm2(path) ?? throw new FileNotFoundException($"Couldn't reopen {path} from the disc archive.");

	// Amedo 2026-11-12: a level loads in steps, each one the old LoadLevelEntities code in its old order. LoadLevelEntities runs
	// them all in this frame; BeginLevelLoad (GameScene.LevelLoading.cs) runs the reading and the collision matching in the
	// background and sends the GPU data a little each frame.
	private sealed record LevelParts(Entity Root, int? ScriptCount, bool GlobalEffectsPending = false); // Amedo 2026-11-15

	private void LoadLevelEntities(GL gl, RenderPipeline pipeline, bool useShadowDir)
	{
		try
		{
			StartLevelLoad();
			var (source, shadowDir) = LevelPackage(useShadowDir);
			var parts = ReadLevel(gl, source, shadowDir, uploads: null);
			PrepareLoadedLevel(parts.Root);
			FinishLevelLoad(gl, pipeline, useShadowDir, parts, MatchCollisionTiles(parts.Root));
		}
		catch (Exception ex)
		{
			LevelLoadFailed(ex);
		}
	}

	private void StartLevelLoad()
	{
		StopSimulation(); // Amedo 2026-09-26
		GameShadowDefs.CratesReceiveShadows = GetGameProject()?.CratesReceiveShadows ?? false; // Amedo
		_reskinCloneOgi.Clear();
		_pristineRm2ForRecovery = null;
		_pristineRm2RecoveryFailed = false;
		ChunkImporter.HdTextureDir = Path.Combine(ModeDir("HD"), "Textures"); // Amedo 2026-10-02
		CrashEngine.Renderer.SmoothNormalCache.Directory = Path.Combine(ModeDir("Cache"), "SmoothNormals"); // Amedo
		DropPlayHost();
	}

	private (string Source, string? ShadowDir) LevelPackage(bool useShadowDir) =>
		(useShadowDir ? _extractedRoot : ResolvePristinePackageSource(), useShadowDir ? SavedChunksDir : null);

	// Amedo 2026-11-12: no scene state here (it may run off the main thread); GPU data goes to uploads when there is one
	private LevelParts ReadLevel(GL gl, string source, string? shadowDir, GpuUploadQueue? uploads)
	{
		_browser.Log("Opening package: " + source);
		using PackageReader packageReader = PackageReader.Open(source);
		if (shadowDir is not null)
		{
			packageReader.ShadowDir = shadowDir;
		}
		_browser.Log($"{packageReader.Records.Count} files indexed" + (shadowDir is not null ? "" : "  [ignoring any saved edits — true pristine disc data]"));
		int? scriptCount = DumpLevelScripts(packageReader);
		_browser.Log("Loading chunk " + _rm2 + "...");
		Entity entity;
		if (uploads is null) entity = ChunkImporter.LoadChunk(gl, packageReader, _rm2, _sm2, out _);
		else using (uploads.Begin()) entity = ChunkImporter.LoadChunk(gl, packageReader, _rm2, _sm2, out _, applyGlobalEffects: false); // Amedo 2026-11-15
		return new LevelParts(entity, scriptCount, GlobalEffectsPending: uploads is not null); // Amedo 2026-11-15
	}

	private int? DumpLevelScripts(PackageReader packageReader)
	{
		_browser.Log("Dumping scripts...");
		try
		{
			ScriptDumper.DumpFromPackage(packageReader, _rm2, _scriptOut);
			string fileNameWithoutExtension = GamePaths.FileNameWithoutExtension(_rm2);
			string path = Path.Combine(_scriptOut, fileNameWithoutExtension);
			int count = (Directory.Exists(path) ? Directory.GetFiles(path, "*.lab").Length : 0);
			_browser.Log($"{count} scripts → Scripts/{fileNameWithoutExtension}/");
			return count;
		}
		catch (Exception ex)
		{
			_browser.Log("Script dump: " + ex.Message);
			return null;
		}
	}

	private void PrepareLoadedLevel(Entity entity)
	{
		StripDanglingLinksForNewGamePreview(entity); // Amedo 2026-09-22
		MakeEditorContentReceiveShadows(entity); // Amedo
	}

	private void FinishLevelLoad(GL gl, RenderPipeline pipeline, bool useShadowDir, LevelParts parts, CollisionTileMatch? tiles)
	{
		Entity entity = parts.Root;
		AttachModernLooks(entity); // Amedo 2026-11-18
		if (parts.GlobalEffectsPending && entity.Get<ChunkSource>()?.Sm2 is { } sm2) MeshDecoder.ApplyGlobalEffects(sm2, entity); // Amedo 2026-11-15
		if (parts.ScriptCount is { } scriptCount) _scriptCount = scriptCount;
		System.Numerics.Vector3? lastFogColor = MeshDecoder.LastFogColor;
		if (lastFogColor.HasValue)
		{
			System.Numerics.Vector3 valueOrDefault = lastFogColor.GetValueOrDefault();
			if (true)
			{
				pipeline.FogColor = valueOrDefault;
			}
		}
		Entity entity2 = new Entity("World Lighting");
		WorldLightingSettings worldLightingSettings = entity2.Add(new WorldLightingSettings());
		if (MeshDecoder.LastFogColorIndex is { } fogIdxLoaded)
			worldLightingSettings.FogColorIndex = fogIdxLoaded;
		(System.Numerics.Vector3, List<(System.Numerics.Vector3, System.Numerics.Vector3)>)? lastWorldLighting = MeshDecoder.LastWorldLighting;
		if (lastWorldLighting.HasValue)
		{
			(System.Numerics.Vector3, List<(System.Numerics.Vector3, System.Numerics.Vector3)>) valueOrDefault2 = lastWorldLighting.GetValueOrDefault();
			if (true)
			{
				(worldLightingSettings.AmbientColor, _) = valueOrDefault2;
				worldLightingSettings.Directional.AddRange(valueOrDefault2.Item2);
				goto IL_0273;
			}
		}
		worldLightingSettings.AmbientColor = System.Numerics.Vector3.One;
		goto IL_0273;
		IL_0273:
		if (useShadowDir) RestoreWorldLightingMeta(worldLightingSettings); // Amedo 2026-09-21
		entity.AddChild(entity2);
		entity.Add(new CrashEngine.Importer.GameLightingEnvironment // Amedo 2026-09-26
		{
			Scenery = entity.Get<ChunkSource>()?.Sm2?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM),
		});
		entity.Add(new CrashEngine.Importer.GameShadowCasters()); // Amedo 2026-09-26
		foreach (var item2 in worldLightingSettings.Directional)
		{
			System.Numerics.Vector3 item = item2.Color;
			Entity entity3 = new Entity("Light Direction");
			GpuMesh mesh = BuildSingleArrowMesh(gl, new System.Numerics.Vector4(item.X, item.Y, item.Z, 1f));
			worldLightingSettings.ArrowRenderers.Add(entity3.Add(new GizmoRenderer
			{
				Mesh = mesh
			}));
			entity2.AddChild(entity3);
		}
		Entity entity4 = new Entity("Skydome");
		SkydomeMarker skydomeMarker = entity4.Add(new SkydomeMarker());
		if (useShadowDir) RestoreSkydomeMeta(skydomeMarker);
		entity.AddChild(entity4);
		_enemyCount = 0;
		foreach (Entity item3 in AllEntities(entity))
		{
			StealthEnemy stealthEnemy = item3.Get<StealthEnemy>();
			if (stealthEnemy != null)
			{
				stealthEnemy.Player = _player;
				_enemyCount++;
			}
		}
		AddRoot(entity);
		BuildLayoutMarkers(entity);
		ApplyCollisionTileMatch(tiles); // Amedo 2026-11-12
		RestoreCollisionMarkers(entity, fromSavedEdits: useShadowDir);
		int value = AllEntities(entity).Count((Entity e) => e.Has<CrashEngine.Importer.MeshRenderer>());
		int value2 = AllEntities(entity).Count((Entity e) => e.Has<InstanceData>());
		_browser.Log($"  Entities: {AllEntities(entity).Count()}  Instances: {value2}  Meshes: {value}");
		foreach (Entity item4 in (from e in AllEntities(entity)
			where e.Has<CrashEngine.Importer.MeshRenderer>()
			select e).Take(3))
		{
			System.Numerics.Vector3 translation = item4.Transform.World.Translation;
			_browser.Log($"  mesh @ ({translation.X:F1}, {translation.Y:F1}, {translation.Z:F1})");
		}
		List<System.Numerics.Vector3> list = (from e in AllEntities(entity)
			where e.Has<InstanceData>()
			select e.Transform.Position).ToList();
		if (list.Count > 0)
		{
			System.Numerics.Vector3 target = list.Aggregate(System.Numerics.Vector3.Zero, (System.Numerics.Vector3 a, System.Numerics.Vector3 b) => a + b) / list.Count;
			_browser.Log($"  avg inst pos: ({target.X:F1}, {target.Y:F1}, {target.Z:F1})");
			_camera?.FocusOn(target, 80f);
		}

		var musicHit = FindLevelMusic(entity.Get<ChunkSource>()?.Rm2);
		if (musicHit is { } m)
		{
			_musicLabel = MusicNames.TryGetValue(m.Id, out var name)
				? $"{name} (id {m.Id})"
				: $"id {m.Id} (undocumented — not in the reference tool's own name table)";
			_browser.Log(m.FromDjInstance
				? $"  Music: {_musicLabel}  [DJ instance's own IVars[0]]"
				: $"  Music: {_musicLabel}  [{m.Count} BeginMusic call(s) found, no DJ instance — fallback tally]");
		}
		else
		{
			_musicLabel = null;
			_browser.Log("  Music: none found (no BeginMusic call in this chunk's scripts).");
		}

		_statusLine = $"Loaded: {_rm2}  |  {_enemyCount} enemies  |  {_scriptCount} scripts";
		_browser.Log(_statusLine);
		PrepareForPlay(); // Amedo 2026-10-02
	}

	private void LevelLoadFailed(Exception ex)
	{
		_statusLine = "ERROR: " + ex.Message;
		_browser.Log(_statusLine);
	}

	private void ReloadLevel(bool fromDiscOnly)
	{
		Entity entity = base.Roots.FirstOrDefault((Entity r) => r.Has<ChunkSource>());
		if (entity != null)
		{
			RemoveRoot(entity);
		}
		_selectedSet.Clear();
		_selected = null;
		_triggersRoot = (_camerasRoot = (_positionsRoot = (_aiPosRoot = null)));
		_objectCollisionRoot = null; // Amedo 2026-10-03
		_gameCameraOverlay = null; // Amedo 2026-10-06
		_collisionTileMap = null;
		_collisionTileBaselinePos.Clear();
		_undoStack.Clear();
		_animUndo.Clear(); // Amedo 2026-10-04
		GL gL = Engine.Instance.GL;
		RenderPipeline renderPipeline = FindFirst<RenderPipeline>();
		if (renderPipeline == null)
		{
			_browser.Log("Reload Level: no RenderPipeline found (unexpected).");
			return;
		}
		_browser.Log(fromDiscOnly ? "Reloading level from the true disc data (ignoring any saved edits)..." : "Reloading level...");
		BeginLevelLoad(gL, renderPipeline, !fromDiscOnly); // Amedo 2026-11-12
	}
}
