using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;
using TwinJoint = Twinsanity.TwinsanityInterchange.Common.TwinJoint;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
	private static bool _stageSkyHidden;
	private static IReadOnlyList<(GpuMesh Mesh, Material Mat)>? _stageSkyBackup;
	private readonly List<Entity> _animCharacters = new();
	private Entity? _animTarget;
	private Entity? _animPreview;
	private AnimatedObject.SlotGroup? _animGroup;
	private (Vector3 Pos, float Yaw, float Pitch, float Speed)? _animCameraRestore;
	private bool _animCameraOrtho;
	private Entity? _animStage;
	private int _animJoint = -1;

	private Entity? PreviewBody => _animPreview is { Children.Count: > 0 } p ? p.Children[0] : null;
	private AnimatedObject? PreviewAnim => PreviewBody?.Get<AnimatedObject>();

	private static PS2AnyOGI? SkeletonOf(AnimatedObject a) =>
		(a.DefaultGroup ?? a.CurrentGroup ?? (a.Groups.Count > 0 ? a.Groups[0] : null))?.Ogi;

	private void RefreshAnimCharacters()
	{
		_animCharacters.Clear();
		foreach (var root in Roots)
		{
			if (!root.Has<ChunkSource>()) continue;
			foreach (var e in AllEntities(root))
				if (e.Get<AnimatedObject>() is { } a && SkeletonOf(a) is { Joints.Count: > 1 })
					_animCharacters.Add(e);
		}
	}

	private void EnterAnimationStage()
	{
		RefreshAnimCharacters();
		EnterAnimationShading();
		if (_camera is not null)
		{
			_animCameraRestore ??= _camera.SaveState();
			_animCameraOrtho = _camera.Orthographic;
		}
		if (!_stageSkyHidden)
		{
			_stageSkyBackup = RenderPipeline.SkydomeOverride;
			RenderPipeline.SkydomeOverride = Array.Empty<(GpuMesh, Material)>();
			_stageSkyHidden = true;
		}
		var pick = _selected is not null && _animCharacters.Contains(_selected) ? _selected
			: _animCharacters.FirstOrDefault(e => e.Get<InstanceData>()?.ObjectId == 0) ?? _animCharacters.FirstOrDefault();
		SetAnimTarget(pick);
	}

	private void LeaveAnimationStage()
	{
		LeaveAnimationShading();
		DestroyAnimPreview();
		_animTarget = null;
		_animGroup = null;
		_animJoint = -1;
		Isolation = null;
		if (_animStage is not null) { RemoveRoot(_animStage); _animStage = null; }
		if (_camera is not null)
		{
			_camera.OrbitTarget = null;
			if (_animCameraRestore is { } c) _camera.RestoreState(c);
			_camera.Orthographic = _animCameraOrtho;
		}
		_animCameraRestore = null;
		if (_stageSkyHidden) { RenderPipeline.SkydomeOverride = _stageSkyBackup; _stageSkyHidden = false; _stageSkyBackup = null; }
	}

	private void SetAnimTarget(Entity? e)
	{
		DestroyAnimPreview();
		_animTarget = e;
		_animGroup = null;
		_animJoint = -1;
		_animPreview = BuildAnimPreview(e);
		if (_animStage is null)
		{
			_animStage = new Entity("__AnimationStage");
			_animStage.Add(new AnimationStageGrid());
			LoadAndAddRoot(_animStage);
		}
		var keep = Roots.Where(r => r == _animStage || r.Has<RenderPipeline>() || r.Has<CameraComponent>());
		Isolation = new RenderIsolation(_animPreview ?? _animStage, keep);
		if (_camera is not null) _camera.Orthographic = false;
		FrameAnimTarget();
	}

	private Entity? BuildAnimPreview(Entity? source)
	{
		if (source?.Get<InstanceData>() is not { } data) return null;
		var root = source;
		while (root.Parent is not null) root = root.Parent;
		if (root.Get<ChunkSource>()?.MeshTables is not { } tables) return null;
		var scale = root.Transform.Scale;
		var gamePos = Matrix4x4.Invert(root.Transform.World, out var toGame)
			? Vector3.Transform(source.Transform.World.Translation, toGame) : Vector3.Zero;
		var preview = new Entity("__AnimationPreview");
		preview.Transform.Scale = scale;
		preview.Transform.Position = -(gamePos * scale);
		if (root.Get<GameLightingEnvironment>() is { } env)
			preview.Add(new GameLightingEnvironment { Scenery = env.Scenery, AmbientOverride = env.AmbientOverride, DirectionalOverride = env.DirectionalOverride });
		var body = new Entity("__AnimationPreviewBody");
		body.Transform.Position = gamePos;
		body.Add(new InstanceData { ObjectId = data.ObjectId, StateFlags = data.StateFlags, Source = data.Source });
		preview.AddChild(body);
		LoadAndAddRoot(preview);
		MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, tables, body);
		if (body.Get<AnimatedObject>() is not { } anim)
		{
			RemoveRoot(preview);
			_browser.Log($"Animation preview: {source.Name} couldn't be rebuilt on its own.");
			return null;
		}
		anim.Stop();
		return preview;
	}

	private void DestroyAnimPreview()
	{
		if (_animPreview is null) return;
		if (PreviewAnim is { } anim)
		{
			var morphs = new HashSet<GpuMesh>(ReferenceEqualityComparer.Instance);
			foreach (var g in anim.Groups.Concat(anim.ModelGroups).Append(anim.DefaultGroup))
				if (g is not null) foreach (var m in g.MorphMeshes) morphs.Add(m.Mesh);
			foreach (var m in morphs) m.Dispose();
		}
		RemoveRoot(_animPreview);
		_animPreview = null;
	}

	private void FrameAnimTarget()
	{
		if (_camera is null) return;
		var center = Vector3.UnitY;
		float dist = 4f;
		if (PreviewAnim is { } a && SkeletonOf(a)?.BoundingBox is { Length: >= 2 } bb)
		{
			var mn = new Vector3(bb[0].X, bb[0].Y, bb[0].Z);
			var mx = new Vector3(bb[1].X, bb[1].Y, bb[1].Z);
			var size = Vector3.Abs(mx - mn);
			if (size.Length() > 0.01f)
			{
				center = Vector3.Transform((mn + mx) * 0.5f, PreviewBody!.Transform.World);
				dist = MathF.Max(1.5f, size.Length() * 1.4f);
			}
		}
		_camera.OrbitAround(center, dist);
	}

	private bool AnimTargetAlive()
	{
		if (_animTarget is null) return false;
		var root = _animTarget;
		while (root.Parent is not null) root = root.Parent;
		return Roots.Contains(root);
	}

	private void UpdateAnimationWorkspace()
	{
		if (_animTarget is not null && !AnimTargetAlive())
		{
			RefreshAnimCharacters();
			SetAnimTarget(_animCharacters.FirstOrDefault());
		}
		if (TextTyping) return;
		if (Input.KeyDown(Key.Escape))
		{
			if (_gizmoDrag is not null) CancelGizmoDrag();
			else _pendingWorkspace = Workspace.Scene;
		}
		if (Input.KeyDown(Key.Space) && PreviewAnim is { } a && _animGroup is not null) a.Playing = !a.Playing;
		if (Input.KeyDown(Key.F)) FrameAnimTarget();
		bool ctrl = Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight);
		bool shift = Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight);
		if (ctrl && Input.KeyDown(Key.Z)) { if (shift) AnimRedo(); else AnimUndo(); }
		if (ctrl && Input.KeyDown(Key.Y)) AnimRedo();
		if (ctrl) return;
		if (Input.KeyDown(Key.I)) InsertKeyframe();
		if (Input.KeyDown(Key.W) && _gizmoDrag is null) _gizmoMode = GizmoMode.Move;
		if (Input.KeyDown(Key.E) && _gizmoDrag is null) _gizmoMode = GizmoMode.Rotate;
	}

	private sealed class AnimationStageGrid : Component
	{
		private GpuMesh? _mesh;

		public override void OnLoad()
		{
			var verts = new List<GpuMesh.Vertex>();
			var minor = new Vector4(0.30f, 0.30f, 0.30f, 1f);
			var major = new Vector4(0.50f, 0.50f, 0.50f, 1f);
			const int extent = 10;
			for (int i = -extent * 2; i <= extent * 2; i++)
			{
				float t = i * 0.5f;
				var col = i % 2 == 0 ? major : minor;
				verts.Add(V(new Vector3(-extent, 0f, t), col));
				verts.Add(V(new Vector3(extent, 0f, t), col));
				verts.Add(V(new Vector3(t, 0f, -extent), col));
				verts.Add(V(new Vector3(t, 0f, extent), col));
			}
			_mesh = new GpuMesh(Engine.Instance.GL, verts.ToArray(), PrimitiveType.Lines);
		}

		public override void OnRender(GL gl)
		{
			if (_mesh is null || RenderPipeline.Instance is not { } pipeline) return;
			var sh = pipeline.Shader;
			sh.Set("StartModel", Matrix4x4.CreateTranslation(Transform.World.Translation));
			sh.Set("twin_material.use_texture", 0f);
			sh.Set("twin_material.double_color", 1f);
			sh.Set("twin_material.alpha_test", 0f);
			sh.Set("twin_material.alpha_blend", 0f);
			sh.Set("twin_material.env_map", 0f);
			sh.Set("twin_material.metalic_specular", 0f);
			sh.Set("twin_material.deform_speed", Vector2.Zero);
			sh.Set("twin_material.uv_scroll_speed", Vector2.Zero);
			sh.Set("twin_material.reflect_dist", Vector2.Zero);
			sh.Set("twin_material.billboard_render", 0f);
			sh.Set("twin_material.two_sided_lighting", 1);
			sh.Set("twin_material.perform_fog", 0f);
			GlState.Culling(gl, false);
			_mesh.Draw(gl);
		}

		public override void OnDestroy() => _mesh?.Dispose();

		private static GpuMesh.Vertex V(Vector3 p, Vector4 c) => new() { Position = p, Normal = Vector3.UnitY, UV = Vector2.Zero, Color = c };
	}
}
