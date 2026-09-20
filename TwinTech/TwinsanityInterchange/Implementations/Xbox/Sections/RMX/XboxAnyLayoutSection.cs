using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX
{
    public class XboxAnyLayoutSection : BaseTwinSection
    {
        public XboxAnyLayoutSection() : base()
        {
            idToClassDictionary.Add(Constants.LAYOUT_TEMPLATES_SECTION, typeof(XboxAnyTemplatesSection));
            idToClassDictionary.Add(Constants.LAYOUT_AI_POSITIONS_SECTION, typeof(XboxAnyAIPositionsSection));
            idToClassDictionary.Add(Constants.LAYOUT_AI_PATHS_SECTION, typeof(XboxAnyAIPathsSection));
            idToClassDictionary.Add(Constants.LAYOUT_POSITIONS_SECTION, typeof(XboxAnyPositionsSection));
            idToClassDictionary.Add(Constants.LAYOUT_PATHS_SECTION, typeof(XboxAnyPathsSection));
            idToClassDictionary.Add(Constants.LAYOUT_SURFACES_SECTION, typeof(XboxAnySurfacesSection));
            idToClassDictionary.Add(Constants.LAYOUT_INSTANCES_SECTION, typeof(XboxAnyInstancesSection));
            idToClassDictionary.Add(Constants.LAYOUT_TRIGGERS_SECTION, typeof(XboxAnyTriggersSection));
            idToClassDictionary.Add(Constants.LAYOUT_CAMERAS_SECTION, typeof(XboxAnyCamerasSection));
        }
    }
}
