using System.Numerics;
using CrashEngine.Runtime.Camera;

namespace CrashEngine.Runtime.Physics;

// Amedo
public sealed class HullBuilder
{
    private const float PointTolerance = 1e-4f;
    private const float AxisTolerance = 0.001f;
    private const float SideTolerance = 0.001f;
    private const float Eps = 2.4999998e-09f;

    private readonly Vector4[] _points = new Vector4[64];
    private readonly byte[] _faceCounts = new byte[64];
    private readonly byte[,] _faceVerts = new byte[64, 0x18];
    private readonly (byte A, byte B)[] _edges = new (byte, byte)[256];
    private readonly Vector4[] _dirs = new Vector4[72];
    private readonly Vector4[] _planes = new Vector4[64];
    private readonly Vector4[] _normals = new Vector4[64];
    private int _pointCount, _faceCount, _edgeCount, _dirCount, _normalCount;
    private bool _building;

    public bool Building => _building;

    public void Begin_1FFF00()
    {
        _pointCount = 0;
        _building = true;
        _edgeCount = 0;
        _faceCount = 0;
        _dirCount = 0;
        _normalCount = 0;
    }

    public void EndPoints_1FFF20() => _building = false;

    public byte AddPoint_1FFF80(Vector4 p)
    {
        for (int i = 0; i < _pointCount; i++)
        {
            var q = _points[i];
            float dx = q.X - p.X, dy = q.Y - p.Y, dz = q.Z - p.Z;
            if (MathF.Sqrt(dx * dx + dy * dy + dz * dz) < PointTolerance) return (byte)i;
        }
        _points[_pointCount++] = p;
        return (byte)(_pointCount - 1);
    }

    public void AddQuad_1FB450(Vector4 a, Vector4 b, Vector4 c, Vector4 d)
    {
        byte ia = AddPoint_1FFF80(a), ib = AddPoint_1FFF80(b), ic = AddPoint_1FFF80(c), id = AddPoint_1FFF80(d);
        AddFace_2000B8([ia, ib, ic, id]);
    }

    // Amedo
    public void AddTriangle_1FB1F8(Vector4 a, Vector4 b, Vector4 c)
    {
        byte ia = AddPoint_1FFF80(a), ib = AddPoint_1FFF80(b), ic = AddPoint_1FFF80(c);
        AddFace_2000B8([ia, ib, ic]);
    }

    // Amedo
    public bool BuildTriangle_1FBB20(Vector4 a, Vector4 b, Vector4 c, ConvexHull hull)
    {
        Begin_1FFF00();
        AddPoint_1FFF80(a);
        AddPoint_1FFF80(b);
        AddPoint_1FFF80(c);
        AddTriangle_1FB1F8(a, b, c);
        AddTriangle_1FB1F8(a, c, b);
        EndPoints_1FFF20();
        if (!CollectEdges_1FA7D0() || !CollectEdgeDirections_1FA938() || !CollectPlanesAndNormals_1FABF0()) return false;
        PackInto_1FAFB0(hull);
        return true;
    }

    public void AddFace_2000B8(ReadOnlySpan<byte> idx)
    {
        int n = idx.Length;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (idx[i] == idx[j]) return;
        _faceCounts[_faceCount] = (byte)n;
        for (int k = 0; k < n; k++) _faceVerts[_faceCount, k] = idx[k];
        _faceCount++;
    }

    public bool Finish_1FFF28(out ConvexHull hull)
    {
        hull = null!;
        if (!CollectEdges_1FA7D0() || !CollectEdgeDirections_1FA938() || !CollectPlanesAndNormals_1FABF0()) return false;
        hull = Pack_1FAFB0();
        return true;
    }

    private bool CollectEdges_1FA7D0()
    {
        _edgeCount = 0;
        for (int f = 0; f < _faceCount; f++)
        {
            int n = _faceCounts[f];
            for (int k = 0; k < n; k++)
            {
                byte a = _faceVerts[f, k], b = _faceVerts[f, (k + 1) % n];
                int e = 0;
                for (; e < _edgeCount; e++)
                {
                    var (ea, eb) = _edges[e];
                    if ((ea == a && eb == b) || (ea == b && eb == a)) break;
                }
                if (e == _edgeCount) _edges[_edgeCount++] = (a, b);
            }
        }
        return true;
    }

    private bool CollectEdgeDirections_1FA938()
    {
        _dirCount = 0;
        for (int f = 0; f < _faceCount; f++)
        {
            int n = _faceCounts[f];
            for (int k = 0; k < n; k++)
            {
                var pa = _points[_faceVerts[f, k]];
                var pb = _points[_faceVerts[f, (k + 1) % n]];
                var d = new Vector4(pa.X - pb.X, pa.Y - pb.Y, pa.Z - pb.Z, 1f);
                float inv = CamMath.InvLengthGuarded_11C030(Eps, d);
                AddUnique(_dirs, ref _dirCount, new Vector4(d.X * inv, d.Y * inv, d.Z * inv, 1f));
            }
        }
        return true;
    }

