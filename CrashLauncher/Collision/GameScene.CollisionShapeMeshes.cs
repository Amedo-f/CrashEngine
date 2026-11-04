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
    private static readonly (int A, int B, int C)[] CollisionBoxFaceTris =
    {
        (4,5,7), (4,7,6),
        (1,0,2), (1,2,3),
        (5,1,3), (5,3,7),
        (0,4,6), (0,6,2),
        (2,6,7), (2,7,3),
        (0,1,5), (0,5,4),
    };

    private static GpuMesh BuildCollisionBoxMesh(GL gl, Vector3[] corners)
    {
        var verts = new GpuMesh.Vertex[CollisionBoxFaceTris.Length * 3];
        int vi = 0;
        foreach (var (a, b, c) in CollisionBoxFaceTris)
        {
            var pa = corners[a]; var pb = corners[b]; var pc = corners[c];
            var n = Vector3.Cross(pb - pa, pc - pa);
            n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.UnitY;
            verts[vi++] = new GpuMesh.Vertex { Position = pa, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pb, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pc, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
        }
        return new GpuMesh(gl, verts, PrimitiveType.Triangles);
    }

    private void RebuildCollisionBoxMesh(Entity e, CollisionBoxMarker marker)
    {
        var rdr = e.Get<DirectCubeRenderer>();
        if (rdr is null) return;
        var old = rdr.Mesh;
        rdr.Mesh = BuildCollisionBoxMesh(Engine.Instance.GL, marker.Corners);
        rdr.OwnsMesh = true;
        if (old is not null && old != _cubeMesh) old.Dispose();
    }

    private static void RampCorners(Vector3[] corners, float highSign, int axis)
    {
        for (int i = 0; i < 8; i++)
        {
            if ((i & 2) == 0) continue;
            float cornerSign = axis == 0 ? ((i & 1) == 0 ? -1f : 1f) : ((i & 4) == 0 ? -1f : 1f);
            if (cornerSign != highSign) corners[i].Y = 0f;
        }
    }

    private static (Vector3[] Verts, (int A, int B, int C)[] Tris) BuildCylinderTopology(float radius, float height, int segments)
    {
        segments = Math.Max(3, segments);
        var verts = new Vector3[segments * 2];
        for (int i = 0; i < segments; i++)
        {
            float ang = MathF.Tau * i / segments;
            float x = MathF.Cos(ang) * radius, z = MathF.Sin(ang) * radius;
            verts[i] = new Vector3(x, 0f, z);
            verts[segments + i] = new Vector3(x, height, z);
        }

        var tris = new List<(int, int, int)>();
        for (int i = 0; i < segments; i++)
        {
            int iN = (i + 1) % segments;
            int bA = iN, bB = i, tA = segments + i, tB = segments + iN;
            tris.Add((bA, bB, tA));
            tris.Add((bA, tA, tB));
        }
        for (int k = 1; k < segments - 1; k++)
            tris.Add((0, k, k + 1));
        for (int k = 1; k < segments - 1; k++)
            tris.Add((segments, segments + (segments - k), segments + (segments - k - 1)));

        return (verts, tris.ToArray());
    }

    private static (Vector3[] Verts, (int A, int B, int C)[] Tris) BuildPlaneTopology(float width, float length)
    {
        float hw = width * 0.5f, hl = length * 0.5f;
        var verts = new[]
        {
            new Vector3(-hw, 0f, -hl),
            new Vector3( hw, 0f, -hl),
            new Vector3( hw, 0f,  hl),
            new Vector3(-hw, 0f,  hl),
        };
        var tris = new (int, int, int)[] { (0, 1, 2), (0, 2, 3) };
        return (verts, tris);
    }

    private static GpuMesh BuildMeshFromTopology(GL gl, Vector3[] corners, (int A, int B, int C)[] triList)
    {
        var verts = new GpuMesh.Vertex[triList.Length * 3];
        int vi = 0;
        foreach (var (a, b, c) in triList)
        {
            var pa = corners[a]; var pb = corners[b]; var pc = corners[c];
            var n = Vector3.Cross(pb - pa, pc - pa);
            n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.UnitY;
            verts[vi++] = new GpuMesh.Vertex { Position = pa, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pb, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pc, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
        }
        return new GpuMesh(gl, verts, PrimitiveType.Triangles);
    }

    private static GpuMesh BuildCollisionCylinderMesh(GL gl, float radius, float height, int segments)
    {
        var (verts, tris) = BuildCylinderTopology(radius, height, segments);
        return BuildMeshFromTopology(gl, verts, tris);
    }

    private static GpuMesh BuildCollisionPlaneMesh(GL gl, float width, float length)
    {
        var (verts, tris) = BuildPlaneTopology(width, length);
        return BuildMeshFromTopology(gl, verts, tris);
    }

    private void RebuildCollisionCylinderMesh(Entity e, CollisionCylinderMarker marker)
    {
        var rdr = e.Get<DirectCubeRenderer>();
        if (rdr is null) return;
        var old = rdr.Mesh;
        rdr.Mesh = BuildCollisionCylinderMesh(Engine.Instance.GL, marker.Radius, marker.Height, marker.Segments);
        rdr.OwnsMesh = true;
        old?.Dispose();
    }

    private void RebuildCollisionPlaneMesh(Entity e, CollisionPlaneMarker marker)
    {
        var rdr = e.Get<DirectCubeRenderer>();
        if (rdr is null) return;
        var old = rdr.Mesh;
        rdr.Mesh = BuildCollisionPlaneMesh(Engine.Instance.GL, marker.Width, marker.Length);
        rdr.OwnsMesh = true;
        old?.Dispose();
    }
}
