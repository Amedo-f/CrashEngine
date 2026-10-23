namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private ushort[] _hullSurfaces_80 = Array.Empty<ushort>();
    private object? _hullSurfacesModel;

    private int HullCount_1F04B8() => _ownHull_4 is not null ? 1 : Ogi is { } ogi ? CrashEngine.Runtime.Physics.OgiHulls.Count(ogi) : (Component5 is not null ? 1 : 0); // Amedo

    private ushort[] HullSurfaces_80()
    {
        var model = CollisionModel;
        if (ReferenceEquals(model, _hullSurfacesModel) && _hullSurfaces_80.Length == HullCount_1F04B8()) return _hullSurfaces_80;
        CopyHullSurfaces_1F0018(model);
        return _hullSurfaces_80;
    }

    private void CopyHullSurfaces_1F0018(object? model)
    {
        _hullSurfacesModel = model;
        int count = HullCount_1F04B8();
        _hullSurfaces_80 = new ushort[count];
        for (int i = 0; i < count; i++) _hullSurfaces_80[i] = World.OwnHullSurface_1F00A0(model, i);
    }

    internal ushort HullSurfaceIndex_1F07D8(int part)
    {
        var table = HullSurfaces_80();
        return part >= 0 && part < table.Length ? table[part] : (ushort)0;
    }

    private void SetSurface_253058(uint w)
    {
        ushort surface = (ushort)(w & 0xFFFF);
        if ((w & 0x10000) != 0)
        {
            var table = HullSurfaces_80();
            for (int i = 0; i < table.Length; i++) table[i] = surface;
            Trace($"surface {surface} on all {table.Length} hull(s)");
            return;
        }
        int hull = (int)((w >> 17) & 0xFF);
        World.SetOwnHullSurface_1F0730(CollisionModel, hull, surface);
        Trace($"own surface of hull {hull} = {surface} (model data)");
    }
}
