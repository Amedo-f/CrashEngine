using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{

    private bool SaveChunk(bool confirm = true) // Amedo 2026-10-04
    {
        StopSimulation(); // Amedo 2026-09-26
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null) { _browser.Log("Save: no loaded chunk has a ChunkSource (unexpected)."); return false; }

        try
        {
            SyncCollisionBoxes(chunkRoot);
            SyncCollisionCylinders(chunkRoot);
            SyncCollisionPlanes(chunkRoot);
            SyncGeneratedMeshCollision(chunkRoot);
            SyncCollisionMeshTransform(chunkRoot);
            SyncTriggerMarkers(chunkRoot);
            SyncCameraMarkers(chunkRoot);
            SyncPositionMarkers(chunkRoot);
            SyncAiPositionMarkers(chunkRoot);
            SyncSceneryLightMarkers(chunkRoot); // Amedo 2026-09-19
            SyncInstanceScale(chunkRoot);
            SyncWorldLighting(chunkRoot);
            SyncParticleEmitters(chunkRoot);
            SaveSoundGainBaselines(chunkRoot);
            if (!SyncBehaviourCode()) { _browser.Log("Save stopped: the behaviour code (Code\\Behaviours) has errors, listed above."); return false; } // Amedo 2026-11-18
            var outDir = SavedChunksDir;
            var result = ChunkExporter.SaveChunk(chunkRoot, outDir);
            SaveCollisionMarkers(chunkRoot);
            var msg = $"Saved {result.InstancesSynced} instance(s), {result.SceneryTilesSynced} scenery tile(s) → {result.Rm2Path}" +
                      (result.Sm2Path is not null ? $" + {Path.GetFileName(result.Sm2Path)}" : "") +
                      (result.GlobalRm2Path is not null ? $" + Startup\\Default.rm2 (shared objects, texture edit)" : "");
            _browser.Log(msg);
            _saveConfirmMessage = msg;
            _openSaveConfirmPopup = confirm; // Amedo 2026-10-04
            _sceneFiles = null; // Amedo 2026-11-12
            return true;
        }
        catch (Exception ex) { _browser.Log($"Save failed: {ex.Message}"); return false; }
    }

    private void ApplyChunkTransform()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2 = chunkRoot?.Get<ChunkSource>()?.Rm2;
        if (chunkRoot is null || rm2 is null) { _browser.Log("Apply Chunk Transform: no loaded chunk."); return; }

        var rootPos = chunkRoot.Transform.Position;
        var rootRot = chunkRoot.Transform.Rotation;
        bool hasOffset = rootPos.LengthSquared() > 1e-8f || Quaternion.Dot(rootRot, Quaternion.Identity) < 0.999999f;
        if (!hasOffset) { _browser.Log("Apply Chunk Transform: this chunk has no offset to apply."); return; }

        var delta = Matrix4x4.CreateFromQuaternion(rootRot) * Matrix4x4.CreateTranslation(rootPos);
        static Vector3 Mirror(Vector3 v) => new(-v.X, v.Y, v.Z);
        Vector3 BakePoint(Vector3 rawOld) => Mirror(Vector3.Transform(Mirror(rawOld), delta));
        TwinVec4 BakeVec(TwinVec4 v)
        {
            var p = BakePoint(new Vector3(v.X, v.Y, v.Z));
            v.X = p.X; v.Y = p.Y; v.Z = p.Z;
            return v;
        }

        int instCount = 0, tileCount = 0, linkCount = 0, wallCount = 0;
        void BakeEntity(Entity ent)
        {
            bool baked = false;
            if (ent.Has<InstanceData>())        { instCount++; baked = true; }
            else if (ent.Has<SceneryTile>())    { tileCount++; baked = true; }
            else if (ent.Has<LinkedSceneryLink>()) { linkCount++; baked = true; }
            else if (ent.Has<LoadWallMarker>()) { wallCount++; baked = true; }
            if (baked) ent.Transform.LocalMatrix = ent.Transform.Local * delta;
            foreach (var c in ent.Children) BakeEntity(c);
        }
        foreach (var c in chunkRoot.Children) BakeEntity(c);

        int vecCount = 0, collTrigCount = 0;
        var coll = rm2.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is not null)
        {
            for (int i = 0; i < coll.Vectors.Count; i++)
            {
                var v = coll.Vectors[i];
                var p = BakePoint(new Vector3(v.X, v.Y, v.Z));
                v.X = p.X; v.Y = p.Y; v.Z = p.Z;
                coll.Vectors[i] = v; // Amedo
            }
            vecCount = coll.Vectors.Count;
            foreach (var t in coll.Triggers)
            {
                var p1 = BakePoint(new Vector3(t.V1.X, t.V1.Y, t.V1.Z));
                var p2 = BakePoint(new Vector3(t.V2.X, t.V2.Y, t.V2.Z));
                t.V1.X = p1.X; t.V1.Y = p1.Y; t.V1.Z = p1.Z;
                t.V2.X = p2.X; t.V2.Y = p2.Y; t.V2.Z = p2.Z;
                collTrigCount++;
            }
        }

        int layoutTrigCount = 0, camCount = 0, posCount = 0, aiCount = 0;
        foreach (var layout in Layouts(rm2))
        {
            foreach (var t in LayoutSectionItems<PS2AnyTrigger>(layout, TwinConstants.LAYOUT_TRIGGERS_SECTION))
            {
                t.Trigger.Position = BakeVec(t.Trigger.Position);
                layoutTrigCount++;
            }

            foreach (var c in LayoutSectionItems<PS2AnyCamera>(layout, TwinConstants.LAYOUT_CAMERAS_SECTION))
            {
                c.CamTrigger.Position = BakeVec(c.CamTrigger.Position);
                camCount++;
            }

            foreach (var p0 in LayoutSectionItems<PS2AnyPosition>(layout, TwinConstants.LAYOUT_POSITIONS_SECTION))
            {
                p0.Position = BakeVec(p0.Position);
                posCount++;
            }

            foreach (var ap in LayoutSectionItems<PS2AnyAIPosition>(layout, TwinConstants.LAYOUT_AI_POSITIONS_SECTION))
            {
                ap.Position = BakeVec(ap.Position);
                aiCount++;
            }
        }

        chunkRoot.Transform.Position = Vector3.Zero;
        chunkRoot.Transform.Rotation = Quaternion.Identity;

        _browser.Log($"Apply Chunk Transform: baked into {instCount} instance(s), {tileCount} scenery tile(s), " +
                      $"{linkCount} linked-scenery link(s), {wallCount} load wall(s), {vecCount} collision " +
                      $"vector(s), {collTrigCount} collision trigger(s), {layoutTrigCount} layout trigger(s), " +
                      $"{camCount} camera(s), {posCount} position(s), {aiCount} AI position(s) — saving + reloading...");

        SaveChunk();
        ReloadLevel();
    }

    private string? _saveConfirmMessage;
    private bool    _openSaveConfirmPopup;

    private void DrawSaveConfirmPopup()
    {
        if (_openSaveConfirmPopup)
        {
            ImGui.OpenPopup("Save Confirmation");
            _openSaveConfirmPopup = false;
        }

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        if (ImGui.BeginPopupModal("Save Confirmation", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped(_saveConfirmMessage ?? "");
            ImGui.Spacing();
            if (ImGui.Button("OK", new Vector2(120f, 0f))) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private void SyncInstanceScale(Entity chunkRoot)
    {
        var chunkSource = chunkRoot.Get<ChunkSource>();
        if (chunkSource?.MeshTables is null) return;
        var gl = Engine.Instance.GL;

        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<InstanceData>()))
        {
            var data = e.Get<InstanceData>()!;
            var lm   = e.Transform.LocalMatrix;
            var scale = e.Transform.Scale;
            var rot   = e.Transform.Rotation;
            var pos   = e.Transform.Position;
            if (lm is not null)
            {
                if (!Matrix4x4.Decompose(lm.Value, out var s2, out var r2, out var p2)) continue;
                scale = s2; rot = r2; pos = p2;
            }
            if (Vector3.Distance(scale, Vector3.One) < 0.0001f) continue;

            var privateObjId = MeshDecoder.PrivatizeInstanceGeometryForScale(
                gl, chunkSource.Rm2, chunkSource.MeshTables, chunkSource.TexCache, data.ObjectId, out var privLog);
            if (privateObjId is null)
            {
                _browser.Log($"Scale bake for {e.Name}: {privLog}");
                continue;
            }

            var touched = MeshDecoder.BakeInstanceScale(gl, chunkSource.MeshTables, privateObjId.Value, scale);
            if (touched.Count == 0) continue;

            data.ObjectId = (ushort)privateObjId.Value;
            data.Source.ObjectId = (ushort)privateObjId.Value;
            MeshDecoder.BuildMeshForInstance(gl, chunkSource.MeshTables, e);

            _browser.Log($"Baked scale ({scale.X:F2}, {scale.Y:F2}, {scale.Z:F2}) into {e.Name}'s own private mesh " +
                         $"copy (ObjectId 0x{privateObjId.Value:X4}, RigidModel id(s): " +
                         $"{string.Join(", ", touched.Select(id => $"0x{id:X4}"))}) — original object and any other " +
                         "instance/copy sharing the old ObjectId are untouched.");

            e.Transform.Scale = Vector3.One;
            e.Transform.LocalMatrix = Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos);
        }
    }

    private void SyncParticleEmitters(Entity chunkRoot)
    {
        int synced = 0;
        foreach (var marker in AllEntities(chunkRoot).Select(e => e.Get<CrashEngine.Importer.ParticleEmitterMarker>()).Where(m => m is not null))
        {
            var pos = marker!.Transform.Position;
            marker.Source.Position.X = pos.X;
            marker.Source.Position.Y = pos.Y;
            marker.Source.Position.Z = pos.Z;
            synced++;
        }
        if (synced > 0)
            _browser.Log($"Synced {synced} particle emitter position(s).");
    }
}
