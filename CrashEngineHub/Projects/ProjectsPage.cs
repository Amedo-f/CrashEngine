using System.Numerics;
using CrashEngine.Install;
using CrashLauncher;
using ImGuiNET;

namespace CrashEngineHub;

// Amedo 2026-11-12
public sealed class ProjectsPage
{
    private readonly ImGuiFileBrowser _browser;
    private readonly NewProjectDialog _newProject;
    private List<ProjectInfo> _projects = new();
    private string _search = "";
    private string _status = "";
    private bool _loaded;

    public ProjectsPage(ImGuiFileBrowser browser)
    {
        _browser = browser;
        _newProject = new NewProjectDialog(browser, s => _status = s);
    }

    public bool DrawsBrowser => _newProject.IsOpen;

    public void Refresh()
    {
        _projects = RecentProjects.Get().Select(ProjectInfo.Read).OfType<ProjectInfo>().ToList();
        _loaded = true;
    }

    public void Draw()
    {
        if (!_loaded) Refresh();
        Header();
        HubUi.Gap(6f);
        ImGui.SetNextItemWidth(320f);
        ImGui.InputTextWithHint("##search", "Search...", ref _search, 128);
        if (_status.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(_status);
        }
        HubUi.Gap(6f);
        Table();
        _newProject.Draw();
    }

    private void Header()
    {
        HubUi.Title("Projects");
        float right = ImGui.GetContentRegionAvail().X;
        ImGui.SameLine(Math.Max(200f, right - 230f));
        if (ImGui.Button("Add", new Vector2(90f, 30f)))
            _browser.OpenFile("Add a project (.tson)", "CrashEngine Project\0*.tson\0All Files\0*.*\0\0", path =>
            {
                if (path is null) return;
                if (ProjectInfo.Read(path) is { } info) { RecentProjects.Add(info.Folder); Refresh(); _status = $"Added {info.Name}."; }
                else _status = "That is not a CrashEngine project.";
            });
        ImGui.SameLine();
        if (HubUi.PrimaryButton("New project", new Vector2(130f, 30f))) _newProject.Open();
    }

    private void Table()
    {
        if (_projects.Count == 0)
        {
            ImGui.TextDisabled("No projects yet. Press \"New project\" to make one from your disc, or \"Add\" to list one you already have.");
            return;
        }
        var rows = _projects.Where(p => _search.Length == 0
            || p.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)
            || p.Folder.Contains(_search, StringComparison.OrdinalIgnoreCase)).ToList();
        var flags = ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp; // Amedo 2026-11-14
        if (!ImGui.BeginTable("##projects", 4, flags, new Vector2(-28f, -20f))) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("NAME", ImGuiTableColumnFlags.WidthStretch, 4f);
        ImGui.TableSetupColumn("DISC", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("MODIFIED", ImGuiTableColumnFlags.WidthStretch, 1.4f);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 40f);
        ImGui.TableHeadersRow();
        foreach (var p in rows)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, 46f);
            ImGui.TableNextColumn();
            if (ImGui.Selectable($"##{p.Folder}", false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap, new Vector2(0f, 42f))) OpenProject(p);
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.TextUnformatted(p.Completed ? p.Name : p.Name + "  (not finished)");
            ImGui.TextDisabled(p.Folder);
            ImGui.EndGroup();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(p.Region.Length > 0 ? p.Region : "-");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Ago(p.Modified));
            ImGui.TableNextColumn();
            if (ImGui.Button($"...##{p.Folder}")) ImGui.OpenPopup($"menu##{p.Folder}");
            if (ImGui.BeginPopup($"menu##{p.Folder}"))
            {
                if (ImGui.MenuItem("Open")) OpenProject(p);
                if (ImGui.MenuItem("Show in folder")) EditorLauncher.ShowFolder(p.Folder);
                ImGui.Separator();
                if (ImGui.MenuItem("Remove from list")) RemoveProject(p);
                ImGui.EndPopup();
            }
        }
        ImGui.EndTable();
    }

    private void OpenProject(ProjectInfo p)
    {
        _status = EditorLauncher.Start(p.Completed ? "--open" : "--resume", p.Tson) ?? (p.Completed ? $"Opening {p.Name}..." : $"Finishing {p.Name}...");
        RecentProjects.Add(p.Folder);
        Refresh();
    }

    private void RemoveProject(ProjectInfo p)
    {
        RecentProjects.Remove(p.Folder);
        _status = $"Removed {p.Name} from the list. Its folder is still on your disk.";
        Refresh();
    }

    private static string Ago(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalMinutes < 1) return "a few seconds ago";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} minutes ago";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours} hours ago";
        if (d.TotalDays < 30) return $"{(int)d.TotalDays} days ago";
        return t.ToString("yyyy-MM-dd");
    }
}
