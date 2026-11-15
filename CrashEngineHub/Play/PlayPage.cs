using System.Numerics;
using CrashEngine.Install;
using CrashEngine.Runtime;
using CrashLauncher;
using ImGuiNET;

namespace CrashEngineHub;

// Amedo 2026-11-12
public sealed class PlayPage
{
    private readonly ImGuiFileBrowser _browser;
    private readonly PlayState _state = PlayState.Load();
    private List<ModCard> _mods;
    private int _selected;
    private string _status = "";
    private Task? _applyTask;
    private ModCard? _applyMod;
    private volatile string _busy = "";
    private volatile float _progress = -1f;
    private ModCard? _doomed;
    private bool _confirmForeign;

    public PlayPage(ImGuiFileBrowser browser)
    {
        _browser = browser;
        _mods = Mods.List(PlayState.ModsFolder);
        _selected = _state.Applied is { Length: > 0 } a ? _mods.FindIndex(m => m.Name == a) : -1;
    }

    private GameDiscs.Disc? Disc => GameDiscs.Known.FirstOrDefault(d => d.Id == _state.DiscId);
    private bool Busy => _applyTask is not null;

    public void Draw()
    {
        FinishApply();
        HubUi.Title("Play");
        ImGui.TextDisabled("Install Crash Twinsanity on this PC from your own disc, with or without a mod.");
        HubUi.Gap(10f);
        ImGui.BeginDisabled(Busy);
        DiscRow();
        PathRow("INSTALL", "Where the game goes (a \"Crash Twinsanity\" folder is made inside)", _state.InstallFolder, p => _state.InstallFolder = p);
        PathRow("SHORTCUT", "Where the shortcut goes", _state.ShortcutFolder, p => _state.ShortcutFolder = p);
        HubUi.Gap(6f);
        Games();
        HubUi.Gap(10f);
        Buttons();
        ImGui.EndDisabled();
        Progress();
        ConfirmRemove();
        ConfirmForeign();
    }

    private void DiscRow()
    {
        HubUi.Label("DISC");
        if (Disc is { } disc)
        {
            ImGui.TextUnformatted($"Crash Twinsanity ({disc.Region})");
            ImGui.SameLine();
            if (ImGui.Button("Change Disc...")) ChooseDisc();
            ImGui.SetCursorPosX(HubUi.LabelWidth);
            ImGui.TextDisabled(_state.Iso);
            if (disc.Id == "EU") LanguageRow();
        }
        else if (ImGui.Button("Choose Disc...", new Vector2(160f, 0f))) ChooseDisc();
        HubUi.Gap(4f);
    }

    private void LanguageRow()
    {
        HubUi.Label("LANGUAGE");
        string device = $"Device language ({CrashEngine.Runtime.Region.Pal.Language.DiscLanguage(null)})";
        ImGui.SetNextItemWidth(300f);
        if (!ImGui.BeginCombo("##language", _state.Language ?? device)) return;
        if (ImGui.Selectable(device, _state.Language is null)) { _state.Language = null; _state.Save(); }
        foreach (var l in PlaySettings.PalLanguages)
            if (ImGui.Selectable(l, _state.Language == l)) { _state.Language = l; _state.Save(); }
        ImGui.EndCombo();
    }

    private void PathRow(string label, string title, string value, Action<string> set)
    {
        HubUi.Label(label);
        ImGui.TextUnformatted(value);
        ImGui.SameLine();
        if (ImGui.Button($"Browse...##{label}")) _browser.OpenFolder(title, value, p => { if (p is null) return; set(p); _state.Save(); });
        HubUi.Gap(4f);
    }

    private void Games()
    {
        HubUi.Label("GAMES");
        ImGui.BeginGroup();
        if (ImGui.RadioButton("Crash Twinsanity (original)", _selected < 0)) _selected = -1;
        var disc = Disc;
        for (int i = 0; i < _mods.Count; i++)
        {
            var m = _mods[i];
            bool fits = m.Region.Length == 0 || disc is null || m.Region == disc.Id;
            ImGui.BeginDisabled(!fits);
            if (ImGui.RadioButton($"{m.Name}##mod{i}", _selected == i)) _selected = i;
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextDisabled(fits ? (m.Region.Length > 0 ? $"mod ({m.Region})" : "mod") : $"needs the {m.Region} disc");
        }
        ImGui.EndGroup();
    }

