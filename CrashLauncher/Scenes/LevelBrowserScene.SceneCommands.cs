using CrashEngine.Core;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-12
public sealed partial class LevelBrowserScene
{
    public const string NewGameHelp =
        "Toggles this whole PROJECT into/out of \"New Game\" mode. When ON,\n" +
        "Build ISO drops every original level's own archive record UNLESS\n" +
        "you've claimed its slot via \"Add Scene...\" (right-click a group\n" +
        "below) -- the real starting level (beach) is never dropped, even\n" +
        "unclaimed, since the shipped game boots straight into it.";

    public const string NewSceneHelp =
        "Creates an EMPTY scene under Levels\\Custom\\ -- a clone of the beach\n" +
        "stripped to just Crash + skydome + lighting + music + sound manager,\n" +
        "with no terrain/collision/instances. To make it render as a real\n" +
        "in-game neighbour it still needs claiming onto an existing level slot\n" +
        "(\"Add Scene...\" on a group below) -- a brand-new archive path is\n" +
        "outside the shipped game's fixed-size per-level id table.";

    private string? _pendingLoadPath;
    private (string Rm2, string Donor)? _pendingAddCrash;

    private bool    _wantOpenCreatePopup;
    private string  _newSceneName = "";
    private string? _createError;
    private string  _createTemplatePath = NewSceneTemplatePath;
    private bool    _createBlank = false;

    private string? _movingSourcePath;
    private string? _moveError;

    private string? _renamingPath;
    private string  _renameNewName = "";
    private string? _renameError;
    private bool    _wantOpenRenamePopup;

    private bool    _wantOpenClaimPopup;
    private string  _claimFilter = "";
    private string? _claimError;
    private string  _claimTemplatePath   = TemplateLevelPath;
    private string  _claimTemplateFilter = "";

    public string? MovingScene => _movingSourcePath;
    public string? MoveError => _moveError;

    public string? TakePendingOpen()
    {
        var path = _pendingLoadPath;
        _pendingLoadPath = null;
        return path;
    }

    public void StartNewScene()
    {
        _newSceneName        = "";
        _createError         = null;
        _createBlank         = false;
        _wantOpenCreatePopup = true;
    }

    public void StartRename(string rm2)
    {
        _renamingPath        = rm2;
        _renameNewName       = GamePaths.FileNameWithoutExtension(rm2);
        _renameError         = null;
        _wantOpenRenamePopup = true;
    }

    public void StartClaim()
    {
        _wantOpenClaimPopup = true;
        _claimFilter        = "";
        _claimError         = null;
    }

    public void StartMove(string rm2)
    {
        _movingSourcePath = rm2;
        _moveError        = null;
    }

    public void CancelMove() => _movingSourcePath = null;

    public void MoveInto(string folderRel)
    {
        if (_movingSourcePath is null) return;
        var destRel = Path.Combine(folderRel, Path.GetFileNameWithoutExtension(_movingSourcePath));
        if (MoveSavedScene(_movingSourcePath, destRel, out var err)) _movingSourcePath = null;
        else _moveError = err;
    }

    public void MoveOnto(string rm2)
    {
        if (_movingSourcePath is null) return;
        var destRel = rm2[..^4];
        if (destRel.Equals(_movingSourcePath[..^4], StringComparison.OrdinalIgnoreCase))
            _moveError = "Source and destination are the same path.";
        else if (MoveSavedScene(_movingSourcePath, destRel, out var err)) _movingSourcePath = null;
        else _moveError = err;
    }

    public bool IsClaimed(string rm2) =>
        _project?.ClaimedLevels.Contains(rm2[..^4], StringComparer.OrdinalIgnoreCase) ?? false;

    public void Unclaim(string rm2)
    {
        if (TryUnclaimLevelSlot(rm2[..^4], out var unclaimErr))
        {
            Log($"Unclaimed '{rm2[..^4]}' -- true original content restored from backup.");
            ScanLevels();
        }
        else
            Log($"Unclaim failed: {unclaimErr}");
    }

    public void DrawScenePopups()
    {
        DrawCreatePopup();
        DrawRenamePopup();
        DrawClaimPopup();
    }

