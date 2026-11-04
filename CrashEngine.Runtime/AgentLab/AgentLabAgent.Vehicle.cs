using System.Numerics;
using CrashEngine.Runtime.Character.Vehicles;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-29
public sealed partial class AgentLabAgent
{
    private (Vector3 Min, Vector3 Max, bool Has)? _ogiBox;
    private ConvexHull? _setBoxHull;

    internal void SetCollidable(bool on) => AgentFlags = on ? AgentFlags | 0x10u : AgentFlags & ~0x10u;

    internal void SnowCloud_150AD0(Matrix4x4? matrix)
    {
        if (CachedTemplate() is not { } cloud) return;
        cloud.AgentFlags &= ~0x10u;
        if (matrix is { } m)
        {
            cloud.AgentFlags |= 0x400u;
            cloud.GameMatrix = m;
        }
        else cloud.AgentFlags &= ~0x410u;
        cloud.ApplyRendered();
    }

    internal void SetBoxOverride_1F0168((Vector3 Min, Vector3 Max)? box)
    {
        if (box is { } b)
        {
            if (_setBoxHull is null) _setBoxHull = ConvexHull.Box(b.Min, b.Max);
            else _setBoxHull.SetBox(b.Min, b.Max);
            _ownHull_4 = _setBoxHull;
            _ogiBox ??= (_boxMin, _boxMax, _hasBox);
            _boxMin = b.Min;
            _boxMax = b.Max;
            _hasBox = true;
            CellBox_1EB158(); // Amedo
            return;
        }
        _ownHull_4 = null;
        if (_ogiBox is not { } saved) return;
        (_boxMin, _boxMax, _hasBox) = saved;
        _ogiBox = null;
        CellBox_1EB158(); // Amedo
    }

    private void SetVehicleRollerbrawl_1247E8(uint arg0)
    {
        int a = (int)(arg0 & 0xFF), b = (int)((arg0 >> 8) & 0xFF);
        var driver = a == 6 ? null : World.PlayerSlot_570(a);
        var rider = b == 6 ? null : World.PlayerSlot_570(b);
        if (driver is null || rider is null) return;
        if (driver.OwnCharacter is not { } c1 || rider.OwnCharacter is not { } c2) return;
        World.Log?.Invoke($"[vehicle {World.Now:0.00}s] 570: {driver.Entity.Name} + {rider.Entity.Name}");
        c1.EnterVehicle_13B938(Vehicle.Rollerbrawl, c2, 0);
        uint p = (World.Progression & 0xFFFFFF0Fu) | 0x30u;
        p = (p & 0xFFFFF0FFu) | ((uint)(c1.Bones.Kind & 0xF) << 8);
        World.Progression = (p & 0xFFFF0FFFu) | ((uint)(c2.Bones.Kind & 0xF) << 12);
    }

    // Amedo
    private void SetVehicleHoverboard_124DE0(uint arg0)
    {
        if (CommandTarget(arg0 & 0xFF) is not { } target) return;
        if (target.OwnCharacter is not { } c || ControllerType < 0) return;
        bool armed = (arg0 & 0x100) != 0;
        World.Log?.Invoke($"[vehicle {World.Now:0.00}s] 571: {target.Entity.Name} rides {Entity.Name}{(armed ? " (armed)" : "")}");
        c.EnterVehicle_13B938(Vehicle.Hoverboard, null, armed ? 1 : 0, this);
        uint p = (World.Progression & 0xFFFFFF0Fu) | ((armed ? 7u : 6u) << 4);
        p = (p & 0xFFFFF0FFu) | ((uint)(c.Bones.Kind & 0xF) << 8);
        World.Progression = (p & 0xFFFF0FFFu) | 0x6000u;
    }

    // Amedo
    private void WrestleCreature_124950()
    {
        if (FocusObjectAwake_88() is not { } focus) return;
        if (OwnCharacter is not { } c || focus.ControllerType != 15) return;
        World.Log?.Invoke($"[vehicle {World.Now:0.00}s] 601: {Entity.Name} wrestles {focus.Entity.Name}");
        c.EnterVehicle_13B938(Vehicle.Wrestle, null, 0, focus);
    }

    private void SetPlayerMode_120288(uint a0, uint a1, uint a2) =>
        World.Progression = (World.Progression & 0xFFFF000Fu) | ((a0 & 0xF) << 4) | ((a1 & 0xF) << 8) | ((a2 & 0xF) << 12);

    private void ExitVehicleMode_1298E0(uint arg0)
    {
        int slot = (int)arg0;
        if ((slot == 6 ? null : World.PlayerSlot_570(slot)) is not { OwnCharacter: { } c }) return;
        c.Binding = null;
        bool had = c.Vehicle is not null;
        c.ExitVehicle_13BCF0(false);
        if (had) World.Progression = (World.Progression & 0xFFFF0F0Fu) | 0x6010u;
    }

    // Amedo 2026-10-01
    private void TouchResponse_10D060(uint arg0)
    {
        switch (arg0 & 3)
        {
            case 1: Motion10 |= 0x10000; break;
            case 2: Motion10 &= ~0x10000u; break;
        }
        switch ((arg0 >> 4) & 3)
        {
            case 1: Motion10 |= 0x40000; break;
            case 2: Motion10 &= ~0x40000u; break;
        }
        switch ((arg0 >> 10) & 3)
        {
            case 1: Motion10 |= 0xE000; break;
            case 2: Motion10 &= ~0xE000u; break;
        }
    }

    private bool IsVehicleRollerbrawl_128CE8() => IsVehicleRollerbrawl_128CE8(OwnCharacter); // Amedo 2026-10-05

    private static bool IsVehicleRollerbrawl_128CE8(Character.CharacterController? c) =>
        c?.Vehicle is { } v &&
        (v.Kind == Vehicle.Rollerbrawl || v.Kind == Vehicle.Passenger && v.Other?.Vehicle?.Kind == Vehicle.Rollerbrawl);

    // Amedo
    private void SuspendPlayerVehicle_1299C8(int slot, bool on)
    {
        if (slot == 6 || World.PlayerSlot_570(slot) is not { } player || (player.ComponentMask & (1u << 11)) == 0) return;
        if (player.OwnCharacter?.Vehicle is not { } vehicle) return;
        vehicle.Suspended = on;
    }
}
