using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawInspectorObjectGameSettings(Entity e, InstanceData inst, PS2AnyObject? thisObj, ChunkSource? scriptChunkSrc, BaseTwinSection? behSecForInsp)
    {
        // Amedo
        if (thisObj is not null && ChunkSourceFor(e) is { } tntChunkSrc && ObjectRunsBehaviour(tntChunkSrc, thisObj, TntCountdownBehaviourId))
        {
            if (ImGui.CollapsingHeader("TNT Countdown##tntcountdown", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawTntCountdownSpeed(e, tntChunkSrc, thisObj);
            }
        }

        // Amedo
        var objChunkSrc = ChunkSourceFor(e);
        if (thisObj is not null && objChunkSrc is not null && ObjectRunsBehaviour(objChunkSrc, thisObj, NitroCrateBehaviourId))
        {
            if (ImGui.CollapsingHeader("Nitro Hop Noise Radius##nitroblastradius"))
            {
                DrawNitroNoiseRadius(e, objChunkSrc, thisObj);
            }
        }

        // Amedo
        if (thisObj is not null && objChunkSrc is not null &&
            (ObjectRunsBehaviour(objChunkSrc, thisObj, NitroCrateBehaviourId) || ObjectRunsBehaviour(objChunkSrc, thisObj, TntCountdownBehaviourId)))
        {
            if (ImGui.CollapsingHeader("Crate Explosion Radius##crateexplode", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawCrateExplosionRadius(e, objChunkSrc, thisObj);
            }
        }

        if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)IronSpringCrateLandedOnId) &&
            scriptChunkSrc is not null)
        {
            var globalBehSecForSpring = MeshDecoder.CodeSection(scriptChunkSrc.GlobalRm2, TwinConstants.CODE_BEHAVIOURS_SECTION);
            if (ImGui.CollapsingHeader("Spring Crate Bounce##springbouncehdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawMultiplierSetting(_springCrateBounce, globalBehSecForSpring, scriptChunkSrc);
            }
        }

        if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)EarthWormSquashLaunchId) &&
            behSecForInsp is not null)
        {
            if (ImGui.CollapsingHeader("Earth Worm Squash Launch##wormlaunchhdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawMultiplierSetting(_wormLaunchPower, behSecForInsp, null);
            }
        }

        if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)RigidCannonActivatedId) &&
            behSecForInsp is not null)
        {
            if (ImGui.CollapsingHeader("Cannon Shot Power##cannonpowerhdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawMultiplierSetting(_cannonShotPower, behSecForInsp, null);
            }
        }

        if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)GenericCreatureDamagedJumpedOnId) &&
            behSecForInsp is not null)
        {
            if (ImGui.CollapsingHeader("Enemy Stomp##enemystomphdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawEnemyStomp(behSecForInsp);
            }
        }

        if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)DjDefaultBehaviourId))
        {
            if (ImGui.CollapsingHeader("DJ Music##djmusichdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (inst.Source.ParamList3.Count == 0)
                {
                    ImGui.TextDisabled("This DJ instance has no ParamList3 (IVars) data at all — nothing to show.");
                }
                else
                {
                    uint djCurrentId = inst.Source.ParamList3[0];
                    DrawLevelMusic(djCurrentId, "DJ instance's own IVars[0] (ParamList3)",
                        newId =>
                        {
                            inst.Source.ParamList3[0] = newId;
                            _musicLabel = MusicNames.TryGetValue(newId, out var name)
                                ? $"{name} (id {newId})" : $"id {newId} (undocumented — not in the reference tool's own name table)";
                            _browser.Log($"DJ Music: set this DJ instance's IVars[0] to {_musicLabel}. Save Chunk to persist.");
                        },
                        () => ResetDjMusicParamToOriginal(inst));
                }
            }
        }
    }

    private void DrawInspectorCrateLandingBounce(PS2AnyObject? thisObj, ChunkSource? scriptChunkSrc)
    {
        uint matchingCrateBounceId = thisObj is not null
            ? CrateLandedOnBehaviourIds.FirstOrDefault(id => thisObj.RefBehaviours.Contains((ushort)id))
            : 0;
        if (matchingCrateBounceId != 0 && scriptChunkSrc is not null)
        {
            var globalBehSecForCrateBounce = MeshDecoder.CodeSection(scriptChunkSrc.GlobalRm2, TwinConstants.CODE_BEHAVIOURS_SECTION);
            if (ImGui.CollapsingHeader("Crate Landing Bounce##cratebouncehdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawCrateLandingBounceForId(scriptChunkSrc, globalBehSecForCrateBounce, matchingCrateBounceId);
            }
        }
    }
}
