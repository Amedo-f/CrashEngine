using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed class InstancePlace
{
    public Matrix4x4 Matrix;
    public string? Chunk;
    public uint Flags;

    public InstancePlace Copy() => new() { Matrix = Matrix, Chunk = Chunk, Flags = Flags };
}

// Amedo
public sealed class InstancePlaces
{
    public string? Chunk;
    public readonly List<InstancePlace> Entries = new(8);

    // Amedo
    public static InstancePlaces Construct_198068(AgentLabAgent who, WorldChunk chunk)
    {
        var p = new InstancePlaces { Chunk = chunk.Name };
        p.Entries.Add(who.PlaceOf_195398(chunk, 0));
        return p;
    }

    // Amedo
    public int KeepLoaded_197E98(AgentLabWorld world)
    {
        Entries.RemoveAll(e => world.ChunkNamed_1F0F58(e.Chunk) is not { } c || world.Unloading_1F0F58(c));
        return Entries.Count;
    }

    // Amedo
    public int DropInChunk_197FA0(AgentLabWorld world, WorldChunk? chunk)
    {
        Entries.RemoveAll(e => ReferenceEquals(world.ChunkNamed_1F0F58(e.Chunk), chunk));
        return Entries.Count;
    }

    // Amedo
    public void Add_198130(AgentLabWorld world, Matrix4x4 standIn, WorldChunk chunk)
    {
        KeepLoaded_197E98(world);
        DropInChunk_197FA0(world, chunk);
        Entries.Add(new InstancePlace { Matrix = standIn, Chunk = chunk.Name });
    }

    // Amedo
    public InstancePlace? FindIn_1982C0(AgentLabWorld world, WorldChunk chunk)
    {
        InstancePlace? found = null;
        foreach (var e in Entries)
            if (ReferenceEquals(world.ChunkNamed_1F0F58(e.Chunk), chunk)) found = e;
        return found;
    }
}
