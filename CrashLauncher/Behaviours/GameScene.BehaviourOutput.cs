using CrashEngine.Importer.AgentLabReadable;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-18
public sealed partial class GameScene
{
    private enum OutputKind { Info, Success, Error }
    private readonly record struct OutputRow(string Text, int Line = 0, int Column = 0, string Code = "", string File = "");
    private OutputKind _behaviourStatusKind;
    private readonly List<OutputRow> _behaviourErrors = new();
    private float _behaviourOutputHeight = 130f;
    private bool _behaviourOutputFromCode;

    private void SetBehaviourStatus(OutputKind kind, string text, IEnumerable<OutputRow>? errors = null)
    {
        _inspectedBehaviourApplyMsg = text;
        _behaviourStatusKind = kind;
        _behaviourOutputFromCode = false;
        _behaviourErrors.Clear();
        if (errors is not null) _behaviourErrors.AddRange(errors);
        ShowBehaviourSquiggles();
    }

    private void ShowBehaviourSquiggles()
    {
        _behaviourCode.Errors.Clear();
        foreach (var e in _behaviourErrors)
            if (e.Line > 0 && e.File.Length == 0) _behaviourCode.Errors.Add(new CodeEditor.CodeError(e.Line - 1, Math.Max(0, e.Column - 1), e.Text));
    }

    private List<OutputRow> ErrorRows(Exception ex)
    {
        if (ex is not ScriptTextException st) return [new OutputRow(ex.Message)];
        int column = st.Column;
        if (column <= 0)
        {
            var lines = _inspectedBehaviourText.Split('\n');
            string line = st.Line >= 1 && st.Line <= lines.Length ? lines[st.Line - 1] : "";
            column = line.Length - line.TrimStart().Length + 1;
        }
        return [new OutputRow(st.Reason, st.Line, column)];
    }

    private static List<OutputRow> ErrorRows(List<BehaviourCompiler.CompileError> errors) =>
        errors.Select(e => new OutputRow(e.Message, e.Line, e.Column, e.Code)).ToList();

    private static string ErrorCount(int n) => n == 1 ? "1 error" : $"{n} errors";

    private void DrawBehaviourOutput(Vector2 size)
    {
        ImGui.BeginChild("##behoutput", size, ImGuiChildFlags.Border);
        ImGui.TextUnformatted("Output");
        if (_behaviourErrors.Count > 0)
        {
            ImGui.SameLine();
            DrawOutputIcon(OutputKind.Error);
            ImGui.SameLine();
            ImGui.TextUnformatted(ErrorCount(_behaviourErrors.Count));
        }
        ImGui.Separator();
        if (string.IsNullOrEmpty(_inspectedBehaviourApplyMsg) && _behaviourErrors.Count == 0)
        {
            ImGui.TextDisabled("Apply checks the script. Errors show here with their line; click one to go to it.");
            ImGui.EndChild();
            return;
        }
        if (ImGui.BeginTable("##behouttable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() + 4f);
            ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Where", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Line 0000, Col 000").X);
            if (!string.IsNullOrEmpty(_inspectedBehaviourApplyMsg))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawOutputIcon(_behaviourStatusKind);
                ImGui.TableNextColumn();
                ImGui.TextWrapped(_inspectedBehaviourApplyMsg);
                ImGui.TableNextColumn();
            }
            for (int i = 0; i < _behaviourErrors.Count; i++)
            {
                var e = _behaviourErrors[i];
                string text = (e.File.Length > 0 ? Path.GetFileName(e.File) + ": " : "") + (e.Code.Length > 0 ? $"{e.Code}: {e.Text}" : e.Text);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawOutputIcon(OutputKind.Error);
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{text}##behout{i}", false, ImGuiSelectableFlags.SpanAllColumns))
                {
                    if (e.File.Length > 0) OpenBehaviourFileInIde(e.File, e.Line, e.Column);
                    else if (e.Line > 0) _behaviourCode.GoTo(e.Line - 1, Math.Max(0, e.Column - 1));
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(e.File.Length > 0 ? $"{text}\n{e.File}\nLine {e.Line}, Col {Math.Max(1, e.Column)} (click to open it in Visual Studio)"
                                   : e.Line > 0 ? $"{text}\nLine {e.Line}, Col {Math.Max(1, e.Column)} (click to go there)" : text);
                ImGui.TableNextColumn();
                ImGui.TextDisabled(e.Line > 0 ? $"Line {e.Line}, Col {Math.Max(1, e.Column)}" : "");
            }
            ImGui.EndTable();
        }
        ImGui.EndChild();
    }

    private static void DrawOutputIcon(OutputKind kind)
    {
        float s = ImGui.GetFontSize();
        var p = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(s, s));
        var draw = ImGui.GetWindowDrawList();
        var c = new Vector2(p.X + s * 0.5f, p.Y + s * 0.5f);
        float r = s * 0.42f;
        uint white = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        switch (kind)
        {
            case OutputKind.Error:
                draw.AddCircleFilled(c, r, ImGui.GetColorU32(new Vector4(0.89f, 0.27f, 0.27f, 1f)));
                float d = r * 0.45f;
                draw.AddLine(c - new Vector2(d, d), c + new Vector2(d, d), white, 1.6f);
                draw.AddLine(c + new Vector2(-d, d), c + new Vector2(d, -d), white, 1.6f);
                break;
            case OutputKind.Success:
                draw.AddCircleFilled(c, r, ImGui.GetColorU32(new Vector4(0.30f, 0.69f, 0.31f, 1f)));
                draw.AddLine(c + new Vector2(-r * 0.5f, 0f), c + new Vector2(-r * 0.12f, r * 0.38f), white, 1.6f);
                draw.AddLine(c + new Vector2(-r * 0.12f, r * 0.38f), c + new Vector2(r * 0.5f, -r * 0.38f), white, 1.6f);
                break;
            default:
                draw.AddCircleFilled(c, r, ImGui.GetColorU32(new Vector4(0.25f, 0.55f, 0.90f, 1f)));
                draw.AddLine(c + new Vector2(0f, -r * 0.05f), c + new Vector2(0f, r * 0.5f), white, 1.6f);
                draw.AddCircleFilled(c + new Vector2(0f, -r * 0.42f), 1.3f, white);
                break;
        }
    }
}
