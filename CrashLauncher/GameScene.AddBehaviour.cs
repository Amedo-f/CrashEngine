using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using ITwinObject = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private bool   _showAddBehLevelPicker;
    private string _addBehLevelFilter = "";
    private bool   _showAddBehObjectPicker;
    private string _addBehSelectedLevel = "";
    private string _addBehObjectFilter = "";
    private List<(uint Id, string Name, ITwinObject.ObjectType Type)>? _addBehObjectList;
    private bool   _showAddBehListPicker;
    private uint   _addBehTargetObjId;
    private uint   _addBehSourceObjId;
    private string _addBehSourceObjName = "";
    private List<AddBehEntry> _addBehList = new();

    private sealed class AddBehEntry { public uint Id; public bool Sel; public string Label = ""; }

    private void OpenAddBehaviourFromObject(uint targetObjId)
    {
        _addBehTargetObjId = targetObjId;
        _addBehLevelFilter = "";
        _showAddBehLevelPicker = true;
    }

    private void DrawAddBehaviourWindows()
    {
        DrawAddBehLevelPicker();
        DrawAddBehObjectPicker();
        DrawAddBehListPicker();
    }

    private PS2AnyTwinsanityRM2? LoadAddBehSourceRm2(string levelPath)
    {
        try
        {
            var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? levelPath[..^4] : levelPath;
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2");
            if (stream is null) return null;
            var rm2 = new PS2AnyTwinsanityRM2();
            using var reader = new BinaryReader(stream);
            rm2.Read(reader, (int)stream.Length);
            return rm2;
        }
        catch (Exception ex) { _browser.Log($"Add Behaviour: couldn't read {levelPath}: {ex.Message}"); return null; }
    }

    private void DrawAddBehLevelPicker()
    {
        if (!_showAddBehLevelPicker) return;
        ImGui.SetNextWindowSize(new Vector2(480f, 420f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Add Behaviour: pick a source level##addbehlevel", ref _showAddBehLevelPicker)) { ImGui.End(); return; }
        ImGui.TextDisabled("Pick the level that has the object whose behaviours you want to copy.");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##addBehLevelFilter", "Search levels...", ref _addBehLevelFilter, 128);
        ImGui.BeginChild("##addBehLevelList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var lvl in GetSwapLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_addBehLevelFilter) &&
                !lvl.Contains(_addBehLevelFilter, StringComparison.OrdinalIgnoreCase)) continue;
            if (ImGui.Selectable(lvl))
            {
                _addBehSelectedLevel = lvl;
                _addBehObjectList = LoadObjectListForLevel(lvl);
                _addBehObjectFilter = "";
                _showAddBehLevelPicker = false;
                _showAddBehObjectPicker = true;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void DrawAddBehObjectPicker()
    {
        if (!_showAddBehObjectPicker) return;
        ImGui.SetNextWindowSize(new Vector2(560f, 460f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Add Behaviour: pick source object from {_addBehSelectedLevel}##addbehobj", ref _showAddBehObjectPicker)) { ImGui.End(); return; }
        ImGui.TextDisabled("Pick the object to copy behaviours FROM (e.g. an enemy).");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##addBehObjFilter", "Search objects...", ref _addBehObjectFilter, 128);
        ImGui.BeginChild("##addBehObjList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        if (_addBehObjectList is not null)
            foreach (var (id, name, type) in _addBehObjectList)
            {
                var label = $"0x{id:X4}  {name}  [{type}]";
                if (!string.IsNullOrWhiteSpace(_addBehObjectFilter) &&
                    !label.Contains(_addBehObjectFilter, StringComparison.OrdinalIgnoreCase)) continue;
                if (ImGui.Selectable(label))
                {
                    _addBehSourceObjId = id;
                    _addBehSourceObjName = name;
                    var srcRm2 = LoadAddBehSourceRm2(_addBehSelectedLevel);
                    var ids = srcRm2 is null ? new List<uint>() : MeshDecoder.GetObjectBehaviourIds(srcRm2, id);
                    _addBehList = ids.Select(i => new AddBehEntry { Id = i, Sel = true,
                        Label = $"Behaviour 0x{i:X}  ({(i % 2 == 0 ? "Starter" : "Graph")})" }).ToList();
                    _showAddBehObjectPicker = false;
                    _showAddBehListPicker = true;
                }
            }
        ImGui.EndChild();
        ImGui.End();
    }

    private void DrawAddBehListPicker()
    {
        if (!_showAddBehListPicker) return;
        ImGui.SetNextWindowSize(new Vector2(460f, 440f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Add Behaviour: {_addBehSourceObjName} -> object 0x{_addBehTargetObjId:X}##addbehlist", ref _showAddBehListPicker)) { ImGui.End(); return; }

        if (_addBehList.Count == 0)
        {
            ImGui.TextWrapped("This object exposes no behaviours in its BehaviourSlots/RefBehaviours.");
            if (ImGui.Button("Close")) _showAddBehListPicker = false;
            ImGui.End();
            return;
        }

        ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f),
            "Experimental: a behaviour that drives the source object's own animations/sounds\n" +
            "may not work on this object. Save Chunk, test, revert (Reload from Disc) if it breaks.");
        if (ImGui.Button("Select All")) foreach (var b in _addBehList) b.Sel = true;
        ImGui.SameLine();
        if (ImGui.Button("Select None")) foreach (var b in _addBehList) b.Sel = false;

        ImGui.BeginChild("##addBehItems", new Vector2(-1f, -40f), ImGuiChildFlags.Border);
        foreach (var b in _addBehList)
        {
            bool sel = b.Sel;
            if (ImGui.Checkbox($"{b.Label}##b{b.Id}", ref sel)) b.Sel = sel;
        }
        ImGui.EndChild();

        int selCount = _addBehList.Count(b => b.Sel);
        ImGui.BeginDisabled(selCount == 0);
        if (ImGui.Button($"Add {selCount} behaviour(s) to object 0x{_addBehTargetObjId:X}", new Vector2(-1f, 0f)))
        {
            ApplyAddBehaviours();
            _showAddBehListPicker = false;
        }
        ImGui.EndDisabled();
        ImGui.End();
    }

    private void ApplyAddBehaviours()
    {
        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        if (chunkSource?.Rm2 is null) { _browser.Log("Add Behaviour: no level loaded."); return; }
        var srcRm2 = LoadAddBehSourceRm2(_addBehSelectedLevel);
        if (srcRm2 is null) { _browser.Log("Add Behaviour: couldn't reload source level."); return; }

        var selected = _addBehList.Where(b => b.Sel).Select(b => b.Id).ToList();
        var attached = MeshDecoder.TransplantBehaviours(srcRm2, chunkSource.Rm2, _addBehSourceObjId, _addBehTargetObjId, selected);
        _browser.Log($"Add Behaviour: mirrored {attached.Count}/{selected.Count} behaviour(s) from {_addBehSourceObjName} (0x{_addBehSourceObjId:X4}) " +
                     $"onto object 0x{_addBehTargetObjId:X4}, preserving its BehaviourSlots/RefBehaviours split. Save Chunk + Build ISO to test — Reload Level from Disc to revert.");
    }
}
