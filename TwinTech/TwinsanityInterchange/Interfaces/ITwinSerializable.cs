using System;
using System.IO;

namespace Twinsanity.TwinsanityInterchange.Interfaces
{
    public interface ITwinSerializable
    {
        /// <summary>
        /// Deserialize item from stream
        /// </summary>
        /// <param name="reader">Data to read from</param>
        /// <param name="length">Length of the data</param>
        void Read(BinaryReader reader, Int32 length);
        /// <summary>
        /// Serialize item
        /// </summary>
        /// <param name="writer">Destination of the data</param>
        void Write(BinaryWriter writer);
        /// <summary>
        /// Compute any needed resources and internals that end user doesn't need to know about
        /// </summary>
        void Compile();
        /// <summary>
        /// Get length of item
        /// </summary>
        /// <returns></returns>
        Int32 GetLength();
    }
}
