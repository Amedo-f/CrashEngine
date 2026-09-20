using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces
{
    public interface ITwinPSF : ITwinItem
    {
        /// <summary>
        /// Font pages
        /// </summary>
        public List<ITwinPTC> FontPages { get; set; }
        /// <summary>
        /// Purpose currently unknown
        /// </summary>
        public List<Vector4> UnkVecs { get; set; }
        /// <summary>
        /// Purpose currently unknown
        /// </summary>
        public Int32 UnkInt { get; set; }
    }
}
