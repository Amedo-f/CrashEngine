using CrashEngine.Core;
using System.Numerics;

namespace CrashEngine.Assets.Modern;

// Amedo 2026-11-18
public sealed class ModernPose
{
    private readonly ModernCharacter _character;
    private readonly Vector3[] _translation;
    private readonly Quaternion[] _rotation;
    private readonly Vector3[] _scale;
    private Vector3[] _fromTranslation = [];
    private Quaternion[] _fromRotation = [];
    private Vector3[] _fromScale = [];
    public readonly Matrix4x4[] World;
    public readonly Matrix4x4[] Skin;

    public ModernPose(ModernCharacter character)
    {
        _character = character;
        int n = character.Joints.Count;
        _translation = new Vector3[n];
        _rotation = new Quaternion[n];
        _scale = new Vector3[n];
        World = new Matrix4x4[n];
        Skin = new Matrix4x4[n];
        Rest();
    }

    public void Rest()
    {
        RestLocal(_translation, _rotation, _scale);
        Compose();
    }

    public void Sample(ModernClip clip, float time)
    {
        SampleLocal(clip, time, _translation, _rotation, _scale);
        Compose();
    }

    public void Blend(ModernClip? from, float fromTime, ModernClip? to, float toTime, float weight)
    {
        int n = _character.Joints.Count;
        if (_fromTranslation.Length != n)
        {
            _fromTranslation = new Vector3[n];
            _fromRotation = new Quaternion[n];
            _fromScale = new Vector3[n];
        }
        if (from is null) RestLocal(_fromTranslation, _fromRotation, _fromScale);
        else SampleLocal(from, fromTime, _fromTranslation, _fromRotation, _fromScale);
        if (to is null) RestLocal(_translation, _rotation, _scale);
        else SampleLocal(to, toTime, _translation, _rotation, _scale);
        for (int j = 0; j < n; j++)
        {
            _translation[j] = Vector3.Lerp(_fromTranslation[j], _translation[j], weight);
            _rotation[j] = Quaternion.Slerp(_fromRotation[j], _rotation[j], weight);
            _scale[j] = Vector3.Lerp(_fromScale[j], _scale[j], weight);
        }
        Compose();
    }

    private void RestLocal(Vector3[] translation, Quaternion[] rotation, Vector3[] scale)
    {
        var joints = _character.Joints;
        for (int j = 0; j < joints.Count; j++)
        {
            translation[j] = joints[j].Translation;
            rotation[j] = joints[j].Rotation;
            scale[j] = joints[j].Scale;
        }
    }

    private void SampleLocal(ModernClip clip, float time, Vector3[] translation, Quaternion[] rotation, Vector3[] scale)
    {
        RestLocal(translation, rotation, scale);
        foreach (var channel in clip.Channels)
        {
            var v = Evaluate(channel, time);
            switch (channel.Path)
            {
                case ModernChannelPath.Translation: translation[channel.Joint] = new Vector3(v.X, v.Y, v.Z); break;
                case ModernChannelPath.Rotation: rotation[channel.Joint] = Quaternion.Normalize(new Quaternion(v.X, v.Y, v.Z, v.W)); break;
                case ModernChannelPath.Scale: scale[channel.Joint] = new Vector3(v.X, v.Y, v.Z); break;
            }
        }
    }

    private void Compose()
    {
        var joints = _character.Joints;
        for (int j = 0; j < joints.Count; j++)
        {
            var local = Matrix4x4.CreateScale(_scale[j]) * Matrix4x4.CreateFromQuaternion(_rotation[j]) * Matrix4x4.CreateTranslation(_translation[j]);
            World[j] = joints[j].Parent >= 0 ? local * World[joints[j].Parent] : local;
            Skin[j] = joints[j].InverseBind * World[j];
        }
    }

    public static Vector4 Evaluate(ModernChannel channel, float time)
    {
        var times = channel.Times;
        bool cubic = channel.Interpolation == ModernInterpolation.CubicSpline;
        int stride = cubic ? 3 : 1, offset = cubic ? 1 : 0;
        if (times.Length == 1 || time <= times[0]) return channel.Values[offset];
        if (time >= times[^1]) return channel.Values[(times.Length - 1) * stride + offset];
        int hi = Array.BinarySearch(times, time);
        if (hi >= 0) return channel.Values[hi * stride + offset];
        hi = ~hi;
        int lo = hi - 1;
        float dt = times[hi] - times[lo];
        float t = dt > 0f ? (time - times[lo]) / dt : 0f;
        bool rotation = channel.Path == ModernChannelPath.Rotation;
        switch (channel.Interpolation)
        {
            case ModernInterpolation.Step:
                return channel.Values[lo];
            case ModernInterpolation.CubicSpline:
            {
                float t2 = t * t, t3 = t2 * t;
                var v = channel.Values[lo * 3 + 1] * (2f * t3 - 3f * t2 + 1f)
                      + channel.Values[lo * 3 + 2] * (dt * (t3 - 2f * t2 + t))
                      + channel.Values[hi * 3 + 1] * (-2f * t3 + 3f * t2)
                      + channel.Values[hi * 3] * (dt * (t3 - t2));
                return rotation ? Vector4.Normalize(v) : v;
            }
            default:
                if (!rotation) return Vector4.Lerp(channel.Values[lo], channel.Values[hi], t);
                var a = channel.Values[lo];
                var b = channel.Values[hi];
                var q = Quaternion.Slerp(new Quaternion(a.X, a.Y, a.Z, a.W), new Quaternion(b.X, b.Y, b.Z, b.W), t);
                return new Vector4(q.X, q.Y, q.Z, q.W);
        }
    }

    public Vector3 SkinPosition(ModernMesh mesh, int vertex)
    {
        int influences = ModernCharacterImporter.MaxInfluences;
        var p = mesh.Positions[vertex];
        var result = Vector3.Zero;
        float total = 0f;
        for (int k = 0; k < influences; k++)
        {
            float w = mesh.Weights[vertex * influences + k];
            if (w <= 0f) continue;
            result += Vector3.Transform(p, Skin[mesh.Joints[vertex * influences + k]]) * w;
            total += w;
        }
        return total > 0f ? result : p;
    }
}
