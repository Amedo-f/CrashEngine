using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Runtime.AgentLab;

namespace CrashEngine.Runtime;

// Amedo
public sealed partial class SceneSimulation
{
    private void InitPlaces()
    {
        World.RestartGlobal_197428 = Restart_197428;
        World.ReleaseGlobal_197570 = ReleaseGlobal_197570;
        World.SwitchCharacter_177A90 = SwitchCharacter_177A90;
        World.EnableCharacters_17C0C8 = () => EnableCharacters_17C0C8(true); // Amedo
    }

    private bool SwitchCharacter_177A90(int slot, bool primary)
    {
        var target = slot == 6 ? null : World.PlayerSlot_570(slot);
        if (target is null) return false;
        int shift = primary ? 8 : 12;
        int old = (int)(World.Progression >> shift) & 0xF;
        var before = old == 6 ? null : World.PlayerSlot_570(old);
        if (before?.OwnCharacter is { } was)
        {
            was.Pad = null;
            was.Move = Vector3.Zero; // Amedo
            was.ActivePlayer = false;
        }
        if (primary)
        {
            if (target.OwnCharacter is not { } c)
            {
                if (!_playerPhysics || target.Chunk.Collision is not { } collision) return false;
                c = BindCharacter(target, collision);
            }
            c.Pad = Pad;
            c.ActivePlayer = true;
            World.SetPlayer_30A5A8(target, c);
            if (!ReferenceEquals(World.RootChunk, target.Chunk)) MovePlayer_1954A8(target.Chunk);
            World.CutsceneCamera?.CameraFocusObject_10F2D8(null);
        }
        World.Progression = (World.Progression & ~(0xFu << shift)) | ((uint)slot & 0xF) << shift;
        _log?.Invoke($"[AgentLab {World.Now:0.00}s] switch character: slot {old} ({before?.Entity.Name ?? "-"}) -> slot {slot} ({target.Entity.Name}){(primary ? ", now the player" : ", second")} (0x177A90)");
        return true;
    }

    private static Matrix4x4 GameMatrixOf(Entity e) =>
        e.Transform.LocalMatrix ?? Matrix4x4.CreateScale(e.Transform.Scale) * Matrix4x4.CreateFromQuaternion(e.Transform.Rotation)
                                 * Matrix4x4.CreateTranslation(e.Transform.Position);

    private void MakePlaces_12DF70(AgentLabAgent agent, WorldChunk chunk)
    {
        if (agent.GameObject is { } go && (int)go.Type == 0) agent.MakeInstancePlaces_197E40(chunk);
    }

    private void AddStandIn_12DF70(AgentLabAgent taken, Entity standIn, WorldChunk chunk)
    {
        var places = taken.Places ?? taken.MakeInstancePlaces_197E40(taken.Chunk);
        places.Add_198130(World, GameMatrixOf(standIn), chunk);
        _log?.Invoke($"[stream] {chunk.Name}: {standIn.Name} is a place of {taken.Entity.Name} ({places.Entries.Count} place(s): {string.Join(", ", places.Entries.Select(p => p.Chunk))})");
    }

    private bool ReleaseOnUnload_197570(AgentLabAgent agent, WorldChunk unloading)
    {
        if (agent.Places is not { } places || ReferenceEquals(agent, World.Player) || agent.SpawnedBy is not null) return false;
        World.UnloadingChunk = unloading;
        bool release = agent.ReleasePlaces_1981A8(places);
        World.UnloadingChunk = null;
        if (release) return false;
        _log?.Invoke($"[stream] {unloading.Name} unloads: {agent.Entity.Name} goes global, back next frame at its place in {agent.Information?.Chunk} (0x1981A8)");
        return true;
    }

    private void ReleaseGlobal_197570(AgentLabAgent agent)
    {
        if (agent.Places is { } places && !agent.ReleasePlaces_1981A8(places)) return;
        _log?.Invoke($"[AgentLab {World.Now:0.00}s] {agent.Entity.Name}: global with no loaded place, released (0x197570)");
        agent.Entity.Active = false;
        ReleaseAgent(agent);
    }

    private void Restart_197428(AgentLabAgent agent, InstancePlace info)
    {
        if (World.ChunkNamed_1F0F58(info.Chunk) is not { } target || InstancesOf(target) is not { } home) return;
        var e = agent.Entity;
        var from = agent.Chunk;
        if (!ReferenceEquals(e.Parent, home))
        {
            RememberHome(e); // Amedo
            e.Parent?.RemoveChild(e);
            home.AddChild(e);
        }
        agent.CanChangeChunk_23D060(target); // Amedo
        agent.Chunk = target;
        agent.LeaveGlobal();
        agent.GameMatrix = info.Matrix;
        SnapDisplay(e);
        World.ClearEvents_197B98(agent); // Amedo
        agent.Restart_22F5A8();
        agent.AttachmentsStep_1959D8(); // Amedo
        agent.CharacterRestart_17A950(); // Amedo
        if (agent.OwnCharacter is { } c && target.Collision is { } collision) c.CollisionWorld = collision;
        _log?.Invoke($"[AgentLab {World.Now:0.00}s] {e.Name}: restarted at its place in {target.Name} at {info.Matrix.Translation} (from {from.Name}, 0x198F90 -> 0x22F5A8 -> 0x1954A8)");
    }

    private void ApplyInformation_173040()
    {
        foreach (var agent in World.Agents.ToList())
        {
            if (ReferenceEquals(agent, World.Player) || agent.Information is not { } info) continue;
            if (World.ChunkNamed_1F0F58(info.Chunk) is not { } target) continue;
            if (ReferenceEquals(agent.Chunk, target) && agent.GameMatrix == info.Matrix) continue;
            Restart_197428(agent, info);
        }
    }
}
