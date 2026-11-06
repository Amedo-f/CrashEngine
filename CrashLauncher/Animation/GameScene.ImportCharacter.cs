using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using TwinMorphAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinMorphAnimation;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using Vector3 = System.Numerics.Vector3;

namespace CrashLauncher;

// Amedo 2026-10-05
public sealed partial class GameScene
{
    private bool ImportCharacter(string modelPath, bool flipX)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Import Character: no level currently loaded."); return true; }
        var rc = CharacterImporter.Extract(modelPath, out string error);
        if (rc is null) { _browser.Log($"Import Character: {error} — {Path.GetFileName(modelPath)} not imported."); return true; }
        if (rc.Joints.Count > RenderPipelineMaxBones)
        {
            _browser.Log($"Import Character: {rc.Joints.Count} bones, the editor draws at most {RenderPipelineMaxBones} — not imported.");
            return true;
        }

        var worlds = rc.Joints.Select(j => flipX ? CharacterBuild.Mirrored(j.BindWorld) : j.BindWorld).ToList();
        foreach (var sm in rc.Meshes)
        {
            if (!flipX) break;
            var m = sm.Mesh;
            for (int i = 0; i < m.Positions.Length; i++) m.Positions[i] = new Vector3(-m.Positions[i].X, m.Positions[i].Y, m.Positions[i].Z);
            for (int i = 0; i < m.Normals.Length; i++) m.Normals[i] = new Vector3(-m.Normals[i].X, m.Normals[i].Y, m.Normals[i].Z);
            for (int i = 0; i + 2 < m.Indices.Length; i += 3) (m.Indices[i + 1], m.Indices[i + 2]) = (m.Indices[i + 2], m.Indices[i + 1]);
        }
        var joints = CharacterBuild.Joints(rc.Joints, worlds);

        var anims = new List<PS2AnyAnimation>();
        var clipNotes = new List<string>();
        foreach (var clip in rc.Clips)
        {
            var frames = clip.Local.Select(p => flipX ? p.Select(CharacterBuild.Mirrored).ToArray() : p).ToList();
            var main = CharacterBuild.Animation(frames, joints.Count, out int clamped);
            if (AnimChannelOps.FitsGameLimits(main) is { } limit) { clipNotes.Add($"clip '{clip.Name}' skipped: {limit}"); continue; }
            anims.Add(new PS2AnyAnimation
            {
                HasAnimationData = true, HasFacialAnimationData = false, TotalFrames = main.TotalFrames, DefaultFPS = (byte)clip.Fps,
                MainAnimation = main, FacialAnimation = new TwinMorphAnimation(),
            });
            clipNotes.Add($"'{clip.Name}' {main.TotalFrames} frames @ {clip.Fps} fps{(clamped > 0 ? $" ({clamped} value(s) past ±8 m clamped)" : "")}");
        }

        try
        {
            var rec = MeshDecoder.BakeExternalCharacterAsObject(Engine.Instance.GL, chunkSource.Rm2, chunkSource.MeshTables!, chunkSource.TexCache,
                chunkSource.GlobalRm2, rc.Meshes, joints, anims, Path.GetFileNameWithoutExtension(modelPath), out string log);
            if (rec is null) { _browser.Log($"Import Character: {log}"); return true; }
            chunkSource.Transplants[rec.ObjectId] = rec;

            var section = FindInstanceSection(chunkRoot, chunkSource);
            if (section is null) { _browser.Log("Import Character: this level has no instances section to add to."); return true; }
            uint stateFlags = NewInstanceStateFlags;
            float extent = 0f;
            foreach (var sm in rc.Meshes)
                foreach (var p in sm.Mesh.Positions) extent = MathF.Max(extent, MathF.Max(MathF.Abs(p.X), MathF.Max(MathF.Abs(p.Y), MathF.Abs(p.Z))));
            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            if (_camera is not null) spawnPos += _camera.Forward * MathF.Max(3f, extent * 2.5f);
            var inst = NewInstance(new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f), rec.ObjectId, null, stateFlags);
            var instRoot = InstancesRoot(chunkRoot);
            var entity = AddOrReuseInstance(section, inst, instRoot, chunkSource);
            SelectClicked(entity, false);
            _browser.Log($"Import Character: {log}. Animations: {(clipNotes.Count == 0 ? "none in the file" : string.Join("; ", clipNotes))}." +
                         (rc.Notes.Count > 0 ? " Notes: " + string.Join("; ", rc.Notes) + "." : "") +
                         " Open the Animation workspace to see its bones. Save Chunk to keep it (the skin is drawn from the imported data; playing it in PCSX2 is not verified yet).");
        }
        catch (Exception ex) { _browser.Log($"Import Character failed: {ex.Message}"); }
        return true;
    }

    private const int RenderPipelineMaxBones = CrashEngine.Renderer.RenderPipeline.MaxBones;
}
