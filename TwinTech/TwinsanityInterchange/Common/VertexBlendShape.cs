using System;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public struct VertexBlendShape // Amedo
    {
        public Vector4 GetVector4()
        {
            var resultVec = new Vector4();
            resultVec.SetBinaryX((UInt32)((Int32)MathF.Round(Offset.X / BlendShape.X))); // Amedo
            resultVec.SetBinaryY((UInt32)((Int32)MathF.Round(Offset.Y / BlendShape.Y))); // Amedo
            resultVec.SetBinaryZ((UInt32)((Int32)MathF.Round(Offset.Z / BlendShape.Z))); // Amedo
            resultVec.W = 1.0f;

            return resultVec;
        }

        public Vector3 BlendShape { get; set; }
        public Vector4 Offset { get; set; }
    }
}
