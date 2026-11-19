using ImGuiNET;
using System.Numerics;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

// Amedo 2026-11-18
public sealed partial class GameScene
{
    private sealed record BehaviourAsset(string Folder, string Name, string? File, uint? Id, bool Global, PS2BehaviourGraph? Graph);

    private List<BehaviourAsset>? _behaviourAssets;
    private object? _behaviourAssetsRm2, _behaviourAssetsGlobalRm2;
    private volatile bool _behaviourAssetsDirty;
    private string _assetsBehaviourFolder = "";
    private string _assetsSelectedBehaviour = "";

    private List<BehaviourAsset> BehaviourAssets()
    {
        var src = CodeSource;
        if (_behaviourAssets is not null && !_behaviourAssetsDirty
            && ReferenceEquals(_behaviourAssetsRm2, src?.Rm2) && ReferenceEquals(_behaviourAssetsGlobalRm2, src?.GlobalRm2)) return _behaviourAssets;
        _behaviourAssetsDirty = false;
        _behaviourAssetsRm2 = src?.Rm2;
        _behaviourAssetsGlobalRm2 = src?.GlobalRm2;
        string root = Path.Combine(BehaviourCodeRoot, "Behaviours");
        var assets = new List<BehaviourAsset>();
        foreach (bool global in new[] { false, true })
        {
            var scripts = LevelBehaviours(global);
            var names = BehaviourSource.ClassNames(scripts);
            var files = BehaviourCodeFiles(global, log: false);
            string dir = BehaviourCodeDir(global);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, graph) in scripts)
            {
                string? file = files.GetValueOrDefault(id);
                if (file is not null) used.Add(file);
                string path = file ?? Path.Combine(dir, BehaviourSource.RelativePath(graph, names[id]));
                assets.Add(new BehaviourAsset(FolderOf(path), Path.GetFileNameWithoutExtension(path), file, id, global, graph));
            }
            if (Directory.Exists(dir))
                foreach (var path in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                    if (!used.Contains(path)) assets.Add(new BehaviourAsset(FolderOf(path), Path.GetFileNameWithoutExtension(path), path, null, global, null));
        }
        return _behaviourAssets = assets.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();

