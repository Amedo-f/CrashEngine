using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;

namespace CrashEngine.Importer;

// Amedo 2026-10-01
public static class InstanceBatcher
{
    public const int Stride = 11;
    public const uint Binding = 3;

    private static bool _enabled = true;
    public static bool Enabled { get => _enabled && GlCaps.VertexStorageBlocks > 0; set => _enabled = value; }
    public static int Batches { get; private set; }
    public static int Instanced { get; private set; }

    public static bool FrontToBack { get; set; }

    private sealed class Group
    {
        public GpuMesh Mesh = null!;
        public Material Material = null!;
        public bool Receiver;
        public MeshRenderer First = null!;
        public Vector4[] Data = new Vector4[Stride * 4];
        public int Count;
        public int Base;
        public int LastFrame;
        public float Nearest;
    }

    private readonly record struct Key(GpuMesh Mesh, Material Material, bool Receiver);

    private static readonly Dictionary<Key, Group> Groups = new();
    private static readonly List<Group> Order = new();
    private static readonly List<Key> Stale = new();
    private static Vector4[] _upload = new Vector4[Stride * 256];
    private static ShaderStorage? _storage;
    private static int _frame;

    static InstanceBatcher() => Engine.GpuContextStarted += ForgetContext;

    private static void ForgetContext()
    {
        Groups.Clear();
        Order.Clear();
        _storage = null;
    }
    private static float[] _keys = new float[64];
    private static int[] _byKey = new int[64];
    private static Vector4[] _sorted = new Vector4[Stride * 64];
    private static readonly Comparison<Group> NearestFirst = static (a, b) => a.Nearest.CompareTo(b.Nearest);

    public static void Defer(MeshRenderer renderer, GpuMesh mesh, Material material, bool receiver, in Matrix4x4 world, ReadOnlySpan<Vector4> light)
    {
        var key = new Key(mesh, material, receiver);
        if (!Groups.TryGetValue(key, out var g)) Groups[key] = g = new Group { Mesh = mesh, Material = material, Receiver = receiver };
        if (g.Count == 0)
        {
            Order.Add(g);
            g.First = renderer;
        }
        int at = g.Count * Stride;
        if (at + Stride > g.Data.Length) Array.Resize(ref g.Data, g.Data.Length * 2);
        var d = g.Data.AsSpan(at, Stride);
        d[0] = new Vector4(world.M11, world.M12, world.M13, world.M14);
        d[1] = new Vector4(world.M21, world.M22, world.M23, world.M24);
        d[2] = new Vector4(world.M31, world.M32, world.M33, world.M34);
        d[3] = new Vector4(world.M41, world.M42, world.M43, world.M44);
        light[..7].CopyTo(d[4..]);
        g.Count++;
    }

    public static void Flush(GL gl)
    {
        _frame++;
        Batches = Instanced = 0;
        if (Order.Count == 0) { Prune(); return; }
        if (FrontToBack) SortNearestFirst();
        int total = 0;
        foreach (var g in Order)
            if (g.Count > 1)
            {
                g.Base = total;
                total += g.Count * Stride;
            }
        if (total > 0 && RenderPipeline.Instance is { } pipeline)
        {
            if (total > _upload.Length) Array.Resize(ref _upload, Math.Max(total, _upload.Length * 2));
            foreach (var g in Order)
                if (g.Count > 1) g.Data.AsSpan(0, g.Count * Stride).CopyTo(_upload.AsSpan(g.Base));
            (_storage ??= new ShaderStorage(gl)).Upload(_upload.AsSpan(0, total), Binding);
            var sh = pipeline.Shader;
            var u = sh.Cached(static s => new Uniforms(s)); // Amedo 2026-10-01
            foreach (var g in Order)
            {
                if (g.Count == 1) { g.First.DrawNow(gl, WorldOf(g)); continue; } // Amedo 2026-10-01
                g.Material.Apply(gl, sh);
                Material.SetSelectPulse(sh, 1f); // Amedo 2026-10-01
                sh.Set(u.UseInstances, 1);
                sh.Set(u.InstanceBase, g.Base);
                if (g.Receiver) GlState.StencilFunc(gl, StencilFunction.Always, 0, 0xFF);
                g.Mesh.DrawInstanced(gl, g.Count);
                if (g.Receiver) GlState.StencilFuncLater(gl, StencilFunction.Always, GameShadowPass.ReceiverBit, 0xFF);
                sh.SetLater(u.UseInstances, 0); // Amedo 2026-10-01
                g.Material.Restore(gl, sh);
                MeshRenderer.DrawCallsThisFrame++;
                Batches++;
                Instanced += g.Count;
            }
        }
        else
        {
            foreach (var g in Order)
                if (g.Count == 1) g.First.DrawNow(gl, WorldOf(g)); // Amedo 2026-10-01
        }
        foreach (var g in Order)
        {
            g.Count = 0;
            g.LastFrame = _frame;
            g.First = null!;
        }
        Order.Clear();
        Prune();
    }

