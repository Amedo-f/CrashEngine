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
using System.Linq;
using System.Text;
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
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;
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
	private sealed class CubeEntry
	{
		public Entity Ent;

		public string Label;

		public DirectCubeRenderer Renderer;

		public CubeEntry(Entity e, string l, DirectCubeRenderer r)
		{
			Ent = e;
			Label = l;
			Renderer = r;
		}
	}

	private sealed class DirectCubeRenderer : Component
	{
		public GpuMesh? Mesh { get; set; }

		public Material Mat { get; } = new Material
		{
			Unlit = true,
			Culling = Material.CullMode.Both,
			AlwaysOnTop = true,
			BaseColor = new System.Numerics.Vector4(1f, 0.85f, 0f, 1f)
		};


		public System.Numerics.Vector4 Color
		{
			get
			{
				return Mat.BaseColor;
			}
			set
			{
				Mat.BaseColor = value;
			}
		}

		public Texture2D? Albedo
		{
			get
			{
				return Mat.Albedo;
			}
			set
			{
				Mat.Albedo = value;
			}
		}

		public float SelectPulse { get; set; } = 1f;

		public bool OwnsMesh { get; set; }


		public override void OnUpdate()
		{
			System.Numerics.Vector2 uvScrollSpeed = Mat.UvScrollSpeed;
			if (uvScrollSpeed.X != 0f || uvScrollSpeed.Y != 0f)
			{
				Mat.UvOffset += uvScrollSpeed * EngineTime.Delta;
			}
		}

		public override void OnRender(GL gl)
		{
			if (Mesh != null)
			{
				RenderPipeline instance = RenderPipeline.Instance;
				if (instance != null)
				{
					TwinShaderProgram shader = instance.Shader;
					shader.Set("StartModel", base.Entity.Transform.World);
					Mat.Apply(gl, shader);
					shader.Set("twin_material.select_pulse", SelectPulse);
					Mesh.Draw(gl);
					Mat.Restore(gl, shader);
				}
			}
		}

		public override void OnDestroy()
		{
			Mat.Albedo?.Dispose();
			if (OwnsMesh) Mesh?.Dispose();
		}
	}

	private sealed class ParticleBillboardRenderer : Component
	{
		public GpuMesh? Mesh { get; set; }

		public Material Mat { get; } = new Material
		{
			Unlit = true,
			Culling = Material.CullMode.Both,
			AlphaBlend = true,
			Blend = Material.BlendMode.Additive,
			DepthWrite = false,
			FogEnabled = false,
			BillboardRender = true
		};


		public System.Numerics.Vector4 Color
		{
			get
			{
				return Mat.BaseColor;
			}
			set
			{
				Mat.BaseColor = value;
			}
		}

		public override bool IsTranslucent => true;

		public override void OnRender(GL gl)
		{
			if (Mesh != null)
			{
				RenderPipeline instance = RenderPipeline.Instance;
				if (instance != null)
				{
					TwinShaderProgram shader = instance.Shader;
					shader.Set("StartModel", base.Entity.Transform.World);
					Mat.Apply(gl, shader);
					Mesh.Draw(gl);
					Mat.Restore(gl, shader);
				}
			}
		}
	}

	private sealed class ParticleEmitterPreview : Component
	{
		public readonly List<TwinParticleSystem> Members = new List<TwinParticleSystem>();

		public readonly List<ParticleBillboardRenderer> Particles = new List<ParticleBillboardRenderer>();

		public override void OnUpdate()
		{
			if (Particles.Count == 0 || Members.Count == 0)
			{
				return;
			}
			int num = Math.Max(1, Particles.Count / Members.Count);
			int num2 = 0;
			for (int i = 0; i < Members.Count; i++)
			{
				if (num2 >= Particles.Count)
				{
					break;
				}
				TwinParticleSystem twinParticleSystem = Members[i];
				List<(float, System.Numerics.Vector4)> colorGradient = MeshDecoder.GetColorGradient(twinParticleSystem);
				List<(float, float)> sizeGradient = MeshDecoder.GetSizeGradient(twinParticleSystem);
				Twinsanity.TwinsanityInterchange.Common.Vector3 unkVec = twinParticleSystem.UnkVec2;
				float num3 = MathF.Sqrt(unkVec.X * unkVec.X + unkVec.Y * unkVec.Y + unkVec.Z * unkVec.Z);
				float num4 = (unkVec.X + unkVec.Y + unkVec.Z) / 3f;
				float num5 = MathF.Max(MathF.Abs(unkVec.X - num4), MathF.Max(MathF.Abs(unkVec.Y - num4), MathF.Abs(unkVec.Z - num4)));
				bool flag = num3 < 0.05f || num5 < MathF.Abs(num4) * 0.35f;
				Twinsanity.TwinsanityInterchange.Common.Vector3 unkVec2 = twinParticleSystem.UnkVec1;
				float num6 = MathF.Max(0.15f, MathF.Sqrt(unkVec2.X * unkVec2.X + unkVec2.Y * unkVec2.Y + unkVec2.Z * unkVec2.Z));
				System.Numerics.Vector3 vector = new System.Numerics.Vector3(unkVec.X, unkVec.Y, unkVec.Z);
				float num7 = vector.Length();
				vector = ((num7 > 0.0001f) ? (vector / num7) : System.Numerics.Vector3.UnitY);
				System.Numerics.Vector3 vector2 = ((System.Numerics.Vector3.Cross(vector, System.Numerics.Vector3.UnitX).LengthSquared() > 0.0001f) ? System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(vector, System.Numerics.Vector3.UnitX)) : System.Numerics.Vector3.UnitZ);
				int num8 = 0;
				while (num8 < num && num2 < Particles.Count)
				{
					ParticleBillboardRenderer particleBillboardRenderer = Particles[num2];
					float num9 = (EngineTime.Total / 1.6f + ((float)num8 * 0.618034f + (float)(i & 0xFF) / 255f)) % 1f;
					float num10 = (float)(num8 + i * 97) * 12.9898f % 1f;
					System.Numerics.Vector3 position;
					if (flag)
					{
						float x = ((float)num8 * 137.50777f + (float)i * 53f) * ((float)Math.PI / 180f);
						float x2 = ((float)num8 * 71.337f + (float)i * 31f) * ((float)Math.PI / 180f);
						System.Numerics.Vector3 vector3 = new System.Numerics.Vector3(MathF.Cos(x) * MathF.Cos(x2), MathF.Sin(x2), MathF.Sin(x) * MathF.Cos(x2));
						position = vector3 * (num9 * num6 * 2.2f + num10 * num6 * 0.4f);
					}
					else
					{
						position = vector * (num9 * num6 * 2.2f) + vector2 * ((num10 - 0.5f) * num6 * 0.9f);
					}
					particleBillboardRenderer.Entity.Transform.Position = position;
					System.Numerics.Vector4 vector4 = MeshDecoder.SampleColorGradient(colorGradient, num9);
					float w = MathF.Sin(num9 * (float)Math.PI);
					float num11 = MeshDecoder.SampleSizeGradient(sizeGradient, num9);
					particleBillboardRenderer.Color = new System.Numerics.Vector4(vector4.X, vector4.Y, vector4.Z, w);
					particleBillboardRenderer.Entity.Transform.Scale = new System.Numerics.Vector3(MathF.Max(0.1f, 1.1f * num11));
					num8++;
					num2++;
				}
			}
		}
	}

	private abstract class CollisionShapeMarker : Component
	{
		public int SurfaceIndex = -1;

		public readonly List<int> OwnedVecIndices = new List<int>();

		public readonly List<int> OwnedTriIndices = new List<int>();
	}

	private sealed class CollisionBoxMarker : CollisionShapeMarker
	{
		public readonly Vector3[] Corners = DefaultCorners();

		public static Vector3[] DefaultCorners()
		{
			var c = new Vector3[8];
			for (int i = 0; i < 8; i++)
				c[i] = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? 0f : 2f, (i & 4) == 0 ? -1f : 1f);
			return c;
		}
	}

	private sealed class CollisionCylinderMarker : CollisionShapeMarker
	{
		public float Radius = 1f;
		public float Height = 2f;
		public int Segments = 12;
	}

	private sealed class CollisionPlaneMarker : CollisionShapeMarker
	{
		public float Width = 2f;
		public float Length = 2f;
	}

	private sealed class GeneratedCollisionMarker : CollisionShapeMarker
	{
		public readonly List<System.Numerics.Vector3> LocalVertices = new List<System.Numerics.Vector3>();
		public readonly List<(int A, int B, int C)> LocalTriangles = new List<(int, int, int)>();

		public List<int> DegenerateOnFirstSync = new List<int>();
	}

	private sealed class WorldLightingSettings : Component
	{
		public System.Numerics.Vector3 AmbientColor;

		public readonly List<(System.Numerics.Vector3 Color, System.Numerics.Vector3 Direction)> Directional = new List<(System.Numerics.Vector3, System.Numerics.Vector3)>();

		public float Intensity = 1f;

		public float SceneryBrightness = 1f;
		// Amedo 2026-09-21
		public float SceneryBakedBrightness = 1f;

		public Dictionary<object, List<Twinsanity.TwinsanityInterchange.Common.Vector4>>? SceneryBaseColors;

		public readonly List<GizmoRenderer> ArrowRenderers = new List<GizmoRenderer>();

		public int FogColorIndex = 5;

		public override void OnUpdate()
		{
			RenderPipeline instance = RenderPipeline.Instance;
			if (instance != null)
			{
				instance.AmbientLightColor = AmbientColor * Intensity;
				instance.DirectionalLights = Directional.Select(((System.Numerics.Vector3 Color, System.Numerics.Vector3 Direction) l) => (l.Color * Intensity, Direction: l.Direction)).ToList();
				int idx = Math.Clamp(FogColorIndex, 0, CrashEngine.Importer.MeshDecoder.FogColors.Length - 1);
				instance.FogColor = CrashEngine.Importer.MeshDecoder.FogColors[idx];
			}
		}
	}

	private sealed class GizmoRenderer : Component
	{
		public GpuMesh? Mesh { get; set; }

		public System.Numerics.Vector3 GizmoPos { get; set; }

		public float GizmoScale { get; set; } = 1f;


		public Quaternion GizmoRot { get; set; } = Quaternion.Identity;


		public bool Visible { get; set; } = false;


		public override void OnRender(GL gl)
		{
			if (Visible && Mesh != null)
			{
				RenderPipeline instance = RenderPipeline.Instance;
				if (instance != null)
				{
					TwinShaderProgram shader = instance.Shader;
					Matrix4x4 m = Matrix4x4.CreateScale(GizmoScale) * Matrix4x4.CreateFromQuaternion(GizmoRot) * Matrix4x4.CreateTranslation(GizmoPos);
					shader.Set("StartModel", m);
					shader.Set("twin_material.use_texture", 0f);
					shader.Set("twin_material.double_color", 1f);
					shader.Set("twin_material.alpha_test", 0f);
					shader.Set("twin_material.alpha_blend", 0f);
					shader.Set("twin_material.env_map", 0f);
					shader.Set("twin_material.metalic_specular", 0f);
					shader.Set("twin_material.deform_speed", System.Numerics.Vector2.Zero);
					shader.Set("twin_material.uv_scroll_speed", System.Numerics.Vector2.Zero);
					shader.Set("twin_material.reflect_dist", System.Numerics.Vector2.Zero);
					shader.Set("twin_material.billboard_render", 0f);
					shader.Set("twin_material.two_sided_lighting", 1);
					shader.Set("twin_material.perform_fog", 0f);
					shader.Set("twin_material.select_pulse", 1f);
					gl.Disable(EnableCap.CullFace);
					gl.DepthFunc(DepthFunction.Always);
					Mesh.Draw(gl);
					gl.DepthFunc(DepthFunction.Lequal);
				}
			}
		}
	}

	private sealed class TexturePickerLevelCache
	{
		public PS2AnyTwinsanityRM2? Rm2;

		public PS2AnyTwinsanitySM2? Sm2;

		public readonly List<(uint Id, Texture2D Thumb, int W, int H, bool IsScenery)> Entries = new List<(uint, Texture2D, int, int, bool)>();
	}

	private enum GizmoMode
	{
		Move,
		Rotate
	}

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
    private const bool ShowTooltips = false;
    private CameraComponent? _camera;

    private static readonly System.Numerics.Vector4 CollisionDebugColor = new(1f, 0.15f, 0.85f, 0.35f);

    private GpuMesh? _cubeMesh;
    private GpuMesh? _cubeWireMesh;
    private Texture2D? _cameraIconTexture;

    // Amedo 2026-09-21
    private Texture2D? _rotateIconTexture;
    private Texture2D? _moveIconTexture;
    private Texture2D? _lightIconTexture; // Amedo 2026-09-19
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
    private CrashEngine.Renderer.BlobShadowRenderer? _blobShadowRenderer;
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
    private TransformSnapshot _inspectorDragBefore;
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
    private string _skyTargetFilter = "";
    private string _lightTargetFilter = "";
    private bool _showTexturePicker;
    private Material? _texturePickerTargetMat;
    private string? _texturePickerLevel;
    private string _texturePickerLevelFilter = "";
    private string _texturePickerTexFilter = "";
    private readonly Dictionary<string, TexturePickerLevelCache> _texturePickerCache = new Dictionary<string, TexturePickerLevelCache>();
    private bool _showUiBrowser;
    private bool _showGameSettings;
    private bool _showGlobalData;
    private string _editMeshIdBuf = "";
    private System.Diagnostics.Process? _playerProcess;
    private readonly ImGuiFileBrowser _fileBrowser = new();
    private readonly Dictionary<uint, string> _scriptTextCache = new Dictionary<uint, string>();
    private readonly Dictionary<uint, CrashEngine.Importer.ScriptDumper.SpawnScriptInfo?> _spawnScriptInfoCache = new();
    private uint? _inspectedBehaviourId;
    private string _inspectedBehaviourText = "";
    private PS2BehaviourGraph? _inspectedBehaviourGraph;
    private string _inspectedBehaviourApplyMsg = "";
    private string _inspectedBehaviourOriginalText = "";
    private bool _inspectedBehaviourManual;
    private string _openBehIdText = "";
    private string _cubeTexPath = "";
    private uint? _ogiPreviewInstance;
    private uint? _ogiPreviewOgiId;

	private static void MaybeTooltip(string text)
	{
		bool flag = false;
	}


	private static readonly Dictionary<uint, string> MusicNames = new()
	{
		{ 7, "BP" }, { 8, "ClassroomCortex" }, { 9, "Henchmania" }, { 10, "WormChase" },
		{ 27, "TitleTheme" }, { 28, "Cavern" }, { 29, "BeeChase" }, { 30, "MechaBandicoot" },
		{ 31, "TotemRiver" }, { 32, "BossTikimon" }, { 33, "IcebergLab" }, { 34, "IceClimb" },
		{ 35, "BossUka" }, { 36, "WalrusChase" }, { 37, "Academy" }, { 38, "AcademyNoLaugh" },
		{ 39, "Undefined" }, { 40, "BossDingodile" }, { 41, "Rooftop" }, { 53, "IcebergLabFast" },
		{ 54, "SlipSlide" }, { 55, "BossNGin" }, { 56, "Hijinks" }, { 57, "Boiler" },
		{ 58, "ClassroomCrash" }, { 59, "BossAmberly" }, { 60, "AltLab" }, { 61, "Rockslide" },
		{ 62, "TwinsanityIsland" }, { 63, "AntAgony" }, { 64, "BossTwins" }, { 136, "BoilerUnused" },
	};

	private static (uint Id, int Count, bool FromDjInstance)? FindLevelMusic(PS2AnyTwinsanityRM2? rm2)
	{
		const int beginMusicCommandId = 88;
		if (rm2 is null) return null;

		var code = rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION);

		var objSec = code?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
		if (objSec is not null)
		{
			uint? djObjectId = null;
			for (int i = 0; i < objSec.GetItemsAmount(); i++)
				if (objSec.GetItem(i) is PS2AnyObject obj && obj.RefBehaviours.Contains((ushort)DjDefaultBehaviourId))
				{ djObjectId = obj.GetID(); break; }

			if (djObjectId is { } oid)
			{
				for (int lid = 0; lid <= 7; lid++)
				{
					var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
					var instSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
					if (instSec is null) continue;
					for (int i = 0; i < instSec.GetItemsAmount(); i++)
						if (instSec.GetItem(i) is PS2AnyInstance inst && inst.ObjectId == oid && inst.ParamList3.Count > 0)
							return (inst.ParamList3[0], 1, true);
				}
			}
		}

		var behSec = code?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
		if (behSec is null) return null;

		var tally = new Dictionary<uint, int>();
		for (int i = 0; i < behSec.GetItemsAmount(); i++)
		{
			if (behSec.GetItem(i) is not PS2BehaviourGraph g) continue;
			foreach (var state in g.ScriptStates)
				foreach (var body in state.Bodies)
					foreach (var cmd in body.Commands)
					{
						if (cmd.CommandIndex != beginMusicCommandId || cmd.Arguments.Count == 0) continue;
						var id = cmd.Arguments[0];
						tally[id] = tally.GetValueOrDefault(id) + 1;
					}
		}
		if (tally.Count == 0) return null;
		var best = tally.OrderByDescending(kv => kv.Value).First();
		return (best.Key, best.Value, false);
	}

	private static string GraphToEditableText(PS2BehaviourGraph g)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using (StreamWriter writer = new StreamWriter(memoryStream, Encoding.UTF8, 4096, leaveOpen: true))
		{
			g.WriteText(writer);
		}
		memoryStream.Position = 0L;
		using StreamReader streamReader = new StreamReader(memoryStream);
		return streamReader.ReadToEnd();
	}


	private void SelectAffected(IEditAction? action)
	{
		if (action == null)
		{
			return;
		}
		List<Entity> list = action.AffectedEntities.ToList();
		if (list.Count == 0)
		{
			return;
		}
		_selectedSet.Clear();
		foreach (Entity item in list)
		{
			_selectedSet.Add(item);
		}
		_selected = list[0];
		_revealSelectionInTree = true;
		SyncSelSnapshot();
	}

	// Amedo 2026-09-20
	private void SyncSelSnapshot()
	{
		_selSnapshot.Clear();
		_selSnapshot.AddRange(_selectedSet);
		_selSnapInit = true;
		_undoCountAtSync = _undoStack.Count;
	}

	private void RecordSelectionChangeIfAny()
	{
		if (!_selSnapInit) { SyncSelSnapshot(); return; }

		bool selChanged  = !(_selectedSet.Count == _selSnapshot.Count && _selectedSet.All(_selSnapshot.Contains));
		bool editHappened = _undoStack.Count != _undoCountAtSync;

		if (editHappened) { SyncSelSnapshot(); return; }
		if (!selChanged) return;

		var before = new List<Entity>(_selSnapshot);
		var after  = new List<Entity>(_selectedSet);
		_undoStack.Push(new SelectionChangeAction { Before = before, After = after, Apply = ApplySelectionList });
		SyncSelSnapshot();
	}

	private void ApplySelectionList(IReadOnlyList<Entity> sel)
	{
		_selectedSet.Clear();
		foreach (var e in sel) _selectedSet.Add(e);
		_selected = sel.Count > 0 ? sel[0] : null;
		_revealSelectionInTree = true;
		SyncSelSnapshot();
	}

	// Amedo 2026-09-20
	private void PushAddWithSelectionRestore(IEditAction addAction, Entity newEntity)
	{
		var before = _selectedSet.ToList();
		_selectedSet.Clear();
		_selectedSet.Add(newEntity);
		_selected = newEntity;
		_revealSelectionInTree = true;
		_undoStack.Push(new CompositeEditAction
		{
			Actions = new IEditAction[]
			{
				addAction,
				new SelectionChangeAction { Before = before, After = new List<Entity> { newEntity }, Apply = ApplySelectionList },
			},
			HandlesSelectionItself = true,
		});
		SyncSelSnapshot();
	}


	private void SelectRange(Entity from, Entity to)
	{
		int a = _treeVisibleOrder.IndexOf(from);
		int b = _treeVisibleOrder.IndexOf(to);
		if (a < 0 || b < 0)
		{
			SelectClicked(to, false);
			return;
		}
		int lo = Math.Min(a, b), hi = Math.Max(a, b);
		_selectedSet.Clear();
		for (int i = lo; i <= hi; i++)
		{
			_selectedSet.Add(_treeVisibleOrder[i]);
		}
		_selected = to;
	}

	private void SelectClicked(Entity? hit, bool additive)
	{
		if (!additive)
		{
			_selectedSet.Clear();
			_selected = hit;
			if (hit != null)
			{
				_selectedSet.Add(hit);
			}
		}
		else if (hit != null)
		{
			if (_selectedSet.Remove(hit))
			{
				_selected = ((_selectedSet.Count > 0) ? _selectedSet.First() : null);
				return;
			}
			_selectedSet.Add(hit);
			_selected = hit;
		}
	}


	private void PushInspectorUndoIfDeactivated(Entity e)
	{
		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			TransformSnapshot after = new TransformSnapshot(e.Transform);
			if (!after.Equals(in _inspectorDragBefore))
			{
				_undoStack.Push(new TransformEditAction
				{
					Entity = e,
					Before = _inspectorDragBefore,
					After = after
				});
			}
		}
	}


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
			LoadLevelEntities(gL, renderPipeline, useShadowDir: true);
			Entity entity6 = new Entity("Meter");
			entity6.Add(new GameHUD
			{
				Scene = this
			});
			AddRoot(entity6);
		}
	}


	private string ResolvePristinePackageSource()
	{
		string path = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build");
		string fileName = Path.GetFileName(_extractedRoot.TrimEnd('\\', '/'));
		string text = Path.Combine(path, "original_backup_" + fileName, "Crash.BD");
		return File.Exists(text) ? text : _extractedRoot;
	}


	private void LoadLevelEntities(GL gl, RenderPipeline pipeline, bool useShadowDir)
	{
		try
		{
			string text = (useShadowDir ? _extractedRoot : ResolvePristinePackageSource());
			_browser.Log("Opening package: " + text);
			using PackageReader packageReader = PackageReader.Open(text);
			if (useShadowDir)
			{
				packageReader.ShadowDir = SavedChunksDir;
			}
			_browser.Log($"{packageReader.Records.Count} files indexed" + (useShadowDir ? "" : "  [ignoring any saved edits — true pristine disc data]"));
			_browser.Log("Dumping scripts...");
			try
			{
				ScriptDumper.DumpFromPackage(packageReader, _rm2, _scriptOut);
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_rm2);
				string path = Path.Combine(_scriptOut, fileNameWithoutExtension);
				_scriptCount = (Directory.Exists(path) ? Directory.GetFiles(path, "*.lab").Length : 0);
				_browser.Log($"{_scriptCount} scripts → Scripts/{fileNameWithoutExtension}/");
			}
			catch (Exception ex)
			{
				_browser.Log("Script dump: " + ex.Message);
			}
			_browser.Log("Loading chunk " + _rm2 + "...");
			_reskinCloneOgi.Clear();
			_pristineRm2ForRecovery = null;
			_pristineRm2RecoveryFailed = false;
			ScriptDumper.ChunkScripts scripts;
			Entity entity = ChunkImporter.LoadChunk(gl, packageReader, _rm2, _sm2, out scripts);
			StripDanglingLinksForNewGamePreview(entity); // Amedo 2026-09-22
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
			entity4.Add(new SkydomeMarker());
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
			BuildCollisionTileMap(entity);
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
		}
		catch (Exception ex2)
		{
			_statusLine = "ERROR: " + ex2.Message;
			_browser.Log(_statusLine);
		}
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
		_collisionTileMap = null;
		_collisionTileBaselinePos.Clear();
		_undoStack.Clear();
		GL gL = Engine.Instance.GL;
		RenderPipeline renderPipeline = FindFirst<RenderPipeline>();
		if (renderPipeline == null)
		{
			_browser.Log("Reload Level: no RenderPipeline found (unexpected).");
			return;
		}
		_browser.Log(fromDiscOnly ? "Reloading level from the true disc data (ignoring any saved edits)..." : "Reloading level...");
		LoadLevelEntities(gL, renderPipeline, !fromDiscOnly);
	}


	protected override void OnUpdate()
	{
		RecordSelectionChangeIfAny(); // Amedo 2026-09-20

		if (_pendingOpenLevelPath is not null)
		{
			var openPath = _pendingOpenLevelPath;
			_pendingOpenLevelPath = null;
			_browser.OpenLevel(openPath);
			return;
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
		UpdateBlobShadowVisual();
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
		UpdateSelectionPulse();
		if (Input.KeyDown(Key.Escape))
		{
			if (_choosingDuplicateDirection)
			{
				_choosingDuplicateDirection = false;
			}
			else if (_glueMode)
			{
				_glueMode = false;
				_glueSource = null;
				_glueAxisLock = -1;
			}
			else
			{
				Engine.Instance.ActiveScene = _browser;
			}
		}
		if (_glueMode)
		{
			if (Input.KeyDown(Key.X))
			{
				_glueAxisLock = 0;
			}
			else if (Input.KeyDown(Key.Y))
			{
				_glueAxisLock = 1;
			}
			else if (Input.KeyDown(Key.Z))
			{
				_glueAxisLock = 2;
			}
		}
		else if ((Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight)) && Input.KeyDown(Key.G) && _selected != null && !_gizmoDragging)
		{
			_glueMode = true;
			_glueSource = _selected;
			_glueAxisLock = -1;
		}
		bool flag = Input.Mouse(MouseButton.Right);
		bool flag2 = Input.Mouse(MouseButton.Left);
		bool flag3 = flag2 && !_mouseWasDown;
		bool flag4 = !flag2 && _mouseWasDown;
		_mouseWasDown = flag2;
		int width = Engine.Instance.Width;
		int height = Engine.Instance.Height;
		System.Numerics.Vector2 mousePosition = Input.MousePosition;
		bool flag5 = !flag && !ImGui.GetIO().WantCaptureMouse;
		if (_gizmoRenderer != null)
		{
			_gizmoRenderer.Visible = _selected != null && !flag && !_choosingDuplicateDirection && _gizmoMode == GizmoMode.Move;
			if (_selected != null)
			{
				_gizmoRenderer.GizmoPos = GizmoPivot();
				_gizmoRenderer.GizmoScale = GizmoScale();
			}
		}
		if (_dupGizmoRenderer != null)
		{
			_dupGizmoRenderer.Visible = _choosingDuplicateDirection && !flag;
			if (_choosingDuplicateDirection)
			{
				_dupGizmoRenderer.GizmoPos = _dupGizmoPivot;
				_dupGizmoRenderer.GizmoScale = GizmoScale();
			}
		}
		WorldLightingSettings worldLightingSettings = _selected?.Get<WorldLightingSettings>();
		if (worldLightingSettings != null)
		{
			System.Numerics.Vector3 translation = _selected.Transform.World.Translation;
			float gizmoScale = GizmoScale() * 1.6f;
			for (int i = 0; i < worldLightingSettings.ArrowRenderers.Count && i < worldLightingSettings.Directional.Count; i++)
			{
				GizmoRenderer gizmoRenderer = worldLightingSettings.ArrowRenderers[i];
				gizmoRenderer.Visible = !flag;
				gizmoRenderer.GizmoPos = translation;
				gizmoRenderer.GizmoScale = gizmoScale;
				gizmoRenderer.GizmoRot = RotationBetween(System.Numerics.Vector3.UnitX, worldLightingSettings.Directional[i].Direction);
			}
			_lastLightingSelected = worldLightingSettings;
		}
		else if (_lastLightingSelected != null)
		{
			foreach (GizmoRenderer arrowRenderer in _lastLightingSelected.ArrowRenderers)
			{
				arrowRenderer.Visible = false;
			}
			_lastLightingSelected = null;
		}
		_gizmoHover = ((!(_selected != null && flag5) || _choosingDuplicateDirection) ? (-1) : ((_gizmoMode == GizmoMode.Move) ? GetGizmoHoverAxis(mousePosition, width, height) : GetRotateHoverAxis(mousePosition, width, height)));
		_dupGizmoHover = ((_choosingDuplicateDirection && flag5) ? GetDuplicateHoverDir(mousePosition, width, height) : (-1));
		if (flag3 && flag5)
		{
			if (_choosingDuplicateDirection)
			{
				if (_dupGizmoHover >= 0)
				{
					_lastDuplicateDirection = SixDirs[_dupGizmoHover];
					DuplicateSelected(_lastDuplicateDirection);
				}
				_choosingDuplicateDirection = false;
			}
			else if (_glueMode)
			{
				Entity entity = ((_camera != null) ? RaycastScene(mousePosition, width, height) : null);
				if (entity != null && entity != _glueSource && _glueSource != null)
				{
					ExecuteGlue(_glueSource, entity);
					_glueMode = false;
					_glueSource = null;
					_glueAxisLock = -1;
				}
			}
			else
			{
				int num = ((_selected == null) ? (-1) : ((_gizmoMode == GizmoMode.Move) ? GetGizmoHoverAxis(mousePosition, width, height) : GetRotateHoverAxis(mousePosition, width, height)));
				if (num >= 0)
				{
					_gizmoDragging = true;
					_gizmoAxis = num;
					_gizmoPrevMouse = mousePosition;
					_gizmoDragBefore.Clear();
					foreach (Entity item2 in _selectedSet)
					{
						_gizmoDragBefore[item2] = new TransformSnapshot(item2.Transform);
					}
				}
				else
				{
					_gizmoDragging = false;
					_gizmoAxis = -1;
					Entity hit = HitTestCameraIcons(mousePosition, width, height)
						?? ((_camera != null) ? RaycastScene(mousePosition, width, height) : null);
					bool additive = Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight);
					SelectClicked(hit, additive);
					if (_selected != null)
					{
						_revealSelectionInTree = true;
					}
				}
			}
		}
		if (_gizmoDragging && flag2 && _selected != null)
		{
			if (_gizmoMode == GizmoMode.Move)
			{
				UpdateGizmoDrag(mousePosition, width, height);
			}
			else
			{
				UpdateRotateDrag(mousePosition, width, height);
			}
		}
		if (flag4)
		{
			if (_gizmoDragging)
			{
				List<IEditAction> list = new List<IEditAction>();
				foreach (KeyValuePair<Entity, TransformSnapshot> item3 in _gizmoDragBefore)
				{
					item3.Deconstruct(out var key, out var value);
					Entity entity2 = key;
					TransformSnapshot other = value;
					TransformSnapshot after = new TransformSnapshot(entity2.Transform);
					if (!after.Equals(in other))
					{
						list.Add(new TransformEditAction
						{
							Entity = entity2,
							Before = other,
							After = after
						});
					}
				}
				if (list.Count == 1)
				{
					_undoStack.Push(list[0]);
				}
				else if (list.Count > 1)
				{
					_undoStack.Push(new CompositeEditAction
					{
						Actions = list
					});
				}
			}
			_gizmoDragging = false;
			_gizmoAxis = -1;
		}
		if (Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight))
		{
			if (Input.KeyDown(Key.Z) && (Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight)))
			{
				SelectAffected(_undoStack.Redo());
			}
			else if (Input.KeyDown(Key.Z))
			{
				SelectAffected(_undoStack.Undo());
			}
			else if (Input.KeyDown(Key.Y))
			{
				SelectAffected(_undoStack.Redo());
			}
			else if (!_choosingDuplicateDirection && ((Input.KeyDown(Key.D) && Input.KeyHeld(Key.A)) || (Input.KeyDown(Key.A) && Input.KeyHeld(Key.D))))
			{
				BeginDuplicatePick();
			}
			else if (!_choosingDuplicateDirection && Input.KeyDown(Key.D) && !Input.KeyHeld(Key.A))
			{
				DuplicateSelected(_lastDuplicateDirection);
			}
		}
		if (Input.KeyDown(Key.Delete))
		{
			DeleteSelected();
		}
	}


	private void SpawnCube(System.Numerics.Vector3 pos, bool asCollisionBox = false)
	{
		_cubeCount++;
		Entity entity = new Entity(asCollisionBox ? $"CollisionBox_{_cubeCount}" : $"Cube_{_cubeCount}");
		entity.Transform.Position = pos;
		var marker = asCollisionBox ? new CollisionBoxMarker() : null;
		DirectCubeRenderer directCubeRenderer = entity.Add(new DirectCubeRenderer
		{
			Mesh = asCollisionBox ? BuildCollisionBoxMesh(Engine.Instance.GL, marker!.Corners) : _cubeMesh,
			Color = (asCollisionBox ? CollisionDebugColor : new System.Numerics.Vector4(1f, 0.85f, 0f, 1f)),
			OwnsMesh = asCollisionBox
		});
		if (asCollisionBox)
		{
			directCubeRenderer.Mat.AlphaBlend = true;
		}
		var collisionParent = asCollisionBox ? GetOrCreateCollisionRoot() : null;
		if (collisionParent is not null)
			collisionParent.AddChild(entity);
		else
			LoadAndAddRoot(entity);
		_cubes.Add(new CubeEntry(entity, asCollisionBox ? $"Collision Box {_cubeCount}" : $"Cube {_cubeCount}", directCubeRenderer));
		if (marker is not null)
		{
			entity.Add(marker);
		}
	}

	private void AttachToCollisionParentOrRoot(Entity entity)
	{
		var collisionParent = GetOrCreateCollisionRoot();
		if (collisionParent is not null)
			collisionParent.AddChild(entity);
		else
			LoadAndAddRoot(entity);
	}

	private Entity? GetOrCreateCollisionRoot()
	{
		var cr = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
		if (cr is null) return null;
		var existing = AllEntities(cr).FirstOrDefault(e => e.Has<CollisionMesh>());
		if (existing is not null) return existing;
		var collRoot = new Entity("Collision") { Active = _showCollision };
		collRoot.Add(new CollisionMesh());
		cr.AddChild(collRoot);
		return collRoot;
	}

	private void SpawnCollisionCylinder(System.Numerics.Vector3 pos)
	{
		_cubeCount++;
		Entity entity = new Entity($"CollisionCylinder_{_cubeCount}");
		entity.Transform.Position = pos;
		var marker = new CollisionCylinderMarker();
		DirectCubeRenderer directCubeRenderer = entity.Add(new DirectCubeRenderer
		{
			Mesh = BuildCollisionCylinderMesh(Engine.Instance.GL, marker.Radius, marker.Height, marker.Segments),
			Color = CollisionDebugColor,
			OwnsMesh = true
		});
		directCubeRenderer.Mat.AlphaBlend = true;
		AttachToCollisionParentOrRoot(entity);
		_cubes.Add(new CubeEntry(entity, $"Collision Cylinder {_cubeCount}", directCubeRenderer));
		entity.Add(marker);
	}

	private void SpawnCollisionPlane(System.Numerics.Vector3 pos)
	{
		_cubeCount++;
		Entity entity = new Entity($"CollisionPlane_{_cubeCount}");
		entity.Transform.Position = pos;
		var marker = new CollisionPlaneMarker();
		DirectCubeRenderer directCubeRenderer = entity.Add(new DirectCubeRenderer
		{
			Mesh = BuildCollisionPlaneMesh(Engine.Instance.GL, marker.Width, marker.Length),
			Color = CollisionDebugColor,
			OwnsMesh = true
		});
		directCubeRenderer.Mat.AlphaBlend = true;
		AttachToCollisionParentOrRoot(entity);
		_cubes.Add(new CubeEntry(entity, $"Collision Plane {_cubeCount}", directCubeRenderer));
		entity.Add(marker);
	}


	private CubeEntry? CubeFor(Entity? e)
	{
		Entity e2 = e;
		return (e2 == null) ? null : _cubes.FirstOrDefault((CubeEntry c) => c.Ent == e2);
	}


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
			var chainChunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
			if (chainChunkRoot is not null)
			{
				var chainPosEnts = AllEntities(chainChunkRoot)
					.Where(x => x.Has<CrashEngine.Importer.PositionMarker>())
					.ToDictionary(x => x.Get<CrashEngine.Importer.PositionMarker>()!.Source.GetID(), x => x);

				points.Add(_selected!.Transform.World.Translation);
				foreach (var pid in chainInst.Source.Positions)
				{
					if (!chainPosEnts.TryGetValue(pid, out var posEnt)) continue;
					points.Add(posEnt.Transform.World.Translation);
				}
			}
		}
	}

	private void UpdateBlobShadowVisual()
	{
		if (_blobShadowRenderer is null)
		{
			var host = new Entity("__BlobShadowViz");
			_blobShadowRenderer = host.Add(new CrashEngine.Renderer.BlobShadowRenderer());
			LoadAndAddRoot(host);
		}

		_blobShadowRenderer.Visible = false;

		var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
		if (chunkRoot is null) return;

		var crashEnt = AllEntities(chunkRoot).FirstOrDefault(e => e.Get<InstanceData>()?.ObjectId == 0x0000);
		if (crashEnt is null) return;

		var collRoot = AllEntities(chunkRoot).FirstOrDefault(e => e.Has<CrashEngine.Importer.CollisionMesh>());
		var collMr = collRoot?.Children.FirstOrDefault()?.Get<CrashEngine.Importer.MeshRenderer>();
		if (collMr is null) return;

		var origin = crashEnt.Transform.World.Translation + Vector3.UnitY * 0.5f;
		if (!RaycastMeshRenderer(collMr, origin, -Vector3.UnitY, out float dist) || dist > 20f) return;

		_blobShadowRenderer.Visible = true;
		_blobShadowRenderer.GroundPoint = origin - Vector3.UnitY * dist;
		_blobShadowRenderer.GroundNormal = Vector3.UnitY;
	}

	private bool WorldToScreen(System.Numerics.Vector3 world, int sw, int sh, out System.Numerics.Vector2 screen)
	{
		screen = System.Numerics.Vector2.Zero;
		if (_camera == null)
		{
			return false;
		}
		System.Numerics.Vector4 vector = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(world, 1f), _camera.View * _camera.Projection);
		if (vector.W <= 0f)
		{
			return false;
		}
		float num = vector.X / vector.W;
		float num2 = vector.Y / vector.W;
		if (num < -1f || num > 1f || num2 < -1f || num2 > 1f)
		{
			return false;
		}
		screen = new System.Numerics.Vector2((num + 1f) * 0.5f * (float)sw, (1f - num2) * 0.5f * (float)sh);
		return true;
	}

	private Entity? HitTestCameraIcons(System.Numerics.Vector2 mouse, int sw, int sh)
	{
		if (_camerasRoot is not { Active: true } camerasRoot) return null;
		foreach (var child in camerasRoot.Children)
		{
			if (!WorldToScreen(child.Transform.World.Translation, sw, sh, out var screen)) continue;
			if (mouse.X >= screen.X - 11f && mouse.X <= screen.X + 11f &&
			    mouse.Y >= screen.Y - 12.5f && mouse.Y <= screen.Y + 7.5f)
				return child;
		}
		return null;
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


	private static string? ResolveCrashPlayerExePath()
	{
		var net9Dir = new DirectoryInfo(AppContext.BaseDirectory);
		var configDir = net9Dir.Parent;
		var launcherProjDir = configDir?.Parent?.Parent;
		var repoRoot = launcherProjDir?.Parent;
		var documentsRoot = repoRoot?.Parent;
		if (documentsRoot is null || configDir is null) return null;
		var candidate = Path.Combine(documentsRoot.FullName, "CrashPlayer", "bin", configDir.Name, net9Dir.Name, "CrashPlayer.exe");
		return File.Exists(candidate) ? candidate : null;
	}

	private void LaunchPlayer()
	{
		if (_playerProcess is { HasExited: false }) { _browser.Log("Launch Player: already running."); return; }

		var exePath = ResolveCrashPlayerExePath();
		if (exePath is null) { _browser.Log("Launch Player: CrashPlayer.exe not found -- build the CrashPlayer project first."); return; }

		var psi = new System.Diagnostics.ProcessStartInfo
		{
			FileName = exePath,
			ArgumentList = { _extractedRoot, _rm2, _sm2 },
			UseShellExecute = false,
		};
		try
		{
			_playerProcess = System.Diagnostics.Process.Start(psi);
			_browser.Log($"Launch Player: started CrashPlayer.exe (pid {_playerProcess?.Id}) -- '{_rm2}' from '{_extractedRoot}'.");
		}
		catch (Exception ex)
		{
			_browser.Log("Launch Player: failed to start -- " + ex.Message);
		}
	}

	private void StopPlayer()
	{
		if (_playerProcess is not { HasExited: false }) { _browser.Log("Stop Player: not running."); return; }
		try { _playerProcess.Kill(entireProcessTree: true); _browser.Log("Stop Player: stopped."); }
		catch (Exception ex) { _browser.Log("Stop Player: failed to stop -- " + ex.Message); }
	}

	private void AddGlobalObjectInstance(uint objectId, string label)
	{
		Entity entity = base.Roots.FirstOrDefault((Entity r) => r.Has<ChunkSource>());
		ChunkSource chunkSource = entity?.Get<ChunkSource>();
		if (entity == null || chunkSource == null)
		{
			_browser.Log("Add Object: no level currently loaded.");
			return;
		}
		BaseTwinSection baseTwinSection = (from e in AllEntities(entity)
			select e.Get<InstanceData>()?.Section).FirstOrDefault((BaseTwinSection s) => s != null);
		if (baseTwinSection == null)
		{
			baseTwinSection = chunkSource.Rm2.GetItem<BaseTwinSection>(0u)?.GetItem<BaseTwinSection>(6u);
			if (baseTwinSection == null)
			{
				_browser.Log("Add Object: this level has no instances section to add to.");
				return;
			}
		}
		System.Numerics.Vector3 vector = _camera?.Transform.Position ?? System.Numerics.Vector3.Zero;

		PS2AnyInstance? srcInst = null;
		for (int lid = 0; lid <= 7 && srcInst is null; lid++)
		{
			var srcLayout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)lid);
			var srcInstSec = srcLayout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
			if (srcInstSec is null) continue;
			for (int i = 0; i < srcInstSec.GetItemsAmount(); i++)
			{
				if (srcInstSec.GetItem(i) is PS2AnyInstance cand && cand.ObjectId == (ushort)objectId) { srcInst = cand; break; }
			}
		}

		PS2AnyInstance pS2AnyInstance = new PS2AnyInstance
		{
			Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(vector.X, vector.Y, vector.Z, 1f),
			RotationX = new TwinIntegerRotation(),
			RotationY = new TwinIntegerRotation(),
			RotationZ = new TwinIntegerRotation(),
			ObjectId = (ushort)objectId,
			RefListIndex = srcInst?.RefListIndex ?? 0,
			OnSpawnHeaderScriptID = srcInst?.OnSpawnHeaderScriptID ?? ushort.MaxValue,
			StateFlags = srcInst?.StateFlags ?? 0u,
			InstancesRelated = 0u,
			Instances = new List<ushort>(),
			PositionsRelated = 0u,
			Positions = new List<ushort>(),
			PathsRelated = 0u,
			Paths = new List<ushort>(),
			ParamList1 = srcInst is not null ? new List<uint>(srcInst.ParamList1) : new List<uint>(),
			ParamList2 = srcInst is not null ? new List<float>(srcInst.ParamList2) : new List<float>(),
			ParamList3 = srcInst is not null ? new List<uint>(srcInst.ParamList3) : new List<uint>()
		};

		Entity parent = entity.Children.FirstOrDefault((Entity c) => c.Name == "Instances") ?? entity;
		Entity entity2 = AddOrReuseInstance(baseTwinSection, pS2AnyInstance, parent, chunkSource);
		SelectClicked(entity2, additive: false);
		_browser.Log($"Add Object: placed a new '{label}' (id 0x{objectId:X4}) at the camera position. " + "Not on the Undo stack yet (Delete Selected still removes it manually). Save Chunk to keep it.");
	}


	private static uint GenerateUniqueInstanceId(BaseTwinSection section)
	{
		uint num = 0;
		while (section.ContainsItem(num)) num++;
		return num;
	}

	private Entity AddOrReuseInstance(BaseTwinSection section, PS2AnyInstance built, Entity instRoot, ChunkSource chunkSource)
	{
		PS2AnyInstance? slot = null;
		for (int i = 0; i < section.GetItemsAmount(); i++)
			if (section.GetItem(i) is PS2AnyInstance cand && cand.StateFlags == 0) { slot = cand; break; }

		PS2AnyInstance target;
		bool reused = slot is not null;
		if (slot is not null)
		{
			target = slot;
			target.Position              = built.Position;
			target.RotationX             = built.RotationX;
			target.RotationY             = built.RotationY;
			target.RotationZ             = built.RotationZ;
			target.ObjectId              = built.ObjectId;
			target.RefListIndex          = built.RefListIndex;
			target.OnSpawnHeaderScriptID = built.OnSpawnHeaderScriptID;
			target.StateFlags            = built.StateFlags;
			target.InstancesRelated      = built.InstancesRelated;
			target.Instances             = built.Instances;
			target.PositionsRelated      = built.PositionsRelated;
			target.Positions             = built.Positions;
			target.PathsRelated          = built.PathsRelated;
			target.Paths                 = built.Paths;
			target.ParamList1            = built.ParamList1;
			target.ParamList2            = built.ParamList2;
			target.ParamList3            = built.ParamList3;
		}
		else
		{
			target = built;
			target.SetID(GenerateUniqueInstanceId(section));
			section.AddItem(target);
		}

		var entity = ChunkImporter.ImportInstance(target, instRoot, new Dictionary<uint, PatrolPath>(), section, isUserAdded: !reused);
		if (chunkSource.MeshTables is not null)
			MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, chunkSource.MeshTables, entity);
		if (reused)
			_browser.Log($"Reused a previously-deleted native slot (id 0x{target.GetID():X4}) instead of adding a new one.");
		return entity;
	}


	private bool _defaultLayoutApplied;
	private const string DefaultDockLayout =
