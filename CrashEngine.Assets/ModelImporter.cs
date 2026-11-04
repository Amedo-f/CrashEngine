using Assimp;
using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using SysVec2 = System.Numerics.Vector2;
using SysVec3 = System.Numerics.Vector3;
using SysVec4 = System.Numerics.Vector4;
using RMat    = CrashEngine.Renderer.Material;
using AScene  = Assimp.Scene;
using AMesh   = Assimp.Mesh;

namespace CrashEngine.Assets;

public sealed class RawSubmesh
{
    public required SysVec3[] Positions;
    public required SysVec3[] Normals;
    public required SysVec2[] UVs;
    public required uint[]    Indices;
    public byte[]?            DiffusePixelsRGBA;
    public int                TexWidth, TexHeight;
    public string             Name = "mesh";

    public bool?               CeUnlit;
    public bool?               CeAlphaBlend;
    public SysVec4[]?          Colors; // Amedo
    public float?              AlphaCutoff; // Amedo
}

public static class ModelImporter
{
    private static readonly string[] GltfExts   = { ".glb", ".gltf" };
    private static readonly string[] AssimpExts  = { ".fbx", ".obj", ".dae", ".3ds", ".ply", ".stl" };

    public static readonly string FileFilter =
        "3D Models\0*.glb;*.gltf;*.fbx;*.obj;*.dae;*.3ds;*.ply;*.stl\0" +
        "GLB / GLTF\0*.glb;*.gltf\0" +
        "FBX\0*.fbx\0" +
        "OBJ\0*.obj\0" +
        "All Files\0*.*\0\0";

    public static string LastReport { get; private set; } = ""; // Amedo 2026-10-02