    private void Buttons()
    {
        ImGui.SetCursorPosX(HubUi.LabelWidth);
        if (HubUi.PrimaryButton("Apply", new Vector2(120f, 32f))) Apply();
        ImGui.SameLine();
        if (ImGui.Button("Add Mod...", new Vector2(110f, 32f))) AddMod();
        if (_selected >= 0 && _selected < _mods.Count)
        {
            ImGui.SameLine();
            if (ImGui.Button("Remove Mod...", new Vector2(130f, 32f))) _doomed = _mods[_selected];
        }
        if (_status.Length > 0)
        {
            ImGui.SetCursorPosX(HubUi.LabelWidth);
            ImGui.TextWrapped(_status);
        }
    }

    private void Progress()
    {
        if (!Busy) return;
        HubUi.Gap(8f);
        ImGui.SetCursorPosX(HubUi.LabelWidth);
        ImGui.TextUnformatted(_busy);
        ImGui.SetCursorPosX(HubUi.LabelWidth);
        float p = _progress;
        ImGui.ProgressBar(p < 0f ? 0f : p, new Vector2(Math.Max(200f, ImGui.GetContentRegionAvail().X - 40f), 0f), p < 0f ? "" : $"{p * 100f:0}%");
    }

    private void ChooseDisc() =>
        _browser.OpenFile("Your Crash Twinsanity disc (.iso)", HubUi.DiscFilter, path =>
        {
            if (path is null) return;
            var (disc, message) = GameDiscs.Identify(path);
            _status = message;
            if (disc is null) return;
            _state.Iso = path;
            _state.DiscId = disc.Id;
            _state.Save();
        });

    private void AddMod() =>
        _browser.OpenFile("Choose a mod (.cemod)", "CrashEngine mod\0*.cemod\0All Files\0*.*\0\0", path =>
        {
            if (path is null) return;
            try
            {
                ModCard card;
                using (var file = File.OpenRead(path)) card = Mods.Add(file, PlayState.ModsFolder);
                _mods = Mods.List(PlayState.ModsFolder);
                _selected = _mods.FindIndex(m => m.File == card.File);
                _status = $"Added {card.Name}.";
            }
            catch (Exception ex) { _status = ex.Message; }
        });

    private void Apply(bool force = false)
    {
        if (Disc is not { } disc || !File.Exists(_state.Iso)) { _status = "Choose your disc first."; return; }
        if (PlayInstall.CheckSpace(_state.Iso, _state.InstallFolder) is { } space) { _status = space; return; }
        if (!force && PlayInstall.ForeignFolder(_state.InstallFolder)) { _confirmForeign = true; return; }
        _applyMod = _selected >= 0 && _selected < _mods.Count ? _mods[_selected] : null;
        var request = new PlayInstall.Request(_state.Iso, disc, _state.InstallFolder, _state.ShortcutFolder, _state.Language, _applyMod);
        _status = "";
        _busy = "Starting...";
        _progress = -1f;
        _applyTask = Task.Run(() => PlayInstall.Run(request, (s, p) => { _busy = s; _progress = p; }));
    }

    private void FinishApply()
    {
        if (_applyTask is not { IsCompleted: true } task) return;
        _applyTask = null;
        if (task.Exception?.InnerException is { } ex)
        {
            _status = $"Apply failed: {ex.Message}";
            return;
        }
        _state.Applied = _applyMod?.Name ?? "";
        _state.Save();
        _status = $"Done. {_applyMod?.Name ?? PlayInstall.GameName} is ready: start it from the shortcut.";
    }

    private void ConfirmForeign()
    {
        if (HubUi.Confirm("Folder in use", ref _confirmForeign, "Install anyway", () =>
            {
                ImGui.TextUnformatted($"\"{PlayInstall.Target(_state.InstallFolder)}\" already has files that the Hub did not put there");
                ImGui.TextUnformatted("(for example an older Build to PC). Installing here mixes them.");
                ImGui.TextDisabled("Choose another INSTALL folder, or install over it anyway (saves are kept).");
            }, actionWidth: 150f))
            Apply(force: true);
    }

    private void ConfirmRemove()
    {
        bool open = _doomed is not null;
        if (HubUi.Confirm("Remove mod?", ref open, "Remove", () =>
            {
                ImGui.TextUnformatted($"Remove the mod \"{_doomed!.Name}\" from the list?");
                ImGui.TextDisabled("If it is the one installed, the game keeps it until you apply something else.");
            }))
        {
            Mods.Remove(_doomed!);
            _mods = Mods.List(PlayState.ModsFolder);
            _selected = -1;
        }
        if (!open) _doomed = null;
    }
}
