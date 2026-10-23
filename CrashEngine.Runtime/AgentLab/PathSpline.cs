using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed class PathSpline
{
    private static readonly float Sixth     = BitConverter.Int32BitsToSingle(0x3E2AAAA6);
    private static readonly float NegSixth  = BitConverter.Int32BitsToSingle(unchecked((int)0xBE2AAAA6));
    private static readonly float Half      = BitConverter.Int32BitsToSingle(0x3EFFFFF9);
    private static readonly float NegHalf   = BitConverter.Int32BitsToSingle(unchecked((int)0xBEFFFFF9));
    private static readonly float NegOne    = BitConverter.Int32BitsToSingle(unchecked((int)0xBF7FFFF9));
    private static readonly float TwoThirds = BitConverter.Int32BitsToSingle(0x3F2AAAA6);
    private static readonly float EndSixth  = BitConverter.Int32BitsToSingle(0x3E2AAAAB);
    private static readonly float LengthStep = BitConverter.Int32BitsToSingle(0x3C23D70A);
    private static readonly float Refine    = BitConverter.Int32BitsToSingle(0x3F7F7CEE);
    private static readonly float BrentTol  = BitConverter.Int32BitsToSingle(0x3851B717);
    private static readonly float Golden    = BitConverter.Int32BitsToSingle(0x3EC3910C);
    private const float Far = 1e30f;
    private const float TinySq = 2.4999998e-09f;

    public Vector3[] Points { get; }
    public float[] Lengths { get; }
    public float[] Steps { get; }
    public int Count => Points.Length;
    public int Segments => Points.Length - 3;
    public float TotalLength => Lengths[Segments - 1];

    private Vector3 _query;
    private int _segment;

    private PathSpline(Vector3[] points, float[] lengths, float[] steps)
    {
        Points  = points;
        Lengths = lengths;
        Steps   = steps;
    }

    public static PathSpline? From(PS2AnyPath path)
    {
        int n = path.ParameterList.Count;
        if (path.PointList.Count < 4 || n < path.PointList.Count - 3) return null;
        var flat = new float[n * 2];
        for (int i = 0; i < n; i++)
        {
            flat[2 * i]     = path.ParameterList[i].X;
            flat[2 * i + 1] = path.ParameterList[i].Y;
        }
        var points = path.PointList.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
        return new PathSpline(points, flat[..n], flat[n..]);
    }

    // Amedo 2026-09-29
    public static PathSpline? From(Vector3[] points, float[] flat)
    {
        int n = flat.Length / 2;
        if (points.Length < 4 || n < points.Length - 3) return null;
        return new PathSpline(points, flat[..n], flat[n..(2 * n)]);
    }

    private static Vector4 Weights(float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return new Vector4(t3 * NegSixth + t2 * Half + t * NegHalf + Sixth,
                           t3 * Half + t2 * NegOne + TwoThirds,
                           t3 * NegHalf + t2 * Half + t * Half + Sixth,
                           t3 * Sixth);
    }

    private Vector3 Combine(int seg, Vector4 w) =>
        Points[seg] * w.X + Points[seg + 1] * w.Y + Points[seg + 2] * w.Z + Points[seg + 3] * w.W;

    public Vector3 PointAt(int seg, float t) => Combine(seg, Weights(t));

    public Vector3 TangentAt(int seg, float t)
    {
        float t2 = t * t, twoT = t + t;
        float w3 = t2 * 0.5f;
        float a  = t2 + w3;
        var d = Combine(seg, new Vector4((t - w3) - 0.5f, a - twoT, (t - a) + 0.5f, w3));
        float lenSq = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
        return lenSq > TinySq ? d * (1f / MathF.Sqrt(lenSq)) : Vector3.Zero;
    }

    private static float DistSq(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public float SegmentLength(int seg, float tEnd)
    {
        float length = 0f;
        if (!(0f <= tEnd)) return length;
        bool first = true;
        var prev = Vector3.Zero;
        for (float t = 0f; t <= tEnd; t += LengthStep)
        {
            var p = PointAt(seg, t);
            if (!first) length += MathF.Sqrt(DistSq(p, prev));
            first = false;
            prev  = p;
        }
        return length;
    }

    private float LocalAtLength(int seg, float target)
    {
        float travelled = 0f, t = 0f;
        bool first = true;
        var prev = Vector3.Zero;
        while (true)
        {
            var p = PointAt(seg, t);
            if (!first)
            {
                travelled += MathF.Sqrt(DistSq(p, prev));
                if (target <= travelled) break;
            }
            first = false;
            t += LengthStep;
            prev = p;
            if (!(t <= 1f)) break;
        }
        return 1f <= t ? 1f : t;
    }

    public Vector3 DirectionAt(float along)
    {
        int s = Locate(along, out float into);
        return TangentAt(s, LocalAtLength(s, into));
    }

    // Amedo 2026-09-29
    public Vector3 PositionAt_188CB0(float along)
    {
        int s = Locate(along, out float into);
        float travelled = 0f, t = 0f;
        bool first = true;
        Vector3 p, output = Vector3.Zero;
        while (true)
        {
            p = PointAt(s, t);
            if (!first)
            {
                travelled += MathF.Sqrt(DistSq(p, output));
                if (into <= travelled) break;
            }
            first = false;
            t += LengthStep;
            output = p;
            if (!(t <= 1f)) break;
        }
        if (1f <= t) output = p;
        return output;
    }

    private int Locate(float along, out float into)
    {
        int segs = Segments;
        float target = Lengths[segs - 1] * along;
        int s;
        if (target <= Lengths[0]) s = 0;
        else if (Lengths[segs - 2] <= target) s = segs - 1;
        else
        {
            int idx = segs >> 1, step = idx;
            for (int guard = 0; ; guard++)
            {
                step >>= 1;
                if (step == 0) step = 1;
                if (target <= Lengths[idx] && Lengths[idx - 1] < target) break;
                idx += target < Lengths[idx] ? -step : step;
                if (guard > 64 || idx < 1 || idx > segs - 2) { idx = Math.Clamp(idx, 1, segs - 2); break; }
            }
            s = idx;
        }
        float segLen = s == 0 ? Lengths[0] : Lengths[s] - Lengths[s - 1];
        into = segLen - (Lengths[s] - target);
        return s;
    }

    public float Closest(Vector3 p, out Vector3 point)
    {
        float bestD = Far, bestT = 0f;
        var bestP = Vector3.Zero;
        int bestSeg = -1;

        int near = 0;
        float nearD = DistSq(Points[0], p);
        for (int i = 1; i < Count; i++)
        {
            float d = DistSq(Points[i], p);
            if (d < nearD) { nearD = d; near = i; }
        }
        int first = near - 3, n = 4;
        if (first < 0) { n = near + 1; first = 0; }
        if (Segments < first + n) n -= first + n - Segments;
        for (int k = 0; k < n; k++, first++)
            if (SegmentClosest(p, first, ref bestD, ref bestP, ref bestT)) bestSeg = first;

        point = bestP;
        if (bestSeg < 0) return 0f;
        float along = bestSeg != 0 ? Lengths[bestSeg - 1] : 0f;
        along += SegmentLength(bestSeg, bestT);
        return along / Lengths[Segments - 1];
    }

    private bool SegmentClosest(Vector3 p, int seg, ref float resD, ref Vector3 resP, ref float resT)
    {
        float step = Steps[seg];
        bool found = false;
        _query   = p;
        _segment = seg;

        var start = PointAt(seg, 0f);
        float segBest = DistSq(p, start);
        var c1 = Points[seg + 1];
        var c2 = Points[seg + 2];
        var c3 = Points[seg + 3];
        var end = new Vector3(((c1.X + c2.X * 4f) + c3.X) * EndSixth,
                              ((c1.Y + c2.Y * 4f) + c3.Y) * EndSixth,
                              ((c1.Z + c2.Z * 4f) + c3.Z) * EndSixth);
        float endD = DistSq(p, end);
        if (endD < segBest) segBest = endD;

        float t = 0f;
        do
        {
            var s = PointAt(seg, t);
            float d = DistSq(p, s);
            if (0f < t && d < segBest * Refine)
            {
                float x = t, fx = d;
                Brent(ref x, ref fx);
                var q = PointAt(seg, x);
                if (fx < resD)
                {
                    resP  = q;
                    found = true;
                    resD  = fx;
                    resT  = x;
                }
                return found;
            }
            if (d < resD)
            {
                found = true;
                resD  = d;
                resP  = s;
                resT  = t;
            }
            t += step;
        } while (t < 1f);
        return found;
    }

    private float Eval(float u) => DistSq(_query, PointAt(_segment, u));

    private void Brent(ref float x, ref float fx)
    {
        float a = 0f, b = 1f;
        float v = x, w = x, fv = fx, fw = fx;
        float d = 0f, e = 0f;
        for (int iter = 1; iter <= 4; iter++)
        {
            float xm = (a + b) * 0.5f;
            float tol2 = BrentTol * MathF.Abs(x);
            if (tol2 < BrentTol) tol2 = BrentTol;
            float tol1 = tol2 * 0.5f;
            if (MathF.Abs(x - xm) < tol2 - (b - a) * 0.5f) return;

            float p = 0f, q = 0f, etemp = 0f;
            if (tol1 < MathF.Abs(e))
            {
                float r = (x - w) * (fx - fv);
                q = (x - v) * (fx - fw);
                p = (x - v) * q - (x - w) * r;
                q = (q - r) + (q - r);
                if (0f < q) p = -p;
                q = MathF.Abs(q);
                etemp = e;
                e = d;
            }

            if (MathF.Abs(p) < MathF.Abs(q * etemp * 0.5f) && q * (a - x) < p && p < q * (b - x))
            {
                d = p / q;
                float u0 = x + d;
                if (u0 - a < tol2 || b - u0 < tol2) d = xm <= x ? -tol1 : tol1;
            }
            else
            {
                e = x < xm ? b - x : a - x;
                d = e * Golden;
            }

            float u = tol1 <= MathF.Abs(d) ? x + d : d < 0f ? x - tol1 : x + tol1;
            float fu = Eval(u);
            if (fu <= fx)
            {
                if (u < x) b = x; else a = x;
                v = w; fv = fw;
                w = x; fw = fx;
                x = u; fx = fu;
            }
            else
            {
                if (u < x) a = u; else b = u;
                if (fu <= fw || w == x)
                {
                    v = w; fv = fw;
                    w = u; fw = fu;
                }
                else if (fu <= fv || v == x || v == w)
                {
                    v = u; fv = fw;
                }
            }
        }
    }
}