    public static List<RawSubmesh>? ExtractRawSubmeshes(string path)
    {
        LastReport = ""; // Amedo 2026-10-02
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (Array.IndexOf(GltfExts, ext) >= 0) return ExtractRawGltf(path);
            if (Array.IndexOf(AssimpExts, ext) >= 0) return ExtractRawAssimp(path);
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModelImporter] ExtractRawSubmeshes failed: {ex}");
            LastReport = $"could not read {Path.GetFileName(path)}: {ex.Message}"; // Amedo
            return null;
        }
    }

    private const string AssimpDefaultMaterial = "DefaultMaterial"; // Amedo
    private const string ReadableImages = "PNG, JPG, TGA, BMP, GIF, PSD, HDR"; // Amedo

    // Amedo
    private static float LinearToSrgb(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

    // Amedo
    private static SysVec4 ToSrgb(SysVec4 c)
    {
        c = SysVec4.Clamp(c, SysVec4.Zero, SysVec4.One);
        return new SysVec4(LinearToSrgb(c.X), LinearToSrgb(c.Y), LinearToSrgb(c.Z), c.W);
    }

    private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255); // Amedo

    // Amedo
    private static bool HasAlpha(byte[] rgba)
    {
        for (int i = 3; i < rgba.Length; i += 4)
            if (rgba[i] != 255) return true;
        return false;
    }

    // Amedo
    private static (byte[] Pixels, int W, int H)? Tinted((byte[] Pixels, int W, int H)? tex, SysVec4 tint, bool opaque)
    {
        tint = SysVec4.Clamp(tint, SysVec4.Zero, SysVec4.One);
        bool rgbPlain = tint.X >= 0.999f && tint.Y >= 0.999f && tint.Z >= 0.999f;
        if (tex is not { } t)
        {
            if (rgbPlain && (opaque || tint.W >= 0.999f)) return null;
            const int n = 8;
            var solid = new byte[n * n * 4];
            for (int i = 0; i < solid.Length; i += 4)
            {
                solid[i] = ToByte(tint.X); solid[i + 1] = ToByte(tint.Y); solid[i + 2] = ToByte(tint.Z);
                solid[i + 3] = opaque ? (byte)255 : ToByte(tint.W);
            }
            return (solid, n, n);
        }
        bool alphaPlain = opaque ? !HasAlpha(t.Pixels) : tint.W >= 0.999f;
        if (rgbPlain && alphaPlain) return t;
        var src = t.Pixels;
        var dst = new byte[src.Length];
        for (int i = 0; i + 3 < src.Length; i += 4)
        {
            dst[i]     = rgbPlain ? src[i]     : (byte)MathF.Round(src[i]     * tint.X);
            dst[i + 1] = rgbPlain ? src[i + 1] : (byte)MathF.Round(src[i + 1] * tint.Y);
            dst[i + 2] = rgbPlain ? src[i + 2] : (byte)MathF.Round(src[i + 2] * tint.Z);
            dst[i + 3] = opaque ? (byte)255 : (byte)MathF.Round(src[i + 3] * tint.W);
        }
        return (dst, t.W, t.H);
    }

    private static Dictionary<string, (bool Unlit, bool AlphaBlend)> ParseCrashEngineMtlHints(string objPath)
    {
        var result = new Dictionary<string, (bool, bool)>();
        string? mtlLib = null;
        foreach (var rawLine in File.ReadLines(objPath))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("mtllib ", StringComparison.Ordinal)) { mtlLib = line[7..].Trim(); break; }
            if (line.Length > 0 && line[0] == 'v') break;
        }
        if (mtlLib is null) return result;
        var mtlPath = Path.Combine(Path.GetDirectoryName(objPath) ?? "", mtlLib);
        if (!File.Exists(mtlPath)) return result;

        string? curMat = null; bool unlit = false, alphaBlend = false, seen = false;
        void Flush() { if (curMat is not null && seen) result[curMat] = (unlit, alphaBlend); }
        foreach (var rawLine in File.ReadLines(mtlPath))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("newmtl ", StringComparison.Ordinal))
            {
                Flush();
                curMat = line[7..].Trim();
                unlit = false; alphaBlend = false; seen = false;
            }
            else if (line.StartsWith("# CE_Unlit ", StringComparison.Ordinal))
                { unlit = line[11..].Trim() == "1"; seen = true; }
            else if (line.StartsWith("# CE_AlphaBlend ", StringComparison.Ordinal))
                { alphaBlend = line[16..].Trim() == "1"; seen = true; }
        }
        Flush();
        return result;
    }

    private static List<RawSubmesh> ExtractRawAssimp(string path)
    {
        using var ctx = new AssimpContext();
        var scene = ctx.ImportFile(path,
            PostProcessSteps.Triangulate      |
            PostProcessSteps.GenerateNormals  |
            PostProcessSteps.FlipUVs          |
            PostProcessSteps.GenerateUVCoords |
            PostProcessSteps.JoinIdenticalVertices);

        var ceHints = Path.GetExtension(path).Equals(".obj", StringComparison.OrdinalIgnoreCase)
            ? ParseCrashEngineMtlHints(path) : null;

        var result   = new List<RawSubmesh>();
        var modelDir = Path.GetDirectoryName(path) ?? "";
        float unit = 1f; // Amedo 2026-10-02
        var notes = new List<string>();
        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var unreadable = new SortedSet<string>(StringComparer.OrdinalIgnoreCase); // Amedo
        int colorsLeftOut = 0; // Amedo
        var tintCache = new Dictionary<(int, bool), (byte[] Pixels, int W, int H)?>(); // Amedo
        if (Path.GetExtension(path).Equals(".fbx", StringComparison.OrdinalIgnoreCase) && FbxUnitScaleFactor(path) is { } cm && cm > 0)
        {
            unit = (float)(cm / 100.0);
            if (unit != 1f) notes.Add(FormattableString.Invariant($"FBX unit = {cm:0.###} cm, scaled x{unit:0.####} to meters"));
        }

        (byte[] Pixels, int W, int H)? DecodeEmbedded(EmbeddedTexture tex)
        {
            try
            {
                if (tex.IsCompressed && tex.CompressedData is { Length: > 0 })
                {
                    using var ms = new MemoryStream(tex.CompressedData);
                    var img = StbImageSharp.ImageResult.FromStream(ms, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                    return (img.Data, img.Width, img.Height);
                }
                if (tex.HasNonCompressedData && tex.Width > 0 && tex.Height > 0)
                {
                    var texels = tex.NonCompressedData;
                    var px = new byte[tex.Width * tex.Height * 4];
                    int count = Math.Min(texels.Length, tex.Width * tex.Height);
                    for (int i = 0; i < count; i++)
                    {
                        px[i * 4 + 0] = texels[i].R; px[i * 4 + 1] = texels[i].G;
                        px[i * 4 + 2] = texels[i].B; px[i * 4 + 3] = texels[i].A;
                    }
                    return (px, tex.Width, tex.Height);
                }
            }
            catch { }
            return null;
        }
        (byte[] Pixels, int W, int H)? DecodeExternal(string fp)
        {
            string? found = null;
            var direct = Path.IsPathRooted(fp) ? fp : Path.Combine(modelDir, fp);
            if (File.Exists(direct)) found = direct;
            var basename = Path.GetFileName(fp);
            if (found is null && !string.IsNullOrEmpty(basename))
            {
                var sibling = Path.Combine(modelDir, basename);
                if (File.Exists(sibling)) found = sibling;
                else { try { found = Directory.EnumerateFiles(modelDir, basename, SearchOption.AllDirectories).FirstOrDefault(); } catch { } }
            }
            if (found is null) return null;
            try
            {
                using var stream = File.OpenRead(found);
                var img = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                return (img.Data, img.Width, img.Height);
            }
            catch { unreadable.Add(fp); return null; } // Amedo
        }

        var texPixelCache = new Dictionary<int, (byte[] Pixels, int W, int H)?>();
        (byte[] Pixels, int W, int H)? GetDiffusePixels(int matIdx)
        {
            if (texPixelCache.TryGetValue(matIdx, out var cached)) return cached;
            (byte[], int, int)? result2 = null;
            if (matIdx >= 0 && matIdx < scene.MaterialCount)
            {
                var ai = scene.Materials[matIdx];
                var allSlots = ai.GetAllMaterialTextures();
                string? fp = null;
                foreach (var want in new[] { TextureType.Diffuse, TextureType.Emissive, TextureType.Ambient, TextureType.Lightmap })
                {
                    foreach (var s in allSlots)
                        if (s.TextureType == want && !string.IsNullOrEmpty(s.FilePath)) { fp = s.FilePath; break; }
                    if (fp is not null) break;
                }
                if (fp is null)
                    foreach (var s in allSlots)
                        if (s.TextureType is TextureType.Unknown or TextureType.None && !string.IsNullOrEmpty(s.FilePath)) { fp = s.FilePath; break; } // Amedo

                if (fp is not null)
                {
                    if (fp.StartsWith("*") && int.TryParse(fp.AsSpan(1), out int ei) &&
                        scene.HasTextures && ei >= 0 && ei < scene.TextureCount)
                    {
                        result2 = DecodeEmbedded(scene.Textures[ei]);
                        if (result2 is null) unreadable.Add($"embedded {fp}"); // Amedo
                    }
                    if (result2 is null && !fp.StartsWith("*")) result2 = DecodeExternal(fp);
                    if (result2 is null && !fp.StartsWith("*") && !unreadable.Contains(fp)) missing.Add(fp); // Amedo
                }
            }
            texPixelCache[matIdx] = result2;
            return result2;
        }

        void Walk(Node node, Matrix4x4 parentWorld)
        {
            var local = node.Transform;
            var world = local * parentWorld;

            foreach (var meshIdx in node.MeshIndices)
            {
                var ai = scene.Meshes[meshIdx];
                if (ai.VertexCount == 0 || !ai.HasFaces) continue;

                bool hasN = ai.HasNormals;
                bool hasU = ai.HasTextureCoords(0);
                var positions = new SysVec3[ai.VertexCount];
                var normals   = new SysVec3[ai.VertexCount];
                var uvs       = new SysVec2[ai.VertexCount];
                for (int i = 0; i < ai.VertexCount; i++)
                {
                    var p  = ai.Vertices[i];
                    var wp = world * p;
                    positions[i] = new SysVec3(wp.X, wp.Y, wp.Z) * unit; // Amedo 2026-10-02

                    var n  = hasN ? ai.Normals[i] : new Vector3D(0, 1, 0);
                    var wn  = world * n;
                    var wo  = world * new Vector3D(0, 0, 0);
                    var dn  = new SysVec3(wn.X - wo.X, wn.Y - wo.Y, wn.Z - wo.Z);
                    normals[i] = GameMath.LengthSquared(dn) > 1e-8f ? SysVec3.Normalize(dn) : SysVec3.UnitY;

                    var u = hasU ? ai.TextureCoordinateChannels[0][i] : new Vector3D(0, 0, 0);
                    uvs[i] = new SysVec2(u.X, u.Y);
                }

                var idx = new List<uint>(ai.FaceCount * 3);
                foreach (var face in ai.Faces)
                    if (face.IndexCount == 3)
                        foreach (var i in face.Indices) idx.Add((uint)i);
                if (idx.Count == 0) continue;

                var diff = GetDiffusePixels(ai.MaterialIndex);
                var matName = ai.MaterialIndex >= 0 && ai.MaterialIndex < scene.MaterialCount
                    ? scene.Materials[ai.MaterialIndex].Name : null;
                (bool Unlit, bool AlphaBlend) hint = default;
                bool haveHint = matName is not null && ceHints is not null && ceHints.TryGetValue(matName, out hint);

                var aiMat = ai.MaterialIndex >= 0 && ai.MaterialIndex < scene.MaterialCount ? scene.Materials[ai.MaterialIndex] : null; // Amedo
                float opacity = aiMat is { HasOpacity: true, Opacity: > 0.01f and < 0.99f } ? aiMat.Opacity : 1f; // Amedo
                SysVec4[]? colors = null; // Amedo
                if (ai.HasVertexColors(0) && ai.VertexColorChannels[0] is { Count: > 0 } ch &&
                    ch.Any(c => MathF.Abs(c.R - ch[0].R) > 0.004f || MathF.Abs(c.G - ch[0].G) > 0.004f || MathF.Abs(c.B - ch[0].B) > 0.004f))
                {
                    if (diff is null)
                    {
                        colors = new SysVec4[ai.VertexCount];
                        for (int i = 0; i < ai.VertexCount && i < ch.Count; i++) colors[i] = new SysVec4(ch[i].R, ch[i].G, ch[i].B, opacity < 1f ? ch[i].A : 1f);
                    }
                    else colorsLeftOut++;
                }
                var tint = diff is null && colors is null && aiMat is { HasColorDiffuse: true } && aiMat.Name != AssimpDefaultMaterial
                    ? ToSrgb(new SysVec4(aiMat.ColorDiffuse.R, aiMat.ColorDiffuse.G, aiMat.ColorDiffuse.B, 1f)) : SysVec4.One; // Amedo
                tint.W = opacity;
                var tintKey = (ai.MaterialIndex, colors is not null); // Amedo
                if (!tintCache.TryGetValue(tintKey, out var tinted)) tintCache[tintKey] = tinted = Tinted(diff, tint, false);
                diff = tinted;

                result.Add(new RawSubmesh
                {
                    Positions = positions, Normals = normals, UVs = uvs, Indices = idx.ToArray(),
                    DiffusePixelsRGBA = diff?.Pixels, TexWidth = diff?.W ?? 0, TexHeight = diff?.H ?? 0,
                    Name = ai.Name.Length > 0 ? ai.Name : $"mesh_{meshIdx}",
                    CeUnlit      = haveHint ? hint.Unlit      : null,
                    CeAlphaBlend = haveHint ? hint.AlphaBlend : opacity < 1f ? true : null, // Amedo
                    Colors       = colors, // Amedo
                });
            }

            foreach (var c in node.Children) Walk(c, world);
        }
        Walk(scene.RootNode, Matrix4x4.Identity);
        foreach (var fp in missing) // Amedo 2026-10-02
            notes.Add($"texture not found: '{fp}' (looked at that path, then for '{Path.GetFileName(fp)}' in {modelDir} and its subfolders) — the mesh uses its material colour"); // Amedo
        foreach (var fp in unreadable) // Amedo
            notes.Add($"texture '{fp}' is in a format that can't be read (readable: {ReadableImages}) — the mesh uses its material colour");
        if (colorsLeftOut > 0) // Amedo
            notes.Add($"{colorsLeftOut} textured mesh(es) also carry painted vertex colours; they're left out (the texture gives the colour)");
        if (scene.HasAnimations || scene.Meshes.Any(m => m.HasBones))
            notes.Add($"skeleton/animations ({scene.AnimationCount} clips) are not imported yet — the model comes in its bind pose");
        LastReport = string.Join("; ", notes);
        return result;
    }

    // Amedo 2026-10-02
    public static double? FbxUnitScaleFactor(string path)
    {
        try
        {
            var d = File.ReadAllBytes(path);
            var key = System.Text.Encoding.ASCII.GetBytes("UnitScaleFactor");
            bool binary = d.Length > 20 && System.Text.Encoding.ASCII.GetString(d, 0, 18) == "Kaydara FBX Binary";
            for (int at = d.AsSpan().IndexOf(key); at >= 0;)
            {
                int i = at + key.Length;
                if (at > 0 && char.IsLetter((char)d[at - 1])) { }
                else if (binary)
                {
                    for (int s = 0; s < 3 && i + 5 <= d.Length && d[i] == (byte)'S'; s++) i += 5 + BitConverter.ToInt32(d, i + 1);
                    if (i + 9 <= d.Length && d[i] == (byte)'D') return BitConverter.ToDouble(d, i + 1);
                }
                else
                {
                    var line = System.Text.Encoding.ASCII.GetString(d, i, Math.Min(120, d.Length - i));
                    var m = System.Text.RegularExpressions.Regex.Match(line, "^\"\\s*,\\s*\"double\"\\s*,\\s*\"Number\"\\s*,\\s*\"[^\"]*\"\\s*,\\s*([-+0-9.eE]+)");
                    if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v)) return v;
                }
                int next = d.AsSpan(at + 1).IndexOf(key);
                at = next < 0 ? -1 : at + 1 + next;
            }
        }
        catch { }
        return null;
    }

    private static List<RawSubmesh> ExtractRawGltf(string path)
    {
        var model  = SharpGLTF.Schema2.ModelRoot.Load(path);
        var result = new List<RawSubmesh>();
        var unreadable = new SortedSet<string>(StringComparer.OrdinalIgnoreCase); // Amedo

        // Amedo
        static (byte[] Pixels, int W, int H)? Decode(SharpGLTF.Schema2.Image? image)
        {
            var img = image?.Content;
            if (img is not { IsValid: true }) return null;
            try
            {
                using var stream = img.Value.Open();
                var decoded = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                return (decoded.Data, decoded.Width, decoded.Height);
            }
            catch { return null; }
        }

        static SharpGLTF.Schema2.MaterialChannel? BaseColorOf(SharpGLTF.Schema2.Material? gm) =>
            gm is null ? null : gm.FindChannel("BaseColor") ?? gm.FindChannel("Diffuse"); // Amedo

        var texPixelCache = new Dictionary<int, (byte[] Pixels, int W, int H)?>();
        (byte[] Pixels, int W, int H)? GetDiffusePixels(SharpGLTF.Schema2.Material? gm)
        {
            if (gm is null) return null;
            if (texPixelCache.TryGetValue(gm.LogicalIndex, out var cached)) return cached;
            var baseColor = BaseColorOf(gm); // Amedo
            var texture = baseColor?.Texture;
            var pixels = Decode(texture?.PrimaryImage) ?? Decode(texture?.FallbackImage);
            if (pixels is null && texture?.PrimaryImage is { } bad)
                unreadable.Add($"{bad.Name ?? $"image {bad.LogicalIndex}"} ({(bad.Content.IsValid ? bad.Content.FileExtension : "unknown format")})");
            var result2 = Tinted(pixels, baseColor is { } c ? ToSrgb(c.Color) : SysVec4.One, gm.Alpha == SharpGLTF.Schema2.AlphaMode.OPAQUE);
            texPixelCache[gm.LogicalIndex] = result2;
            return result2;
        }

        void Walk(SharpGLTF.Schema2.Node node, System.Numerics.Matrix4x4 parentWorld)
        {
            var world = node.LocalMatrix * parentWorld;
            if (node.Mesh is not null)
            {
                foreach (var prim in node.Mesh.Primitives)
                {
                    var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
                    if (positions is null || positions.Count == 0) continue;
                    var normalsAcc = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                    var baseColor  = BaseColorOf(prim.Material); // Amedo
                    var uvTransform = baseColor?.TextureTransform;
                    int uvSet = uvTransform?.TextureCoordinateOverride ?? baseColor?.TextureCoordinate ?? 0;
                    var uvsAcc     = (prim.GetVertexAccessor($"TEXCOORD_{uvSet}") ?? prim.GetVertexAccessor("TEXCOORD_0"))?.AsVector2Array(); // Amedo
                    var colorsAcc  = prim.GetVertexAccessor("COLOR_0")?.AsColorArray(1f); // Amedo
                    var alpha = prim.Material?.Alpha ?? SharpGLTF.Schema2.AlphaMode.OPAQUE; // Amedo

                    int n = positions.Count;
                    var pos = new SysVec3[n];
                    var nrm = new SysVec3[n];
                    var uv  = new SysVec2[n];
                    var col = colorsAcc is not null ? new SysVec4[n] : null; // Amedo
                    for (int i = 0; i < n; i++)
                    {
                        pos[i] = SysVec3.Transform(positions[i], world);
                        var localN = normalsAcc is not null ? normalsAcc[i] : SysVec3.UnitY;
                        var wn = SysVec3.TransformNormal(localN, world);
                        nrm[i] = GameMath.LengthSquared(wn) > 1e-8f ? SysVec3.Normalize(wn) : SysVec3.UnitY;
                        uv[i]  = uvsAcc is not null ? new SysVec2(uvsAcc[i].X, uvsAcc[i].Y) : SysVec2.Zero;
                        if (uvTransform is not null) uv[i] = SysVec2.Transform(uv[i], uvTransform.Matrix); // Amedo
                        if (col is not null) // Amedo
                        {
                            col[i] = ToSrgb(colorsAcc![i]);
                            if (alpha == SharpGLTF.Schema2.AlphaMode.OPAQUE) col[i].W = 1f;
                        }
                    }
                    var tris = new List<uint>(); // Amedo
                    foreach (var (a, b, c) in prim.GetTriangleIndices()) { tris.Add((uint)a); tris.Add((uint)b); tris.Add((uint)c); }
                    if (tris.Count == 0) continue;
                    uint[] idx = tris.ToArray();

                    var diff = GetDiffusePixels(prim.Material);
                    result.Add(new RawSubmesh
                    {
                        Positions = pos, Normals = nrm, UVs = uv, Indices = idx,
                        DiffusePixelsRGBA = diff?.Pixels, TexWidth = diff?.W ?? 0, TexHeight = diff?.H ?? 0,
                        Name = node.Name ?? node.Mesh.Name ?? "mesh",
                        CeUnlit      = prim.Material?.Unlit == true ? true : null, // Amedo
                        CeAlphaBlend = alpha == SharpGLTF.Schema2.AlphaMode.BLEND, // Amedo
                        AlphaCutoff  = alpha == SharpGLTF.Schema2.AlphaMode.MASK ? prim.Material!.AlphaCutoff : null, // Amedo
                        Colors       = col, // Amedo
                    });
                }
            }
            foreach (var c in node.VisualChildren) Walk(c, world);
        }
        foreach (var scene in model.LogicalScenes)
            foreach (var node in scene.VisualChildren)
                Walk(node, System.Numerics.Matrix4x4.Identity);
        LastReport = string.Join("; ", unreadable.Select(u => $"texture {u} is in a format that can't be read (readable: {ReadableImages}) — the mesh uses its material colour")); // Amedo
        return result;
    }

    public static Entity? Load(GL gl, string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (Array.IndexOf(GltfExts, ext) >= 0)
                return GlbLoader.Load(gl, path);
            if (Array.IndexOf(AssimpExts, ext) >= 0)
                return LoadAssimp(gl, path);
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModelImporter] Failed: {ex}");
            return null;
        }
    }


    private static Entity LoadAssimp(GL gl, string path)
    {
        using var ctx = new AssimpContext();
        var scene = ctx.ImportFile(path,
            PostProcessSteps.Triangulate      |
            PostProcessSteps.GenerateNormals  |
            PostProcessSteps.FlipUVs          |
            PostProcessSteps.GenerateUVCoords |
            PostProcessSteps.JoinIdenticalVertices);

        var root       = new Entity(Path.GetFileNameWithoutExtension(path));
        var embedded   = BuildEmbeddedTextures(gl, scene);
        var modelDir   = Path.GetDirectoryName(path) ?? "";

        WalkNode(gl, scene, scene.RootNode, root, embedded, modelDir);
        return root;
    }

    private static Dictionary<int, Texture2D> BuildEmbeddedTextures(GL gl, AScene scene)
    {
        var map = new Dictionary<int, Texture2D>();
        if (!scene.HasTextures) return map;
        for (int i = 0; i < scene.TextureCount; i++)
        {
            var tex = scene.Textures[i];
            if (tex.IsCompressed)
            {
                try
                {
                    using var ms = new MemoryStream(tex.CompressedData);
                    var img = StbImageSharp.ImageResult.FromStream(ms,
                        StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                    map[i] = new Texture2D(gl, img.Data, (uint)img.Width, (uint)img.Height);
                    continue;
                }
                catch { }
            }
            map[i] = Texture2D.White(gl);
        }
        return map;
    }

    private static void WalkNode(GL gl, AScene scene, Node node,
                                  Entity parent,
                                  Dictionary<int, Texture2D> embedded,
                                  string modelDir)
    {
        var entity = new Entity(string.IsNullOrEmpty(node.Name) ? "Node" : node.Name);
        node.Transform.Decompose(out var scale, out var rot, out var trans);
        entity.Transform.Position = new SysVec3(trans.X, trans.Y, trans.Z);
        entity.Transform.Rotation = new System.Numerics.Quaternion(rot.X, rot.Y, rot.Z, rot.W);
        entity.Transform.Scale    = new SysVec3(scale.X, scale.Y, scale.Z);
        parent.AddChild(entity);

        foreach (var meshIdx in node.MeshIndices)
        {
            var ai  = scene.Meshes[meshIdx];
            var gpu = BuildGpuMesh(gl, ai);
            if (gpu is null) continue;

            var mat    = BuildMaterial(gl, scene, ai.MaterialIndex, embedded, modelDir);
            var child  = new Entity(ai.Name.Length > 0 ? ai.Name : $"mesh_{meshIdx}");
            child.Add(new MeshRenderer { Mesh = gpu, Material = mat });
            entity.AddChild(child);
        }

        foreach (var c in node.Children)
            WalkNode(gl, scene, c, entity, embedded, modelDir);
    }

    private static GpuMesh? BuildGpuMesh(GL gl, AMesh ai)
    {
        if (ai.VertexCount == 0 || !ai.HasFaces) return null;

        bool hasN = ai.HasNormals;
        bool hasU = ai.HasTextureCoords(0);
        bool hasC = ai.HasVertexColors(0);

        var verts = new GpuMesh.Vertex[ai.VertexCount];
        for (int i = 0; i < ai.VertexCount; i++)
        {
            var p = ai.Vertices[i];
            var n = hasN ? ai.Normals[i]                      : new Vector3D(0, 1, 0);
            var u = hasU ? ai.TextureCoordinateChannels[0][i] : new Vector3D(0, 0, 0);
            var c = hasC ? ai.VertexColorChannels[0][i]       : new Color4D(1, 1, 1, 1);
            verts[i] = new GpuMesh.Vertex
            {
                Position = new SysVec3(p.X, p.Y, p.Z),
                Normal   = new SysVec3(n.X, n.Y, n.Z),
                UV       = new SysVec2(u.X, u.Y),
                Color    = new SysVec4(c.R, c.G, c.B, c.A),
            };
        }

        var idx = new List<uint>(ai.FaceCount * 3);
        foreach (var face in ai.Faces)
            foreach (var i in face.Indices)
                idx.Add((uint)i);

        return idx.Count == 0 ? null : new GpuMesh(gl, verts, idx.ToArray());
    }

    private static RMat BuildMaterial(GL gl, AScene scene, int matIdx,
                                       Dictionary<int, Texture2D> embedded,
                                       string modelDir)
    {
        var mat = new RMat { Culling = RMat.CullMode.Both, Unlit = true };
        if (matIdx < 0 || matIdx >= scene.MaterialCount) return mat;

        var ai = scene.Materials[matIdx];

        if (ai.HasColorDiffuse)
        {
            var c = ai.ColorDiffuse;
            mat.BaseColor = new SysVec4(c.R, c.G, c.B, c.A);
        }

        if (ai.HasTextureDiffuse)
        {
            var slot = ai.TextureDiffuse;
            if (slot.FilePath.StartsWith("*"))
            {
                if (int.TryParse(slot.FilePath[1..], out int ei) &&
                    embedded.TryGetValue(ei, out var et))
                    mat.Albedo = et;
            }
            else
            {
                var tp = slot.FilePath;
                if (!Path.IsPathRooted(tp))
                    tp = Path.Combine(modelDir, tp);
                mat.Albedo = Texture2D.FromFile(gl, tp);
            }
        }

        return mat;
    }
}
