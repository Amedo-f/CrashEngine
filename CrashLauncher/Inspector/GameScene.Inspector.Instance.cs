using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private static readonly Dictionary<int, string> ParamList2Labels = new()
    {
        [1]  = "WalkSpeedPercentage",
        [3]  = "BaseGravity",
        [4]  = "AirGravity",
        [5]  = "StrafingSpeed",
        [6]  = "WalkSpeed",
        [7]  = "RunSpeed",
        [9]  = "SpinThrowForwardForce",
        [10] = "SpinLength",
        [11] = "SpinDelay",
        [15] = "JumpAirSpeed",
        [16] = "JumpHeight",
        [17] = "JumpArcUnk18",
        [18] = "JumpArcUnk19",
        [19] = "JumpEdgeSpeed",
        [20] = "DoubleJumpHeight",
        [21] = "DoubleJumpUnk22",
        [22] = "DoubleJumpArcUnk",
        [23] = "SlideJumpForce1",
        [24] = "SlideJumpForce2",
        [25] = "SlideJumpForce3",
        [26] = "SlideJumpArc",
        [32] = "BodyslamUpwardForce",
        [33] = "BodyslamGravityForce",
        [40] = "CrawlSpeed",
        [41] = "CrawlTimeFromStand",
        [42] = "CrawlTimeToStand",
        [43] = "CrawlTimeToRun",
        [44] = "SlideSpeed",
        [45] = "SlideSlowdownTime1",
        [46] = "SlideSlowdownTime2",
        [47] = "SlideSlowdownTime3",
        [48] = "SlideUnk49",
        [49] = "SlideUnk50",
    };

    private void DrawInspectorInstance(Entity e, InstanceData? inst)
    {
        if (inst is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Instance##inst", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled($"Object ID:    0x{inst.ObjectId:X4}");
                ImGui.TextDisabled($"State Flags:  0x{inst.Source.StateFlags:X8}");
                DrawUniqueObject(e, inst); // Amedo

                DrawInspectorInstanceFlags(inst);

                DrawInspectorMovement(e, inst); // Amedo 2026-11-15

                DrawInspectorInstanceRefs(inst);

                DrawInspectorInstanceParams(inst);

                DrawInspectorObjectDefinition(e, inst);


                ImGui.Separator();
                if (ImGui.CollapsingHeader("Effects##fx"))
                    DrawEffectsBrowser();

                if (inst.LinkedPaths.Count > 0)
                    ImGui.TextDisabled($"Paths: {inst.LinkedPaths.Count}  ({string.Join(", ", inst.LinkedPaths.Select(p => $"0x{p:X4}"))})");

                DrawInspectorConditions(e, inst);

                DrawInspectorInstanceMeshes(e);

                DrawSwapObjectAndCharacterSection(e, inst);
            }
        }
    }

    private void DrawInspectorInstanceFlags(InstanceData inst)
    {
        if (ImGui.CollapsingHeader("Instance Flags (this placement only)##instflags"))
        {
            var instFlagsEnum = (Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState)inst.Source.StateFlags;
            foreach (var flag in Enum.GetValues<Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState>())
            {
                if (flag == 0) continue;
                bool on = instFlagsEnum.HasFlag(flag);
                if (ImGui.Checkbox($"{flag}##instflag_{flag}", ref on))
                {
                    instFlagsEnum = on ? instFlagsEnum | flag : instFlagsEnum & ~flag;
                    inst.Source.StateFlags = (uint)instFlagsEnum;
                }
            }
            ImGui.TextDisabled("Save Chunk to persist, then Build ISO + test.");
        }
    }

    private void DrawInspectorInstanceRefs(InstanceData inst)
    {
        if (inst.Source.Instances.Count > 0 || inst.Source.Paths.Count > 0) // Amedo 2026-11-15
        {
            if (ImGui.CollapsingHeader("Referenced Instances/Paths##instrefs", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var chunkRootForRefs = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                Entity? FindInstanceEntity(ushort iid) => chunkRootForRefs is null ? null :   // Amedo 2026-09-23
                    AllEntities(chunkRootForRefs).FirstOrDefault(x =>
                        x.Get<InstanceData>()?.Source.GetID() == iid);

                // Amedo 2026-09-23
                if (inst.Source.Instances.Count > 0)
                {
                    ImGui.TextDisabled($"Instances ({inst.Source.Instances.Count}) — IDs this instance messages:");
                    for (int i = 0; i < inst.Source.Instances.Count; i++)
                    {
                        ushort iid = inst.Source.Instances[i];
                        var itarget = FindInstanceEntity(iid);
                        ImGui.TextUnformatted($"  [{i}] Instance_{iid:X4}" + (itarget is null ? "  (not found in this chunk)" : $"  ({itarget.Name})"));
                        if (itarget is not null)
                        {
                            ImGui.SameLine();
                            if (ImGui.SmallButton($"Select##instref_{i}")) SelectClicked(itarget, false);
                        }
                    }
                }

                if (inst.Source.Paths.Count > 0)
                {
                    ImGui.TextDisabled($"Paths ({inst.Source.Paths.Count}), in order:");
                    for (int i = 0; i < inst.Source.Paths.Count; i++)
                        ImGui.TextUnformatted($"  [{i}] Path_{inst.Source.Paths[i]:X4}  (Path markers not loaded in the scene yet)");
                }
            }
        }
    }

    private void DrawInspectorInstanceParams(InstanceData inst)
    {
        if (inst.Source.ParamList2.Count > 0)
        {
            if (ImGui.CollapsingHeader("Character Settings##paramlist2"))
            {
                if (ImGui.Button("Reset to Original##resetparamlist2"))
                    ResetParamList2ToOriginal(inst);
                for (int p = 0; p < inst.Source.ParamList2.Count; p++)
                {
                    string plabel = ParamList2Labels.TryGetValue(p, out var name) ? $"[{p}] {name}" : $"Param[{p}]";
                    float pval = inst.Source.ParamList2[p];
                    if (ImGui.InputFloat($"{plabel}##paramlist2_{p}", ref pval))
                        inst.Source.ParamList2[p] = pval;
                }
            }
        }

        if (inst.Source.ParamList1.Count > 0)
        {
            if (ImGui.CollapsingHeader("Params (advanced)##paramlist13"))
            {
                ImGui.TextDisabled("Raw, mostly-unidentified per-instance parameters (hex).\n" +
                                    "Meaning varies by object type — edit only if you know\n" +
                                    "what a specific index does for this instance's own type.");
                ImGui.Text("ParamList1");
                for (int p = 0; p < inst.Source.ParamList1.Count; p++)
                {
                    int pval1 = unchecked((int)inst.Source.ParamList1[p]);
                    if (ImGui.InputInt($"[{p}]##paramlist1_{p}", ref pval1, 0, 0, ImGuiInputTextFlags.CharsHexadecimal))
                        inst.Source.ParamList1[p] = unchecked((uint)pval1);
                }
            }
        }

        // Amedo 2026-09-23
        if (inst.Source.ParamList3.Count > 0)
        {
            if (ImGui.CollapsingHeader("IVars (raw)##paramlist3ivars"))
            {
                for (int p = 0; p < inst.Source.ParamList3.Count; p++)
                {
                    int pval3 = unchecked((int)inst.Source.ParamList3[p]);
                    if (ImGui.InputInt($"[{p}]##ivars_{p}", ref pval3))
                        inst.Source.ParamList3[p] = unchecked((uint)pval3);
                }
            }
        }
    }

    private void DrawInspectorInstanceMeshes(Entity e)
    {
        int totalV = 0, totalF = 0, subCount = 0;
        var childRenderers = new List<CrashEngine.Importer.MeshRenderer>();
        foreach (var child in AllEntities(e).Skip(1))
        {
            var cmr = child.Get<CrashEngine.Importer.MeshRenderer>();
            if (cmr?.Mesh is null) continue;
            totalV += cmr.Mesh.VertexCount;
            totalF += cmr.Mesh.IndexCount > 0
                      ? cmr.Mesh.IndexCount / 3
                      : cmr.Mesh.VertexCount / 3;
            childRenderers.Add(cmr);
            subCount++;
        }

        var firstMat = childRenderers.FirstOrDefault()?.Material;

        ImGui.Separator();
        ImGui.Text("Meshes");
        if (subCount > 0)
        {
            ImGui.TextDisabled($"Sub-meshes:  {subCount}");
            ImGui.TextDisabled($"Vertices:    {totalV:N0}");
            ImGui.TextDisabled($"Triangles:   {totalF:N0}");

            var firstTex    = firstMat?.Albedo;
            var firstCenter = firstMat?.LocalCenter;

            if (firstTex is not null)
            {
                float avail  = ImGui.GetContentRegionAvail().X;
                float aspect = firstTex.Height > 0 ? (float)firstTex.Width / firstTex.Height : 1f;
                float th     = MathF.Min(avail / aspect, 128f);
                float tw     = th * aspect;
                ImGui.TextDisabled($"Texture: {firstTex.Width}x{firstTex.Height}{firstTex.HdLabel}"); // Amedo 2026-10-02
                ImGui.Image((nint)firstTex.GlId, new Vector2(tw, th), new Vector2(0f, 1f), new Vector2(1f, 0f));
            }
            else
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Texture: none");
            }

            if (firstCenter.HasValue)
            {
                var lc = firstCenter.Value;
                ImGui.TextDisabled($"Local center: ({lc.X:F2}, {lc.Y:F2}, {lc.Z:F2})");
            }
        }
        else
        {
            ImGui.TextDisabled("  (no mesh decoded)");
        }

        if (firstMat is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Material##inst", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (!string.IsNullOrEmpty(firstMat.ShaderType))
                    ImGui.TextDisabled($"PS2 Shader: {firstMat.ShaderType}");

                string[] cullLabels = { "Front", "Back", "Both" };
                int cullIdx = (int)firstMat.Culling;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.Combo("Cull Mode##inst", ref cullIdx, cullLabels, cullLabels.Length))
                    foreach (var r in childRenderers)
                        if (r.Material is not null)
                            r.Material.Culling = (Material.CullMode)cullIdx;

                var bc = firstMat.BaseColor;
                if (ImGui.ColorEdit4("Color##inst", ref bc))
                    foreach (var r in childRenderers)
                        if (r.Material is not null) r.Material.BaseColor = bc;

                bool unlit = firstMat.Unlit;
                if (ImGui.Checkbox("Unlit##inst", ref unlit))
                    foreach (var r in childRenderers)
                        if (r.Material is not null) { r.Material.Unlit = unlit; r.Material.UnlitToggledByUser = true; }

                bool ab = firstMat.AlphaBlend;
                if (ImGui.Checkbox("Alpha Blend##inst", ref ab))
                    foreach (var r in childRenderers)
                        if (r.Material is not null) r.Material.AlphaBlend = ab;

                if (firstMat.AlphaBlend)
                {
                    string[] blendLabels = { "Standard", "Additive", "Subtractive" };
                    int blendIdx = (int)firstMat.Blend;
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.Combo("Blend##inst", ref blendIdx, blendLabels, blendLabels.Length))
                        foreach (var r in childRenderers)
                            if (r.Material is not null)
                                r.Material.Blend = (Material.BlendMode)blendIdx;
                }

                bool fog = firstMat.FogEnabled;
                if (ImGui.Checkbox("Fog##inst", ref fog))
                    foreach (var r in childRenderers)
                        if (r.Material is not null) r.Material.FogEnabled = fog;

                var spd = firstMat.UvScrollSpeed;
                if (spd.X != 0f || spd.Y != 0f)
                    ImGui.TextDisabled($"UV Scroll: {spd.X:F4} , {spd.Y:F4} /s");
            }
        }
    }

    private void ResetParamList2ToOriginal(InstanceData data)
    {
        try
        {
            var freshRm2 = ReadPristineRm2(_rm2);

            var original = FindLayoutInstance(freshRm2, c => c.GetID() == data.Source.GetID() && c.ObjectId == data.ObjectId);
            if (original is null)
            {
                _browser.Log("Reset Character Settings: couldn't find this instance's original data on disc.");
                return;
            }

            data.Source.ParamList2.Clear();
            data.Source.ParamList2.AddRange(original.ParamList2);
            _browser.Log("Reset Character Settings to their original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Character Settings failed: {ex.Message}"); }
    }
}
