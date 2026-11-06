using CrashEngine.Core;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void ShowOpenFileDialog(string title, string filter, Action<string?> callback) =>
        _fileBrowser.OpenFile(title, filter, callback);

    private void ShowSaveFileDialog(string title, string filter, string defaultFileName, Action<string?> callback) =>
        _fileBrowser.OpenSave(title, filter, defaultFileName, callback);
}