    // Amedo 2026-10-01
    private sealed class Uniforms(TwinShaderProgram sh)
    {
        public readonly TwinShaderProgram.Slot UseInstances = sh.Uniform("UseInstances");
        public readonly TwinShaderProgram.Slot InstanceBase = sh.Uniform("InstanceBase");
    }

    private static void SortNearestFirst()
    {
        if (RenderPipeline.Instance?.Camera is not { } camera || !Matrix4x4.Invert(camera.View, out var inverse)) return;
        var eye = inverse.Translation;
        for (int o = 0; o < Order.Count; o++)
        {
            var g = Order[o];
            var center = g.Mesh.LocalBounds(out var min, out var max) ? (min + max) * 0.5f : Vector3.Zero;
            if (_keys.Length < g.Count)
            {
                _keys = new float[g.Count * 2];
                _byKey = new int[g.Count * 2];
            }
            for (int i = 0; i < g.Count; i++)
            {
                var d = g.Data.AsSpan(i * Stride, 4);
                var at = new Vector3(d[3].X + center.X * d[0].X + center.Y * d[1].X + center.Z * d[2].X,
                                     d[3].Y + center.X * d[0].Y + center.Y * d[1].Y + center.Z * d[2].Y,
                                     d[3].Z + center.X * d[0].Z + center.Y * d[1].Z + center.Z * d[2].Z);
                _keys[i] = GameMath.LengthSquared(at - eye);
                _byKey[i] = i;
            }
            Array.Sort(_keys, _byKey, 0, g.Count);
            g.Nearest = _keys[0];
            if (g.Count < 2) continue;
            int length = g.Count * Stride;
            if (_sorted.Length < length) _sorted = new Vector4[length * 2];
            for (int i = 0; i < g.Count; i++) g.Data.AsSpan(_byKey[i] * Stride, Stride).CopyTo(_sorted.AsSpan(i * Stride));
            _sorted.AsSpan(0, length).CopyTo(g.Data);
        }
        Order.Sort(NearestFirst);
    }

    // Amedo 2026-10-01
    private static Matrix4x4 WorldOf(Group g)
    {
        var d = g.Data;
        return new Matrix4x4(d[0].X, d[0].Y, d[0].Z, d[0].W,
                             d[1].X, d[1].Y, d[1].Z, d[1].W,
                             d[2].X, d[2].Y, d[2].Z, d[2].W,
                             d[3].X, d[3].Y, d[3].Z, d[3].W);
    }

    private static void Prune()
    {
        if (_frame % 300 != 0) return;
        Stale.Clear();
        foreach (var (key, g) in Groups)
            if (_frame - g.LastFrame > 300) Stale.Add(key);
        foreach (var key in Stale) Groups.Remove(key);
    }
}
