using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Renderer;

// Amedo
public sealed partial class GpuMesh
{
    public const float SmoothRadiusScale = 0.06f; // Amedo
    public const float SkinRadiusScale = 0.12f; // Amedo
    public const float SmoothRadiusMax = 0.3f; // Amedo

    public static bool HasSmoothNormals(Vertex[] verts)
    {
        foreach (ref readonly var v in verts.AsSpan())
            if (v.SmoothNormal != Vector3.Zero) return true;
        return false;
    }

    // Amedo
    public static void SmoothNormals(IReadOnlyList<Vertex[]> parts, IReadOnlyList<uint[]> indices)
    {
        int count = 0;
        foreach (var p in parts) count += p.Length;
        if (count < 3 || !SmoothNormalCache.Enabled)
        {
            ComputeSmoothNormals(parts, indices);
            return;
        }
        var key = SmoothNormalCache.KeyOf(parts, indices);
        if (SmoothNormalCache.TryGet(key, count, out var cached))
        {
            if (!SmoothNormalCache.Verify)
            {
                int at = 0;
                foreach (var part in parts)
                    for (int i = 0; i < part.Length; i++) part[i].SmoothNormal = cached[at++];
                return;
            }
            ComputeSmoothNormals(parts, indices);
            int k = 0;
            foreach (var part in parts)
                foreach (ref readonly var v in part.AsSpan())
                    if (!SameBits(v.SmoothNormal, cached[k++]))
                    {
                        Interlocked.Increment(ref SmoothNormalCache.VerifyFailures);
                        return;
                    }
            return;
        }
        ComputeSmoothNormals(parts, indices);
        var result = new Vector3[count];
        int n = 0;
        foreach (var part in parts)
            foreach (ref readonly var v in part.AsSpan()) result[n++] = v.SmoothNormal;
        SmoothNormalCache.Put(key, result);
    }

    private static bool SameBits(Vector3 a, Vector3 b) =>
        BitConverter.SingleToInt32Bits(a.X) == BitConverter.SingleToInt32Bits(b.X) &&
        BitConverter.SingleToInt32Bits(a.Y) == BitConverter.SingleToInt32Bits(b.Y) &&
        BitConverter.SingleToInt32Bits(a.Z) == BitConverter.SingleToInt32Bits(b.Z);

