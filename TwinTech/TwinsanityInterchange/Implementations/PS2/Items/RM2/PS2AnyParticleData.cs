using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common.Particles;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2
{
    public class PS2AnyParticleData : BaseTwinItem, ITwinParticle
    {
        public UInt32 Version { get; set; }
        public List<TwinParticleSystem> ParticleSystems { get; set; }
        public List<TwinParticleEmitter> ParticleEmitters { get; set; }

        public PS2AnyParticleData()
        {
            Version = 0x1E;
            ParticleSystems = new List<TwinParticleSystem>();
            ParticleEmitters = new List<TwinParticleEmitter>();
        }

        public override Int32 GetLength()
        {
            return 12 + ParticleSystems.Sum(t => t.GetLength()) + ParticleEmitters.Sum(i => i.GetLength());
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            Version = reader.ReadUInt32();
            if ((Version < 0x5 || Version > 0x1E) && Version != 0x20)
            {
                throw new Exception($"Invalid/Deprecated particle data section version: {Version}");
            }
            var typesAmt = reader.ReadInt32();
            for (var i = 0; i < typesAmt; ++i)
            {
                var type = new TwinParticleSystem(Version);
                type.Read(reader, length);
                ParticleSystems.Add(type);
            }
            var instAmt = reader.ReadInt32();
            for (var i = 0; i < instAmt; ++i)
            {
                var inst = new TwinParticleEmitter(Version);
                inst.Read(reader, length);
                ParticleEmitters.Add(inst);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(ParticleSystems.Count);
            foreach (var t in ParticleSystems)
            {
                t.Write(writer);
            }
            writer.Write(ParticleEmitters.Count);
            foreach (var i in ParticleEmitters)
            {
                i.Write(writer);
            }
        }

        public override String GetName()
        {
            return $"Particle data {id:X}";
        }
    }
}
