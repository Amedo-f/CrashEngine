using System.Text.Json;
using BHRecord = Twinsanity.TwinsanityInterchange.Common.BHRecord;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace CrashEngine.Importer;

public static class AssetUnpacker
{
    public sealed record Result(int Files, int Chunks, int Assets, int Textures, int Errors, int Skipped = 0);

    private static readonly (int Id, string Folder)[] GraphicsSections =
    {
        (Constants.GRAPHICS_MATERIALS_SECTION,    "Material"),
        (Constants.GRAPHICS_MODELS_SECTION,       "Model"),
        (Constants.GRAPHICS_RIGID_MODELS_SECTION, "RigidModel"),
        (Constants.GRAPHICS_SKINS_SECTION,        "Skin"),
        (Constants.GRAPHICS_BLEND_SKINS_SECTION,  "BlendSkin"),
        (Constants.GRAPHICS_MESHES_SECTION,       "Mesh"),
        (Constants.GRAPHICS_LODS_SECTION,         "LodModel"),
        (Constants.GRAPHICS_SKYDOMES_SECTION,     "Skydome"),
    };

    private static readonly (int Id, string Folder)[] CodeSections =
    {
        (Constants.CODE_GAME_OBJECTS_SECTION,                  "GameObject"),
        (Constants.CODE_BEHAVIOURS_SECTION,                    "BehaviourGraph"),
        (Constants.CODE_ANIMATIONS_SECTION,                    "Animation"),
        (Constants.CODE_OGIS_SECTION,                          "OGI"),
        (Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION,  "BehaviourCommandsSequence"),
        (Constants.CODE_SOUND_EFFECTS_SECTION,                 "SoundEffect"),
        (Constants.CODE_LANG_ENG_SECTION,                      "SoundEffectEN"),
        (Constants.CODE_LANG_FRE_SECTION,                      "SoundEffectFR"),
        (Constants.CODE_LANG_GER_SECTION,                      "SoundEffectGR"),
        (Constants.CODE_LANG_SPA_SECTION,                      "SoundEffectSP"),
        (Constants.CODE_LANG_ITA_SECTION,                      "SoundEffectIT"),
        (Constants.CODE_LANG_JPN_SECTION,                      "SoundEffectJP"),
    };

    private static readonly (int Id, string Folder)[] LayoutSections =
    {
        (Constants.LAYOUT_INSTANCES_SECTION,     "Instance"),
        (Constants.LAYOUT_TEMPLATES_SECTION,     "InstanceTemplate"),
        (Constants.LAYOUT_AI_POSITIONS_SECTION,  "AiPosition"),
        (Constants.LAYOUT_AI_PATHS_SECTION,      "AiPath"),
        (Constants.LAYOUT_POSITIONS_SECTION,     "Position"),
        (Constants.LAYOUT_PATHS_SECTION,         "Path"),
        (Constants.LAYOUT_SURFACES_SECTION,      "CollisionSurface"),
        (Constants.LAYOUT_TRIGGERS_SECTION,      "Trigger"),
        (Constants.LAYOUT_CAMERAS_SECTION,       "Camera"),
    };

