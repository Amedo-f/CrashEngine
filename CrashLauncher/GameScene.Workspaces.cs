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
	private enum Workspace { Scene, Animation }

	private static readonly Workspace[] Workspaces = { Workspace.Scene, Workspace.Animation };
	private Workspace _workspace = Workspace.Scene;
	private Workspace? _pendingWorkspace;
	private bool _pendingWorkspaceReset;
	private readonly Dictionary<Workspace, string> _workspaceLayouts = new();
	private string? _savedWorkspaceLayout;
	private int _workspaceSaveTick;

	private const string AnimationDockLayout =
@"[Window][##dockhost]
Pos=0,32
Size=1920,977
Collapsed=0

[Window][##topbar]
Pos=0,0
Size=1920,32
Collapsed=0

[Window][Characters##anim]
Pos=0,32
Size=325,380
Collapsed=0
DockId=0x00000025,0

[Window][Bones##anim]
Pos=0,414
Size=325,318
Collapsed=0
DockId=0x00000026,0

[Window][Inspector##panel]
Pos=1505,32
Size=415,700
Collapsed=0
DockId=0x00000028,0

[Window][Timeline##anim]
Pos=0,734
Size=1920,275
Collapsed=0
DockId=0x00000022,0

[Docking][Data]
DockSpace       ID=0x50DE06D3 Window=0x5B220BC7 Pos=0,32 Size=1920,977 Split=Y
  DockNode      ID=0x00000021 Parent=0x50DE06D3 SizeRef=1920,700 Split=X
    DockNode    ID=0x00000023 Parent=0x00000021 SizeRef=325,700 Split=Y
      DockNode  ID=0x00000025 Parent=0x00000023 SizeRef=325,380
      DockNode  ID=0x00000026 Parent=0x00000023 SizeRef=325,318
    DockNode    ID=0x00000024 Parent=0x00000021 SizeRef=1593,700 Split=X
      DockNode  ID=0x00000027 Parent=0x00000024 SizeRef=1176,700 CentralNode=1
      DockNode  ID=0x00000028 Parent=0x00000024 SizeRef=415,700
  DockNode      ID=0x00000022 Parent=0x50DE06D3 SizeRef=1920,275
";

	private static string DefaultLayoutOf(Workspace ws) => ws == Workspace.Animation ? AnimationDockLayout : DefaultDockLayout;

	// Amedo
	private static string MigrateLayout(string ini) => ini
		.Replace("[Window][Scene##panel]", "[Window][Hierarchy##panel]")
		.Replace("[Window][Assets]", "[Window][Project##panel]")
		.Replace("[Window][Output]", "[Window][Console##panel]");

	private static string WorkspaceLayoutFile(Workspace ws) =>
		System.IO.Path.Combine(AppContext.BaseDirectory, $"workspace_{ws}.ini");

	private string LayoutOf(Workspace ws)
	{
		if (_workspaceLayouts.TryGetValue(ws, out var ini)) return ini;
		try
		{
			var file = WorkspaceLayoutFile(ws);
			if (File.Exists(file)) return _workspaceLayouts[ws] = MigrateLayout(File.ReadAllText(file)); // Amedo
		}
		catch (Exception e) { _browser.Log($"Workspace layout {ws} couldn't be read: {e.Message}"); }
		return DefaultLayoutOf(ws);
	}

	private void ApplyPendingWorkspaceLayout()
	{
		if (!_defaultLayoutApplied)
		{
			if (_stageSkyHidden) { RenderPipeline.SkydomeOverride = _stageSkyBackup; _stageSkyHidden = false; }
			ImGui.LoadIniSettingsFromMemory(LayoutOf(_workspace));
			_defaultLayoutApplied = true;
			_savedWorkspaceLayout = null;
		}
		if (_pendingWorkspaceReset)
		{
			_pendingWorkspaceReset = false;
			_workspaceLayouts.Remove(_workspace);
			ImGui.LoadIniSettingsFromMemory(DefaultLayoutOf(_workspace));
			_savedWorkspaceLayout = null;
		}
		if (_pendingWorkspace is { } next && next != _workspace)
		{
			StoreWorkspaceLayout(force: true);
			if (_workspace == Workspace.Animation) LeaveAnimationStage();
			_workspace = next;
			ImGui.LoadIniSettingsFromMemory(LayoutOf(next));
			_savedWorkspaceLayout = null;
			if (next == Workspace.Animation) EnterAnimationStage();
		}
		_pendingWorkspace = null;
		if (++_workspaceSaveTick >= 120)
		{
			_workspaceSaveTick = 0;
			StoreWorkspaceLayout(force: false);
		}
	}

	private void StoreWorkspaceLayout(bool force)
	{
		string ini = ImGui.SaveIniSettingsToMemory();
		if (!force && ini == _savedWorkspaceLayout) return;
		_workspaceLayouts[_workspace] = ini;
		_savedWorkspaceLayout = ini;
		try { File.WriteAllText(WorkspaceLayoutFile(_workspace), ini); }
		catch (Exception e) { _browser.Log($"Workspace layout {_workspace} couldn't be saved: {e.Message}"); }
	}

	private void ResetWorkspaceLayout() => _pendingWorkspaceReset = true;

	private void DrawWorkspaceTabs()
	{
		var active = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive];
		foreach (var ws in Workspaces)
		{
			bool on = ws == _workspace;
			if (on) ImGui.PushStyleColor(ImGuiCol.Button, active);
			ImGui.BeginDisabled(Simulating && !on);
			if (ImGui.SmallButton($"{ws}##workspace{ws}") && !on) _pendingWorkspace = ws;
			ImGui.EndDisabled();
			if (on) ImGui.PopStyleColor();
			if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
				MaybeTooltip(Simulating && !on ? "Stop Play Scene to switch workspaces."
					: ws == Workspace.Scene ? "Scene workspace — the level, the Hierarchy, the Inspector, Assets and Output."
					: "Animation workspace — one character on its own, its skeleton, skins and animations, with a timeline.\nRight-drag orbits, the wheel zooms, middle-drag pans, Esc goes back to the Scene.");
			ImGui.SameLine();
		}
		ImGui.TextDisabled("|");
		ImGui.SameLine();
	}


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

	private static readonly Dictionary<int, string> ReactNames = new()
	{
		[0] = "head", [1] = "spine 1", [2] = "spine 2", [3] = "spine 3", [4] = "spine 4", [5] = "pelvis",
		[6] = "leg", [7] = "leg", [8] = "leg", [9] = "leg", [10] = "leg", [11] = "leg",
		[12] = "arm", [13] = "arm", [14] = "arm", [15] = "arm", [16] = "arm", [17] = "arm", [18] = "arm", [19] = "arm",
		[20] = "hair", [21] = "hair", [22] = "finger", [23] = "finger", [24] = "foot", [25] = "foot",
		[26] = "ear", [27] = "ear", [28] = "nose",
	};

	private static string JointLabel(TwinJoint j) =>
		ReactNames.TryGetValue(j.ReactId, out var n) ? $"Bone {j.Index}  ({n} · {j.ReactId})" : $"Bone {j.Index}";

	private void DrawAnimationWorkspace()
	{
		DrawArmatureOverlay();
		var anim = PreviewAnim;
		var ogi = anim is null ? null : SkeletonOf(anim);

		if (ImGui.Begin("Characters##anim", ImGuiWindowFlags.NoCollapse))
		{
			if (ImGui.SmallButton("Refresh##animrefresh")) RefreshAnimCharacters();
			ImGui.SameLine();
			ImGui.TextDisabled($"{_animCharacters.Count} in the level");
			if (ImGui.BeginChild("##animchars", new Vector2(0f, 120f), ImGuiChildFlags.Border))
			{
				foreach (var e in _animCharacters)
				{
					if (ImGui.Selectable($"{e.Name}##animchar{e.GetHashCode()}", e == _animTarget) && e != _animTarget)
						SetAnimTarget(e);
				}
			}
			ImGui.EndChild();
			if (anim is not null && ogi is not null)
			{
				ImGui.TextUnformatted(_animTarget!.Name);
				ImGui.TextDisabled($"Skeleton: OGI 0x{ogi.GetID():X4}, {ogi.Joints.Count} joints, {ogi.ExitPoints.Count} exit points");
				ImGui.TextDisabled(ogi.SkinID != 0 ? $"Skin: 0x{ogi.SkinID:X4}" : "Skin: none");
				ImGui.TextDisabled(ogi.BlendSkinID != 0 ? $"Blend skin (face shapes): 0x{ogi.BlendSkinID:X4}" : "Blend skin: none");
				ImGui.TextDisabled($"Rigid parts: {ogi.RigidModelIds.Count}   Collision parts: {ogi.Collisions.Count}");
				if (ImGui.Button("T-pose##animtpose"))
				{
					anim.Stop();
					_animGroup = null;
				}
				if (ImGui.IsItemHovered()) MaybeTooltip("The bind pose — the skeleton with no animation on it.");
				ImGui.SameLine();
				ImGui.TextDisabled($"{anim.Groups.Count} animations");
				if (ImGui.BeginChild("##animlist", Vector2.Zero, ImGuiChildFlags.Border))
				{
					foreach (var g in anim.Groups.OrderBy(g => g.SlotIndex))
					{
						string face = g.Anim.HasFacialAnimationData ? "  face" : "";
						string label = $"Slot {g.SlotIndex,2}   0x{g.AnimId:X4}   {g.Anim.TotalFrames} f @ {g.Anim.DefaultFPS}{face}##animslot{g.SlotIndex}";
						if (ImGui.Selectable(label, g == _animGroup))
						{
							_animGroup = g;
							UseCloneIfAny(g);
							anim.Loop = true;
							anim.Play(g);
						}
					}
				}
				ImGui.EndChild();
			}
			else if (_animCharacters.Count == 0)
				ImGui.TextDisabled("This level has no animated characters.");
		}
		ImGui.End();

		var viewOgi = _animGroup?.Ogi ?? ogi;
		if (ImGui.Begin("Bones##anim", ImGuiWindowFlags.NoCollapse))
		{
			if (viewOgi is null) ImGui.TextDisabled("Pick a character.");
			else
			{
				if (_animGroup is not null && _animGroup.Ogi != ogi)
					ImGui.TextDisabled($"This animation's own skeleton: OGI 0x{viewOgi.GetID():X4}, {viewOgi.Joints.Count} joints");
				foreach (var j in viewOgi.Joints)
					if (j.ParentIndex < 0 || j.ParentIndex >= viewOgi.Joints.Count || j.ParentIndex == j.Index)
						DrawJointNode(viewOgi, j, 0);
			}
		}
		ImGui.End();

		if (ImGui.Begin("Timeline##anim", ImGuiWindowFlags.NoCollapse))
		{
			if (anim is null || _animGroup is null) ImGui.TextDisabled("Pick an animation from the Characters panel.");
			else
			{
				int frames = Math.Max(1, _animGroup.Anim.MainAnimation.AnimatedTransformations.Count);
				if (ImGui.Button(anim.Playing ? "Pause##animplay" : "Play##animplay")) anim.Playing = !anim.Playing;
				ImGui.SameLine();
				bool loop = anim.Loop;
				if (ImGui.Checkbox("Loop##animloop", ref loop)) anim.Loop = loop;
				ImGui.SameLine();
				ImGui.TextDisabled($"Slot {_animGroup.SlotIndex} · {frames} frames · {_animGroup.Anim.DefaultFPS} fps · {AnimatedObject.NaturalDuration(_animGroup):0.00} s · frame {anim.Frame:0.0}");
				ImGui.SameLine();
				ImGui.Checkbox("All frames##animallframes", ref _timelineAllFrames);
				if (ImGui.IsItemHovered()) MaybeTooltip("Off: only the real keys (frames the motion bends at — the rest are filled in between, exactly).\nOn: every frame of the game's data as a key.");
				DrawAnimRecordBar(anim, _animGroup);
				DrawChannelTimeline(anim, _animGroup, frames);
			}
		}
		ImGui.End();
	}

	private static readonly string[] ChannelNames =
		{ "Position X", "Position Y", "Position Z", "Rotation X", "Rotation Y", "Rotation Z", "Scale X", "Scale Y", "Scale Z" };

	private sealed class BoneChannels
	{
		public bool HasSettings;
		public readonly bool[] Animated = new bool[9];
		public readonly float[][] Values = new float[9][];
		public readonly float[] Min = new float[9], Max = new float[9];
	}

	private (AnimatedObject.SlotGroup Group, int Joint, BoneChannels Data)? _channelCache;
	private static readonly BoneChannels NoBoneChannels = new();

	private BoneChannels ChannelsOf(AnimatedObject.SlotGroup g, int joint)
	{
		if (_channelCache is { } c && c.Group == g && c.Joint == joint) return c.Data;
		var data = ReadBoneChannels(g.Anim.MainAnimation, joint);
		_channelCache = (g, joint, data);
		return data;
	}

	private static float ChannelValue(short raw, bool rotation) => rotation ? raw * (360f * 16f / 65536f) : raw / 4096f;

	private static BoneChannels ReadBoneChannels(Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation anim, int joint)
	{
		var bc = new BoneChannels();
		if (joint < 0 || joint >= anim.JointSettings.Count) return bc;
		bc.HasSettings = true;
		var js = anim.JointSettings[joint];
		var types = new[] { js.TranslateX, js.TranslateY, js.TranslateZ, js.RotateX, js.RotateY, js.RotateZ, js.ScaleX, js.ScaleY, js.ScaleZ };
		int s = js.TransformationIndex, a = js.AnimationTransformationIndex;
		int frames = anim.AnimatedTransformations.Count;
		for (int ch = 0; ch < 9; ch++)
		{
			bool rot = ch is >= 3 and <= 5;
			float[] v;
			if (types[ch] == Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType.Animated && frames > 0)
			{
				bc.Animated[ch] = true;
				v = new float[frames];
				for (int f = 0; f < frames; f++)
				{
					var tr = anim.AnimatedTransformations[f].Transforms;
					v[f] = a < tr.Count ? ChannelValue(tr[a].PureValue, rot) : 0f;
					if (rot && f > 0)
					{
						while (v[f] - v[f - 1] > 180f) v[f] -= 360f;
						while (v[f] - v[f - 1] < -180f) v[f] += 360f;
					}
				}
				a++;
			}
			else
			{
				v = new[] { s < anim.StaticTransformations.Count ? ChannelValue(anim.StaticTransformations[s].PureValue, rot) : 0f };
				s++;
			}
			bc.Values[ch] = v;
			bc.Min[ch] = v.Min();
			bc.Max[ch] = v.Max();
		}
		return bc;
	}

	private static bool BoneIsAnimated(Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation anim, int joint)
	{
		if (joint < 0 || joint >= anim.JointSettings.Count) return false;
		var js = anim.JointSettings[joint];
		var animated = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType.Animated;
		return js.TranslateX == animated || js.TranslateY == animated || js.TranslateZ == animated
			|| js.RotateX == animated || js.RotateY == animated || js.RotateZ == animated
			|| js.ScaleX == animated || js.ScaleY == animated || js.ScaleZ == animated;
	}

	private void DrawChannelTimeline(AnimatedObject anim, AnimatedObject.SlotGroup g, int frames)
	{
		bool hasBone = _animJoint >= 0;
		var bc = hasBone ? ChannelsOf(g, _animJoint) : NoBoneChannels;
		const float labelW = 110f, rowH = 22f, rulerH = 20f;
		float eventsH = EventRowH;
		float avail = ImGui.GetContentRegionAvail().X;
		float frameW = MathF.Max(8f, (avail - labelW - 16f) / frames);
		var size = new Vector2(labelW + frameW * frames + 8f, rulerH + eventsH + rowH * (hasBone ? 9f : 1f));
		if (!ImGui.BeginChild("##animtrack", Vector2.Zero, ImGuiChildFlags.Border, ImGuiWindowFlags.HorizontalScrollbar)) { ImGui.EndChild(); return; }
		var dl = ImGui.GetWindowDrawList();
		var o = ImGui.GetCursorScreenPos();
		ImGui.InvisibleButton("##animtrackarea", size);
		bool hovered = ImGui.IsItemHovered(), held = ImGui.IsItemActive();
		var mouse = ImGui.GetMousePos();
		float x0 = o.X + labelW;
		float FrameX(float f) => x0 + f * frameW + frameW * 0.5f;

		uint textCol = ImGui.GetColorU32(ImGuiCol.Text), dimCol = ImGui.GetColorU32(ImGuiCol.TextDisabled);
		uint rowA = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.03f)), rowB = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f));
		uint keyCol = ImGui.GetColorU32(new Vector4(0.95f, 0.75f, 0.25f, 1f)), curveCol = ImGui.GetColorU32(new Vector4(0.35f, 0.75f, 1f, 0.9f));
		uint staticCol = ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 0.5f)), headCol = ImGui.GetColorU32(new Vector4(1f, 0.3f, 0.3f, 1f));

		int labelStep = frameW >= 28f ? 1 : frameW >= 14f ? 2 : frameW >= 9f ? 5 : 10;
		for (int f = 0; f < frames; f++)
		{
			float x = FrameX(f);
			bool major = f % labelStep == 0;
			dl.AddLine(new Vector2(x, o.Y + rulerH - (major ? 7f : 4f)), new Vector2(x, o.Y + rulerH), dimCol);
			if (major) dl.AddText(new Vector2(x - 3f, o.Y + 1f), dimCol, f.ToString());
		}

		bool eventTip = DrawEventRow(dl, g, o, x0, frameW, size.X, o.Y + rulerH, mouse, hovered);
		if (!hasBone) dl.AddText(new Vector2(o.X + 4f, o.Y + rulerH + eventsH + 4f), dimCol, "Pick a bone in the Bones panel to see its channels. Bones marked • move in this animation.");
		for (int ch = 0; hasBone && ch < 9; ch++)
		{
			float y0 = o.Y + rulerH + eventsH + ch * rowH, yc = y0 + rowH * 0.5f;
			dl.AddRectFilled(new Vector2(o.X, y0), new Vector2(o.X + size.X, y0 + rowH), ch % 2 == 0 ? rowA : rowB);
			dl.AddText(new Vector2(o.X + 4f, y0 + 4f), bc.HasSettings ? textCol : dimCol, ChannelNames[ch]);
			if (!bc.HasSettings)
			{
				if (ch == 0) dl.AddText(new Vector2(x0 + 4f, y0 + 4f), dimCol, "not in this animation — the bone stays in its bind pose");
				continue;
			}
			var v = bc.Values[ch];
			if (!bc.Animated[ch])
			{
				dl.AddLine(new Vector2(x0, yc), new Vector2(x0 + frameW * frames, yc), staticCol, 2f);
				dl.AddText(new Vector2(x0 + 4f, y0 + 4f), dimCol, ch is >= 3 and <= 5 ? $"{v[0]:0.##}° (fixed)" : $"{v[0]:0.###} (fixed)");
				continue;
			}
			float lo = bc.Min[ch], hi = bc.Max[ch], span = hi - lo;
			float Y(float val) => span < 1e-6f ? yc : y0 + rowH - 3f - (val - lo) / span * (rowH - 6f);
			for (int f = 1; f < v.Length; f++)
				dl.AddLine(new Vector2(FrameX(f - 1), Y(v[f - 1])), new Vector2(FrameX(f), Y(v[f])), curveCol, 1.5f);
			float k = MathF.Min(4f, frameW * 0.3f);
			var keyMask = _timelineAllFrames ? null : Channel(g.Anim.MainAnimation, _animJoint, ch).IsKey;
			for (int f = 0; f < v.Length; f++)
			{
				if (keyMask is not null && (f >= keyMask.Length || !keyMask[f])) continue;
				var c = new Vector2(FrameX(f), yc);
				dl.AddQuadFilled(c + new Vector2(0f, -k), c + new Vector2(k, 0f), c + new Vector2(0f, k), c + new Vector2(-k, 0f), keyCol);
			}
		}

		float headX = FrameX(anim.Frame);
		dl.AddLine(new Vector2(headX, o.Y), new Vector2(headX, o.Y + size.Y), headCol, 2f);

		if ((held || (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))) && mouse.X >= x0)
		{
			float f = Math.Clamp((mouse.X - x0) / frameW - 0.5f, 0f, frames - 1);
			anim.Playing = false;
			anim.Frame = f;
		}
		if (hovered && !eventTip && mouse.X >= x0 && bc.HasSettings)
		{
			int f = Math.Clamp((int)((mouse.X - x0) / frameW), 0, frames - 1);
			int ch = mouse.Y < o.Y + rulerH + eventsH ? -1 : (int)((mouse.Y - o.Y - rulerH - eventsH) / rowH);
			if (ch is >= 0 and < 9)
			{
				var v = bc.Values[ch];
				float val = bc.Animated[ch] ? v[Math.Min(f, v.Length - 1)] : v[0];
				ImGui.SetTooltip($"{ChannelNames[ch]} at frame {f}: {(ch is >= 3 and <= 5 ? $"{val:0.###}°" : $"{val:0.####}")}{(bc.Animated[ch] ? "" : " (fixed for the whole animation)")}");
			}
		}
		ImGui.EndChild();
	}

	private void DrawJointNode(PS2AnyOGI ogi, TwinJoint j, int depth)
	{
		if (depth > 64) return;
		var children = ogi.Joints.Where(c => c.ParentIndex == j.Index && c.Index != j.Index).ToList();
		var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
		if (children.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;
		if (_animJoint == j.Index) flags |= ImGuiTreeNodeFlags.Selected;
		string moving = _animGroup is not null && BoneIsAnimated(_animGroup.Anim.MainAnimation, j.Index) ? " •" : "";
		bool open = ImGui.TreeNodeEx($"{JointLabel(j)}{moving}##joint{j.Index}", flags);
		if (ImGui.IsItemClicked()) _animJoint = j.Index;
		if (!open) return;
		foreach (var c in children) DrawJointNode(ogi, c, depth + 1);
		ImGui.TreePop();
	}

	private void DrawAnimationInspector()
	{
		var anim = PreviewAnim;
		var ogi = anim is null ? null : _animGroup?.Ogi ?? SkeletonOf(anim);
		if (anim is null || ogi is null) { ImGui.TextDisabled("Pick a character in the Characters panel."); return; }
		ImGui.TextUnformatted(_animTarget!.Name);
		if (_animGroup is { } g)
		{
			ImGui.SeparatorText("Animation");
			ImGui.TextDisabled($"Slot {g.SlotIndex}, animation 0x{g.AnimId:X4}, OGI 0x{g.Ogi.GetID():X4}");
			ImGui.TextDisabled($"{g.Anim.TotalFrames} frames at {g.Anim.DefaultFPS} fps, {AnimatedObject.NaturalDuration(g):0.00} s");
			ImGui.TextDisabled(g.Anim.HasFacialAnimationData ? "Has face shape keys" : "No face shape keys");
			DrawAnimEventsInfo(g);
		}
		if (_animJoint >= 0 && _animJoint < ogi.Joints.Count)
		{
			var j = ogi.Joints[_animJoint];
			ImGui.SeparatorText("Bone");
			ImGui.TextUnformatted(JointLabel(j));
			ImGui.TextDisabled($"Parent: {(j.ParentIndex >= 0 && j.ParentIndex != j.Index ? $"Bone {j.ParentIndex}" : "none")}");
			ImGui.TextDisabled($"Bind position: {j.LocalTranslation.X:0.###}, {j.LocalTranslation.Y:0.###}, {j.LocalTranslation.Z:0.###}");
			ImGui.TextDisabled($"Bind rotation: {j.LocalRotation.X:0.###}, {j.LocalRotation.Y:0.###}, {j.LocalRotation.Z:0.###}, {j.LocalRotation.W:0.###}");
			if (anim.JointWorlds is { } worlds && worlds.TryGetValue(j.Index, out var m))
				ImGui.TextDisabled($"Now (world): {m.Translation.X:0.###}, {m.Translation.Y:0.###}, {m.Translation.Z:0.###}");
			if (_animGroup is { } ag) DrawBoneTransformPanel(anim, ag, j.Index);
		}
	}

	private (Vector2 Pos, Vector2 Size) _inspectorRect;
	private bool _viewGizmoDragging;

	private enum ViewShading { Wire, Unlit, Lit }
	private ViewShading _animShading = ViewShading.Lit;
	private (bool Wire, bool Lit)? _sceneShadingBackup;

	private static ViewShading CurrentShading() =>
		RenderPipeline.Instance?.Wireframe == true ? ViewShading.Wire : GameLightingEnvironment.Enabled ? ViewShading.Lit : ViewShading.Unlit;

	private static void ApplyShading(ViewShading s)
	{
		if (RenderPipeline.Instance is { } rp) rp.Wireframe = s == ViewShading.Wire;
		if (s != ViewShading.Wire) GameLightingEnvironment.Enabled = s == ViewShading.Lit;
	}

	private void EnterAnimationShading()
	{
		_sceneShadingBackup ??= (RenderPipeline.Instance?.Wireframe ?? false, GameLightingEnvironment.Enabled);
		ApplyShading(_animShading);
	}

	private void LeaveAnimationShading()
	{
		_animShading = CurrentShading();
		if (_sceneShadingBackup is { } b)
		{
			if (RenderPipeline.Instance is { } rp) rp.Wireframe = b.Wire;
			GameLightingEnvironment.Enabled = b.Lit;
		}
		_sceneShadingBackup = null;
	}

	private void DrawViewShading(float rightEdge)
	{
		var cur = CurrentShading();
		ImGui.SetNextWindowPos(new Vector2(rightEdge, ViewOrigin.Y + 8f), ImGuiCond.Always, new Vector2(1f, 0f)); // Amedo
		ImGui.SetNextWindowBgAlpha(0.55f);
		const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
		                               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;
		if (!ImGui.Begin("##viewshading", flags)) { ImGui.End(); return; }
		var active = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive];
		foreach (var (mode, label, tip) in new[]
		{
			(ViewShading.Wire, "Wire", "Wireframe — only the mesh's edges."),
			(ViewShading.Unlit, "Unlit", "Unlit — textures and vertex colors, no lighting."),
			(ViewShading.Lit, "Lit", "Lit — the game's own lighting (ambient + the 3 strongest lights), like in game."),
		})
		{
			bool on = cur == mode;
			if (on) ImGui.PushStyleColor(ImGuiCol.Button, active);
			if (ImGui.SmallButton($"{label}##shade{mode}")) ApplyShading(mode);
			if (on) ImGui.PopStyleColor();
			if (ImGui.IsItemHovered()) MaybeTooltip(tip);
			ImGui.SameLine();
		}
		if (_workspace == Workspace.Animation)
		{
			ImGui.TextDisabled("|");
			ImGui.SameLine();
			if (_showArmature) ImGui.PushStyleColor(ImGuiCol.Button, active);
			if (ImGui.SmallButton("Bones##showarmature")) _showArmature = !_showArmature;
			if (_showArmature) ImGui.PopStyleColor();
			if (ImGui.IsItemHovered()) MaybeTooltip("Show Armature — the skeleton drawn over the character, from each joint to its child\n(the game keeps joints only, no bone shapes). Click a bone to pick it.");
			ImGui.SameLine();
		}
		ImGui.NewLine();
		ImGui.End();
	}

	private static readonly (Vector3 Axis, string Label, Vector4 Color)[] ViewAxes =
	{
		(Vector3.UnitX, "X", new Vector4(0.92f, 0.27f, 0.33f, 1f)),
		(Vector3.UnitY, "Y", new Vector4(0.55f, 0.82f, 0.22f, 1f)),
		(Vector3.UnitZ, "Z", new Vector4(0.27f, 0.52f, 0.93f, 1f)),
	};

	private Vector3 ViewPivot()
	{
		if (_camera?.OrbitTarget is { } orbit) return orbit;
		if (_workspace == Workspace.Scene && _selected is not null) return _selected.Transform.World.Translation;
		return (_camera?.Transform.Position ?? Vector3.Zero) + (_camera?.Forward ?? -Vector3.UnitZ) * 20f;
	}

	private string ViewName()
	{
		if (_camera is null || !_camera.Orthographic) return "Perspective";
		var f = _camera.Forward;
		foreach (var (axis, label, _) in ViewAxes)
		{
			if (Vector3.Dot(f, -axis) > 0.999f) return $"Ortho · from +{label}";
			if (Vector3.Dot(f, axis) > 0.999f) return $"Ortho · from -{label}";
		}
		return "Ortho";
	}

	private void DrawViewGizmo()
	{
		if (_camera is null || Simulating || _camera.External) return;
		const float size = 96f, radius = 33f, dot = 9f;
		float right = ViewOrigin.X + ViewW; // Amedo
		float top = ViewOrigin.Y + 8f;
		DrawViewShading(right - size - 16f);
		ImGui.SetNextWindowPos(new Vector2(right - size - 10f, top));
		ImGui.SetNextWindowSize(new Vector2(size, size + 26f));
		ImGui.SetNextWindowBgAlpha(0f);
		const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
		                               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar;
		if (!ImGui.Begin("##viewgizmo", flags)) { ImGui.End(); return; }
		var dl = ImGui.GetWindowDrawList();
		var c = ImGui.GetWindowPos() + new Vector2(size * 0.5f, size * 0.5f);
		var mouse = ImGui.GetMousePos();
		bool inDisc = Vector2.Distance(mouse, c) <= radius + dot && ImGui.IsWindowHovered();

		var camRight = _camera.Right;
		var camUp = _camera.Up;
		var camFwd = _camera.Forward;
		var items = new List<(Vector2 P, float Depth, Vector3 Axis, string Label, Vector4 Color, bool Positive)>(6);
		foreach (var (axis, label, color) in ViewAxes)
			foreach (bool pos in new[] { true, false })
			{
				var a = pos ? axis : -axis;
				items.Add((c + new Vector2(Vector3.Dot(a, camRight), -Vector3.Dot(a, camUp)) * radius, Vector3.Dot(a, camFwd), a, label, color, pos));
			}
		items.Sort((x, y) => y.Depth.CompareTo(x.Depth));

		int hoverIndex = -1;
		for (int i = items.Count - 1; i >= 0; i--)
			if (ImGui.IsWindowHovered() && Vector2.Distance(mouse, items[i].P) <= dot) { hoverIndex = i; break; }

		if (inDisc || _viewGizmoDragging) dl.AddCircleFilled(c, radius + dot + 2f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)), 32);
		for (int i = 0; i < items.Count; i++)
		{
			var it = items[i];
			if (it.Positive) dl.AddLine(c, it.P, ImGui.GetColorU32(it.Color), 2f);
		}
		for (int i = 0; i < items.Count; i++)
		{
			var it = items[i];
			var col = it.Color;
			bool hover = i == hoverIndex;
			if (it.Positive)
			{
				dl.AddCircleFilled(it.P, dot, ImGui.GetColorU32(hover ? Vector4.One : col), 20);
				var ts = ImGui.CalcTextSize(it.Label);
				dl.AddText(it.P - ts * 0.5f, ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.08f, 1f)), it.Label);
			}
			else
			{
				dl.AddCircleFilled(it.P, dot - 1f, ImGui.GetColorU32(new Vector4(col.X, col.Y, col.Z, hover ? 0.75f : 0.35f)), 20);
				dl.AddCircle(it.P, dot - 1f, ImGui.GetColorU32(col), 20, 1.5f);
				if (hover) dl.AddText(it.P - ImGui.CalcTextSize("-" + it.Label) * 0.5f, ImGui.GetColorU32(Vector4.One), "-" + it.Label);
			}
		}

		if (hoverIndex >= 0)
		{
			var it = items[hoverIndex];
			ImGui.SetTooltip($"View from {(it.Positive ? "+" : "-")}{it.Label} (orthographic)");
			if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _camera.SnapTo(-it.Axis, ViewPivot());
		}
		else if (inDisc && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _viewGizmoDragging = true;
		if (_viewGizmoDragging)
		{
			if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
			{
				var d = ImGui.GetIO().MouseDelta;
				if (d != Vector2.Zero) _camera.Rotate(-d.X * 0.4f, -d.Y * 0.4f);
			}
			else _viewGizmoDragging = false;
		}

		ImGui.SetCursorPos(new Vector2(4f, size + 2f));
		if (ImGui.SmallButton(_camera.Orthographic ? "Ortho##viewproj" : "Persp##viewproj"))
		{
			if (_camera.Orthographic) _camera.Orthographic = false;
			else _camera.SnapTo(_camera.Forward, ViewPivot());
		}
		if (ImGui.IsItemHovered()) MaybeTooltip($"{ViewName()} — click to switch between perspective (3D) and orthographic (2D).\nNumpad 1/3/7 = view along Z/X/Y (Ctrl = the other side), Numpad 5 = switch.");
		ImGui.End();

		if (!ImGui.GetIO().WantTextInput && !TextTyping)
		{
			bool ctrl = ImGui.GetIO().KeyCtrl;
			if (ImGui.IsKeyPressed(ImGuiKey.Keypad1, false)) _camera.SnapTo(ctrl ? Vector3.UnitZ : -Vector3.UnitZ, ViewPivot());
			if (ImGui.IsKeyPressed(ImGuiKey.Keypad3, false)) _camera.SnapTo(ctrl ? Vector3.UnitX : -Vector3.UnitX, ViewPivot());
			if (ImGui.IsKeyPressed(ImGuiKey.Keypad7, false)) _camera.SnapTo(ctrl ? Vector3.UnitY : -Vector3.UnitY, ViewPivot());
			if (ImGui.IsKeyPressed(ImGuiKey.Keypad5, false))
			{
				if (_camera.Orthographic) _camera.Orthographic = false;
				else _camera.SnapTo(_camera.Forward, ViewPivot());
			}
		}
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
