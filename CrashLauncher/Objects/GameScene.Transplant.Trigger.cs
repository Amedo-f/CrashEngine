using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private bool                       _showTriggerTransplantLevelPicker;
    private string                     _triggerTransplantLevelFilter = "";
    private bool                       _showTriggerTransplantPicker;
    private string                     _triggerTransplantSelectedLevel = "";
    private string                     _triggerTransplantFilter = "";
    private List<(uint Id, string Summary)>? _triggerTransplantList;

    private void DrawTriggerTransplantLevelPickerWindow()
    {
        if (!_showTriggerTransplantLevelPicker) return;
        ImGui.SetNextWindowSize(new Vector2(480f, 420f), ImGuiCond.FirstUseEver);
        if (!_windowChrome.Begin("Add Trigger: pick a source level##trigtransplantlevel", ref _showTriggerTransplantLevelPicker)) return;
        ImGui.TextDisabled("Pick the level to copy a trigger from.");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##trigTransplantLevelFilter", "Search levels...", ref _triggerTransplantLevelFilter, 128);
        ImGui.BeginChild("##trigTransplantLevelList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var lvl in GetSwapLevelList())
        {
            if (!string.IsNullOrWhiteSpace(_triggerTransplantLevelFilter) &&
                !lvl.Contains(_triggerTransplantLevelFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable(lvl))
            {
                _triggerTransplantSelectedLevel = lvl;
                _triggerTransplantList = LoadTriggerListForLevel(lvl);
                _showTriggerTransplantLevelPicker = false;
                _showTriggerTransplantPicker = true;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private List<(uint Id, string Summary)> LoadTriggerListForLevel(string levelPath)
    {
        var result = new List<(uint, string)>();
        try
        {
            var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? levelPath[..^4] : levelPath;
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2");
            if (stream is null) return result;
            var srcRm2 = new PS2AnyTwinsanityRM2();
            using (var reader = new BinaryReader(stream)) srcRm2.Read(reader, (int)stream.Length);
            for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = srcRm2.GetItem<BaseTwinSection>((uint)lid);
                var trigSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
                if (trigSec is null) continue;
                for (int i = 0; i < trigSec.GetItemsAmount(); i++)
                {
                    if (trigSec.GetItem(i) is not PS2AnyTrigger t) continue;
                    var msgs = string.Join(",", t.TriggerMessages.Where(m => m != 0));
                    var instRefs = t.Trigger.Instances.Count > 0 ? $" watches:{t.Trigger.Instances.Count} inst" : "";
                    result.Add((t.GetID(), $"msgs:[{(msgs.Length > 0 ? msgs : "none")}]{instRefs}"));
                }
            }
        }
        catch (Exception ex) { _browser.Log($"Add Trigger: failed to list triggers in {levelPath}: {ex.Message}"); }
        return result;
    }

    private void DrawTriggerTransplantPickerWindow()
    {
        if (!_showTriggerTransplantPicker) return;
        ImGui.SetNextWindowSize(new Vector2(560f, 460f), ImGuiCond.FirstUseEver);
        if (!_windowChrome.Begin($"Add Trigger: pick a trigger from {_triggerTransplantSelectedLevel}##trigtransplantpick", ref _showTriggerTransplantPicker)) return;
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##trigTransplantFilter", "Search by hex id or message id...", ref _triggerTransplantFilter, 128);
        ImGui.BeginChild("##trigTransplantList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        foreach (var (id, summary) in _triggerTransplantList ?? Enumerable.Empty<(uint, string)>())
        {
            if (!string.IsNullOrWhiteSpace(_triggerTransplantFilter) &&
                !summary.Contains(_triggerTransplantFilter, StringComparison.OrdinalIgnoreCase) &&
                !$"{id:X4}".Contains(_triggerTransplantFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ImGui.Selectable($"Trigger 0x{id:X4}  {summary}"))
            {
                AddTriggerTransplant(_triggerTransplantSelectedLevel, id);
                _showTriggerTransplantPicker = false;
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }

    private void AddTriggerTransplant(string sourceLevelPath, uint triggerId)
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkRoot is null || chunkSource?.Rm2 is null) { _browser.Log("Add Trigger: no level currently loaded."); return; }

        var basePath = sourceLevelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? sourceLevelPath[..^4] : sourceLevelPath;

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            using var stream = pkg.OpenByPath($"{basePath}.rm2")
                ?? throw new FileNotFoundException($"'{basePath}.rm2' not found in the package.");
            var sourceRm2 = new PS2AnyTwinsanityRM2();
            using (var reader = new BinaryReader(stream))
                sourceRm2.Read(reader, (int)stream.Length);

            PS2AnyTrigger? srcTrig = null;
            for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION && srcTrig is null; lid++)
            {
                var layout = sourceRm2.GetItem<BaseTwinSection>((uint)lid);
                var trigSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
                if (trigSec is null) continue;
                for (int i = 0; i < trigSec.GetItemsAmount(); i++)
                    if (trigSec.GetItem(i) is PS2AnyTrigger cand && cand.GetID() == triggerId) { srcTrig = cand; break; }
            }
            if (srcTrig is null) { _browser.Log($"Add Trigger: trigger 0x{triggerId:X4} not found in {sourceLevelPath}."); return; }

            BaseTwinSection? destTrigSection = null;
            BaseTwinSection? fallbackLayout = null;
            for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)lid);
                if (layout is null) continue;
                if (layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION) is not null)
                    fallbackLayout = layout;
                var trigSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
                if (trigSec is not null) { destTrigSection = trigSec; break; }
            }
            if (destTrigSection is null)
            {
                if (fallbackLayout is null) { _browser.Log("Add Trigger: this level has no layout section to add to."); return; }
                destTrigSection = new BaseTwinSection();
                destTrigSection.SetID((uint)TwinConstants.LAYOUT_TRIGGERS_SECTION);
                fallbackLayout.AddItem(destTrigSection);
                _browser.Log("Add Trigger: this level had no Triggers section at all — created one.");
            }

            var spawnPos = _camera?.Transform.Position ?? Vector3.Zero;
            var newTrig = new PS2AnyTrigger();
            newTrig.Trigger.Header              = srcTrig.Trigger.Header;
            newTrig.Trigger.ObjectActivatorMask = srcTrig.Trigger.ObjectActivatorMask;
            newTrig.Trigger.UnkFloat            = srcTrig.Trigger.UnkFloat;
            newTrig.Trigger.Rotation            = new TwinVec4(srcTrig.Trigger.Rotation.X, srcTrig.Trigger.Rotation.Y, srcTrig.Trigger.Rotation.Z, srcTrig.Trigger.Rotation.W);
            newTrig.Trigger.Scale               = new TwinVec4(srcTrig.Trigger.Scale.X, srcTrig.Trigger.Scale.Y, srcTrig.Trigger.Scale.Z, srcTrig.Trigger.Scale.W);
            newTrig.Trigger.Position            = new TwinVec4(spawnPos.X, spawnPos.Y, spawnPos.Z, 1f);
            newTrig.Trigger.InstanceExtensionValue = srcTrig.Trigger.InstanceExtensionValue;
            for (int i = 0; i < 4; i++) newTrig.TriggerMessages[i] = srcTrig.TriggerMessages[i];

            bool remapped = _lastAddedInstanceId.HasValue;
            if (remapped)
                newTrig.Trigger.Instances.Add(_lastAddedInstanceId!.Value);
            else
                foreach (var iid in srcTrig.Trigger.Instances) newTrig.Trigger.Instances.Add(iid);

            newTrig.SetID(GenerateUniqueInstanceId(destTrigSection));
            destTrigSection.AddItem(newTrig);

            if (_triggersRoot is not null)
            {
                var e = new Entity($"Trigger_{newTrig.GetID():X4}");
                e.Transform.Position = new Vector3(newTrig.Trigger.Position.X, newTrig.Trigger.Position.Y, newTrig.Trigger.Position.Z);
                e.Transform.Scale    = new Vector3(MathF.Max(newTrig.Trigger.Scale.X, 0.05f), MathF.Max(newTrig.Trigger.Scale.Y, 0.05f), MathF.Max(newTrig.Trigger.Scale.Z, 0.05f));
                var rdr = e.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = new Vector4(0.55f, 0.8f, 1f, 0.35f) });
                rdr.Mat.AlphaBlend = true;
                e.Add(new CrashEngine.Importer.TriggerMarker { Source = newTrig });
                _triggersRoot.AddChild(e);
                _triggersRoot.Active = _showTriggers;
                SelectClicked(e, false);
            }

            var msgList = string.Join(",", newTrig.TriggerMessages.Where(m => m != 0));
            _browser.Log($"Add Trigger: copied trigger 0x{triggerId:X4} from {sourceLevelPath} → new id 0x{newTrig.GetID():X4}, " +
                         $"placed at the camera position, messages:[{(msgList.Length > 0 ? msgList : "none")}]. " +
                         (remapped
                             ? $"Instances list points at the object Add Object placed most recently (id 0x{_lastAddedInstanceId:X4})."
                             : "WARNING: no object was added via \"Add Object\" this session (or the link was lost) — " +
                               "the Instances list was copied AS-IS from the source level and almost certainly points " +
                               "at the wrong (or nonexistent) instance here. Use Add Object first, then Add Trigger.") +
                         " Save Chunk to keep it, then Build ISO + test.");
        }
        catch (Exception ex) { _browser.Log($"Add Trigger failed: {ex.Message}"); }
    }
}
