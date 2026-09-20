using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using PS2AnySound = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnySound;
using PS2AnyAnimation = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using ITwinObject = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private uint?  _globalDataSelectedObjectId;
    private string _globalDataSearchFilter = "";

    private PreviewFbo? _gdFbo;
    private List<(GpuMesh Mesh, Material Mat, Matrix4x4 Local)> _gdSkelParts = new();
    private PS2AnyOGI?      _gdPreviewOgi;
    private TwinAnimation?  _gdAnimData;
    private int   _gdAnimTotalFrames;
    private float _gdAnimFps = 30f;
    private float _gdAnimFrame;
    private bool  _gdAnimPlaying;
    private bool  _gdAnimLoop = true;
    private Matrix4x4[] _gdBoneMatrices = RenderPipeline.IdentityBoneMatrices;
    private float _gdYaw = 30f, _gdPitch = 15f, _gdDist = 5f;
    private Vector3 _gdCenter = Vector3.Zero;
    private string _gdPreviewLabel = "";

    private void DrawGlobalDataWindow()
    {
        if (!_showGlobalData) return;

        if (_gdAnimData is not null)
        {
            if (_gdAnimPlaying)
            {
                var dt = ImGui.GetIO().DeltaTime;
                _gdAnimFrame += dt * _gdAnimFps;
                if (_gdAnimFrame > _gdAnimTotalFrames)
                {
                    if (_gdAnimLoop) _gdAnimFrame %= MathF.Max(1f, _gdAnimTotalFrames);
                    else { _gdAnimFrame = _gdAnimTotalFrames; _gdAnimPlaying = false; }
                }
            }
            if (_gdPreviewOgi is not null)
                _gdBoneMatrices = MeshDecoder.SampleAnimationPose(_gdPreviewOgi, _gdAnimData, _gdAnimFrame);
        }

        ImGui.SetNextWindowSize(new Vector2(1040f, 560f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Default.rm2 Data##globaldata", ref _showGlobalData))
        {
            ImGui.End();
            return;
        }

        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var globalRm2 = chunkSource?.GlobalRm2;
        if (globalRm2 is null)
        {
            ImGui.TextDisabled("No level is loaded yet (Startup\\Default.rm2 loads alongside any level).");
            ImGui.End();
            return;
        }

        var objSec = globalRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
        if (objSec is null)
        {
            ImGui.TextDisabled("Startup\\Default.rm2 has no Objects section (unexpected).");
            ImGui.End();
            return;
        }

        ImGui.TextDisabled("Shared objects/behaviours/sounds every level can reference — same data,\n" +
                            "no placed position (see the tooltip on the top-bar button).");
        ImGui.Separator();

        float previewW = 340f;

        ImGui.BeginChild("##globaldataleft", new Vector2(220f, -1f), ImGuiChildFlags.Border);
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##globaldatasearch", "Search...", ref _globalDataSearchFilter, 128);
        ImGui.Separator();
        for (int i = 0; i < objSec.GetItemsAmount(); i++)
        {
            if (objSec.GetItem(i) is not PS2AnyObject obj) continue;
            string label = string.IsNullOrEmpty(obj.Name) ? $"0x{obj.GetID():X4}" : obj.Name;
            if (!string.IsNullOrEmpty(_globalDataSearchFilter) &&
                label.IndexOf(_globalDataSearchFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            bool selected = _globalDataSelectedObjectId == obj.GetID();
            if (ImGui.Selectable($"0x{obj.GetID():X4}  {label}##globalobj{obj.GetID()}", selected))
                _globalDataSelectedObjectId = obj.GetID();
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##globaldatamiddle", new Vector2(-previewW - 8f, -1f), ImGuiChildFlags.Border);
        var selectedObj = _globalDataSelectedObjectId.HasValue
            ? objSec.GetItem<PS2AnyObject>(_globalDataSelectedObjectId.Value)
            : null;
        if (selectedObj is null)
        {
            ImGui.TextDisabled("Pick an object on the left.");
        }
        else
        {
            DrawGlobalObjectDetails(selectedObj);
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##globaldatapreview", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        DrawGlobalDataPreviewPanel();
        ImGui.EndChild();

        ImGui.End();
    }

    private void DrawGlobalObjectDetails(PS2AnyObject obj)
    {
        ImGui.TextColored(new Vector4(1f, 1f, 0.4f, 1f), string.IsNullOrEmpty(obj.Name) ? $"0x{obj.GetID():X4}" : obj.Name);
        ImGui.TextDisabled($"ID: 0x{obj.GetID():X4}  ({obj.GetID()})   Type: {obj.Type}");

        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var globalRm2 = chunkSource?.GlobalRm2;
        if (globalRm2 is null) return;

        if (obj.RefSounds.Count > 0 || obj.SoundSlots.Any(s => s != 0xFFFF))
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Sounds##globalsoundshdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var sndSec = globalRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                    ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_SOUND_EFFECTS_SECTION);
                var soundIds = obj.RefSounds.Concat(obj.SoundSlots).Where(sid => sid != 0xFFFF).Distinct().ToList();
                if (soundIds.Count == 0)
                    ImGui.TextDisabled("Referenced slot(s) are all empty (0xFFFF) — nothing to show.");
                foreach (var sid in soundIds)
                {
                    var snd = sndSec?.GetItem<PS2AnySound>(sid);
                    ImGui.PushID((int)sid);
                    if (snd is null)
                    {
                        ImGui.TextDisabled($"Sound id 0x{sid:X} — not found in the global sound section.");
                    }
                    else
                    {
                        double durationSeconds = snd.Sound.Length / 16.0 * 28.0 / snd.GetFreq();
                        ImGui.Text($"Sound 0x{sid:X} (ID {sid}) — {snd.GetFreq()}Hz, {FormatMinSec(durationSeconds)}");
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Play##gsndplay")) PlaySoundEffect(snd);
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Replace...##gsndreplace"))
                        { ReplaceSoundEffectAudio(snd, sid, GlobalRm2Path); chunkSource!.GlobalRm2Dirty = true; }
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Reset Original##gsndreset"))
                        { ResetSoundEffectToOriginal(snd, sid, GlobalRm2Path); chunkSource!.GlobalRm2Dirty = true; }

                        float gain = GetPersistedSoundEffectGain(snd, sid, GlobalRm2Path);
                        ImGui.SetNextItemWidth(150f);
                        if (ImGui.SliderFloat("Volume (gain)##gsndgain", ref gain, 0.1f, 3f, "%.2fx"))
                        { ApplySoundEffectGain(snd, sid, gain, GlobalRm2Path); chunkSource!.GlobalRm2Dirty = true; }
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("Digital gain applied to this sound's own waveform before re-encoding — " +
                                         "always computed from the ORIGINAL audio, never compounds. Save Chunk to persist.\n" +
                                         "⚠ This is a GLOBAL sound — every level that references this object shares it.");
                    }
                    ImGui.PopID();
                }
                if (_musicLastError is not null)
                    ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), $"Error: {_musicLastError}");
            }
        }

        BaseTwinSection? globalBehSecForSlots = null;
        if (obj.RefBehaviours.Count > 0)
        {
            globalBehSecForSlots = globalRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            if (globalBehSecForSlots is not null)
            {
                ImGui.Separator();
                if (ImGui.CollapsingHeader($"Behaviours ({obj.RefBehaviours.Count})##globalobjbeh"))
                    DrawBehavioursSectionBody(obj, globalBehSecForSlots, editingGlobal: true, chunkSource);
            }
        }

        if (obj.BehaviourSlots.Any(s => s != 0xFFFF) && globalBehSecForSlots is not null)
        {
            var slotNames = obj.Type == ITwinObject.ObjectType.Character
                ? CharacterGameObjectScriptOrder : GameObjectScriptOrder;
            ImGui.Separator();
            if (ImGui.CollapsingHeader($"Behaviour Slots (by event)##globalobjslots"))
            {
                ImGui.BeginChild("##globalobjslotslist", new Vector2(-1f, 200f), ImGuiChildFlags.Border);
                for (int i = 0; i < obj.BehaviourSlots.Count; i++)
                {
                    ushort bid = obj.BehaviourSlots[i];
                    if (bid == 0xFFFF) continue;
                    string evName = i < slotNames.Length ? slotNames[i] : $"Slot{i}";
                    var bItem = globalBehSecForSlots.GetItem<BaseTwinItem>(bid);
                    string bLabel = bItem is PS2BehaviourGraph bg && !string.IsNullOrEmpty(bg.Name)
                        ? bg.Name : (bItem is TwinBehaviourStarter ? "(starter, no name)" : "(missing)");
                    string bType = bItem is TwinBehaviourStarter ? "Starter" : bItem is PS2BehaviourGraph ? "Graph" : "?";
                    bool clicked = ImGui.Selectable($"{evName,-28} 0x{bid:X4}  [{bType,-7}]  {bLabel}##gobjslot{i}");
                    if (clicked && bItem is PS2BehaviourGraph clickedGraph)
                    {
                        _inspectedBehaviourId = bid;
                        _inspectedBehaviourGraph = clickedGraph;
                        _inspectedBehaviourManual = true;
                        _inspectedBehaviourText = GraphToEditableText(clickedGraph);
                        _inspectedBehaviourOriginalText = GetPristineBehaviourGraphText(true, bid) ?? _inspectedBehaviourText;
                        _inspectedBehaviourApplyMsg = "";
                    }
                }
                ImGui.EndChild();
                ImGui.TextDisabled("Click a row to open it in the Behaviours editor above.");
            }
        }

        if (obj.RefAnimations.Count > 0)
        {
            var globalAnimSec = globalRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_ANIMATIONS_SECTION);
            if (globalAnimSec is not null)
            {
                ImGui.Separator();
                if (ImGui.CollapsingHeader($"Animations ({obj.RefAnimations.Count})##globalobjanim"))
                {
                    foreach (var animId in obj.RefAnimations)
                    {
                        ImGui.PushID((int)animId + 0x10000);
                        var anim = globalAnimSec.GetItem<PS2AnyAnimation>(animId);
                        if (anim is null)
                        {
                            ImGui.TextDisabled($"Animation 0x{animId:X4} ({animId}) — not found.");
                        }
                        else
                        {
                            float fps = anim.DefaultFPS > 0 ? anim.DefaultFPS : 30f;
                            double durationSeconds = anim.TotalFrames / fps;
                            ImGui.Text($"Animation 0x{animId:X4} ({animId}) — {anim.TotalFrames} frames @ " +
                                        $"{anim.DefaultFPS}fps ({FormatMinSec(durationSeconds)})" +
                                        (anim.HasFacialAnimationData ? "  [+facial]" : ""));
                            ImGui.SameLine();
                            if (!anim.HasAnimationData)
                            {
                                ImGui.TextDisabled("(no skeletal data)");
                            }
                            else if (ImGui.SmallButton("Preview##ganimprev"))
                            {
                                SelectGlobalAnimationPreview(obj, chunkSource, anim, animId);
                            }
                        }
                        ImGui.PopID();
                    }
                }
            }
        }

        if (obj.RefOGIs.Count > 0)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader($"OGI ({obj.RefOGIs.Count})##globalobjogi"))
            {
                if (chunkSource?.MeshTables is null)
                {
                    ImGui.TextDisabled("No mesh tables loaded yet.");
                }
                else
                {
                    foreach (var ogiId in obj.RefOGIs)
                    {
                        ImGui.PushID((int)ogiId);
                        var meshParts = MeshDecoder.GetObjectMeshPartsByOgi(chunkSource.MeshTables, ogiId);
                        if (meshParts is null || meshParts.Count == 0)
                        {
                            ImGui.TextDisabled($"OGI 0x{ogiId:X4} — no renderable parts (script-only / manager OGI).");
                        }
                        else
                        {
                            ImGui.Text($"OGI 0x{ogiId:X4} — {meshParts.Count} part(s).");
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Preview##gogiprev"))
                            {
                                var directParts = chunkSource.GlobalRm2 is not null
                                    ? MeshDecoder.GetObjectMeshPartsByOgiDirect(Engine.Instance.GL, chunkSource.GlobalRm2, chunkSource.GlobalTexCache, ogiId)
                                    : null;
                                SelectGlobalOgiPreview(chunkSource, ogiId, directParts ?? meshParts);
                            }
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Edit UV...##gogiuv"))
                                OpenUvEditor(chunkSource, ogiId, $"{obj.Name} — OGI 0x{ogiId:X4}");

                            int partIdx = 0;
                            foreach (var part in meshParts)
                            {
                                if (part.Mat.Albedo is null) { partIdx++; continue; }
                                ImGui.PushID(partIdx);
                                ImGui.Image((nint)part.Mat.Albedo.GlId, new Vector2(32f, 32f), new Vector2(0f, 1f), new Vector2(1f, 0f));
                                ImGui.SameLine();
                                if (ImGui.SmallButton("Import Texture...##gogitex"))
                                    ImportTextureOverMaterial(part.Mat);
                                ImGui.PopID();
                                partIdx++;
                            }
                        }
                        ImGui.PopID();
                    }
                }
            }
        }
    }

    private void SelectGlobalOgiPreview(ChunkSource? chunkSource, uint ogiId,
        List<(GpuMesh Mesh, Material Mat, Matrix4x4 JointLocal, bool IsSkinned)> meshParts)
    {
        _gdSkelParts = meshParts.Select(p => (p.Mesh, p.Mat, p.JointLocal)).ToList();
        _gdPreviewOgi = chunkSource?.MeshTables is not null ? MeshDecoder.GetRawOgi(chunkSource.MeshTables, ogiId) : null;
        _gdAnimData = null;
        _gdAnimPlaying = false;
        _gdAnimFrame = 0f;
        _gdBoneMatrices = RenderPipeline.IdentityBoneMatrices;
        _gdPreviewLabel = $"OGI 0x{ogiId:X4}";
        FrameGlobalDataPreview();
    }

    private void SelectGlobalAnimationPreview(PS2AnyObject obj, ChunkSource? chunkSource, PS2AnyAnimation anim, uint animId)
    {
        uint? skeletonOgiId = null;
        List<(GpuMesh Mesh, Material Mat, Matrix4x4 JointLocal, bool IsSkinned)>? meshParts = null;
        if (chunkSource?.MeshTables is not null)
        {
            foreach (var candidateOgiId in obj.RefOGIs)
            {
                var parts = MeshDecoder.GetObjectMeshPartsByOgi(chunkSource.MeshTables, candidateOgiId);
                if (parts is not null && parts.Count > 0) { skeletonOgiId = candidateOgiId; meshParts = parts; break; }
            }
        }

        _gdPreviewOgi = skeletonOgiId.HasValue ? MeshDecoder.GetRawOgi(chunkSource!.MeshTables!, skeletonOgiId.Value) : null;
        _gdSkelParts = meshParts?.Select(p => (p.Mesh, p.Mat, p.JointLocal)).ToList() ?? new();
        _gdAnimData = anim.MainAnimation;
        _gdAnimTotalFrames = anim.TotalFrames;
        _gdAnimFps = anim.DefaultFPS > 0 ? anim.DefaultFPS : 30f;
        _gdAnimFrame = 0f;
        _gdAnimPlaying = true;
        _gdPreviewLabel = $"Animation 0x{animId:X4}" +
            (skeletonOgiId.HasValue ? $"  (skeleton: OGI 0x{skeletonOgiId.Value:X4})" : "  (no OGI to preview against)");
        FrameGlobalDataPreview();
    }

    private void FrameGlobalDataPreview()
    {
        var centers = _gdSkelParts.Where(p => p.Mat.LocalCenter.HasValue).Select(p => p.Mat.LocalCenter!.Value).ToList();
        if (centers.Count > 0)
        {
            _gdCenter = centers.Aggregate(Vector3.Zero, (a, b) => a + b) / centers.Count;
            _gdDist = MathF.Max(2f, _gdCenter.Length() * 2f + 3f);
        }
        else
        {
            _gdCenter = Vector3.Zero;
            _gdDist = 5f;
        }
        _gdYaw = 30f; _gdPitch = 15f;
    }

    private void DrawGlobalDataPreviewPanel()
    {
        if (_gdSkelParts.Count == 0)
        {
            ImGui.TextDisabled("Select an OGI or Animation on the left\nand press its \"Preview\" button.");
            return;
        }

        ImGui.TextDisabled(_gdPreviewLabel);
        float timelineH = _gdAnimData is not null ? 58f : 0f;
        var avail = ImGui.GetContentRegionAvail();
        int pw = Math.Max(64, (int)avail.X), ph = Math.Max(64, (int)(avail.Y - timelineH));
        RenderGlobalDataPreview(pw, ph);

        ImGui.Image((nint)_gdFbo!.ColorTexture, new Vector2(pw, ph), new Vector2(1, 1), new Vector2(0, 0));
        if (ImGui.IsItemHovered())
        {
            var io = ImGui.GetIO();
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _gdYaw   += io.MouseDelta.X * 0.4f;
                _gdPitch  = Math.Clamp(_gdPitch + io.MouseDelta.Y * 0.4f, -89f, 89f);
            }
            if (io.MouseWheel != 0)
                _gdDist = Math.Clamp(_gdDist * (1f - io.MouseWheel * 0.12f), 0.2f, 500f);
        }

        if (_gdAnimData is not null)
        {
            ImGui.Spacing();
            if (ImGui.Button(_gdAnimPlaying ? "Pause##gdanim" : "Play##gdanim")) _gdAnimPlaying = !_gdAnimPlaying;
            ImGui.SameLine();
            ImGui.Checkbox("Loop##gdanim", ref _gdAnimLoop);
            ImGui.SameLine();
            ImGui.Text($"Frame {_gdAnimFrame:F1} / {_gdAnimTotalFrames}  ({_gdAnimFps:F0} fps)");

            float f = _gdAnimFrame;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.SliderFloat("##gdtimeline", ref f, 0f, _gdAnimTotalFrames))
            {
                _gdAnimFrame = f;
                _gdAnimPlaying = false;
            }
        }
    }

    private void RenderGlobalDataPreview(int w, int h)
    {
        var gl = Engine.Instance.GL;
        _gdFbo ??= new PreviewFbo(gl, w, h);
        _gdFbo.Resize(w, h);

        var clearColor = new Vector3(0.13f, 0.13f, 0.16f);
        _gdFbo.Begin(clearColor);

        var pipeline = RenderPipeline.Instance;
        if (pipeline is not null && _gdSkelParts.Count > 0)
        {
            var sh = pipeline.Shader;
            sh.Use();

            float yr = _gdYaw * MathF.PI / 180f, pr = _gdPitch * MathF.PI / 180f;
            var eye = _gdCenter + new Vector3(
                MathF.Cos(pr) * MathF.Sin(yr),
                MathF.Sin(pr),
                MathF.Cos(pr) * MathF.Cos(yr)) * _gdDist;

            var view = CameraComponent.CreateLookAtRH(eye, _gdCenter, Vector3.UnitY);
            var proj = CameraComponent.CreatePerspectiveRH(50f * MathF.PI / 180f, (float)w / h, 0.05f, 20000f);

            sh.Set("StartView", view);
            sh.Set("StartProjection", proj);
            sh.Set("StartModel", Matrix4x4.Identity);
            sh.Set("EyePosition", eye);
            sh.Set("EyeDirection", Vector3.Normalize(_gdCenter - eye));
            sh.Set("Resolution", new Vector2(w, h));
            sh.Set("Time", 0f);
            sh.Set("FogColor", clearColor);
            sh.Set("Diffuse", Vector4.One);
            sh.Set("Opacity", 1f);
            sh.Set("FlipY", 0f);
            sh.Set("DiffuseOnly", 0f);
            sh.SetMatrixArray("BoneMatrices", _gdBoneMatrices);

            gl.Disable(Silk.NET.OpenGL.EnableCap.Blend);
            foreach (var (mesh, mat, local) in _gdSkelParts)
            {
                sh.Set("StartModel", local);
                mat.FogEnabled = false;
                mat.Apply(gl, sh);
                mesh.Draw(gl);
                mat.Restore(gl, sh);
            }
        }

        _gdFbo.End(Engine.Instance.Width, Engine.Instance.Height);
    }

    private const string GlobalRm2Path = @"Startup\Default.rm2";

    private static readonly string[] GameObjectScriptOrder =
    {
        "OnSpawn", "OnTrigger", "OnDamage", "OnTouch", "OnHeadbutt", "OnLand",
        "OnGettingSpinAttacked", "OnGettingBodyslamAttacked", "OnGettingSlideAttacked",
        "OnPhysicsCollision", "Unk10",
    };

    private static readonly string[] CharacterGameObjectScriptOrder =
    {
        "OnSpawn", "OnTrigger", "OnDamage", "OnTouch", "OnHeadbutt", "OnLand",
        "OnGettingSpinAttacked", "OnGettingBodyslamAttacked", "OnGettingSlideAttacked",
        "OnPhysicsCollision", "Unk10",
        "OnFallingDeath", "OnIdle", "OnShuffleFeet", "OnWalk", "OnRun", "OnStrafeLeft",
        "OnStrafeRight", "OnSpin", "OnSpinEnd", "OnSpinPunch", "OnSpinPunchEnd", "OnSlideJump",
        "OnStandingJump", "OnRunningJump", "OnDoubleJump", "OnMaxedDoubleJump",
        "OnSuperKneeDropHang", "OnSuperKneeDrop", "OnFlyingKick", "OnStompKick",
        "OnRadialBlastHang", "OnRadialBlast", "OnShortFall", "OnLongFall", "Unk35", "Unk36",
        "OnFlyingKickFall", "Unk38", "OnSoftLand", "OnLandWhileMoving", "OnHardLand",
        "OnSuperKneeDropLand", "Unk43", "OnFlyingKickLand", "OnStompKickLand", "OnStandToCrouch",
        "OnCrouchToCrawl", "OnCrawlToCrouch", "OnCrouchToStand", "OnCrawlToStand", "OnRunToSlide",
        "Unk52", "OnSlideToCrouch", "OnSlideToStand", "OnThrowHandExtend", "OnAbortThrow",
        "OnWallJumpReel", "OnCeilingReel", "OnAbortReel", "OnThrowPunch", "OnCeilingPropel",
        "OnWallJumpHold", "OnWallJumpRelease", "OnWallJumpPropel", "Unk65", "OnLeaveCoOp",
        "OnDefaultDeath", "OnBodyslam", "OnSpinThrow", "OnJumpThrow", "OnDrawMultitool",
        "OnSheathMultitool", "OnFireMultitool", "OnEnterVehicleMode", "OnExitVehicleMode",
        "OnEnterVehicleModeIdle", "OnExitVehicleModeIdle", "Unk78", "OnDefaultDeath2",
        "OnRecoil1", "OnRecoil2", "OnRecoil3", "OnRecoil4", "OnVehicleModifierHold",
        "OnVehicleModifierRelease", "OnSkateForwardsStraight", "OnSkateForwardsLeft",
        "OnSkateForwardsRight", "OnSkateBackwardsStraight", "OnSkateBackwardsLeft",
        "OnSkateBackwardsRight", "OnSkateCrouchStraight", "OnSkateCrouchRight",
        "OnSkateCrouchLeft", "OnSkateLand", "OnSkateFlatSpin", "OnSkateSpinOver",
        "OnVehicleGroundTrickForward", "OnVehicleGroundTrickBackward", "OnStandingJump2",
        "OnRunningJump2", "OnAboutToWinBrawl", "OnWinBrawl", "OnAboutToLoseBrawl", "OnLoseBrawl",
        "OnChargeMultitool", "OnFailToFireMultitool", "OnFailToRadialBlast", "OnSkateJump",
        "OnSkateImpact",
    };
}
