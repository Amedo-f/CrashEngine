using System;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace Twinsanity.TwinsanityInterchange.Interfaces
{
    public interface ITwinPTC : ITwinItem
    {
        /// <summary>
        /// Texture's Twinsanity ID
        /// </summary>
        public UInt32 TexID { get; set; }
        /// <summary>
        /// Material's Twinsanity ID
        /// </summary>
        public UInt32 MatID { get; set; }
        /// <summary>
        /// Texture object
        /// </summary>
        public ITwinTexture Texture { get; set; }
        /// <summary>
        /// Material object
        /// </summary>
        public ITwinMaterial Material { get; set; }
    }
}
