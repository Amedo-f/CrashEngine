using CrashEngine.Core;
using CrashEngine.Importer;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinTrigger = Twinsanity.TwinsanityInterchange.Common.TwinTrigger;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void SyncTriggerMarkers(Entity chunkRoot) =>
        SyncMarkers<TriggerMarker>(chunkRoot, (e, m) => { if (m.Source is not null) WriteTriggerBox(e, m.Source.Trigger); });

    private void SyncCameraMarkers(Entity chunkRoot) =>
        SyncMarkers<CameraMarker>(chunkRoot, (e, m) => { if (m.Source is not null) WriteTriggerBox(e, m.Source.CamTrigger); });

    private void SyncPositionMarkers(Entity chunkRoot) =>
        SyncMarkers<PositionMarker>(chunkRoot, (e, m) => { if (m.Source is not null) m.Source.Position = MarkerPosition(e, m.Source.Position); });

    private void SyncAiPositionMarkers(Entity chunkRoot) =>
        SyncMarkers<AiPositionMarker>(chunkRoot, (e, m) => { if (m.Source is not null) m.Source.Position = MarkerPosition(e, m.Source.Position); });

    private void SyncSceneryLightMarkers(Entity chunkRoot) =>
        SyncMarkers<SceneryLightMarker>(chunkRoot, (e, m) => { if (m.Source is not null) m.Source.Position = MarkerPosition(e, m.Source.Position); });

    private void SyncMarkers<T>(Entity chunkRoot, Action<Entity, T> write) where T : Component
    {
        foreach (var e in AllEntities(chunkRoot).Where(e => e.Has<T>()))
            write(e, e.Get<T>()!);
    }

    private static void WriteTriggerBox(Entity e, TwinTrigger trigger)
    {
        var pos = e.Transform.Position;
        var scl = e.Transform.Scale;
        trigger.Position = new TwinVec4(pos.X, pos.Y, pos.Z, 1f);
        trigger.Scale    = new TwinVec4(scl.X, scl.Y, scl.Z, 1f);
        var rot = System.Numerics.Quaternion.Normalize(e.Transform.Rotation);
        trigger.Rotation = new TwinVec4(rot.X, rot.Y, rot.Z, rot.W);
    }

    private static TwinVec4 MarkerPosition(Entity e, TwinVec4 old)
    {
        var pos = e.Transform.Position;
        return new TwinVec4(pos.X, pos.Y, pos.Z, old.W);
    }
}
