using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabAgent
{
    private void RequestOnTarget(uint arg0, uint arg1)
    {
        var source = _controller ?? this;
        if (source.Slot((int)(arg1 & 0xFFFF)) is not { Graph: { } g } r) return;
        bool control = (arg1 & 0x40000) != 0;
        int channel = (int)((arg1 >> 16) & 1); // Amedo 2026-09-30
        int code = (int)(arg0 & 0xFF);
        if (code != SourceNone)
        {
            if (CodeObject(code) is { } target) target.RequestFrom(g, r.Starter, control ? this : null, channel);
            return;
        }
        if ((arg1 & 0x20000) != 0)
        {
            foreach (var link in Links.ToList())
                link?.RequestFrom(g, r.Starter, control ? this : null, channel);
            return;
        }
        uint objectId = (arg0 >> 16) & 0xFFFF;
        if (objectId == SourceNone) return;
        foreach (var link in Links.ToList())
            if (link is not null && (link.Instance.ObjectId & 0x7FFF) == objectId)
                link.RequestFrom(g, r.Starter, control ? this : null, channel);
    }

    // Amedo 2026-09-30
    private void RequestFrom(PS2BehaviourGraph g, TwinBehaviourStarter? starter, AgentLabAgent? controller, int channel)
    {
        if (controller is not null) _controller = controller;
        Trace($"request '{g.Name}' from {(controller ?? this).Entity.Name}{(channel != 0 ? " on channel 1" : "")}");
        var entered = EnterChannel(channel);
        _pendingBehaviour = (g, starter, this, this);
        LeaveChannel(entered);
    }

    private void PrepareSpeech(uint arg0, uint arg1)
    {
        if (World.Music is not { } music) return;
        int id = (short)IntVariable(arg0);
        Trace($"prepare speech track {id} (0x29DF38)");
        music.Begin(1, id, 1f, 1f, 0f, Entity.Name, autoStart: false, loop: (arg1 & 1) != 0, category: 3); // Amedo 2026-10-06
    }

    // Amedo
    private void SetFocusToOwner_221E68()
    {
        if (_controller is not { } owner) return;
        _hasFocusPosition = false;
        if (owner.Destroyed) // Amedo
        {
            _focusObject = null;
            _focusCamera = false;
            return;
        }
        _focusObject = owner;
        _focusCamera = false;
        _hasFocusObject = true;
        Trace($"focus on owner {owner.Entity.Name}");
    }

    // Amedo
    private void StopSpeech_224B38()
    {
        Trace("stop speech (0x29DD48)");
        World.Music?.Reset_29DD48(1);
    }

    // Amedo
    private void PlayMovie_121028(uint arg0, uint arg1)
    {
        int movie = RootController().IntVariable(arg0);
        int fade = (int)(BitConverter.UInt32BitsToSingle(arg1) * 9000f);
        Trace($"play movie {movie} (fade {fade} ticks)");
        World.RequestMovie_17B868(fade, movie);
    }

    private void PlaySpeech()
    {
        if (World.Music is not { } music) return;
        if (!music.PlaySpeech_29E088(1, 1f, 1f, 0f)) Trace("speech not ready, play queued (0x29E088)"); // Amedo
    }

    private void CharactersToIdle(uint arg0)
    {
        const int IdleSlot = 0xC;
        static void Idle(AgentLabAgent? who) // Amedo
        {
            if (who is not null && !who.Destroyed && (who.ComponentMask & (1u << 12)) != 0) who.TriggerEventSlot(IdleSlot, who, true); // Amedo 2026-11-17
        }
        if ((arg0 & 1) != 0)
        {
            foreach (int slot in (ReadOnlySpan<int>)[0, 1, 2, 3, 5]) Idle(World.PlayerSlot_570(slot)); // Amedo
            return;
        }
        if ((arg0 & 0xE) != 0)
        {
            if ((arg0 & 2) != 0) Idle(World.PlayerSlot_570(0)); // Amedo
            if ((arg0 & 4) != 0) Idle(World.PlayerSlot_570(1));
            if ((arg0 & 8) != 0) Idle(World.PlayerSlot_570(5));
            return;
        }
        Idle(this); // Amedo
    }

    private AgentLabAgent RootController()
    {
        var a = this;
        for (int guard = 0; a._controller is { } c && guard < 16; guard++) a = c;
        return a;
    }

    private AnimatedObject.SlotGroup? ForeignSlot(AgentLabAgent root, int slot, float blend)
    {
        if (root.GameObject is not { } obj) return null;
        ushort animId = slot < obj.AnimationSlots.Count ? obj.AnimationSlots[slot] : (ushort)0xFFFF;
        ushort ogiId = slot < obj.OGISlots.Count ? obj.OGISlots[slot] : (ushort)0xFFFF;
        if (ogiId == 0xFFFF)
        {
            if (animId != 0xFFFF) World.Unimplemented("animation", 0x211B08, "controller slot with an animation but no OGI");
            else Anim?.RequestNone(blend);
            return null;
        }
        var group = World.ForeignGroup(this, ogiId, animId);
        Anim ??= Entity.Get<AnimatedObject>();
        return group;
    }

    private void ReleaseTarget(uint arg0)
    {
        int code = (int)(arg0 & 0xFF);
        if (code != SourceNone)
        {
            if (CodeObject(code) is { } target) target._controller = null;
            return;
        }
        if ((arg0 & 0x200) == 0) return;
        foreach (var link in Links.ToList())
            if (link is not null) link._controller = null;
    }
}
