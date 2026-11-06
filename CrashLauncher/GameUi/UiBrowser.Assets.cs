using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;
using PS2PSM = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSM;
using PS2PSF = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSF;
using PS2PTC = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PTC;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private Texture2D? _uiZoomTex;
    private bool       _uiZoomFlip180;
    private bool       _uiZoomFlipV;

    // Amedo 2026-09-24
    private void DrawUiLegalBlackout()
    {
        ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), "Legal / Copyright screen image");
        ImGui.TextWrapped("The Legal screen itself can't be skipped: boot state 1 loads the fonts, Frontend.bin " +
                          "and Startup\\Default while it is shown, and state 2 then waits for the controller. " +
                          "This replaces the Legal image (Language\\Legal\\*.psm) with a solid black one through " +
                          "the project's SavedChunks override, so the same load time passes on a black screen. " +
                          "Build ISO picks it up automatically; no game code is changed.");
        var legalDir = System.IO.Path.Combine(SavedChunksDir, "Language", "Legal");
        bool active = System.IO.Directory.Exists(legalDir) && System.IO.Directory.GetFiles(legalDir, "*.psm").Length > 0;
        if (active) ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "Status: BLACKED OUT (override in SavedChunks\\Language\\Legal)");
        else ImGui.TextDisabled("Status: original image");

        ImGui.BeginDisabled(active);
        if (ImGui.Button("Black out Legal screen##legalblack")) BlackOutLegalScreen();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!active);
        if (ImGui.Button("Restore original image##legalrestore")) RestoreLegalScreen(legalDir);
        ImGui.EndDisabled();
    }

    private void BlackOutLegalScreen()
    {
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            var records = pkg.Records
                .Where(r => r.Path.StartsWith(@"Language\Legal\", StringComparison.OrdinalIgnoreCase) &&
                            r.Path.EndsWith(".psm", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (records.Count == 0) { _browser.Log("Legal blackout: no Language\\Legal\\*.psm found in the disc archive."); return; }

            int written = 0;
            foreach (var rec in records)
            {
                using var stream = pkg.OpenRecord(rec);
                using var reader = new System.IO.BinaryReader(stream);
                var psm = new PS2PSM();
                psm.Read(reader, (int)stream.Length);

                foreach (var ptc in psm.PTCs)
                {
                    if (ptc.Texture is not PS2AnyTexture tex) continue;
                    tex.CalculateData();
                    int w = tex.ImageWidthPower  > 0 ? 1 << tex.ImageWidthPower  : 1;
                    int h = tex.ImageHeightPower > 0 ? 1 << tex.ImageHeightPower : 1;
                    var black = new List<TwinColor>(w * h);
                    for (int i = 0; i < w * h; i++) black.Add(new TwinColor(0, 0, 0, 255));
                    tex.FromBitmap(black, w, tex.TexFun, tex.TextureFormat);
                }

                var dest = System.IO.Path.Combine(SavedChunksDir, rec.Path);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
                using (var fs = System.IO.File.Create(dest))
                using (var bw = new System.IO.BinaryWriter(fs))
                    psm.Write(bw);
                written++;

                foreach (var key in _uiTextureCache.Keys.Where(k => k.EndsWith(rec.Path, StringComparison.OrdinalIgnoreCase)).ToList())
                    _uiTextureCache.Remove(key);
            }
            _browser.Log($"Legal blackout: wrote {written} black Legal image(s) to SavedChunks\\Language\\Legal — Build ISO will include them.");
        }
        catch (Exception ex) { _browser.Log($"Legal blackout failed: {ex.Message}"); }
    }

    private void RestoreLegalScreen(string legalDir)
    {
        try
        {
            foreach (var f in System.IO.Directory.GetFiles(legalDir, "*.psm"))
            {
                System.IO.File.Delete(f);
                var rel = @"Language\Legal\" + System.IO.Path.GetFileName(f);
                foreach (var key in _uiTextureCache.Keys.Where(k => k.EndsWith(rel, StringComparison.OrdinalIgnoreCase)).ToList())
                    _uiTextureCache.Remove(key);
            }
            _browser.Log("Legal blackout: override removed — the original Legal image is back for the next build.");
        }
        catch (Exception ex) { _browser.Log($"Legal restore failed: {ex.Message}"); }
    }

    private static readonly string[] UiLanguages = { "English", "French", "German", "Italian", "Spanish" };

    private void DrawUiLanguageCombo(string id)
    {
        ImGui.SetNextItemWidth(160f);
        if (ImGui.BeginCombo(id, _uiLanguage))
        {
            foreach (var l in UiLanguages)
                if (ImGui.Selectable(l, l == _uiLanguage))
                    _uiLanguage = l;
            ImGui.EndCombo();
        }
    }

    private void DrawUiLegal()
    {
        DrawUiLanguageCombo("##uiLegalLang");
        ImGui.Separator();
        DrawUiPsmGroup(GetLegalPsmPaths(), kind: UiAssetKind.SplitFrames, flip180: true); // Amedo 2026-09-24
    }

    private void DrawUiLevelTitles()
    {
        DrawUiLanguageCombo("##uiLang");
        ImGui.Separator();

        var files = GetUiLanguageFiles(_uiLanguage);
        if (files is null) { ImGui.TextDisabled("(couldn't list files)"); return; }
        if (files.Count == 0) { ImGui.TextDisabled("(no title cards found for this language)"); return; }

        foreach (var path in files)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (ImGui.CollapsingHeader(name + "##uititle"))
                DrawUiPsmGroup(new[] { path }, flip180: true);
        }
    }

    // Amedo 2026-09-24
    private List<string> GetLegalPsmPaths()
    {
        const string key = "__legal__";
        if (_uiLangFileCache.TryGetValue(key, out var cached)) return cached;
        var list = new List<string>();
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            list = pkg.Records.Select(r => r.Path.TrimStart('\\'))
                .Where(p => p.StartsWith(@"Language\Legal\", StringComparison.OrdinalIgnoreCase) &&
                            p.EndsWith(".psm", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) { _browser.Log($"UI Browser: {ex.Message}"); }
        _uiLangFileCache[key] = list;
        return list;
    }

    private List<string>? GetUiLanguageFiles(string lang)
    {
        if (_uiLangFileCache.TryGetValue(lang, out var cached)) return cached;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            var prefix = $@"Language\Titles\{lang}\";
            var list = pkg.Records
                .Select(r => r.Path)
                .Where(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _uiLangFileCache[lang] = list;
            return list;
        }
        catch (Exception ex) { _browser.Log($"UI Browser: {ex.Message}"); return null; }
    }

    private void DrawUiPsmGroup(IReadOnlyList<string> paths, UiAssetKind kind = UiAssetKind.Psm, bool flip180 = false, bool flipV = false)
    {
        foreach (var path in paths)
        {
            ImGui.Text(System.IO.Path.GetFileName(path));
            var textures = GetOrDecodeUiAsset(path, kind);
            if (textures is null) { ImGui.TextDisabled("  (failed to decode — see Output log)"); continue; }
            DrawUiTextureGrid(textures, flip180, flipV);
            ImGui.Separator();
        }
    }

    private void DrawUiPtc(string path, bool flip180 = false, bool flipV = false)
    {
        ImGui.Text(System.IO.Path.GetFileName(path));
        var textures = GetOrDecodeUiAsset(path, UiAssetKind.Ptc);
        if (textures is null) { ImGui.TextDisabled("  (failed to decode — see Output log)"); return; }
        DrawUiTextureGrid(textures, flip180, flipV);
    }

    private void DrawUiPsfGroup(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            ImGui.Text(System.IO.Path.GetFileName(path));
            var textures = GetOrDecodeUiAsset(path, UiAssetKind.Psf);
            if (textures is null) { ImGui.TextDisabled("  (failed to decode — see Output log)"); continue; }
            DrawUiTextureGrid(textures, flipV: true);
            ImGui.Separator();
        }
    }

    private enum UiAssetKind { Psm, SplitFrames, Ptc, Psf }

    private List<Texture2D>? GetOrDecodeUiAsset(string path, UiAssetKind kind)
    {
        var cacheKey = $"{kind}:{path}";
        if (_uiTextureCache.TryGetValue(cacheKey, out var cached)) return cached;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir; // Amedo 2026-09-24
            using var stream = pkg.OpenByPath(path);
            if (stream is null) { _browser.Log($"UI Browser: '{path}' not found in the disc archive."); return null; }
            using var reader = new BinaryReader(stream);

            List<Texture2D> textures = kind switch
            {
                UiAssetKind.Psf         => DecodePsfFile(reader, (int)stream.Length),
                UiAssetKind.Ptc         => DecodePtcFile(reader, (int)stream.Length),
                UiAssetKind.SplitFrames => DecodeSplitFramesFile(reader, (int)stream.Length),
                _                       => DecodePsmFile(reader, (int)stream.Length),
            };
            _uiTextureCache[cacheKey] = textures;
            return textures;
        }
        catch (Exception ex)
        {
            _browser.Log($"UI Browser: failed to decode '{path}': {ex.Message}");
            return null;
        }
    }

    private List<Texture2D> DecodePsmFile(BinaryReader reader, int length)
    {
        var psm = new PS2PSM();
        psm.Read(reader, length);
        return MeshDecoder.DecodePsm(Engine.Instance.GL, psm);
    }

    private List<Texture2D> DecodeSplitFramesFile(BinaryReader reader, int length)
    {
        var psm = new PS2PSM();
        psm.Read(reader, length);
        return MeshDecoder.DecodeSplitFrames(Engine.Instance.GL, psm);
    }

    private List<Texture2D> DecodePtcFile(BinaryReader reader, int length)
    {
        var ptc = new PS2PTC();
        ptc.Read(reader, length);
        var tex = MeshDecoder.DecodeUiTexture(Engine.Instance.GL, ptc);
        return tex is null ? new List<Texture2D>() : new List<Texture2D> { tex };
    }

    private List<Texture2D> DecodePsfFile(BinaryReader reader, int length)
    {
        var psf = new PS2PSF();
        psf.Read(reader, length);
        return MeshDecoder.DecodePsf(Engine.Instance.GL, psf);
    }

    private void DrawUiTextureGrid(List<Texture2D> textures, bool flip180 = false, bool flipV = false)
    {
        if (textures.Count == 0) { ImGui.TextDisabled("  (no decodable textures)"); return; }

        const float cell = 150f;
        float avail  = ImGui.GetContentRegionAvail().X;
        int   perRow = Math.Max(1, (int)(avail / (cell + 12f)));
        var   uv0    = flip180 ? new Vector2(1f, 1f) : flipV ? new Vector2(0f, 1f) : Vector2.Zero;
        var   uv1    = flip180 ? new Vector2(0f, 0f) : flipV ? new Vector2(1f, 0f) : Vector2.One;

        for (int i = 0; i < textures.Count; i++)
        {
            var tex     = textures[i];
            float aspect = tex.Height > 0 ? (float)tex.Width / tex.Height : 1f;
            float th     = MathF.Min(cell / MathF.Max(aspect, 0.01f), cell);
            float tw     = th * aspect;

            ImGui.BeginGroup();
            ImGui.Image((nint)tex.GlId, new Vector2(tw, th), uv0, uv1);
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                _uiZoomTex = tex;
                _uiZoomFlip180 = flip180;
                _uiZoomFlipV = flipV;
            }
            ImGui.TextDisabled($"{tex.Width}x{tex.Height}");
            ImGui.EndGroup();

            if ((i + 1) % perRow != 0 && i != textures.Count - 1) ImGui.SameLine();
        }
    }

    private void DrawUiZoomOverlay()
    {
        if (_uiZoomTex is null) return;
        var tex = _uiZoomTex;

        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.Pos);
        ImGui.SetNextWindowSize(vp.Size);
        ImGui.SetNextWindowBgAlpha(0.92f);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings
                  | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoNav;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.Begin("##uiZoomOverlay", flags);

        ImGui.Text($"{tex.Width}x{tex.Height}  —  double-click, click the background, or press Esc to close");

        var avail  = ImGui.GetContentRegionAvail();
        float aspect = tex.Height > 0 ? (float)tex.Width / tex.Height : 1f;
        float dh = avail.Y;
        float dw = dh * aspect;
        if (dw > avail.X) { dw = avail.X; dh = dw / aspect; }

        var cursor = ImGui.GetCursorPos();
        ImGui.SetCursorPos(new Vector2(cursor.X + MathF.Max(0f, (avail.X - dw) * 0.5f), cursor.Y));
        var uv0 = _uiZoomFlip180 ? new Vector2(1f, 1f) : _uiZoomFlipV ? new Vector2(0f, 1f) : Vector2.Zero;
        var uv1 = _uiZoomFlip180 ? new Vector2(0f, 0f) : _uiZoomFlipV ? new Vector2(1f, 0f) : Vector2.One;
        ImGui.Image((nint)tex.GlId, new Vector2(dw, dh), uv0, uv1);
        bool clickedImage = ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);

        bool clickedBackground = ImGui.IsWindowHovered() && !ImGui.IsItemHovered() &&
                                  ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        if (clickedImage || clickedBackground || ImGui.IsKeyPressed(ImGuiKey.Escape))
            _uiZoomTex = null;

        ImGui.End();
        ImGui.PopStyleVar();
    }
}
