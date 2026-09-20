using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class Vector2 : ITwinSerializable
    {
        public float X { get; set; }
        public float Y { get; set; }
        public int GetLength()
        {
            return Constants.SIZE_VECTOR2;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            X = reader.ReadSingle();
            Y = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
        }
    }
}
