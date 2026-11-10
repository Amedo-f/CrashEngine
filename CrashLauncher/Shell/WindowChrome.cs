using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed class WindowChrome
{
    private const uint CloseHoverColor = 0xFF2311E8;
    private const float AnimSeconds = 0.22f;

    private sealed class State
    {
        public required string Title;
        public bool Docked, Maximized, Minimized, RestoreNextFrame, FocusNextFrame;
        public int LastFrame = -1;
        public double HiddenUntil, TabShownAt;
        public Vector2 Pos, Size, RestorePos, RestoreSize, TabMin, TabMax;
    }

    private sealed class Flight
    {
        public required State State;
        public required Vector2 FromMin, FromMax;
        public required double Start;
        public required bool ToTab;
    }

    private readonly Dictionary<string, State> _states = new();
    private readonly List<State> _minimized = new();
    private readonly List<Flight> _flights = new();
    private Vector2 _workPos, _workSize;

    public void SetWorkArea(Vector2 pos, Vector2 size)
    {
        _workPos = pos;
        _workSize = size;
    }

    public void Reveal(string name)
    {
        if (_states.TryGetValue(name, out var state) && state.Minimized) Restore(state);
    }

    public bool Begin(string name, ref bool open, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        if (!_states.TryGetValue(name, out var state)) _states[name] = state = new State { Title = TitleOf(name) };
        int frame = ImGui.GetFrameCount();
        bool reopened = state.LastFrame < frame - 1;
        state.LastFrame = frame;
        if (state.Minimized && reopened) Unminimize(state);
        if (state.Minimized || ImGui.GetTime() < state.HiddenUntil) return false;

        if (state.FocusNextFrame) ImGui.SetNextWindowFocus();
        state.FocusNextFrame = false;
        flags |= ImGuiWindowFlags.NoCollapse;
        if (!state.Docked)
        {
            if (state.Maximized)
            {
                ImGui.SetNextWindowPos(_workPos, ImGuiCond.Always);
                ImGui.SetNextWindowSize(_workSize, ImGuiCond.Always);
                flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
            }
            else if (state.RestoreNextFrame)
            {
                ImGui.SetNextWindowPos(state.RestorePos, ImGuiCond.Always);
                ImGui.SetNextWindowSize(state.RestoreSize, ImGuiCond.Always);
            }
        }
        state.RestoreNextFrame = false;

        bool visible = ImGui.Begin(name, flags);
        state.Docked = ImGui.IsWindowDocked();
        state.Pos = ImGui.GetWindowPos();
        state.Size = ImGui.GetWindowSize();
        if (state.Docked) state.Maximized = false;
        else DrawTitleButtons(state, ref open);
        if (visible && !state.Minimized) return true;
        ImGui.End();
        return false;
    }

    public void DrawMinimizedTabs()
    {
        int frame = ImGui.GetFrameCount();
        double now = ImGui.GetTime();
        for (int i = 0; i < _minimized.Count; i++)
        {
            var state = _minimized[i];
            if (state.LastFrame < frame - 1) continue;
            float fade = Ease((float)((now - state.TabShownAt) / AnimSeconds));
            ImGui.SameLine();
            ImGui.SetCursorPosY((ImGui.GetWindowHeight() - ImGui.GetFrameHeight()) * 0.5f);
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, MathF.Max(0.05f, fade));
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.Tab));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.TabHovered));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.TabActive));
            bool clicked = ImGui.Button($"{state.Title}##minimized{i}");
            ImGui.PopStyleColor(3);
            ImGui.PopStyleVar();
            state.TabMin = ImGui.GetItemRectMin();
            state.TabMax = ImGui.GetItemRectMax();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Show {state.Title}");
            if (!clicked) continue;
            Restore(state);
            i--;
        }
    }

    public void DrawEffects()
    {
        if (_flights.Count == 0) return;
        double now = ImGui.GetTime();
        var drawList = ImGui.GetForegroundDrawList();
        uint fill = ImGui.GetColorU32(ImGuiCol.WindowBg, 0.85f);
        uint title = ImGui.GetColorU32(ImGuiCol.TitleBgActive);
        uint border = ImGui.GetColorU32(ImGuiCol.Border);
        float titleHeight = ImGui.GetFrameHeight();
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            var flight = _flights[i];
            float t = (float)((now - flight.Start) / AnimSeconds);
            if (t >= 1f) { _flights.RemoveAt(i); continue; }
            Vector2 toMin, toMax;
            if (flight.ToTab)
            {
                if (flight.State.TabMax == Vector2.Zero) continue;
                (toMin, toMax) = (flight.State.TabMin, flight.State.TabMax);
            }
            else (toMin, toMax) = WindowRect(flight.State);
            float e = Ease(t);
            var min = Vector2.Lerp(flight.FromMin, toMin, e);
            var max = Vector2.Lerp(flight.FromMax, toMax, e);
            float bar = MathF.Min(titleHeight, max.Y - min.Y);
            drawList.AddRectFilled(min, max, fill, 4f);
            drawList.AddRectFilled(min, new Vector2(max.X, min.Y + bar), title, 4f, ImDrawFlags.RoundCornersTop);
            drawList.AddRect(min, max, border, 4f);
            var textSize = ImGui.CalcTextSize(flight.State.Title);
            if (textSize.X + 12f < max.X - min.X && textSize.Y <= bar)
                drawList.AddText(new Vector2(min.X + 6f, min.Y + (bar - textSize.Y) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), flight.State.Title);
        }
    }

    private void Minimize(State state)
    {
        state.Minimized = true;
        state.TabMin = state.TabMax = Vector2.Zero;
        state.TabShownAt = ImGui.GetTime();
        _minimized.Add(state);
        var (min, max) = WindowRect(state);
        _flights.Add(new Flight { State = state, FromMin = min, FromMax = max, Start = ImGui.GetTime(), ToTab = true });
    }

    private void Restore(State state)
    {
        var from = (state.TabMin, state.TabMax);
        Unminimize(state);
        if (from.TabMax == Vector2.Zero) return;
        state.HiddenUntil = ImGui.GetTime() + AnimSeconds;
        _flights.Add(new Flight { State = state, FromMin = from.TabMin, FromMax = from.TabMax, Start = ImGui.GetTime(), ToTab = false });
    }

    private void Unminimize(State state)
    {
        state.Minimized = false;
        state.FocusNextFrame = true;
        _minimized.Remove(state);
        _flights.RemoveAll(f => f.State == state);
    }

    private (Vector2 Min, Vector2 Max) WindowRect(State state) =>
        state.Maximized ? (_workPos, _workPos + _workSize) : (state.Pos, state.Pos + state.Size);

    private static float Ease(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        float u = 1f - t;
        return 1f - u * u * u;
    }

    private static string TitleOf(string name)
    {
        int cut = name.IndexOf("##", StringComparison.Ordinal);
        return cut < 0 ? name : name[..cut];
    }

    private void DrawTitleButtons(State state, ref bool open)
    {
        Vector2 pos = state.Pos, size = state.Size;
        float border = ImGui.GetStyle().WindowBorderSize;
        float height = ImGui.GetFrameHeight() - border;
        float width = MathF.Round(height * 1.5f);
        var right = new Vector2(pos.X + size.X - border, pos.Y + border + height);
        var left = new Vector2(right.X - width * 3f, pos.Y + border);
        if (left.X < pos.X) return;

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(left, right, false);
        uint background = ImGui.GetColorU32(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) ? ImGuiCol.TitleBgActive : ImGuiCol.TitleBg);
        drawList.AddRectFilled(left, right, background);

        bool hoverable = ImGui.IsWindowHovered();
        uint ink = ImGui.GetColorU32(ImGuiCol.Text);
        float r = MathF.Round(height * 0.2f);
        for (int i = 0; i < 3; i++)
        {
            var min = new Vector2(left.X + width * i, left.Y);
            var max = new Vector2(min.X + width, right.Y);
            bool hovered = hoverable && ImGui.IsMouseHoveringRect(min, max, false);
            uint fill = background;
            if (hovered)
            {
                fill = i == 2 ? CloseHoverColor : ImGui.GetColorU32(ImGuiCol.ButtonHovered);
                drawList.AddRectFilled(min, max, fill);
            }
            var c = new Vector2(MathF.Round((min.X + max.X) * 0.5f), MathF.Round((min.Y + max.Y) * 0.5f));
            switch (i)
            {
                case 0:
                    drawList.AddLine(new Vector2(c.X - r, c.Y), new Vector2(c.X + r, c.Y), ink);
                    break;
                case 1 when state.Maximized:
                    float o = MathF.Max(2f, MathF.Round(r * 0.4f));
                    drawList.AddRect(new Vector2(c.X - r + o, c.Y - r), new Vector2(c.X + r, c.Y + r - o), ink);
                    drawList.AddRectFilled(new Vector2(c.X - r, c.Y - r + o), new Vector2(c.X + r - o, c.Y + r), fill);
                    drawList.AddRect(new Vector2(c.X - r, c.Y - r + o), new Vector2(c.X + r - o, c.Y + r), ink);
                    break;
                case 1:
                    drawList.AddRect(new Vector2(c.X - r, c.Y - r), new Vector2(c.X + r, c.Y + r), ink);
                    break;
                default:
                    drawList.AddLine(new Vector2(c.X - r, c.Y - r), new Vector2(c.X + r, c.Y + r), ink);
                    drawList.AddLine(new Vector2(c.X - r, c.Y + r), new Vector2(c.X + r, c.Y - r), ink);
                    break;
            }
            if (!hovered || !ImGui.IsMouseReleased(ImGuiMouseButton.Left)) continue;
            var pressedAt = ImGui.GetIO().MouseClickedPos[0];
            if (pressedAt.X < min.X || pressedAt.X >= max.X || pressedAt.Y < min.Y || pressedAt.Y >= max.Y) continue;
            if (i == 0) Minimize(state);
            else if (i == 1) ToggleMaximized(state);
            else open = false;
        }
        drawList.PopClipRect();
    }

    private static void ToggleMaximized(State state)
    {
        if (state.Maximized)
        {
            state.Maximized = false;
            state.RestoreNextFrame = true;
            return;
        }
        state.RestorePos = state.Pos;
        state.RestoreSize = state.Size;
        state.Maximized = true;
    }
}
