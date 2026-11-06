using System.Numerics;
using System.Text;
using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using ImporterMeshRenderer = CrashEngine.Importer.MeshRenderer;

namespace CrashLauncher;

public partial class GameScene
{
    private void ExportSelectedObjectToObj()
    {
        var root = _selected;
        if (root is null) { _browser.Log("Export Object: nothing selected."); return; }

        var parts = AllEntities(root)
            .Select(e => (Entity: e, Mr: e.Get<ImporterMeshRenderer>()))
            .Where(p => p.Mr is { Mesh.RaycastVertices: not null, Mesh.RaycastIndices: not null })
            .ToList();
        if (parts.Count == 0)
        {
            _browser.Log($"Export Object: '{root.Name}' has no real mesh geometry (itself or its children) to export.");
            return;
        }

        ShowSaveFileDialog("Export Object (OBJ + textures)", "Wavefront OBJ\0*.obj\0All Files\0*.*\0\0",
            SanitizeFileName(root.Name) + ".obj", path =>
        {
        if (path is null) return;
        var dir      = Path.GetDirectoryName(path) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(path);
        var mtlName  = baseName + ".mtl";

        Matrix4x4.Invert(root.Transform.World, out var rootInv);

        var gl = Engine.Instance.GL;
        var materialNames = new Dictionary<uint, string>();
        var mtl = new StringBuilder();
        foreach (var (_, mr) in parts)
        {
            var tex = mr!.Material?.Albedo;
            uint key = tex?.GlId ?? 0;
            if (materialNames.ContainsKey(key)) continue;
            string matName = tex is null ? "NoTexture" : $"tex_{key}";
            materialNames[key] = matName;

            mtl.AppendLine($"newmtl {matName}");
            mtl.AppendLine("Ka 1.000 1.000 1.000");
            mtl.AppendLine("Kd 1.000 1.000 1.000");
            mtl.AppendLine("d 1.0");
            mtl.AppendLine("illum 1");
            mtl.AppendLine($"# CE_Unlit {(mr!.Material?.Unlit == true ? 1 : 0)}");
            mtl.AppendLine($"# CE_AlphaBlend {(mr.Material?.AlphaBlend == true ? 1 : 0)}");
            if (tex is not null)
            {
                var pngName = $"{baseName}_tex_{key}.png";
                ExportTexturePng(gl, tex, Path.Combine(dir, pngName));
                mtl.AppendLine($"map_Kd {pngName}");
            }
            mtl.AppendLine();
        }
        File.WriteAllText(Path.Combine(dir, mtlName), mtl.ToString());

        var sb = new StringBuilder();
        sb.AppendLine($"# CrashEngine object export -- '{root.Name}' ({parts.Count} mesh part(s))");
        sb.AppendLine($"mtllib {mtlName}");
        int vOffset = 0;
        foreach (var (entity, mr) in parts)
        {
            var verts = mr!.Mesh!.RaycastVertices!;
            var idx   = mr.Mesh!.RaycastIndices!;
            var local = entity.Transform.World * rootInv;
            var nrm   = Matrix4x4.Transpose(Matrix4x4Invert(local));

            sb.AppendLine($"# {entity.Name}");
            foreach (var v in verts)
            {
                var p = Vector3.Transform(v.Position, local);
                sb.AppendLine($"v {(-p.X).ToStr()} {p.Y.ToStr()} {p.Z.ToStr()}");
            }
            foreach (var v in verts)
            {
                var n = Vector3.TransformNormal(v.Normal, nrm);
                if (n.LengthSquared() > 1e-12f) n = Vector3.Normalize(n);
                sb.AppendLine($"vn {(-n.X).ToStr()} {n.Y.ToStr()} {n.Z.ToStr()}");
            }
            foreach (var v in verts)
                sb.AppendLine($"vt {v.UV.X.ToStr()} {(1f - v.UV.Y).ToStr()}");

            uint key = mr.Material?.Albedo?.GlId ?? 0;
            sb.AppendLine($"usemtl {materialNames[key]}");
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                int a = (int)idx[i] + 1 + vOffset, b = (int)idx[i + 1] + 1 + vOffset, c = (int)idx[i + 2] + 1 + vOffset;
                sb.AppendLine($"f {a}/{a}/{a} {c}/{c}/{c} {b}/{b}/{b}");
            }
            vOffset += verts.Length;
        }
        File.WriteAllText(path, sb.ToString());
        _browser.Log($"Exported '{root.Name}' to {path} ({parts.Count} part(s), {materialNames.Count} material(s)) -- open in Blender.");
        });
    }

    private static unsafe void ExportTexturePng(GL gl, Texture2D tex, string path)
    {
        var pixels = new byte[tex.Width * tex.Height * 4];
        tex.Bind(0);
        fixed (byte* p = pixels)
            gl.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        using var fs = File.Create(path);
        new StbImageWriteSharp.ImageWriter().WritePng(pixels, (int)tex.Width, (int)tex.Height,
            StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
    }

    private static Matrix4x4 Matrix4x4Invert(Matrix4x4 m)
        => Matrix4x4.Invert(m, out var inv) ? inv : Matrix4x4.Identity;

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Length == 0 ? "object" : name;
    }
}

file static class FloatExt
{
    public static string ToStr(this float f) => f.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
