using System.Numerics;
using CrashEngine.Runtime.AgentLab;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-29
public abstract class SubCamera
{
    public const uint KindPoint = 0x1C02, KindLine = 0x1C03, KindPath = 0x1C04, KindSpline = 0x1C06, KindPoint2 = 0x1C0B,
                      KindOrbit = 0x1C0C, KindLine2 = 0x1C0D, KindSpecial = 0x1C0E, KindZone = 0x1C0F, KindBoss = 0xA19; // Amedo

    public uint Kind { get; }
    public float Lead { get; }

    protected SubCamera(uint kind, float lead)
    {
        Kind = kind;
        Lead = lead;
    }

    public abstract float Compute_14(GameCamera cam, uint target, uint x, uint output);
    public abstract void At_1C(GameCamera cam, float t, uint output);
    public virtual void Enter_24(GameCamera cam, uint eye, uint target) { }

    public static SubCamera Create(CameraSubBase sub)
    {
        uint kind = (uint)sub.GetCameraType();
        return sub switch
        {
            CameraPoint p  => new PointCamera(V(p.Point), p.UnkFloat2),
            CameraLine l   => new LineCamera(V(l.LineStart), V(l.LineEnd), l.UnkFloat2),
            CameraPath p   => PathCamera.From(p) is { } path ? path : new UnportedCamera(kind, "path without spline data"),
            CameraPoint2 p => new Point2Camera(V(p.Point), p.UnkFloat3, p.UnkByte, p.UnkFloat2),
            CameraSpline s => SplineCamera.From(s) is { } spline ? spline : new UnportedCamera(kind, "spline without points"), // Amedo 2026-09-29
            CameraLine2 l  => new Line2Camera(V(l.LineStart), V(l.LineEnd), l.UnkFloat3, l.UnkFloat4, l.UnkFloat2), // Amedo 2026-10-02
            BossCamera b   => new BossAreaCamera(b),
            CameraZone z   => new ZoneCamera(z),
            CameraSub1C0C  => new UnportedCamera(kind, "orbit camera from level data: 0x27D1D0 reads 4 bytes, the fields 0x10..0x40 are unset"), // Amedo
            _              => new UnportedCamera(kind, null),
        };
    }

    internal static Vector4 V(Twinsanity.TwinsanityInterchange.Common.Vector4 v) => new(v.X, v.Y, v.Z, v.W); // Amedo 2026-10-02

    // Amedo 2026-10-02
    internal static Matrix4x4 M(Twinsanity.TwinsanityInterchange.Common.Matrix4 m) =>
        new(m.Column1.X, m.Column1.Y, m.Column1.Z, m.Column1.W, m.Column2.X, m.Column2.Y, m.Column2.Z, m.Column2.W,
            m.Column3.X, m.Column3.Y, m.Column3.Z, m.Column3.W, m.Column4.X, m.Column4.Y, m.Column4.Z, m.Column4.W);

    public static Vector4 Lerp_18C948(float k, Vector4 a, Vector4 b)
    {
        if (k == 0f) return a;
        if (k == 1f) return b;
        return new Vector4((b.X - a.X) * k + a.X, (b.Y - a.Y) * k + a.Y, (b.Z - a.Z) * k + a.Z, b.W);
    }

    public static float Project_184260(Vector4 a, Vector4 b, Vector4 p)
    {
        float n = ((b.X * p.X + b.Y * p.Y + b.Z * p.Z) - (a.X * p.X + a.Y * p.Y + a.Z * p.Z))
                - ((b.X * a.X + b.Y * a.Y + b.Z * a.Z) - (a.X * a.X + a.Y * a.Y + a.Z * a.Z));
        if (n * n == 0f) return 0f;
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return n / (dx * dx + dy * dy + dz * dz);
    }

    public static float InvLength_11C030(float eps, Vector4 v)
    {
        float sq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        return sq <= eps ? 0f : 1f / MathF.Sqrt(sq);
    }

    protected static float Clamp01(float t) => 1f < t ? 1f : t < 0f ? 0f : t;
}

public sealed class PointCamera : SubCamera
{
    private readonly Vector4 _point;

