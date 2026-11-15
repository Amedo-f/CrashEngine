using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.OpenGL;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{


    private readonly string _extractedRoot;
    private readonly string _rm2;
    private readonly string _sm2;
    private readonly string _scriptOut;
    private readonly LevelBrowserScene _browser;
    private string? _pendingOpenLevelPath;
    private string? _autoAddCrashFrom;
    private string _statusLine = "Loading...";
    private int _enemyCount;
    private int _scriptCount;
    private string? _musicLabel;
    private PlayerProxy? _player;
    private CameraComponent? _camera;

    private static readonly System.Numerics.Vector4 CollisionDebugColor = new(1f, 0.15f, 0.85f, 0.35f);

    private GpuMesh? _cubeMesh;
    private GpuMesh? _cubeWireMesh;
    private Texture2D? _cameraIconTexture;
    private static readonly System.Numerics.Vector2 CameraIconMin = new(-14f, -17f), CameraIconMax = new(14f, 11f); // Amedo 2026-10-04

    // Amedo 2026-09-21
    private Texture2D? _rotateIconTexture;
    private Texture2D? _moveIconTexture;
    private GpuMesh? _quadMesh;
    private GpuMesh? _boxZoneMesh;
    private GpuMesh? _gizmoMesh;
    private GizmoRenderer? _gizmoRenderer;
    private WorldLightingSettings? _lastLightingSelected;
    private readonly List<CubeEntry> _cubes = new List<CubeEntry>();
    private readonly List<CubeEntry> _pendingDel = new List<CubeEntry>();
    private readonly List<Entity> _importedRoots = new List<Entity>();
    private readonly List<Entity> _pendingDelImports = new List<Entity>();
    private int _cubeCount = 0;
    private bool _pendingSpawn;
    private bool _pendingSpawnCollisionBox;
    private bool _pendingSpawnCollisionCylinder;
    private bool _pendingSpawnCollisionPlane;
    private string _sceneFilter = "";
    private string? _importError;
    private bool _flipModelX = true;
    private bool _showCollision;
    private Entity? _selected;
    private readonly HashSet<Entity> _selectedSet = new HashSet<Entity>();
    private CrashEngine.Renderer.PositionChainRenderer? _positionChainRenderer;
    private bool _revealSelectionInTree;
    private readonly HashSet<Entity> _treeRevealAncestors = new HashSet<Entity>();

    private readonly List<Entity> _treeVisibleOrder = new List<Entity>();
    private Entity? _rangeSelectAnchor;
    private int _gizmoAxis = -1;
    private int _gizmoHover = -1;
    private bool _gizmoDragging;
    private GizmoMode _gizmoMode = GizmoMode.Move;
    private static readonly System.Numerics.Vector3[] SixDirs = new System.Numerics.Vector3[6]
    {
        System.Numerics.Vector3.UnitX,
        -System.Numerics.Vector3.UnitX,
        System.Numerics.Vector3.UnitY,
        -System.Numerics.Vector3.UnitY,
        System.Numerics.Vector3.UnitZ,
        -System.Numerics.Vector3.UnitZ
    };
    private bool _choosingDuplicateDirection;
    private System.Numerics.Vector3 _lastDuplicateDirection = System.Numerics.Vector3.UnitX;
    private System.Numerics.Vector3 _dupGizmoPivot;
    private int _dupGizmoHover = -1;
    private GpuMesh? _dupGizmoMesh;
    private GizmoRenderer? _dupGizmoRenderer;
    private readonly Dictionary<Entity, TransformSnapshot> _gizmoDragBefore = new Dictionary<Entity, TransformSnapshot>();
    private readonly EditorUndoStack _undoStack = new EditorUndoStack();
    // Amedo 2026-09-20
    private readonly List<Entity> _selSnapshot = new List<Entity>();
    private bool _selSnapInit;
    private int _undoCountAtSync;
    private bool _glueMode;
    private Entity? _glueSource;
    private int _glueAxisLock = -1;
    private System.Numerics.Vector2 _gizmoPrevMouse;
    private bool _mouseWasDown;
    private const float StemLen = 1.8f;
    private const float HeadLen = 0.45f;
    private const float TipLen = 2.25f;
    private bool _built;
    private string _linkTargetFilter = "";
    private string _addLinkFilter = "";
    private string _lightTargetFilter = "";
    private bool _showUiBrowser;
    private bool _showGameSettings;
    private bool _showGlobalData;
    private string _editMeshIdBuf = "";
    private System.Diagnostics.Process? _playerProcess;
    private readonly ImGuiFileBrowser _fileBrowser = new();
    private readonly WindowChrome _windowChrome = new();
    private readonly Dictionary<uint, string> _scriptTextCache = new Dictionary<uint, string>();
    private readonly Dictionary<uint, CrashEngine.Importer.ScriptDumper.SpawnScriptInfo?> _spawnScriptInfoCache = new();
    private uint? _inspectedBehaviourId;
    private string _inspectedBehaviourText = "";
    private PS2BehaviourGraph? _inspectedBehaviourGraph;
    private string _inspectedBehaviourApplyMsg = "";
    private string _openBehIdText = "";
    private string _cubeTexPath = "";
    private uint? _ogiPreviewInstance;
    private uint? _ogiPreviewOgiId;



	public GameScene(string extractedRoot, string rm2, string sm2, string scriptOut, LevelBrowserScene browser, string? autoAddCrashFrom = null)
	{
		_extractedRoot = extractedRoot;
		_rm2 = rm2;
		_sm2 = sm2;
		_scriptOut = scriptOut;
		_browser = browser;
		_autoAddCrashFrom = autoAddCrashFrom;
	}


	protected override void Build()
	{
		GpuOwner.Current = this;
		if (!_built)
		{
			_built = true;
			GL gL = Engine.Instance.GL;
			EnsureCubeMesh(gL);
			EnsureCubeWireMesh(gL);
			EnsureQuadMesh(gL);
			EnsureLightIconQuad(gL); // Amedo 2026-09-19
			_cameraIconTexture = Texture2D.FromFile(gL, Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "camera_marker.png"));
			_rotateIconTexture = Texture2D.FromFile(gL, Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "sync-icon-128.png"));
			_moveIconTexture = Texture2D.FromFile(gL, Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "move-icon-512.png"));
			_gizmoMesh = BuildGizmoMesh(gL);
			_dupGizmoMesh = BuildDuplicateGizmoMesh(gL);
			Entity entity = new Entity("RenderPipeline");
			RenderPipeline renderPipeline = entity.Add(new RenderPipeline());
			entity.Add(new GameShadowPass()); // Amedo 2026-09-26
			AddRoot(entity);
			Entity entity2 = new Entity("Camera");
			_camera = entity2.Add(new CameraComponent());
			_camera.Far = 20000f;
			renderPipeline.Camera = _camera;
			AddRoot(entity2);
			Entity entity3 = new Entity("Player");
			_player = entity3.Add(new PlayerProxy());
			AddRoot(entity3);
			Entity entity4 = new Entity("__Gizmo");
			_gizmoRenderer = entity4.Add(new GizmoRenderer
			{
				Mesh = _gizmoMesh
			});
			AddRoot(entity4);
			Entity entity5 = new Entity("__DuplicateGizmo");
			_dupGizmoRenderer = entity5.Add(new GizmoRenderer
			{
				Mesh = _dupGizmoMesh
			});
			AddRoot(entity5);
			Entity entity6 = new Entity("Meter");
			entity6.Add(new GameHUD
			{
				Scene = this
			});
			BeginLevelLoad(gL, renderPipeline, useShadowDir: true, then: () => AddRoot(entity6)); // Amedo 2026-11-12
		}
	}


	protected override void OnUpdate()
	{
		if (UpdateLevelLoad()) return; // Amedo 2026-11-12
		RecordSelectionChangeIfAny(); // Amedo 2026-09-20
		UpdateSimulation(); // Amedo 2026-09-26

		if (_pendingOpenLevelPath is not null)
		{
			var openPath = _pendingOpenLevelPath;
			_pendingOpenLevelPath = null;
			LeaveScene(() => _browser.OpenLevel(openPath)); // Amedo 2026-09-28
			if (!Simulating) return;
		}
		if (_autoAddCrashFrom is not null &&
		    base.Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2 is not null)
		{
			var crashSrc = _autoAddCrashFrom;
			_autoAddCrashFrom = null;
			AutoAddCrashFromTemplate(crashSrc);
		}
		CrashEngine.Importer.MeshRenderer.DrawCallsThisFrame = 0;
		UpdatePositionChainVisual();
		SyncLightMarkerPositionsLive(); // Amedo 2026-09-26
		UpdatePendingEdits();
		UpdateSelectionPulse();
		HandlePlayViewKeys(); // Amedo 2026-09-28
		if (_workspace == Workspace.Animation) // Amedo 2026-10-04
		{
			UpdateAnimationWorkspace();
			return;
		}
		UpdateEscapeAndGlueKeys();
		UpdateSceneMouse();
		UpdateShortcutKeys();
		if (_workspace == Workspace.Scene && !PlayFullView)
		{
			UpdateAiPathLines(); // Amedo 2026-11-15
			UpdatePositionPins(); // Amedo 2026-11-15
			UpdateCameraIcons(); // Amedo 2026-11-15
		}
		UpdatePlayerCamera(); // Amedo 2026-11-15
	}

	private void UpdatePendingEdits()
	{
		if (_pendingDel.Count > 0)
		{
			PS2AnyCollisionData pS2AnyCollisionData = base.Roots.FirstOrDefault((Entity r) => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2?.GetItem<PS2AnyCollisionData>(9u);
			foreach (CubeEntry item in _pendingDel)
			{
				if (pS2AnyCollisionData != null)
				{
					CollisionShapeMarker collisionBoxMarker = item.Ent.Get<CollisionShapeMarker>();
					if (collisionBoxMarker != null)
					{
						foreach (int ownedTriIndex in collisionBoxMarker.OwnedTriIndices)
						{
							if (ownedTriIndex >= 0 && ownedTriIndex < pS2AnyCollisionData.Triangles.Count)
							{
								TwinCollisionTriangle twinCollisionTriangle = pS2AnyCollisionData.Triangles[ownedTriIndex];
								pS2AnyCollisionData.Triangles[ownedTriIndex] = new TwinCollisionTriangle
								{
									Vector1Index = twinCollisionTriangle.Vector1Index,
									Vector2Index = twinCollisionTriangle.Vector1Index,
									Vector3Index = twinCollisionTriangle.Vector1Index,
									SurfaceIndex = twinCollisionTriangle.SurfaceIndex
								};
							}
						}
					}
				}
				_selectedSet.Remove(item.Ent);
				if (_selected == item.Ent)
				{
					_selected = _selectedSet.FirstOrDefault();
				}
				if (item.Ent.Parent is { } p) p.RemoveChild(item.Ent);
				else RemoveRoot(item.Ent);
				_cubes.Remove(item);
			}
			_pendingDel.Clear();
		}
		foreach (Entity pendingDelImport in _pendingDelImports)
		{
			_selectedSet.Remove(pendingDelImport);
			if (_selected == pendingDelImport)
			{
				_selected = _selectedSet.FirstOrDefault();
			}
			RemoveRoot(pendingDelImport);
			_importedRoots.Remove(pendingDelImport);
		}
		_pendingDelImports.Clear();
		if (_pendingSpawn)
		{
			_pendingSpawn = false;
			AddRealCube();
		}
		if (_pendingSpawnCollisionBox)
		{
			_pendingSpawnCollisionBox = false;
			SpawnCube(new System.Numerics.Vector3(0f, 0f, 0f), asCollisionBox: true);
		}
		if (_pendingSpawnCollisionCylinder)
		{
			_pendingSpawnCollisionCylinder = false;
			SpawnCollisionCylinder(new System.Numerics.Vector3(0f, 0f, 0f));
		}
		if (_pendingSpawnCollisionPlane)
		{
			_pendingSpawnCollisionPlane = false;
			SpawnCollisionPlane(new System.Numerics.Vector3(0f, 0f, 0f));
		}
	}


	private IEnumerable<Entity> FlatEntities()
	{
		foreach (var root in Roots)
			foreach (var e in AllEntities(root))
				yield return e;
	}

	private static IEnumerable<Entity> AllEntities(Entity e)
	{
		yield return e;
		foreach (var child in e.Children)
			foreach (var sub in AllEntities(child))
				yield return sub;
	}


	public override void OnImGuiRender()
	{
		if (DrawLevelLoading()) return; // Amedo 2026-11-12
		KeepHeldPictureFresh();
		ApplyPendingWorkspaceLayout(); // Amedo 2026-10-04
		DrawSaveConfirmPopup();
		DrawResetAllConfirmPopup();
		DrawLeavePlayConfirmPopup(); // Amedo 2026-09-28
		DrawBuildProgress();
		if (PlayFullView) // Amedo 2026-09-28
		{
			Engine.Instance.SceneViewport = null;
			FitGameViewport(); // Amedo 2026-10-26
			DrawPlayOverlay();
			DrawSimulatorOverlay(); // Amedo 2026-10-26
			return;
		}
		int width = Engine.Instance.Width;
		int height = Engine.Instance.Height;
		float y = (float)height - 32f - 180f - 2f;
		DrawTopBar(width);
		_windowChrome.SetWorkArea(new System.Numerics.Vector2(0f, 32f), new System.Numerics.Vector2(width, (float)height - 32f));
		DrawBuildWindow();
		_fileBrowser.Draw();
		DrawUiBrowser();
		DrawGameSettings();
		DrawGlobalDataWindow();
		DrawAbout();
		DrawShortcuts();
		DrawUvEditorWindow();
		DrawTexturePickerWindow();
		DrawVertexColorPickerWindow();
		DrawGroundColorPickerWindow();
		DrawConditionPickerWindow();
		DrawCrashPickerWindow();
		DrawScriptsBrowserWindow();
		DrawObjectTransplantLevelPickerWindow();
		DrawObjectTransplantObjectPickerWindow();
		DrawAddBehaviourWindows();
		DrawAddToAssetsPopup();
		DrawAddObjectToAssetsPopup();
		DrawTriggerTransplantLevelPickerWindow();
		DrawTriggerTransplantPickerWindow();
		ImGui.SetNextWindowPos(new System.Numerics.Vector2(0f, 32f));
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(width, (float)height - 32f));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);
		ImGui.Begin("##dockhost", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus);
		ImGui.PopStyleVar(3);
		uint iD = ImGui.GetID("MainDockSpace");
		ImGui.DockSpace(iD, System.Numerics.Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);
		UpdateSceneViewport(iD, StripHeight); // Amedo 2026-10-26
		FitGameViewport(); // Amedo 2026-10-26
		ImGui.End();
		DrawWorkspaceTabs(); // Amedo 2026-10-26
		if (_simulation is { } sim) DrawSimulationScreen(sim); // Amedo 2026-10-26
		DrawSimulatorOverlay(); // Amedo 2026-10-26
		DrawHierarchyPanel(y);
		DrawInspectorPanel(width, y);

		DrawSceneOverlays();
		if (_workspace == Workspace.Scene) // Amedo 2026-10-04
		{
		DrawConsolePanel(width, height);

		DrawAssetsPanel();
		DrawBehaviourTab(); // Amedo 2026-09-25
		}
		else DrawAnimationWorkspace();
		DrawViewGizmo(); // Amedo 2026-10-04
		DrawGizmoModeButtons(); // Amedo
		DrawAiToolOverlay(); // Amedo 2026-11-15
		_windowChrome.DrawEffects();
	}
}
