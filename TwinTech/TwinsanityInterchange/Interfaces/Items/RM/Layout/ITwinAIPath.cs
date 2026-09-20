using System;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinAIPath : ITwinItem
    {
        /// <summary>
        /// AI path arguments
        /// </summary>
        UInt16[] Args { get; set; }
    }
}
