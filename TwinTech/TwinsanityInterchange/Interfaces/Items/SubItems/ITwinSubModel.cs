using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems
{
    public interface ITwinSubModel : ITwinSerializable
    {
        /// <summary>
        /// Vertex positions
        /// </summary>
        List<Vector4> Vertexes { get; set; }
        /// <summary>
        /// UV map
        /// </summary>
        List<Vector4> UVW { get; set; }
        /// <summary>
        /// Vertex colors
        /// </summary>
        List<Vector4> Colors { get; set; }
        /// <summary>
        /// Vertex emit colors
        /// </summary>
        List<Vector4> EmitColor { get; set; }
        /// <summary>
        /// Vertex normals
        /// </summary>
        List<Vector4> Normals { get; set; }
        /// <summary>
        /// Vertex indexing order
        /// </summary>
        List<bool> Connection { get; set; }
        /// <summary>
        /// The amount of verticies in the batch that form triangle strip/strips
        /// </summary>
        List<Int32> GroupSizes { get; set; }

        /// <summary>
        /// Converts VIF code into vertex data
        /// </summary>
        void CalculateData();

        UInt32 GetMinSkinCoord();
    }
}