    public PointCamera(Vector4 point, float lead) : base(KindPoint, lead) => _point = point;

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        cam.M.V(output, _point);
        return 1f;
    }

    public override void At_1C(GameCamera cam, float t, uint output) => cam.M.V(output, _point);
}

public sealed class LineCamera : SubCamera
{
    private readonly Vector4 _a, _b;

    public LineCamera(Vector4 a, Vector4 b, float lead) : base(KindLine, lead)
    {
        _a = a;
        _b = b;
    }

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        float t = Project_184260(_a, _b, cam.M.V(target));
        At_1C(cam, t, output);
        return t;
    }

    public override void At_1C(GameCamera cam, float t, uint output)
    {
        float dx = _a.X - _b.X, dy = _a.Y - _b.Y, dz = _a.Z - _b.Z;
        t += Lead / MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        cam.M.V(output, Lerp_18C948(Clamp01(t), _a, _b));
    }
}

public sealed class PathCamera : SubCamera
{
    private readonly PathSpline _path;

    private PathCamera(PathSpline path, float lead) : base(KindPath, lead) => _path = path;

    public static PathCamera? From(CameraPath p)
    {
        var points = new Vector3[p.PathPoints.Count];
        for (int i = 0; i < points.Length; i++) points[i] = new Vector3(p.PathPoints[i].X, p.PathPoints[i].Y, p.PathPoints[i].Z);
        var flat = new float[p.UnkData.Count * 2];
        for (int i = 0; i < p.UnkData.Count; i++)
        {
            flat[2 * i]     = BitConverter.UInt32BitsToSingle((uint)p.UnkData[i]);
            flat[2 * i + 1] = BitConverter.UInt32BitsToSingle((uint)(p.UnkData[i] >> 32));
        }
        return PathSpline.From(points, flat) is { } spline ? new PathCamera(spline, p.UnkFloat2) : null;
    }

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        var p = cam.M.V(target);
        float t = _path.Closest(new Vector3(p.X, p.Y, p.Z), out _);
        At_1C(cam, t, output);
        return t;
    }

    public override void At_1C(GameCamera cam, float t, uint output)
    {
        t += Lead / _path.TotalLength;
        var p = _path.PositionAt_188CB0(!(t <= 1f) ? 1f : t < 0f ? 0f : t);
        cam.M.V(output, new Vector4(p, 1f));
    }
}

public sealed class Point2Camera : SubCamera
{
    private static readonly float Eps = BitConverter.Int32BitsToSingle(0x312BCC76);
    private readonly Vector4 _point;
    private readonly float _k;
    private readonly int _mode;

    public Point2Camera(Vector4 point, float k, byte mode, float lead) : base(KindPoint2, lead)
    {
        _point = point;
        _k = k;
        _mode = mode;
    }

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        var t = cam.M.V(target);
        if (_mode == 0) cam.M.V(output, Lerp_18C948(_k, t, _point));
        else if (_mode is 1 or 2)
        {
            var d = new Vector4(_point.X - t.X, _point.Y - t.Y, _point.Z - t.Z, _point.W);
            float inv = InvLength_11C030(Eps, d);
            var o = new Vector4(d.X * inv * _k + t.X, d.Y * inv * _k + t.Y, d.Z * inv * _k + t.Z, _point.W);
            if (_mode == 2 && 1f < Project_184260(t, _point, o)) o = _point;
            cam.M.V(output, o);
        }
        return 1f;
    }

    public override void At_1C(GameCamera cam, float t, uint output) => cam.M.V(output, _point);
}

// Amedo 2026-09-29
public sealed class SplineCamera : SubCamera
{
    private static readonly float Golden = BitConverter.Int32BitsToSingle(0x3EC3910C);
    private static readonly float Tol = BitConverter.Int32BitsToSingle(0x3851B717);
    private static readonly float Shrink = BitConverter.Int32BitsToSingle(0x3F7F7CEE);
    private static readonly float Far = BitConverter.Int32BitsToSingle(0x7149F2CA);
    private static readonly float LeadStep = BitConverter.Int32BitsToSingle(0x3AC80000);
    private static readonly float WeightStep = BitConverter.Int32BitsToSingle(0x3D200000);

