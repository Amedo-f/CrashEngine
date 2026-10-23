using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabWorld
{
    private const int TriggerListCap = 0x20;

    private readonly List<LayoutTrigger> _playerTriggers = new();
    private readonly List<LayoutTrigger> _touching = new();
    private readonly List<AgentLabAgent> _activators = new();
    private bool _triggersLoaded;

    public List<LayoutTrigger> Triggers { get; } = new();
    public bool TraceTriggers { get; set; }
    public IReadOnlyList<LayoutTrigger> PlayerTriggers => _playerTriggers;

    private int Ticks => (int)(Now * 9000.0);

    // Amedo
    internal void LoadChunkTriggers_26DDE8(WorldChunk? chunk = null)
    {
        if (!_triggersLoaded) LoadTriggers();
        else if (chunk is not null && !Triggers.Any(t => ReferenceEquals(t.Chunk, chunk))) LoadTriggersOf(chunk);
    }

    private void LoadTriggers()
    {
        _triggersLoaded = true;
        foreach (var chunk in Chunks) LoadTriggersOf(chunk); // Amedo 2026-10-01
        Log?.Invoke($"[triggers] {Triggers.Count} layout triggers ({Triggers.Count(t => (t.Flags & LayoutTrigger.CharacterFlag) != 0)} tested by the character, {Chunks.Sum(c => c.SoundBoxes_198.Count)} sound boxes)");
    }

    private int _nextTriggerIndex; // Amedo 2026-10-01

    // Amedo 2026-10-01
    private void LoadTriggersOf(WorldChunk chunk)
    {
        chunk.SoundBoxes_198.Clear(); // Amedo
        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout = chunk.Rm2.GetItem<BaseTwinSection>((uint)lid);
            var instances = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION);
            var triggers = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_TRIGGERS_SECTION);
            if (instances is null || triggers is null) continue;
            for (int i = 0; i < triggers.GetItemsAmount(); i++)
                if (triggers.GetItem(i) is PS2AnyTrigger t)
                {
                    if ((t.Trigger.Header & 0xFF) == 0) // Amedo: RegisterTrigger 0x266100
                    {
                        chunk.AddSoundBox_1F1E30(new SoundBox(t));
                        continue;
                    }
                    var trigger = new LayoutTrigger(this, t, instances, lid - Constants.LEVEL_LAYOUT_1_SECTION) { Index = _nextTriggerIndex++, Chunk = chunk };
                    Triggers.Add(trigger);
                    trigger.Cell.BoxMin_40 = trigger.Bounds.Min; // Amedo
                    trigger.Cell.BoxMax_50 = trigger.Bounds.Max;
                    CellsSort_1EB020(trigger.Cell, CellsOf_160(chunk));
                }
        }
    }

    // Amedo 2026-10-01
    private void DropTriggersOf(WorldChunk chunk)
    {
        chunk.SoundBoxes_198.Clear(); // Amedo
        foreach (var t in Triggers) if (ReferenceEquals(t.Chunk, chunk)) CellsLeave(t.Cell); // Amedo
        Triggers.RemoveAll(t => ReferenceEquals(t.Chunk, chunk));
        _playerTriggers.RemoveAll(t => ReferenceEquals(t.Chunk, chunk));
    }

    // Amedo 2026-09-29
    private void ResetTriggers()
    {
        foreach (var chunk in Chunks) chunk.SoundBoxes_198.Clear(); // Amedo
        foreach (var t in Triggers) CellsLeave(t.Cell); // Amedo
        Triggers.Clear();
        _playerTriggers.Clear();
        _touching.Clear();
        _activators.Clear();
        _triggersLoaded = false;
    }

    private void StepTriggers(WorldChunk chunk) // Amedo 2026-10-01
    {
        if (!_triggersLoaded) LoadTriggers();
        int now = Ticks;
        foreach (var t in Triggers)
        {
            if (!ReferenceEquals(t.Chunk, chunk)) continue; // Amedo 2026-10-01
            if ((t.Flags & LayoutTrigger.CharacterFlag) != 0) continue;
            if (now - t.LastCheck < t.Interval)
            {
                if (t.LastCheck == 0) t.LastCheck = now;
                continue;
            }
            _activators.Clear();
            GatherActivators(t, _activators);
            t.Process(_activators);
            t.LastCheck = now;
        }
    }

    private void GatherActivators(LayoutTrigger t, List<AgentLabAgent> into)
    {
        foreach (var a in Agents)
        {
            if (a.Destroyed || !a.Entity.Active || (a.AgentFlags & 1) != 0 || (a.ComponentMask & t.Mask) == 0) continue;
            if (!ReferenceEquals(a.Chunk, t.Chunk)) continue; // Amedo 2026-09-28
            bool hit = false;
            if (ReferenceEquals(a, Player) && PlayerCharacter is { } c) hit = t.Overlaps(c.TriggerHull, a.GameMatrix);
            else if (a.Body() is { } body)
                foreach (var (hull, m) in body.Parts)
                    if (t.Overlaps(hull, m)) { hit = true; break; }
            if (!hit) continue;
            into.Add(a);
            if (into.Count >= TriggerListCap) break;
        }
    }

    // Amedo
    internal void CheckTriggers_131320(Character.CharacterController c)
    {
        if (ReferenceEquals(c, PlayerCharacter)) CharacterTriggers();
    }

    // Amedo
    internal void ClearTriggers_110(Character.CharacterController c)
    {
        if (ReferenceEquals(c, PlayerCharacter)) _playerTriggers.Clear();
    }

    private void CharacterTriggers()
    {
        if (!_triggersLoaded) LoadTriggers();
        if (PlayerCharacter is not { } c || Player is not { Destroyed: false } player) return;
        var m = player.GameMatrix;
        _touching.Clear();
        foreach (var t in Triggers)
        {
            if (!ReferenceEquals(t.Chunk, player.Chunk)) continue; // Amedo 2026-09-28
            if (!t.Overlaps(c.TriggerHull, m)) continue;
            _touching.Add(t);
            if (_touching.Count >= TriggerListCap) break;
        }
        int i = 0, j = 0;
        while (i < _playerTriggers.Count && j < _touching.Count)
        {
            var o = _playerTriggers[i];
            var n = _touching[j];
            if (ReferenceEquals(o, n)) { CharacterInTrigger(n, player, true); i++; j++; }
            else if (o.Index < n.Index) { CharacterInTrigger(o, player, false); i++; }
            else { CharacterInTrigger(n, player, true); j++; }
        }
        for (; i < _playerTriggers.Count; i++) CharacterInTrigger(_playerTriggers[i], player, false);
        for (; j < _touching.Count; j++) CharacterInTrigger(_touching[j], player, true);
        _playerTriggers.Clear();
        _playerTriggers.AddRange(_touching);
    }

    private void CharacterInTrigger(LayoutTrigger t, AgentLabAgent character, bool inside)
    {
        if ((t.Flags & LayoutTrigger.CharacterFlag) == 0) return;
        if (!t.AnyActivator && !ReferenceEquals(character, Player)) return;
        _activators.Clear();
        if (inside) _activators.Add(character);
        t.Process(_activators);
        t.LastCheck = Ticks;
    }
}
