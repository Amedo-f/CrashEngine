using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private sealed class GizmoRenderer : Component
	{
		public GpuMesh? Mesh { get; set; }

		public System.Numerics.Vector3 GizmoPos { get; set; }

		public float GizmoScale { get; set; } = 1f;


		public Quaternion GizmoRot { get; set; } = Quaternion.Identity;

		public Matrix4x4? GizmoBasis { get; set; } // Amedo 2026-10-04

		public bool Visible { get; set; } = false;

		public override bool IsOverlay => true; // Amedo 2026-10-02

		public override void OnRender(GL gl)
		{
			if (Visible && Mesh != null)
			{
				RenderPipeline instance = RenderPipeline.Instance;
				if (instance != null)
				{
					TwinShaderProgram shader = instance.Shader;
					Matrix4x4 m = Matrix4x4.CreateScale(GizmoScale) * (GizmoBasis ?? Matrix4x4.CreateFromQuaternion(GizmoRot)) * Matrix4x4.CreateTranslation(GizmoPos); // Amedo 2026-10-04
					shader.Set("StartModel", m);
					shader.Set("twin_material.use_texture", 0f);
					shader.Set("twin_material.double_color", 1f);
					shader.Set("twin_material.alpha_test", 0f);
					shader.Set("twin_material.alpha_blend", 0f);
					shader.Set("twin_material.env_map", 0f);
					shader.Set("twin_material.metalic_specular", 0f);
					shader.Set("twin_material.deform_speed", System.Numerics.Vector2.Zero);
					shader.Set("twin_material.uv_scroll_speed", System.Numerics.Vector2.Zero);
					shader.Set("twin_material.reflect_dist", System.Numerics.Vector2.Zero);
					shader.Set("twin_material.billboard_render", 0f);
					shader.Set("twin_material.two_sided_lighting", 1);
					shader.Set("twin_material.perform_fog", 0f);
					shader.Set("twin_material.select_pulse", 1f);
					CrashEngine.Renderer.GlState.Culling(gl, false); // Amedo 2026-10-01
					CrashEngine.Renderer.GlState.DepthFunc(gl, DepthFunction.Always);
					Mesh.Draw(gl);
					CrashEngine.Renderer.GlState.DepthFunc(gl, DepthFunction.Lequal);
				}
			}
		}
	}

	private enum GizmoMode
	{
		Move,
		Rotate
	}

    private Vector3 GizmoPivot()
    {
        if (_selectedSet.Count == 0) return _selected?.Transform.World.Translation ?? Vector3.Zero;
        var sum = Vector3.Zero;
        foreach (var e in _selectedSet) sum += e.Transform.World.Translation;
        return sum / _selectedSet.Count;
    }

    private float GizmoScale()
    {
        if (_camera is null || _selected is null) return 1f;
        float dist = Vector3.Distance(_camera.Transform.Position, GizmoPivot());
        return MathF.Max(dist * 0.055f, 0.5f);
    }

    private int GetGizmoHoverAxis(Vector2 mousePos, int sw, int sh)
    {
        if (_selected is null) return -1;
        var   gPos = GizmoPivot();
        float tipW = TipLen * GizmoScale();
        var tips = new[]
        {
            gPos + new Vector3(tipW, 0,    0),
            gPos + new Vector3(0,    tipW, 0),
            gPos + new Vector3(0,    0,    tipW),
        };
        for (int i = 0; i < 3; i++)
        {
            if (!WorldToScreen(tips[i], sw, sh, out var sp)) continue;
            if (Vector2.Distance(sp, mousePos) < 18f) return i;
        }
        return -1;
    }

    private int GetDuplicateHoverDir(Vector2 mousePos, int sw, int sh)
    {
        float tipW = TipLen * GizmoScale();
        for (int i = 0; i < SixDirs.Length; i++)
        {
            if (!WorldToScreen(_dupGizmoPivot + SixDirs[i] * tipW, sw, sh, out var sp)) continue;
            if (Vector2.Distance(sp, mousePos) < 18f) return i;
        }
        return -1;
    }

    private static Vector3[] RingPoints(Vector3 center, Vector3 axis, float radius, int segments = 48)
    {
        var arbitrary = MathF.Abs(Vector3.Dot(axis, Vector3.UnitY)) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        var u = Vector3.Normalize(Vector3.Cross(axis, arbitrary));
        var v = Vector3.Cross(axis, u);
        var pts = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments * MathF.Tau;
            pts[i] = center + (u * MathF.Cos(t) + v * MathF.Sin(t)) * radius;
        }
        return pts;
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab   = b - a;
        float l2 = ab.LengthSquared();
        if (l2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    private const float ViewRingScale = 1.15f;

    private Vector3 RotateRingAxis(int index) => index switch
    {
        0 => Vector3.UnitX,
        1 => Vector3.UnitY,
        2 => Vector3.UnitZ,
        _ => _camera is not null ? _camera.Forward : Vector3.UnitZ,
    };

    private int GetRotateHoverAxis(Vector2 mousePos, int sw, int sh)
    {
        if (_selected is null) return -1;
        var   gPos   = GizmoPivot();
        float radius = TipLen * GizmoScale();

        int best = -1; float bestDist = 10f;
        for (int a = 0; a < 4; a++)
        {
            var pts = RingPoints(gPos, RotateRingAxis(a), a == 3 ? radius * ViewRingScale : radius);
            Vector2? prev = null;
            for (int i = 0; i <= pts.Length; i++)
            {
                if (!WorldToScreen(pts[i % pts.Length], sw, sh, out var sp)) { prev = null; continue; }
                if (prev is { } pv)
                {
                    float d = DistancePointToSegment(mousePos, pv, sp);
                    if (d < bestDist) { bestDist = d; best = a; }
                }
                prev = sp;
            }
        }
        return best;
    }

    private void UpdateGizmoDrag(Vector2 mousePos, int sw, int sh)
    {
        if (_selected is null || _camera is null) return;
        var delta = mousePos - _gizmoPrevMouse;
        _gizmoPrevMouse = mousePos;
        if (delta.LengthSquared() < 0.01f) return;

        var worldAxis = _gizmoAxis switch
        {
            0 => Vector3.UnitX,
            1 => Vector3.UnitY,
            _ => Vector3.UnitZ,
        };

        var objWorldPos = GizmoPivot();
        if (!WorldToScreen(objWorldPos, sw, sh, out var sObj) ||
            !WorldToScreen(objWorldPos + worldAxis, sw, sh, out var sTip))
            return;

        var   sAxis    = Vector2.Normalize(sTip - sObj);
        float movement = Vector2.Dot(delta, sAxis);
        float dist     = Vector3.Distance(_camera.Transform.Position, objWorldPos);
        float wpx      = _camera.WorldPerPixel(dist, sh); // Amedo 2026-10-04

        var worldDelta = worldAxis * movement * wpx;

        foreach (var ent in _selectedSet)
            ApplyWorldDelta(ent, worldDelta);
    }

    private void UpdateRotateDrag(Vector2 mousePos, int sw, int sh)
    {
        if (_selected is null || _camera is null) return;
        var gPos = GizmoPivot();
        if (!WorldToScreen(gPos, sw, sh, out var sc)) return;

        var prevVec = _gizmoPrevMouse - sc;
        var curVec  = mousePos - sc;
        _gizmoPrevMouse = mousePos;
        if (prevVec.LengthSquared() < 4f || curVec.LengthSquared() < 4f) return;

        float prevAngle = MathF.Atan2(prevVec.Y, prevVec.X);
        float curAngle  = MathF.Atan2(curVec.Y, curVec.X);
        float delta     = curAngle - prevAngle;
        while (delta >  MathF.PI) delta -= MathF.Tau;
        while (delta < -MathF.PI) delta += MathF.Tau;
        delta = -delta;

        var worldAxis = RotateRingAxis(_gizmoAxis);

        float facing = Vector3.Dot(worldAxis, Vector3.Normalize(_camera.Transform.Position - gPos));
        float angle  = facing < 0f ? -delta : delta;

        foreach (var ent in _selectedSet)
            ApplyWorldRotation(ent, gPos, worldAxis, angle);
    }

    private static void ApplyWorldDelta(Entity ent, Vector3 worldDelta)
    {
        var localDelta = worldDelta;
        if (ent.Transform.Parent is not null)
        {
            Matrix4x4.Invert(ent.Transform.Parent.World, out var invParent);
            localDelta = Vector3.TransformNormal(worldDelta, invParent);
        }

        if (ent.Transform.LocalMatrix is { } lm)
        {
            lm.M41 += localDelta.X;
            lm.M42 += localDelta.Y;
            lm.M43 += localDelta.Z;
            ent.Transform.LocalMatrix = lm;
        }
        else
        {
            ent.Transform.Position += localDelta;
        }
    }

    private static void ApplyWorldRotation(Entity ent, Vector3 pivotWorld, Vector3 axisWorld, float angleRad)
    {
        var worldBefore = ent.Transform.World;
        var rot          = Matrix4x4.CreateFromAxisAngle(axisWorld, angleRad);
        var newWorld     = worldBefore
                          * Matrix4x4.CreateTranslation(-pivotWorld)
                          * rot
                          * Matrix4x4.CreateTranslation(pivotWorld);

        var newLocal = newWorld;
        if (ent.Transform.Parent is not null && Matrix4x4.Invert(ent.Transform.Parent.World, out var invParent))
            newLocal = newWorld * invParent;

        if (ent.Transform.LocalMatrix is not null)
        {
            ent.Transform.LocalMatrix = newLocal;
        }
        else if (Matrix4x4.Decompose(newLocal, out var scale, out var rotation, out var translation))
        {
            ent.Transform.Position = translation;
            ent.Transform.Rotation = rotation;
            ent.Transform.Scale    = scale;
        }
        else
        {
            ent.Transform.LocalMatrix = newLocal;
        }
    }

    private static Vector3 MatrixToEulerXYZ(Matrix4x4 m)
    {
        float y = MathF.Asin(Math.Clamp(-m.M13, -1f, 1f));
        float x = MathF.Atan2(m.M23, m.M33);
        float z = MathF.Atan2(m.M12, m.M11);
        return new Vector3(x, y, z);
    }

    private static Quaternion EulerXYZToQuaternion(Vector3 eulerRad) =>
        Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateRotationX(eulerRad.X) * Matrix4x4.CreateRotationY(eulerRad.Y) * Matrix4x4.CreateRotationZ(eulerRad.Z));

    private static GpuMesh BuildGizmoMesh(GL gl)
    {
        var v = new List<GpuMesh.Vertex>();
        AppendArrow(v, Quaternion.Identity,
                    new Vector4(1f, 0.2f, 0.2f, 1f));
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitZ,  MathF.PI / 2f),
                    new Vector4(0.2f, 1f, 0.2f, 1f));
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2f),
                    new Vector4(0.2f, 0.2f, 1f, 1f));
        return new GpuMesh(gl, v.ToArray(), PrimitiveType.Triangles);
    }

    private static GpuMesh BuildDuplicateGizmoMesh(GL gl)
    {
        var v = new List<GpuMesh.Vertex>();
        var col = new Vector4(1f, 0.65f, 0.15f, 1f);
        AppendArrow(v, Quaternion.Identity,                                          col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitY,  MathF.PI),      col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitZ,  MathF.PI / 2f), col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathF.PI / 2f), col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2f), col);
        AppendArrow(v, Quaternion.CreateFromAxisAngle(Vector3.UnitY,  MathF.PI / 2f), col);
        return new GpuMesh(gl, v.ToArray(), PrimitiveType.Triangles);
    }

    private static void AppendArrow(List<GpuMesh.Vertex> v, Quaternion rot, Vector4 col)
    {
        const float SL = StemLen, SR = 0.06f, HL = HeadLen, HR = 0.18f;
        GpuMesh.Vertex Pt(float x, float y, float z) => new()
        {
            Position = Vector3.Transform(new Vector3(x, y, z), rot),
            Normal   = Vector3.UnitY, UV = Vector2.Zero, Color = col,
        };
        v.Add(Pt(0,-SR, 0)); v.Add(Pt(SL,-SR, 0)); v.Add(Pt(SL, SR, 0));
        v.Add(Pt(0,-SR, 0)); v.Add(Pt(SL, SR, 0)); v.Add(Pt(0,  SR, 0));
        v.Add(Pt(0, 0,-SR)); v.Add(Pt(SL, 0,-SR)); v.Add(Pt(SL, 0, SR));
        v.Add(Pt(0, 0,-SR)); v.Add(Pt(SL, 0, SR)); v.Add(Pt(0,  0, SR));
        float bx = SL, tx = SL + HL;
        v.Add(Pt(bx, HR,  0)); v.Add(Pt(bx,  0, HR)); v.Add(Pt(tx, 0, 0));
        v.Add(Pt(bx,  0, HR)); v.Add(Pt(bx,-HR,  0)); v.Add(Pt(tx, 0, 0));
        v.Add(Pt(bx,-HR,  0)); v.Add(Pt(bx,  0,-HR)); v.Add(Pt(tx, 0, 0));
        v.Add(Pt(bx,  0,-HR)); v.Add(Pt(bx, HR,  0)); v.Add(Pt(tx, 0, 0));
    }

    private static GpuMesh BuildSingleArrowMesh(GL gl, Vector4 col)
    {
        var v = new List<GpuMesh.Vertex>();
        AppendArrow(v, Quaternion.Identity, col);
        return new GpuMesh(gl, v.ToArray(), PrimitiveType.Triangles);
    }


    private static Quaternion RotationBetween(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        to   = Vector3.Normalize(to);
        float d = Vector3.Dot(from, to);
        if (d > 0.9999f) return Quaternion.Identity;
        if (d < -0.9999f)
        {
            var axis = Vector3.Cross(Vector3.UnitY, from);
            if (axis.LengthSquared() < 1e-6f) axis = Vector3.Cross(Vector3.UnitZ, from);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }
        var c = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(c.X, c.Y, c.Z, 1f + d));
    }

    private static (float X, float Y, float Z) DecomposeXyzEulerDegrees(Matrix4x4 m)
    {
        float sb = Math.Clamp(-m.M13, -1f, 1f);
        float b  = MathF.Asin(sb);
        float cb = MathF.Cos(b);
        float a, c;
        if (MathF.Abs(cb) > 1e-5f)
        {
            a = MathF.Atan2(m.M23, m.M33);
            c = MathF.Atan2(m.M12, m.M11);
        }
        else
        {
            c = 0f;
            a = sb > 0 ? MathF.Atan2(m.M21, m.M22) : MathF.Atan2(-m.M21, m.M22);
        }
        return (a * 180f / MathF.PI, b * 180f / MathF.PI, c * 180f / MathF.PI);
    }
}