    private readonly int _n;
    private readonly float _len, _invLen;
    private readonly Vector3[] _p;
    private readonly uint[] _w;
    private readonly float[] _lengths, _steps;
    private readonly uint _flags;

    private Vector3 _target, _cur;
    private float _bestDist, _bestU;
    private int _seg = -1;

    private Vector3 _fd0, _fd1, _fd2, _fd3;
    private Vector3 _hitPoint;
    private float _hitDist, _hitU;
    private int _hitSeg;

    private SplineCamera(int n, float len, Vector3[] p, uint[] w, float[] lengths, float[] steps, uint flags, float lead)
        : base(KindSpline, lead)
    {
        _n = n;
        _len = len;
        _invLen = 1f / len;
        _p = p;
        _w = w;
        _lengths = lengths;
        _steps = steps;
        _flags = flags;
    }

    public static SplineCamera? From(CameraSpline s)
    {
        int n = s.UnkData.Count;
        if (n < 1 || s.PathPoints.Count != n + 1 || s.InterpolationPoints.Count != n + 1) return null;
        var p = new Vector3[2 * n + 2];
        var w = new uint[2 * n + 2];
        for (int i = 0; i <= n; i++)
        {
            var a = s.PathPoints[i];
            var b = s.InterpolationPoints[i];
            p[2 * i] = new Vector3(a.X, a.Y, a.Z);
            w[2 * i] = BitConverter.SingleToUInt32Bits(a.W);
            p[2 * i + 1] = new Vector3(b.X, b.Y, b.Z);
            w[2 * i + 1] = BitConverter.SingleToUInt32Bits(b.W);
        }
        var flat = new float[2 * n];
        for (int i = 0; i < n; i++)
        {
            flat[2 * i] = s.UnkData[i].X;
            flat[2 * i + 1] = s.UnkData[i].Y;
        }
        var lengths = new float[n];
        var steps = new float[n];
        Array.Copy(flat, 0, lengths, 0, n);
        Array.Copy(flat, n, steps, 0, n);
        return new SplineCamera(n, s.UnkFloat3, p, w, lengths, steps, s.UnkShort, s.UnkFloat2);
    }

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        var tv = cam.M.V(target);
        _hitSeg = -1;
        Closest_18A7E8(new Vector3(tv.X, tv.Y, tv.Z));
        float t = (_hitU + _hitSeg) / _n;
        At_1C(cam, t, output);
        float t2 = t + Lead_279728(t) / _lengths[_n - 1];
        if (1f < t2) t2 = 1f;
        else if (t2 < 0f) t2 = 0f;
        Weight_279520(cam, t2, target, output);
        return t;
    }

    public override void At_1C(GameCamera cam, float t, uint output)
    {
        t += Lead_279728(t) / _lengths[_n - 1];
        if (1f < t) t = 1f;
        else if (t < 0f) t = 0f;
        int i = Segment_18E958(t * (_n * _len), out float s);
        cam.M.V(output, new Vector4(Eval_18A018(s, i), 1f));
    }

    private int Segment_18E958(float d, out float s)
    {
        s = 0f;
        if (d <= 0f) return 0;
        float l = _len;
        if (l * _n <= d)
        {
            s = l;
            return _n - 1;
        }
        if (_n < 1) return 0;
        if (d < l)
        {
            s = d;
            return 0;
        }
        float prev, next = l, fj;
        int k = 0, j = 2, idx;
        do
        {
            prev = next;
            fj = j;
            idx = k + 1;
            if (_n <= idx) return 0;
            j = k + 3;
            next = l * fj;
            k = idx;
        } while (l * fj <= d);
        s = d - prev;
        return idx;
    }

    private Vector3 Eval_18A018(float s, int i)
    {
        if (_len < s) s = _len;
        float u = s * _invLen;
        float u2 = u * u;
        float u3 = u2 * u;
        float h11 = u3 - u2;
        float c = (u3 + u3) - u2 * 3f;
        float h10 = (h11 - u2) + u;
        float h01 = -c;
        float h00 = c + 1f;
        return Blend_18A228(2 * i, h00, h10, h01, h11);
    }

    private Vector3 Blend_18A228(int k, float h00, float h10, float h01, float h11)
    {
        var r = _p[k] * h00;
        r = new Vector3(r.X + _p[k + 1].X * h10, r.Y + _p[k + 1].Y * h10, r.Z + _p[k + 1].Z * h10);
        r = new Vector3(r.X + _p[k + 2].X * h01, r.Y + _p[k + 2].Y * h01, r.Z + _p[k + 2].Z * h01);
        return new Vector3(r.X + _p[k + 3].X * h11, r.Y + _p[k + 3].Y * h11, r.Z + _p[k + 3].Z * h11);
    }

    private static float Dist2(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    private void Closest_18A7E8(Vector3 target)
    {
        _hitDist = Far;
        int best = 0;
        float bestD = Dist2(_p[0], target);
        for (int i = 2; i < _n * 2 + 2; i += 2)
        {
            float d = Dist2(_p[i], target);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        int start = best / 2 - 1, count = 2;
        if (start < 0)
        {
            count = best / 2 + 1;
            start = 0;
        }
        if (_n < start + count) count -= start + count - _n;
        for (int c = 0; c < count; c++, start++)
            if (SegmentClosest_18A350(target, start)) _hitSeg = start;
    }

    private bool SegmentClosest_18A350(Vector3 target, int seg)
    {
        bool found = false;
        float h = _steps[seg];
        Differences_18A980(h, 2 * seg);
        _target = target;
        _cur = _fd0;
        _seg = seg;
        _bestU = 0f;
        _bestDist = Dist2(target, _fd0);
        var end = _p[2 * seg + 2];
        float dEnd = Dist2(target, end);
        if (dEnd < _bestDist)
        {
            _cur = end;
            _bestDist = dEnd;
            _bestU = 1f;
        }
        float u = 0f;
        do
        {
            var cur = _fd0;
            float d = Dist2(target, cur);
            float next;
            if (0f < u && d < _bestDist * Shrink)
            {
                float x = u, fx = d;
                Brent_18EC40(ref x, ref fx);
                next = u + h;
                float x2 = x * x;
                float x3 = x2 * x;
                float h11 = x3 - x2;
                float c = (x3 + x3) - x2 * 3f;
                float h10 = (h11 - x2) + x;
                cur = Blend_18A228(2 * seg, c + 1f, h10, -c, h11);
                _bestU = x;
                _cur = cur;
                _bestDist = fx;
                if (fx < _hitDist)
                {
                    _hitPoint = cur;
                    found = true;
                    _hitDist = _bestDist;
                    _hitU = _bestU;
                }
            }
            else
            {
                next = u + h;
                if (d < _hitDist)
                {
                    found = true;
                    _hitDist = d;
                    _hitPoint = cur;
                    _hitU = u;
                }
            }
            _fd0 += _fd1;
            _fd1 += _fd2;
            _fd2 += _fd3;
            u = next;
        } while (u < 1f);
        return found;
    }

    private void Differences_18A980(float h, int k)
    {
        Vector3 p0 = _p[k], t0 = _p[k + 1], p1 = _p[k + 2], t1 = _p[k + 3];
        var sum = t0 + t1;
        var d = p0 - p1;
        var a = (d + d) + sum;
        var b = d * -3f - (t0 + sum);
        float h2 = h * h;
        float h3 = h2 * h;
        var ah3 = a * h3;
        var bh2 = b * h2;
        var ch = t0 * h;
        _fd0 = p0;
        _fd1 = (ah3 + bh2) + ch;
        _fd3 = ah3 * 6f;
        _fd2 = _fd3 + (bh2 + bh2);
    }

    private float Distance_18A6D8(float u)
    {
        float u2 = u * u;
        float u3 = u2 * u;
        float h11 = u3 - u2;
        float c = (u3 + u3) - u2 * 3f;
        float h10 = (h11 - u2) + u;
        var p = Blend_18A228(2 * _seg, c + 1f, h10, -c, h11);
        return Dist2(_target, p);
    }

    private int Brent_18EC40(ref float x, ref float fx)
    {
        const int itmax = 4;
        float a = 0f, b = 1f;
        float v = x, w = x, fv = fx, fw = fx;
        float d = 0f, e = 0f;
        for (int iter = 1; ; )
        {
            float tol = Tol * MathF.Abs(x);
            if (tol < Tol) tol = Tol;
            float xm = (a + b) * 0.5f;
            if (MathF.Abs(x - xm) < tol - (b - a) * 0.5f) return 0;
            float tol1 = tol * 0.5f;
            float p = 0f, q = 0f, etemp = 0f;
            if (tol1 < MathF.Abs(e))
            {
                float r = (x - w) * (fx - fv);
                q = (x - v) * (fx - fw);
                p = (x - v) * q - (x - w) * r;
                float qr = q - r;
                q = qr + qr;
                if (0f < q) p = -p;
                q = MathF.Abs(q);
                etemp = e;
                e = d;
            }
            float u;
            if (MathF.Abs(p) < MathF.Abs(q * etemp * 0.5f) && q * (a - x) < p && p < q * (b - x))
            {
                d = p / q;
                u = x + d;
                if (u - a < tol || b - u < tol) d = xm <= x ? -tol1 : tol1;
            }
            else
            {
                e = x < xm ? b - x : a - x;
                d = e * Golden;
            }
            if (tol1 <= MathF.Abs(d)) u = x + d;
            else if (d < 0f) u = x - tol1;
            else u = x + tol1;
            float fu = Distance_18A6D8(u);
            if (fu <= fx)
            {
                if (u < x) b = x;
                else a = x;
                v = w;
                fv = fw;
                w = x;
                fw = fx;
                x = u;
                fx = fu;
            }
            else
            {
                if (u < x) a = u;
                else b = u;
                if (fu <= fw || w == x)
                {
                    v = w;
                    fv = fw;
                    w = u;
                    fw = fu;
                }
                else if (fu <= fv || v == x || v == w)
                {
                    v = u;
                    fv = fw;
                }
            }
            iter++;
            if (itmax < iter) return -1;
        }
    }

    private float Lead_279728(float t)
    {
        if ((_flags & 1) != 0) return Lead;
        float f = t * _n;
        int k = (int)f;
        if (f < k) k--;
        if (!(k < _n)) return 0f;
        uint pj = 0, nj = 0;
        float prev = LeadOf_27D160(Find_27C9D8(2 * k, true, ref pj));
        float next = LeadOf_27D160(Find_27C9D8(2 * k, false, ref nj));
        return Interpolate(t, prev, pj, next, nj);
    }

    private void Weight_279520(GameCamera cam, float t, uint target, uint output)
    {
        float f = t * _n;
        int k = (int)f;
        if (f < k) k--;
        if (!(k < _n)) return;
        uint pj = 0, nj = 0;
        float prev = WeightOf_27D108(Find_27C9D8(2 * k, true, ref pj));
        float next = WeightOf_27D108(Find_27C9D8(2 * k, false, ref nj));
        float kw = Interpolate(t, prev, pj, next, nj);
        cam.M.V(output, Lerp_18C948(kw, cam.M.V(output), cam.M.V(target)));
    }

    private float Interpolate(float t, float prev, uint pj, float next, uint nj)
    {
        float count = _n * 2 + 2;
        float tn = Index(nj) / count;
        float tp = Index(pj) / count;
        float s = t - tp;
        if (s < 0f) s = 0f;
        return (next - prev) * (s / (tn - tp)) + prev;
    }

    private static float Index(uint j) => (int)j < 0 ? (float)(int)((j >> 1) | (j & 1)) * 2f : (int)j;

    private uint Find_27C9D8(int i, bool backward, ref uint j)
    {
        uint w = 0;
        uint count = (uint)(_n * 2 + 2);
        uint idx = backward ? (uint)i : (uint)i + 2;
        uint step = backward ? unchecked((uint)-2) : 2u;
        while (idx < count)
        {
            w = _w[idx];
            if (((w >> 24) & 1) == 0)
            {
                j = idx;
                return w;
            }
            idx = unchecked(idx + step);
        }
        return w;
    }

    private static float LeadOf_27D160(uint w) => (float)((w >> 8) & 0xFFFF) * LeadStep + -50f;

    private static float WeightOf_27D108(uint w) => (float)(w & 0xFF) * WeightStep + -5f;
}

// Amedo 2026-10-02
public sealed class Line2Camera : SubCamera
{
    private readonly Vector4 _a, _b;
    private readonly float _near, _far;

    public Line2Camera(Vector4 a, Vector4 b, float near, float far, float lead) : base(KindLine2, lead)
    {
        _a = a;
        _b = b;
        _near = near;
        _far = far;
    }

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        var p = cam.M.V(x + 0x40);
        float dx = p.X - _a.X, dz = p.Z - _a.Z;
        float d = MathF.Sqrt(dx * dx + dz * dz);
        float t = 0f;
        if (_far < d) t = 1f;
        else if (!(d < _near)) t = (d - _near) / (_far - _near) + _near;
        At_1C(cam, t, output);
        return t;
    }

    public override void At_1C(GameCamera cam, float t, uint output) => cam.M.V(output, Lerp_18C948(t, _a, _b));
}

