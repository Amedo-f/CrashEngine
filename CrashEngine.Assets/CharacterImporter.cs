using Assimp;
using SysMat = System.Numerics.Matrix4x4;
using SysQuat = System.Numerics.Quaternion;

namespace CrashEngine.Assets;

// Amedo 2026-10-05
public sealed class RawJoint
{
    public required string Name;
    public int Parent = -1;
    public SysMat BindWorld = SysMat.Identity;
}

// Amedo 2026-10-05
public sealed class RawSkinnedSubmesh
{
    public required RawSubmesh Mesh;
    public required int[] Joint;
    public required float[] Weight;
}

// Amedo 2026-10-05
public sealed class RawClip
{
    public required string Name;
    public int Fps;
    public required SysMat[][] Local;
}

// Amedo 2026-10-05
public sealed class RawCharacter
{
    public readonly List<RawJoint> Joints = new();
    public readonly List<RawSkinnedSubmesh> Meshes = new();
    public readonly List<RawClip> Clips = new();
    public readonly List<string> Notes = new();
}

// Amedo 2026-10-05
public static class CharacterImporter
{
    public const int WeightsPerVertex = 3;
    public const int MaxGameFps = 31;
    public const int FallbackFps = 25;

    private const PostProcessSteps Steps = PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.FlipUVs |
                                           PostProcessSteps.GenerateUVCoords | PostProcessSteps.JoinIdenticalVertices;

