using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace CrashEngine.Importer;

public static class BlankSceneTemplate
{
    public static PS2AnyTwinsanityRM2 CreateBlankRm2()
    {
        var rm2 = new PS2AnyTwinsanityRM2();

        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout = new PS2AnyLayoutSection();
            layout.SetID((uint)lid);
            AddEmpty<PS2AnyTemplatesSection>(layout, Constants.LAYOUT_TEMPLATES_SECTION);
            AddEmpty<PS2AnyAIPositionsSection>(layout, Constants.LAYOUT_AI_POSITIONS_SECTION);
            AddEmpty<PS2AnyAIPathsSection>(layout, Constants.LAYOUT_AI_PATHS_SECTION);
            AddEmpty<PS2AnyPositionsSection>(layout, Constants.LAYOUT_POSITIONS_SECTION);
            AddEmpty<PS2AnyPathsSection>(layout, Constants.LAYOUT_PATHS_SECTION);
            AddEmpty<PS2AnySurfacesSection>(layout, Constants.LAYOUT_SURFACES_SECTION);
            AddEmpty<PS2AnyInstancesSection>(layout, Constants.LAYOUT_INSTANCES_SECTION);
            AddEmpty<PS2AnyTriggersSection>(layout, Constants.LAYOUT_TRIGGERS_SECTION);
            AddEmpty<PS2AnyCamerasSection>(layout, Constants.LAYOUT_CAMERAS_SECTION);
            rm2.AddItem(layout);
        }

        var code = new PS2AnyCodeSection();
        code.SetID((uint)Constants.LEVEL_CODE_SECTION);
        AddEmpty<PS2AnyGameObjectsSection>(code, Constants.CODE_GAME_OBJECTS_SECTION);
        AddEmpty<PS2AnyBehavioursSection>(code, Constants.CODE_BEHAVIOURS_SECTION);
        AddEmpty<PS2AnyAnimationsSection>(code, Constants.CODE_ANIMATIONS_SECTION);
        AddEmpty<PS2AnyOGIsSection>(code, Constants.CODE_OGIS_SECTION);
        AddEmpty<PS2AnyBehaviourCommandsSequencesSection>(code, Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);
        { var unk = new BaseTwinItem(); unk.SetID((uint)Constants.CODE_UNK_ITEM); code.AddItem(unk); }
        AddEmpty<PS2AnySoundsSection>(code, Constants.CODE_SOUND_EFFECTS_SECTION);
        AddEmpty<PS2AnySoundsSection>(code, Constants.CODE_LANG_ENG_SECTION);
        AddEmpty<PS2AnySoundsSection>(code, Constants.CODE_LANG_FRE_SECTION);
        AddEmpty<PS2AnySoundsSection>(code, Constants.CODE_LANG_GER_SECTION);
        AddEmpty<PS2AnySoundsSection>(code, Constants.CODE_LANG_SPA_SECTION);
        AddEmpty<PS2AnySoundsSection>(code, Constants.CODE_LANG_ITA_SECTION);
        AddEmpty<PS2AnySoundsSection>(code, Constants.CODE_LANG_JPN_SECTION);
        rm2.AddItem(code);

        var gfx = new PS2AnyGraphicsSection();
        gfx.SetID((uint)Constants.LEVEL_GRAPHICS_SECTION);
        AddEmpty<PS2AnyTexturesSection>(gfx, Constants.GRAPHICS_TEXTURES_SECTION);
        AddEmpty<PS2AnyMaterialsSection>(gfx, Constants.GRAPHICS_MATERIALS_SECTION);
        AddEmpty<PS2AnyModelsSection>(gfx, Constants.GRAPHICS_MODELS_SECTION);
        AddEmpty<PS2AnyRigidModelsSection>(gfx, Constants.GRAPHICS_RIGID_MODELS_SECTION);
        AddEmpty<PS2AnySkinsSection>(gfx, Constants.GRAPHICS_SKINS_SECTION);
        AddEmpty<PS2AnyBlendSkinsSection>(gfx, Constants.GRAPHICS_BLEND_SKINS_SECTION);
        AddEmpty<PS2AnyMeshesSection>(gfx, Constants.GRAPHICS_MESHES_SECTION);
        rm2.AddItem(gfx);

