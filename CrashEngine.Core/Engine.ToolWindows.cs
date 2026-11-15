namespace CrashEngine.Core;

// Amedo 2026-11-12
public sealed partial class Engine
{
    private readonly List<ToolWindow> _toolWindows = new();

    public bool CanOpenToolWindows => Desktop is not null && _imgui is not null && !Gles;

    public ToolWindow? OpenToolWindow(string title, int width, int height, Action draw)
    {
        if (!CanOpenToolWindows) return null;
        try
        {
            var tool = new ToolWindow(Desktop!, _pendingIcons, title, width, height, draw);
            _toolWindows.Add(tool);
            return tool;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Engine] tool window: {ex.Message}");
            return null;
        }
    }

    private void RenderToolWindows()
    {
        if (_toolWindows.Count == 0) return;
        for (int i = _toolWindows.Count - 1; i >= 0; i--)
        {
            if (_toolWindows[i].IsOpen) continue;
            _toolWindows[i].Destroy();
            _toolWindows.RemoveAt(i);
        }
        for (int i = 0; i < _toolWindows.Count; i++) _toolWindows[i].Render();
        Desktop!.GLContext?.MakeCurrent();
    }

    private void DestroyToolWindows()
    {
        foreach (var tool in _toolWindows) tool.Destroy();
        _toolWindows.Clear();
        Desktop?.GLContext?.MakeCurrent();
    }
}
