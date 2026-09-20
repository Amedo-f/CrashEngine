using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinPosition : ITwinItem
    {
        /// <summary>
        /// Coordinates in the chunk
        /// </summary>
        Vector4 Position { get; set; }
    }
}
