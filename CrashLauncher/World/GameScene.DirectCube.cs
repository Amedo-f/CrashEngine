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
	private sealed class CubeEntry
	{
		public Entity Ent;

		public string Label;

		public DirectCubeRenderer Renderer;

		public CubeEntry(Entity e, string l, DirectCubeRenderer r)
		{
			Ent = e;
			Label = l;
			Renderer = r;
		}
	}

	private sealed class DirectCubeRenderer : Component
	{
		public GpuMesh? Mesh { get; set; }

		public Material Mat { get; } = new Material
		{
			Unlit = true,
			Culling = Material.CullMode.Both,
			AlwaysOnTop = true,
			BaseColor = new System.Numerics.Vector4(1f, 0.85f, 0f, 1f)
		};


		public System.Numerics.Vector4 Color
		{
			get
			{
				return Mat.BaseColor;
			}
			set
			{
				Mat.BaseColor = value;
			}
		}

		public Texture2D? Albedo
		{
			get
			{
				return Mat.Albedo;
			}
			set
			{
				Mat.Albedo = value;
			}
		}

		public float SelectPulse { get; set; } = 1f;

		public bool OwnsMesh { get; set; }

		public override bool IsOverlay => Mat.AlwaysOnTop || Mat.IgnoreDepthTest; // Amedo 2026-10-02


		public override void OnUpdate()
		{
			System.Numerics.Vector2 uvScrollSpeed = Mat.UvScrollSpeed;
			if (uvScrollSpeed.X != 0f || uvScrollSpeed.Y != 0f)
			{
				Mat.UvOffset += uvScrollSpeed * EngineTime.Delta;
			}
		}

		public override void OnRender(GL gl)
		{
			if (Mesh != null)
			{
				RenderPipeline instance = RenderPipeline.Instance;
				if (instance != null)
				{
					TwinShaderProgram shader = instance.Shader;
					shader.Set("StartModel", base.Entity.Transform.World);
					Mat.Apply(gl, shader);
					shader.Set("twin_material.select_pulse", SelectPulse);
					Mesh.Draw(gl);
					Mat.Restore(gl, shader);
				}
			}
		}

		public override void OnDestroy()
		{
			Mat.Albedo?.Dispose();
			if (OwnsMesh) Mesh?.Dispose();
		}
	}

	private void SpawnCube(System.Numerics.Vector3 pos, bool asCollisionBox = false)
	{
		_cubeCount++;
		Entity entity = new Entity(asCollisionBox ? $"CollisionBox_{_cubeCount}" : $"Cube_{_cubeCount}");
		entity.Transform.Position = pos;
		var marker = asCollisionBox ? new CollisionBoxMarker() : null;
		DirectCubeRenderer directCubeRenderer = entity.Add(new DirectCubeRenderer
		{
			Mesh = asCollisionBox ? BuildCollisionBoxMesh(Engine.Instance.GL, marker!.Corners) : _cubeMesh,
			Color = (asCollisionBox ? CollisionDebugColor : new System.Numerics.Vector4(1f, 0.85f, 0f, 1f)),
			OwnsMesh = asCollisionBox
		});
		if (asCollisionBox)
		{
			directCubeRenderer.Mat.AlphaBlend = true;
		}
		var collisionParent = asCollisionBox ? GetOrCreateCollisionRoot() : null;
		if (collisionParent is not null)
			collisionParent.AddChild(entity);
		else
			LoadAndAddRoot(entity);
		_cubes.Add(new CubeEntry(entity, asCollisionBox ? $"Collision Box {_cubeCount}" : $"Cube {_cubeCount}", directCubeRenderer));
		if (marker is not null)
		{
			entity.Add(marker);
		}
	}

	private CubeEntry? CubeFor(Entity? e)
	{
		Entity e2 = e;
		return (e2 == null) ? null : _cubes.FirstOrDefault((CubeEntry c) => c.Ent == e2);
	}
}
