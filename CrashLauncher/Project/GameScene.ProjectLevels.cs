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
using PS2AnySound = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnySound;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PointLight = Twinsanity.TwinsanityInterchange.Common.Lights.PointLight;
using NegativeLight = Twinsanity.TwinsanityInterchange.Common.Lights.NegativeLight;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using ITwinCamera = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.ITwinCamera;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private string GetPristineArchiveSource()
    {
        var backupBd = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build",
                                     "original_backup_" + Path.GetFileName(_extractedRoot.TrimEnd('\\', '/')), "Crash.BD");
        return File.Exists(backupBd) ? backupBd : _extractedRoot;
    }

    private CrashProject? GetGameProject()
    {
        if (_gameProjectLoaded) return _gameProject;
        _gameProjectLoaded = true;
        var projectRoot = Path.GetDirectoryName(_scriptOut) ?? _extractedRoot;
        _gameProject = CrashProject.Open(projectRoot);
        return _gameProject;
    }

    // Amedo 2026-09-22
    private string ModeDir(string name) =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot,
            GetGameProject() is { IsNewGame: true } ? name + "_NewGame" : name);

    private string SavedChunksDir => ModeDir("SavedChunks");

    private List<string> GetSwapLevelList()
    {
        if (_swapLevelList is not null) return _swapLevelList;
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var pkg = PackageReader.Open(GetPristineArchiveSource());
            foreach (var rec in pkg.GetByExtension(".rm2"))
            {
                var rel = rec.Path.Replace('/', '\\');
                if (string.Equals(rel, _rm2, StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.EndsWith(@"Startup\Default.rm2", StringComparison.OrdinalIgnoreCase)) continue;
                if (seen.Add(rel)) list.Add(rel);
            }
        }
        catch (Exception ex) { _browser.Log($"Swap Object: level scan failed: {ex.Message}"); }

        try
        {
            var savedChunksDir = SavedChunksDir;
            if (Directory.Exists(savedChunksDir))
            {
                foreach (var file in Directory.GetFiles(savedChunksDir, "*.rm2", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(savedChunksDir, file);
                    if (string.Equals(rel, _rm2, StringComparison.OrdinalIgnoreCase)) continue;
                    if (seen.Add(rel)) list.Add(rel);
                }
            }
        }
        catch (Exception ex) { _browser.Log($"Swap Object: SavedChunks scan failed: {ex.Message}"); }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        _swapLevelList = list;
        return list;
    }

    private PS2AnyTwinsanityRM2? GetForeignRm2(string rm2Path)
    {
        if (_foreignRm2Cache.TryGetValue(rm2Path, out var cached)) return cached;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            var rm2 = ChunkImporter.ParseRm2Only(pkg, rm2Path);
            _foreignRm2Cache[rm2Path] = rm2;
            return rm2;
        }
        catch (Exception ex)
        {
            _browser.Log($"Swap Object: failed to open {rm2Path}: {ex.Message}");
            return null;
        }
    }
}
