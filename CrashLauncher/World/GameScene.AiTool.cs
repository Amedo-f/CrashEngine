using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using Silk.NET.Input;
using System.Numerics;
using PS2AnyAIPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPath;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

// Amedo 2026-11-15
public sealed partial class GameScene
{
    private bool _aiTool;
    private Entity? _aiLinkFrom;
    private Entity? _aiPatrolOwner;
    private (BaseTwinSection Section, PS2AnyAIPath Path)? _aiSelectedPath;
    private Texture2D? _aiToolIcon;
    private bool _aiToolIconTried;

    private const float AiPathPickPixels = 7f;
    private const float PatrolCheckLift = 0.5f;
    private static readonly uint AiPreviewColor = 0xFF40E0FF, AiPickedPathColor = 0xFF40E0FF, PatrolBlockedColor = 0xFF1E8CFF;

    private bool AiToolActive => _aiTool && _workspace == Workspace.Scene && !PlayFullView;

    private InstanceData? AiPatrolInstance => _aiPatrolOwner is { Parent: not null } owner ? owner.Get<InstanceData>() : null;

    private void SetAiTool(bool on)
    {
        _aiTool = on;
        _aiLinkFrom = null;
        _aiSelectedPath = null;
        _aiPatrolOwner = on && _selected?.Get<InstanceData>() is not null ? _selected : null;
        if (!on) return;
        if (_aiPatrolOwner is null && _aiPosRoot is not null) { _aiPosRoot.Active = true; _showAiPositions = true; }
        if (_aiPatrolOwner is not null && _positionsRoot is not null) { _positionsRoot.Active = true; _showPositions = true; }
    }

