using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2BehaviourCommand = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommand;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using CrashEngine.Runtime.AgentLab;
using CrashEngine.Runtime.Physics;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
	private Entity? _eventsTarget;
	private List<AnimEvent> _targetEvents = new();
	private readonly List<AnimEvent> _slotEvents = new();
	private float _eventPrevFrame = -1f;
	private readonly List<(Vector3 Pos, double Until, bool Step)> _eventFlashes = new();
	private (PS2BehaviourCommand Cmd, int Slot)? _selEvent;
	private readonly HashSet<PS2BehaviourGraph> _scriptEdited = new(ReferenceEqualityComparer.Instance);
	private (PS2BehaviourCommand Cmd, uint[] Before)? _eventEditUndo;
	private int _eventPreviewSurface = 10;
	private ParticleEffectsRenderer? _animFx;
	private RuntimeParticles? _animParticles;
	private IReadOnlyList<CollisionWorld.Surface>? _animSurfaces;
	private Entity? _animSurfacesTarget;

	private static readonly uint EventStepCol = 0xFF66D96Bu, EventOtherCol = 0xFFE6C24Du, EventSelCol = 0xFF2E9BFFu;
	private const ushort SandStepSystem = 0xF4;

	private sealed class ScriptArgsAction : IEditAction
	{
		public required PS2BehaviourCommand Command;
		public required uint[] Before, After;
		public required Action<PS2BehaviourCommand, uint[]> Apply;
		public void Undo() => Apply(Command, Before);
		public void Redo() => Apply(Command, After);
		public IEnumerable<Entity> AffectedEntities => Array.Empty<Entity>();
	}

	private List<AnimEvent> TargetEvents()
	{
		if (!ReferenceEquals(_eventsTarget, _animTarget))
		{
			_eventsTarget = _animTarget;
			_targetEvents = LoadTargetEvents(_animTarget);
		}
		return _targetEvents;
	}

	private ChunkSource? TargetChunkSource()
	{
		var root = _animTarget;
		while (root?.Parent is not null) root = root.Parent;
		return root?.Get<ChunkSource>();
	}

	private static List<AnimEvent> LoadTargetEvents(Entity? target)
	{
		if (target?.Get<InstanceData>() is not { } data) return new();
		var root = target;
		while (root.Parent is not null) root = root.Parent;
		if (root.Get<ChunkSource>() is not { } src) return new();
		if (AnimEventScan.FindObject(data.ObjectId, src.Rm2, src.GlobalRm2) is not { } obj) return new();
		return AnimEventScan.Scan(AnimEventScan.GraphsOf(obj, src.Rm2, src.GlobalRm2)).OrderBy(e => e.Slot).ThenByDescending(e => e.SecondsBeforeEnd).ToList();
	}

	private void RescanEvents() => _eventsTarget = null;

	private List<AnimEvent> SlotEvents(AnimatedObject.SlotGroup g)
	{
		_slotEvents.Clear();
		foreach (var e in TargetEvents())
			if (e.Slot == g.SlotIndex) _slotEvents.Add(e);
		return _slotEvents;
	}

	private AnimEvent? SelectedEvent(AnimatedObject.SlotGroup g)
	{
		if (_selEvent is not { } sel || sel.Slot != g.SlotIndex) return null;
		foreach (var e in SlotEvents(g))
			if (ReferenceEquals(e.Command, sel.Cmd)) return e;
		return null;
	}

	private bool IsSelected(AnimEvent e) => _selEvent is { } s && ReferenceEquals(s.Cmd, e.Command) && s.Slot == e.Slot;

	private static float EventFrame(AnimEvent e, AnimatedObject.SlotGroup g)
	{
		int frames = g.Anim.MainAnimation.AnimatedTransformations.Count;
		float fps = g.Anim.DefaultFPS > 0 ? g.Anim.DefaultFPS : 30f;
		return Math.Clamp(frames - e.SecondsBeforeEnd * fps, 0f, MathF.Max(0f, frames - 1));
	}

	private static string SurfaceName(int id)
	{
		string n = Enum.IsDefined(typeof(SurfaceType), (ushort)id) ? ((SurfaceType)(ushort)id).ToString() : $"surface {id}";
		if (n.StartsWith("SURF_")) n = n[5..];
		if (n.StartsWith("NORMAL_")) n = n[7..];
		n = n.Replace('_', ' ').ToLowerInvariant();
		return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n[1..] : n;
	}

	private string ExitPointName(int exitPoint, PS2AnyOGI? ogi)
	{
		if (ogi is null || exitPoint >= ogi.ExitPoints.Count) return "the character's root";
		int joint = (int)ogi.ExitPoints[exitPoint].ParentJointIndex;
		return joint >= 0 && joint < ogi.Joints.Count ? $"exit point {exitPoint} on {JointLabel(ogi.Joints[joint])}" : $"exit point {exitPoint}";
	}

	private IReadOnlyList<CollisionWorld.Surface> AnimSurfaces()
	{
		if (_animSurfaces is null || !ReferenceEquals(_animSurfacesTarget, _animTarget))
		{
			_animSurfacesTarget = _animTarget;
			_animSurfaces = TargetChunkSource() is { } src ? CollisionWorld.LevelSurfaces(src.Rm2, src.GlobalRm2) : Array.Empty<CollisionWorld.Surface>();
		}
		return _animSurfaces;
	}

	private CollisionWorld.Surface? SurfaceById(int id)
	{
		foreach (var s in AnimSurfaces())
			if (s.Id == id) return s;
		return null;
	}

	private RuntimeParticles? AnimParticles()
	{
		if (_animPreview is null) return null;
		if (_animParticles is not null && _animFx?.Entity is { } holder && ReferenceEquals(holder.Parent, _animPreview)) return _animParticles;
		if (TargetChunkSource() is not { } src) return null;
		var e = new Entity("__AnimationParticles");
		_animFx = e.Add(new ParticleEffectsRenderer { DistanceGating = false });
		EnsureFxTexturePagesLoaded();
		_animFx.Pages = _fxTexturePages;
		_animFx.Decals = _fxDecals;
		_animPreview.AddChild(e);
		_animParticles = new RuntimeParticles(_animFx, src.GlobalRm2, src.Rm2);
		return _animParticles;
	}

	private string SystemName(ushort sys) =>
		sys == 0xFFFF ? "none" : AnimParticles()?.System(sys) is { } s ? $"0x{sys:X4} {ParticleNames.Clean(s.Name)}" : $"0x{sys:X4}";

	private Matrix4x4? ExitPointMatrix(int exitPoint, Dictionary<int, Matrix4x4> worlds, PS2AnyOGI ogi)
	{
		if (PreviewBody is not { } body) return null;
		if (exitPoint >= ogi.ExitPoints.Count) return body.Transform.World;
		var ep = ogi.ExitPoints[exitPoint];
		return worlds.TryGetValue((int)ep.ParentJointIndex, out var jw) ? MeshDecoder.TwinMatToSys(ep.Matrix) * jw : body.Transform.World;
	}

	private (ushort Particle, bool Footprint, bool Dust, ushort Sound, float Volume) EventEffect(AnimEvent e, int surfaceId)
	{
		if (e.Selector == AnimEventScan.NoSelector) return (e.System, false, false, 0xFFFF, -1f);
		if (SurfaceById(surfaceId) is not { } s) return (e.System, false, false, 0xFFFF, -1f);
		ushort particle = s.ParticleAt(e.Selector);
		bool print = s.Id is 10 or 15;
		float vol = -1f;
		ushort sound = 0xFFFF;
		if (e.Selector < 6) sound = s.SoundAt(e.Selector, out vol);
		return (particle, print, particle == 0xFFFF && s.Id == 10, sound, vol);
	}

	private void FireEventPreview(AnimEvent e, Dictionary<int, Matrix4x4> worlds, PS2AnyOGI ogi)
	{
		if (AnimParticles() is not { } fx || _animPreview is null || PreviewBody is not { } body) return;
		if (ExitPointMatrix(e.ExitPoint, worlds, ogi) is not { } world || !Matrix4x4.Invert(_animPreview.Transform.World, out var toGame)) return;
		var m = world * toGame;
		var (particle, print, dust, _, _) = EventEffect(e, _eventPreviewSurface);
		if (particle != 0xFFFF && fx.Spawn(particle, 1, true, m.Translation) is var h and >= 0) fx.SetEmitMatrix(h, m);
		if (dust) fx.Spawn(SandStepSystem, 1, true, m.Translation);
		if (print)
		{
			var pos = m.Translation;
			if (AnimEventScan.HasOffset(e.Command)) pos += AnimEventScan.Offset(e.Command);
			pos.Y = body.Transform.LocalMatrix is { } lm ? lm.M42 + 0.09f : body.Transform.Position.Y + 0.09f;
			var dir = new Vector3(m.M31, m.M32, m.M33);
			if (Vector3.Dot(Vector3.Normalize(dir), Vector3.UnitZ) < 0.7f) dir = Vector3.UnitZ;
			dir.Y = 0f;
			if (dir.LengthSquared() > 1e-8f) dir = Vector3.Normalize(dir);
			fx.AddDecal_1ADC18(pos, Vector3.UnitY, dir, 1, 0, 1, 1);
		}
	}

	private void DrawEventFlashes(Dictionary<int, Matrix4x4> worlds, PS2AnyOGI ogi)
	{
		double now = ImGui.GetTime();
		_animParticles?.Update();
		if (PreviewAnim is { } anim && _animGroup is { } g)
		{
			float f = anim.Frame;
			if (anim.Playing && _eventPrevFrame >= 0f && f != _eventPrevFrame)
				foreach (var e in SlotEvents(g))
				{
					float ef = EventFrame(e, g);
					bool crossed = f > _eventPrevFrame ? ef > _eventPrevFrame && ef <= f : ef > _eventPrevFrame || ef <= f;
					if (!crossed) continue;
					if (ExitPointMatrix(e.ExitPoint, worlds, ogi) is { } m) _eventFlashes.Add((m.Translation, now + 0.4, e.Selector != AnimEventScan.NoSelector));
					FireEventPreview(e, worlds, ogi);
				}
			_eventPrevFrame = f;
		}
		else _eventPrevFrame = -1f;
		if (_eventFlashes.Count == 0) return;
		var dl = ImGui.GetBackgroundDrawList();
		int sw = ViewW, sh = ViewH; // Amedo
		for (int i = _eventFlashes.Count - 1; i >= 0; i--)
		{
			var (pos, until, step) = _eventFlashes[i];
			float t = (float)(1.0 - (until - now) / 0.4);
			if (t >= 1f) { _eventFlashes.RemoveAt(i); continue; }
			if (!WorldToScreen(pos, sw, sh, out var s)) continue;
			uint col = (step ? EventStepCol : EventOtherCol) & 0x00FFFFFFu | ((uint)(255 * (1f - t)) << 24);
			dl.AddCircle(s, 5f + 18f * t, col, 24, 2.5f);
		}
	}
}
