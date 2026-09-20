using System.Numerics;
using System.Text;
using CrashEngine.Core;
using CrashEngine.Importer;
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

    private sealed class ObjImportPart
    {
        public string Name = "part";
        public readonly List<GpuMesh.Vertex> Verts = new();
        public readonly List<uint> Idx = new();
        public readonly Dictionary<(int p, int u, int n), uint> VertKey = new();
        public string? Material;
    }

    private void ImportSelectedObjectFromObj()
    {
        ShowOpenFileDialog("Import Object (OBJ + textures)", "Wavefront OBJ\0*.obj\0All Files\0*.*\0\0", path =>
        {
        if (path is null) return;
        var dir = Path.GetDirectoryName(path) ?? ".";

        var positions = new List<Vector3>();
        var normals   = new List<Vector3>();
        var uvs       = new List<Vector2>();
        var parts     = new List<ObjImportPart>();
        ObjImportPart? cur = null;
        string? mtlLib = null;
        int skippedFaces = 0, partCounter = 0;

        ObjImportPart CurrentPart(string? nameHint = null)
        {
            if (cur is null || nameHint is not null)
            {
                cur = new ObjImportPart { Name = nameHint ?? $"part_{++partCounter}" };
                parts.Add(cur);
            }
            return cur;
        }

        uint ResolveVertex(ObjImportPart part, int pi, int ui, int ni)
        {
            var key = (pi, ui, ni);
            if (part.VertKey.TryGetValue(key, out var existing)) return existing;
            var v = new GpuMesh.Vertex
            {
                Position = pi >= 0 && pi < positions.Count ? positions[pi] : Vector3.Zero,
                Normal   = ni >= 0 && ni < normals.Count ? normals[ni] : Vector3.UnitY,
                UV       = ui >= 0 && ui < uvs.Count ? uvs[ui] : Vector2.Zero,
                Color    = Vector4.One,
            };
            uint idx = (uint)part.Verts.Count;
            part.Verts.Add(v);
            part.VertKey[key] = idx;
            return idx;
        }

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line[0] == '#')
            {
                var name = line[1..].Trim();
                if (name.Length > 0 && !name.StartsWith("CrashEngine", StringComparison.OrdinalIgnoreCase))
                    CurrentPart(name);
                continue;
            }
            var parts2 = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts2.Length == 0) continue;

            switch (parts2[0])
            {
                case "mtllib" when parts2.Length >= 2:
                    mtlLib = parts2[1];
                    break;
                case "v" when parts2.Length >= 4:
                    positions.Add(new Vector3(-float.Parse(parts2[1], inv), float.Parse(parts2[2], inv), float.Parse(parts2[3], inv)));
                    break;
                case "vn" when parts2.Length >= 4:
                    normals.Add(new Vector3(-float.Parse(parts2[1], inv), float.Parse(parts2[2], inv), float.Parse(parts2[3], inv)));
                    break;
                case "vt" when parts2.Length >= 3:
                    uvs.Add(new Vector2(float.Parse(parts2[1], inv), 1f - float.Parse(parts2[2], inv)));
                    break;
                case "usemtl" when parts2.Length >= 2:
                {
                    var p = CurrentPart();
                    if (p.Material is not null && p.Material != parts2[1] && p.Verts.Count > 0)
                        p = CurrentPart($"part_{++partCounter}");
                    p.Material = parts2[1];
                    break;
                }
                case "f" when parts2.Length >= 4:
                {
                    var p = CurrentPart();
                    var refs = new (int p, int u, int n)[parts2.Length - 1];
                    bool ok = true;
                    for (int i = 1; i < parts2.Length; i++)
                    {
                        var tok = parts2[i].Split('/');
                        int P(string s, int count) => string.IsNullOrEmpty(s) ? -1 : (int.Parse(s, inv) is var n && n > 0 ? n - 1 : count + n);
                        int pi = tok.Length > 0 ? P(tok[0], positions.Count) : -1;
                        int ui = tok.Length > 1 ? P(tok[1], uvs.Count) : -1;
                        int ni = tok.Length > 2 ? P(tok[2], normals.Count) : -1;
                        if (pi < 0 || pi >= positions.Count) { ok = false; break; }
                        refs[i - 1] = (pi, ui, ni);
                    }
                    if (!ok) { skippedFaces++; continue; }
                    var idx = refs.Select(r => ResolveVertex(p, r.p, r.u, r.n)).ToArray();
                    for (int i = 1; i + 1 < idx.Length; i++)
                    { p.Idx.Add(idx[0]); p.Idx.Add(idx[i + 1]); p.Idx.Add(idx[i]); }
                    break;
                }
            }
        }
        parts.RemoveAll(p => p.Verts.Count == 0 || p.Idx.Count == 0);

        var materialToTexFile = new Dictionary<string, string>();
        if (mtlLib is not null && File.Exists(Path.Combine(dir, mtlLib)))
        {
            string? curMat = null;
            foreach (var rawLine in File.ReadAllLines(Path.Combine(dir, mtlLib)))
            {
                var line = rawLine.Trim();
                var mp = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (mp.Length == 0) continue;
                if (mp[0] == "newmtl" && mp.Length >= 2) curMat = mp[1];
                else if (mp[0] == "map_Kd" && mp.Length >= 2 && curMat is not null)
                    materialToTexFile[curMat] = Path.IsPathRooted(mp[1]) ? mp[1] : Path.Combine(dir, mp[1]);
            }
        }

        if (parts.Count == 0)
        { _browser.Log($"Import Object: no usable geometry found in {path}."); return; }

        var texCache = new Dictionary<string, Texture2D>();
        Texture2D? LoadTex(string p) =>
            texCache.TryGetValue(p, out var t) ? t :
            File.Exists(p) ? texCache[p] = Texture2D.FromFile(Engine.Instance.GL, p)! : null;

        var rootName = Path.GetFileNameWithoutExtension(path);
        var root = new Entity(rootName);
        foreach (var part in parts)
        {
            var child = new Entity(part.Name);
            var mat = new Material();
            var bmin = new Vector3(float.MaxValue); var bmax = new Vector3(float.MinValue);
            foreach (var v in part.Verts) { bmin = Vector3.Min(bmin, v.Position); bmax = Vector3.Max(bmax, v.Position); }
            mat.LocalCenter    = (bmin + bmax) * 0.5f;
            mat.BoundingRadius = Vector3.Distance(bmin, bmax) * 0.5f;
            var mr = new ImporterMeshRenderer
            {
                Mesh     = new GpuMesh(Engine.Instance.GL, part.Verts.ToArray(), part.Idx.ToArray()),
                Material = mat,
            };
            if (part.Material is not null && materialToTexFile.TryGetValue(part.Material, out var texPath))
                mr.Material.Albedo = LoadTex(texPath);
            child.Add(mr);
            root.AddChild(child);
        }
        var cameraWorldPos = _camera?.Transform.Position ?? Vector3.Zero;
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var sceneryHome = chunkRoot is not null ? AllEntities(chunkRoot).FirstOrDefault(e => e.Name == "Scenery") : null;
        if (sceneryHome is not null)
        {
            sceneryHome.AddChild(root);
            root.Transform.Position = Matrix4x4.Invert(sceneryHome.Transform.World, out var sceneryInv)
                ? Vector3.Transform(cameraWorldPos, sceneryInv) : cameraWorldPos;
        }
        else
        {
            root.Transform.Position = cameraWorldPos;
            LoadAndAddRoot(root);
        }

        _browser.Log($"Imported {path} as new object '{rootName}' at the camera position " +
                     $"({parts.Count} part(s), {materialToTexFile.Count} material(s)" +
                     (skippedFaces > 0 ? $", {skippedFaces} face(s) skipped" : "") +
                     ") — LIVE PREVIEW ONLY, not written into the real PS2 chunk format.");
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
