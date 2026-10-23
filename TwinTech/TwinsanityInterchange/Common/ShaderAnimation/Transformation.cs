namespace Twinsanity.TwinsanityInterchange.Common.ShaderAnimation
{
    public struct Transformation : Interfaces.ITwinSerializable // Amedo
    {
        System.Int16 transformValue;

        public System.Single Value
        {
            get => transformValue / 4096f;
            set => transformValue = (System.Int16)(value * 4096);
        }

        public System.Single RotationValue
        {
            get => (transformValue * 16) / (float)(System.UInt16.MaxValue + 1) * (float)System.Math.PI * 2;
            set => transformValue = (System.Int16)((System.UInt16.MaxValue + 1) * value / (16 * (float)System.Math.PI * 2));
        }

        public System.Int16 PureValue => transformValue;

        public System.Int32 GetLength() => 2;

        public void Compile() { }

        public void Read(System.IO.BinaryReader reader, System.Int32 length) => transformValue = reader.ReadInt16();

        public void Write(System.IO.BinaryWriter writer) => writer.Write(transformValue);
    }
}