        string FolderOf(string path)
        {
            string folder = Path.GetRelativePath(root, Path.GetDirectoryName(path)!);
            return folder == "." ? "" : folder;
        }
    }

    private void DrawBehaviourAssetsBreadcrumb()
    {
        ImGui.SameLine(); ImGui.TextDisabled(">"); ImGui.SameLine();
        if (ImGui.SmallButton("Behaviours")) { _assetsBehaviourFolder = ""; _assetsFilter = ""; }
        if (_assetsBehaviourFolder.Length == 0) return;
        var parts = _assetsBehaviourFolder.Split(Path.DirectorySeparatorChar);
        for (int i = 0; i < parts.Length; i++)
        {
            ImGui.SameLine(); ImGui.TextDisabled(">"); ImGui.SameLine();
            if (ImGui.SmallButton($"{parts[i]}##bcrumb{i}")) { _assetsBehaviourFolder = string.Join(Path.DirectorySeparatorChar, parts[..(i + 1)]); _assetsFilter = ""; }
        }
    }

    private void DrawBehaviourAssets()
    {
        if (CodeSource?.Rm2 is null)
        {
            ImGui.TextDisabled("Open a scene to see its behaviour scripts.");
            return;
        }
        var assets = BehaviourAssets();
        const float cell = 84f;
        int columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (cell + 8f)));
        int col = 0;
        void Wrap() { if (++col < columns) ImGui.SameLine(); else col = 0; }

        string here = _assetsBehaviourFolder;
        bool searching = !string.IsNullOrWhiteSpace(_assetsFilter);
        if (!searching)
            foreach (var sub in assets.Select(a => NextBehaviourFolder(a.Folder, here)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            {
                DrawBehaviourFolderTile(sub, here.Length == 0 ? sub : here + Path.DirectorySeparatorChar + sub, cell);
                Wrap();
            }
        var shown = searching
            ? assets.Where(a => (here.Length == 0 || a.Folder.Equals(here, StringComparison.OrdinalIgnoreCase) || a.Folder.StartsWith(here + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                && a.Name.Contains(_assetsFilter.Trim(), StringComparison.OrdinalIgnoreCase))
            : assets.Where(a => a.Folder.Equals(here, StringComparison.OrdinalIgnoreCase));
        foreach (var asset in shown)
        {
            DrawBehaviourAssetTile(asset, cell);
            Wrap();
        }
        if (col != 0) ImGui.NewLine();
    }

    private static string? NextBehaviourFolder(string folder, string here)
    {
        string rest;
        if (here.Length == 0) rest = folder;
        else if (folder.StartsWith(here + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) rest = folder[(here.Length + 1)..];
        else return null;
        if (rest.Length == 0) return null;
        int cut = rest.IndexOf(Path.DirectorySeparatorChar);
        return cut < 0 ? rest : rest[..cut];
    }

    private void DrawBehaviourFolderTile(string name, string path, float cell)
    {
        ImGui.PushID("behfld_" + path);
        ImGui.BeginGroup();
        if (DrawFolderTile("##btn", cell)) _assetsBehaviourFolder = path;
        if (ImGui.IsItemHovered() && path.Equals(BehaviourCodeLevel(true), StringComparison.OrdinalIgnoreCase))
            ImGui.SetTooltip("Scripts shared by every level (Startup\\Default.rm2)");
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + cell);
        ImGui.TextWrapped(name);
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private void DrawBehaviourAssetTile(BehaviourAsset asset, float cell)
    {
        string key = asset.File ?? $"{asset.Global}:{asset.Id}";
        ImGui.PushID("beh_" + key);
        ImGui.BeginGroup();
        if (DrawCodeFileTile("##btn", cell, key == _assetsSelectedBehaviour, asset.File is not null)) _assetsSelectedBehaviour = key;
        if (ImGui.IsItemHovered())
        {
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) OpenBehaviourAsset(asset);
            string id = asset.Id is uint n ? $"\nScript 0x{n:X4}" : "";
            ImGui.SetTooltip(asset.File is null
                ? $"{asset.Name}.cs{id}\nNot written yet: double-click writes the file and opens it in Visual Studio"
                : $"{asset.Name}.cs{id}\nDouble-click to open in Visual Studio");
        }
        if (ImGui.BeginPopupContextItem("##ctx"))
        {
            if (ImGui.MenuItem("Open in Visual Studio")) OpenBehaviourAsset(asset);
            if (asset.Id is uint id && asset.Graph is not null && ImGui.MenuItem("Open in Behaviour Tab")) OpenBehaviourInTab(id, asset.Graph, asset.Global, manual: true);
            if (asset.File is not null && ImGui.MenuItem("Show in Explorer")) ShowInFileBrowser(Path.GetDirectoryName(asset.File)!);
            ImGui.EndPopup();
        }
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + cell);
        ImGui.TextWrapped(asset.Name);
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private void OpenBehaviourAsset(BehaviourAsset asset)
    {
        if (asset.File is not null) OpenBehaviourFileInIde(asset.File);
        else if (asset.Id is uint id && asset.Graph is not null) OpenBehaviourInIde(id, asset.Graph, asset.Global);
    }

    private static bool DrawCodeFileTile(string id, float cell, bool selected, bool written)
    {
        var pos = ImGui.GetCursorScreenPos();
        bool clicked = ImGui.InvisibleButton(id, new Vector2(cell, cell));
        bool hovered = ImGui.IsItemHovered();
        var draw = ImGui.GetWindowDrawList();
        if (selected || hovered)
            draw.AddRectFilled(pos, pos + new Vector2(cell, cell), ImGui.GetColorU32(new Vector4(0.26f, 0.59f, 0.98f, selected ? 0.35f : 0.15f)), 4f);
        float alpha = written ? 1f : 0.45f;
        float w = cell * 0.56f, h = cell * 0.72f, fold = cell * 0.16f;
        var a = new Vector2(pos.X + (cell - w) * 0.5f, pos.Y + (cell - h) * 0.5f);
        var b = a + new Vector2(w, h);
        uint page = ImGui.GetColorU32(new Vector4(0.92f, 0.92f, 0.95f, alpha));
        draw.AddRectFilled(a, new Vector2(b.X - fold, b.Y), page);
        draw.AddRectFilled(new Vector2(b.X - fold, a.Y + fold), b, page);
        draw.AddTriangleFilled(new Vector2(b.X - fold, a.Y), new Vector2(b.X, a.Y + fold), new Vector2(b.X - fold, a.Y + fold), ImGui.GetColorU32(new Vector4(0.68f, 0.68f, 0.74f, alpha)));
        var badgeSize = new Vector2(w * 0.78f, h * 0.34f);
        var badge = new Vector2(a.X + (w - badgeSize.X) * 0.5f, a.Y + h * 0.48f);
        draw.AddRectFilled(badge, badge + badgeSize, ImGui.GetColorU32(new Vector4(0.41f, 0.26f, 0.67f, alpha)), 3f);
        var label = ImGui.CalcTextSize("C#");
        draw.AddText(badge + (badgeSize - label) * 0.5f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), "C#");
        return clicked;
    }
}
