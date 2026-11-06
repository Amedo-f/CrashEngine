using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;
using PS2BehaviourCommand = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommand;
using CrashEngine.Runtime.AgentLab;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
	private const float EventRowH = 22f;

	private bool DrawEventRow(ImDrawListPtr dl, AnimatedObject.SlotGroup g, Vector2 o, float x0, float frameW, float width, float y, Vector2 mouse, bool hovered)
	{
		var events = SlotEvents(g);
		dl.AddRectFilled(new Vector2(o.X, y), new Vector2(o.X + width, y + EventRowH), ImGui.GetColorU32(new Vector4(0.4f, 0.9f, 0.5f, 0.05f)));
		dl.AddText(new Vector2(o.X + 4f, y + 4f), ImGui.GetColorU32(ImGuiCol.Text), "Events");
		bool tip = false;
		bool inRow = hovered && mouse.Y >= y && mouse.Y < y + EventRowH;
		foreach (var e in events)
		{
			float x = x0 + EventFrame(e, g) * frameW + frameW * 0.5f;
			bool sel = IsSelected(e);
			uint col = sel ? EventSelCol : e.Selector != AnimEventScan.NoSelector ? EventStepCol : EventOtherCol;
			dl.AddLine(new Vector2(x, y + 2f), new Vector2(x, y + EventRowH - 2f), col, sel ? 2.5f : 1.5f);
			dl.AddTriangleFilled(new Vector2(x, y + 3f), new Vector2(x + 8f, y + 7f), new Vector2(x, y + 11f), col);
			if (e.ExitPoint != AnimEventScan.RootExitPoint) dl.AddText(new Vector2(x + 3f, y + 9f), col, e.ExitPoint.ToString());
			if (!inRow || tip || MathF.Abs(mouse.X - x) >= 7f) continue;
			tip = true;
			ImGui.SetTooltip(e.Selector != AnimEventScan.NoSelector ? $"Footstep · exit point {e.ExitPoint} · frame {EventFrame(e, g):0.0} — click to open it in the Inspector"
				: $"Timed effect · frame {EventFrame(e, g):0.0} — click to open it in the Inspector");
			if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _selEvent = (e.Command, e.Slot);
		}
		if (events.Count == 0) dl.AddText(new Vector2(x0 + 4f, y + 4f), ImGui.GetColorU32(ImGuiCol.TextDisabled), "no events for this animation in the character's scripts");
		return tip;
	}

	private void BeginEventEdit(PS2BehaviourCommand cmd)
	{
		if (_eventEditUndo is null) _eventEditUndo = (cmd, cmd.Arguments.ToArray());
	}

	private void EndEventEdit(AnimEvent e)
	{
		if (_eventEditUndo is not { } u) return;
		_eventEditUndo = null;
		var after = u.Cmd.Arguments.ToArray();
		if (after.AsSpan().SequenceEqual(u.Before)) return;
		_animUndo.Push(new ScriptArgsAction { Command = u.Cmd, Before = u.Before, After = after, Apply = ApplyScriptArgs });
		_scriptEdited.Add(e.Graph);
		RescanEvents();
	}

	private void ApplyScriptArgs(PS2BehaviourCommand cmd, uint[] values)
	{
		for (int i = 0; i < values.Length && i < cmd.Arguments.Count; i++) cmd.Arguments[i] = values[i];
		foreach (var e in TargetEvents())
			if (ReferenceEquals(e.Command, cmd)) { _scriptEdited.Add(e.Graph); break; }
		RescanEvents();
	}

	private void EditEvent(AnimEvent e, Action<PS2BehaviourCommand> change)
	{
		BeginEventEdit(e.Command);
		change(e.Command);
		EndEventEdit(e);
	}

	private void DrawAnimEventsInfo(AnimatedObject.SlotGroup g)
	{
		var events = SlotEvents(g);
		if (events.Count == 0) return;
		ImGui.SeparatorText("Events (from the scripts)");
		var ogi = g.Ogi;
		for (int i = 0; i < events.Count; i++)
		{
			var e = events[i];
			var col = ImGui.ColorConvertU32ToFloat4(e.Selector != AnimEventScan.NoSelector ? EventStepCol : EventOtherCol);
			ImGui.PushStyleColor(ImGuiCol.Text, col);
			string label = e.Selector != AnimEventScan.NoSelector ? $"frame {EventFrame(e, g):0.0}   footstep · {ExitPointName(e.ExitPoint, ogi)}##ev{i}"
				: $"frame {EventFrame(e, g):0.0}   timed effect · {ExitPointName(e.ExitPoint, ogi)}##ev{i}";
			if (ImGui.Selectable(label, IsSelected(e))) _selEvent = IsSelected(e) ? null : (e.Command, e.Slot);
			ImGui.PopStyleColor();
		}
		if (SelectedEvent(g) is { } sel) DrawEventEditor(sel, g, ogi);
	}

	private void DrawEventEditor(AnimEvent e, AnimatedObject.SlotGroup g, PS2AnyOGI ogi)
	{
		bool canEdit = AnimEventScan.Editable(e.Command) && !Simulating;
		ImGui.SeparatorText(e.Selector != AnimEventScan.NoSelector ? "Event: footstep" : "Event: timed effect");
		ImGui.TextDisabled($"From the script {e.Behaviour} (state {e.State}, body {e.Body}) — it plays this animation, then adds this trail.");
		if (e.Command.Arguments.Count > 3 && e.Command.Arguments[3] != 0xFFFFFFFFu)
			ImGui.TextDisabled($"Only fires on {SurfaceName((int)e.Command.Arguments[3])}.");
		ImGui.BeginDisabled(!canEdit);

		int frames = Math.Max(1, g.Anim.MainAnimation.AnimatedTransformations.Count);
		float fps = g.Anim.DefaultFPS > 0 ? g.Anim.DefaultFPS : 30f;
		float frame = frames - e.SecondsBeforeEnd * fps;
		ImGui.TextUnformatted("Frame");
		ImGui.SameLine(110f);
		ImGui.SetNextItemWidth(-1f);
		if (ImGui.DragFloat("##evframe", ref frame, 0.05f, 0f, frames, "%.2f"))
		{
			BeginEventEdit(e.Command);
			AnimEventScan.SetSecondsBeforeEnd(e.Command, MathF.Max(0f, (frames - Math.Clamp(frame, 0f, frames)) / fps));
			RescanEvents();
		}
		if (ImGui.IsItemDeactivated()) EndEventEdit(e);

		ImGui.TextUnformatted("Where");
		ImGui.SameLine(110f);
		ImGui.SetNextItemWidth(-1f);
		if (ImGui.BeginCombo("##evexit", ExitPointName(e.ExitPoint, ogi)))
		{
			for (int i = 0; i <= ogi.ExitPoints.Count; i++)
			{
				int ep = i < ogi.ExitPoints.Count ? i : AnimEventScan.RootExitPoint;
				if (ImGui.Selectable($"{ExitPointName(ep, ogi)}##ep{i}", ep == e.ExitPoint) && ep != e.ExitPoint)
					EditEvent(e, c => AnimEventScan.SetExitPoint(c, ep));
			}
			ImGui.EndCombo();
		}

		ImGui.TextUnformatted("Effect");
		ImGui.SameLine(110f);
		ImGui.SetNextItemWidth(-1f);
		string effectLabel = e.Selector != AnimEventScan.NoSelector ? $"By the ground under it — effect set {e.Selector}" : "Its own particle (no ground)";
		if (ImGui.BeginCombo("##eveffect", effectLabel))
		{
			for (int set = 0; set <= 6; set++)
			{
				int value = set < 6 ? set : AnimEventScan.NoSelector;
				string name = set < 6 ? $"By the ground under it — effect set {set}" : "Its own particle (no ground)";
				if (ImGui.Selectable(name, value == e.Selector) && value != e.Selector) EditEvent(e, c => AnimEventScan.SetSelector(c, value));
			}
			ImGui.EndCombo();
		}

		if (e.Selector == AnimEventScan.NoSelector)
		{
			ImGui.TextUnformatted("Particle");
			ImGui.SameLine(110f);
			ImGui.SetNextItemWidth(-1f);
			if (ImGui.BeginCombo("##evsystem", SystemName(e.System)))
			{
				if (ImGui.Selectable("none##sysnone", e.System == 0xFFFF) && e.System != 0xFFFF) EditEvent(e, c => AnimEventScan.SetSystem(c, 0xFFFF));
				if (AnimParticles() is { } fx)
					for (int i = 0; i < RuntimeParticles.MaxSystems; i++)
					{
						if (fx.System(i) is not { } s) continue;
						ushort id = (ushort)i;
						if (ImGui.Selectable($"0x{i:X4} {ParticleNames.Clean(s.Name)}##sys{i}", id == e.System) && id != e.System) EditEvent(e, c => AnimEventScan.SetSystem(c, id));
					}
				ImGui.EndCombo();
			}
		}
		ImGui.EndDisabled();

		ImGui.Spacing();
		ImGui.TextUnformatted("Preview on");
		ImGui.SameLine(110f);
		ImGui.SetNextItemWidth(-70f);
		if (ImGui.BeginCombo("##evsurface", SurfaceById(_eventPreviewSurface) is { } ps ? SurfaceName(ps.Id) : "none"))
		{
			foreach (var s in AnimSurfaces())
			{
				if (s.Id is 4 or 5 or 18 or 19 or 20 or 23 or 24 or 26 or 27) continue;
				if (ImGui.Selectable($"{SurfaceName(s.Id)}##surf{s.Id}", s.Id == _eventPreviewSurface)) _eventPreviewSurface = s.Id;
			}
			ImGui.EndCombo();
		}
		ImGui.SameLine();
		if (ImGui.Button("Fire##evfire", new Vector2(-1f, 0f)) && PreviewJointWorlds(out var pogi) is { } pw && pogi is not null)
		{
			FireEventPreview(e, pw, pogi);
			if (ExitPointMatrix(e.ExitPoint, pw, pogi) is { } m) _eventFlashes.Add((m.Translation, ImGui.GetTime() + 0.4, e.Selector != AnimEventScan.NoSelector));
		}

		var (particle, print, dust, sound, vol) = EventEffect(e, _eventPreviewSurface);
		ImGui.TextDisabled("What happens:");
		ImGui.BulletText($"Particle: {SystemName(particle)}{(dust ? $" — plus sand dust {SystemName(SandStepSystem)}" : "")}");
		if (print) ImGui.BulletText("Footprint on the ground");
		if (e.Selector != AnimEventScan.NoSelector)
			ImGui.BulletText(sound != 0xFFFF ? $"Sound 0x{sound:X4}, volume {vol:0.##}" : "No sound on this ground");
		else if (e.Sound != 0xFFFF) ImGui.BulletText($"Sound: the character's sound slot {e.Sound}");

		if (e.Selector != AnimEventScan.NoSelector && ImGui.TreeNode($"Every ground for effect set {e.Selector}##evtable"))
		{
			if (ImGui.BeginTable("##evsurfaces", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
			{
				ImGui.TableSetupColumn("Ground");
				ImGui.TableSetupColumn("Particle");
				ImGui.TableSetupColumn("Sound");
				ImGui.TableHeadersRow();
				foreach (var s in AnimSurfaces())
				{
					var (p, fp, du, sn, sv) = EventEffect(e, s.Id);
					if (p == 0xFFFF && !fp && sn == 0xFFFF) continue;
					ImGui.TableNextRow();
					ImGui.TableNextColumn();
					if (ImGui.Selectable($"{SurfaceName(s.Id)}##row{s.Id}", s.Id == _eventPreviewSurface, ImGuiSelectableFlags.SpanAllColumns)) _eventPreviewSurface = s.Id;
					ImGui.TableNextColumn();
					ImGui.TextUnformatted(p != 0xFFFF ? SystemName(p) + (fp ? " + footprint" : "") : du ? "sand dust + footprint" : fp ? "footprint" : "-");
					ImGui.TableNextColumn();
					ImGui.TextUnformatted(sn != 0xFFFF ? $"0x{sn:X4} @ {sv:0.##}" : "-");
				}
				ImGui.EndTable();
			}
			ImGui.TextDisabled("This table belongs to the level's surfaces (every character that steps there).");
			ImGui.TreePop();
		}
		if (_scriptEdited.Contains(e.Graph))
			ImGui.TextColored(new Vector4(0.95f, 0.75f, 0.25f, 1f), "Script edited — Save (in the Timeline) writes it with the level.");
		var others = TargetEvents().Where(x => ReferenceEquals(x.Command, e.Command) && x.Slot != e.Slot).Select(x => x.Slot).Distinct().ToList();
		if (others.Count > 0) ImGui.TextDisabled($"The same trail is also used by slot(s) {string.Join(", ", others)}.");
	}
}
