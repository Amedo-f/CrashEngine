using System;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics
{
    public class PS2AnyMesh : PS2AnyRigidModel, ITwinMesh
    {
        public override String GetName()
        {
            return $"Mesh {id:X}";
        }
    }
}
