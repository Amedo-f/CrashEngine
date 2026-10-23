namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabAgent
{
    private const int MaxLinks = 16;

    private readonly List<(AgentLabAgent? Agent, byte Flags)> _links = new(); // Amedo
    private int _linkCursor; // Amedo 2026-10-01
    private bool _hasLinkList; // Amedo 2026-10-05

    public int LinkCount => _links.Count;

    public IEnumerable<AgentLabAgent?> Links => _links.Select(l => l.Agent is { Destroyed: false } a ? a : null);

    private void LoadLinks()
    {
        _links.Clear();
        foreach (var id in Instance.LinkedInstances)
            if (World.Find(Instance.Section, id) is { } other) AddLink(other, true);
    }

    private AgentLabAgent? LinkAt(int index) =>
        index >= 0 && index < _links.Count && _links[index].Agent is { Destroyed: false } a ? a : null;

    private bool AddLink(AgentLabAgent? other, bool isProtected = false)
    {
        AttachmentsOf_196E20(); // Amedo
        return LinkInstance_195AC8(other, isProtected);
    }

    // Amedo
    private int IndexOfLinked_195708(AgentLabAgent? other) => other is null ? -1 : _links.FindIndex(l => ReferenceEquals(l.Agent, other));

    // Amedo
    private bool LinkInstance_195AC8(AgentLabAgent? other, bool isProtected)
    {
        if (IndexOfLinked_195708(other) >= 0) return true;
        if (_links.Count >= MaxLinks) return false;
        _links.Add((other, isProtected ? (byte)1 : (byte)0));
        return true;
    }

    // Amedo
    private bool UnlinkAt_195748(int index, bool force)
    {
        if (!force && (_links[index].Flags & 1) != 0) return false;
        int last = _links.Count - 1;
        _links[index] = _links[last];
        _links.RemoveAt(last);
        return true;
    }

    // Amedo
    private bool NoneKept_195C28() => !_links.Any(l => (l.Flags & 1) != 0);

    // Amedo
    private void UnlinkUnkept_196948()
    {
        for (int i = 0; i < _links.Count;)
            if (!UnlinkAt_195748(i, false)) i++;
    }

    // Amedo
    private void UnlinkMissing_196B00()
    {
        for (int i = 0, n = 0; i < _links.Count && n < _links.Count; n++)
        {
            if (_links[i].Agent is not null) i++;
            else if (!UnlinkAt_195748(i, false)) return;
        }
    }

    // Amedo
    private void ReleaseLinkedInstances_196E88(byte mask)
    {
        for (int i = 0; i < _links.Count;)
        {
            if ((_links[i].Flags & mask) == 0)
            {
                i++;
                continue;
            }
            var linked = _links[i].Agent;
            UnlinkIndex_196CB0(i);
            if (linked is not null) Trace($"released {linked.Entity.Name} (sticky)");
        }
    }

    // Amedo 2026-09-28
    internal void DropLinksTo(AgentLabAgent dead)
    {
        for (int i = _links.Count - 1; i >= 0; i--)
            if (ReferenceEquals(_links[i].Agent, dead)) UnlinkAt_195748(i, true);
    }

    // Amedo 2026-10-01
    private void SetLinkCursor_222BC0(uint arg0)
    {
        uint index = arg0 - 1;
        if (index < (uint)_links.Count) _linkCursor = (int)(index & 0x1F);
    }

    private void NextLink_222938() => _linkCursor = (_linkCursor == _links.Count - 1 ? 0 : _linkCursor + 1) & 0x1F;

    private bool AtLastLink_229C98 => _linkCursor == _links.Count - 1;

    private AgentLabAgent? TakeFirstLink()
    {
        if (_links.Count == 0) return null;
        var first = LinkAt(0);
        UnlinkAt_195748(0, true);
        return first;
    }

    // Amedo
    private void LinkAllOf_195B90(AgentLabAgent other)
    {
        other.AttachmentsOf_196E20();
        foreach (var (agent, flags) in _links.ToList())
            if (agent is { Destroyed: false }) other.LinkInstance_195AC8(agent, (flags & 1) != 0);
    }

    // Amedo 2026-09-29
    private void BroadcastUserMessage_250550(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint w = Arg(0);
        ushort message = (ushort)(w & 0x7FF);
        System.Numerics.Vector3? offset = (w & 0x800) != 0 ? new System.Numerics.Vector3(F(5), F(6), F(7)) : null;
        var center = SourcePosition((int)((w >> 16) & 0xFF), (int)(w >> 24), (int)((w >> 12) & 0xF), offset);
        ushort filter = (ushort)(Arg(9) & 0xFFFF);
        foreach (var o in World.QuerySphere(center, F(1), 2u, 1, this, true, 0, true, 0x80)) // Amedo
            if (o is AgentLabAgent target && (filter == 0xFFFF || (target.Instance.ObjectId & 0x7FFF) == filter))
                target.Deliver(message, this);
    }

    // Amedo
    private bool AttachFilter_24F950(uint arg1, AgentLabAgent target)
    {
        if ((arg1 & 0xC0000000) == 0) return true;
        bool attached = PathIndexOf_194C18(target) >= 0;
        return (arg1 & 0x80000000) != 0 ? attached : !attached;
    }

    private void SendMessage(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        uint arg0 = Arg(0), arg1 = Arg(1);
        ushort id = (ushort)(arg0 & 0xFFFF);
        int index = (int)((arg0 >> 16) & 0xFF);
        bool unlink = (arg1 & 0x80000) != 0;

        if ((arg1 & 0x20000) != 0)
        {
            foreach (var target in Links.ToList()) if (target is not null && AttachFilter_24F950(arg1, target)) target.Deliver(id, this); // Amedo
            if (unlink) UnlinkAll_1966F8(); // Amedo
            return;
        }

        if ((arg1 & 0x1000000) != 0)
        {
            int n = ValueOf(Arg(3));
            int pow = n < 0 ? 0 : n >= 31 ? int.MaxValue : 1 << n;
            uint mask = ((uint)ValueOf(Arg(2)) >> (n * (int)((arg1 >> 25) & 0x1F))) & (uint)(pow - 1);
            for (int i = 0; i < _links.Count && i < 32; i++, mask >>= 1)
            {
                if ((mask & 1) == 0 || LinkAt(i) is not { } target || !AttachFilter_24F950(arg1, target)) continue; // Amedo
                target.Deliver(id, this);
                if (unlink) UnlinkInstance_196520(target, update: true); // Amedo
            }
            return;
        }

        if ((arg1 & 0xFFFF) != 0xFF)
        {
            bool firstOnly = index == 0;
            for (int i = 0; i < _links.Count; i++)
            {
                if (LinkAt(i) is not { } target || (target.Instance.ObjectId & 0x7FFF) != (arg1 & 0xFFFF) || !AttachFilter_24F950(arg1, target)) continue; // Amedo
                target.Deliver(id, this);
                if (unlink) UnlinkInstance_196520(target, update: true); // Amedo
                if (firstOnly) break;
            }
            return;
        }

        AgentLabAgent? to;
        bool canUnlink = true; // Amedo: a participant (index < 0xDE) is never unlinked
        if ((arg1 & 0x10000) != 0)
        {
            if ((arg1 & 0x100000) != 0) index = _links.Count - 1;
            to = LinkAt((arg1 & 0x40000) != 0 ? _linkCursor : index); // Amedo 2026-10-01
        }
        else
        {
            canUnlink = index >= 0xDE; // Amedo
            to = index < 0xDE ? Participant(index) : CodeObject(index);
        }
        if (to is null) return;
        if (unlink && canUnlink) UnlinkInstance_196520(to, update: true); // Amedo: off the links and the attachments
        to.Deliver(id, this);
    }
}
