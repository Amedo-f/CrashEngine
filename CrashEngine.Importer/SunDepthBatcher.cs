using System.Numerics;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;

namespace CrashEngine.Importer;

// Amedo
public static class SunDepthBatcher
{
    private sealed class Group
    {
        public GpuMesh Mesh = null!;
        public Material Material = null!;
        public Vector4[] Data = new Vector4[InstanceBatcher.Stride * 4];
        public int Count;
        public int Base;
        public int LastFrame;
    }

    private readonly record struct Key(GpuMesh Mesh, Material Material);

    private static readonly Dictionary<Key, Group> Groups = new();
    private static readonly List<Group> Order = new();
    private static readonly List<Key> Stale = new();
    private static Vector4[] _upload = new Vector4[InstanceBatcher.Stride * 256];
    private static ShaderStorage? _storage;
    private static int _flushes;

    public static void Defer(GpuMesh mesh, Material material, in Matrix4x4 world)
    {
        var key = new Key(mesh, material);
        if (!Groups.TryGetValue(key, out var g)) Groups[key] = g = new Group { Mesh = mesh, Material = material };
        if (g.Count == 0) Order.Add(g);
        int at = g.Count * InstanceBatcher.Stride;
        if (at + InstanceBatcher.Stride > g.Data.Length) Array.Resize(ref g.Data, g.Data.Length * 2);
        var d = g.Data.AsSpan(at, InstanceBatcher.Stride);
        d[0] = new Vector4(world.M11, world.M12, world.M13, world.M14);
        d[1] = new Vector4(world.M21, world.M22, world.M23, world.M24);
        d[2] = new Vector4(world.M31, world.M32, world.M33, world.M34);
        d[3] = new Vector4(world.M41, world.M42, world.M43, world.M44);
        d[4] = Vector4.Zero;
        g.Count++;
    }

    public static void Flush(GL gl)
    {
        _flushes++;
        if (Order.Count == 0) { Prune(); return; }
        int total = 0;
        foreach (var g in Order)
        {
            g.Base = total;
            total += g.Count * InstanceBatcher.Stride;
        }
        if (total > _upload.Length) Array.Resize(ref _upload, Math.Max(total, _upload.Length * 2));
        foreach (var g in Order) g.Data.AsSpan(0, g.Count * InstanceBatcher.Stride).CopyTo(_upload.AsSpan(g.Base));
        (_storage ??= new ShaderStorage(gl)).Upload(_upload.AsSpan(0, total), InstanceBatcher.Binding);
        for (int pass = 0; pass < 2; pass++)
            foreach (var g in Order)
                if ((g.Material.AlphaTest > 0f) == (pass == 1)) Draw(gl, g);
        foreach (var g in Order)
        {
            g.Count = 0;
            g.LastFrame = _flushes;
        }
        Order.Clear();
        Prune();
    }

    private static void Draw(GL gl, Group g)
    {
        var sh = SunShadowPass.UseDepthProgram(g.Material.AlphaTest > 0f);
        var u = sh.Cached(static s => new Uniforms(s));
        g.Material.Apply(gl, sh);
        GlState.DepthMask(gl, true);
        GlState.Culling(gl, false);
        sh.Set(u.UseInstances, 1);
        sh.Set(u.InstanceBase, g.Base);
        g.Mesh.DrawInstanced(gl, g.Count);
        sh.Set(u.UseInstances, 0);
        g.Material.Restore(gl, sh);
        SunShadowPass.Draws++;
    }

    private sealed class Uniforms(TwinShaderProgram sh)
    {
        public readonly TwinShaderProgram.Slot UseInstances = sh.Uniform("UseInstances");
        public readonly TwinShaderProgram.Slot InstanceBase = sh.Uniform("InstanceBase");
    }

    private static void Prune()
    {
        if (_flushes % 1200 != 0) return;
        Stale.Clear();
        foreach (var (key, g) in Groups)
            if (_flushes - g.LastFrame > 1200) Stale.Add(key);
        foreach (var key in Stale) Groups.Remove(key);
    }
}
