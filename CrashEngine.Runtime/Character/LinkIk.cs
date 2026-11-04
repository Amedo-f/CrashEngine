using System.Numerics;
using CrashEngine.Runtime.Camera;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-10-05
public sealed class LinkIk
{
    private const float RotEps = 5e-4f, ClampEps = 5e-5f, Tiny = 1e-10f, DirEps = 2.4999998e-09f;

    private static readonly float[] Lengths = { 0.37f, 0.3f, 0.9f, 0.256f, 0.29f };
    private static readonly float[] Angles = { -0.15707964f, -1.1780972f, 1.5707964f, 1.5707964f, 0.7853982f };
    private static readonly (float Min, float Max)[] Limits =
    {
        (-1.2566371f, -0.31415927f), (-2.5132742f, -0.62831855f), (0f, 2.3561945f), (1.2566371f, 1.8849558f), (0.15707964f, 1.4137167f),
    };

    private sealed class Joint
    {
        public Joint? Next, Parent;
        public float X, Y, Len;
        public float LocalCos, LocalSin, AbsCos, AbsSin;
        public bool Limited;
        public float MinCos, MinSin, MaxCos, MaxSin;
    }

    private Joint? _head;
    private Vector3 _origin, _dir;
    private float _endX, _endY, _targetX, _targetY;

    public static LinkIk Create_14D680()
    {
        var ik = new LinkIk();
        Joint? tail = null;
        for (int i = 0; i < Lengths.Length; i++)
        {
            var j = new Joint { Parent = tail, Len = Lengths[i] };
            (j.LocalCos, j.LocalSin) = GameMath.CosSin_18B918(GameMath.FromValue_11BFA0(Angles[i], 0));
            Fk_18B408(j);
            if (tail is null) ik._head = j;
            else tail.Next = j;
            tail = j;
        }
        for (int i = 0; i < Limits.Length; i++) ik.SetLimits_181D20(i + 1, Limits[i].Min, Limits[i].Max);
        return ik;
    }

    public void Solve(Vector3 start, Vector3 target, Vector3[] points)
    {
        Setup_18B0F0(start, target);
        Output_18B290(DirEps, points, 2);
    }

    private void SetLimits_181D20(int index, float min, float max)
    {
        var j = _head!;
        for (int i = 1; i < index; i++) j = j.Next!;
        var (c, s) = GameMath.CosSin_18B918(GameMath.FromValue_11BFA0(min, 0));
        Normalize_18C548(ref s, ref c, RotEps);
        j.MinSin = s;
        j.MinCos = c;
        (c, s) = GameMath.CosSin_18B918(GameMath.FromValue_11BFA0(max, 0));
        Normalize_18C548(ref s, ref c, RotEps);
        j.MaxSin = s;
        j.MaxCos = c;
        j.Limited = true;
    }

    private void Setup_18B0F0(Vector3 start, Vector3 target)
    {
        _origin = start;
        var d = new Vector3(target.X - start.X, 0f, target.Z - start.Z);
        _dir = d * GameMath.InvLength_11C030(DirEps, new Vector4(d, 0f));
        var t = target - _origin;
        _targetX = t.X * _dir.X + t.Y * _dir.Y + t.Z * _dir.Z;
        _targetY = t.Y;
    }

    private int Output_18B290(float tol, Vector3[] points, int iterations)
    {
        int n = Ccd_182378(iterations, tol);
        int k = 0;
        for (var j = _head!; j.Next is { } next; j = next)
            points[k++] = _dir * next.X + new Vector3(0f, next.Y, 0f) + _origin;
        points[k] = _dir * _endX + new Vector3(0f, _endY, 0f) + _origin;
        return n;
    }

    private static float Inv(float l) => Tiny < l || l < -Tiny ? 1f / l : 0f;