    private void DrawCreatePopup()
    {
        if (_wantOpenCreatePopup) { ImGui.OpenPopup("##createScene"); _wantOpenCreatePopup = false; }
        if (!ImGui.BeginPopup("##createScene")) return;
        ImGui.TextUnformatted("New Scene — scene name:");
        ImGui.SetNextItemWidth(220f);
        ImGui.InputText("##newSceneName", ref _newSceneName, 64);

        ImGui.TextColored(new Vector4(0.6f, 0.85f, 1f, 1f),
            "Clones the beach into a TRULY EMPTY canvas: keeps only Crash, the\n" +
            "skydome, world lighting, the DJ music object and the sound manager.\n" +
            "Terrain, collision, particle/visual effects, linked scenery, all other\n" +
            "sounds, and every other instance/enemy, trigger, camera, path and\n" +
            "surface is stripped — you build the scene up yourself from here.");

        if (_createError is not null)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _createError);

        if (ImGui.Button("Create", new Vector2(100f, 0f)))
        {
            if (TryCreateBlankScene(_newSceneName, _createTemplatePath, _createBlank, out var rm2Rel, out var err))
            {
                ScanLevels();
                _pendingLoadPath = rm2Rel;
                _pendingAddCrash = (rm2Rel!, CrashDonorLevelPath);
                ImGui.CloseCurrentPopup();
            }
            else
            {
                _createError = err;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(100f, 0f)))
            ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void DrawRenamePopup()
    {
        if (_wantOpenRenamePopup) { ImGui.OpenPopup("##renameScene"); _wantOpenRenamePopup = false; }
        if (!ImGui.BeginPopup("##renameScene")) return;
        ImGui.TextUnformatted($"Rename {_renamingPath}");
        ImGui.TextDisabled("New name (letters/digits/underscore, same folder, extension implied):");
        ImGui.SetNextItemWidth(240f);
        ImGui.InputText("##renameNewName", ref _renameNewName, 64);
        if (_renameError is not null)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _renameError);

        if (ImGui.Button("Rename", new Vector2(100f, 0f)))
        {
            if (RenameSavedScene(_renamingPath!, _renameNewName, out var err))
                ImGui.CloseCurrentPopup();
            else
                _renameError = err;
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(100f, 0f)))
            ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void DrawClaimPopup()
    {
        if (_wantOpenClaimPopup) { ImGui.OpenPopup("##claimScene"); _wantOpenClaimPopup = false; }
        if (!ImGui.BeginPopup("##claimScene")) return;
        ImGui.TextColored(new Vector4(1f, 0.7f, 0.3f, 1f),
            "Pick a level to CLAIM -- its real content is replaced with a blank scene.");

        ImGui.TextDisabled($"1) Template to clone FROM (currently: {_claimTemplatePath}):");
        ImGui.SetNextItemWidth(320f);
        ImGui.InputTextWithHint("##claimTemplateFilter", "Search template levels...", ref _claimTemplateFilter, 128);
        ImGui.BeginChild("##claimTemplateList", new Vector2(320f, 90f), ImGuiChildFlags.Border);
        foreach (var lvl in GetTemplateLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_claimTemplateFilter) &&
                !lvl.Contains(_claimTemplateFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable(lvl, lvl.Equals(_claimTemplatePath, StringComparison.OrdinalIgnoreCase)))
                _claimTemplatePath = lvl;
        }
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.TextDisabled("2) Real level slot to CLAIM (gets replaced with the template above):");
        ImGui.SetNextItemWidth(320f);
        ImGui.InputTextWithHint("##claimFilter", "Search levels...", ref _claimFilter, 128);
        if (_claimError is not null)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _claimError);
        ImGui.BeginChild("##claimList", new Vector2(320f, 160f), ImGuiChildFlags.Border);
        foreach (var lvl in GetTemplateLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_claimFilter) &&
                !lvl.Contains(_claimFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            bool alreadyClaimed = _project?.ClaimedLevels.Contains(lvl, StringComparer.OrdinalIgnoreCase) ?? false;
            bool isStartingLevel = lvl.Equals(TemplateLevelPath, StringComparison.OrdinalIgnoreCase);
            var label = isStartingLevel ? $"{lvl}  (starting level -- can't claim)"
                      : alreadyClaimed  ? $"{lvl}  (already claimed)"
                      : lvl;
            ImGui.BeginDisabled(isStartingLevel);
            if (ImGui.Selectable(label))
            {
                if (TryClaimLevelSlot(lvl, _claimTemplatePath, out var err))
                {
                    Log($"Claimed '{lvl}' for New Game (template: {_claimTemplatePath}). Save Chunk on it not needed -- already written.");
                    ScanLevels();
                    ImGui.CloseCurrentPopup();
                }
                else _claimError = err;
            }
            ImGui.EndDisabled();
        }
        ImGui.EndChild();
        if (ImGui.Button("Cancel", new Vector2(100f, 0f)))
            ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }
}
