using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinPath : ITwinItem
    {
        /// <summary>
        /// Connected positions
        /// </summary>
        List<Vector4> PointList { get; set; }
        /// <summary>
        /// Parameter for each position
        /// </summary>
        List<Vector2> ParameterList { get; set; }
    }
}