    private int Ccd_182378(int iterations, float tol)
    {
        var head = _head!;
        float dx = _targetX - head.X, dy = _targetY - head.Y;
        float sum = 0f;
        var last = head;
        for (var j = head; j is not null; j = j.Next)
        {
            sum += j.Len;
            last = j;
        }
        int done = 0;
        if (dx * dx + dy * dy < sum * sum)
        {
            var j = last;
            while (0 < iterations)
            {
                float ex = last.Len * last.AbsCos + last.X, ey = last.Len * last.AbsSin + last.Y;
                _endX = ex;
                _endY = ey;
                float rx = _targetX - ex, ry = _targetY - ey;
                if (rx * rx + ry * ry <= tol * (_targetX * ex + _targetY * ey)) break;
                float ax = _targetX - j.X, ay = _targetY - j.Y;
                float bx = ex - j.X, by = ey - j.Y;
                float ia = Inv(MathF.Sqrt(ax * ax + ay * ay));
                ax *= ia;
                ay *= ia;
                float ib = Inv(MathF.Sqrt(bx * bx + by * by));
                bx *= ib;
                by *= ib;
                float s = bx * ay - by * ax, c = bx * ax + by * ay;
                float il = Inv(MathF.Sqrt(s * s + c * c));
                if (0f < il) Rotate_18B4A0(j, s * il, c * il);
                Normalize_18C548(ref j.LocalSin, ref j.LocalCos, RotEps);
                for (var k = j; k is not null; k = k.Next) Fk_18B408(k);
                if (j != head) j = j.Parent!;
                else
                {
                    done++;
                    j = last;
                }
                if (done >= iterations) break;
            }
        }
        else
        {
            float c = head.AbsCos, s = head.AbsSin;
            float rs = -dx * s + dy * c, rc = dx * c + dy * s;
            float il = Inv(MathF.Sqrt(rs * rs + rc * rc));
            if (0f < il) Rotate_18B4A0(head, rs * il, rc * il);
            for (var k = head.Next; k is not null; k = k.Next)
            {
                k.LocalCos = 1f;
                k.LocalSin = 0f;
            }
        }
        for (var k = head; k is not null; k = k.Next) Fk_18B408(k);
        _endX = last.Len * last.AbsCos + last.X;
        _endY = last.Len * last.AbsSin + last.Y;
        return done;
    }

    private static void Rotate_18B4A0(Joint j, float s, float c)
    {
        Rotate_18C670(ref j.LocalSin, ref j.LocalCos, s, c, RotEps);
        Clamp_181EF0(j, ref j.LocalSin, ref j.LocalCos, ClampEps);
    }

    private static void Fk_18B408(Joint j)
    {
        j.AbsCos = j.LocalCos;
        j.AbsSin = j.LocalSin;
        if (j.Parent is not { } p) return;
        Rotate_18C670(ref j.AbsSin, ref j.AbsCos, p.AbsSin, p.AbsCos, RotEps);
        j.X = p.Len * p.AbsCos + p.X;
        j.Y = p.Len * p.AbsSin + p.Y;
    }

    private static void Rotate_18C670(ref float sin, ref float cos, float s, float c, float eps)
    {
        float nc = cos * c - sin * s, ns = cos * s + sin * c;
        cos = nc;
        sin = ns;
        Normalize_18C548(ref sin, ref cos, eps);
    }

    private static void Normalize_18C548(ref float sin, ref float cos, float eps)
    {
        float l2 = sin * sin + cos * cos, e = l2 - 1f;
        if (e * e <= eps) return;
        float inv = Inv(MathF.Sqrt(l2));
        sin *= inv;
        cos *= inv;
    }

    private static void Clamp_181EF0(Joint j, ref float sin, ref float cos, float eps)
    {
        if (!j.Limited) return;
        float s = sin, c = cos;
        if (-eps <= c * j.MaxSin - s * j.MaxCos && c * j.MinSin - s * j.MinCos <= eps) return;
        float k = j.MinCos * j.MaxCos + j.MinSin * j.MaxSin + 1f;
        k = eps < k || k < -eps ? 1f / k : 0f;
        k = MathF.Sqrt(k) * 0.70710677f;
        float bs, bc;
        if (0f < k)
        {
            bs = (j.MinSin + j.MaxSin) * k;
            bc = (j.MinCos + j.MaxCos) * k;
            if (j.MinCos * j.MaxSin - j.MinSin * j.MaxCos < -eps)
            {
                bs = -bs;
                bc = -bc;
            }
        }
        else
        {
            bs = j.MinCos;
            bc = -j.MinSin;
        }
        float anti = c * -bs - s * -bc;
        if (-eps <= anti && anti <= eps)
        {
            sin = bs;
            cos = bc;
            return;
        }
        if (-eps <= c * j.MinSin - s * j.MinCos && anti <= eps)
        {
            sin = j.MinSin;
            cos = j.MinCos;
            return;
        }
        if (-eps <= anti && c * j.MaxSin - s * j.MaxCos <= eps)
        {
            sin = j.MaxSin;
            cos = j.MaxCos;
            return;
        }
        sin = bs;
        cos = bc;
    }
}