// Amedo 2026-10-02
public sealed class BossAreaCamera : SubCamera
{
    private static readonly float Eps = BitConverter.Int32BitsToSingle(0x312BCC76);
    private static readonly float TurnEps = BitConverter.Int32BitsToSingle(0x3851B717);
    private readonly Matrix4x4 _toLocal, _toWorld;
    private readonly float _top, _distance, _height;
    private readonly bool _scaled, _sphere;
    private readonly float _pull, _heightCenter, _heightEdge, _turn;
    private Vector4 _prev;

    public BossAreaCamera(BossCamera b) : base(KindBoss, b.UnkFloat2)
    {
        _toLocal = M(b.UnkMatrix1);
        _toWorld = M(b.UnkMatrix2);
        _top = b.UnkVector.X;
        _distance = b.UnkVector.Y;
        _height = b.UnkVector.Z;
        _scaled = b.UnkByte1 != 0;
        _pull = b.UnkFloat3;
        _heightCenter = b.UnkFloat4;
        _heightEdge = b.UnkFloat5;
        _turn = b.UnkFloat6;
        _sphere = b.UnkByte2 != 0;
    }

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        var local = CamMath.Transform_18E4F0(_toLocal, cam.M.V(x + 0x40));
        if (!(0f < local.Y) || !(local.Y < _top))
        {
            cam.M.V(output, cam.M.V(target));
            return 0f;
        }
        float y = _scaled ? local.Y + Height_27C700(local) : local.Y + _height;
        var dir = new Vector4(local.X, 0f, local.Z, local.W);
        float inv = InvLength_11C030(Eps, dir);
        dir = new Vector4(dir.X * inv, dir.Y * inv, dir.Z * inv, dir.W);
        float dist = _scaled ? Distance_27C6A0(local) : _distance;
        var o = Turn_2792A0(new Vector4(dir.X * dist, y, dir.Z * dist, 1f), dist);
        cam.M.V(output, CamMath.Transform_18E4F0(_toWorld, o));
        return 0f;
    }

    public override void At_1C(GameCamera cam, float t, uint output) { }

    public override void Enter_24(GameCamera cam, uint eye, uint target) => _prev = CamMath.Transform_18E4F0(_toLocal, cam.M.V(eye));

    private float Height_27C700(Vector4 v)
    {
        float l = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return (_heightCenter - _heightEdge) * ((_distance - l) / _distance) + _heightEdge;
    }

    private float Distance_27C6A0(Vector4 v)
    {
        float l = MathF.Sqrt(_sphere ? v.X * v.X + v.Y * v.Y + v.Z * v.Z : v.X * v.X + v.Z * v.Z);
        return (_distance - l) * _pull + l;
    }

    private Vector4 Turn_2792A0(Vector4 o, float dist)
    {
        if (_turn < -TurnEps || TurnEps < _turn)
        {
            float lo = MathF.Sqrt(o.X * o.X + o.Z * o.Z), lp = MathF.Sqrt(_prev.X * _prev.X + _prev.Z * _prev.Z);
            float dot = o.X * _prev.X + o.Z * _prev.Z;
            float limit = _turn / dist;
            int angle = CamMath.Acos_18B738(dot / (lo * lp));
            if (CamMath.Trunc(limit * CamMath.RadToUnit) < angle)
            {
                float r = _turn / dist;
                var a = RotY_18BEC0(CamMath.RotY_18BBC0(CamMath.FromValue_11BFA0(r, 0)), _prev);
                var b = RotY_18BEC0(CamMath.RotY_18BBC0(CamMath.FromValue_11BFA0(-r, 0)), _prev);
                o = Distance(o, a) < Distance(o, b) ? a : b;
            }
        }
        _prev = o;
        return o;
    }

    private static float Distance(Vector4 a, Vector4 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static Vector4 RotY_18BEC0(Matrix4x4 m, Vector4 p) =>
        new(p.X * m.M11 + p.Z * m.M31, p.Y, p.X * m.M13 + p.Z * m.M33, p.W);
}

