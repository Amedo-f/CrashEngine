using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void EnsureCubeMesh(GL gl)
    {
        if (_cubeMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float z, Vector3 n, float u, float v) =>
            new() { Position = new(x, y, z), Normal = n, UV = new(u, v), Color = w };
        var U = Vector3.UnitY;  var D = -Vector3.UnitY;
        var F = Vector3.UnitZ;  var B = -Vector3.UnitZ;
        var R = Vector3.UnitX;  var L = -Vector3.UnitX;
        _cubeMesh = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-1,2,-1,U,0,0), V( 1,2,-1,U,1,0), V( 1,2, 1,U,1,1),
            V( 1,2, 1,U,1,1), V(-1,2, 1,U,0,1), V(-1,2,-1,U,0,0),
            V(-1,0,-1,D,0,0), V( 1,0, 1,D,1,1), V( 1,0,-1,D,1,0),
            V( 1,0, 1,D,1,1), V(-1,0,-1,D,0,0), V(-1,0, 1,D,0,1),
            V(-1,0,1,F,0,1), V( 1,2,1,F,1,0), V(-1,2,1,F,0,0),
            V(-1,0,1,F,0,1), V( 1,0,1,F,1,1), V( 1,2,1,F,1,0),
            V(-1,0,-1,B,1,1), V(-1,2,-1,B,1,0), V( 1,2,-1,B,0,0),
            V(-1,0,-1,B,1,1), V( 1,2,-1,B,0,0), V( 1,0,-1,B,0,1),
            V(1,0,-1,R,0,1), V(1,2, 1,R,1,0), V(1,0, 1,R,1,1),
            V(1,0,-1,R,0,1), V(1,2,-1,R,0,0), V(1,2, 1,R,1,0),
            V(-1,0,-1,L,1,1), V(-1,0, 1,L,0,1), V(-1,2, 1,L,0,0),
            V(-1,0,-1,L,1,1), V(-1,2, 1,L,0,0), V(-1,2,-1,L,1,0),
        }, PrimitiveType.Triangles);
    }

    private void EnsureCubeWireMesh(GL gl)
    {
        if (_cubeWireMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float z) =>
            new() { Position = new(x, y, z), Normal = Vector3.UnitY, UV = Vector2.Zero, Color = w };
        void Edge(List<GpuMesh.Vertex> v, (float x, float y, float z) a, (float x, float y, float z) b)
        {
            v.Add(V(a.x, a.y, a.z));
            v.Add(V(b.x, b.y, b.z));
        }
        var bl = new List<GpuMesh.Vertex>();
        Edge(bl, (-1,0,-1), ( 1,0,-1)); Edge(bl, ( 1,0,-1), ( 1,0, 1));
        Edge(bl, ( 1,0, 1), (-1,0, 1)); Edge(bl, (-1,0, 1), (-1,0,-1));
        Edge(bl, (-1,2,-1), ( 1,2,-1)); Edge(bl, ( 1,2,-1), ( 1,2, 1));
        Edge(bl, ( 1,2, 1), (-1,2, 1)); Edge(bl, (-1,2, 1), (-1,2,-1));
        Edge(bl, (-1,0,-1), (-1,2,-1)); Edge(bl, ( 1,0,-1), ( 1,2,-1));
        Edge(bl, ( 1,0, 1), ( 1,2, 1)); Edge(bl, (-1,0, 1), (-1,2, 1));
        _cubeWireMesh = new GpuMesh(gl, bl.ToArray(), PrimitiveType.Lines);
    }

    // Amedo 2026-09-19
    private GpuMesh? _lightIconQuad;
    private void EnsureLightIconQuad(GL gl)
    {
        if (_lightIconQuad is not null) return;
        var col = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float u, float v, Vector3 n) => new() { Position = new(x, y, 0f), Normal = n, UV = new(u, v), Color = col };
        var F = Vector3.UnitZ; var B = -Vector3.UnitZ;
        const float s = 1.6f;
        _lightIconQuad = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-s,-s, 0f,1f, F), V(s,-s, 1f,1f, F), V(s,s, 1f,0f, F),
            V(-s,-s, 0f,1f, F), V(s,s, 1f,0f, F), V(-s,s, 0f,0f, F),
            V(-s,-s, 0f,1f, B), V(s,s, 1f,0f, B), V(s,-s, 1f,1f, B),
            V(-s,-s, 0f,1f, B), V(-s,s, 0f,0f, B), V(s,s, 1f,0f, B),
        }, PrimitiveType.Triangles);
    }

    private void EnsureQuadMesh(GL gl)
    {
        if (_quadMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, Vector3 n) => new() { Position = new(x, y, 0f), Normal = n, UV = Vector2.Zero, Color = w };
        var F = Vector3.UnitZ; var B = -Vector3.UnitZ;
        _quadMesh = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-1,-1,F), V(1,-1,F), V(1,1,F), V(-1,-1,F), V(1,1,F), V(-1,1,F),
            V(-1,-1,B), V(1,1,B), V(1,-1,B), V(-1,-1,B), V(-1,1,B), V(1,1,B),
        }, PrimitiveType.Triangles);
    }

    private void EnsureBoxZoneMesh(GL gl)
    {
        if (_boxZoneMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float z, Vector3 n) => new() { Position = new(x, y, z), Normal = n, UV = Vector2.Zero, Color = w };
        var U = Vector3.UnitY;  var D = -Vector3.UnitY;
        var F = Vector3.UnitZ;  var B = -Vector3.UnitZ;
        var R = Vector3.UnitX;  var L = -Vector3.UnitX;
        _boxZoneMesh = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-1,1,-1,U), V( 1,1,-1,U), V( 1,1, 1,U),
            V( 1,1, 1,U), V(-1,1, 1,U), V(-1,1,-1,U),
            V(-1,-1,-1,D), V( 1,-1, 1,D), V( 1,-1,-1,D),
            V( 1,-1, 1,D), V(-1,-1,-1,D), V(-1,-1, 1,D),
            V(-1,-1,1,F), V( 1,1,1,F), V(-1,1,1,F),
            V(-1,-1,1,F), V( 1,-1,1,F), V( 1,1,1,F),
            V(-1,-1,-1,B), V(-1,1,-1,B), V( 1,1,-1,B),
            V(-1,-1,-1,B), V( 1,1,-1,B), V( 1,-1,-1,B),
            V(1,-1,-1,R), V(1,1, 1,R), V(1,-1, 1,R),
            V(1,-1,-1,R), V(1,1,-1,R), V(1,1, 1,R),
            V(-1,-1,-1,L), V(-1,-1, 1,L), V(-1,1, 1,L),
            V(-1,-1,-1,L), V(-1,1, 1,L), V(-1,1,-1,L),
        }, PrimitiveType.Triangles);
    }
}
