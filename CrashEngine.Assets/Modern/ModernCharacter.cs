using System.Numerics;

namespace CrashEngine.Assets.Modern;

// Amedo 2026-11-18
public sealed class ModernJoint
{
    public required string Name;
    public int Parent = -1;
    public Vector3 Translation;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3 Scale = Vector3.One;
    public Matrix4x4 InverseBind = Matrix4x4.Identity;

    public Matrix4x4 RestLocal => Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Translation);
}

// Amedo 2026-11-18
public sealed class ModernMesh
{
    public required string Name;
    public int Material = -1;
    public required Vector3[] Positions;
    public required Vector3[] Normals;
    public required Vector4[] Tangents;
    public required Vector2[] Uv;
    public Vector4[]? Colors;
    public int Influences;
    public required ushort[] Joints;
    public required float[] Weights;
    public required uint[] Indices;
}

// Amedo 2026-11-18
public enum ModernAlpha { Opaque, Mask, Blend }

// Amedo 2026-11-18
public sealed class ModernMaterial
{
    public required string Name;
    public Vector4 BaseColor = Vector4.One;
    public int BaseColorTexture = -1;
    public float Metallic = 1f;
    public float Roughness = 1f;
    public int MetallicRoughnessTexture = -1;
    public int NormalTexture = -1;
    public float NormalScale = 1f;
    public int OcclusionTexture = -1;
    public float OcclusionStrength = 1f;
    public Vector3 Emissive;
    public int EmissiveTexture = -1;
    public ModernAlpha Alpha;
    public float AlphaCutoff = 0.5f;
    public bool DoubleSided;
    public bool Unlit;
}

// Amedo 2026-11-18
public sealed class ModernTexture
{
    public required string Name;
    public required byte[] Encoded;
    public required string MimeType;
}

// Amedo 2026-11-18
public enum ModernChannelPath { Translation, Rotation, Scale }

// Amedo 2026-11-18
public enum ModernInterpolation { Linear, Step, CubicSpline }

// Amedo 2026-11-18
public sealed class ModernChannel
{
    public int Joint;
    public ModernChannelPath Path;
    public ModernInterpolation Interpolation;
    public required float[] Times;
    public required Vector4[] Values;
}

// Amedo 2026-11-18
public sealed class ModernClip
{
    public required string Name;
    public float Duration;
    public readonly List<ModernChannel> Channels = new();
}

// Amedo 2026-11-18
public sealed class ModernCharacter
{
    public readonly List<ModernJoint> Joints = new();
    public readonly List<ModernMesh> Meshes = new();
    public readonly List<ModernMaterial> Materials = new();
    public readonly List<ModernTexture> Textures = new();
    public readonly List<ModernClip> Clips = new();
    public readonly List<string> Notes = new();

    public int VertexCount => Meshes.Sum(m => m.Positions.Length);
    public int TriangleCount => Meshes.Sum(m => m.Indices.Length / 3);
}
