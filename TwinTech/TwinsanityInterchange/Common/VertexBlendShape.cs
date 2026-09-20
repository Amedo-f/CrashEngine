using System;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class VertexBlendShape
    {
        public Vector4 GetVector4()
        {
            var resultVec = new Vector4();
            resultVec.SetBinaryX((UInt32)((Int32)(Offset.X / BlendShape.X)));
            resultVec.SetBinaryY((UInt32)((Int32)(Offset.Y / BlendShape.Y)));
            resultVec.SetBinaryZ((UInt32)((Int32)(Offset.Z / BlendShape.Z)));
            resultVec.W = 1.0f;

            return resultVec;
        }

        public Vector3 BlendShape { get; set; }
        public Vector4 Offset { get; set; }
    }
}
