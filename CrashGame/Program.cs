using CrashEngine.Core;
using CrashGame;

// Amedo 2026-09-30
var logPath = Path.Combine(AppContext.BaseDirectory, "CrashGame.log");
StreamWriter log;
try { log = new StreamWriter(logPath, append: false) { AutoFlush = false }; }
catch (IOException)
{
    logPath = Path.Combine(AppContext.BaseDirectory, $"CrashGame_{Environment.ProcessId}.log");
    log = new StreamWriter(logPath, append: false) { AutoFlush = false };
}
using var logOwner = log;
Console.SetOut(log);
Console.SetError(log);
AppDomain.CurrentDomain.UnhandledException += (_, e) => { Console.WriteLine(e.ExceptionObject); log.Flush(); };
try
{
    var cfg = GameConfig.Load(args);
    Console.WriteLine($"[CrashGame] project disc {cfg.Disc}, saved chunks {cfg.SavedChunks}, level {cfg.Level}");
    GraphicsSettings.DisplayModeAvailable = !cfg.Hidden; // Amedo
    GraphicsSettings.FrameLimits = CrashEngine.Runtime.Region.RegionProfile.ForDisc(cfg.Disc).FrameLimits; // Amedo 2026-11-11
    JitWarmup.Start(typeof(CrashEngine.Runtime.SceneSimulation).Assembly, typeof(CrashEngine.Importer.MeshDecoder).Assembly, // Amedo 2026-10-01
        typeof(CrashEngine.Renderer.RenderPipeline).Assembly, typeof(Engine).Assembly, typeof(Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2).Assembly); // Amedo
    CrashEngine.Renderer.SceneTarget.ReuseHeldFrames = true;
    CrashEngine.Renderer.GpuMesh.KeepVertices = false;
    var engine = new Engine("Crash Twinsanity", cfg.Width, cfg.Height, visible: false);
    if (CrashEngine.Renderer.Texture2D.LoadWindowIcons(Path.Combine(AppContext.BaseDirectory, "Icons"), "game-icon") is { Length: > 0 } icons) engine.SetWindowIcon(icons); // Amedo 2026-11-15
    engine.ActiveScene = new PlayScene(cfg);
    engine.Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    log.Flush();
    if (OperatingSystem.IsWindows()) CrashEngine.Platform.Windows.WinMessageBox.ShowError(ex.Message + "\n\nDetails: " + logPath, "CrashGame");
    return 1;
}
finally
{
    log.Flush();
}
return 0;
