using System.Numerics;
using CrashEngine.Core;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinShader = Twinsanity.TwinsanityInterchange.Common.TwinShader;
using TwinSceneryLeaf = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryLeaf;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashEngine.Importer;

public static class ChunkExporter
{
    public readonly record struct SaveResult(string Rm2Path, string? Sm2Path, int InstancesSynced, int SceneryTilesSynced, string? GlobalRm2Path);

    private const string GlobalRm2RelativePath = @"Startup\Default.rm2";

    public static (int InstancesSynced, int SceneryTilesSynced) SyncEntityGraph(Entity chunkRoot)
    {
        int instSynced = 0, tileSynced = 0;

        var scenery = chunkRoot.Get<ChunkSource>()?.Sm2?.GetItem<PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);

        void Visit(Entity e)
        {
            var inst = e.Get<InstanceData>();
            if (inst is not null && SyncInstance(e, inst)) instSynced++;

            var tile = e.Get<SceneryTile>();
            if (tile is not null && SyncSceneryTile(e, tile, scenery)) tileSynced++;

            var link = e.Get<LinkedSceneryLink>();
            if (link is not null) SyncLinkedScenery(e, link);

            var wall = e.Get<LoadWallMarker>();
            if (wall is not null) SyncLoadWall(e, wall);

            var zoneBox = e.Get<LoadZoneBoxMarker>();
            if (zoneBox is not null) SyncLoadZoneBox(e, zoneBox);

            var mr = e.Get<MeshRenderer>();
            if (mr?.Material is not null) SyncMaterial(mr.Material);

            foreach (var child in e.Children) Visit(child);
        }
        Visit(chunkRoot);



