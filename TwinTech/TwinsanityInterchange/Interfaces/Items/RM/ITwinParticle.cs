using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM
{
    public interface ITwinParticle : ITwinItem
    {
        UInt32 Version { get; set; }
        List<TwinParticleSystem> ParticleSystems { get; set; }
        List<TwinParticleEmitter> ParticleEmitters { get; set; }
    }
}