    private void DrawAiToolButton(float icon)
    {
        if (_workspace != Workspace.Scene) return;
        if (!_aiToolIconTried)
        {
            _aiToolIconTried = true;
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "ai_position_marker.png");
            if (File.Exists(path)) _aiToolIcon = Texture2D.FromFile(Engine.Instance.GL, path);
        }
        bool on = _aiTool;
        if (on) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
        bool clicked = _aiToolIcon is not null
            ? ImGui.ImageButton("##aitool", (nint)_aiToolIcon.GlId, new Vector2(icon, icon), Vector2.Zero, Vector2.One, Vector4.Zero, Vector4.One)
            : ImGui.SmallButton("AI##aitool");
        if (on) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("AI tool: place and link AI points in the scene.\nWith an enemy selected: its own patrol points.");
        if (clicked) SetAiTool(!_aiTool);
    }

    private bool AiToolClick(Vector2 mouse, int w, int h)
    {
        if (!AiToolActive || _camera is null) return false;
        if (_aiLinkFrom is { Parent: null }) _aiLinkFrom = null;
        var hit = RaycastScene(mouse, w, h);
        bool chain = Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight);
        if (AiPatrolInstance is { } owner) PatrolClick(owner, hit, mouse, w, h);
        else NetworkClick(hit, mouse, w, h, chain);
        return true;
    }

    private void NetworkClick(Entity? hit, Vector2 mouse, int w, int h, bool chain)
    {
        _aiSelectedPath = null;
        if (hit?.Get<AiPositionMarker>() is { } target)
        {
            if (_aiLinkFrom?.Get<AiPositionMarker>() is { } from && !ReferenceEquals(_aiLinkFrom, hit))
            {
                var rec = new LayoutEditRecorder();
                if (LinkAiPositions(rec, from, target, out var error))
                {
                    _undoStack.Push(rec.Finish(hit));
                    _browser.Log($"AI tool: linked {_aiLinkFrom.Name} and {hit.Name}. Save Chunk to keep.");
                }
                else _browser.Log($"AI tool: not linked, {error}.");
                _aiLinkFrom = chain ? hit : null;
            }
            else _aiLinkFrom = hit;
            SelectClicked(hit, false);
            return;
        }
        if (PickAiPath(mouse, w, h) is { } picked)
        {
            _aiSelectedPath = picked;
            _aiLinkFrom = null;
            return;
        }
        if (hit?.Get<InstanceData>() is not null)
        {
            _aiPatrolOwner = hit;
            _aiLinkFrom = null;
            if (_positionsRoot is not null) { _positionsRoot.Active = true; _showPositions = true; }
            SelectClicked(hit, false);
            return;
        }
        if (_aiPosRoot is null || !TryRaycastLevel(mouse, w, h, _aiPosRoot, out var ground))
        {
            _aiLinkFrom = null;
            return;
        }
        var add = new LayoutEditRecorder();
        var linkFrom = chain ? _aiLinkFrom?.Get<AiPositionMarker>() : null;
        var layout = _aiLinkFrom?.Get<AiPositionMarker>() is { } near ? AiLayoutOf(near.Source) : null;
        if (AddAiPosition(add, ground, layout) is not { } created) return;
        if (linkFrom is not null && !LinkAiPositions(add, linkFrom, created.Get<AiPositionMarker>()!, out var linkError))
            _browser.Log($"AI tool: new point not linked, {linkError}.");
        _undoStack.Push(add.Finish(created));
        _aiLinkFrom = created;
    }

    private void PatrolClick(InstanceData owner, Entity? hit, Vector2 mouse, int w, int h)
    {
        if (hit is not null && ReferenceEquals(hit, _aiPatrolOwner)) return;
        var rec = new LayoutEditRecorder();
        if (hit?.Get<PositionMarker>() is { } existing)
        {
            if (AppendPatrolPoint(rec, owner, existing, out var error)) _undoStack.Push(rec.Finish(_aiPatrolOwner));
            else _browser.Log($"AI tool: {error}.");
            return;
        }
        if (hit?.Get<InstanceData>() is not null)
        {
            _aiPatrolOwner = hit;
            SelectClicked(hit, false);
            return;
        }
        if (_positionsRoot is null || !TryRaycastLevel(mouse, w, h, _positionsRoot, out var ground)) return;
        if (AddPatrolPoint(rec, owner, ground) is null) return;
        _undoStack.Push(rec.Finish(_aiPatrolOwner));
        if (!ReferenceEquals(_selected, _aiPatrolOwner)) SelectClicked(_aiPatrolOwner, false);
        _browser.Log($"AI tool: patrol point {owner.Source.Positions.Count} added to {_aiPatrolOwner!.Name}. Save Chunk to keep.");
    }

    private bool AiToolEscape()
    {
        if (!AiToolActive) return false;
        if (_aiLinkFrom is not null || _aiSelectedPath is not null) { _aiLinkFrom = null; _aiSelectedPath = null; }
        else if (_aiPatrolOwner is not null) _aiPatrolOwner = null;
        else SetAiTool(false);
        return true;
    }

    private bool AiToolDelete()
    {
        if (!AiToolActive || _aiSelectedPath is not { } picked) return false;
        var rec = new LayoutEditRecorder();
        DeleteAiPath(rec, picked.Section, picked.Path);
        _undoStack.Push(rec.Finish(null));
        _aiSelectedPath = null;
        _browser.Log("AI tool: link deleted. Save Chunk to keep.");
        return true;
    }

    private IEnumerable<(BaseTwinSection Section, PS2AnyAIPath Path, Entity A, Entity B)> AiLinks()
    {
        if (_aiPosRoot is not { Active: true } root || Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2 is not { } rm2) yield break;
        var markers = new Dictionary<object, Entity>(ReferenceEqualityComparer.Instance);
        foreach (var e in root.Children)
            if (e.Active && e.Get<AiPositionMarker>()?.Source is { } s) markers[s] = e;
        foreach (var layout in Layouts(rm2))
        {
            var posSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_POSITIONS_SECTION);
            var pathSec = layout.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_AI_PATHS_SECTION);
            if (posSec is null || pathSec is null) continue;
            for (int i = 0; i < pathSec.GetItemsAmount(); i++)
            {
                if (pathSec.GetItem(i) is not PS2AnyAIPath p || p.Args.Length < 2) continue;
                if (!posSec.ContainsItem(p.Args[0]) || !posSec.ContainsItem(p.Args[1])) continue;
                if (!markers.TryGetValue(posSec.GetItem<PS2AnyAIPosition>(p.Args[0]), out var a) || !markers.TryGetValue(posSec.GetItem<PS2AnyAIPosition>(p.Args[1]), out var b)) continue;
                yield return (pathSec, p, a, b);
            }
        }
    }

    private (BaseTwinSection Section, PS2AnyAIPath Path)? PickAiPath(Vector2 mouse, int w, int h)
    {
        (BaseTwinSection, PS2AnyAIPath)? best = null;
        float bestDistance = AiPathPickPixels;
        foreach (var (section, path, a, b) in AiLinks())
        {
            if (!WorldToScreen(a.Transform.World.Translation, w, h, out var sa) || !WorldToScreen(b.Transform.World.Translation, w, h, out var sb)) continue;
            float d = DistanceToSegment(mouse, sa, sb);
            if (d < bestDistance) { bestDistance = d; best = (section, path); }
        }
        return best;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float len2 = ab.LengthSquared();
        float t = len2 < 1e-6f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    private void DrawAiToolOverlay()
    {
        if (!AiToolActive) return;
        if (_aiLinkFrom is { Parent: null }) _aiLinkFrom = null;
        int w = ViewW, h = ViewH;
        var draw = ImGui.GetForegroundDrawList();
        var mouse = Input.MousePosition;

        if (_aiSelectedPath is { } picked)
            foreach (var (_, path, a, b) in AiLinks())
                if (ReferenceEquals(path, picked.Path) && WorldToScreen(a.Transform.World.Translation, w, h, out var sa) && WorldToScreen(b.Transform.World.Translation, w, h, out var sb))
                    draw.AddLine(sa, sb, AiPickedPathColor, 4f);

        if (_aiLinkFrom is { } from && MouseInView(mouse) && WorldToScreen(from.Transform.World.Translation, w, h, out var start))
            draw.AddLine(start, mouse, AiPreviewColor, 2f);

        if (AiPatrolInstance is { } owner) DrawPatrolObstacles(owner, draw, w, h);
        DrawAiToolPanel(owner: AiPatrolInstance);
    }

    private void DrawPatrolObstacles(InstanceData owner, ImDrawListPtr draw, int w, int h)
    {
        if (_positionsRoot is null || Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>() is not { } src || LevelCollision(src) is not { } world) return;
        var lift = new Vector3(0f, PatrolCheckLift, 0f);
        var points = PatrolPoints(owner.Source).Where(p => p.Point is not null).Select(p => new Vector3(p.Point!.Position.X, p.Point.Position.Y, p.Point.Position.Z)).ToList();
        var space = _positionsRoot.Transform.World;
        for (int i = 0; i + 1 < points.Count; i++)
        {
            if (!world.SegmentCast(points[i] + lift, points[i + 1] + lift, 0xFFFFFFFFu, out _)) continue;
            if (WorldToScreen(Vector3.Transform(points[i], space), w, h, out var a) && WorldToScreen(Vector3.Transform(points[i + 1], space), w, h, out var b))
                draw.AddLine(a, b, PatrolBlockedColor, 4f);
        }
    }

    private void DrawAiLayoutWarning()
    {
        if (Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2 is not { } rm2) return;
        var inUse = AiLayoutsInUse(rm2);
        if (inUse.Count < 2) return;
        ImGui.TextColored(new Vector4(1f, 0.6f, 0.2f, 1f), $"AI points are split over {inUse.Count} layouts: the game only reads one network.");
        if (ImGui.SmallButton("Merge them into the main network"))
        {
            var rec = new LayoutEditRecorder();
            int moved = MergeAiLayouts(rec);
            _undoStack.Push(rec.Finish(null));
            _browser.Log($"AI tool: moved {moved} AI point(s) and their links into the main network layout. Link them to it, then Save Chunk.");
        }
    }

    private void DrawAiToolPanel(InstanceData? owner)
    {
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
                                       ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;
        ImGui.SetNextWindowPos(ViewOrigin + new Vector2(56f, 8f));
        ImGui.SetNextWindowBgAlpha(0.75f);
        if (!ImGui.Begin("##aitoolpanel", flags)) { ImGui.End(); return; }
        if (owner is not null && _aiPatrolOwner is not null)
        {
            ImGui.TextUnformatted($"AI Tool: patrol points of {_aiPatrolOwner.Name}");
            var info = MovementOf(owner);
            if (info.IdleModes.Count > 0)
                ImGui.TextDisabled($"Idle mode: {info.Current(owner.Source.StateFlags)?.Name ?? info.DefaultIdle}");
            ImGui.TextDisabled($"Points: {owner.Source.Positions.Count}");
            ImGui.TextDisabled("Click ground: add a point   Click a yellow point: reuse it");
            ImGui.TextDisabled("Orange line: blocked by collision (the enemy stops there)");
            ImGui.TextDisabled("Esc: back to the AI network");
        }
        else
        {
            ImGui.TextUnformatted("AI Tool: AI network");
            DrawAiLayoutWarning();
            ImGui.TextDisabled("Click ground: add a point   Point then point: link");
            ImGui.TextDisabled("Shift: chain   Click a line: select it   Del: delete it");
            ImGui.TextDisabled("Click an enemy: its patrol points   Esc: exit");
            if (_aiSelectedPath is { } picked)
            {
                ImGui.Separator();
                ImGui.TextUnformatted($"Link 0x{picked.Path.Args[0]:X4} - 0x{picked.Path.Args[1]:X4}");
                if (ImGui.SmallButton("Delete link")) AiToolDelete();
            }
        }
        ImGui.End();
    }
}
