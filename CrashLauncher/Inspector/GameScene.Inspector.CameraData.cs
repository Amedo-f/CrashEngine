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
    private void DrawInspectorCameraData(Entity e)
    {
        var camMarker = e.Get<CrashEngine.Importer.CameraMarker>();
        if (camMarker is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Camera Data##cam", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var camData = camMarker.Source;
                ImGui.TextDisabled("Position/Rotation/Scale: see Transform above.");

                static string CamTypeName(ITwinCamera.CameraType t) => t switch
                {
                    ITwinCamera.CameraType.Null => "None",
                    ITwinCamera.CameraType.BossCamera => "Boss",
                    ITwinCamera.CameraType.CameraPoint => "Point",
                    ITwinCamera.CameraType.CameraLine => "Line",
                    ITwinCamera.CameraType.CameraPath => "Path",
                    ITwinCamera.CameraType.CameraSpline => "Spline",
                    ITwinCamera.CameraType.CameraSub1C09 => "Unk1C09",
                    ITwinCamera.CameraType.CameraPoint2 => "Point2",
                    ITwinCamera.CameraType.CameraSub1C0C => "Unk1C0C",
                    ITwinCamera.CameraType.CameraLine2 => "Line2",
                    ITwinCamera.CameraType.CameraZone => "Zone",
                    _ => $"Unk (0x{(uint)t:X})",
                };
                ImGui.TextDisabled($"Cam Item 1: {CamTypeName(camData.TypeIndex1)}    Cam Item 2: {CamTypeName(camData.TypeIndex2)}");

                ImGui.SetNextItemWidth(-1f);
                int camByte = camData.UnkByte;
                if (ImGui.DragInt("Unk Byte##camData", ref camByte, 1f, 0, 255)) camData.UnkByte = (byte)camByte;
                ImGui.SetNextItemWidth(-1f);
                int camShort = camData.UnkShort;
                if (ImGui.DragInt("Unk Short##camData", ref camShort, 1f, 0, ushort.MaxValue)) camData.UnkShort = (ushort)camShort;
                ImGui.SetNextItemWidth(-1f);
                float camFloat1 = camData.UnkFloat1;
                if (ImGui.DragFloat("Unk Float1##camData", ref camFloat1, 0.01f)) camData.UnkFloat1 = camFloat1;

                ImGui.Separator();
                ImGui.TextDisabled("Flags (CamHeader) -- labels confirmed real, everything else is a raw bit:");

                uint header = camData.CameraHeader;
                bool Bit(int i) => (header & (1u << i)) != 0;
                void SetBit(int i, bool v) { if (v) header |= (1u << i); else header &= ~(1u << i); }

                bool FlagBox(int i, string label)
                {
                    bool v = Bit(i);
                    if (ImGui.Checkbox($"{label}##camflag{i}", ref v)) { SetBit(i, v); camData.CameraHeader = header; return true; }
                    return false;
                }

                FlagBox(0, "Flag 0");
                ImGui.SameLine(); FlagBox(1, "Flag 1");
                ImGui.SameLine(); FlagBox(2, "Flag 2");
                if (Bit(2))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int u3 = unchecked((int)camData.UnkInt3);
                    if (ImGui.DragInt("Unk Int3##camData", ref u3)) camData.UnkInt3 = unchecked((uint)u3);
                    ImGui.SetNextItemWidth(-1f);
                    int u4 = unchecked((int)camData.UnkInt4);
                    if (ImGui.DragInt("Unk Int4##camData", ref u4)) camData.UnkInt4 = unchecked((uint)u4);
                    ImGui.Unindent();
                }

                FlagBox(3, "Distance");
                if (Bit(3))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f4 = camData.UnkFloat4;
                    if (ImGui.DragFloat("Distance Min##camData", ref f4, 0.05f)) camData.UnkFloat4 = f4;
                    ImGui.SetNextItemWidth(-1f);
                    float f5 = camData.UnkFloat5;
                    if (ImGui.DragFloat("Distance Max##camData", ref f5, 0.05f)) camData.UnkFloat5 = f5;
                    ImGui.Unindent();
                }

                FlagBox(4, "Flag 4");
                ImGui.SameLine(); FlagBox(5, "Flag 5");

                FlagBox(6, "Angle Around Focus");
                if (Bit(6))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i5 = unchecked((int)camData.UnkInt5);
                    if (ImGui.DragInt("Angle Min##camData", ref i5)) camData.UnkInt5 = unchecked((uint)i5);
                    ImGui.SetNextItemWidth(-1f);
                    int i6 = unchecked((int)camData.UnkInt6);
                    if (ImGui.DragInt("Angle Max##camData", ref i6)) camData.UnkInt6 = unchecked((uint)i6);
                    ImGui.Unindent();
                }

                FlagBox(7, "Field of View");
                if (Bit(7))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i1 = unchecked((int)camData.UnkInt1);
                    if (ImGui.DragInt("FOV Min##camData", ref i1)) camData.UnkInt1 = unchecked((uint)i1);
                    ImGui.SetNextItemWidth(-1f);
                    int i2 = unchecked((int)camData.UnkInt2);
                    if (ImGui.DragInt("FOV Max##camData", ref i2)) camData.UnkInt2 = unchecked((uint)i2);
                    ImGui.Unindent();
                }

                FlagBox(8, "Local XYZ Offset From Focus");
                bool bit28 = Bit(28);
                if (Bit(8) || bit28)
                {
                    ImGui.Indent();
                    ImGui.SetNextItemWidth(-1f);
                    var v1 = new Vector3(camData.UnkVector1.X, camData.UnkVector1.Y, camData.UnkVector1.Z);
                    if (ImGui.DragFloat3("Offset 1 (XYZ)##camData", ref v1, 0.05f))
                    { var u1 = camData.UnkVector1; u1.X = v1.X; u1.Y = v1.Y; u1.Z = v1.Z; camData.UnkVector1 = u1; } // Amedo
                    if (bit28)
                    {
                        ImGui.SetNextItemWidth(-1f);
                        float w1 = camData.UnkVector1.W;
                        if (ImGui.DragFloat("Offset 1 W##camData", ref w1, 0.05f)) { var u1 = camData.UnkVector1; u1.W = w1; camData.UnkVector1 = u1; } // Amedo
                    }
                    ImGui.SetNextItemWidth(-1f);
                    var v2 = new Vector3(camData.UnkVector2.X, camData.UnkVector2.Y, camData.UnkVector2.Z);
                    if (ImGui.DragFloat3("Offset 2 (XYZ)##camData", ref v2, 0.05f))
                    { var u2 = camData.UnkVector2; u2.X = v2.X; u2.Y = v2.Y; u2.Z = v2.Z; camData.UnkVector2 = u2; } // Amedo
                    if (bit28)
                    {
                        ImGui.SetNextItemWidth(-1f);
                        float w2 = camData.UnkVector2.W;
                        if (ImGui.DragFloat("Offset 2 W##camData", ref w2, 0.05f)) { var u2 = camData.UnkVector2; u2.W = w2; camData.UnkVector2 = u2; } // Amedo
                    }
                    ImGui.Unindent();
                }

                bool bit9 = Bit(9), bit10 = Bit(10);
                FlagBox(9, "Flag 9");
                ImGui.SameLine(); FlagBox(10, "Flag 10");
                if (bit9 || bit10)
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f2 = camData.UnkFloat2;
                    if (ImGui.DragFloat("Unk Float2##camData", ref f2, 0.05f)) camData.UnkFloat2 = f2;
                    ImGui.SetNextItemWidth(-1f);
                    float f3 = camData.UnkFloat3;
                    if (ImGui.DragFloat("Unk Float3##camData", ref f3, 0.05f)) camData.UnkFloat3 = f3;
                    ImGui.Unindent();
                }

                FlagBox(11, "Lock Control");

                FlagBox(12, "Flag 12");
                if (Bit(12))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f6 = camData.UnkFloat6;
                    if (ImGui.DragFloat("Unk Float6##camData", ref f6, 0.05f)) camData.UnkFloat6 = f6;
                    ImGui.Unindent();
                }

                FlagBox(13, "Movement Speed");
                if (Bit(13))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f7 = camData.UnkFloat7;
                    if (ImGui.DragFloat("Speed##camData", ref f7, 0.05f)) camData.UnkFloat7 = f7;
                    ImGui.Unindent();
                }

                FlagBox(14, "Flag 14");

                FlagBox(15, "Flag 15");
                if (Bit(15))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i7 = unchecked((int)camData.UnkInt7);
                    if (ImGui.DragInt("Unk Int7##camData", ref i7)) camData.UnkInt7 = unchecked((uint)i7);
                    ImGui.Unindent();
                }

                FlagBox(16, "Flag 16");
                if (Bit(16))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i8 = unchecked((int)camData.UnkInt8);
                    if (ImGui.DragInt("Unk Int8##camData", ref i8)) camData.UnkInt8 = unchecked((uint)i8);
                    ImGui.Unindent();
                }

                FlagBox(17, "Flag 17");
                if (Bit(17))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i9 = unchecked((int)camData.UnkInt9);
                    if (ImGui.DragInt("Unk Int9##camData", ref i9)) camData.UnkInt9 = unchecked((uint)i9);
                    ImGui.Unindent();
                }

                FlagBox(18, "Flag 18");
                if (Bit(18))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f8 = camData.UnkFloat8;
                    if (ImGui.DragFloat("Unk Float8##camData", ref f8, 0.05f)) camData.UnkFloat8 = f8;
                    ImGui.Unindent();
                }

                FlagBox(19, "Lock Control And Look Up");
                FlagBox(20, "Lock Vertical Angle");
                FlagBox(21, "Unused Fixed Point? (uncertain)");

                if (ImGui.TreeNode("Other flags (no confirmed label)##camotherflags"))
                {
                    foreach (int i in new[] { 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 })
                    {
                        FlagBox(i, $"Flag {i}" + (i == 28 ? " (Offset W, see flag 8 above)" : ""));
                        if (i != 31) ImGui.SameLine();
                    }
                    ImGui.TreePop();
                }

                ImGui.Spacing();
                ImGui.TextDisabled($"CamHeader raw: 0x{camData.CameraHeader:X8}");
            }
        }
    }
}
