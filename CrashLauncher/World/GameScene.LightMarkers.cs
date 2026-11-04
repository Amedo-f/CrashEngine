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
using PS2AnyAIPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPath;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinBoundingBoxBuilder;
using TwinChunkLinkBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinChunkLinkBoundingBoxBuilder;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using TwinTrigger = Twinsanity.TwinsanityInterchange.Common.TwinTrigger;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    // Amedo 2026-09-19
    private void BuildSceneryLightMarkers(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery scenery)
    {
        if (_lightsRoot is null) return;
        foreach (var ch in _lightsRoot.Children.ToList()) _lightsRoot.RemoveChild(ch);
        for (int i = 0; i < scenery.PointLights.Count; i++)
            AddOneLightMarker(scenery.PointLights[i], false, $"PointLight_{i}");
        for (int i = 0; i < scenery.NegativeLights.Count; i++)
            AddOneLightMarker(scenery.NegativeLights[i], true, $"NegLight_{i}");
    }

    // Amedo 2026-09-19
    private Entity? AddOneLightMarker(Twinsanity.TwinsanityInterchange.Common.Lights.Light light, bool isNegative, string name)
    {
        if (_lightsRoot is null) return null;
        var ent = new Entity(name);
        ent.Transform.Position = new Vector3(light.Position.X, light.Position.Y, light.Position.Z);
        ent.Transform.Scale = new Vector3(0.5f, 0.5f, 0.5f);
        _lightsRoot.AddChild(ent);
        ent.Add(new CrashEngine.Importer.SceneryLightMarker { Source = light, IsNegative = isNegative });

        var tint = isNegative ? new Vector4(1f, 0.4f, 0.4f, 1f) : new Vector4(1f, 1f, 1f, 1f);

        Texture2D? tex = null;
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "lightbulb.png");
            if (System.IO.File.Exists(iconPath)) tex = Texture2D.FromFile(Engine.Instance.GL, iconPath);
        }
        catch { }

        if (tex is not null)
        {
            var iconRdr = ent.Add(new DirectCubeRenderer { Mesh = _lightIconQuad ?? _quadMesh, Color = tint });
            iconRdr.Albedo              = tex;
            iconRdr.Mat.AlphaBlend      = true;
            iconRdr.Mat.BillboardRender = true;
            iconRdr.Mat.Culling         = Material.CullMode.Both;
            iconRdr.Mat.DepthWrite      = false;
            iconRdr.Mat.IgnoreDepthTest = true;
        }
        else
        {
            var fillRdr = ent.Add(new DirectCubeRenderer { Mesh = _cubeMesh, Color = new Vector4(tint.X, tint.Y, tint.Z, 0.85f) });
            fillRdr.Mat.AlphaBlend = true; fillRdr.Mat.DepthWrite = false; fillRdr.Mat.IgnoreDepthTest = true;
        }
        return ent;
    }

    // Amedo 2026-09-26
    private void SyncLightMarkerPositionsLive()
    {
        if (_lightsRoot is null) return;
        foreach (var ent in _lightsRoot.Children)
        {
            var light = ent.Get<CrashEngine.Importer.SceneryLightMarker>()?.Source;
            if (light is null) continue;
            var p = ent.Transform.Position;
            var src = light.Position;
            if (src.X == p.X && src.Y == p.Y && src.Z == p.Z) continue;
            light.Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(p.X, p.Y, p.Z, src.W);
        }
    }

    // Amedo 2026-09-26
    private static int EnableAddedSceneryLights(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery scenery)
    {
        int gAmb = scenery.AmbientLights.Count, gDir = scenery.DirectionalLights.Count, gPt = scenery.PointLights.Count;
        int totalNodes = scenery.Sceneries.Count;
        int rescoped = 0;
        void Enable(int gi)
        {
            if (gi < 0 || gi >= 128 || totalNodes == 0) return;
            if (scenery.Sceneries.Any(nd => nd.LightsEnabler[gi])) return;
            foreach (var nd in scenery.Sceneries) nd.LightsEnabler[gi] = true;
            rescoped++;
        }
        for (int p = 0; p < gPt; p++) Enable(gAmb + gDir + p);
        for (int q = 0; q < scenery.NegativeLights.Count; q++) Enable(gAmb + gDir + gPt + q);
        return rescoped;
    }

    // Amedo 2026-09-19
    private void SelectLightMarkerFor(Twinsanity.TwinsanityInterchange.Common.Lights.Light light)
    {
        var ent = _lightsRoot?.Children.FirstOrDefault(c => ReferenceEquals(c.Get<CrashEngine.Importer.SceneryLightMarker>()?.Source, light));
        if (ent is not null) SelectClicked(ent, false);
    }

    // Amedo 2026-09-19
    private void RemoveLightMarkerFor(Twinsanity.TwinsanityInterchange.Common.Lights.Light light)
    {
        var ent = _lightsRoot?.Children.FirstOrDefault(c => ReferenceEquals(c.Get<CrashEngine.Importer.SceneryLightMarker>()?.Source, light));
        if (ent is not null) _lightsRoot!.RemoveChild(ent);
    }
}
