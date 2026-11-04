using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Renderer;
using ImGuiNET;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-10-26
public sealed class TouchPad
{
    private const float TapSeconds = 0.25f, TapMove = 0.15f;

    private sealed class Finger
    {
        public TouchControl? Stick;
        public float Held, MaxMove;
        public bool Seen;
    }

    private readonly Dictionary<long, Finger> _fingers = new();
    private readonly List<long> _lifted = new();
    private readonly List<string> _taps = new();
    private readonly HashSet<string> _down = new();
    private readonly Dictionary<string, Vector2> _sticks = new();

    public TouchPad(TouchLayout layout) => Layout = layout;

    public TouchLayout Layout { get; }

    public bool IsDown(TouchControl c) => _down.Contains(c.Id);

    public Vector2 StickOf(TouchControl c) => _sticks.TryGetValue(c.Id, out var v) ? v : Vector2.Zero;

    public void Apply(IReadOnlyList<(long Id, Vector2 Pos)> touches, float screenW, float screenH, float dt, CharacterPad pad)
    {
        foreach (var press in _taps) Press(pad, press);
        _taps.Clear();
        _down.Clear();
        _sticks.Clear();

        foreach (var f in _fingers.Values) f.Seen = false;
        foreach (var (id, pos) in touches)
        {
            if (!_fingers.TryGetValue(id, out var f))
                _fingers[id] = f = new Finger { Stick = Layout.Controls.FirstOrDefault(c => c.IsStick && c.Hit(pos, screenW, screenH)) };
            f.Seen = true;
            f.Held += dt;
            if (f.Stick is { } s)
            {
                var (c, size) = s.Rect(screenW, screenH);
                var v = (pos - c) / (size.X * 0.5f);
                if (GameMath.Length(v) > 1f) v = GameMath.Normalize(v);
                f.MaxMove = MathF.Max(f.MaxMove, GameMath.Length(v));
                pad.SetHostStick(v.X, -v.Y, right: s.Pad == "RightStick");
                _sticks[s.Id] = v;
                _down.Add(s.Id);
                continue;
            }
            foreach (var c in Layout.Controls)
                if (!c.IsStick && c.Hit(pos, screenW, screenH))
                {
                    Press(pad, c.Pad);
                    _down.Add(c.Id);
                }
        }

        _lifted.Clear();
        foreach (var (id, f) in _fingers)
        {
            if (f.Seen) continue;
            _lifted.Add(id);
            if (f.Stick is { Press: { } press } && f.Held <= TapSeconds && f.MaxMove <= TapMove) _taps.Add(press);
        }
        foreach (var id in _lifted) _fingers.Remove(id);
    }

    private static void Press(CharacterPad pad, string name)
    {
        switch (name)
        {
            case "Start": pad.Start = true; return;
            case "Select": pad.Select = true; return;
            case "L3": pad.L3 = true; return;
            case "R3": pad.R3 = true; return;
        }
        int i = name switch
        {
            "Right" => CharacterPad.Right, "Left" => CharacterPad.Left, "Up" => CharacterPad.Up, "Down" => CharacterPad.Down,
            "Triangle" => CharacterPad.Triangle, "Circle" => CharacterPad.Circle, "Cross" => CharacterPad.Cross, "Square" => CharacterPad.Square,
            "L1" => CharacterPad.L1, "R1" => CharacterPad.R1, "L2" => CharacterPad.L2, "R2" => CharacterPad.R2,
            _ => -1,
        };
        if (i >= 0) pad.Pressure[i] = 255;
    }
}

public sealed class TouchOverlay(string folder)
{
    private readonly Dictionary<string, Texture2D?> _images = new();

    public void Draw(ImDrawListPtr dl, TouchPad pad, Vector2 origin, Vector2 screen)
    {
        uint idle = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, pad.Layout.Opacity));
        foreach (var control in pad.Layout.Controls)
        {
            var (center, size) = control.Rect(screen.X, screen.Y);
            center += origin;
            bool down = pad.IsDown(control);
            var shown = down && !control.IsStick ? size * 0.92f : size;
            if (Image(control.Image) is { } image)
                dl.AddImage((nint)image.GlId, center - shown * 0.5f, center + shown * 0.5f, Vector2.Zero, Vector2.One, down ? 0xFFFFFFFF : idle);
            if (control.Knob is { } knobName && Image(knobName) is { } knob)
            {
                var at = center + pad.StickOf(control) * size.X * 0.5f;
                var k = size * 0.45f;
                dl.AddImage((nint)knob.GlId, at - k * 0.5f, at + k * 0.5f, Vector2.Zero, Vector2.One, down ? 0xFFFFFFFF : idle);
            }
        }
    }

    private Texture2D? Image(string name)
    {
        if (!_images.TryGetValue(name, out var texture))
        {
            var path = Path.Combine(folder, name);
            texture = Texture2D.FromFile(Engine.Instance.GL, path);
            if (texture is null) Console.WriteLine($"[touch] couldn't load {path}");
            _images[name] = texture;
        }
        return texture;
    }
}
