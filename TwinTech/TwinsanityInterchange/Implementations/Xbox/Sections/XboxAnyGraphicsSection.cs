using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.Graphics;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections
{
    public class XboxAnyGraphicsSection : BaseTwinSection
    {
        public XboxAnyGraphicsSection() : base()
        {
            idToClassDictionary.Add(Constants.GRAPHICS_TEXTURES_SECTION, typeof(XboxAnyTexturesSection));
            idToClassDictionary.Add(Constants.GRAPHICS_MATERIALS_SECTION, typeof(XboxAnyMaterialsSection));
            idToClassDictionary.Add(Constants.GRAPHICS_MODELS_SECTION, typeof(XboxAnyModelsSection));
            idToClassDictionary.Add(Constants.GRAPHICS_RIGID_MODELS_SECTION, typeof(XboxAnyRigidModelsSection));
            idToClassDictionary.Add(Constants.GRAPHICS_SKINS_SECTION, typeof(XboxAnySkinsSection));
            idToClassDictionary.Add(Constants.GRAPHICS_BLEND_SKINS_SECTION, typeof(XboxAnyBlendSkinsSection));
            idToClassDictionary.Add(Constants.GRAPHICS_MESHES_SECTION, typeof(XboxAnyMeshesSection));
            idToClassDictionary.Add(Constants.GRAPHICS_LODS_SECTION, typeof(XboxAnyLODsSection));
            idToClassDictionary.Add(Constants.GRAPHICS_SKYDOMES_SECTION, typeof(XboxAnySkydomesSection));
        }
    }
}