        var particles = new PS2AnyParticleData();
        particles.SetID((uint)Constants.LEVEL_PARTICLES_ITEM);
        rm2.AddItem(particles);

        var coll = new PS2AnyCollisionData();
        coll.SetID((uint)Constants.LEVEL_COLLISION_ITEM);
        rm2.AddItem(coll);

        return rm2;
    }

    public static PS2AnyTwinsanitySM2 CreateBlankSm2(string sceneName)
    {
        var sm2 = new PS2AnyTwinsanitySM2();

        var gfx = new PS2AnyGraphicsSection();
        gfx.SetID((uint)Constants.SCENERY_GRAPHICS_SECTION);
        AddEmpty<PS2AnyTexturesSection>(gfx, Constants.GRAPHICS_TEXTURES_SECTION);
        AddEmpty<PS2AnyMaterialsSection>(gfx, Constants.GRAPHICS_MATERIALS_SECTION);
        AddEmpty<PS2AnyModelsSection>(gfx, Constants.GRAPHICS_MODELS_SECTION);
        AddEmpty<PS2AnyRigidModelsSection>(gfx, Constants.GRAPHICS_RIGID_MODELS_SECTION);
        AddEmpty<PS2AnyMeshesSection>(gfx, Constants.GRAPHICS_MESHES_SECTION);
        sm2.AddItem(gfx);

        var scenery = new PS2AnyScenery
        {
            Name = sceneName,
            FogColor = 0,
            HasLighting = false,
        };
        scenery.SetID((uint)Constants.SCENERY_SECENERY_ITEM);
        sm2.AddItem(scenery);

        var link = new PS2AnyLink();
        link.SetID((uint)Constants.SCENERY_LINK_ITEM);
        sm2.AddItem(link);

        return sm2;
    }

    private static void AddEmpty<T>(BaseTwinSection parent, int id) where T : BaseTwinSection, new()
    {
        var sec = new T();
        sec.SetID((uint)id);
        parent.AddItem(sec);
    }

    public static (PS2AnyTwinsanityRM2 Rm2, PS2AnyTwinsanitySM2 Sm2) CreateFromRealLevel(
        PS2AnyTwinsanityRM2 sourceRm2, PS2AnyTwinsanitySM2 sourceSm2, string sceneName)
    {
        var rm2 = DeepClone<PS2AnyTwinsanityRM2>(sourceRm2);
        var sm2 = DeepClone<PS2AnyTwinsanitySM2>(sourceSm2);

        var scenery = sm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        if (scenery is not null)
            scenery.Name = sceneName;

        return (rm2, sm2);
    }

    private const uint CrashStateFlags        = 0x7D2Eu;
    private const ushort DjMusicObjectId      = 0x013C;
    private const ushort AmbienceManagerObjId = 0x02F5;

    public static (PS2AnyTwinsanityRM2 Rm2, PS2AnyTwinsanitySM2 Sm2) CreateCleanTemplate(
        PS2AnyTwinsanityRM2 sourceRm2, PS2AnyTwinsanitySM2 sourceSm2, string sceneName)
    {
        var rm2 = DeepClone<PS2AnyTwinsanityRM2>(sourceRm2);
        var sm2 = DeepClone<PS2AnyTwinsanitySM2>(sourceSm2);

        int[] toEmpty = {
            Constants.LAYOUT_TEMPLATES_SECTION, Constants.LAYOUT_AI_POSITIONS_SECTION,
            Constants.LAYOUT_AI_PATHS_SECTION,  Constants.LAYOUT_POSITIONS_SECTION,
            Constants.LAYOUT_PATHS_SECTION,     Constants.LAYOUT_SURFACES_SECTION,
            Constants.LAYOUT_TRIGGERS_SECTION,  Constants.LAYOUT_CAMERAS_SECTION,
        };

        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
            if (layout is null) continue;

            foreach (int sid in toEmpty)
                layout.GetItem<BaseTwinSection>((uint)sid)?.ClearItems();

            var instSec = layout.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION);
            if (instSec is null) continue;

            var keep = new List<ITwinItem>();
            for (int i = 0; i < instSec.GetItemsAmount(); i++)
            {
                if (instSec.GetItem(i) is not PS2AnyInstance inst) continue;

                if (inst.StateFlags == CrashStateFlags)
                {
                    SetPos(inst, 0f, 0f, 0f);
                    ZeroRotation(inst);
                    keep.Add(inst);
                }
                else if (inst.ObjectId == DjMusicObjectId)
                {
                    SetPos(inst, 0f, 6f, 0f);
                    keep.Add(inst);
                }
                else if (inst.ObjectId == AmbienceManagerObjId)
                {
                    SetPos(inst, 0f, 3f, 0f);
                    keep.Add(inst);
                }
            }
            instSec.ClearItems();
            foreach (var k in keep) instSec.AddItem(k);
        }

        var scenery = sm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        if (scenery is not null)
        {
            scenery.Name = sceneName;
            scenery.Sceneries.Clear();
        }

        var collision = rm2.GetItem<PS2AnyCollisionData>((uint)Constants.LEVEL_COLLISION_ITEM);
        if (collision is not null)
        {
            collision.Triggers.Clear();
            collision.Groups.Clear();
            collision.Triangles.Clear();
            collision.Vectors.Clear();
        }

        var particles = rm2.GetItem<PS2AnyParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        if (particles is not null)
        {
            particles.ParticleSystems.Clear();
            particles.ParticleEmitters.Clear();
        }

        var link = sm2.GetItem<PS2AnyLink>((uint)Constants.SCENERY_LINK_ITEM);
        if (link is not null)
            link.LinksList.Clear();

        return (rm2, sm2);
    }

    private static void SetPos(PS2AnyInstance inst, float x, float y, float z)
    {
        inst.Position.X = x;
        inst.Position.Y = y;
        inst.Position.Z = z;
    }

    private static void ZeroRotation(PS2AnyInstance inst)
    {
        inst.RotationX.Angle = 0; inst.RotationX.Fract = 0;
        inst.RotationY.Angle = 0; inst.RotationY.Fract = 0;
        inst.RotationZ.Angle = 0; inst.RotationZ.Fract = 0;
    }

    private static T DeepClone<T>(T source) where T : BaseTwinSection, new()
    {
        PrepSkinsForWrite(source);
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true))
            source.Write(bw);
        ms.Position = 0;
        var clone = new T();
        using var br = new BinaryReader(ms);
        clone.Read(br, (int)ms.Length);
        PrepSkinsForWrite(clone);
        return clone;
    }

    public static void PrepSkinsForWrite(BaseTwinSection section)
    {
        for (int i = 0; i < section.GetItemsAmount(); i++)
        {
            var item = section.GetItem(i);
            switch (item)
            {
                case ITwinSkin skin:
                    foreach (var ss in skin.SubSkins) ss.CalculateData();
                    break;
                case ITwinBlendSkin blend:
                    foreach (var sb in blend.SubBlends)
                    {
                        sb.GetType().GetMethod("CalculateData", Type.EmptyTypes)?.Invoke(sb, null);
                        foreach (var m in sb.Models)
                            m.GetType().GetMethod("CalculateData", Type.EmptyTypes)?.Invoke(m, null);
                    }
                    break;
                case BaseTwinSection nested:
                    PrepSkinsForWrite(nested);
                    break;
            }
        }
    }
}
