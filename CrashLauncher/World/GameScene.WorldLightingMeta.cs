using CrashEngine.Core;
using CrashEngine.Importer;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinAmbientLight = Twinsanity.TwinsanityInterchange.Common.Lights.AmbientLight;
using TwinDirectionalLight = Twinsanity.TwinsanityInterchange.Common.Lights.DirectionalLight;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;

namespace CrashLauncher;

public sealed partial class GameScene
{
    // Amedo 2026-09-21
    private sealed class WorldLightingMeta
    {
        public float Intensity { get; set; } = 1f;
        public float SceneryBrightness { get; set; } = 1f;
        public float SkydomeBrightness { get; set; } = 1f;
    }

    private string GetWorldLightingMetaPath()
    {
        var root = ModeDir("WorldLighting");
        var rel = _rm2.Replace('/', '\\');
        if (rel.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)) rel = rel[..^4];
        return Path.Combine(root, rel + ".json");
    }

    private void SaveWorldLightingMeta(WorldLightingSettings settings, SkydomeMarker? sky)
    {
        try
        {
            var path = GetWorldLightingMetaPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(
                new WorldLightingMeta { Intensity = settings.Intensity, SceneryBrightness = settings.SceneryBrightness, SkydomeBrightness = sky?.Brightness ?? 1f },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private WorldLightingMeta? ReadWorldLightingMeta()
    {
        try
        {
            var path = GetWorldLightingMetaPath();
            return File.Exists(path) ? System.Text.Json.JsonSerializer.Deserialize<WorldLightingMeta>(File.ReadAllText(path)) : null;
        }
        catch { return null; }
    }

    private void RestoreSkydomeMeta(SkydomeMarker sky)
    {
        if (ReadWorldLightingMeta() is { } meta) sky.Brightness = sky.BakedBrightness = meta.SkydomeBrightness;
    }

    private void RestoreWorldLightingMeta(WorldLightingSettings settings)
    {
        try
        {
            if (ReadWorldLightingMeta() is not { } meta) return;
            float f = meta.Intensity;
            if (f > 0.001f)
            {
                settings.AmbientColor /= f;
                for (int i = 0; i < settings.Directional.Count; i++)
                {
                    var (col, dir) = settings.Directional[i];
                    settings.Directional[i] = (col / f, dir);
                }
            }
            settings.Intensity = f;

            settings.SceneryBrightness = meta.SceneryBrightness;
            settings.SceneryBakedBrightness = meta.SceneryBrightness;
        }
        catch { }
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

        SaveWorldLightingMeta(settings, AllEntities(chunkRoot).Select(c => c.Get<SkydomeMarker>()).FirstOrDefault(s => s is not null)); // Amedo 2026-09-21

        _browser.Log($"Synced world lighting: {ambientSynced} ambient + {directionalSynced} directional light(s) (intensity x{settings.Intensity:F2}), " +
                     $"fog = {CrashEngine.Importer.MeshDecoder.FogColorNames[fogIdxToSave]}, HasLighting={sceneryItem.HasLighting}.");

        // Amedo 2026-09-19
        int rescoped = EnableAddedSceneryLights(sceneryItem); // Amedo 2026-09-26
        if (rescoped > 0) _browser.Log($"Enabled {rescoped} added light(s) on all scenery nodes; locality comes from each light's Position+Radius+UnkShort falloff (RE-verified).");
    }
}
