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

    private readonly List<Action<float>> _pulseResetters = new();

    private void UpdateSelectionPulse()
    {
        foreach (var reset in _pulseResetters) reset(1f);
        _pulseResetters.Clear();
        if (_selectedSet.Count == 0 || PlayFullView) return; // Amedo 2026-09-28

        float pulse = 1f + MathF.Sin(EngineTime.Total * 4.5f) * 0.3f;

        void Pulse(Action<float> set) { set(pulse); _pulseResetters.Add(set); }

        foreach (var root in _selectedSet)
            foreach (var e in AllEntities(root))
            {
                if (e.Get<CrashEngine.Importer.MeshRenderer>() is { } mr1) Pulse(v => mr1.SelectPulse = v);
                if (e.Get<CrashEngine.Renderer.MeshRenderer>()  is { } mr2) Pulse(v => mr2.SelectPulse = v);
                if (e.Get<DirectCubeRenderer>()                 is { } dcr) Pulse(v => dcr.SelectPulse = v);
            }
    }
}
