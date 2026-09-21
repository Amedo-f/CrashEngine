using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using System.Numerics;
using System.Text.Json;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private sealed class CollisionMarkerData
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";

        public float[] Position { get; set; } = new float[3];
        public float[] Rotation { get; set; } = new float[4];
        public float[] Scale { get; set; } = new float[] { 1f, 1f, 1f };
        public float[]? LocalMatrix { get; set; }

        public int SurfaceIndex { get; set; }

        public int[] OwnedVecIndices { get; set; } = System.Array.Empty<int>();
        public int[] OwnedTriIndices { get; set; } = System.Array.Empty<int>();

        public float[]? Corners { get; set; }
        public float Radius { get; set; }
        public float Height { get; set; }
        public int Segments { get; set; }
        public float Width { get; set; }
        public float Length { get; set; }
        public float[]? GenVertices { get; set; }
        public int[]? GenTriangles { get; set; }

        public string ParentKind { get; set; } = "Collision";
        public uint ParentSourceId { get; set; }
        public int ParentChildIndex { get; set; } = -1;
        public float[] ParentPos { get; set; } = new float[3];
    }

    private sealed class CollisionMarkersFile
    {
        public List<CollisionMarkerData> Markers { get; set; } = new();
    }

    private static readonly JsonSerializerOptions CollisionMarkerJsonOptions = new() { WriteIndented = true };

    private string GetCollisionMarkersPath()
    {
        var root = ModeDir("CollisionMarkers");
        var rel = _rm2.Replace('/', '\\');
        if (rel.EndsWith(".rm2", System.StringComparison.OrdinalIgnoreCase)) rel = rel[..^4];
        return Path.Combine(root, rel + ".json");
    }

    private void SaveCollisionMarkers(Entity chunkRoot)
    {
        var file = new CollisionMarkersFile();

        foreach (var e in Roots.SelectMany(AllEntities))
        {
            var shape = e.Get<CollisionShapeMarker>();
            if (shape is null) continue;

            var d = new CollisionMarkerData
            {
                Name = e.Name,
                SurfaceIndex = shape.SurfaceIndex,
                OwnedVecIndices = shape.OwnedVecIndices.ToArray(),
                OwnedTriIndices = shape.OwnedTriIndices.ToArray(),
            };

            var t = e.Transform;
            d.Position = new[] { t.Position.X, t.Position.Y, t.Position.Z };
            d.Rotation = new[] { t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Rotation.W };
            d.Scale = new[] { t.Scale.X, t.Scale.Y, t.Scale.Z };
            if (t.LocalMatrix is { } lm)
                d.LocalMatrix = new[] { lm.M11, lm.M12, lm.M13, lm.M14, lm.M21, lm.M22, lm.M23, lm.M24,
                                        lm.M31, lm.M32, lm.M33, lm.M34, lm.M41, lm.M42, lm.M43, lm.M44 };

            switch (shape)
            {
                case CollisionBoxMarker box:
                    d.Type = "Box";
                    d.Corners = new float[24];
                    for (int i = 0; i < 8; i++) { d.Corners[i * 3] = box.Corners[i].X; d.Corners[i * 3 + 1] = box.Corners[i].Y; d.Corners[i * 3 + 2] = box.Corners[i].Z; }
                    break;
                case CollisionCylinderMarker cyl:
                    d.Type = "Cylinder"; d.Radius = cyl.Radius; d.Height = cyl.Height; d.Segments = cyl.Segments;
                    break;
                case CollisionPlaneMarker pln:
                    d.Type = "Plane"; d.Width = pln.Width; d.Length = pln.Length;
                    break;
                case GeneratedCollisionMarker gen:
                    d.Type = "Generated";
                    d.GenVertices = new float[gen.LocalVertices.Count * 3];
                    for (int i = 0; i < gen.LocalVertices.Count; i++) { d.GenVertices[i * 3] = gen.LocalVertices[i].X; d.GenVertices[i * 3 + 1] = gen.LocalVertices[i].Y; d.GenVertices[i * 3 + 2] = gen.LocalVertices[i].Z; }
                    d.GenTriangles = new int[gen.LocalTriangles.Count * 3];
                    for (int i = 0; i < gen.LocalTriangles.Count; i++) { d.GenTriangles[i * 3] = gen.LocalTriangles[i].A; d.GenTriangles[i * 3 + 1] = gen.LocalTriangles[i].B; d.GenTriangles[i * 3 + 2] = gen.LocalTriangles[i].C; }
                    break;
                default:
                    continue;
            }

            var parent = e.Parent;
            if (parent?.Get<SceneryTile>() is { } ptileDirect)
            {
                d.ParentKind = "SceneryTile";
                d.ParentSourceId = ptileDirect.SourceId;
                var wp = parent.Transform.World.Translation;
                d.ParentPos = new[] { wp.X, wp.Y, wp.Z };
            }
            else if (parent?.Parent?.Get<SceneryTile>() is { } ptileGrand)
            {
                d.ParentKind = "SceneryTileChild";
                d.ParentSourceId = ptileGrand.SourceId;
                d.ParentChildIndex = parent.Parent.Children.ToList().IndexOf(parent);
                var wp = parent.Parent.Transform.World.Translation;
                d.ParentPos = new[] { wp.X, wp.Y, wp.Z };
            }
            else if (parent?.Get<InstanceData>() is { } pinst)
            {
                d.ParentKind = "Instance";
                d.ParentSourceId = pinst.Source.GetID();
                var wp = parent.Transform.World.Translation;
                d.ParentPos = new[] { wp.X, wp.Y, wp.Z };
            }

            file.Markers.Add(d);
        }

        var path = GetCollisionMarkersPath();
        try
        {
            if (file.Markers.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(file, CollisionMarkerJsonOptions));
        }
        catch (System.Exception ex) { _browser.Log($"Collision markers: save failed: {ex.Message}"); }
    }

    private void RestoreCollisionMarkers(Entity chunkRoot, bool fromSavedEdits)
    {
        var path = GetCollisionMarkersPath();
        if (!File.Exists(path)) return;

        CollisionMarkersFile? file;
        try { file = JsonSerializer.Deserialize<CollisionMarkersFile>(File.ReadAllText(path)); }
        catch (System.Exception ex) { _browser.Log($"Collision markers: load failed: {ex.Message}"); return; }
        if (file is null || file.Markers.Count == 0) return;

        var gl = Engine.Instance.GL;
        var sceneryTiles = AllEntities(chunkRoot).Where(x => x.Has<SceneryTile>()).ToList();
        var instances = AllEntities(chunkRoot).Where(x => x.Has<InstanceData>()).ToList();
        int restored = 0;

        foreach (var d in file.Markers)
        {
            CollisionShapeMarker marker;
            GpuMesh mesh;

            switch (d.Type)
            {
                case "Box":
                {
                    var box = new CollisionBoxMarker();
                    if (d.Corners is { Length: 24 })
                        for (int i = 0; i < 8; i++) box.Corners[i] = new Vector3(d.Corners[i * 3], d.Corners[i * 3 + 1], d.Corners[i * 3 + 2]);
                    mesh = BuildCollisionBoxMesh(gl, box.Corners);
                    marker = box;
                    break;
                }
                case "Cylinder":
                {
                    var cyl = new CollisionCylinderMarker { Radius = d.Radius, Height = d.Height, Segments = d.Segments };
                    mesh = BuildCollisionCylinderMesh(gl, cyl.Radius, cyl.Height, cyl.Segments);
                    marker = cyl;
                    break;
                }
                case "Plane":
                {
                    var pln = new CollisionPlaneMarker { Width = d.Width, Length = d.Length };
                    mesh = BuildCollisionPlaneMesh(gl, pln.Width, pln.Length);
                    marker = pln;
                    break;
                }
                case "Generated":
                {
                    var gen = new GeneratedCollisionMarker();
                    if (d.GenVertices is { } gv)
                        for (int i = 0; i + 2 < gv.Length; i += 3) gen.LocalVertices.Add(new Vector3(gv[i], gv[i + 1], gv[i + 2]));
                    if (d.GenTriangles is { } gt)
                        for (int i = 0; i + 2 < gt.Length; i += 3) gen.LocalTriangles.Add((gt[i], gt[i + 1], gt[i + 2]));
                    mesh = BuildGeneratedCollisionMesh(gl, gen.LocalVertices, gen.LocalTriangles);
                    marker = gen;
                    break;
                }
                default:
                    continue;
            }

            marker.SurfaceIndex = d.SurfaceIndex;
            if (fromSavedEdits)
            {
                marker.OwnedVecIndices.AddRange(d.OwnedVecIndices);
                marker.OwnedTriIndices.AddRange(d.OwnedTriIndices);
            }

            var entity = new Entity(d.Name);
            entity.Transform.Position = new Vector3(d.Position[0], d.Position[1], d.Position[2]);
            entity.Transform.Rotation = new Quaternion(d.Rotation[0], d.Rotation[1], d.Rotation[2], d.Rotation[3]);
            entity.Transform.Scale = new Vector3(d.Scale[0], d.Scale[1], d.Scale[2]);
            if (d.LocalMatrix is { Length: 16 } m)
                entity.Transform.LocalMatrix = new Matrix4x4(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]);

            var rdr = entity.Add(new DirectCubeRenderer { Mesh = mesh, Color = CollisionDebugColor, OwnsMesh = true });
            rdr.Mat.AlphaBlend = true;

            Entity? targetParent = null;
            if (d.ParentKind == "SceneryTile")
                targetParent = FindNearestByParentKey(sceneryTiles, x => x.Get<SceneryTile>()!.SourceId, d.ParentSourceId, d.ParentPos);
            else if (d.ParentKind == "SceneryTileChild")
            {
                var owningTile = FindNearestByParentKey(sceneryTiles, x => x.Get<SceneryTile>()!.SourceId, d.ParentSourceId, d.ParentPos);
                if (owningTile is not null)
                {
                    var children = owningTile.Children.ToList();
                    if (d.ParentChildIndex >= 0 && d.ParentChildIndex < children.Count)
                        targetParent = children[d.ParentChildIndex];
                    else
                        targetParent = children.FirstOrDefault(c => c.Get<CrashEngine.Importer.MeshRenderer>() is not null) ?? owningTile;
                }
            }
            else if (d.ParentKind == "Instance")
                targetParent = FindNearestByParentKey(instances, x => x.Get<InstanceData>()!.Source.GetID(), d.ParentSourceId, d.ParentPos);

            if (targetParent is not null)
                targetParent.AddChild(entity);
            else
                AttachToCollisionParentOrRoot(entity);

            entity.Add(marker);
            _cubeCount++;
            _cubes.Add(new CubeEntry(entity, d.Name, rdr));
            restored++;
        }

        if (restored > 0) _browser.Log($"Collision markers: restored {restored} editable collision shape(s).");
    }

    private static Entity? FindNearestByParentKey(List<Entity> candidates, System.Func<Entity, uint> idOf, uint wantId, float[] wantPos)
    {
        Entity? best = null;
        float bestDist = float.MaxValue;
        var want = new Vector3(wantPos[0], wantPos[1], wantPos[2]);
        foreach (var c in candidates)
        {
            if (idOf(c) != wantId) continue;
            float dist = Vector3.DistanceSquared(c.Transform.World.Translation, want);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }
        return best;
    }
}
