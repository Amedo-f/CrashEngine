using System.Numerics;
using CrashEngine.Install;
using CrashLauncher;
using ImGuiNET;

namespace CrashEngineHub;

// Amedo 2026-11-12
public sealed class NewProjectDialog
{
    private readonly ImGuiFileBrowser _browser;
    private readonly Action<string> _status;
    private bool _open, _wantOpen;
    private string _name = "New project";
    private string _location = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrashEngineProjects");
    private string _disc = "";
    private string _discNote = "";
    private string _error = "";

    public NewProjectDialog(ImGuiFileBrowser browser, Action<string> status)
    {
        _browser = browser;
        _status = status;
    }

    public bool IsOpen => _open;

    public void Open()
    {
        _open = _wantOpen = true;
        _error = "";
    }

    public void Draw()
    {
        if (_wantOpen) { ImGui.OpenPopup("New project"); _wantOpen = false; }
        ImGui.SetNextWindowSize(new Vector2(620f, 0f), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal("New project", ref _open, ImGuiWindowFlags.AlwaysAutoResize)) return;
        Row("Name", ref _name, null);
        Row("Location", ref _location, () => _browser.OpenFolder("Where the project folder goes", _location, p => { if (p is not null) _location = p; }));
        Row("Disc", ref _disc, () => _browser.OpenFile("Your Crash Twinsanity disc (.iso)", HubUi.DiscFilter, p => { if (p is not null) { _disc = p; CheckDisc(); } }));
        if (_discNote.Length > 0) ImGui.TextDisabled(_discNote);
        if (_error.Length > 0) HubUi.Error(_error);
        HubUi.Gap(8f);
        if (ImGui.Button("Create", new Vector2(140f, 30f))) Create();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(140f, 30f))) { _open = false; ImGui.CloseCurrentPopup(); }
        _browser.Draw();
        ImGui.EndPopup();
    }

    private static void Row(string label, ref string value, Action? browse)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(browse is null ? 590f : 510f);
        ImGui.InputText($"##{label}", ref value, 512);
        if (browse is null) return;
        ImGui.SameLine();
        if (ImGui.Button($"Browse##{label}", new Vector2(72f, 0f))) browse();
    }

    private void CheckDisc() => _discNote = File.Exists(_disc) ? GameDiscs.Identify(_disc).Message : "";

    private void Create()
    {
        string name = _name.Trim(), location = _location.Trim(), disc = _disc.Trim();
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { _error = "Give the project a name that can be a folder name."; return; }
        if (location.Length == 0) { _error = "Choose where the project goes."; return; }
        bool iso = File.Exists(disc) && disc.EndsWith(".iso", StringComparison.OrdinalIgnoreCase);
        bool folder = Directory.Exists(disc) && File.Exists(Path.Combine(disc, "SYSTEM.CNF"));
        if (!iso && !folder) { _error = "Choose your disc: a .iso file, or a folder with the disc's files."; return; }
        if (Directory.Exists(Path.Combine(location, name))) { _error = "A folder with that name is already there."; return; }
        var error = EditorLauncher.Start("--create", disc, location, name);
        if (error is not null) { _error = error; return; }
        _status($"Creating {name} in the editor...");
        _open = false;
        ImGui.CloseCurrentPopup();
    }
}
