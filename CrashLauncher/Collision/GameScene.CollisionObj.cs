using CrashEngine.Core;
using CrashEngine.Importer;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private int _maxPolysPerGroup = 22;

    private void ExportCollisionToObj()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2  = chunkRoot?.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (chunkRoot is null || coll is null) { _browser.Log("Export Collision: no loaded chunk/collision."); return; }
        if (coll.Vectors.Count == 0 || coll.Triangles.Count == 0) { _browser.Log("Export Collision: no collision data to export."); return; }

        ShowSaveFileDialog("Export Collision to OBJ", "Wavefront OBJ\0*.obj\0All Files\0*.*\0\0", "collision.obj", path =>
        {
            if (path is null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# CrashEngine collision export — same X-mirror convention as Twinsanity Editor's CollisionImporter");
            sb.AppendLine($"# {coll.Vectors.Count} vertices, {coll.Triangles.Count} triangles");
            sb.AppendLine("# Edit freely in Blender (move/sculpt/delete/add) then use \"Import Collision from OBJ...\".");
            foreach (var v in coll.Vectors)
                sb.AppendLine($"v {(-v.X).ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                               $"{v.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                               $"{v.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            foreach (var t in coll.Triangles)
            {
                if (t.Vector1Index < 0 || t.Vector2Index < 0 || t.Vector3Index < 0 ||
                    t.Vector1Index >= coll.Vectors.Count || t.Vector2Index >= coll.Vectors.Count || t.Vector3Index >= coll.Vectors.Count)
                    continue;
                sb.AppendLine($"f {t.Vector1Index + 1} {t.Vector2Index + 1} {t.Vector3Index + 1}");
            }
            File.WriteAllText(path, sb.ToString());
            _browser.Log($"Exported collision to {path} ({coll.Vectors.Count} vertice(s), {coll.Triangles.Count} triangle(s)).");
        });
    }

    private void ImportCollisionFromObj()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2  = chunkRoot?.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (chunkRoot is null || coll is null) { _browser.Log("Import Collision: no loaded chunk/collision."); return; }

        ShowOpenFileDialog("Import Collision from OBJ", "Wavefront OBJ\0*.obj\0All Files\0*.*\0\0", path =>
        {
        if (path is null) return;

        var newVerts = new List<TwinVec4>();
        var rawTris  = new List<(int a, int b, int c)>();
        int skippedFaces = 0;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            if (parts[0] == "v" && parts.Length >= 4)
            {
                if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                    float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                    newVerts.Add(new TwinVec4(-x, y, z, 1f));
            }
            else if (parts[0] == "f" && parts.Length >= 4)
            {
                int ParseRef(string token)
                {
                    var posStr = token.Split('/')[0];
                    if (!int.TryParse(posStr, out var n)) return -1;
                    return n > 0 ? n - 1 : newVerts.Count + n;
                }
                var faceIdx = new int[parts.Length - 1];
                bool ok = true;
                for (int i = 1; i < parts.Length; i++)
                {
                    faceIdx[i - 1] = ParseRef(parts[i]);
                    if (faceIdx[i - 1] < 0 || faceIdx[i - 1] >= newVerts.Count) ok = false;
                }
                if (!ok) { skippedFaces++; continue; }
                for (int i = 1; i + 1 < faceIdx.Length; i++)
                    rawTris.Add((faceIdx[0], faceIdx[i], faceIdx[i + 1]));
            }
        }

        if (newVerts.Count == 0 || rawTris.Count == 0)
        { _browser.Log($"Import Collision: no usable geometry found in {path}."); return; }

        var triGroups   = new List<List<(int a, int b, int c)>>();
        var vertToGroup = new Dictionary<int, int>();
        foreach (var tri in rawTris)
        {
            int grp;
            bool found = vertToGroup.TryGetValue(tri.a, out grp) || vertToGroup.TryGetValue(tri.b, out grp) || vertToGroup.TryGetValue(tri.c, out grp);
            if (found && triGroups[grp].Count >= _maxPolysPerGroup) found = false;
            if (!found)
            {
                grp = triGroups.Count;
                triGroups.Add(new List<(int, int, int)>());
            }
            triGroups[grp].Add(tri);
            vertToGroup[tri.a] = grp; vertToGroup[tri.b] = grp; vertToGroup[tri.c] = grp;
        }
        for (int i = 0; i < triGroups.Count; i++)
        {
            if (triGroups[i].Count == 0) continue;
            var verts = new HashSet<int>();
            foreach (var t in triGroups[i]) { verts.Add(t.a); verts.Add(t.b); verts.Add(t.c); }
            for (int j = i + 1; j < triGroups.Count; j++)
            {
                if (triGroups[j].Count == 0) continue;
                if (triGroups[i].Count + triGroups[j].Count > _maxPolysPerGroup) continue;
                bool shares = triGroups[j].Any(t => verts.Contains(t.a) || verts.Contains(t.b) || verts.Contains(t.c));
                if (shares) { triGroups[i].AddRange(triGroups[j]); triGroups[j].Clear(); }
            }
        }
        triGroups.RemoveAll(g => g.Count == 0);

        var newTris   = new List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle>();
        var newGroups = new List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation>();
        foreach (var g in triGroups)
        {
            uint off = (uint)newTris.Count;
            foreach (var (a, b, c) in g)
                newTris.Add(new Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle
                { Vector1Index = a, Vector2Index = b, Vector3Index = c, SurfaceIndex = 0 });
            newGroups.Add(new Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation
            { Offset = off, Size = (uint)g.Count });
        }

        var newTriggers = BuildTriggerTree(newGroups, newTris, newVerts);

        coll.Vectors.Clear();   coll.Vectors.AddRange(newVerts);
        coll.Triangles.Clear(); coll.Triangles.AddRange(newTris);
        coll.Groups.Clear();    coll.Groups.AddRange(newGroups);
        coll.Triggers.Clear();  coll.Triggers.AddRange(newTriggers);

        RedrawLevelCollision(chunkRoot!, coll, rm2!);

        _browser.Log($"Imported collision from {path}: {newVerts.Count} vertice(s), {newTris.Count} triangle(s), " +
                     $"{newGroups.Count} group(s), {newTriggers.Count} trigger-tree node(s) " +
                     $"({skippedFaces} malformed face(s) skipped, all SurfaceIndex=0/default). " +
                     "Save Chunk + Build ISO, then test.");
        });
    }
}
