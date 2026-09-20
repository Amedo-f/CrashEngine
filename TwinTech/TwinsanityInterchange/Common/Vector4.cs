using System;
using System.Diagnostics;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class Vector4 : ITwinSerializable
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }
        public Vector4()
        {
            X = 0.0f;
            Y = 0.0f;
            Z = 0.0f;
            W = 0.0f;
        }
        public Vector4(float X, float Y, float Z, float W)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
            this.W = W;
        }

        public Vector4(Vector3 pos, float W)
        {
            this.X = pos.X;
            this.Y = pos.Y;
            this.Z = pos.Z;
            this.W = W;
        }

        public Vector4(Vector4 other)
        {
            X = other.X;
            Y = other.Y;
            Z = other.Z;
            W = other.W;
        }
        public int GetLength()
        {
            return Constants.SIZE_VECTOR4;
        }

        public void Compile()
        {
            return;
        }

        public void Normalize()
        {
            var length = Length();
            X /= length;
            Y /= length;
            Z /= length;
        }

        public Single Length()
        {
            return (Single)Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        public void Read(BinaryReader reader, int length)
        {
            X = reader.ReadSingle();
            Y = reader.ReadSingle();
            Z = reader.ReadSingle();
            W = reader.ReadSingle();
        }

        public Vector4 Multiply(float value)
        {
            var resVec = new Vector4
            {
                X = X * value,
                Y = Y * value,
                Z = Z * value,
                W = W * value
            };
            return resVec;
        }

        public Vector4 Divide(float value)
        {
            Debug.Assert(Math.Abs(value) > 0.000001f, "Division value can't be 0");
            var resVec = new Vector4()
            {
                X = X / value,
                Y = Y / value,
                Z = Z / value,
                W = W / value
            };
            return resVec;
        }

        public static Vector4 operator +(Vector4 v1, Vector4 v2)
        {
            return new Vector4(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z, v1.W + v2.W);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
            writer.Write(W);
        }
        public void SetBinaryX(UInt32 src)
        {
            X = BitConverter.ToSingle(BitConverter.GetBytes(src), 0);
        }
        public void SetBinaryY(UInt32 src)
        {
            Y = BitConverter.ToSingle(BitConverter.GetBytes(src), 0);
        }
        public void SetBinaryZ(UInt32 src)
        {
            Z = BitConverter.ToSingle(BitConverter.GetBytes(src), 0);
        }
        public void SetBinaryW(UInt32 src)
        {
            W = BitConverter.ToSingle(BitConverter.GetBytes(src), 0);
        }
        public UInt32 GetBinaryX()
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(X), 0);
        }
        public UInt32 GetBinaryY()
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(Y), 0);
        }
        public UInt32 GetBinaryZ()
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(Z), 0);
        }
        public UInt32 GetBinaryW()
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(W), 0);
        }
        public override String ToString()
        {
            return $"({X:0.00000}; {Y:0.00000}; {Z:0.00000}; {W:0.00000})";
        }
        public Color GetColor()
        {
            var c = new Color
            {
                R = (Byte)(X * 255),
                G = (Byte)(Y * 255),
                B = (Byte)(Z * 255),
                A = (Byte)(W * 255),
                AlphaBlendFlag = StoresColorWithAlphaBlend
            };
            return c;
        }
        public Boolean StoresColorWithAlphaBlend { get; set; }
        public static Vector4 FromColor(Color c)
        {
            var vec = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            vec.StoresColorWithAlphaBlend = c.AlphaBlendFlag;
            return vec;
        }

        private String DebuggerDisplay
        {
            get => $"x,y,z,w = {X},{Y},{Z},{W}; BinX, BinY, BinZ, BinW = {GetBinaryX():X}, {GetBinaryY():X}, {GetBinaryZ():X}, {GetBinaryW():X}";
        }
    }
}
