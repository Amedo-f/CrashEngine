using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using ITwinModel = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinModel;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private sealed class UvEditPart
    {
        public required uint RmId;
        public required ITwinModel Model;
        public required int SubIndex;
        public required Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems.ITwinSubModel Sub;
        public required Texture2D? Tex;
        public required List<TwinVec4> OriginalUVW;
    }

    private bool _uvEditorOpen;
    private string _uvEditorLabel = "";
    private ChunkSource? _uvChunkSource;
    private bool _uvIsGlobal = true;
    private List<UvEditPart> _uvParts = new();
    private int _uvCurrentPart;
    private readonly HashSet<int> _uvSelected = new();

    private Vector2 _uvPan = new(0.1f, 0.1f);
    private float _uvZoom = 1f;
    private bool _uvBoxSelecting;
    private Vector2 _uvBoxStartScreen;
    private bool _uvDraggingVerts;
    private Vector2 _uvDragLastScreen;
    private Vector2 _uvCanvasOrigin;
    private float _uvCanvasSize = 512f;

    private void OpenUvEditor(ChunkSource? chunkSource, uint ogiId, string label)
    {
        if (chunkSource?.GlobalRm2 is null) { _browser.Log("UV Editor: no global data loaded."); return; }
        var srcModels = MeshDecoder.GetObjectSourceModelsByOgiDirect(chunkSource.GlobalRm2, ogiId);
        if (srcModels is null) { _browser.Log("UV Editor: this OGI has no editable parts."); return; }

        _uvParts.Clear();
        foreach (var (rmId, model, materialIds) in srcModels)
        {
            var shaders = MeshDecoder.GetMaterialsShadersDirect(chunkSource.GlobalRm2, materialIds);
            for (int si = 0; si < model.SubModels.Count; si++)
            {
                var sub = model.SubModels[si];
                sub.CalculateData();
                Texture2D? tex = null;
                if (si < shaders.Count && shaders[si] is { } sh)
                    chunkSource.GlobalTexCache.TryGetValue(sh.TextureId, out tex);
                _uvParts.Add(new UvEditPart
                {
                    RmId = rmId, Model = model, SubIndex = si, Sub = sub, Tex = tex,
                    OriginalUVW = sub.UVW.Select(v => new TwinVec4(v.X, v.Y, v.Z, v.W)).ToList(),
                });
            }
        }
        if (_uvParts.Count == 0) { _browser.Log("UV Editor: no editable UV data found for this OGI."); return; }

        _uvEditorLabel = label;
        _uvChunkSource = chunkSource;
        _uvIsGlobal = true;
        _uvCurrentPart = 0;
        _uvSelected.Clear();
        _uvPan = new Vector2(0.1f, 0.1f);
        _uvZoom = 1f;
        _uvEditorOpen = true;
    }

    private void DrawUvEditorWindow()
    {
        if (!_uvEditorOpen) return;
        ImGui.SetNextWindowSize(new Vector2(900f, 640f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"UV Editor — {_uvEditorLabel}###uveditor", ref _uvEditorOpen))
        {
            ImGui.End();
            return;
        }
        if (_uvCurrentPart >= _uvParts.Count) _uvCurrentPart = 0;
        var part = _uvParts[_uvCurrentPart];

        if (_uvParts.Count > 1)
        {
            ImGui.SetNextItemWidth(220f);
            if (ImGui.BeginCombo("Part##uvpart", $"Part {_uvCurrentPart + 1}/{_uvParts.Count} (RigidModel 0x{part.RmId:X4}, sub {part.SubIndex})"))
            {
                for (int i = 0; i < _uvParts.Count; i++)
                {
                    bool sel = i == _uvCurrentPart;
                    if (ImGui.Selectable($"Part {i + 1}/{_uvParts.Count}##uvpartsel{i}", sel))
                    { _uvCurrentPart = i; _uvSelected.Clear(); }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
        }
        ImGui.TextDisabled($"{part.Sub.Vertexes.Count} vert(s)");

        ImGui.Columns(2, "##uveditorcols", true);
        if (ImGui.GetColumnWidth(0) < 260f) ImGui.SetColumnWidth(0, 260f);

        DrawUvCanvas(part);

        ImGui.NextColumn();

        ImGui.TextDisabled($"Selected: {_uvSelected.Count} / {part.Sub.Vertexes.Count}");
        if (ImGui.Button("Select All##uvselall")) { _uvSelected.Clear(); for (int i = 0; i < part.Sub.Vertexes.Count; i++) _uvSelected.Add(i); }
        ImGui.SameLine();
        if (ImGui.Button("Clear##uvselclear")) _uvSelected.Clear();

        ImGui.Separator();
        ImGui.TextDisabled("Move (nudge selected)");
        float nudge = 0.01f;
        ImGui.SetNextItemWidth(90f);
        ImGui.DragFloat("Step##uvnudgestep", ref nudge, 0.001f, 0.0001f, 1f, "%.4f");
        if (ImGui.Button("←##uvnl")) NudgeSelected(part, -nudge, 0); ImGui.SameLine();
        if (ImGui.Button("→##uvnr")) NudgeSelected(part, nudge, 0); ImGui.SameLine();
        if (ImGui.Button("↑##uvnu")) NudgeSelected(part, 0, nudge); ImGui.SameLine();
        if (ImGui.Button("↓##uvnd")) NudgeSelected(part, 0, -nudge);

        ImGui.Separator();
        ImGui.TextDisabled("Scale (around selection center)");
        if (ImGui.Button("-10%##uvscaledown2")) ScaleSelected(part, 0.9f); ImGui.SameLine();
        if (ImGui.Button("-2%##uvscaledown1")) ScaleSelected(part, 0.98f); ImGui.SameLine();
        if (ImGui.Button("+2%##uvscaleup1")) ScaleSelected(part, 1.02f); ImGui.SameLine();
        if (ImGui.Button("+10%##uvscaleup2")) ScaleSelected(part, 1.1f);

        ImGui.TextDisabled("Rotate (around selection center)");
        if (ImGui.Button("-15°##uvrotdown2")) RotateSelected(part, -15f); ImGui.SameLine();
        if (ImGui.Button("-5°##uvrotdown1")) RotateSelected(part, -5f); ImGui.SameLine();
        if (ImGui.Button("+5°##uvrotup1")) RotateSelected(part, 5f); ImGui.SameLine();
        if (ImGui.Button("+15°##uvrotup2")) RotateSelected(part, 15f);

        ImGui.Separator();
        if (_uvSelected.Count == 1)
        {
            int only = _uvSelected.First();
            var uv = part.Sub.UVW[only];
            float ux = uv.X, uy = uv.Y;
            ImGui.SetNextItemWidth(120f);
            if (ImGui.DragFloat("U##uvexactu", ref ux, 0.002f)) SetUv(part, only, ux, uy);
            ImGui.SetNextItemWidth(120f);
            if (ImGui.DragFloat("V##uvexactv", ref uy, 0.002f)) SetUv(part, only, ux, uy);
        }

        ImGui.Separator();
        ImGui.SetNextItemWidth(120f);
        ImGui.DragFloat("Zoom##uvzoom", ref _uvZoom, 0.02f, 0.25f, 8f);
        if (ImGui.Button("Reset View##uvresetview")) { _uvPan = new Vector2(0.1f, 0.1f); _uvZoom = 1f; }

        ImGui.Separator();
        if (ImGui.Button("Reset This Part to Original UVs##uvreset"))
        {
            for (int i = 0; i < part.Sub.UVW.Count && i < part.OriginalUVW.Count; i++)
            {
                var o = part.OriginalUVW[i];
                part.Sub.UVW[i] = new TwinVec4(o.X, o.Y, o.Z, o.W);
            }
            RefreshUvPart(part);
        }
        ImGui.TextWrapped("Edits apply directly to the level's own live data (same object Save Chunk already writes) -- Save Chunk to keep them, Build ISO + test to see them on real hardware.");

        ImGui.Columns(1);
        ImGui.End();
    }

    private void SetUv(UvEditPart part, int idx, float u, float v)
    {
        var old = part.Sub.UVW[idx];
        part.Sub.UVW[idx] = new TwinVec4(u, v, old.Z, old.W);
        RefreshUvPart(part);
    }

    private void NudgeSelected(UvEditPart part, float du, float dv)
    {
        foreach (var i in _uvSelected)
        {
            var uv = part.Sub.UVW[i];
            part.Sub.UVW[i] = new TwinVec4(uv.X + du, uv.Y + dv, uv.Z, uv.W);
        }
        RefreshUvPart(part);
    }

    private (float X, float Y) SelectionCenter(UvEditPart part)
    {
        if (_uvSelected.Count == 0) return (0.5f, 0.5f);
        float sx = 0, sy = 0;
        foreach (var i in _uvSelected) { var uv = part.Sub.UVW[i]; sx += uv.X; sy += uv.Y; }
        return (sx / _uvSelected.Count, sy / _uvSelected.Count);
    }

    private void ScaleSelected(UvEditPart part, float factor)
    {
        var (cx, cy) = SelectionCenter(part);
        foreach (var i in _uvSelected)
        {
            var uv = part.Sub.UVW[i];
            part.Sub.UVW[i] = new TwinVec4(cx + (uv.X - cx) * factor, cy + (uv.Y - cy) * factor, uv.Z, uv.W);
        }
        RefreshUvPart(part);
    }

    private void RotateSelected(UvEditPart part, float degrees)
    {
        var (cx, cy) = SelectionCenter(part);
        float rad = degrees * MathF.PI / 180f;
        float cos = MathF.Cos(rad), sin = MathF.Sin(rad);
        foreach (var i in _uvSelected)
        {
            var uv = part.Sub.UVW[i];
            float dx = uv.X - cx, dy = uv.Y - cy;
            part.Sub.UVW[i] = new TwinVec4(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos, uv.Z, uv.W);
        }
        RefreshUvPart(part);
    }

    private void RefreshUvPart(UvEditPart part)
    {
        part.Sub.Compile();
        if (Engine.Instance?.GL is { } gl)
            MeshDecoder.RefreshObjectMeshVertexData(gl, GetChunkMeshTablesHandle(), part.RmId, part.Model);
        if (_uvIsGlobal && _uvChunkSource is not null) _uvChunkSource.GlobalRm2Dirty = true;
    }

    private object? GetChunkMeshTablesHandle()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        return chunkRoot?.Get<ChunkSource>()?.MeshTables;
    }

    private Vector2 UvToScreen(float u, float v) =>
        _uvCanvasOrigin + new Vector2((u - _uvPan.X) * _uvCanvasSize * _uvZoom, (1f - (v - _uvPan.Y)) * _uvCanvasSize * _uvZoom);

    private (float U, float V) ScreenToUv(Vector2 screen)
    {
        var local = (screen - _uvCanvasOrigin) / (_uvCanvasSize * _uvZoom);
        return (local.X + _uvPan.X, 1f - local.Y + _uvPan.Y);
    }

    private void DrawUvCanvas(UvEditPart part)
    {
        ImGui.BeginChild("##uvcanvaschild", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        var avail = ImGui.GetContentRegionAvail();
        _uvCanvasSize = MathF.Max(64f, MathF.Min(avail.X, avail.Y) - 4f);
        _uvCanvasOrigin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        var bgMin = _uvCanvasOrigin + new Vector2(-_uvPan.X, _uvPan.Y - 1f) * _uvCanvasSize * _uvZoom;
        if (part.Tex is not null)
        {
            var p0 = UvToScreen(0f, 1f);
            var p1 = UvToScreen(1f, 0f);
            drawList.AddImage((nint)part.Tex.GlId, p0, p1);
        }
        else
        {
            drawList.AddRectFilled(_uvCanvasOrigin, _uvCanvasOrigin + new Vector2(_uvCanvasSize, _uvCanvasSize), 0xFF303030);
        }
        drawList.AddRect(_uvCanvasOrigin, _uvCanvasOrigin + new Vector2(_uvCanvasSize, _uvCanvasSize), 0xFF808080);

        ImGui.InvisibleButton("##uvcanvasinput", new Vector2(_uvCanvasSize, _uvCanvasSize),
            ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        bool hovered = ImGui.IsItemHovered();
        var mouse = ImGui.GetMousePos();

        if (hovered)
        {
            float wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f) _uvZoom = Math.Clamp(_uvZoom * (1f + wheel * 0.1f), 0.25f, 8f);
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
            {
                var d = ImGui.GetIO().MouseDelta;
                _uvPan -= new Vector2(d.X, -d.Y) / (_uvCanvasSize * _uvZoom);
            }
        }

        var tris = MeshDecoder.GetSubModelTriangleIndices(part.Sub);
        foreach (var (a, b, c) in tris)
        {
            if (a >= part.Sub.UVW.Count || b >= part.Sub.UVW.Count || c >= part.Sub.UVW.Count) continue;
            var pa = UvToScreen(part.Sub.UVW[a].X, part.Sub.UVW[a].Y);
            var pb = UvToScreen(part.Sub.UVW[b].X, part.Sub.UVW[b].Y);
            var pc = UvToScreen(part.Sub.UVW[c].X, part.Sub.UVW[c].Y);
            drawList.AddLine(pa, pb, 0xC000FFFF);
            drawList.AddLine(pb, pc, 0xC000FFFF);
            drawList.AddLine(pc, pa, 0xC000FFFF);
        }

        const float pickRadius = 7f;
        int hoveredVert = -1;
        for (int i = 0; i < part.Sub.UVW.Count; i++)
        {
            var p = UvToScreen(part.Sub.UVW[i].X, part.Sub.UVW[i].Y);
            if (hovered && Vector2.Distance(mouse, p) <= pickRadius) hoveredVert = i;
            bool sel = _uvSelected.Contains(i);
            drawList.AddCircleFilled(p, sel ? 5f : 3.5f, sel ? 0xFF30D0FF : 0xFFFFFFFF);
            if (sel) drawList.AddCircle(p, 6f, 0xFF30D0FF, 0, 1.5f);
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (hoveredVert >= 0)
            {
                bool additive = ImGui.GetIO().KeyCtrl;
                if (!additive && !_uvSelected.Contains(hoveredVert)) _uvSelected.Clear();
                if (_uvSelected.Contains(hoveredVert) && additive) _uvSelected.Remove(hoveredVert);
                else _uvSelected.Add(hoveredVert);
                _uvDraggingVerts = true;
                _uvDragLastScreen = mouse;
            }
            else
            {
                _uvBoxSelecting = true;
                _uvBoxStartScreen = mouse;
                if (!ImGui.GetIO().KeyCtrl) _uvSelected.Clear();
            }
        }
        if (_uvDraggingVerts && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && _uvSelected.Count > 0)
        {
            var delta = mouse - _uvDragLastScreen;
            _uvDragLastScreen = mouse;
            float du = delta.X / (_uvCanvasSize * _uvZoom);
            float dv = -delta.Y / (_uvCanvasSize * _uvZoom);
            NudgeSelected(part, du, dv);
        }
        if (_uvBoxSelecting)
        {
            drawList.AddRect(_uvBoxStartScreen, mouse, 0xFF30D0FF);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                var lo = Vector2.Min(_uvBoxStartScreen, mouse);
                var hi = Vector2.Max(_uvBoxStartScreen, mouse);
                for (int i = 0; i < part.Sub.UVW.Count; i++)
                {
                    var p = UvToScreen(part.Sub.UVW[i].X, part.Sub.UVW[i].Y);
                    if (p.X >= lo.X && p.X <= hi.X && p.Y >= lo.Y && p.Y <= hi.Y) _uvSelected.Add(i);
                }
                _uvBoxSelecting = false;
            }
        }
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) _uvDraggingVerts = false;

        ImGui.EndChild();
    }
}
