using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems
{
    public interface ITwinBlendSkinModel : ITwinSerializable
    {
        /// <summary>
        /// Scale used to compress vectors into UInt16
        /// </summary>
        UInt32 CompileScale { get; set; }
        /// <summary>
        /// Total amount of vertexes in the model
        /// </summary>
        Int32 VertexesAmount { get; set; }
        /// <summary>
        /// Blend shape scale vector
        /// </summary>
        Vector3 BlendShape { get; set; }
        /// <summary>
        /// All the faces information for the model
        /// </summary>
        List<ITwinBlendSkinFace> Faces { get; set; }
        /// <summary>
        /// Vertexes of the model
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
        /// Joints for this model
        /// </summary>
        List<VertexJointInfo> SkinJoints { get; set; }
        /// <summary>
        /// The amount of verticies in the batch that form triangle strip/strips
        /// </summary>
        List<Int32> GroupSizes { get; set; }

        /// <summary>
        /// Converts VIF code into the vertex data of the model
        /// </summary>
        void CalculateData();
    }
}
