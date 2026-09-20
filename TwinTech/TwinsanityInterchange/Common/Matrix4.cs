using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class Matrix4 : ITwinSerializable
    {
        // Twinsanity matrices are column-major
        public Vector4 Column1 { get; set; }
        public Vector4 Column2 { get; set; }
        public Vector4 Column3 { get; set; }
        public Vector4 Column4 { get; set; }
        public Matrix4()
        {
            Column1 = new Vector4();
            Column2 = new Vector4();
            Column3 = new Vector4();
            Column4 = new Vector4();
        }
        public int GetLength()
        {
            return Column1.GetLength() + Column2.GetLength() + Column3.GetLength() + Column4.GetLength();
        }

        public void Compile()
        {
            return;
        }

        public Vector4 this[int key]
        {
            get
            {
                return key switch
                {
                    0 => Column1,
                    1 => Column2,
                    2 => Column3,
                    3 => Column4,
                    _ => throw new IndexOutOfRangeException(),
                };
            }
            set
            {
                switch (key)
                {
                    case 0:
                        Column1 = value;
                        break;
                    case 1:
                        Column2 = value;
                        break;
                    case 2:
                        Column3 = value;
                        break;
                    case 3:
                        Column4 = value;
                        break;
                }
                throw new IndexOutOfRangeException();
            }
        }
        public void Read(BinaryReader reader, int length)
        {
            Column1.Read(reader, Constants.SIZE_VECTOR4);
            Column2.Read(reader, Constants.SIZE_VECTOR4);
            Column3.Read(reader, Constants.SIZE_VECTOR4);
            Column4.Read(reader, Constants.SIZE_VECTOR4);
        }

        public void Write(BinaryWriter writer)
        {
            Column1.Write(writer);
            Column2.Write(writer);
            Column3.Write(writer);
            Column4.Write(writer);
        }
    }
}
