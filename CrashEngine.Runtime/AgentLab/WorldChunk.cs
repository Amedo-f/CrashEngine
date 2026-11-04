using System.Numerics;
using CrashEngine.Core;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed class WorldChunk
{
    public string Name { get; }
    public PS2AnyTwinsanityRM2 Rm2 { get; private set; }
    public PS2AnyTwinsanitySM2? Sm2 { get; private set; }
    public NavGraph Nav { get; private set; }
    public SceneryTree? Tree { get; set; }
    public Physics.CollisionWorld? Collision { get; set; }
    public Matrix4x4 ToRoot { get; set; } = Matrix4x4.Identity;
    public Matrix4x4 ToAnchor { get; set; } = Matrix4x4.Identity;
    public Entity? Host { get; set; }
    public object? MeshTables { get; set; }
    public readonly SceneSound[] Sounds = [SceneSound.Default, SceneSound.Default]; // Amedo 2026-10-05
    public int DrawFrame { get; set; } = -1; // Amedo
    public Physics.ChunkRigidBodies? RigidBodies_164; // Amedo
    public InstanceCell?[]? Cells_160; // Amedo
    public readonly List<SoundBox> SoundBoxes_198 = new(); // Amedo

    public void ReleaseData()
    {
        Rm2 = new PS2AnyTwinsanityRM2();
        Sm2 = null;
        Nav = new NavGraph(Rm2);
        Tree = null;
        Collision = null;
        Host = null;
        MeshTables = null;
        RigidBodies_164 = null;
        Cells_160 = null;
    }

    // Amedo
    public bool AddSoundBox_1F1E30(SoundBox box)
    {
        if (SoundBoxes_198.Count == 7) return false;
        SoundBoxes_198.Add(box);
        return true;
    }

    // Amedo
    public bool InSoundBox_1F1E68(Vector3 p)
    {
        foreach (var box in SoundBoxes_198)
            if (box.Contains_1E4BE0(p)) return true;
        return false;
    }

    // Amedo
    public Physics.ChunkRigidBodies ChunkRigidBodiesOf_1F1CF0() => RigidBodies_164 ??= new Physics.ChunkRigidBodies();

    public WorldChunk(string name, PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanitySM2? sm2)
    {
        LiveProbe.Track("worldchunk", this, Path.GetFileName(name));
        Name = name;
        Rm2  = rm2;
        Sm2  = sm2;
        Nav  = new NavGraph(rm2);
    }

    public Vector3 PointToRoot(Vector3 p) => Vector3.Transform(p, ToRoot);
}

// Amedo 2026-10-05
public readonly record struct SceneSound(byte Kind, bool Flag100, bool Flag200, float A, float B, float C)
{
    public static readonly SceneSound Default = new(0xFF, false, false, 0f, 0f, 1f);
}
