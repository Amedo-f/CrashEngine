using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
	private enum Workspace { Scene, Animation }

	private static readonly Workspace[] Workspaces = { Workspace.Scene, Workspace.Animation };
	private Workspace _workspace = Workspace.Scene;
	private Workspace? _pendingWorkspace;
	private bool _pendingWorkspaceReset;
	private readonly Dictionary<Workspace, string> _workspaceLayouts = new();
	private string? _savedWorkspaceLayout;
	private int _workspaceSaveTick;

	private const string AnimationDockLayout =
@"[Window][##dockhost]
Pos=0,32
Size=1920,977
Collapsed=0

[Window][##topbar]
Pos=0,0
Size=1920,32
Collapsed=0

[Window][Characters##anim]
Pos=0,32
Size=325,380
Collapsed=0
DockId=0x00000025,0

[Window][Bones##anim]
Pos=0,414
Size=325,318
Collapsed=0
DockId=0x00000026,0

[Window][Inspector##panel]
Pos=1505,32
Size=415,700
Collapsed=0
DockId=0x00000028,0

[Window][Timeline##anim]
Pos=0,734
Size=1920,275
Collapsed=0
DockId=0x00000022,0

[Docking][Data]
DockSpace       ID=0x50DE06D3 Window=0x5B220BC7 Pos=0,32 Size=1920,977 Split=Y
  DockNode      ID=0x00000021 Parent=0x50DE06D3 SizeRef=1920,700 Split=X
    DockNode    ID=0x00000023 Parent=0x00000021 SizeRef=325,700 Split=Y
      DockNode  ID=0x00000025 Parent=0x00000023 SizeRef=325,380
      DockNode  ID=0x00000026 Parent=0x00000023 SizeRef=325,318
    DockNode    ID=0x00000024 Parent=0x00000021 SizeRef=1593,700 Split=X
      DockNode  ID=0x00000027 Parent=0x00000024 SizeRef=1176,700 CentralNode=1
      DockNode  ID=0x00000028 Parent=0x00000024 SizeRef=415,700
  DockNode      ID=0x00000022 Parent=0x50DE06D3 SizeRef=1920,275
";

	private static string DefaultLayoutOf(Workspace ws) => ws == Workspace.Animation ? AnimationDockLayout : DefaultDockLayout;

	// Amedo
	private static string MigrateLayout(string ini) => ini
		.Replace("[Window][Scene##panel]", "[Window][Hierarchy##panel]")
		.Replace("[Window][Assets]", "[Window][Project##panel]")
		.Replace("[Window][Output]", "[Window][Console##panel]");

	private static string WorkspaceLayoutFile(Workspace ws) =>
		System.IO.Path.Combine(AppContext.BaseDirectory, $"workspace_{ws}.ini");

	private string LayoutOf(Workspace ws)
	{
		if (_workspaceLayouts.TryGetValue(ws, out var ini)) return ini;
		try
		{
			var file = WorkspaceLayoutFile(ws);
			if (File.Exists(file)) return _workspaceLayouts[ws] = MigrateLayout(File.ReadAllText(file)); // Amedo
		}
		catch (Exception e) { _browser.Log($"Workspace layout {ws} couldn't be read: {e.Message}"); }
		return DefaultLayoutOf(ws);
	}

	private void ApplyPendingWorkspaceLayout()
	{
		if (!_defaultLayoutApplied)
		{
			if (_stageSkyHidden) { RenderPipeline.SkydomeOverride = _stageSkyBackup; _stageSkyHidden = false; }
			ImGui.LoadIniSettingsFromMemory(LayoutOf(_workspace));
			_defaultLayoutApplied = true;
			_savedWorkspaceLayout = null;
		}
		if (_pendingWorkspaceReset)
		{
			_pendingWorkspaceReset = false;
			_workspaceLayouts.Remove(_workspace);
			ImGui.LoadIniSettingsFromMemory(DefaultLayoutOf(_workspace));
			_savedWorkspaceLayout = null;
		}
		if (_pendingWorkspace is { } next && next != _workspace)
		{
			StoreWorkspaceLayout(force: true);
			if (_workspace == Workspace.Animation) LeaveAnimationStage();
			_workspace = next;
			ImGui.LoadIniSettingsFromMemory(LayoutOf(next));
			_savedWorkspaceLayout = null;
			if (next == Workspace.Animation) EnterAnimationStage();
		}
		_pendingWorkspace = null;
		if (++_workspaceSaveTick >= 120)
		{
			_workspaceSaveTick = 0;
			StoreWorkspaceLayout(force: false);
		}
	}

	private void StoreWorkspaceLayout(bool force)
	{
		string ini = ImGui.SaveIniSettingsToMemory();
		if (!force && ini == _savedWorkspaceLayout) return;
		_workspaceLayouts[_workspace] = ini;
		_savedWorkspaceLayout = ini;
		try { File.WriteAllText(WorkspaceLayoutFile(_workspace), ini); }
		catch (Exception e) { _browser.Log($"Workspace layout {_workspace} couldn't be saved: {e.Message}"); }
	}

	private void ResetWorkspaceLayout() => _pendingWorkspaceReset = true;

	private const float WorkspaceTabHeight = 24f;
	private static Vector3? _workspaceStrip;

	private void DrawWorkspaceTabs()
	{
		if (_workspaceStrip is not { } strip) return;
		ImGui.SetNextWindowPos(new Vector2(strip.X, strip.Y));
		ImGui.SetNextWindowSize(new Vector2(strip.Z, StripHeight));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
		const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
		                               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar;
		bool open = ImGui.Begin("##workspacetabs", flags);
		ImGui.PopStyleVar(3);
		if (!open) { ImGui.End(); return; }
		var colors = ImGui.GetStyle().Colors;
		var dl = ImGui.GetWindowDrawList();
		var origin = ImGui.GetWindowPos();
		dl.AddRectFilled(origin, origin + new Vector2(strip.Z, WorkspaceTabHeight), ImGui.GetColorU32(colors[(int)ImGuiCol.TitleBg]));
		float x = origin.X;
		foreach (var ws in Workspaces)
		{
			bool on = ws == _workspace;
			string label = ws.ToString();
			float w = ImGui.CalcTextSize(label).X + 44f;
			var min = new Vector2(x, origin.Y);
			var max = min + new Vector2(w, WorkspaceTabHeight);
			ImGui.SetCursorScreenPos(min);
			ImGui.BeginDisabled(Simulating && !on);
			if (ImGui.InvisibleButton($"##workspace{ws}", max - min) && !on) _pendingWorkspace = ws;
			bool hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
			ImGui.EndDisabled();
			var bg = on ? ImGuiCol.TabActive : hovered && !Simulating ? ImGuiCol.TabHovered : ImGuiCol.Tab;
			dl.AddRectFilled(min, max, ImGui.GetColorU32(colors[(int)bg]));
			if (on) dl.AddRectFilled(min, new Vector2(max.X, min.Y + 2f), ImGui.GetColorU32(colors[(int)ImGuiCol.ButtonActive]));
			uint text = ImGui.GetColorU32(colors[(int)(on ? ImGuiCol.Text : ImGuiCol.TextDisabled)]);
			DrawWorkspaceIcon(dl, ws, new Vector2(x + 17f, origin.Y + WorkspaceTabHeight * 0.5f), text);
			dl.AddText(new Vector2(x + 30f, origin.Y + (WorkspaceTabHeight - ImGui.GetFontSize()) * 0.5f), text, label);
			x += w + 1f;
		}
		if (_workspace == Workspace.Scene)
		{
			DrawStripPlayControls(origin, strip.Z, x);
			DrawGameViewToolbar(origin, strip.Z);
		}
		ImGui.End();
	}

	private float _stripPlayWidth;

	private void DrawStripPlayControls(Vector2 origin, float width, float tabsEnd)
	{
		ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 2f));
		float y = origin.Y + (WorkspaceTabHeight - ImGui.GetFrameHeight()) * 0.5f;
		float playX = MathF.Max(tabsEnd + 8f, origin.X + (width - _stripPlayWidth) * 0.5f);
		ImGui.SetCursorScreenPos(new Vector2(playX, y));
		ImGui.BeginGroup();
		if (_simulation is not null) DrawStopButton();
		else DrawPlayButton();
		ImGui.EndGroup();
		_stripPlayWidth = ImGui.GetItemRectSize().X;
		ImGui.PopStyleVar();
	}

	private static void DrawWorkspaceIcon(ImDrawListPtr dl, Workspace ws, Vector2 c, uint color)
	{
		if (ws == Workspace.Scene)
		{
			for (int i = -1; i <= 1; i += 2)
			{
				dl.AddLine(c + new Vector2(i * 2.5f - 1f, -5f), c + new Vector2(i * 2.5f + 1f, 5f), color, 1.2f);
				dl.AddLine(c + new Vector2(-5f, i * 2.5f), c + new Vector2(5f, i * 2.5f), color, 1.2f);
			}
			return;
		}
		dl.AddLine(c + new Vector2(-5f, 4f), c + new Vector2(-5f, -4f), color, 1.2f);
		dl.AddLine(c + new Vector2(-5f, 0f), c + new Vector2(2f, 0f), color, 1.2f);
		dl.AddTriangleFilled(c + new Vector2(1f, -3f), c + new Vector2(6f, 0f), c + new Vector2(1f, 3f), color);
	}

	private bool _defaultLayoutApplied;

	private const string DefaultDockLayout = // Amedo
@"[Window][##dockhost]
Pos=0,32
Size=1920,977
Collapsed=0

[Window][Hierarchy##panel]
Pos=0,32
Size=325,728
Collapsed=0
DockId=0x00000003,0

[Window][Inspector##panel]
Pos=1505,32
Size=415,977
Collapsed=0
DockId=0x00000006,0

[Window][Project##panel]
Pos=0,762
Size=1503,247
Collapsed=0
DockId=0x00000001,0

[Window][Console##panel]
Pos=0,762
Size=1503,247
Collapsed=0
DockId=0x00000001,1

[Window][Behaviour##panel]
Pos=0,762
Size=1503,247
Collapsed=0
DockId=0x00000001,2

[Docking][Data]
DockSpace       ID=0x50DE06D3 Window=0x5B220BC7 Pos=0,32 Size=1920,977 Split=X
  DockNode      ID=0x00000005 Parent=0x50DE06D3 SizeRef=1503,977 Split=Y
    DockNode    ID=0x00000004 Parent=0x00000005 SizeRef=1503,728 Split=X
      DockNode  ID=0x00000003 Parent=0x00000004 SizeRef=325,728
      DockNode  ID=0x00000002 Parent=0x00000004 SizeRef=1176,728 CentralNode=1
    DockNode    ID=0x00000001 Parent=0x00000005 SizeRef=1503,247
  DockNode      ID=0x00000006 Parent=0x50DE06D3 SizeRef=415,977
";
}
