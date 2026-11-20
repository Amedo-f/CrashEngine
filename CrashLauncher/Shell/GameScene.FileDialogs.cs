using CrashEngine.Core;
using CrashEngine.Platform;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void ShowOpenFileDialog(string title, string filter, Action<string?> callback) =>
        _fileBrowser.OpenFile(title, filter, callback);

    private void ShowSaveFileDialog(string title, string filter, string defaultFileName, Action<string?> callback) =>
        _fileBrowser.OpenSave(title, filter, defaultFileName, callback);

    private void ShowFolderDialog(string title, string startDir, Action<string?> callback) =>
        _fileBrowser.OpenFolder(title, startDir, callback);

    // Amedo 2026-11-18
    private static void ShowInFileBrowser(string folder) => PlatformFiles.ShowFolder(folder); // Amedo 2026-11-20
}
