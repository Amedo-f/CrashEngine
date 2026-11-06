using CrashEngine.Core;
using CrashEngine.Renderer;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private sealed class WorldLightingSettings : Component
	{
		public System.Numerics.Vector3 AmbientColor;

		public readonly List<(System.Numerics.Vector3 Color, System.Numerics.Vector3 Direction)> Directional = new List<(System.Numerics.Vector3, System.Numerics.Vector3)>();

		public float Intensity = 1f;

		public float SceneryBrightness = 1f;
		// Amedo 2026-09-21
		public float SceneryBakedBrightness = 1f;

		public Dictionary<object, List<Twinsanity.TwinsanityInterchange.Common.Vector4>>? SceneryBaseColors;

		public readonly List<GizmoRenderer> ArrowRenderers = new List<GizmoRenderer>();

		public int FogColorIndex = 5;

		public override void OnUpdate()
		{
			RenderPipeline instance = RenderPipeline.Instance;
			if (instance != null)
			{
				instance.AmbientLightColor = AmbientColor * Intensity;
				instance.DirectionalLights = Directional.Select(((System.Numerics.Vector3 Color, System.Numerics.Vector3 Direction) l) => (l.Color * Intensity, Direction: l.Direction)).ToList();
				int idx = Math.Clamp(FogColorIndex, 0, CrashEngine.Importer.MeshDecoder.FogColors.Length - 1);
				instance.FogColor = CrashEngine.Importer.MeshDecoder.FogColors[idx];
				instance.FogIndex = idx; // Amedo 2026-09-26
			}
			if (base.Entity.Parent?.Get<CrashEngine.Importer.GameLightingEnvironment>() is { } env) // Amedo 2026-09-26
			{
				env.AmbientOverride = AmbientColor * Intensity;
				env.DirectionalOverride = Directional.Select(l => (l.Color * Intensity, new System.Numerics.Vector3(-l.Direction.X, l.Direction.Y, l.Direction.Z))).ToList();
			}
		}
	}
}
