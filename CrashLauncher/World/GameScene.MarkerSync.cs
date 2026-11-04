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
using PS2AnyPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPath;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;
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
using ITwinObject = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void SyncTriggerMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.TriggerMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.TriggerMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var scl = e.Transform.Scale;
            marker.Source.Trigger.Position = new TwinVec4(pos.X, pos.Y, pos.Z, 1f);
            marker.Source.Trigger.Scale    = new TwinVec4(scl.X, scl.Y, scl.Z, 1f);
            var rot = System.Numerics.Quaternion.Normalize(e.Transform.Rotation);
            marker.Source.Trigger.Rotation = new TwinVec4(rot.X, rot.Y, rot.Z, rot.W);
        }
    }

    private void SyncCameraMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.CameraMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.CameraMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var scl = e.Transform.Scale;
            marker.Source.CamTrigger.Position = new TwinVec4(pos.X, pos.Y, pos.Z, 1f);
            marker.Source.CamTrigger.Scale    = new TwinVec4(scl.X, scl.Y, scl.Z, 1f);
            var rot = System.Numerics.Quaternion.Normalize(e.Transform.Rotation);
            marker.Source.CamTrigger.Rotation = new TwinVec4(rot.X, rot.Y, rot.Z, rot.W);
        }
    }

    private void SyncPositionMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.PositionMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.PositionMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var w = marker.Source.Position.W;
            marker.Source.Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(pos.X, pos.Y, pos.Z, w);
        }
    }

    private void SyncAiPositionMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.AiPositionMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.AiPositionMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var w = marker.Source.Position.W;
            marker.Source.Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(pos.X, pos.Y, pos.Z, w);
        }
    }

    // Amedo 2026-09-19
    private void SyncSceneryLightMarkers(Entity chunkRoot)
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<CrashEngine.Importer.SceneryLightMarker>()))
        {
            var marker = e.Get<CrashEngine.Importer.SceneryLightMarker>()!;
            if (marker.Source is null) continue;
            var pos = e.Transform.Position;
            var w = marker.Source.Position.W;
            marker.Source.Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(pos.X, pos.Y, pos.Z, w);
        }
    }
}
