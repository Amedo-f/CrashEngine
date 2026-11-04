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
    private void DrawInspectorAddToAssets(Entity e)
    {
        if (e.Get<SceneryTile>() is not null)
        {
            if (ImGui.Button("Convert to Object (real)...", new Vector2(-1f, 0f)))
            {
                ConvertSceneryTileToObject(e);
            }
            if (ImGui.IsItemHovered())
            {
                MaybeTooltip("Turns this scenery tile into a real Object + Instance, reusing its\nSAME already-compiled mesh/material/texture data as-is (not re-baked),\nthen removes the original tile. Real Instances never go through the\nscenery tree's own visibility gate — sidesteps the 'disappears near\ncamera' bug entirely. Non-uniform scale on the tile can't be kept\n(Instances have no scale field). Save Chunk + Build ISO to test.");
            }

            var sceneryTargets = (_selectedSet.Count > 0 ? _selectedSet : new HashSet<Entity> { e })
                .Where(x => x.Get<SceneryTile>() is not null).ToList();
            if (sceneryTargets.Count > 0)
            {
                var label = sceneryTargets.Count > 1 ? $"Add {sceneryTargets.Count} Tiles to Assets..." : "Add to Assets...";
                if (ImGui.Button(label + "##addtoassets", new Vector2(-1f, 0f)))
                {
                    OpenAddToAssetsPopup(sceneryTargets);
                }
                if (ImGui.IsItemHovered())
                {
                    MaybeTooltip("Saves this tile (or every selected tile, keeping their relative\nlayout) as a reusable custom asset — pick or create a named folder,\nthen import the whole group into ANY level later from the Assets\npanel's 'My Assets' tab. Grafted as independent leaf(s) on import,\nnever merged into one shared tree with the destination's own scenery.");
                }
            }
        }

        if (e.Get<InstanceData>() is not null && e.Get<SceneryTile>() is null)
        {
            if (ImGui.Button("Add Object to Assets...##addobjtoassets", new Vector2(-1f, 0f)))
                OpenAddObjectToAssetsPopup(e);
            if (ImGui.IsItemHovered())
                MaybeTooltip("Saves this OBJECT (its full graph: code/behaviours/OGI/animations/\ngraphics + this instance's settings) as a reusable asset in 'My Assets'.\nRe-import into ANY level later. v1 references the source level, so keep\nit around. A 3D thumbnail is baked from this object's current mesh.");
        }
    }
}