    private static void ComputeSmoothNormals(IReadOnlyList<Vertex[]> parts, IReadOnlyList<uint[]> indices)
    {
        int total = 0;
        foreach (var p in parts) total += p.Length;
        if (total < 3) return;
        var job = new SmoothJob { Parts = parts, Indices = indices, PartStart = new int[parts.Count + 1], GroupOf = new int[total] };
        for (int i = 0; i < parts.Count; i++) job.PartStart[i + 1] = job.PartStart[i] + parts[i].Length;
        var groups = new Dictionary<(int, int, int), int>(total);
        var groupPos = new List<Vector3>(total);
        var lo = new Vector3(float.MaxValue);
        var hi = new Vector3(float.MinValue);
        for (int pi = 0; pi < parts.Count; pi++)
        {
            var verts = parts[pi];
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i].Position;
                lo = Vector3.Min(lo, p);
                hi = Vector3.Max(hi, p);
                var q = p * 10000f;
                var key = ((int)MathF.Round(q.X), (int)MathF.Round(q.Y), (int)MathF.Round(q.Z));
                if (!groups.TryGetValue(key, out int g))
                {
                    groups[key] = g = groupPos.Count;
                    groupPos.Add(p);
                }
                job.GroupOf[job.PartStart[pi] + i] = g;
            }
        }
        int groupCount = groupPos.Count;
        job.GroupPos = groupPos;

        int faceCapacity = 0;
        foreach (var idx in indices) faceCapacity += idx.Length / 3;
        job.FacePart = new int[faceCapacity];
        job.FaceFirst = new int[faceCapacity];
        var faceGroup = new int[faceCapacity * 3];
        var faceNormal = new Vector3[faceCapacity];
        int faces = 0;
        for (int pi = 0; pi < parts.Count; pi++)
        {
            var verts = parts[pi];
            var idx = indices[pi];
            int start = job.PartStart[pi];
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                int g0 = job.GroupOf[start + (int)idx[t]], g1 = job.GroupOf[start + (int)idx[t + 1]], g2 = job.GroupOf[start + (int)idx[t + 2]];
                if (g0 == g1 || g1 == g2 || g0 == g2) continue;
                var a = verts[idx[t]].Position;
                var n = Vector3.Cross(verts[idx[t + 1]].Position - a, verts[idx[t + 2]].Position - a);
                if (GameMath.LengthSquared(n) < 1e-20f) continue;
                job.FacePart[faces] = pi;
                job.FaceFirst[faces] = t;
                faceGroup[faces * 3] = g0;
                faceGroup[faces * 3 + 1] = g1;
                faceGroup[faces * 3 + 2] = g2;
                faceNormal[faces] = n;
                faces++;
            }
        }
        if (faces == 0) return;

        int halfEdges = faces * 3;
        var edgeKey = new long[halfEdges];
        var edgeOwner = new int[halfEdges];
        for (int h = 0; h < halfEdges; h++)
        {
            int a = faceGroup[h], b = faceGroup[h - h % 3 + (h % 3 + 1) % 3];
            edgeKey[h] = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            edgeOwner[h] = h;
        }
        Array.Sort(edgeKey, edgeOwner);
        var twin = new int[halfEdges];
        Array.Fill(twin, -1);
        for (int i = 0; i + 1 < halfEdges; i++)
            if (edgeKey[i] == edgeKey[i + 1] && (i == 0 || edgeKey[i - 1] != edgeKey[i]))
            {
                twin[edgeOwner[i]] = edgeOwner[i + 1];
                twin[edgeOwner[i + 1]] = edgeOwner[i];
            }
        var flip = new bool[faces];
        var seen = new bool[faces];
        var stack = new int[faces];
        for (int first = 0; first < faces; first++)
        {
            if (seen[first]) continue;
            seen[first] = true;
            int top = 0;
            stack[top++] = first;
            while (top > 0)
            {
                int f = stack[--top];
                for (int e = 0; e < 3; e++)
                {
                    int h = f * 3 + e, t = twin[h];
                    if (t < 0) continue;
                    int n = t / 3;
                    if (n == f || seen[n]) continue;
                    flip[n] = (faceGroup[h] == faceGroup[t]) != flip[f];
                    seen[n] = true;
                    stack[top++] = n;
                }
            }
        }
        job.FaceNormal = faceNormal;
        job.FaceUnit = new Vector3[faces];
        for (int f = 0; f < faces; f++)
        {
            if (flip[f]) faceNormal[f] = -faceNormal[f];
            job.FaceUnit[f] = GameMath.Normalize(faceNormal[f]);
        }

        job.GroupStart = new int[groupCount + 1];
        for (int f = 0; f < faces; f++)
            for (int k = 0; k < 3; k++) job.GroupStart[faceGroup[f * 3 + k] + 1]++;
        for (int g = 0; g < groupCount; g++) job.GroupStart[g + 1] += job.GroupStart[g];
        job.GroupFaces = new int[job.GroupStart[groupCount]];
        var fill = (int[])job.GroupStart.Clone();
        for (int f = 0; f < faces; f++)
            for (int k = 0; k < 3; k++) job.GroupFaces[fill[faceGroup[f * 3 + k]]++] = f;

        bool skinned = false;
        foreach (var part in parts)
        {
            foreach (ref readonly var v in part.AsSpan())
                if (v.BoneWeights != Vector3.Zero) { skinned = true; break; }
            if (skinned) break;
        }
        job.Radius = Math.Min(GameMath.Length((hi - lo)) * (skinned ? SkinRadiusScale : SmoothRadiusScale), SmoothRadiusMax);
        if (job.Radius > 1e-4f)
        {
            job.Cells = new Dictionary<(int, int, int), List<int>>();
            for (int g = 0; g < groupCount; g++)
            {
                var c = job.CellOf(groupPos[g]);
                if (!job.Cells.TryGetValue(c, out var list)) job.Cells[c] = list = new List<int>(4);
                list.Add(g);
            }
        }

        job.Done = new bool[total];
        if (groupCount < 4096)
        {
            var stamp = new int[faces];
            Array.Fill(stamp, -1);
            var near = new List<(int, float)>(64);
            for (int g = 0; g < groupCount; g++) job.Run(g, stamp, near);
            return;
        }
        Parallel.For(0, groupCount,
            () =>
            {
                var stamp = new int[faces];
                Array.Fill(stamp, -1);
                return (Stamp: stamp, Near: new List<(int, float)>(64));
            },
            (g, _, local) =>
            {
                job.Run(g, local.Stamp, local.Near);
                return local;
            },
            _ => { });
    }

    // Amedo
    private sealed class SmoothJob
    {
        public required IReadOnlyList<Vertex[]> Parts;
        public required IReadOnlyList<uint[]> Indices;
        public required int[] PartStart;
        public required int[] GroupOf;
        public List<Vector3> GroupPos = null!;
        public int[] FacePart = null!, FaceFirst = null!, GroupStart = null!, GroupFaces = null!;
        public Vector3[] FaceNormal = null!, FaceUnit = null!;
        public float Radius;
        public Dictionary<(int, int, int), List<int>>? Cells;
        public bool[] Done = null!;

        public (int, int, int) CellOf(Vector3 p) =>
            ((int)MathF.Floor(p.X / Radius), (int)MathF.Floor(p.Y / Radius), (int)MathF.Floor(p.Z / Radius));

        private void Take(int other, float weight, int g, int[] stamp, List<(int, float)> near)
        {
            for (int i = GroupStart[other]; i < GroupStart[other + 1]; i++)
            {
                int f = GroupFaces[i];
                if (stamp[f] == g) continue;
                stamp[f] = g;
                near.Add((f, weight));
            }
        }

        public void Run(int g, int[] stamp, List<(int Face, float Weight)> near)
        {
            near.Clear();
            Take(g, 1f, g, stamp, near);
            if (Cells is not null)
            {
                var p = GroupPos[g];
                var (cx, cy, cz) = CellOf(p);
                for (int x = cx - 1; x <= cx + 1; x++)
                    for (int y = cy - 1; y <= cy + 1; y++)
                        for (int z = cz - 1; z <= cz + 1; z++)
                        {
                            if (!Cells.TryGetValue((x, y, z), out var list)) continue;
                            foreach (int other in list)
                            {
                                if (other == g) continue;
                                float d = GameMath.Distance(p, GroupPos[other]);
                                if (d >= Radius) continue;
                                float w = 1f - d / Radius;
                                Take(other, w * w, g, stamp, near);
                            }
                        }
            }

            for (int i = GroupStart[g]; i < GroupStart[g + 1]; i++)
            {
                int f = GroupFaces[i];
                var own = FaceUnit[f];
                var sum = Vector3.Zero;
                foreach (var (other, weight) in near)
                {
                    float facing = GameMath.Dot(FaceUnit[other], own);
                    if (facing <= 0f) continue;
                    sum += FaceNormal[other] * (weight * facing);
                }
                var smooth = GameMath.LengthSquared(sum) < 1e-20f ? own : GameMath.Normalize(sum);
                int pi = FacePart[f];
                var verts = Parts[pi];
                var idx = Indices[pi];
                int start = PartStart[pi];
                for (int k = 0; k < 3; k++)
                {
                    uint vi = idx[FaceFirst[f] + k];
                    int at = start + (int)vi;
                    if (GroupOf[at] != g || Done[at]) continue;
                    Done[at] = true;
                    verts[vi].SmoothNormal = smooth;
                }
            }
        }
    }
}
