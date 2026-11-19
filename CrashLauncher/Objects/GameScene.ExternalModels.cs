using CrashEngine.Core;
using CrashEngine.Importer;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void BakeExternalModel(string modelPath, bool flipX = true)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Model: no level currently loaded."); return; }
        if (CrashEngine.Assets.CharacterImporter.HasSkeleton(modelPath) && ImportCharacter(modelPath, flipX)) return; // Amedo 2026-10-05

        var modern = ReadModernCharacter(modelPath); // Amedo 2026-11-18
        var raw = CrashEngine.Assets.ModelImporter.ExtractRawSubmeshes(modelPath);
        if (raw is null || raw.Count == 0) { _browser.Log($"Add Model: couldn't read any meshes from {Path.GetFileName(modelPath)}. {CrashEngine.Assets.ModelImporter.LastReport}"); return; } // Amedo

        if (flipX)
            foreach (var sm in raw)
            {
                for (int i = 0; i < sm.Positions.Length; i++)
                    sm.Positions[i] = new System.Numerics.Vector3(-sm.Positions[i].X, sm.Positions[i].Y, sm.Positions[i].Z);
                for (int i = 0; i < sm.Normals.Length; i++)
                    sm.Normals[i] = new System.Numerics.Vector3(-sm.Normals[i].X, sm.Normals[i].Y, sm.Normals[i].Z);
                for (int i = 0; i + 2 < sm.Indices.Length; i += 3)
                    (sm.Indices[i + 1], sm.Indices[i + 2]) = (sm.Indices[i + 2], sm.Indices[i + 1]);
            }

        try
        {
            var rec = MeshDecoder.BakeExternalModelAsObject(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables!,
                chunkSource.TexCache, chunkSource.GlobalRm2, raw, Path.GetFileNameWithoutExtension(modelPath), out string log);
            if (rec is null) { _browser.Log($"Add Model: {log}"); return; }

            // Amedo 2026-09-20
            chunkSource.Transplants[rec.ObjectId] = MeshDecoder.ToTransplantRecord(rec);

            var section = FindInstanceSection(chunkRoot, chunkSource);
            if (section is null) { _browser.Log("Add Model: this level has no instances section to add to."); return; }

            uint templateStateFlags = NewInstanceStateFlags;

            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            float extent = 0f; // Amedo 2026-10-02
            foreach (var sm in raw)
                foreach (var p in sm.Positions) extent = MathF.Max(extent, MathF.Max(MathF.Abs(p.X), MathF.Max(MathF.Abs(p.Y), MathF.Abs(p.Z))));
            if (_camera is not null) spawnPos += _camera.Forward * MathF.Max(3f, extent * 2.5f);
            var inst = NewInstance(new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f), rec.ObjectId, null, templateStateFlags);

            var instRoot = InstancesRoot(chunkRoot);
            var newEntity = AddOrReuseInstance(section, inst, instRoot, chunkSource);
            if (modern is not null) AddModernLook(newEntity, modelPath, modern); // Amedo 2026-11-18

            SelectClicked(newEntity, false);
            if (CrashEngine.Assets.ModelImporter.LastReport is { Length: > 0 } importNotes) _browser.Log($"Add Model: {importNotes}"); // Amedo 2026-10-02
            _browser.Log($"Add Model: {log} Placed {MathF.Max(3f, extent * 2.5f):0.#} m in front of the camera " + // Amedo 2026-10-02
                          $"(StateFlags 0x{templateStateFlags:X}, fixed Visible+ShadowActive — independent of this level's own content). " +
                          "Not on the Undo stack yet, but Delete Selected removes it normally. Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Model failed: {ex.Message}"); }
    }

    private void BakeExternalModelAsScenery(string modelPath, bool flipX = true)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var sm2         = chunkSource?.Sm2;
        var sceneryRoot = chunkRoot is null ? null : AllEntities(chunkRoot).FirstOrDefault(x => x.Name == "Scenery");
        if (chunkRoot is null || sm2 is null || sceneryRoot is null)
        { _browser.Log("Add Scenery: this chunk has no Scenery (SM2) to add to."); return; }

        var scenery = sm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>(
            (uint)TwinConstants.SCENERY_SECENERY_ITEM);
        if (scenery is null) { _browser.Log("Add Scenery: this chunk's SM2 has no Scenery item."); return; }

        var raw = CrashEngine.Assets.ModelImporter.ExtractRawSubmeshes(modelPath);
        if (raw is null || raw.Count == 0) { _browser.Log($"Add Scenery: couldn't read any meshes from {Path.GetFileName(modelPath)}. {CrashEngine.Assets.ModelImporter.LastReport}"); return; } // Amedo

        if (flipX)
            foreach (var sm in raw)
            {
                for (int i = 0; i < sm.Positions.Length; i++)
                    sm.Positions[i] = new Vector3(-sm.Positions[i].X, sm.Positions[i].Y, sm.Positions[i].Z);
                for (int i = 0; i < sm.Normals.Length; i++)
                    sm.Normals[i] = new Vector3(-sm.Normals[i].X, sm.Normals[i].Y, sm.Normals[i].Z);
                for (int i = 0; i + 2 < sm.Indices.Length; i += 3)
                    (sm.Indices[i + 1], sm.Indices[i + 2]) = (sm.Indices[i + 2], sm.Indices[i + 1]);
            }

        try
        {
            var rec = MeshDecoder.BakeExternalModelAsScenery(Engine.Instance.GL, sm2, chunkSource!.SceneryTexCache,
                chunkSource.GlobalRm2, raw, Path.GetFileNameWithoutExtension(modelPath), out string log);
            if (rec is null) { _browser.Log($"Add Scenery: {log}"); return; }

            var leaf = new Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryLeaf();
            for (int i = 0; i < rec.MeshIds.Count; i++)
            {
                leaf.MeshIDs.Add(rec.MeshIds[i]);
                leaf.MeshModelMatrices.Add(MeshDecoder.IdentityTwinMat4());
                leaf.BoundingBoxes.Add(rec.LocalBoxes[i]);
            }
            var lightTemplate = scenery.Sceneries.FirstOrDefault();
            if (lightTemplate is not null)
                Array.Copy(lightTemplate.LightsEnabler, leaf.LightsEnabler, leaf.LightsEnabler.Length);
            else
                for (int i = 0; i < leaf.LightsEnabler.Length; i++) leaf.LightsEnabler[i] = true;

            MeshDecoder.GraftIndependentSceneryLeaf(scenery, leaf, rec.CombinedMin, rec.CombinedMax);

            // Amedo 2026-09-20
            foreach (var mid in rec.MeshIds) chunkSource.SceneryBakes[mid] = rec;

            RebuildSceneryKeepingMoves(sceneryRoot, chunkSource, sm2);

            var newIds = new HashSet<uint>(rec.MeshIds);
            var justAdded = sceneryRoot.Children.FirstOrDefault(c =>
                c.Get<SceneryTile>() is { } t && newIds.Contains(t.SourceId));
            if (justAdded is not null) SelectClicked(justAdded, false);

            if (CrashEngine.Assets.ModelImporter.LastReport is { Length: > 0 } importNotes) _browser.Log($"Add Scenery: {importNotes}"); // Amedo 2026-10-02
            _browser.Log($"Add Scenery: {log} Placed at the world origin (0,0,0) as an independent scenery leaf. " +
                          "Not on the Undo stack, but Delete Selected removes it normally. Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Scenery failed: {ex.Message}"); }
    }

    private void AddRealCube()
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Cube: no level currently loaded."); return; }

        (Vector3 Normal, Vector3 A, Vector3 B, Vector3 C, Vector3 D)[] faces =
        {
            (new Vector3( 1, 0, 0), new Vector3(0.5f,-0.5f,-0.5f), new Vector3(0.5f, 0.5f,-0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f,-0.5f, 0.5f)),
            (new Vector3(-1, 0, 0), new Vector3(-0.5f,-0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f,-0.5f), new Vector3(-0.5f,-0.5f,-0.5f)),
            (new Vector3( 0, 1, 0), new Vector3(-0.5f, 0.5f,-0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3( 0.5f, 0.5f, 0.5f), new Vector3( 0.5f, 0.5f,-0.5f)),
            (new Vector3( 0,-1, 0), new Vector3(-0.5f,-0.5f, 0.5f), new Vector3(-0.5f,-0.5f,-0.5f), new Vector3( 0.5f,-0.5f,-0.5f), new Vector3( 0.5f,-0.5f, 0.5f)),
            (new Vector3( 0, 0, 1), new Vector3(-0.5f,-0.5f, 0.5f), new Vector3( 0.5f,-0.5f, 0.5f), new Vector3( 0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)),
            (new Vector3( 0, 0,-1), new Vector3( 0.5f,-0.5f,-0.5f), new Vector3(-0.5f,-0.5f,-0.5f), new Vector3(-0.5f, 0.5f,-0.5f), new Vector3( 0.5f, 0.5f,-0.5f)),
        };

        var positions = new List<Vector3>();
        var normals   = new List<Vector3>();
        var uvs       = new List<Vector2>();
        var indices   = new List<uint>();
        foreach (var (n, a, b, c, d) in faces)
        {
            uint baseIdx = (uint)positions.Count;
            positions.Add(a); positions.Add(b); positions.Add(c); positions.Add(d);
            normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            indices.Add(baseIdx); indices.Add(baseIdx + 1); indices.Add(baseIdx + 2);
            indices.Add(baseIdx); indices.Add(baseIdx + 2); indices.Add(baseIdx + 3);
        }

        const int texSize = 32;
        var pixels = new byte[texSize * texSize * 4];
        for (int p = 0; p < texSize * texSize; p++)
        {
            pixels[p * 4 + 0] = 127; pixels[p * 4 + 1] = 127; pixels[p * 4 + 2] = 127; pixels[p * 4 + 3] = 255;
        }

        var raw = new List<CrashEngine.Assets.RawSubmesh>
        {
            new CrashEngine.Assets.RawSubmesh
            {
                Positions = positions.ToArray(), Normals = normals.ToArray(), UVs = uvs.ToArray(),
                Indices   = indices.ToArray(),
                DiffusePixelsRGBA = pixels, TexWidth = texSize, TexHeight = texSize,
                Name = "Cube",
                CeAlphaBlend = false,
                CeUnlit = false,
            },
        };

        try
        {
            var rec = MeshDecoder.BakeExternalModelAsObject(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables!,
                chunkSource.TexCache, chunkSource.GlobalRm2, raw, "Cube", out string log);
            if (rec is null) { _browser.Log($"Add Cube: {log}"); return; }

            // Amedo 2026-09-20
            chunkSource.Transplants[rec.ObjectId] = MeshDecoder.ToTransplantRecord(rec);

            var section = FindInstanceSection(chunkRoot, chunkSource);
            if (section is null) { _browser.Log("Add Cube: this level has no instances section to add to."); return; }

            uint templateStateFlags = NewInstanceStateFlags;

            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            var inst = NewInstance(new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f), rec.ObjectId, null, templateStateFlags);

            var instRoot = InstancesRoot(chunkRoot);
            var newEntity = AddOrReuseInstance(section, inst, instRoot, chunkSource);

            SelectClicked(newEntity, false);
            _browser.Log($"Add Cube: {log} Placed at the camera position " +
                          $"(StateFlags 0x{templateStateFlags:X}, fixed Visible+ShadowActive). " +
                          "Not on the Undo stack yet, but Delete Selected removes it normally. Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Cube failed: {ex.Message}"); }
    }
}
