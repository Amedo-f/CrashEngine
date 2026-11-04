using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private void DrawTopBar(int width)
	{
		ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (32f - ImGui.GetFontSize()) * 0.5f))); // Amedo
		bool topBar = ImGui.BeginMainMenuBar();
		ImGui.PopStyleVar();
		if (topBar)
		{
		DrawMainMenus(); // Amedo
		float value = ((EngineTime.Delta > 0f) ? (1f / EngineTime.Delta) : 0f);
		int drawCallsThisFrame = CrashEngine.Importer.MeshRenderer.DrawCallsThisFrame;
		Engine.Instance.SetTitleDetail($"{_rm2}  |  {_enemyCount} enemies  |  {_scriptCount} scripts" +
		                               (_musicLabel is not null ? $"  |  Music: {_musicLabel}" : "")); // Amedo
		if (_glueMode)
		{
			ImGui.SameLine();
			int glueAxisLock = _glueAxisLock;
			if (1 == 0)
			{
			}
			string text = glueAxisLock switch
			{
				0 => "X", 
				1 => "Y", 
				2 => "Z", 
				_ => "auto", 
			};
			if (1 == 0)
			{
			}
			string text2 = text;
			ImGui.TextColored(new System.Numerics.Vector4(1f, 0.85f, 0.2f, 1f), "  |  GLUE MODE (axis: " + text2 + ") — click target, X/Y/Z=lock axis, Esc=cancel");
		}
		ImGui.SameLine((float)width - 430f);
		bool animUndo = _workspace == Workspace.Animation; // Amedo 2026-10-04
		ImGui.BeginDisabled(animUndo ? !_animUndo.CanUndo : !_undoStack.CanUndo); // Amedo 2026-10-04
		if (ImGui.SmallButton("Undo"))
		{
			if (animUndo) AnimUndo(); // Amedo 2026-10-04
			else SelectAffected(_undoStack.Undo());
		}
		ImGui.EndDisabled();
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Ctrl+Z");
		}
		ImGui.SameLine();
		ImGui.BeginDisabled(animUndo ? !_animUndo.CanRedo : !_undoStack.CanRedo); // Amedo 2026-10-04
		if (ImGui.SmallButton("Redo"))
		{
			if (animUndo) AnimRedo(); // Amedo 2026-10-04
			else SelectAffected(_undoStack.Redo());
		}
		ImGui.EndDisabled();
		if (ImGui.IsItemHovered())
		{
			MaybeTooltip("Ctrl+Y / Ctrl+Shift+Z"); // Amedo 2026-10-04
		}
		ImGui.SameLine((float)width - 320f);
		string camSpeedText = (_camera != null) ? $"Cam speed: {_camera.Speed:F1}  |  " : "";
		if (drawCallsThisFrame == 0)
		{
			ImGui.TextColored(new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f), $"{camSpeedText}draws: {drawCallsThisFrame}  FPS {value:F0}  ESC=back");
		}
		else
		{
			ImGui.TextDisabled($"{camSpeedText}draws: {drawCallsThisFrame}  FPS {value:F0}  ESC=back");
		}
		ImGui.EndMainMenuBar(); // Amedo
		}
	}
}
