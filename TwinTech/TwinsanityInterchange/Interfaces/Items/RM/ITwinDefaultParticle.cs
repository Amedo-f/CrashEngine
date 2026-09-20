using System;
using System.Collections.Generic;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM
{
    /// <summary>
    /// Special particle data section only used in Default
    /// </summary>
    public interface ITwinDefaultParticle : ITwinParticle
    {
        /// <summary>
        /// Texture IDs bank
        /// </summary>
        UInt32[] TextureIDs { get; set; }
        /// <summary>
        /// Material IDs bank
        /// </summary>
        UInt32[] MaterialIDs { get; set; }
        /// <summary>
        /// Decal texture
        /// </summary>
        UInt32 DecalTextureID { get; set; }
        /// <summary>
        /// Decal material
        /// </summary>
        UInt32 DecalMaterialID { get; set; }
        /// <summary>
        /// Unknown binary data
        /// </summary>
        Byte[] UnkData { get; set; }
        /// <summary>
        /// Unknown binary data
        /// </summary>
        Byte[] UnkBlob { get; set; }
        /// <summary>
        /// Unknown integer parameters
        /// </summary>
        Int32[] UnkInts { get; set; }
        /// <summary>
        /// Unknown binary data lists
        /// </summary>
        List<Byte[]> UnkBlobs { get; set; }
    }
}
