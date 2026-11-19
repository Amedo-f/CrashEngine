using CrashEngine.Core;

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

    // Amedo 2026-11-19
    private readonly List<Entity> _selectionOrder = new();
    private readonly HashSet<Entity> _selectionOrderSet = new();

    // Amedo 2026-11-19
    private bool SyncSelectionOrder()
    {
        if (_selectionOrder.Count == _selectedSet.Count && _selectionOrderSet.SetEquals(_selectedSet)) return false;
        _selectionOrder.RemoveAll(e => !_selectedSet.Contains(e));
        _selectionOrderSet.IntersectWith(_selectedSet);
        foreach (var e in _selectedSet)
            if (_selectionOrderSet.Add(e)) _selectionOrder.Add(e);
        return true;
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
