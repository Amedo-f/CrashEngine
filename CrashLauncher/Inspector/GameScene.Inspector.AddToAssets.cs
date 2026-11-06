using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawInspectorAddToAssets(Entity e)
    {
        if (e.Get<SceneryTile>() is not null)
        {
            if (ImGui.Button("Convert to Object (real)...", new Vector2(-1f, 0f)))
            {
                ConvertSceneryTileToObject(e);
            }

            var sceneryTargets = (_selectedSet.Count > 0 ? _selectedSet : new HashSet<Entity> { e })
                .Where(x => x.Get<SceneryTile>() is not null).ToList();
            if (sceneryTargets.Count > 0)
            {
                var label = sceneryTargets.Count > 1 ? $"Add {sceneryTargets.Count} Tiles to Assets..." : "Add to Assets...";
                if (ImGui.Button(label + "##addtoassets", new Vector2(-1f, 0f)))
                {
                    OpenAddToAssetsPopup(sceneryTargets);
                }
            }
        }

        if (e.Get<InstanceData>() is not null && e.Get<SceneryTile>() is null)
        {
            if (ImGui.Button("Add Object to Assets...##addobjtoassets", new Vector2(-1f, 0f)))
                OpenAddObjectToAssetsPopup(e);
        }
    }
}