@"[Window][##dockhost]
Pos=0,32
Size=1920,977
Collapsed=0

[Window][##topbar]
Pos=0,0
Size=1920,32
Collapsed=0

[Window][Scene##panel]
Pos=0,32
Size=325,977
Collapsed=0
DockId=0x00000003,0

[Window][Inspector##panel]
Pos=1505,32
Size=415,977
Collapsed=0
DockId=0x00000006,0

[Window][Assets]
Pos=327,762
Size=1176,247
Collapsed=0
DockId=0x00000001,0

[Window][Output]
Pos=327,762
Size=1176,247
Collapsed=0
DockId=0x00000001,1

[Docking][Data]
DockSpace       ID=0x50DE06D3 Window=0x5B220BC7 Pos=0,32 Size=1920,977 Split=X
  DockNode      ID=0x00000005 Parent=0x50DE06D3 SizeRef=1503,977 Split=X
    DockNode    ID=0x00000003 Parent=0x00000005 SizeRef=325,977 Selected=0x32F5ED47
    DockNode    ID=0x00000004 Parent=0x00000005 SizeRef=1593,977 Split=Y
      DockNode  ID=0x00000002 Parent=0x00000004 SizeRef=1920,728 CentralNode=1
      DockNode  ID=0x00000001 Parent=0x00000004 SizeRef=1920,247 Selected=0x26CE0345
  DockNode      ID=0x00000006 Parent=0x50DE06D3 SizeRef=415,977 Selected=0x58565765
";

	public override void OnImGuiRender()
	{
		if (!_defaultLayoutApplied) { ImGui.LoadIniSettingsFromMemory(DefaultDockLayout); _defaultLayoutApplied = true; }
		DrawSaveConfirmPopup();
		DrawResetAllConfirmPopup();
		DrawBuildProgress();
		int width = Engine.Instance.Width;
		int height = Engine.Instance.Height;
		float y = (float)height - 32f - 180f - 2f;
		ImGui.SetNextWindowPos(new System.Numerics.Vector2(0f, 0f));
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(width, 32f));
		ImGui.Begin("##topbar", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus);
		float value = ((EngineTime.Delta > 0f) ? (1f / EngineTime.Delta) : 0f);
		int drawCallsThisFrame = CrashEngine.Importer.MeshRenderer.DrawCallsThisFrame;
		ImGui.Text($"  {_rm2}  |  {_enemyCount} enemies  |  {_scriptCount} scripts" +
		           (_musicLabel is not null ? $"  |  Music: {_musicLabel}" : ""));
		ImGui.SameLine();
		if (ImGui.SmallButton("UI##uibrowserbtn"))
		{
			_showUiBrowser = !_showUiBrowser;
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Browse the game's own loading screens, icons, decals, and fonts —\nreal assets decoded straight from disc, same as the game shows them.");
		}
		ImGui.SameLine();
		if (ImGui.SmallButton("Game Settings##gamesettingsbtn"))
		{
			_showGameSettings = !_showGameSettings;
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Gameplay-rule toggles — real script edits to shared behaviour\ngraphs (Startup\\Default.rm2), not per-object Inspector tweaks.");
		}
		ImGui.SameLine();
		if (ImGui.SmallButton("Default.rm2 Data##globaldatabtn"))
		{
			_showGlobalData = !_showGlobalData;
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Browse Startup\\Default.rm2's own shared objects/behaviours/sounds\ndirectly by list — this data has no Layout/scenery of its own, so\nthere's nothing to click on in the 3D view the way a per-level\nobject works.");
		}
		ImGui.SameLine();
		if (ImGui.SmallButton("About##aboutbtn"))
		{
			_showAbout = !_showAbout;
		}
		ImGui.SameLine();
		if (ImGui.SmallButton("Shortcuts##shortcutsbtn")) // Amedo 2026-09-22
		{
			_showShortcuts = !_showShortcuts;
		}
		ImGui.SameLine();
		if (ImGui.SmallButton("Reset Layout##resetlayoutbtn"))
		{
			ImGui.LoadIniSettingsFromMemory(DefaultDockLayout); // Amedo 2026-09-19
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Restore the default panel layout — Scene/Hierarchy left,\nInspector right, Assets+Output docked at the bottom.");
		}
		if (_glueMode)
		{
			ImGui.SameLine();
			int glueAxisLock = _glueAxisLock;
			if (1 == 0)
			{
			}
			string text = glueAxisLock switch
			{
				0 => "X", 
				1 => "Y", 
				2 => "Z", 
				_ => "auto", 
			};
			if (1 == 0)
			{
			}
			string text2 = text;
			ImGui.TextColored(new System.Numerics.Vector4(1f, 0.85f, 0.2f, 1f), "  |  GLUE MODE (axis: " + text2 + ") — click target, X/Y/Z=lock axis, Esc=cancel");
		}
		ImGui.SameLine((float)width - 430f);
		ImGui.BeginDisabled(!_undoStack.CanUndo);
		if (ImGui.SmallButton("Undo"))
		{
			SelectAffected(_undoStack.Undo());
		}
		ImGui.EndDisabled();
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Ctrl+Z");
		}
		ImGui.SameLine();
		ImGui.BeginDisabled(!_undoStack.CanRedo);
		if (ImGui.SmallButton("Redo"))
		{
			SelectAffected(_undoStack.Redo());
		}
		ImGui.EndDisabled();
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Ctrl+Y");
		}
		ImGui.SameLine((float)width - 320f);
		string camSpeedText = (_camera != null) ? $"Cam speed: {_camera.Speed:F1}  |  " : "";
		if (drawCallsThisFrame == 0)
		{
			ImGui.TextColored(new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f), $"{camSpeedText}draws: {drawCallsThisFrame}  FPS {value:F0}  ESC=back");
		}
		else
		{
			ImGui.TextDisabled($"{camSpeedText}draws: {drawCallsThisFrame}  FPS {value:F0}  ESC=back");
		}
		ImGui.End();
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
		ImGui.End();
		ImGui.SetNextWindowPos(new System.Numerics.Vector2(0f, 32f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(280f, y), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(180f, 120f), new System.Numerics.Vector2(float.MaxValue, float.MaxValue));
		ImGui.Begin("Scene##panel", ImGuiWindowFlags.NoCollapse);
		// Amedo 2026-09-21
		{
			float gizmoHalf = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
			bool gizmoIsMove = _gizmoMode == GizmoMode.Move;
			System.Numerics.Vector4 gizmoActiveCol = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive];
			float gizmoIconSz = ImGui.GetFontSize();
			if (gizmoIsMove) ImGui.PushStyleColor(ImGuiCol.Button, gizmoActiveCol);
			bool moveClicked;
			if (_moveIconTexture != null)
			{
				moveClicked = ImGui.ImageButton("##movegizmo", (nint)_moveIconTexture.GlId,
					new System.Numerics.Vector2(gizmoIconSz, gizmoIconSz),
					new System.Numerics.Vector2(0f, 0f), new System.Numerics.Vector2(1f, 1f),
					new System.Numerics.Vector4(0f, 0f, 0f, 0f), new System.Numerics.Vector4(1f, 1f, 1f, 1f));
			}
			else
			{
				moveClicked = ImGui.Button("Position", new System.Numerics.Vector2(gizmoHalf, 0f));
			}
			if (gizmoIsMove) ImGui.PopStyleColor();
			if (ImGui.IsItemHovered())
			{
				ImGui.SetTooltip("Position");
			}
			if (moveClicked)
			{
				_gizmoMode = GizmoMode.Move;
			}
			ImGui.SameLine();
			if (!gizmoIsMove) ImGui.PushStyleColor(ImGuiCol.Button, gizmoActiveCol);
			float rotIconSz = ImGui.GetFontSize();
			bool rotClicked;
			if (_rotateIconTexture != null)
			{
				rotClicked = ImGui.ImageButton("##rotgizmo", (nint)_rotateIconTexture.GlId,
					new System.Numerics.Vector2(rotIconSz, rotIconSz),
					new System.Numerics.Vector2(0f, 0f), new System.Numerics.Vector2(1f, 1f),
					new System.Numerics.Vector4(0f, 0f, 0f, 0f), new System.Numerics.Vector4(1f, 1f, 1f, 1f));
			}
			else
			{
				rotClicked = ImGui.Button("Rotation", new System.Numerics.Vector2(gizmoHalf, 0f));
			}
			if (!gizmoIsMove) ImGui.PopStyleColor();
			if (ImGui.IsItemHovered())
			{
				ImGui.SetTooltip("Rotation");
			}
			if (rotClicked)
			{
				_gizmoMode = GizmoMode.Rotate;
			}
		}
		ImGui.Separator();
		if (ImGui.Button("Deselect", new System.Numerics.Vector2(-1f, 0f)))
		{
			SelectClicked(null, additive: false);
		}

		if (ImGui.Button("Show Scripts (names)", new System.Numerics.Vector2(-1f, 0f)))
		{
			_showScriptsBrowser = true;
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Lists every item in this chunk's CODE_BEHAVIOURS_SECTION. PS2BehaviourGraph\nitems (odd id) store a REAL name string in the file itself — shown here\ninstead of just the hex id. TwinBehaviourStarter items (even id) have no\nName field in the format at all, shown as \"(starter, no name)\".");
		}
		ImGui.BeginDisabled(_selectedSet.Count == 0);
		if (ImGui.Button("Delete Selected", new System.Numerics.Vector2(-1f, 0f)))
		{
			DeleteSelected();
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Del key also works. Only real PS2 objects/scenery tiles are\nremoved from the archive — cubes just disappear from the editor.");
		}
		ImGui.EndDisabled();

		ImGui.Spacing();
		ImGui.TextColored(new System.Numerics.Vector4(1f, 0.82f, 0.15f, 1f), "CrashEngine");
		ImGui.Separator();

		if (ImGui.CollapsingHeader("File"))
		{
			ImGui.Indent();
			ImGui.BeginDisabled(_building);
			if (ImGui.Button("Save Chunk (.rm2/.sm2)", new System.Numerics.Vector2(-1f, 0f)))
			{
				SaveChunk();
			}
			if (ImGui.Button("Reload Level (keep saved edits)", new System.Numerics.Vector2(-1f, 0f)))
			{
				ReloadLevel(fromDiscOnly: false);
			}
			if (ImGui.Button("Reload Level from Disc (discard saved edits)", new System.Numerics.Vector2(-1f, 0f)))
			{
				ReloadLevel(fromDiscOnly: true);
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Re-extracts just THIS level fresh from the real disc archive —\nignores SavedChunks entirely. Use this to recover from a broken\nsaved edit (e.g. a bad texture import) without redoing the whole\nCreate Project extraction. Any unsaved live edits on this level\nare lost too.");
			}
			ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.15f, 0.15f, 1f));
			if (ImGui.Button("Reset ALL Levels From Disc (delete all saved edits)", new System.Numerics.Vector2(-1f, 0f)))
			{
				_openResetAllConfirmPopup = true;
			}
			ImGui.PopStyleColor();
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("PROJECT-WIDE, not just this level: permanently deletes the entire\nSavedChunks folder (every level's saved edits) so everything reads\nfresh from the real disc archive again. No backup, no Undo — asks\nfor confirmation first.");
			}
			EnsureIsoOutputPathLoaded(); // Amedo 2026-09-20
			ImGui.TextDisabled("ISO output (blank = default path):");
			ImGui.SetNextItemWidth(-32f);
			if (ImGui.InputText("##isoOutPath", ref _isoOutputPath, 512u))
				SaveIsoOutputPath();
			ImGui.SameLine();
			if (ImGui.Button("...##isoOutBrowse", new System.Numerics.Vector2(-1f, 0f)))
			{
				ShowSaveFileDialog("Choose ISO output file (overwritten every build)", "PS2 ISO\0*.iso\0All Files\0*.*\0\0", "game_test.iso", text3 =>
				{
					if (text3 != null)
					{
						_isoOutputPath = text3;
						SaveIsoOutputPath();
					}
				});
			}
			if (ImGui.Button("Build ISO (PS2)", new System.Numerics.Vector2(-1f, 0f)))
			{
				BuildIso();
			}
			ImGui.EndDisabled();
			if (ImGui.Button("Export Collision to OBJ...", new System.Numerics.Vector2(-1f, 0f)))
			{
				ExportCollisionToObj();
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Writes this level's ENTIRE current collision (Coll.Triangles/\nColl.Vectors) to a plain .obj file — edit it in Blender (move/\nsculpt/delete verts, add new geometry, anything), then use\n\"Import Collision from OBJ...\" to bring it back. Vertex ORDER is\npreserved exactly (needed so an edited-in-place OBJ round-trips\ncorrectly) — don't reorder/reindex vertices in Blender (a plain\nedit that doesn't add/remove verts is always safe).");
			}
			if (ImGui.Button("Import Collision from OBJ...", new System.Numerics.Vector2(-1f, 0f)))
			{
				ImportCollisionFromObj();
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Replaces this level's ENTIRE collision (Coll.Triangles/Coll.Vectors,\nColl.Groups rebuilt as one big group) from a .obj file — use\nthe SAME file \"Export Collision to OBJ...\" wrote (edited in\nBlender) so vertex count/order still lines up with anything else\nthat might reference it. Triggers are left untouched. Save Chunk +\nBuild ISO to test.");
			}
			if (ImGui.Button("Import Object (OBJ + textures)...", new System.Numerics.Vector2(-1f, 0f)))
			{
				bool flipX = _flipModelX;
				ShowOpenFileDialog("Import Object (real, as PS2 Object)", ModelImporter.FileFilter, modelPath =>
				{
					if (modelPath != null) BakeExternalModel(modelPath, flipX);
				});
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Imports a mesh file (.obj + its .mtl + textures, or .glb/.fbx/etc.) as\na REAL PS2 RigidModel/OGI/Object, placed as a normal Instance at\nthe camera — same real pipeline \"+ Add Model (real, as Object)\"\nabove uses. Unlike the Inspector's own \"Export Object\" round-trip\npreview, this DOES survive Save Chunk + Build ISO.");
			}
			ImGui.Unindent();
		}

		if (ImGui.CollapsingHeader("Add"))
		{
		if (ImGui.Button("+ Cube", new System.Numerics.Vector2(134f, 0f)))
		{
			_pendingSpawn = true;
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("A real, unit-sized (1x1x1) box baked as a genuine PS2 RigidModel/\nOGI/Object and placed as a normal Instance at the camera's current\nposition — same real pipeline \"+ Add Model\" uses, procedural geometry\ninstead of an imported file. Starts with a real, opaque solid-gray\ntexture+material, so Import Texture (Inspector) can replace it with\nanything afterward, exactly like any other real object (max 512x512).\nSave Chunk to keep it, then Build ISO + test.");
		}
		if (ImGui.Button("+ Add Crash (full, with scripts)", new System.Numerics.Vector2(-1f, 0f)))
		{
			_showCrashPicker = true;
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Crash is defined LOCALLY per-level (not shared like Wumpa/Nitro) —\nthis does a real full transplant (object + OGIs + animations +\nbehaviours/scripts + sounds + everything it references) from a\nlevel that already has a real, working Crash. First attempt at a\nfull script transplant this session — not guaranteed to work\nperfectly the first try, see the Add Crash log message.");
		}
		if (ImGui.Button("+ Add Object (full, from another level)", new System.Numerics.Vector2(-1f, 0f)))
		{
			_objectTransplantLevelFilter = "";
			_showObjectTransplantLevelPicker = true;
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Same full transplant as Add Crash (object + OGIs + animations +\nbehaviours/scripts + sounds + everything it references), but for ANY\nobject in ANY level — pick the source level, then pick the object by\nname. Copies the source's own Instance config (StateFlags/ParamLists)\ntoo, same as Add Crash — needed for it to behave correctly, not just\nrender. Save Chunk to keep it, then Build ISO + test.");
		}
		if (ImGui.Button("+ Add Trigger (from another level)", new System.Numerics.Vector2(-1f, 0f)))
		{
			_triggerTransplantLevelFilter = "";
			_showTriggerTransplantLevelPicker = true;
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Same source-level browse as Add Object, but for a Trigger volume —\npick the source level, then pick the trigger by id/messages. Copies\nits volume shape + TriggerMessages. If you added an object with\n\"Add Object\" FIRST (same session, not saved/reloaded since), the new\ntrigger's own Instances list is automatically pointed at THAT\nobject's new instance id, so touching it fires the same messages the\nobject's script listens for — do Add Object, then Add Trigger, in\nthat order. Otherwise the source trigger's own (likely wrong once\ncopied) Instances list is kept as-is and logged as a warning. Save\nChunk to keep it, then Build ISO + test.");
		}
		if (_importError != null)
		{
			ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), _importError);
		}
		float flipW = ImGui.CalcTextSize("Flip X (fix mirror)").X + ImGui.GetFrameHeight()
		              + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetStyle().ItemSpacing.X;
		if (ImGui.Button("+ Add Model (real, as Object)", new System.Numerics.Vector2(-flipW, 0f)))
		{
			_importError = null;
			bool flipX = _flipModelX;
			ShowOpenFileDialog("Import 3D Model (real)", ModelImporter.FileFilter, modelPath =>
			{
				if (modelPath != null) BakeExternalModel(modelPath, flipX);
			});
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Bakes the mesh into a real PS2 RigidModel/OGI/Object and places it\nas a normal Instance — same pipeline Add Object (full) already uses\nlive in PCSX2. One real PS2 texture per submesh (diffuse only).\nSave Chunk to keep it, then Build ISO + test.");
		}
		ImGui.SameLine();
		ImGui.Checkbox("Flip X (fix mirror)##flipModelX", ref _flipModelX);

		if (ImGui.Button("+ Add Scenery (real)", new System.Numerics.Vector2(-flipW, 0f)))
		{
			_importError = null;
			bool flipX = _flipModelX;
			ShowOpenFileDialog("Import 3D Model as Scenery (real)", ModelImporter.FileFilter, modelPath =>
			{
				if (modelPath != null) BakeExternalModelAsScenery(modelPath, flipX);
			});
		}
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Bakes the mesh into real, independent PS2 SCENERY (a new scenery leaf\nreferencing a freshly-baked SM2 mesh) placed at the world origin (0,0,0)\n-- behaves like any scenery tile you add: move it, Ctrl+D, Delete. Unlike\n\"+ Add Model\", no Object/Instance is created. Save Chunk to keep it,\nthen Build ISO + test.");
		}
		ImGui.SameLine();
		ImGui.Checkbox("Flip X (fix mirror)##flipSceneryX", ref _flipModelX);

		if (ImGui.Button("+ AI Position", new System.Numerics.Vector2(134f, 0f)))
			AddAiPositionAt(System.Numerics.Vector3.Zero);
		if (ImGui.IsItemHovered())
			MaybeTooltip("Drops a new AI position (red nav point) at the world origin (0,0,0),\nlike everything else you add. Enemies roam among nearby AI positions.\nCtrl+D copies it next to itself, drag to move, Delete removes. Connect\nthem with \"+ AI Path\". Save Chunk to keep.");
		ImGui.SameLine();
		if (ImGui.Button("+ AI Path (link all selected)", new System.Numerics.Vector2(-1f, 0f)))
			AddAiPathBetweenSelected();
		if (ImGui.IsItemHovered())
			MaybeTooltip("Select ALL the AI positions you want linked (Ctrl+click), then this\nconnects them in one click: 2 -> a single edge, 3+ -> a closed loop\n(nearest-neighbour order, follows the terrain). Enemies route between\nconnected nodes; without paths they only wander to the nearest. Save Chunk.");
		}
		if (ImGui.CollapsingHeader("Collision"))
		{
			ImGui.Indent();
			if (ImGui.Button("+ Collision Box", new System.Numerics.Vector2(-1f, 0f)))
			{
				_pendingSpawnCollisionBox = true;
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Drops a visible, resizable box (drag it, resize via the Transform\npanel's Scale) that ALSO emits real PS2 collision geometry matching\nits current position/scale — for sanity-testing that collision\nround-trips through Save Chunk + Build ISO + an emulator at all,\nindependent of any mesh decoding. Synced into the real collision\ndata on every Save Chunk (not immediately) — move/resize it as much\nas you want first. Any existing cube can also opt in/out via its own\n\"Emit Collision\" checkbox in the Inspector.");
			}
			if (ImGui.Button("+ Cylinder Collision", new System.Numerics.Vector2(-1f, 0f)))
			{
				_pendingSpawnCollisionCylinder = true;
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Flat-capped cylinder (an N-sided prism, not a rounded/spherical-cap\ncapsule) — Radius/Height/Segments editable in the Inspector.\nSegments locks once synced (changes the actual vertex/triangle\ncount); Radius/Height stay editable anytime. Same sync-on-Save-Chunk\nbehaviour as + Collision Box.");
			}
			if (ImGui.Button("+ Plane Collision", new System.Numerics.Vector2(-1f, 0f)))
			{
				_pendingSpawnCollisionPlane = true;
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("A single flat quad (Width x Length, editable in the Inspector) —\nfor patching a ramp/wall/ceiling piece without a full box. Single-\nsided: rotate the entity (same gizmo as anything else) to orient it,\nplaced flat by default matches real ground's own solid-side\nconvention. Same sync-on-Save-Chunk behaviour as + Collision Box.");
			}
			if (ImGui.Checkbox("Show Collision", ref _showCollision))
			{
				SetCollisionVisible(_showCollision);
			}
			RenderPipeline instance = RenderPipeline.Instance;
			if (instance != null)
			{
				bool wire = instance.Wireframe;
					if (ImGui.Checkbox("Show Mesh (Wireframe)", ref wire))
					{
						instance.Wireframe = wire;
					}
					if (ImGui.IsItemHovered())
					{
						MaybeTooltip("Draws the whole world as edge-only wireframe so you can read the\nactual triangle topology of every model/scenery mesh. The sky stays\nsolid and the UI is unaffected. Purely a viewport view mode — never\nsaved, never changes any data.");
					}
					bool v = instance.LitEnabled;
				if (ImGui.Checkbox("Lit Shading (experimental)", ref v))
				{
					instance.LitEnabled = v;
				}
				if (ImGui.IsItemHovered())
				{
					MaybeTooltip("Global on/off for the real per-level world lighting term (see the\n\"World Lighting\" Hierarchy entry). Off by default because the\nreal game's own scenery appears fully unlit/baked-colour, matching\nTT Lab's own reference shader - this is a one-click experiment/\ncomparison switch, and doubles as an instant revert if enabling it\never looks wrong.");
				}
			}
			if (ImGui.Button("Clear Collision Data (experimental)", new System.Numerics.Vector2(-1f, 0f)))
			{
				ClearCollisionData();
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Empties this level's ground/world collision triangles — for\ntesting whether the real game (built ISO, in an emulator) falls\nthrough the world without them. Save Chunk + Build ISO to test.");
			}
			ImGui.Unindent();
		}

		if (ImGui.CollapsingHeader("UI"))
		{
			ImGui.Indent();
			if (ImGui.Checkbox("Show Triggers", ref _showTriggers) && _triggersRoot != null)
			{
				_triggersRoot.Active = _showTriggers;
			}
			ImGui.SameLine();
			if (ImGui.Checkbox("Show Cameras", ref _showCameras) && _camerasRoot != null)
			{
				_camerasRoot.Active = _showCameras;
			}
			if (ImGui.Checkbox("Show Positions", ref _showPositions) && _positionsRoot != null)
			{
				_positionsRoot.Active = _showPositions;
			}
			ImGui.SameLine();
			if (ImGui.Checkbox("Show AI Positions", ref _showAiPositions) && _aiPosRoot != null)
			{
				_aiPosRoot.Active = _showAiPositions;
			}
			if (ImGui.Checkbox("Show Load Scenes", ref _showLoadScenes) && _loadScenesRoot != null)
			{
				_loadScenesRoot.Active = _showLoadScenes;
			}
			ImGui.SameLine();
			if (ImGui.Checkbox("Show Particle Emitters", ref _showParticleEmitters) && _particleEmittersRoot != null)
			{
				_particleEmittersRoot.Active = _showParticleEmitters;
			}
			if (ImGui.Checkbox("Show Lights", ref _showLights) && _lightsRoot != null) // Amedo 2026-09-19
			{
				_lightsRoot.Active = _showLights;
			}
			bool playerRunning = _playerProcess is { HasExited: false };
			ImGui.BeginDisabled(playerRunning);
			if (ImGui.Button("Launch Player")) LaunchPlayer();
			ImGui.EndDisabled();
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Starts the real, standalone CrashPlayer.exe (see project_crashengine_agentlabvm\nmemory) for this level as a separate process -- reflects your saved edits\n(SavedChunks), same as reloading normally would. The editor itself runs no\nAgentLab/physics code at all anymore.");
			}
			ImGui.SameLine();
			ImGui.BeginDisabled(!playerRunning);
			if (ImGui.Button("Stop Player")) StopPlayer();
			ImGui.EndDisabled();
			if (ImGui.Button("Delete Linked Scenery (test)"))
			{
				DeleteLinkedScenery();
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Clears this chunk's whole LinksList (adjacent-level links, e.g. what\n\"Show Load Scenes\" visualizes) in-memory only — for isolating whether\nsomething related to a linked neighbouring chunk is active in the\nbackground during a live-memory investigation. Reload Level from Disc\nto undo (not on the undo stack). Save Chunk to persist if you actually\nmean to remove them.");
			}
			ImGui.SameLine();
			if (ImGui.Button("Delete Skydome (test)"))
			{
				DeleteSkydome();
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Clears this chunk's skydome (the rotating/animated background sky\npass, drawn every frame) in-memory only — another candidate source\nof constant background activity to rule out during a live-memory\ninvestigation. Reload Level from Disc to undo (not on the undo\nstack). Save Chunk to persist if you actually mean to remove it.");
			}
			if (ImGui.Button("Copy All Ground Colors From Level...", new System.Numerics.Vector2(-1f, 0f)))
			{
				_groundColorPickerFilter = "";
				_showGroundColorPicker = true;
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Whole-level, no-selection version of Copy Vertex Colors From Level —\npicks a level and copies vertex colors for EVERY scenery mesh in this\nchunk that shares the exact same underlying mesh id with that level\n(same asset reused across dimension variants, e.g. beach vs AltEarth).\nCoverage isn't total — tiles with no matching id are left untouched.");
			}
			ImGui.Unindent();
		}

		if (ImGui.CollapsingHeader("Game Settings"))
		{
			ImGui.Indent();
			if (ImGui.Button("Open Game Settings...", new System.Numerics.Vector2(-1f, 0f)))
			{
				_showGameSettings = !_showGameSettings;
			}
			if (ImGui.IsItemHovered())
			{
				MaybeTooltip("Gameplay-rule toggles — real script edits to shared behaviour\ngraphs (Startup\\Default.rm2), not per-object Inspector tweaks.\nSame window as the top bar's \"Game Settings\" button.");
			}
			ImGui.Unindent();
		}
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.SetNextItemWidth(-1f);
		ImGui.InputText("##filter", ref _sceneFilter, 128u);
		ImGui.Separator();
		ImGui.BeginChild("##tree", new System.Numerics.Vector2(-1f, -1f), ImGuiChildFlags.None);
		if (!string.IsNullOrWhiteSpace(_sceneFilter))
		{
			foreach (Entity item in FlatEntities())
			{
				bool flag;
				switch (item.Name)
				{
				case "__Gizmo":
				case "Meter":
				case "Player":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag && item.Name.Contains(_sceneFilter, StringComparison.OrdinalIgnoreCase))
				{
					ImGui.PushID(RuntimeHelpers.GetHashCode(item));
					if (ImGui.Selectable(item.Name, _selectedSet.Contains(item)))
					{
						SelectClicked(item, Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight));
					}
					ImGui.PopID();
				}
			}
		}
		else
		{
			_treeRevealAncestors.Clear();
			if (_revealSelectionInTree && _selected != null)
			{
				for (Entity parent = _selected.Parent; parent != null; parent = parent.Parent)
				{
					_treeRevealAncestors.Add(parent);
				}
			}
			_treeVisibleOrder.Clear();
			foreach (Entity root in base.Roots)
			{
				DrawEntityNode(root);
			}
			_revealSelectionInTree = false;
		}
		ImGui.EndChild();
		ImGui.End();
		ImGui.SetNextWindowPos(new System.Numerics.Vector2((float)width - 280f, 32f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(280f, y), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(180f, 120f), new System.Numerics.Vector2(float.MaxValue, float.MaxValue));
		ImGui.Begin("Inspector##panel", ImGuiWindowFlags.NoCollapse);
		if (_selected != null)
		{
			ImGui.PushID(_selected.GetHashCode());
			DrawInspector(_selected);
			ImGui.PopID();
		}
		else
		{
			ImGui.TextDisabled("No object selected - pick something from the Hierarchy.");
		}
		ImGui.End();

		// Amedo 2026-09-21
		bool blockingUi = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup);

		if (!blockingUi && _selected != null && _camera != null && !_choosingDuplicateDirection && _gizmoMode == GizmoMode.Move)
		{
			System.Numerics.Vector3 vector = GizmoPivot();
			float num = 2.25f * GizmoScale();
			System.Numerics.Vector3[] array = new System.Numerics.Vector3[3]
			{
				vector + new System.Numerics.Vector3(num, 0f, 0f),
				vector + new System.Numerics.Vector3(0f, num, 0f),
				vector + new System.Numerics.Vector3(0f, 0f, num)
			};
			uint[] array2 = new uint[3] { 4282401023u, 4282449728u, 4294918208u };
			uint[] array3 = new uint[3] { 1614823679u, 1614872384u, 1627340864u };
			ImDrawListPtr foregroundDrawList = ImGui.GetForegroundDrawList();
			if (WorldToScreen(vector, width, height, out var screen))
			{
				for (int i = 0; i < 3; i++)
				{
					if (WorldToScreen(array[i], width, height, out var screen2))
					{
						foregroundDrawList.AddLine(screen, screen2, array3[i], 1.5f);
						float num2 = ((_gizmoHover == i || (_gizmoDragging && _gizmoAxis == i)) ? 11f : 7f);
						foregroundDrawList.AddCircleFilled(screen2, num2, array2[i]);
						foregroundDrawList.AddCircle(screen2, num2 + 1.5f, 4278190080u, 12, 1.5f);
					}
				}
			}
		}
		if (!blockingUi && _selected != null && _camera != null && !_choosingDuplicateDirection && _gizmoMode == GizmoMode.Rotate)
		{
			System.Numerics.Vector3 vector2 = GizmoPivot();
			float num3 = 2.25f * GizmoScale();
			System.Numerics.Vector3 position = _camera.Transform.Position;
			uint[] array4 = new uint[4] { 4282401023u, 4282449728u, 4294918208u, 4292927712u };
			uint[] array5 = new uint[4] { 1614823679u, 1614872384u, 1627340864u, 2162221280u };
			ImDrawListPtr foregroundDrawList2 = ImGui.GetForegroundDrawList();
			System.Numerics.Vector2 valueOrDefault = default(System.Numerics.Vector2);
			for (int j = 0; j < 4; j++)
			{
				bool flag2 = _gizmoHover == j || (_gizmoDragging && _gizmoAxis == j);
				uint col = (flag2 ? array4[j] : array5[j]);
				float thickness = (flag2 ? 3f : ((j == 3) ? 2f : 1.5f));
				System.Numerics.Vector3[] array6 = RingPoints(vector2, RotateRingAxis(j), (j == 3) ? (num3 * 1.15f) : num3);
				System.Numerics.Vector2? vector3 = null;
				bool flag3 = false;
				for (int k = 0; k <= array6.Length; k++)
				{
					System.Numerics.Vector3 vector4 = array6[k % array6.Length];
					bool flag4 = j == 3 || System.Numerics.Vector3.Dot(vector4 - vector2, position - vector2) > 0f;
					if (!WorldToScreen(vector4, width, height, out var screen3))
					{
						vector3 = null;
						continue;
					}
					int num4;
					if (vector3.HasValue)
					{
						valueOrDefault = vector3.GetValueOrDefault();
						num4 = 1;
					}
					else
					{
						num4 = 0;
					}
					if (((uint)num4 & (flag3 ? 1u : 0u) & (flag4 ? 1u : 0u)) != 0)
					{
						foregroundDrawList2.AddLine(valueOrDefault, screen3, col, thickness);
					}
					vector3 = screen3;
					flag3 = flag4;
				}
			}
		}
		if (!blockingUi && _choosingDuplicateDirection && _camera != null)
		{
			float num5 = 2.25f * GizmoScale();
			ImDrawListPtr foregroundDrawList3 = ImGui.GetForegroundDrawList();
			if (WorldToScreen(_dupGizmoPivot, width, height, out var screen4))
			{
				for (int l = 0; l < SixDirs.Length; l++)
				{
					if (WorldToScreen(_dupGizmoPivot + SixDirs[l] * num5, width, height, out var screen5))
					{
						foregroundDrawList3.AddLine(screen4, screen5, 1627366694u, 1.5f);
						float num6 = ((_dupGizmoHover == l) ? 11f : 7f);
						foregroundDrawList3.AddCircleFilled(screen5, num6, 4280722943u);
						foregroundDrawList3.AddCircle(screen5, num6 + 1.5f, 4278190080u, 12, 1.5f);
					}
				}
			}
		}
		if (_camera != null)
		{
			ImDrawListPtr backgroundDrawList = ImGui.GetBackgroundDrawList();
			foreach (CubeEntry cube in _cubes)
			{
				System.Numerics.Vector3 world = cube.Ent.Transform.World.Translation + new System.Numerics.Vector3(0f, 2.3f, 0f);
				if (WorldToScreen(world, width, height, out var screen6))
				{
					uint col2 = (_selectedSet.Contains(cube.Ent) ? 4286644223u : 4289374890u);
					float x = ImGui.CalcTextSize(cube.Label).X;
					backgroundDrawList.AddText(screen6 - new System.Numerics.Vector2(x * 0.5f, 0f), col2, cube.Label);
				}
			}
		}
		if (!blockingUi && _camera != null)
		{
			Entity camerasRoot = _camerasRoot;
			if (camerasRoot != null && camerasRoot.Active)
			{
				ImDrawListPtr foregroundDrawList4 = ImGui.GetForegroundDrawList();
				foreach (Entity child in _camerasRoot.Children)
				{
					if (WorldToScreen(child.Transform.World.Translation, width, height, out var screen7))
					{
						System.Numerics.Vector2 p_min = screen7 - new System.Numerics.Vector2(11f, 7.5f);
						System.Numerics.Vector2 p_max = screen7 + new System.Numerics.Vector2(11f, 7.5f);
						uint col3 = 4292878544u;
						uint col4 = 4278190080u;
						System.Numerics.Vector2 p_min2 = screen7 + new System.Numerics.Vector2(-4.4f, -12.5f);
						System.Numerics.Vector2 p_max2 = screen7 + new System.Numerics.Vector2(4.4f, -7.5f);
						foregroundDrawList4.AddRectFilled(p_min2, p_max2, col3, 1f);
						foregroundDrawList4.AddRect(p_min2, p_max2, col4, 1f, ImDrawFlags.None, 1f);
						foregroundDrawList4.AddRectFilled(p_min, p_max, col3, 2f);
						foregroundDrawList4.AddRect(p_min, p_max, col4, 2f, ImDrawFlags.None, 1.5f);
						foregroundDrawList4.AddCircleFilled(screen7, 6f, 4280295456u, 16);
						foregroundDrawList4.AddCircle(screen7, 6f, 4291611852u, 16, 1.5f);
						foregroundDrawList4.AddCircleFilled(screen7, 2.4f, 4286611584u, 12);
					}
				}
			}
		}
		ImGui.SetNextWindowPos(new System.Numerics.Vector2(0f, (float)height - 180f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(width, 180f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(200f, 60f), new System.Numerics.Vector2(float.MaxValue, float.MaxValue));
		ImGui.Begin("Output", ImGuiWindowFlags.NoCollapse);
		foreach (string item2 in _browser.GetLog())
		{
			ImGui.TextUnformatted(item2);
		}
		if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
		{
			ImGui.SetScrollHereY(1f);
		}
		ImGui.End();

		DrawAssetsPanel();
	}
}