    public static bool HasSkeleton(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".glb" or ".gltf" or ".obj" or ".stl" or ".ply") return false;
        try
        {
            using var ctx = AssimpNative.NewContext(); // Amedo 2026-11-20
            var scene = ctx.ImportFile(path, PostProcessSteps.None);
            return scene.Meshes.Any(m => m.HasBones);
        }
        catch { return false; }
    }

    private static SysMat ToSys(Matrix4x4 m) => new(m.A1, m.B1, m.C1, m.D1, m.A2, m.B2, m.C2, m.D2,
                                                    m.A3, m.B3, m.C3, m.D3, m.A4, m.B4, m.C4, m.D4);

    private static SysMat Scaled(SysMat m, float unit)
    {
        m.M41 *= unit; m.M42 *= unit; m.M43 *= unit;
        return m;
    }

    public static RawCharacter? Extract(string path, out string error)
    {
        error = "";
        var raw = ModelImporter.ExtractRawSubmeshes(path);
        if (raw is null || raw.Count == 0) { error = $"no meshes could be read from the file {ModelImporter.LastReport}"; return null; } // Amedo
        using var ctx = AssimpNative.NewContext(); // Amedo 2026-11-20
        var scene = ctx.ImportFile(path, Steps);
        float unit = 1f;
        if (Path.GetExtension(path).Equals(".fbx", StringComparison.OrdinalIgnoreCase) && ModelImporter.FbxUnitScaleFactor(path) is { } cm && cm > 0)
            unit = (float)(cm / 100.0);

        var boneNames = new HashSet<string>(scene.Meshes.SelectMany(m => m.Bones).Select(b => b.Name));
        if (boneNames.Count == 0) { error = "the file has no skinned bones"; return null; }

        var global = new Dictionary<Node, SysMat>();
        var meshNode = new Dictionary<int, Node>();
        var order = new List<Node>();
        void Walk(Node n, SysMat parent)
        {
            var g = ToSys(n.Transform) * parent;
            global[n] = g;
            foreach (var mi in n.MeshIndices) meshNode.TryAdd(mi, n);
            order.Add(n);
            foreach (var c in n.Children) Walk(c, g);
        }
        Walk(scene.RootNode, SysMat.Identity);

        var wanted = new HashSet<Node>();
        foreach (var n in order)
            if (boneNames.Contains(n.Name))
                for (var p = n; p is not null && p != scene.RootNode; p = p.Parent) wanted.Add(p);
        foreach (var n in order) // Amedo 2026-11-18
            if (!wanted.Contains(n) && n.MeshCount == 0 && n.Parent is { } wp && wanted.Contains(wp)) wanted.Add(n); // Amedo 2026-11-18
        var rc = new RawCharacter();
        var index = new Dictionary<string, int>();
        var nodes = new List<Node>();
        foreach (var n in order)
        {
            if (!wanted.Contains(n)) continue;
            int parent = n.Parent is { } pn && index.TryGetValue(pn.Name, out var pi) ? pi : -1;
            index[n.Name] = rc.Joints.Count;
            rc.Joints.Add(new RawJoint { Name = n.Name, Parent = parent, BindWorld = Scaled(global[n], unit) });
            nodes.Add(n);
        }

        var bound = new HashSet<string>();
        foreach (var (m, mi) in scene.Meshes.Select((m, i) => (m, i)))
        {
            var w = meshNode.TryGetValue(mi, out var node) ? global[node] : SysMat.Identity;
            foreach (var b in m.Bones)
            {
                if (!bound.Add(b.Name) || !index.TryGetValue(b.Name, out int ji)) continue;
                if (SysMat.Invert(ToSys(b.OffsetMatrix), out var inv)) rc.Joints[ji].BindWorld = Scaled(inv * w, unit);
            }
        }

        int rawIndex = 0, unweighted = 0;
        foreach (var n in order)
            foreach (var mi in n.MeshIndices)
            {
                var m = scene.Meshes[mi];
                if (m.VertexCount == 0 || !m.HasFaces) continue;
                if (rawIndex >= raw.Count) break;
                var sub = raw[rawIndex++];
                if (sub.Positions.Length != m.VertexCount) { error = $"mesh '{m.Name}' vertex count mismatch ({sub.Positions.Length} vs {m.VertexCount})"; return null; }
                var influences = new List<(int Joint, float Weight)>[m.VertexCount];
                foreach (var b in m.Bones)
                {
                    if (!index.TryGetValue(b.Name, out int ji)) continue;
                    foreach (var vw in b.VertexWeights)
                        if (vw.Weight > 0f && vw.VertexID < m.VertexCount) (influences[vw.VertexID] ??= new()).Add((ji, vw.Weight));
                }
                var joint = new int[m.VertexCount * WeightsPerVertex];
                var weight = new float[m.VertexCount * WeightsPerVertex];
                int rigidJoint = RigidJoint(n, index); // Amedo 2026-11-18
                if (m.BoneCount == 0) rc.Notes.Add(rigidJoint >= 0 ? $"mesh '{m.Name}' has no bone weights and moves with bone '{rc.Joints[rigidJoint].Name}'" : $"mesh '{m.Name}' has no bone weights and follows the first bone"); // Amedo 2026-11-18
                for (int v = 0; v < m.VertexCount; v++)
                {
                    var list = influences[v];
                    if (list is null || list.Count == 0) { unweighted++; joint[v * WeightsPerVertex] = Math.Max(rigidJoint, 0); weight[v * WeightsPerVertex] = 1f; continue; } // Amedo 2026-11-18
                    var top = list.OrderByDescending(x => x.Weight).Take(WeightsPerVertex).ToList();
                    float sum = top.Sum(x => x.Weight);
                    for (int k = 0; k < top.Count; k++)
                    {
                        joint[v * WeightsPerVertex + k] = top[k].Joint;
                        weight[v * WeightsPerVertex + k] = top[k].Weight / sum;
                    }
                }
                rc.Meshes.Add(new RawSkinnedSubmesh { Mesh = sub, Joint = joint, Weight = weight });
            }
        if (rc.Meshes.Count == 0) { error = "no skinned mesh could be matched to its bones"; return null; }
        if (unweighted > 0) rc.Notes.Add($"{unweighted} vertex(es) had no bone weights and follow the first bone");

        foreach (var anim in scene.Animations)
        {
            double tps = anim.TicksPerSecond > 0 ? anim.TicksPerSecond : FallbackFps;
            int fps = tps is >= 1 and <= MaxGameFps ? (int)Math.Round(tps) : FallbackFps;
            double seconds = anim.DurationInTicks / tps;
            int frames = Math.Max(1, (int)Math.Floor(seconds * fps + 0.5) + 1);
            if (fps != (int)Math.Round(tps)) rc.Notes.Add($"clip '{anim.Name}' resampled from {tps:0.##} to {fps} fps (the game keeps at most {MaxGameFps})");
            var channels = anim.NodeAnimationChannels.ToDictionary(c => c.NodeName, c => c);
            var local = new SysMat[frames][];
            for (int f = 0; f < frames; f++)
            {
                double t = f / (double)fps * tps;
                var pose = local[f] = new SysMat[nodes.Count];
                for (int j = 0; j < nodes.Count; j++)
                {
                    var n = nodes[j];
                    var l = channels.TryGetValue(n.Name, out var ch) ? Sample(ch, t, n.Transform) : ToSys(n.Transform);
                    if (rc.Joints[j].Parent < 0 && n.Parent is { } p) l *= global[p];
                    pose[j] = Scaled(l, unit);
                }
            }
            rc.Clips.Add(new RawClip { Name = anim.Name[(anim.Name.LastIndexOf('|') + 1)..], Fps = fps, Local = local }); // Amedo 2026-11-18
        }
        if (unit != 1f) rc.Notes.Add(FormattableString.Invariant($"FBX unit scaled x{unit:0.####} to meters"));
        return rc;
    }

    // Amedo 2026-11-18
    private static int RigidJoint(Node meshNode, Dictionary<string, int> joints)
    {
        for (var p = meshNode.Parent; p is not null; p = p.Parent)
            if (joints.TryGetValue(p.Name, out int j)) return j;
        return -1;
    }

    private static SysMat Sample(NodeAnimationChannel ch, double t, Matrix4x4 rest)
    {
        rest.Decompose(out var rs, out var rr, out var rt);
        var pos = ch.PositionKeyCount > 0 ? Lerp(ch.PositionKeys, t) : rt;
        var scl = ch.ScalingKeyCount > 0 ? Lerp(ch.ScalingKeys, t) : rs;
        var rot = ch.RotationKeyCount > 0 ? Slerp(ch.RotationKeys, t) : rr;
        var q = SysQuat.Normalize(new SysQuat(rot.X, rot.Y, rot.Z, rot.W));
        return SysMat.CreateScale(scl.X, scl.Y, scl.Z) * SysMat.CreateFromQuaternion(q) * SysMat.CreateTranslation(pos.X, pos.Y, pos.Z);
    }

    private static Vector3D Lerp(List<VectorKey> keys, double t)
    {
        if (t <= keys[0].Time) return keys[0].Value;
        for (int i = 1; i < keys.Count; i++)
        {
            if (t > keys[i].Time) continue;
            var a = keys[i - 1]; var b = keys[i];
            float k = (float)((t - a.Time) / Math.Max(1e-9, b.Time - a.Time));
            return a.Value + (b.Value - a.Value) * k;
        }
        return keys[^1].Value;
    }

    private static Assimp.Quaternion Slerp(List<QuaternionKey> keys, double t)
    {
        if (t <= keys[0].Time) return keys[0].Value;
        for (int i = 1; i < keys.Count; i++)
        {
            if (t > keys[i].Time) continue;
            var a = keys[i - 1]; var b = keys[i];
            float k = (float)((t - a.Time) / Math.Max(1e-9, b.Time - a.Time));
            var q = SysQuat.Slerp(new SysQuat(a.Value.X, a.Value.Y, a.Value.Z, a.Value.W), new SysQuat(b.Value.X, b.Value.Y, b.Value.Z, b.Value.W), k);
            return new Assimp.Quaternion(q.W, q.X, q.Y, q.Z);
        }
        return keys[^1].Value;
    }
}
