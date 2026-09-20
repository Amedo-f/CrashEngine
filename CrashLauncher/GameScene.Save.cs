using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinAmbientLight = Twinsanity.TwinsanityInterchange.Common.Lights.AmbientLight;
using TwinDirectionalLight = Twinsanity.TwinsanityInterchange.Common.Lights.DirectionalLight;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{

    private void SaveChunk()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null) { _browser.Log("Save: no loaded chunk has a ChunkSource (unexpected)."); return; }

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
            var outDir = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "SavedChunks");
            var result = ChunkExporter.SaveChunk(chunkRoot, outDir);
            SaveCollisionMarkers(chunkRoot);
            var msg = $"Saved {result.InstancesSynced} instance(s), {result.SceneryTilesSynced} scenery tile(s) → {result.Rm2Path}" +
                      (result.Sm2Path is not null ? $" + {Path.GetFileName(result.Sm2Path)}" : "") +
                      (result.GlobalRm2Path is not null ? $" + Startup\\Default.rm2 (shared objects, texture edit)" : "");
            _browser.Log(msg);
            _saveConfirmMessage = msg;
            _openSaveConfirmPopup = true;
        }
        catch (Exception ex) { _browser.Log($"Save failed: {ex.Message}"); }
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
        for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
            if (layout is null) continue;

            var trigSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
            if (trigSec is not null)
                for (int i = 0; i < trigSec.GetItemsAmount(); i++)
                    if (trigSec.GetItem(i) is PS2AnyTrigger t)
                    {
                        var p = BakePoint(new Vector3(t.Trigger.Position.X, t.Trigger.Position.Y, t.Trigger.Position.Z));
                        t.Trigger.Position.X = p.X; t.Trigger.Position.Y = p.Y; t.Trigger.Position.Z = p.Z;
                        layoutTrigCount++;
                    }

            var camSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_CAMERAS_SECTION);
            if (camSec is not null)
                for (int i = 0; i < camSec.GetItemsAmount(); i++)
                    if (camSec.GetItem(i) is PS2AnyCamera c)
                    {
                        var p = BakePoint(new Vector3(c.CamTrigger.Position.X, c.CamTrigger.Position.Y, c.CamTrigger.Position.Z));
                        c.CamTrigger.Position.X = p.X; c.CamTrigger.Position.Y = p.Y; c.CamTrigger.Position.Z = p.Z;
                        camCount++;
                    }

            var posSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_POSITIONS_SECTION);
            if (posSec is not null)
                for (int i = 0; i < posSec.GetItemsAmount(); i++)
                    if (posSec.GetItem(i) is PS2AnyPosition p0)
                    {
                        var p = BakePoint(new Vector3(p0.Position.X, p0.Position.Y, p0.Position.Z));
                        p0.Position.X = p.X; p0.Position.Y = p.Y; p0.Position.Z = p.Z;
                        posCount++;
                    }

            var aiSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);
            if (aiSec is not null)
                for (int i = 0; i < aiSec.GetItemsAmount(); i++)
                    if (aiSec.GetItem(i) is PS2AnyAIPosition ap)
                    {
                        var p = BakePoint(new Vector3(ap.Position.X, ap.Position.Y, ap.Position.Z));
                        ap.Position.X = p.X; ap.Position.Y = p.Y; ap.Position.Z = p.Z;
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
        ReloadLevel(fromDiscOnly: false);
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

    private bool _openResetAllConfirmPopup;

    private void ResetAllLevelsFromDisc()
    {
        // Amedo 2026-09-20
        var savedChunksDir = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "SavedChunks");
        if (Directory.Exists(savedChunksDir))
        {
            int fileCount = Directory.GetFiles(savedChunksDir, "*", SearchOption.AllDirectories).Length;
            try
            {
                Directory.Delete(savedChunksDir, recursive: true);
                _browser.Log($"Reset All Levels From Disc: deleted {fileCount} saved edit file(s) across the whole project.");
            }
            catch (Exception ex)
            {
                _browser.Log("Reset All Levels From Disc: FAILED to delete SavedChunks -- " + ex.Message);
                return;
            }
        }

        try
        {
            var buildDir  = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build");
            var backupDir = Path.Combine(buildDir, "original_backup_" + Path.GetFileName(_extractedRoot.TrimEnd('\\', '/')));
            var backupBd  = Path.Combine(backupDir, "Crash.BD");
            var backupBh  = Path.Combine(backupDir, "Crash.BH");
            var liveBd    = Path.Combine(_extractedRoot, "Crash6", "Crash.BD");
            var liveBh    = Path.Combine(_extractedRoot, "Crash6", "Crash.BH");
            if (File.Exists(backupBd) && File.Exists(backupBh) && File.Exists(liveBd))
            {
                File.Copy(backupBd, liveBd, overwrite: true);
                File.Copy(backupBh, liveBh, overwrite: true);
                _browser.Log("Reset All Levels From Disc: restored the disc archive (Crash.BD/BH) from the pristine " +
                              "original backup -- every level now reads truly original data (including changes Build ISO had baked in).");
            }
        }
        catch (Exception ex)
        {
            _browser.Log("Reset All Levels From Disc: WARNING -- could not restore disc archive from backup: " + ex.Message);
        }

        var openChunk = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (openChunk is not null)
            ReloadLevel(fromDiscOnly: true);
        else
            _browser.Log("Reset All Levels From Disc: done. Open any level to see the original data.");
    }

    private void DrawResetAllConfirmPopup()
    {
        if (_openResetAllConfirmPopup)
        {
            ImGui.OpenPopup("Reset All Levels From Disc?");
            _openResetAllConfirmPopup = false;
        }

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        if (ImGui.BeginPopupModal("Reset All Levels From Disc?", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped("This permanently deletes EVERY saved edit for EVERY level in this " +
                               "project (the whole SavedChunks folder) -- not just the level you have " +
                               "open right now. There is no backup and no Undo. Only the real, " +
                               "unmodified disc data will remain.");
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
            if (ImGui.Button("Delete All Saved Edits", new Vector2(200f, 0f)))
            {
                ResetAllLevelsFromDisc();
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120f, 0f))) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private bool _building;
    private float _buildProgress;
    private readonly object _buildLogLock = new();
    private readonly List<string> _buildLog = new();

    private string _isoOutputPath = "";
    // Amedo 2026-09-20
    private bool _isoPathLoaded;
    private string IsoOutputPathFile =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, ".iso_output_path.txt");

    private void EnsureIsoOutputPathLoaded()
    {
        if (_isoPathLoaded) return;
        _isoPathLoaded = true;
        try { if (File.Exists(IsoOutputPathFile)) _isoOutputPath = File.ReadAllText(IsoOutputPathFile).Trim(); }
        catch { }
    }

    private void SaveIsoOutputPath()
    {
        try { File.WriteAllText(IsoOutputPathFile, _isoOutputPath ?? ""); } catch { }
    }

    private void BuildIso()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null) { _browser.Log("Build ISO: no loaded chunk has a ChunkSource (unexpected)."); return; }
        if (_building) return;

        ChunkExporter.SyncEntityGraph(chunkRoot);
        SyncCollisionBoxes(chunkRoot);
        SyncCollisionCylinders(chunkRoot);
        SyncCollisionPlanes(chunkRoot);
        SyncGeneratedMeshCollision(chunkRoot);
        SyncTriggerMarkers(chunkRoot);
        SyncCameraMarkers(chunkRoot);
        SyncPositionMarkers(chunkRoot);
        SyncAiPositionMarkers(chunkRoot);
        SyncSceneryLightMarkers(chunkRoot); // Amedo 2026-09-19
        SyncInstanceScale(chunkRoot);
        SyncWorldLighting(chunkRoot);
        SaveSoundGainBaselines(chunkRoot);

        _building = true;
        _buildProgress = 0f;
        lock (_buildLogLock) _buildLog.Clear();

        void BLog(string msg)
        {
            lock (_buildLogLock)
            {
                _buildLog.Add(msg);
                if (_buildLog.Count > 300) _buildLog.RemoveAt(0);
            }
        }

        var workDir        = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build");
        var savedChunksDir = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "SavedChunks");
        var outPathOverride = string.IsNullOrWhiteSpace(_isoOutputPath) ? null : _isoOutputPath;
        BLog($"Build ISO: starting on disc '{_extractedRoot}' — merges every chunk saved this session, not just the one open now.");

        Func<string, bool>? excludePath = null;
        List<string>? keepLevelPaths = null;
        List<string>? excludedCutscenes = null;
        const string startingLevel = @"Levels\Earth\Hub\beach";
        var projectRoot = Path.GetDirectoryName(_scriptOut) ?? _extractedRoot;
        if (CrashProject.Open(projectRoot) is { IsNewGame: true } proj)
        {
            if (proj.ExcludedCutscenes.Count == 0)
            {
                var fmvRoot = Path.Combine(_extractedRoot, "FMV");
                if (Directory.Exists(fmvRoot))
                {
                    foreach (var f in Directory.EnumerateFiles(fmvRoot, "*.pss", SearchOption.AllDirectories))
                        if (BuildIsoToPS2.IsStoryCutscene(Path.GetFileName(f)))
                            proj.ExcludedCutscenes.Add(Path.GetRelativePath(fmvRoot, f));
                    if (proj.ExcludedCutscenes.Count > 0) proj.Save();
                }
            }
            excludedCutscenes = proj.ExcludedCutscenes;
            var claimed = new HashSet<string>(proj.ClaimedLevels.Select(p => p.Replace('/', '\\').TrimStart('\\')),
                                               StringComparer.OrdinalIgnoreCase);
            excludePath = normalizedPath =>
            {
                if (!normalizedPath.StartsWith(@"Levels\", StringComparison.OrdinalIgnoreCase)) return false;
                var ext = Path.GetExtension(normalizedPath);
                if (!ext.Equals(".rm2", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".sm2", StringComparison.OrdinalIgnoreCase)) return false;
                var basePath = normalizedPath[..^ext.Length];
                if (basePath.Equals(startingLevel, StringComparison.OrdinalIgnoreCase)) return false;
                return !claimed.Contains(basePath);
            };
            keepLevelPaths = new List<string>(claimed) { startingLevel };
            BLog($"Build ISO: New Game mode ON — excluding every original level NOT in ClaimedLevels ({proj.ClaimedLevels.Count} claimed).");
        }

        Task.Run(() =>
        {
            try
            {
                var isoPath = CrashLauncher.BuildIsoToPS2.Build(
                    chunkRoot, workDir, _extractedRoot, savedChunksDir,
                    BLog, p => _buildProgress = p, outPathOverride, excludePath, keepLevelPaths, excludedCutscenes);
                BLog($"Build ISO: done. Open in PCSX2: {isoPath}");
                _browser.Log($"Build ISO: done. Open in PCSX2: {isoPath}");
            }
            catch (Exception ex)
            {
                BLog($"Build ISO failed: {ex.Message}");
                _browser.Log($"Build ISO failed: {ex.Message}");
            }
            finally { _building = false; }
        });
    }

    private void DrawBuildProgress()
    {
        if (!_building) return;

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(560f, 280f), ImGuiCond.Always);
        ImGui.Begin("Building ISO...",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);

        ImGui.ProgressBar(_buildProgress, new Vector2(-1f, 22f), $"{_buildProgress * 100f:F0}%");
        ImGui.Separator();
        ImGui.BeginChild("##buildlog", new Vector2(-1f, -1f), ImGuiChildFlags.None);
        lock (_buildLogLock)
        {
            foreach (var line in _buildLog) ImGui.TextUnformatted(line);
        }
        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) ImGui.SetScrollHereY(1f);
        ImGui.EndChild();

        ImGui.End();
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

    private void SyncWorldLighting(Entity chunkRoot)
    {
        var settings = AllEntities(chunkRoot).Select(c => c.Get<WorldLightingSettings>()).FirstOrDefault(s => s is not null);
        if (settings is null) return;

        var sm2 = chunkRoot.Get<ChunkSource>()?.Sm2;
        var sceneryItem = sm2?.GetItem<PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);
        if (sceneryItem is null) return;

        int fogIdxToSave = Math.Clamp(settings.FogColorIndex, 0, CrashEngine.Importer.MeshDecoder.FogColors.Length - 1);
        sceneryItem.FogColor = (uint)fogIdxToSave;

        int ambientSynced = 0, directionalSynced = 0;
        if (sceneryItem.AmbientLights.Count == 0)
        {
            sceneryItem.AmbientLights.Add(new TwinAmbientLight
            {
                UnkData = 0x00000100,
                Radius = 4.5f,
                Position = new TwinVec4(-215.889404f, -1.5f, -1493.219116f, 1f),
                UnkVec1 = new TwinVec4(-149909.125f, -150001.5f, -149666.171875f, 1f),
                UnkVec2 = new TwinVec4(150090.875f, 149998.5f, 150333.828125f, 1f),
            });
        }
        {
            var c = settings.AmbientColor * settings.Intensity;
            sceneryItem.AmbientLights[0].Color = new TwinVec4(c.X, c.Y, c.Z, 0f);
            ambientSynced = 1;
        }

        (uint UnkData, float Radius, TwinVec4 Position, TwinVec4 UnkVec1, TwinVec4 UnkVec2, short UnkShort)[] dirRefs =
        {
            (0x00000101, 7.436417f, new TwinVec4(-216.137772f, -1.5f, -1492.898926f, 1f),
             new TwinVec4(-299909.34375f, -300001.46875f, -299665.8125f, 1f),
             new TwinVec4(300090.59375f, 299998.46875f, 300334.125f, 1f), -5040),
            (0x00000101, 4.111821f, new TwinVec4(-216.137772f, -1.5f, -1492.898926f, 1f),
             new TwinVec4(-249909.359375f, -250001.484375f, -249665.828125f, 1f),
             new TwinVec4(250090.609375f, 249998.484375f, 250334.125f, 1f), 4712),
        };
        while (sceneryItem.DirectionalLights.Count < settings.Directional.Count)
        {
            var r = dirRefs[Math.Min(sceneryItem.DirectionalLights.Count, dirRefs.Length - 1)];
            sceneryItem.DirectionalLights.Add(new TwinDirectionalLight
            {
                UnkData = r.UnkData, Radius = r.Radius, Position = r.Position,
                UnkVec1 = r.UnkVec1, UnkVec2 = r.UnkVec2, UnkShort = r.UnkShort,
            });
        }

        int n = Math.Min(settings.Directional.Count, sceneryItem.DirectionalLights.Count);
        for (int i = 0; i < n; i++)
        {
            var (col, dir) = settings.Directional[i];
            var boosted = col * settings.Intensity;
            var light = sceneryItem.DirectionalLights[i];
            light.Color = new TwinVec4(boosted.X, boosted.Y, boosted.Z, 0f);
            light.UnkVec3 = new TwinVec4(-dir.X, dir.Y, dir.Z, 0f);
            directionalSynced++;
        }

        if (ambientSynced > 0 || directionalSynced > 0) sceneryItem.HasLighting = true;

        _browser.Log($"Synced world lighting: {ambientSynced} ambient + {directionalSynced} directional light(s) (intensity x{settings.Intensity:F2}), " +
                     $"fog = {CrashEngine.Importer.MeshDecoder.FogColorNames[fogIdxToSave]}, HasLighting={sceneryItem.HasLighting}.");

        // Amedo 2026-09-19
        int gAmb = sceneryItem.AmbientLights.Count, gDir = sceneryItem.DirectionalLights.Count, gPt = sceneryItem.PointLights.Count;
        int totalNodes = sceneryItem.Sceneries.Count;
        int rescoped = 0;
        void EnableEditorLight(int gi)
        {
            if (gi < 0 || gi >= 128 || totalNodes == 0) return;
            int cnt = sceneryItem.Sceneries.Count(nd => nd.LightsEnabler[gi]);
            if (cnt != 0 && cnt != totalNodes) return;
            if (cnt == totalNodes) return;
            foreach (var nd in sceneryItem.Sceneries) nd.LightsEnabler[gi] = true;
            rescoped++;
        }
        for (int p = 0; p < gPt; p++) EnableEditorLight(gAmb + gDir + p);
        for (int q = 0; q < sceneryItem.NegativeLights.Count; q++) EnableEditorLight(gAmb + gDir + gPt + q);
        if (rescoped > 0) _browser.Log($"Enabled {rescoped} added light(s) on all scenery nodes; locality comes from each light's Position+Radius+UnkShort falloff (RE-verified).");
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
