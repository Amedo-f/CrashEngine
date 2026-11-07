using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

// Amedo
public sealed partial class GameScene
{
    // Amedo
    private static T? FindIn<T>(PS2AnyTwinsanityRM2?[] order, int sectionId, uint id, bool code) where T : BaseTwinItem
    {
        foreach (var rm2 in order)
            if ((code ? MeshDecoder.CodeSection(rm2, sectionId) : MeshDecoder.GraphicsSection(rm2, sectionId)) is { } sec && sec.ContainsItem(id))
                return sec.GetItem<T>(id);
        return null;
    }

    // Amedo
    private void DrawCratesReceiveShadows(ChunkSource src)
    {
        if (GetGameProject() is not { } proj) return;
        bool on = proj.CratesReceiveShadows;
        if (ImGui.Checkbox("Crates Receive Shadows##cratesreceive", ref on))
        {
            proj.CratesReceiveShadows = on;
            proj.Save();
            GameShadowDefs.CratesReceiveShadows = on;
            int changed = 0;
            foreach (var cs in Roots.Select(r => r.Get<ChunkSource>()).OfType<ChunkSource>().Distinct())
                changed += GameShadowDefs.ApplyCrateReceivers(cs.Rm2, cs.GlobalRm2, on);
            src.GlobalRm2Dirty = true;
            RefreshShadowReceivers();
            _browser.Log($"Crates Receive Shadows: {(on ? "on" : "off")} ({changed} crate material layer(s) changed in the loaded levels). " +
                         "Every level applies it when it loads (editor, Play, PC build); Save Chunk writes it into this level and Default.rm2 for Build ISO.");
        }
    }

    // Amedo
    private static List<TwinShader> EntityShaders(Entity e)
    {
        var shaders = new List<TwinShader>();
        void Walk(Entity x)
        {
            foreach (var c in x.Components)
                if (c is CrashEngine.Importer.MeshRenderer { Material: { } mat })
                    foreach (var layer in new[] { mat }.Concat(mat.Layers ?? []))
                        if (layer.SourceShader is TwinShader sh && !shaders.Contains(sh)) shaders.Add(sh);
            foreach (var child in x.Children) Walk(child);
        }
        Walk(e);
        return shaders;
    }

    // Amedo
    private void DrawReceiveShadows(Entity e)
    {
        var shaders = EntityShaders(e);
        if (shaders.Count == 0) return;
        bool all = shaders.All(s => s.AlphaCorrectionValue);
        if (ImGui.Checkbox("Receive Shadows##receiveshadows", ref all))
        {
            foreach (var sh in shaders) sh.AlphaCorrectionValue = all;
            if (ChunkSourceFor(e) is { } src) src.GlobalRm2Dirty = true;
            RefreshShadowReceivers();
        }
    }

    // Amedo
    private const uint BakeTemplateMaterialId = 0xEE85EFBC;

    // Amedo
    private void MakeEditorContentReceiveShadows(Entity chunk)
    {
        if (chunk.Get<ChunkSource>() is not { } src) return;
        int changed = 0;
        void Receive(PS2AnyMaterial? mat)
        {
            if (mat is null) return;
            foreach (var sh in mat.Shaders)
                if (!sh.AlphaCorrectionValue) { sh.AlphaCorrectionValue = true; changed++; }
        }
        var order = new[] { src.Rm2, src.GlobalRm2 };
        if (MeshDecoder.CodeSection(src.Rm2, TwinConstants.CODE_GAME_OBJECTS_SECTION) is { } objs)
            for (int i = 0; i < objs.GetItemsAmount(); i++)
            {
                if (objs.GetItem(i) is not PS2AnyObject o || !o.Name.StartsWith("CE_", StringComparison.Ordinal)) continue;
                foreach (var ogiId in o.OGISlots.Where(x => x != 0xFFFF).Distinct())
                    if (FindIn<PS2AnyOGI>(order, TwinConstants.CODE_OGIS_SECTION, ogiId, true) is { } ogi)
                        foreach (var rmId in ogi.RigidModelIds)
                            if (FindIn<PS2AnyRigidModel>([src.Rm2], TwinConstants.GRAPHICS_RIGID_MODELS_SECTION, rmId, false) is { } rm)
                                foreach (var matId in rm.Materials)
                                    Receive(FindIn<PS2AnyMaterial>([src.Rm2], TwinConstants.GRAPHICS_MATERIALS_SECTION, matId, false));
            }
        var template = FindIn<PS2AnyMaterial>([src.GlobalRm2], TwinConstants.GRAPHICS_MATERIALS_SECTION, BakeTemplateMaterialId, false);
        if (template is not null &&
            src.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION)?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MATERIALS_SECTION) is { } sceneryMats)
            for (int i = 0; i < sceneryMats.GetItemsAmount(); i++)
                if (sceneryMats.GetItem(i) is PS2AnyMaterial m && m.Name == template.Name) Receive(m);
        if (changed == 0) return;
        foreach (var x in AllEntities(chunk))
            foreach (var c in x.Components)
                if (c is CrashEngine.Importer.MeshRenderer { Material: { } mat }) ApplyReceiverFlag(mat);
        _browser.Log($"Shadows: {changed} material layer(s) of objects/scenery made in the editor now receive shadows. Save Chunk to keep it.");
    }

    // Amedo
    private void RefreshShadowReceivers()
    {
        foreach (var root in Roots)
            foreach (var x in AllEntities(root))
                foreach (var c in x.Components)
                    if (c is CrashEngine.Importer.MeshRenderer { Material: { } m }) ApplyReceiverFlag(m);
    }

    // Amedo
    private static void ApplyReceiverFlag(CrashEngine.Renderer.Material m)
    {
        if (m.SourceShader is TwinShader sh) m.ShadowReceiver = sh.AlphaCorrectionValue;
        if (m.Layers is { } layers)
            foreach (var layer in layers)
                if (layer.SourceShader is TwinShader lsh) layer.ShadowReceiver = lsh.AlphaCorrectionValue;
    }
}