        return (instSynced, tileSynced);
    }

    public static SaveResult SaveChunk(Entity chunkRoot, string outDir)
    {
        var source = chunkRoot.Get<ChunkSource>()
            ?? throw new InvalidOperationException($"'{chunkRoot.Name}' has no ChunkSource — was it built by ChunkImporter.LoadChunk?");

        var (instSynced, tileSynced) = SyncEntityGraph(chunkRoot);

        var rm2Out = Path.Combine(outDir, source.Rm2Path);
        Directory.CreateDirectory(Path.GetDirectoryName(rm2Out)!);
        WriteItem(source.Rm2, rm2Out);

        string? sm2Out = null;
        if (source.Sm2 is not null && source.Sm2Path is not null)
        {
            sm2Out = Path.Combine(outDir, source.Sm2Path);
            Directory.CreateDirectory(Path.GetDirectoryName(sm2Out)!);
            WriteItem(source.Sm2, sm2Out);
        }

        string? globalOut = null;
        if (source.GlobalRm2Dirty && source.GlobalRm2 is not null)
        {
            globalOut = Path.Combine(outDir, GlobalRm2RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(globalOut)!);
            WriteItem(source.GlobalRm2, globalOut);
            source.GlobalRm2Dirty = false;
        }

        return new SaveResult(rm2Out, sm2Out, instSynced, tileSynced, globalOut);
    }

    private static void WriteItem(Twinsanity.TwinsanityInterchange.Interfaces.ITwinSerializable item, string path)
    {
        var tempPath = path + ".tmp";
        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
            item.Write(bw);
        File.Move(tempPath, path, overwrite: true);
    }

    public static bool SyncInstance(Entity e, InstanceData inst)
    {
        if (e.Transform.LocalMatrix is not { } lm) return false;
        inst.Source.Position.X = lm.M41;
        inst.Source.Position.Y = lm.M42;
        inst.Source.Position.Z = lm.M43;

        float ry = MathF.Asin(Math.Clamp(-lm.M13, -1f, 1f));
        float rx = MathF.Atan2(lm.M23, lm.M33);
        float rz = MathF.Atan2(lm.M12, lm.M11);
        inst.Source.RotationX.SetRotation(rx * 180f / MathF.PI);
        inst.Source.RotationY.SetRotation(ry * 180f / MathF.PI);
        inst.Source.RotationZ.SetRotation(rz * 180f / MathF.PI);
        return true;
    }

    private static bool SyncSceneryTile(Entity e, SceneryTile tile, PS2AnyScenery? scenery)
    {
        if (e.Transform.LocalMatrix is not { } lm) return false;
        var oldPos = new Vector3(tile.Source.Column4.X, tile.Source.Column4.Y, tile.Source.Column4.Z);
        var newPos = new Vector3(lm.M41, lm.M42, lm.M43);
        tile.Source.Column1 = new TwinVec4(lm.M11, lm.M12, lm.M13, lm.M14);
        tile.Source.Column2 = new TwinVec4(lm.M21, lm.M22, lm.M23, lm.M24);
        tile.Source.Column3 = new TwinVec4(lm.M31, lm.M32, lm.M33, lm.M34);
        tile.Source.Column4 = new TwinVec4(lm.M41, lm.M42, lm.M43, lm.M44);

        var delta = newPos - oldPos;
        if (delta != Vector3.Zero)
        {
            var node = tile.Node;
            int idx = tile.IsLod ? node.LodModelMatrices.IndexOf(tile.Source) : node.MeshModelMatrices.IndexOf(tile.Source);
            int bboxIdx = tile.IsLod ? node.MeshIDs.Count + idx : idx;
            if (idx >= 0 && bboxIdx >= 0 && bboxIdx < node.BoundingBoxes.Count)
            {
                var bb = node.BoundingBoxes[bboxIdx];
                var localMin = new Vector3(bb[0].X, bb[0].Y, bb[0].Z);
                var localMax = new Vector3(bb[1].X, bb[1].Y, bb[1].Z);

                if (scenery is null)
                {
                    var a = newPos + localMin;
                    var b = newPos + localMax;
                    MeshDecoder.ApplySceneryBounds(node, Vector3.Min(a, b), Vector3.Max(a, b));
                }
                else
                {
                    uint meshOrLodId;
                    var leaf = new TwinSceneryLeaf();
                    if (tile.IsLod)
                    {
                        meshOrLodId = node.LodIDs[idx];
                        node.LodIDs.RemoveAt(idx);
                        node.LodModelMatrices.RemoveAt(idx);
                        leaf.LodIDs.Add(meshOrLodId);
                        leaf.LodModelMatrices.Add(tile.Source);
                    }
                    else
                    {
                        meshOrLodId = node.MeshIDs[idx];
                        node.MeshIDs.RemoveAt(idx);
                        node.MeshModelMatrices.RemoveAt(idx);
                        leaf.MeshIDs.Add(meshOrLodId);
                        leaf.MeshModelMatrices.Add(tile.Source);
                    }
                    node.BoundingBoxes.RemoveAt(bboxIdx);
                    leaf.BoundingBoxes.Add(bb);
                    Array.Copy(node.LightsEnabler, leaf.LightsEnabler, node.LightsEnabler.Length);

                    var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                    bool any = false;
                    for (int j = 0; j < node.MeshIDs.Count; j++)
                    {
                        var pos = new Vector3(node.MeshModelMatrices[j].Column4.X, node.MeshModelMatrices[j].Column4.Y, node.MeshModelMatrices[j].Column4.Z);
                        var box = node.BoundingBoxes[j];
                        var ra = pos + new Vector3(box[0].X, box[0].Y, box[0].Z);
                        var rb = pos + new Vector3(box[1].X, box[1].Y, box[1].Z);
                        min = Vector3.Min(min, Vector3.Min(ra, rb));
                        max = Vector3.Max(max, Vector3.Max(ra, rb));
                        any = true;
                    }
                    for (int j = 0; j < node.LodIDs.Count; j++)
                    {
                        var pos = new Vector3(node.LodModelMatrices[j].Column4.X, node.LodModelMatrices[j].Column4.Y, node.LodModelMatrices[j].Column4.Z);
                        var box = node.BoundingBoxes[node.MeshIDs.Count + j];
                        var ra = pos + new Vector3(box[0].X, box[0].Y, box[0].Z);
                        var rb = pos + new Vector3(box[1].X, box[1].Y, box[1].Z);
                        min = Vector3.Min(min, Vector3.Min(ra, rb));
                        max = Vector3.Max(max, Vector3.Max(ra, rb));
                        any = true;
                    }
                    if (any) MeshDecoder.ApplySceneryBounds(node, min, max);

                    MeshDecoder.GraftIndependentSceneryLeaf(scenery, leaf, newPos + localMin, newPos + localMax);
                    tile.Node = leaf;
                }
            }
        }
        return true;
    }

    private static void SyncMaterial(CrashEngine.Renderer.Material mat)
    {
        if (mat.SourceShader is not TwinShader shader) return;
        shader.ABlending = mat.AlphaBlend ? TwinShader.AlphaBlending.ON : TwinShader.AlphaBlending.OFF;
        if (shader.ShaderType is TwinShader.Type.StandardLit or TwinShader.Type.StandardUnlit)
            shader.ShaderType = mat.Unlit ? TwinShader.Type.StandardUnlit : TwinShader.Type.StandardLit;

        if (mat.Unlit && mat.UnlitToggledByUser &&
            mat.SourceSubModel is Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel sub &&
            sub.Colors is not null)
        {
            var neutral = new TwinVec4(0.498f, 0.498f, 0.498f, 1f);
            for (int i = 0; i < sub.Colors.Count; i++) sub.Colors[i] = neutral;
            sub.Compile();
        }
    }

    private static bool SyncLinkedScenery(Entity e, LinkedSceneryLink link)
    {
        if (e.Transform.LocalMatrix is not { } lm) return false;
        link.Source.ChunkMatrix.Column1 = new TwinVec4(lm.M11, lm.M12, lm.M13, lm.M14);
        link.Source.ChunkMatrix.Column2 = new TwinVec4(lm.M21, lm.M22, lm.M23, lm.M24);
        link.Source.ChunkMatrix.Column3 = new TwinVec4(lm.M31, lm.M32, lm.M33, lm.M34);
        link.Source.ChunkMatrix.Column4 = new TwinVec4(lm.M41, lm.M42, lm.M43, lm.M44);
        if (Matrix4x4.Invert(lm, out var inv))
        {
            link.Source.ObjectMatrix.Column1 = new TwinVec4(inv.M11, inv.M12, inv.M13, inv.M14);
            link.Source.ObjectMatrix.Column2 = new TwinVec4(inv.M21, inv.M22, inv.M23, inv.M24);
            link.Source.ObjectMatrix.Column3 = new TwinVec4(inv.M31, inv.M32, inv.M33, inv.M34);
            link.Source.ObjectMatrix.Column4 = new TwinVec4(inv.M41, inv.M42, inv.M43, inv.M44);
        }
        return true;
    }

    private static bool SyncLoadWall(Entity e, LoadWallMarker wall)
    {
        if (e.Transform.LocalMatrix is not { } lm) return false;
        if (wall.Source.LoadingWall is not { } loadWall) return false;

        Vector3 Corner(float x, float y) => Vector3.Transform(new Vector3(x, y, 0f), lm);
        var p1 = Corner(-1f, -1f);
        var p2 = Corner( 1f, -1f);
        var p3 = Corner( 1f,  1f);
        var p4 = Corner(-1f,  1f);
        loadWall.Column1 = new TwinVec4(p1.X, p1.Y, p1.Z, 1f);
        loadWall.Column2 = new TwinVec4(p2.X, p2.Y, p2.Z, 1f);
        loadWall.Column3 = new TwinVec4(p3.X, p3.Y, p3.Z, 1f);
        loadWall.Column4 = new TwinVec4(p4.X, p4.Y, p4.Z, 1f);
        return true;
    }

    private static bool SyncLoadZoneBox(Entity e, LoadZoneBoxMarker marker)
    {
        if (e.Transform.LocalMatrix is not { } lm) return false;
        RecomputeBoundingBox(marker.Box.BondingBoxBuilder, lm);
        return true;
    }

    public static void RecomputeBoundingBox(Twinsanity.TwinsanityInterchange.Common.TwinBoundingBoxBuilder box, Matrix4x4 lm)
    {
        var rightVec = new Vector3(lm.M11, lm.M12, lm.M13);
        var upVec    = new Vector3(lm.M21, lm.M22, lm.M23);
        var fwdVec   = new Vector3(lm.M31, lm.M32, lm.M33);
        var center   = new Vector3(lm.M41, lm.M42, lm.M43);

        float hx = rightVec.Length(), hy = upVec.Length(), hz = fwdVec.Length();
        var right = hx > 1e-6f ? rightVec / hx : Vector3.UnitX;
        var up    = hy > 1e-6f ? upVec    / hy : Vector3.UnitY;
        var fwd   = hz > 1e-6f ? fwdVec   / hz : Vector3.UnitZ;

        Vector3 P(int sx, int sy, int sz) => center + sx * hx * right + sy * hy * up + sz * hz * fwd;
        var p0 = P(-1, -1, -1); var p1 = P(1, -1, -1); var p2 = P(-1, 1, -1); var p3 = P(1, 1, -1);
        var p4 = P(-1, 1, 1);   var p5 = P(1, 1, 1);   var p6 = P(-1, -1, 1); var p7 = P(1, -1, 1);

        box.BoundingBoxPoints.Clear();
        foreach (var p in new[] { p0, p1, p2, p3, p4, p5, p6, p7 })
            box.BoundingBoxPoints.Add(new TwinVec4(p.X, p.Y, p.Z, 1f));

        TwinVec4 Plane(Vector3 n, Vector3 onFace) => new(n.X, n.Y, n.Z, -Vector3.Dot(n, onFace));
        box.UnkVectors1.Clear();
        box.UnkVectors1.Add(Plane(-fwd, p0));
        box.UnkVectors1.Add(Plane( up,  p2));
        box.UnkVectors1.Add(Plane( fwd, p4));
        box.UnkVectors1.Add(Plane(-up,  p0));
        box.UnkVectors1.Add(Plane( right, p1));
        box.UnkVectors1.Add(Plane(-right, p0));

        box.UnkVectors2.Clear();
        box.UnkVectors2.Add(new TwinVec4(-right.X, -right.Y, -right.Z, 1f));
        box.UnkVectors2.Add(new TwinVec4(-up.X,    -up.Y,    -up.Z,    1f));
        box.UnkVectors2.Add(new TwinVec4(-fwd.X,    -fwd.Y,   -fwd.Z,  1f));

        box.UnkVectors3.Clear();
        box.UnkVectors3.Add(new TwinVec4(-fwd.X,  -fwd.Y,  -fwd.Z,  1f));
        box.UnkVectors3.Add(new TwinVec4( up.X,     up.Y,    up.Z,   1f));
        box.UnkVectors3.Add(new TwinVec4( right.X,  right.Y, right.Z,1f));

        box.UnkShorts.Clear();
        box.UnkShorts.AddRange(new ushort[] { 256, 769, 515, 2, 1283, 1029, 516, 1797, 1543, 1030, 263, 1536 });
        box.UnkBytes1.Clear();
        box.UnkBytes1.AddRange(new byte[] { 0, 5, 10, 15, 20, 25 });
        box.UnkBytes2.Clear();
        box.UnkBytes2.AddRange(new byte[] { 4,2,3,1,0,4, 4,5,3,2,4,6, 7,5,4,4,0,1, 7,6,4,3,5,7, 1,4,4,2,0,6 });
    }
}
