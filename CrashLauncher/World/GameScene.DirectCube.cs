using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;

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
