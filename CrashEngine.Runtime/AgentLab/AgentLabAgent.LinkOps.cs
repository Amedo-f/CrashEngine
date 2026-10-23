namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    // Amedo
    private void ReleaseCached_24D858(uint arg0)
    {
        if ((AgentFlags & 0x80) == 0 || CachedTemplate() is not { } held) return;
        if (_hasLinkList) UnlinkInstance_196520(held, update: true); // Amedo
        ushort id = (ushort)(arg0 & 0xFFFF);
        if (id == 0xFFFF) return;
        Trace($"released {held.Entity.Name}, message {id}");
        held.Deliver(id, this);
    }

    // Amedo
    private void LinkReferenced_24C270(uint arg0)
    {
        var who = (arg0 & 7) switch
        {
            0 => FocusObject,
            1 => Alive(ref _slot118),
            2 => CachedTemplate(),
            _ => null,
        };
        if (who is not null) AddLink(who);
    }

    private void UnlinkFromTarget_210DB8(uint w)
    {
        if ((w & 0xFF) != 0xFF && (w & 0x200) == 0)
        {
            if (CodeObject((int)(w & 0xFF)) is { } target && target.UnlinkFrom_210DB8(this, (w & 0x800) != 0)) Trace($"unlinked from {target.Entity.Name}"); // Amedo
            return;
        }
        if ((w & 0x100) != 0)
        {
            if (_links.Count == 0) return;
            for (_linkCursor = 0; ; _linkCursor = (_linkCursor + 1) & 0x1F)
            {
                LinkAt(_linkCursor)?.UnlinkFrom_210DB8(this, (w & 0x800) != 0); // Amedo
                if (_linkCursor >= _links.Count - 1) break;
            }
            return;
        }
        AgentLabAgent? to = null;
        if ((w & 0x400) != 0) to = LinkAt(_linkCursor);
        else if ((w & 0x200) != 0) to = LinkAt((int)(w & 0xFF));
        to?.UnlinkFrom_210DB8(this, (w & 0x800) != 0); // Amedo
    }

    // Amedo
    private bool UnlinkFrom_210DB8(AgentLabAgent who, bool force) => _hasLinkList && UnlinkInstance_196520(who, update: true, force: force);

    private void AddToLinkedObjectsByte_218FD8(uint filterAndIndex, uint select, uint valueWord)
    {
        if (!_hasLinkList && _links.Count == 0) return;
        ushort filter = (ushort)(filterAndIndex & 0xFFFF);
        int index = (int)(filterAndIndex >> 16);
        int value = ValueOf(valueWord);
        void Add(AgentLabAgent? t)
        {
            if (t is null || (filter != 0xFFFF && (ushort)t.Instance.ObjectId != filter)) return;
            t.Counters[index] = Math.Clamp(t.Counters.GetValueOrDefault(index) + value, 0, 255);
        }
        if ((select & 0x100) != 0)
        {
            for (int i = 0; i < _links.Count; i++) Add(LinkAt(i));
            return;
        }
        int at;
        if ((select & 0x400) != 0) at = 0;
        else if ((select & 0x800) != 0) at = (_links.Count - 1) & 0xFF;
        else if ((select & 0x200) != 0) at = _linkCursor;
        else at = (int)(select & 0xFF);
        if (at != 0xFF) Add(LinkAt(at));
    }

    private void ModifyLinkedCounters_212528(int index, int value, uint filterWord)
    {
        ushort filter = (ushort)(filterWord & 0xFFFF);
        for (int i = 0; i < _links.Count; i++)
        {
            if (LinkAt(i) is not { } t) continue;
            if (filter != 0xFFFF && (t.Instance.ObjectId & 0x7FFF) != filter) continue;
            t.Counters[index] = Math.Clamp(t.Counters.GetValueOrDefault(index) + value, 0, 255);
        }
    }

    // Amedo
    private const uint LinkBusy = 0x100;

    // Amedo
    private int FindLink_129BF8(bool busy, int index, int steps)
    {
        int dir = steps < 1 ? -1 : 1, last = _links.Count - 1;
        for (int n = 0; ; n += dir, index += dir)
        {
            if (index < 0 || index > last) return -1;
            if (((_links[index].Agent?.AgentFlags ?? 0u) & LinkBusy) != 0 == busy) return index; // Amedo
            if (n == steps) return -1;
            if (index == 0 && steps < 0) return -1;
            if (index == last && !(steps < 1)) return -1;
        }
    }

    // Amedo
    private int CountBusyLinks_129CA8(uint from, uint to)
    {
        int busy = 0;
        for (uint i = from; i < to && i < (uint)_links.Count; i++)
            if (((_links[(int)i].Agent?.AgentFlags ?? 0u) & LinkBusy) != 0) busy++; // Amedo
        return busy;
    }

    // Amedo
    private void SetLinkedNearestPlayer_1250E0(uint arg0)
    {
        if (!_hasLinkList && _links.Count == 0 || World.Player is not { } player) return;
        var p = player.GamePosition;
        uint b0 = arg0 & 0xFF, b1 = (arg0 >> 8) & 0xFF;
        uint from = b0 != 0xFF ? b0 - 1 : 0, to = b1 != 0xFF ? b1 : (uint)_links.Count;
        int nearest = -1;
        float best = 1e30f;
        for (uint i = from; i < to && i < (uint)_links.Count; i++)
        {
            if (_links[(int)i].Agent is not { } linked) continue; // Amedo
            var (min, max) = linked.TreeBox;
            var c = (max - min) * 0.5f + min;
            float dx = p.X - c.X, dy = p.Y - c.Y, dz = p.Z - c.Z;
            float d = dx * dx + dy * dy + dz * dz;
            if (d < best)
            {
                best = d;
                nearest = (int)i;
            }
        }
        int ahead = FindLink_129BF8(true, nearest, 10), behind = FindLink_129BF8(true, nearest, -10);
        int side = 1, result;
        if (CountBusyLinks_129CA8(from, to) < 2) result = nearest;
        else if (ahead == -1) result = behind == -1 ? nearest : FindLink_129BF8(false, (int)((uint)(nearest - 1 + (int)to) >> 1), -10);
        else if (behind == -1)
        {
            result = FindLink_129BF8(false, (int)((uint)(nearest + (int)from) >> 1), 10);
            side = -1;
        }
        else if ((uint)(nearest - behind) < (uint)(ahead - nearest)) result = FindLink_129BF8(false, nearest, 10);
        else
        {
            result = FindLink_129BF8(false, nearest, -10);
            side = -1;
        }
        if (result == -1)
        {
            _slot118 = null;
            return;
        }
        if (_links[result].Agent is not { } chosen) return; // Amedo
        if ((chosen.AgentFlags & LinkBusy) != 0)
        {
            _ = side > 0 ? FindLink_129BF8(false, 0, 100) : FindLink_129BF8(false, (int)to - 1, -100);
            if (nearest < 0) return;
            if (_links[nearest].Agent is not { } near) return; // Amedo
            chosen = near;
        }
        _slot118 = chosen;
        chosen.SetAgentFlags(LinkBusy);
    }
}
