using CrashEngine.Core;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-12
public sealed partial class GameScene
{
    private sealed record SceneFile(string Rm2, string Folder, string Name, bool Edited);

    private const string LevelsRoot = @"Levels\";

    private string _assetsScenesFolder = "";
    private string? _assetsSelectedScene;
    private List<SceneFile>? _sceneFiles;

    private List<SceneFile> SceneFiles()
    {
        SyncWithBrowser();
        if (_sceneFiles is not null) return _sceneFiles;
        var saved = SavedChunksDir;
        _sceneFiles = _browser.VisibleScenes()
            .Select(rm2 => new SceneFile(rm2, GamePaths.Folder(ShownPath(rm2)), GamePaths.FileNameWithoutExtension(rm2), File.Exists(GamePaths.OnDisk(saved, rm2)))) // Amedo 2026-11-14
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _sceneFiles;
    }

    private static string ShownPath(string rel) =>
        rel.StartsWith(LevelsRoot, StringComparison.OrdinalIgnoreCase) ? rel[LevelsRoot.Length..] : rel;

    private string RealFolder(string shown) =>
        SceneFiles().Any(s => s.Rm2.StartsWith(LevelsRoot + shown + "\\", StringComparison.OrdinalIgnoreCase)) ? LevelsRoot + shown : shown;

