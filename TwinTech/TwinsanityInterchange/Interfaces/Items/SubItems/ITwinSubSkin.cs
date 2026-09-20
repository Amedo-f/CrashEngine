using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems
{
    public interface ITwinSubSkin : ITwinSerializable
    {
        /// <summary>
        /// Material to render model with
        /// </summary>
        UInt32 Material { get; set; }
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
        /// Model's joints
        /// </summary>
        List<VertexJointInfo> SkinJoints { get; set; }
        /// <summary>
        /// The amount of verticies in the batch that form triangle strip/strips
        /// </summary>
        List<Int32> GroupSizes { get; set; }

        UInt32 GetMinSkinCoord();


        /// <summary>
        /// Converts VIF code into vertex data
        /// </summary>
        void CalculateData();
    }
}
