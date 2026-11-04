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
    private void DrawBehavioursSectionBody(PS2AnyObject obj, BaseTwinSection behSecForList, bool editingGlobal, ChunkSource? scriptChunkSrc)
    {
        if (editingGlobal)
            ImGui.TextDisabled("Editing the GLOBAL (Startup\\Default.rm2) scripts — shared by\n" +
                                "EVERY placed instance of this exact object type, in every level.");
        ImGui.BeginChild("##objBehList", new Vector2(-1f, 200f), ImGuiChildFlags.Border);
        foreach (var bid in obj.RefBehaviours)
        {
            var bItem = behSecForList.GetItem<BaseTwinItem>(bid);
            string bLabel = bItem is PS2BehaviourGraph bg && !string.IsNullOrEmpty(bg.Name)
                ? bg.Name : (bItem is TwinBehaviourStarter ? "(starter, no name)" : "(missing)");
            string bType = bItem is TwinBehaviourStarter ? "Starter" : bItem is PS2BehaviourGraph ? "Graph" : "?";
            bool clicked = ImGui.Selectable($"0x{bid:X4}  [{bType,-7}]  {bLabel}", _inspectedBehaviourId == bid); // Amedo 2026-09-25
            if (clicked && bItem is PS2BehaviourGraph clickedGraph)
                OpenBehaviourInTab(bid, clickedGraph, editingGlobal, manual: false); // Amedo 2026-09-25
        }
        ImGui.EndChild();

        ImGui.SetNextItemWidth(90f);
        ImGui.InputText("##openBehIdInput", ref _openBehIdText, 8,
            ImGuiInputTextFlags.CharsHexadecimal);
        ImGui.SameLine();
        if (ImGui.Button("Open by ID (hex)##openbehid"))
        {
            if (uint.TryParse(_openBehIdText, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out uint manualId) &&
                behSecForList.GetItem<PS2BehaviourGraph>(manualId) is { } manualGraph)
            {
                OpenBehaviourInTab(manualId, manualGraph, editingGlobal, manual: true); // Amedo 2026-09-25
            }
            else
            {
                _browser.Log($"Open by ID: 0x{_openBehIdText} not found in this " +
                    $"{(editingGlobal ? "GLOBAL" : "LOCAL")} Behaviours section (or not a Graph).");
            }
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Open ANY behaviour graph in this section by its raw hex id --\n" +
                         "not just ones listed above. Real use case: a script's own\n" +
                         "terminal state can hand off to a totally different graph by id\n" +
                         "(e.g. \"State_3(2635)\") that's never in RefBehaviours at all.");

        if (_inspectedBehaviourId is uint ibid && _inspectedBehaviourGraph is not null) // Amedo 2026-09-25
        {
            ImGui.TextDisabled($"0x{ibid:X4} is open in the Behaviour tab.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Show##showbehtab")) _focusBehaviourTab = true;
        }
    }

    private string? GetPristineBehaviourGraphText(bool isGlobal, uint bid)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            string relPath = isGlobal ? @"Startup\Default.rm2" : _rm2;
            using var stream = pkg.OpenByPath(relPath);
            if (stream is null) return null;
            using var reader = new BinaryReader(stream);
            var freshRm2 = isGlobal
                ? (Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2)new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default()
                : new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshGraph = freshBehSec?.GetItem<PS2BehaviourGraph>(bid);
            return freshGraph is null ? null : GraphToEditableText(freshGraph);
        }
        catch { return null; }
    }
}