    private bool CollectPlanesAndNormals_1FABF0()
    {
        _normalCount = 0;
        for (int f = 0; f < _faceCount; f++)
        {
            var plane = PlaneFromTriangle_185DC0(_points[_faceVerts[f, 0]], _points[_faceVerts[f, 1]], _points[_faceVerts[f, 2]]);
            bool above = false, below = false;
            for (int i = 0; i < _pointCount; i++)
            {
                var p = _points[i];
                float s = plane.X * p.X + plane.Y * p.Y + plane.Z * p.Z + plane.W;
                if (SideTolerance < s) above = true;
                if (s < -SideTolerance) below = true;
            }
            if (above)
            {
                if (below) return false;
                plane = new Vector4(-plane.X, -plane.Y, -plane.Z, -plane.W);
                int n = _faceCounts[f];
                for (int j = 0; j < n >> 1; j++)
                    (_faceVerts[f, j], _faceVerts[f, n - 1 - j]) = (_faceVerts[f, n - 1 - j], _faceVerts[f, j]);
            }
            _planes[f] = plane;
            var nrm = new Vector4(plane.X, plane.Y, plane.Z, 1f);
            float inv = CamMath.InvLengthGuarded_11C030(Eps, nrm);
            AddUnique(_normals, ref _normalCount, new Vector4(nrm.X * inv, nrm.Y * inv, nrm.Z * inv, 1f));
        }
        return true;
    }

    private static void AddUnique(Vector4[] list, ref int count, Vector4 u)
    {
        for (int j = 0; j < count; j++)
        {
            var q = list[j];
            float ax = u.X - q.X, ay = u.Y - q.Y, az = u.Z - q.Z;
            if (MathF.Sqrt(ax * ax + ay * ay + az * az) < AxisTolerance) return;
            float bx = -u.X - q.X, by = -u.Y - q.Y, bz = -u.Z - q.Z;
            if (MathF.Sqrt(bx * bx + by * by + bz * bz) < AxisTolerance) return;
        }
        list[count++] = u;
    }

    private ConvexHull Pack_1FAFB0()
    {
        var hull = new ConvexHull();
        PackInto_1FAFB0(hull);
        return hull;
    }

    // Amedo
    private void PackInto_1FAFB0(ConvexHull hull)
    {
        if (hull.Points.Length != _pointCount) hull.Points = new Vector3[_pointCount];
        if (hull.Faces.Length != _faceCount) hull.Faces = new Vector4[_faceCount];
        if (hull.EdgeDirs.Length != _dirCount) hull.EdgeDirs = new Vector3[_dirCount];
        if (hull.Normals.Length != _normalCount) hull.Normals = new Vector3[_normalCount];
        if (hull.Edges.Length != _edgeCount) hull.Edges = new (byte, byte)[_edgeCount];
        if (hull.FaceStart.Length != _faceCount) hull.FaceStart = new byte[_faceCount];
        for (int i = 0; i < _pointCount; i++) hull.Points[i] = new Vector3(_points[i].X, _points[i].Y, _points[i].Z);
        for (int i = 0; i < _faceCount; i++) hull.Faces[i] = _planes[i];
        for (int i = 0; i < _dirCount; i++) hull.EdgeDirs[i] = new Vector3(_dirs[i].X, _dirs[i].Y, _dirs[i].Z);
        for (int i = 0; i < _normalCount; i++) hull.Normals[i] = new Vector3(_normals[i].X, _normals[i].Y, _normals[i].Z);
        int total = 0;
        for (int f = 0; f < _faceCount; f++) total += 1 + _faceCounts[f];
        if (hull.FaceVerts.Length != total) hull.FaceVerts = new byte[total];
        int o = 0;
        for (int f = 0; f < _faceCount; f++)
        {
            hull.FaceStart[f] = (byte)o;
            int n = _faceCounts[f];
            hull.FaceVerts[o++] = (byte)n;
            for (int k = 0; k < n; k++) hull.FaceVerts[o++] = _faceVerts[f, k];
        }
        for (int i = 0; i < _edgeCount; i++) hull.Edges[i] = _edges[i];
    }

    public static Vector4 PlaneFromTriangle_185DC0(Vector4 a, Vector4 b, Vector4 c)
    {
        var u = new Vector4(b.X - a.X, b.Y - a.Y, b.Z - a.Z, b.W);
        var v = new Vector4(c.X - a.X, c.Y - a.Y, c.Z - a.Z, c.W);
        var n = new Vector4(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X, 1f);
        if (Small(n))
        {
            if (!Small(u)) n = PerpendicularOf_18DA40(u);
            else if (!Small(v)) n = PerpendicularOf_18DA40(v);
            else n = new Vector4(0f, 1f, 0f, 1f);
        }
        float k = CamMath.InvLengthGuarded_11C030(Eps, n);
        float x = n.X * k, y = n.Y * k, z = n.Z * k;
        return new Vector4(x, y, z, -(x * a.X + y * a.Y + z * a.Z));
    }

    private static bool Small(Vector4 v) => MathF.Abs(v.X) <= 5e-05f && MathF.Abs(v.Y) <= 5e-05f && MathF.Abs(v.Z) <= 5e-05f;

    public static Vector4 PerpendicularOf_18DA40(Vector4 v)
    {
        float xx = v.X * v.X, zz = v.Z * v.Z;
        if (!(1e-20f < xx + v.Y * v.Y + zz)) return v;
        if (zz <= xx) return new Vector4(-v.Y * 3f, v.X * 3f, 0f, 1f);
        return new Vector4(0f, v.Z * 3f, -v.Y * 3f, 1f);
    }
}
