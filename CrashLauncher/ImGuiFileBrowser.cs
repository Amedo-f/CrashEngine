using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed class ImGuiFileBrowser
{
    private enum Mode { None, Open, Save }

    private readonly struct Entry
    {
        public readonly string Name;
        public readonly bool IsDir;
        public readonly DateTime Modified;
        public readonly long Size;
        public Entry(string name, bool isDir, DateTime modified, long size)
        { Name = name; IsDir = isDir; Modified = modified; Size = size; }
    }

    private Mode _mode = Mode.None;
    private bool _wantOpenPopup;
    private bool _maximized;
    private string _title = "";
    private Action<string?>? _callback;

    private readonly List<string> _checkedNames = new();

    private string _currentDir = "";
    private readonly List<Entry> _entries = new();
    private string _typedName = "";
    private string _pathEditBuf = "";
    private string _search = "";
    private string? _error;

    private List<(string Desc, string[] Patterns)> _filters = new();
    private int _filterIndex;

    private readonly List<string> _backStack = new();
    private readonly List<string> _forwardStack = new();

    private static string? s_lastDir;
    private static readonly List<string> s_recent = new();

    private const string PopupId = "Browse File##imguifilebrowser";

    public void OpenFile(string title, string filter, Action<string?> onPicked)
    {
        BeginRequest(Mode.Open, title, filter, onPicked);
        _typedName = "";
    }

    public void OpenSave(string title, string filter, string defaultFileName, Action<string?> onPicked)
    {
        BeginRequest(Mode.Save, title, filter, onPicked);
        _typedName = defaultFileName;
    }

    private void BeginRequest(Mode mode, string title, string filter, Action<string?> onPicked)
    {
        _mode = mode;
        _title = title;
        _callback = onPicked;
        _filters = ParseFilter(filter);
        _filterIndex = 0;
        _error = null;
        _search = "";
        _checkedNames.Clear();
        _backStack.Clear();
        _forwardStack.Clear();
        NavigateTo(s_lastDir is not null && Directory.Exists(s_lastDir)
            ? s_lastDir
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), recordHistory: false);
        _wantOpenPopup = true;
    }

    private static List<(string, string[])> ParseFilter(string filter)
    {
        var parts = filter.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<(string, string[])>();
        for (int i = 0; i + 1 < parts.Length; i += 2)
            result.Add((parts[i], parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries)));
        if (result.Count == 0) result.Add(("All Files", new[] { "*.*" }));
        return result;
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern == "*.*" || pattern == "*") return true;
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
            return fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesCurrentFilter(string fileName) =>
        _filters[_filterIndex].Patterns.Any(p => MatchesPattern(fileName, p));

    private void NavigateTo(string dir, bool recordHistory = true)
    {
        try
        {
            dir = Path.GetFullPath(dir);
            if (!Directory.Exists(dir)) { _error = $"Folder not found: {dir}"; return; }

            if (recordHistory && _currentDir.Length > 0 && _currentDir != dir)
            {
                _backStack.Add(_currentDir);
                _forwardStack.Clear();
            }

            _currentDir = dir;
            _pathEditBuf = dir;
            _search = "";
            _checkedNames.Clear();
            s_lastDir = dir;
            _error = null;

            s_recent.RemoveAll(d => string.Equals(d, dir, StringComparison.OrdinalIgnoreCase));
            s_recent.Insert(0, dir);
            if (s_recent.Count > 10) s_recent.RemoveAt(s_recent.Count - 1);

            Refresh();
        }
        catch (Exception ex) { _error = ex.Message; }
    }

    private void GoBack()
    {
        if (_backStack.Count == 0) return;
        var target = _backStack[^1];
        _backStack.RemoveAt(_backStack.Count - 1);
        _forwardStack.Add(_currentDir);
        NavigateTo(target, recordHistory: false);
    }

    private void GoForward()
    {
        if (_forwardStack.Count == 0) return;
        var target = _forwardStack[^1];
        _forwardStack.RemoveAt(_forwardStack.Count - 1);
        _backStack.Add(_currentDir);
        NavigateTo(target, recordHistory: false);
    }

    private void Refresh()
    {
        _entries.Clear();
        try
        {
            var dirs = Directory.GetDirectories(_currentDir)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            foreach (var d in dirs)
            {
                DateTime mod = default;
                try { mod = Directory.GetLastWriteTime(d); } catch {  }
                _entries.Add(new Entry(Path.GetFileName(d) ?? d, true, mod, -1));
            }

            var files = Directory.GetFiles(_currentDir)
                .Where(f => MatchesCurrentFilter(Path.GetFileName(f)))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                DateTime mod = default; long size = 0;
                try { var fi = new FileInfo(f); mod = fi.LastWriteTime; size = fi.Length; } catch { }
                _entries.Add(new Entry(Path.GetFileName(f), false, mod, size));
            }
        }
        catch (Exception ex) { _error = ex.Message; }
    }

    private string CandidatePath => Path.Combine(_currentDir, _typedName);

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(_typedName)) return;
        string path = CandidatePath;

        if (_mode == Mode.Open)
        {
            if (!File.Exists(path)) { _error = "That file doesn't exist."; return; }
        }
        else
        {
            if (!Path.HasExtension(path))
            {
                var firstPattern = _filters[_filterIndex].Patterns.FirstOrDefault();
                if (firstPattern is not null && firstPattern.StartsWith("*.", StringComparison.Ordinal))
                    path += firstPattern[1..];
            }
        }

        var cb = _callback;
        Close();
        cb?.Invoke(path);
    }

    private void Cancel()
    {
        var cb = _callback;
        Close();
        cb?.Invoke(null);
    }

    private void Close()
    {
        _mode = Mode.None;
        _callback = null;
        if (ImGui.IsPopupOpen(PopupId)) ImGui.CloseCurrentPopup();
    }

    public void Draw()
    {
        if (_wantOpenPopup) { ImGui.OpenPopup(PopupId); _wantOpenPopup = false; }
        if (_mode == Mode.None) return;

        ImGui.SetNextWindowSize(new Vector2(900f, 560f), ImGuiCond.FirstUseEver);
        if (_maximized)
        {
            var vp = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(vp.Pos);
            ImGui.SetNextWindowSize(vp.Size);
        }
        bool open = true;
        if (ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.TextDisabled(_title);
            ImGui.SameLine(ImGui.GetWindowWidth() - 90f);
            if (ImGui.Button(_maximized ? "Restore" : "Maximize", new Vector2(80f, 0f)))
                _maximized = !_maximized;
            ImGui.Separator();
            DrawToolbar();
            ImGui.Spacing();

            ImGui.BeginChild("##sidebar", new Vector2(190f, -58f), ImGuiChildFlags.Border);
            DrawSidebar();
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("##mainpane", new Vector2(0f, -58f), ImGuiChildFlags.None);
            DrawEntryTable();
            ImGui.EndChild();

            DrawFooter();
            ImGui.EndPopup();
        }
        if (!open) Cancel();
    }

    private void DrawToolbar()
    {
        ImGui.BeginDisabled(_backStack.Count == 0);
        if (ImGui.Button("<-")) GoBack();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(_forwardStack.Count == 0);
        if (ImGui.Button("->")) GoForward();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Up"))
        {
            var parent = Directory.GetParent(_currentDir);
            if (parent is not null) NavigateTo(parent.FullName);
        }
        ImGui.SameLine();
        if (ImGui.Button("Refresh")) Refresh();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(MathF.Max(120f, ImGui.GetContentRegionAvail().X - 260f));
        if (ImGui.InputText("##pathedit", ref _pathEditBuf, 512, ImGuiInputTextFlags.EnterReturnsTrue))
            NavigateTo(_pathEditBuf);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f);
        ImGui.InputTextWithHint("##search", "Search this folder...", ref _search, 128);
    }

    private void DrawSidebar()
    {
        ImGui.TextDisabled("Quick Access");
        DrawSidebarEntry("Home",      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        DrawSidebarEntry("Desktop",   Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        DrawSidebarEntry("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        DrawSidebarEntry("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        DrawSidebarEntry("Pictures",  Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        DrawSidebarEntry("Music",     Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        DrawSidebarEntry("Videos",    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

        ImGui.Spacing();
        ImGui.TextDisabled("Drives");
        string? driveTarget = null;
        try
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
                if (ImGui.Selectable(d.RootDirectory.FullName))
                    driveTarget = d.RootDirectory.FullName;
        }
        catch {  }
        if (driveTarget is not null) NavigateTo(driveTarget);

        if (s_recent.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Recent");
            string? recentTarget = null;
            foreach (var dir in s_recent.Where(d => !string.Equals(d, _currentDir, StringComparison.OrdinalIgnoreCase)))
            {
                var label = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(label)) label = dir;
                if (ImGui.Selectable(label))
                    recentTarget = dir;
                if (ImGui.IsItemHovered()) MaybeTooltip(dir);
            }
            if (recentTarget is not null) NavigateTo(recentTarget);
        }
    }

    private void DrawSidebarEntry(string label, string path)
    {
        if (!Directory.Exists(path)) return;
        if (ImGui.Selectable(label, string.Equals(_currentDir, path, StringComparison.OrdinalIgnoreCase)))
            NavigateTo(path);
    }

    private static void MaybeTooltip(string text)
    {
        if (ImGui.BeginTooltip()) { ImGui.TextUnformatted(text); ImGui.EndTooltip(); }
    }

    private static readonly Vector2 IconSize = new(18f, 14f);

    private static void DrawFolderIcon()
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float w = IconSize.X, h = IconSize.Y;
        uint fill   = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.78f, 0.25f, 1f));
        uint border = ImGui.ColorConvertFloat4ToU32(new Vector4(0.75f, 0.55f, 0.1f, 1f));

        dl.AddRectFilled(pos + new Vector2(0f, 0f), pos + new Vector2(w * 0.55f, h * 0.28f), fill, 1f);
        var bodyMin = pos + new Vector2(0f, h * 0.22f);
        var bodyMax = pos + new Vector2(w, h);
        dl.AddRectFilled(bodyMin, bodyMax, fill, 1.5f);
        dl.AddRect(bodyMin, bodyMax, border, 1.5f);

        ImGui.Dummy(IconSize);
    }

    private void DrawEntryTable()
    {
        string? navigateTarget = null;
        bool confirmRequested = false;

        bool showCheckboxes = _mode == Mode.Open;
        int columnCount = showCheckboxes ? 4 : 3;

        var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY |
                    ImGuiTableFlags.Resizable;
        if (ImGui.BeginTable("##entrytable", columnCount, flags, ImGui.GetContentRegionAvail()))
        {
            if (showCheckboxes)
                ImGui.TableSetupColumn("##check", ImGuiTableColumnFlags.WidthFixed, 26f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 3f);
            ImGui.TableSetupColumn("Date Modified", ImGuiTableColumnFlags.WidthFixed, 150f);
            ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 80f);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            foreach (var entry in _entries)
            {
                if (_search.Length > 0 && entry.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                ImGui.TableNextRow();
                int col = 0;

                if (showCheckboxes)
                {
                    ImGui.TableSetColumnIndex(col++);
                    if (!entry.IsDir)
                    {
                        bool isChecked = _checkedNames.Contains(entry.Name);
                        ImGui.PushID(entry.Name);
                        if (ImGui.Checkbox("##chk", ref isChecked))
                        {
                            if (isChecked)
                            {
                                _checkedNames.Remove(entry.Name);
                                _checkedNames.Add(entry.Name);
                                _typedName = entry.Name;
                            }
                            else
                            {
                                _checkedNames.Remove(entry.Name);
                                if (_typedName == entry.Name)
                                    _typedName = _checkedNames.Count > 0 ? _checkedNames[^1] : "";
                            }
                        }
                        ImGui.PopID();
                    }
                }

                ImGui.TableSetColumnIndex(col++);
                if (entry.IsDir) DrawFolderIcon();
                else             ImGui.Dummy(IconSize);
                ImGui.SameLine();

                bool selected = !entry.IsDir && (_checkedNames.Contains(entry.Name) || _typedName == entry.Name);
                if (ImGui.Selectable(entry.Name, selected, ImGuiSelectableFlags.SpanAllColumns))
                {
                    if (!entry.IsDir) _typedName = entry.Name;
                }
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    if (entry.IsDir) navigateTarget = Path.Combine(_currentDir, entry.Name);
                    else { _typedName = entry.Name; confirmRequested = true; }
                }

                ImGui.TableSetColumnIndex(col++);
                ImGui.TextDisabled(entry.Modified == default ? "-" : entry.Modified.ToString("yyyy-MM-dd HH:mm"));

                ImGui.TableSetColumnIndex(col);
                ImGui.TextDisabled(entry.IsDir ? "-" : FormatSize(entry.Size));
            }
            ImGui.EndTable();
        }

        if (navigateTarget is not null) NavigateTo(navigateTarget);
        else if (confirmRequested) Confirm();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
    }

    private void DrawFooter()
    {
        if (_error is not null)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _error);

        if (_checkedNames.Count > 1)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f),
                $"{_checkedNames.Count} files checked — only the last one checked ('{_typedName}') " +
                "will be used (multi-file import isn't wired up here yet).");

        float filterW = 220f;
        float buttonsW = 220f;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - filterW - buttonsW - 16f);
        ImGui.InputTextWithHint("##filebrowsername", "File name", ref _typedName, 260);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(filterW);
        var currentDesc = _filters[_filterIndex].Desc;
        if (ImGui.BeginCombo("##filebrowserfilter", currentDesc))
        {
            for (int i = 0; i < _filters.Count; i++)
                if (ImGui.Selectable(_filters[i].Desc, i == _filterIndex))
                { _filterIndex = i; Refresh(); }
            ImGui.EndCombo();
        }

        bool canConfirm = _mode == Mode.Save
            ? !string.IsNullOrWhiteSpace(_typedName)
            : !string.IsNullOrWhiteSpace(_typedName) && File.Exists(CandidatePath);

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.45f, 0.85f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.28f, 0.53f, 0.93f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.38f, 0.75f, 1f));
        ImGui.BeginDisabled(!canConfirm);
        if (ImGui.Button(_mode == Mode.Save ? "Save" : "Open", new Vector2(100f, 0f)))
            Confirm();
        ImGui.EndDisabled();
        ImGui.PopStyleColor(3);
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(100f, 0f)))
            Cancel();
    }
}
