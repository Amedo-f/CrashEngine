namespace CrashEngine.Core;

public static class EngineTime
{
    public static float Delta      { get; internal set; }
    public static float FixedDelta { get; internal set; }
    public static float Total      { get; internal set; }
    public static float TimeScale  { get; set; } = 1f;
}
