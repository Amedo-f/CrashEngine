using CrashEngine.Core;
using Silk.NET.OpenGL;

namespace CrashEngine.Stealth;

public sealed class DetectionMeter : Component
{
    public StealthEnemy? Enemy { get; set; }

    private float _printTimer = 0f;

    public override void OnUpdate()
    {
        if (Enemy is null) return;
        _printTimer += EngineTime.Delta;
        if (_printTimer < 0.5f) return;
        _printTimer = 0f;

        int bars   = (int)(Enemy.DetectionPct / 5f);
        string bar = "[" + new string('|', bars) + new string(' ', 20 - bars) + "]";
        string state = Enemy.State.ToString().PadRight(7);
        Console.Write($"\r{state} {bar} {Enemy.DetectionPct,5:F1}%   ");
    }

    public override void OnRender(GL _)
    {
    }
}