    private List<string> SceneSubFolders(string folder)
    {
        var prefix = folder.Length == 0 ? "" : folder + "\\";
        return SceneFiles()
            .Where(s => s.Folder.Length > prefix.Length && s.Folder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Folder[prefix.Length..].Split('\\')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void OpenSceneFromProject(string rm2)
    {
        if (!rm2.Equals(_rm2, StringComparison.OrdinalIgnoreCase)) _pendingOpenLevelPath = rm2;
    }

    private void ToggleNewGame()
    {
        var current = _rm2;
        LeaveScene(() =>
        {
            _browser.SetNewGame(!_browser.IsNewGame);
            _browser.OpenLevel(_browser.IsVisible(current) ? current : _browser.StartScene);
        });
    }

    private void DrawScenesBreadcrumb()
    {
        ImGui.SameLine(); ImGui.TextDisabled(">"); ImGui.SameLine();
        if (ImGui.SmallButton("Scenes")) GoToAssets(AssetsView.Scenes);
        if (_assetsScenesFolder.Length > 0)
        {
            var segs = _assetsScenesFolder.Split('\\');
            var accum = "";
            for (int i = 0; i < segs.Length; i++)
            {
                accum = i == 0 ? segs[i] : accum + "\\" + segs[i];
                ImGui.SameLine(); ImGui.TextDisabled(">"); ImGui.SameLine();
                if (ImGui.SmallButton($"{segs[i]}##scrumb{i}"))
                {
                    _assetsScenesFolder = accum;
                    _assetsLevel = ""; _assetsCategory = ""; _assetsFilter = "";
                }
            }
        }
        if (_assetsLevel.Length > 0)
        {
            ImGui.SameLine(); ImGui.TextDisabled(">"); ImGui.SameLine();
            if (ImGui.SmallButton(GamePaths.FileName(_assetsLevel))) { _assetsCategory = ""; _assetsFilter = ""; } // Amedo 2026-11-14
        }
        if (_assetsCategory.Length > 0)
        {
            ImGui.SameLine(); ImGui.TextDisabled(">"); ImGui.SameLine();
            ImGui.TextUnformatted(_assetsCategory);
        }
    }

    private void DrawScenesFolder()
    {
        DrawMoveBanner();

        const float cell = 84f;
        float avail = ImGui.GetContentRegionAvail().X;
        int columns = Math.Max(1, (int)(avail / (cell + 8f)));
        int col = 0;
        void Wrap() { if (++col < columns) ImGui.SameLine(); else col = 0; }

        bool searching = !string.IsNullOrWhiteSpace(_assetsFilter);
        if (!searching)
            foreach (var sub in SceneSubFolders(_assetsScenesFolder))
            {
                DrawSceneFolderTile(sub, cell);
                Wrap();
            }

        var scenes = searching
            ? SceneFiles().Where(s => s.Rm2.Contains(_assetsFilter, StringComparison.OrdinalIgnoreCase))
            : SceneFiles().Where(s => s.Folder.Equals(_assetsScenesFolder, StringComparison.OrdinalIgnoreCase));
        foreach (var scene in scenes)
        {
            DrawSceneTile(scene, cell);
            Wrap();
        }
        if (col != 0) ImGui.NewLine();

        if (ImGui.BeginPopupContextWindow("##scenesctx", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            if (ImGui.MenuItem("New Scene...")) _browser.StartNewScene();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(LevelBrowserScene.NewSceneHelp);
            if (_browser.HasProject && ImGui.MenuItem("Add Scene (claim a level slot)...")) _browser.StartClaim();
            ImGui.EndPopup();
        }
    }

    private void DrawMoveBanner()
    {
        if (_browser.MovingScene is { } moving)
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), $"Moving {GamePaths.FileNameWithoutExtension(moving)}: click a folder to move it inside, or a scene to replace it.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Cancel Move")) _browser.CancelMove();
        }
        if (_browser.MoveError is { } error)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), error);
    }

    private void DrawSceneFolderTile(string name, float cell)
    {
        var path = _assetsScenesFolder.Length == 0 ? name : _assetsScenesFolder + "\\" + name;
        bool moving = _browser.MovingScene is not null;
        ImGui.PushID("scnfld_" + name);
        ImGui.BeginGroup();
        if (DrawFolderTile("##btn", cell))
        {
            if (moving) _browser.MoveInto(RealFolder(path));
            else _assetsScenesFolder = path;
        }
        if (moving && ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!moving && ImGui.BeginPopupContextItem("##ctx"))
        {
            if (ImGui.MenuItem("Open")) _assetsScenesFolder = path;
            if (_browser.HasProject && ImGui.MenuItem("Add Scene (claim a level slot)...")) _browser.StartClaim();
            ImGui.EndPopup();
        }
        ImGui.TextWrapped(name);
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private void DrawSceneTile(SceneFile scene, float cell)
    {
        bool open = scene.Rm2.Equals(_rm2, StringComparison.OrdinalIgnoreCase);
        bool moving = _browser.MovingScene is not null;
        ImGui.PushID("scn_" + scene.Rm2);
        ImGui.BeginGroup();
        if (DrawSceneIcon("##btn", cell, open, scene.Rm2.Equals(_assetsSelectedScene, StringComparison.OrdinalIgnoreCase)))
        {
            if (moving) _browser.MoveOnto(scene.Rm2);
            else _assetsSelectedScene = scene.Rm2;
        }
        if (ImGui.IsItemHovered())
        {
            if (moving) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            else if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) OpenSceneFromProject(scene.Rm2);
            ImGui.SetTooltip(scene.Rm2 + (open ? "\nOpen now" : "\nDouble-click to open") + (scene.Edited ? "\nHas saved edits" : ""));
        }
        if (!moving) DrawSceneMenu(scene, open);
        ImGui.TextWrapped(scene.Edited ? scene.Name + " *" : scene.Name);
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private void DrawSceneMenu(SceneFile scene, bool open)
    {
        if (!ImGui.BeginPopupContextItem("##ctx")) return;
        if (ImGui.MenuItem("Open", null, false, !open)) OpenSceneFromProject(scene.Rm2);
        if (ImGui.MenuItem("Browse Scenery"))
        {
            _assetsScenesFolder = scene.Folder;
            _assetsLevel = scene.Rm2[..^4];
            _assetsCategory = ""; _assetsFilter = "";
        }
        ImGui.Separator();
        ImGui.BeginDisabled(open);
        if (ImGui.MenuItem("Move...")) _browser.StartMove(scene.Rm2);
        if (ImGui.MenuItem("Rename...")) _browser.StartRename(scene.Rm2);
        if (ImGui.MenuItem("Delete")) _browser.DeleteSavedScene(scene.Rm2);
        if (_browser.IsClaimed(scene.Rm2) && ImGui.MenuItem("Unclaim (restore original)")) _browser.Unclaim(scene.Rm2);
        ImGui.EndDisabled();
        if (open) ImGui.TextDisabled("Open another scene to move, rename or delete this one.");
        ImGui.EndPopup();
    }

    private static bool DrawSceneIcon(string id, float cell, bool open, bool selected)
    {
        var pos = ImGui.GetCursorScreenPos();
        bool clicked = ImGui.InvisibleButton(id, new Vector2(cell, cell));
        bool hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        if (selected) dl.AddRectFilled(pos, pos + new Vector2(cell, cell), ImGui.GetColorU32(ImGuiCol.Header), 4f);
        float p = cell * 0.14f;
        var a = pos + new Vector2(p, p);
        var b = pos + new Vector2(cell - p, cell - p);
        float w = b.X - a.X, h = b.Y - a.Y;
        dl.AddRectFilled(a, b, ImGui.GetColorU32(hovered ? new Vector4(0.38f, 0.52f, 0.70f, 1f) : new Vector4(0.30f, 0.42f, 0.58f, 1f)), 5f);
        uint hill = ImGui.GetColorU32(new Vector4(0.86f, 0.90f, 0.95f, 1f));
        float ground = b.Y - h * 0.14f;
        dl.AddTriangleFilled(new Vector2(a.X + w * 0.08f, ground), new Vector2(a.X + w * 0.40f, a.Y + h * 0.38f), new Vector2(a.X + w * 0.72f, ground), hill);
        dl.AddTriangleFilled(new Vector2(a.X + w * 0.45f, ground), new Vector2(a.X + w * 0.68f, a.Y + h * 0.52f), new Vector2(a.X + w * 0.92f, ground), hill);
        dl.AddCircleFilled(new Vector2(a.X + w * 0.77f, a.Y + h * 0.26f), w * 0.09f, hill);
        if (open) dl.AddRect(a, b, ImGui.GetColorU32(new Vector4(1f, 0.6f, 0.15f, 1f)), 5f, ImDrawFlags.None, 2.5f);
        return clicked;
    }
}
