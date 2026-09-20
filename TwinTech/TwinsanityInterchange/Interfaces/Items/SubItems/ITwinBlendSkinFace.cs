using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems
{
    public interface ITwinBlendSkinFace : ITwinSerializable
    {
        /// <summary>
        /// Amount of vertexes in the face
        /// </summary>
        UInt32 VertexesAmount { get; set; }
        /// <summary>
        /// Final positions of vertexes for the face in case of XBox
        /// <para>Offsets for vertexes for the face in case of PS2</para>
        /// </summary>
        List<VertexBlendShape> Vertices { get; set; }

        /// <summary>
        /// Converts compressed vertex data into proper format
        /// </summary>
        void CalculateData();
    }
}
