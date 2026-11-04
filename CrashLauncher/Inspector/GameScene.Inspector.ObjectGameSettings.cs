using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnySound = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnySound;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PointLight = Twinsanity.TwinsanityInterchange.Common.Lights.PointLight;
using NegativeLight = Twinsanity.TwinsanityInterchange.Common.Lights.NegativeLight;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using ITwinCamera = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.ITwinCamera;

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
            var globalBehSecForSpring = scriptChunkSrc.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            if (ImGui.CollapsingHeader("Spring Crate Bounce##springbouncehdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawSpringCrateBounce(scriptChunkSrc, globalBehSecForSpring);
            }
        }

        if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)EarthWormSquashLaunchId) &&
            behSecForInsp is not null)
        {
            if (ImGui.CollapsingHeader("Earth Worm Squash Launch##wormlaunchhdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawWormLaunchPower(behSecForInsp);
            }
        }

        if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)RigidCannonActivatedId) &&
            behSecForInsp is not null)
        {
            if (ImGui.CollapsingHeader("Cannon Shot Power##cannonpowerhdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawCannonShotPower(behSecForInsp);
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
            var globalBehSecForCrateBounce = scriptChunkSrc.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            if (ImGui.CollapsingHeader("Crate Landing Bounce##cratebouncehdr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawCrateLandingBounceForId(scriptChunkSrc, globalBehSecForCrateBounce, matchingCrateBounceId);
            }
        }
    }
}