    public static Result Unpack(PackageReader pkg, string projectPath,
                                Action<string> log, Action<float> progress,
                                IReadOnlySet<string>? skip = null,
                                Action<string>? onRecordDone = null,
                                CancellationToken ct = default,
                                ManualResetEventSlim? pauseGate = null)
    {
        var assetsRoot = Path.Combine(projectPath, "assets");
        var rawRoot    = Path.Combine(assetsRoot, "raw");

        int files = 0, chunks = 0, assets = 0, textures = 0, errors = 0, skipped = 0, done = 0;
        var records = pkg.Records;

        void ProcessRecord(BHRecord rec)
        {
            pauseGate?.Wait(ct);
            if (skip is not null && skip.Contains(rec.Path))
            {
                Interlocked.Increment(ref skipped);
                int nSkip = Interlocked.Increment(ref done);
                progress(nSkip / (float)records.Count);
                return;
            }

            var outPath = Path.Combine(rawRoot, rec.Path.Replace('\\', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            using (var ms = pkg.OpenRecord(rec))
            using (var fs = File.Create(outPath))
                ms.CopyTo(fs);
            Interlocked.Increment(ref files);

            var low = rec.Path.ToLowerInvariant();
            var isRm2     = low.EndsWith(".rm2");
            var isSm2     = low.EndsWith(".sm2");
            var isDefault = low.EndsWith("default.rm2");

            if (isRm2 || isSm2)
            {
                log($"Unpacking {Path.GetFileName(rec.Path)}...");
                try
                {
                    using var ms = pkg.OpenRecord(rec);
                    using var br = new BinaryReader(ms);

                    ITwinSection chunk = isDefault ? new PS2Default()
                                       : isRm2     ? new PS2AnyTwinsanityRM2()
                                                   : new PS2AnyTwinsanitySM2();
                    chunk.Read(br, (int)ms.Length);
                    Interlocked.Increment(ref chunks);

                    var (a, t) = DumpChunk(chunk, rec.Path, assetsRoot, isSm2, log);
                    Interlocked.Add(ref assets, a);
                    Interlocked.Add(ref textures, t);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref errors);
                    log($"  WARN {rec.Path}: {ex.Message}");
                }
            }

            onRecordDone?.Invoke(rec.Path);
            int n = Interlocked.Increment(ref done);
            progress(n / (float)records.Count);
        }

        int splitIndex = (int)(records.Count * 0.7f);
        var firstPart  = records.Take(splitIndex).ToList();
        var secondPart = records.Skip(splitIndex).ToList();

        var multiCoreOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount)),
            CancellationToken = ct,
        };
        Parallel.ForEach(firstPart, multiCoreOptions, ProcessRecord);

        var singleCoreOptions = new ParallelOptions { MaxDegreeOfParallelism = 1, CancellationToken = ct };
        Parallel.ForEach(secondPart, singleCoreOptions, ProcessRecord);

        return new Result(files, chunks, assets, textures, errors, skipped);
    }


    private static (int assets, int textures) DumpChunk(ITwinSection chunk, string chunkPath,
                                                        string assetsRoot, bool isSm2,
                                                        Action<string> log)
    {
        int assets = 0, textures = 0;

        var variation = chunkPath.Replace('\\', '_').Replace('/', '_');

        int gfxId = isSm2 ? Constants.SCENERY_GRAPHICS_SECTION
                          : Constants.LEVEL_GRAPHICS_SECTION;
        var gfx = chunk.GetItem<PS2AnyGraphicsSection>((uint)gfxId);
        if (gfx is not null)
        {
            textures += DumpTextures(gfx, assetsRoot, chunkPath, variation, log);

            foreach (var (id, folder) in GraphicsSections)
                assets += DumpSection(gfx, id, Path.Combine(assetsRoot, folder),
                                      folder, chunkPath, variation, log);
        }

        if (!isSm2)
        {
            var code = chunk.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
            if (code is not null)
                foreach (var (id, folder) in CodeSections)
                    assets += DumpSection(code, id, Path.Combine(assetsRoot, folder),
                                          folder, chunkPath, variation, log);

            for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = chunk.GetItem<BaseTwinSection>((uint)lid);
                if (layout is null) continue;

                foreach (var (id, folder) in LayoutSections)
                {
                    var dir = Path.Combine(assetsRoot, "Instance",
                        chunkPath.Replace('\\', Path.DirectorySeparatorChar),
                        folder, $"Layout_{lid}");
                    assets += DumpSection(layout, id, dir, folder, chunkPath, variation, log);
                }
            }
        }

        return (assets, textures);
    }

    private static int DumpSection(BaseTwinSection parent, int sectionId,
                                   string outDir, string typeName,
                                   string chunkPath, string variation,
                                   Action<string> log)
    {
        var sec = parent.GetItem<BaseTwinSection>((uint)sectionId);
        if (sec is null || sec.GetItemsAmount() == 0) return 0;

        int written = 0;
        for (int i = 0; i < sec.GetItemsAmount(); i++)
        {
            if (sec.GetItem(i) is not ITwinItem item) continue;
            try
            {
                var name  = SafeName(item.GetName(), item.GetID());
                var stem  = $"{name}_{variation}";

                if (item is ITwinSkin skin)
                    foreach (var ss in skin.SubSkins) ss.CalculateData();
                else if (item is ITwinBlendSkin blend)
                    foreach (var sb in blend.SubBlends)
                    {
                        sb.GetType().GetMethod("CalculateData", Type.EmptyTypes)?.Invoke(sb, null);
                        foreach (var m in sb.Models)
                            m.GetType().GetMethod("CalculateData", Type.EmptyTypes)?.Invoke(m, null);
                    }

                using var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms))
                    item.Write(bw);

                Directory.CreateDirectory(outDir);
                File.WriteAllBytes(Path.Combine(outDir, stem + ".data"), ms.ToArray());
                WriteJson(Path.Combine(outDir, stem + ".json"),
                          typeName, item.GetName(), item.GetID(), stem + ".data", chunkPath);
                written++;
            }
            catch (Exception ex)
            {
                log($"  WARN {typeName} {item.GetID():X8}: {ex.Message}");
            }
        }
        return written;
    }

    private static int DumpTextures(PS2AnyGraphicsSection gfx, string assetsRoot,
                                    string chunkPath, string variation, Action<string> log)
    {
        var texSec = gfx.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (texSec is null) return 0;

        var outDir = Path.Combine(assetsRoot, "Texture");
        int written = 0;
        for (int i = 0; i < texSec.GetItemsAmount(); i++)
        {
            if (texSec.GetItem(i) is not PS2AnyTexture tex) continue;
            try
            {
                tex.CalculateData();
                if (tex.Colors.Count == 0) continue;

                int w = tex.ImageWidthPower  > 0 ? (1 << tex.ImageWidthPower)  : 1;
                int h = tex.ImageHeightPower > 0 ? (1 << tex.ImageHeightPower) : 1;

                var rgba = new byte[tex.Colors.Count * 4];
                int idx = 0;
                foreach (TwinColor c in tex.Colors)
                {
                    rgba[idx++] = c.R;
                    rgba[idx++] = c.G;
                    rgba[idx++] = c.B;
                    rgba[idx++] = c.A;
                }

                var stem = $"Texture {tex.GetID():X8}_{variation}";
                Directory.CreateDirectory(outDir);
                using (var fs = File.Create(Path.Combine(outDir, stem + ".png")))
                    new StbImageWriteSharp.ImageWriter().WritePng(
                        rgba, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);

                WriteJson(Path.Combine(outDir, stem + ".json"),
                          "Texture", $"Texture {tex.GetID():X8}", tex.GetID(), stem + ".png", chunkPath);
                written++;
            }
            catch (Exception ex)
            {
                log($"  WARN texture {tex.GetID():X8}: {ex.Message}");
            }
        }
        return written;
    }


    private static void WriteJson(string path, string type, string name, uint id,
                                  string dataFile, string chunkPath)
    {
        var json = JsonSerializer.Serialize(new
        {
            Type = type,
            Name = name,
            ID = id,
            Data = dataFile,
            Variation = chunkPath,
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string SafeName(string? name, uint id)
    {
        if (string.IsNullOrWhiteSpace(name))
            return $"{id:X8}";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (cleaned.Length == 0) return $"{id:X8}";
        if (cleaned.Length > 80) cleaned = cleaned[..80];
        return $"{cleaned} {id:X8}";
    }
}