// Amedo 2026-10-02
public sealed class ZoneCamera : SubCamera
{
    private static readonly float Far = BitConverter.Int32BitsToSingle(0x7149F2CA);
    private static readonly float SegEps = BitConverter.Int32BitsToSingle(0x2EDBE6FF);

    private readonly record struct Rect(Vector4 U, Vector4 V, Vector4 O, float LenU, float LenV)
    {
        public static Rect From(Twinsanity.TwinsanityInterchange.Common.Vector4[] d) =>
            new(SubCamera.V(d[1]), SubCamera.V(d[2]), SubCamera.V(d[3]), d[4].X, d[4].Y);
    }

    private readonly Rect _place, _probe;

    public ZoneCamera(CameraZone z) : base(KindZone, 0f)
    {
        _place = Rect.From(z.UnkData1);
        _probe = Rect.From(z.UnkData2);
    }

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        float u = 0f, v = 0f;
        Project_189800(_probe, cam.M.V(x + 0x40), ref u, ref v);
        cam.M.V(output, Place_189EC8(_place, u, v));
        return u;
    }

    public override void At_1C(GameCamera cam, float t, uint output) { }

    private static Vector4 Place_189EC8(Rect r, float u, float v)
    {
        float ax = r.U.X * u * r.LenU, ay = r.U.Y * u * r.LenU, az = r.U.Z * u * r.LenU;
        float bx = r.V.X * v * r.LenV, by = r.V.Y * v * r.LenV, bz = r.V.Z * v * r.LenV;
        return new Vector4(ax + bx + r.O.X, ay + by + r.O.Y, az + bz + r.O.Z, 1f);
    }

    private static void Project_189800(Rect r, Vector4 p, ref float u, ref float v)
    {
        float dx = p.X - r.O.X, dy = p.Y - r.O.Y, dz = p.Z - r.O.Z;
        float pu = r.U.X * dx + r.U.Y * dy + r.U.Z * dz;
        float pv = r.V.X * dx + r.V.Y * dy + r.V.Z * dz;
        if (0f <= pu && pu <= r.LenU && 0f <= pv && pv <= r.LenV)
        {
            u = pu / r.LenU;
            v = pv / r.LenV;
            return;
        }
        var c0 = r.O;
        var c1 = new Vector4(r.U.X * r.LenU + r.O.X, r.U.Y * r.LenU + r.O.Y, r.U.Z * r.LenU + r.O.Z, 1f);
        var c2 = new Vector4(r.V.X * r.LenV + c1.X, r.V.Y * r.LenV + c1.Y, r.V.Z * r.LenV + c1.Z, 1f);
        var c3 = new Vector4(r.V.X * r.LenV + r.O.X, r.V.Y * r.LenV + r.O.Y, r.V.Z * r.LenV + r.O.Z, 1f);
        float best = Far;
        if (Edge(c0, c1, p, ref best, out float t)) { u = t; v = 0f; }
        if (Edge(c1, c2, p, ref best, out t)) { v = t; u = 1f; }
        if (Edge(c3, c2, p, ref best, out t)) { u = t; v = 1f; }
        if (Edge(c0, c3, p, ref best, out t)) { v = t; u = 0f; }
    }

    private static bool Edge(Vector4 a, Vector4 b, Vector4 p, ref float best, out float t)
    {
        t = 0f;
        float d = Closest_184668(a, b, p, out var c);
        if (!(d < best)) return false;
        best = d;
        t = Project_184260(a, b, c);
        t = t < 0f ? 0f : 1f < t ? 1f : t;
        return true;
    }

    private static float Closest_184668(Vector4 a, Vector4 b, Vector4 p, out Vector4 c)
    {
        float aa = a.X * a.X + a.Y * a.Y + a.Z * a.Z;
        float ba = b.X * a.X + b.Y * a.Y + b.Z * a.Z;
        float bp = b.X * p.X + b.Y * p.Y + b.Z * p.Z;
        float ap = a.X * p.X + a.Y * p.Y + a.Z * p.Z;
        float n = (bp - ap) - (ba - aa);
        if (!(0f < n))
        {
            c = a;
            return DistanceSq(p, a);
        }
        float lx = a.X - b.X, ly = a.Y - b.Y, lz = a.Z - b.Z;
        float l = lx * lx + ly * ly + lz * lz;
        if (!(n < l))
        {
            c = b;
            return DistanceSq(p, b);
        }
        float inv = SegEps < l || l < -SegEps ? 1f / l : 0f;
        float k = -inv * n;
        c = new Vector4(lx * k + a.X, ly * k + a.Y, lz * k + a.Z, a.W);
        return MathF.Abs(k * n + DistanceSq(p, a));
    }

    private static float DistanceSq(Vector4 p, Vector4 q)
    {
        float dx = p.X - q.X, dy = p.Y - q.Y, dz = p.Z - q.Z;
        return dx * dx + dy * dy + dz * dz;
    }
}

