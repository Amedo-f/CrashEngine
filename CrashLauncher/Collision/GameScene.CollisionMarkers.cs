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
	private abstract class CollisionShapeMarker : Component
	{
		public int SurfaceIndex = -1;

		public readonly List<int> OwnedVecIndices = new List<int>();

		public readonly List<int> OwnedTriIndices = new List<int>();
	}

	private sealed class CollisionBoxMarker : CollisionShapeMarker
	{
		public readonly Vector3[] Corners = DefaultCorners();

		public static Vector3[] DefaultCorners()
		{
			var c = new Vector3[8];
			for (int i = 0; i < 8; i++)
				c[i] = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? 0f : 2f, (i & 4) == 0 ? -1f : 1f);
			return c;
		}
	}

	private sealed class CollisionCylinderMarker : CollisionShapeMarker
	{
		public float Radius = 1f;
		public float Height = 2f;
		public int Segments = 12;
	}

	private sealed class CollisionPlaneMarker : CollisionShapeMarker
	{
		public float Width = 2f;
		public float Length = 2f;
	}

	private sealed class GeneratedCollisionMarker : CollisionShapeMarker
	{
		public readonly List<System.Numerics.Vector3> LocalVertices = new List<System.Numerics.Vector3>();
		public readonly List<(int A, int B, int C)> LocalTriangles = new List<(int, int, int)>();

		public List<int> DegenerateOnFirstSync = new List<int>();
	}

	private void AttachToCollisionParentOrRoot(Entity entity)
	{
		var collisionParent = GetOrCreateCollisionRoot();
		if (collisionParent is not null)
			collisionParent.AddChild(entity);
		else
			LoadAndAddRoot(entity);
	}

	private Entity? GetOrCreateCollisionRoot()
	{
		var cr = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
		if (cr is null) return null;
		var existing = AllEntities(cr).FirstOrDefault(e => e.Has<CollisionMesh>());
		if (existing is not null) return existing;
		var collRoot = new Entity("Collision") { Active = _showCollision };
		collRoot.Add(new CollisionMesh());
		cr.AddChild(collRoot);
		return collRoot;
	}

	private void SpawnCollisionCylinder(System.Numerics.Vector3 pos)
	{
		_cubeCount++;
		Entity entity = new Entity($"CollisionCylinder_{_cubeCount}");
		entity.Transform.Position = pos;
		var marker = new CollisionCylinderMarker();
		DirectCubeRenderer directCubeRenderer = entity.Add(new DirectCubeRenderer
		{
			Mesh = BuildCollisionCylinderMesh(Engine.Instance.GL, marker.Radius, marker.Height, marker.Segments),
			Color = CollisionDebugColor,
			OwnsMesh = true
		});
		directCubeRenderer.Mat.AlphaBlend = true;
		AttachToCollisionParentOrRoot(entity);
		_cubes.Add(new CubeEntry(entity, $"Collision Cylinder {_cubeCount}", directCubeRenderer));
		entity.Add(marker);
	}

	private void SpawnCollisionPlane(System.Numerics.Vector3 pos)
	{
		_cubeCount++;
		Entity entity = new Entity($"CollisionPlane_{_cubeCount}");
		entity.Transform.Position = pos;
		var marker = new CollisionPlaneMarker();
		DirectCubeRenderer directCubeRenderer = entity.Add(new DirectCubeRenderer
		{
			Mesh = BuildCollisionPlaneMesh(Engine.Instance.GL, marker.Width, marker.Length),
			Color = CollisionDebugColor,
			OwnsMesh = true
		});
		directCubeRenderer.Mat.AlphaBlend = true;
		AttachToCollisionParentOrRoot(entity);
		_cubes.Add(new CubeEntry(entity, $"Collision Plane {_cubeCount}", directCubeRenderer));
		entity.Add(marker);
	}
}
