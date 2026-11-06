using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;

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

    private static GpuMesh BuildCollisionBoxMesh(GL gl, Vector3[] corners) => BuildMeshFromTopology(gl, corners, CollisionBoxFaceTris);

    private void ReplaceCollisionShapeMesh(Entity e, Func<GL, GpuMesh> build)
    {
        var rdr = e.Get<DirectCubeRenderer>();
        if (rdr is null) return;
        var old = rdr.Mesh;
        rdr.Mesh = build(Engine.Instance.GL);
        rdr.OwnsMesh = true;
        if (old is not null && old != _cubeMesh) old.Dispose();
    }

    private void RebuildCollisionBoxMesh(Entity e, CollisionBoxMarker marker) =>
        ReplaceCollisionShapeMesh(e, gl => BuildCollisionBoxMesh(gl, marker.Corners));

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

    private static GpuMesh BuildMeshFromTopology(GL gl, IReadOnlyList<Vector3> corners, IReadOnlyList<(int A, int B, int C)> triList)
    {
        var verts = new GpuMesh.Vertex[triList.Count * 3];
        int vi = 0;
        for (int t = 0; t < triList.Count; t++)
        {
            var (a, b, c) = triList[t];
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

    private void RebuildCollisionCylinderMesh(Entity e, CollisionCylinderMarker marker) =>
        ReplaceCollisionShapeMesh(e, gl => BuildCollisionCylinderMesh(gl, marker.Radius, marker.Height, marker.Segments));

    private void RebuildCollisionPlaneMesh(Entity e, CollisionPlaneMarker marker) =>
        ReplaceCollisionShapeMesh(e, gl => BuildCollisionPlaneMesh(gl, marker.Width, marker.Length));
}
