using CrashEngine.Core;
using CrashEngine.Runtime.Debug;

namespace CrashLauncher;

// Amedo 2026-10-03
public sealed partial class GameScene
{
    private bool _showObjectCollision;
    private Entity? _objectCollisionRoot;

    private void AddObjectCollisionRoot(Entity chunkRoot)
    {
        var root = new Entity("ObjectCollision") { Active = _showObjectCollision };
        root.Add(new ObjectCollisionOverlay(chunkRoot, () => _simulation?.World));
        chunkRoot.AddChild(root);
        _objectCollisionRoot = root;
    }
}