// Amedo
public sealed class OrbitCamera : SubCamera
{
    public const uint BaseTable = 0x304D38, Table = 0x304B38;
    private static readonly float Eps = BitConverter.Int32BitsToSingle(0x312BCC76);
    private readonly uint _obj;

    public OrbitCamera(uint obj) : base(KindOrbit, 0f) => _obj = obj;

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        var m = cam.M;
        var c = m.V(_obj + 0x30);
        var t = m.V(x + 0x40);
        var d = new Vector4(t.X - c.X, c.Y - c.Y, t.Z - c.Z, 1f);
        float dist = MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        float min = m.F(_obj + 0x14);
        float radius = m.F(_obj + 0x10) + (dist < min ? min : dist);
        float inv = InvLength_11C030(Eps, d);
        var eye = new Vector4(d.X * inv * radius + c.X, d.Y * inv * radius + c.Y, d.Z * inv * radius + c.Z, 1f);
        float near = m.F(_obj + 0x24), far = m.F(_obj + 0x20);
        float height;
        if (dist < near) height = m.F(_obj + 0x18);
        else if (far < dist) height = m.F(_obj + 0x1C);
        else height = (m.F(_obj + 0x1C) - m.F(_obj + 0x18)) * ((dist - near) / (far - near)) + m.F(_obj + 0x18);
        eye.Y = t.Y + height;
        m.V(output, eye);
        return 0f;
    }

    public override void At_1C(GameCamera cam, float t, uint output) { }
}

public sealed class UnportedCamera : SubCamera
{
    private readonly string _compute, _at;

    public UnportedCamera(uint kind, string? why) : base(kind, 0f)
    {
        string what = why is null ? "" : $" ({why})";
        _compute = $"sub-camera 0x{kind:X} vt+0x14{what}";
        _at = $"sub-camera 0x{kind:X} vt+0x1C{what}";
    }

    public override float Compute_14(GameCamera cam, uint target, uint x, uint output)
    {
        cam.Missing(_compute);
        return 0f;
    }

    public override void At_1C(GameCamera cam, float t, uint output) => cam.Missing(_at);
}
