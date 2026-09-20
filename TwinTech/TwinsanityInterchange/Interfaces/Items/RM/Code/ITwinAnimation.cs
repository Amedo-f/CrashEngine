using System;
using Twinsanity.TwinsanityInterchange.Common.Animation;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code
{
    public interface ITwinAnimation : ITwinItem
    {
        /// <summary>
        /// If main animation data is present
        /// </summary>
        Boolean HasAnimationData { get; set; }
        /// <summary>
        /// If facial animation data is present
        /// </summary>
        Boolean HasFacialAnimationData { get; set; }
        /// <summary>
        /// Total amount of frames(keyframes) in the animation
        /// </summary>
        UInt16 TotalFrames { get; set; }
        /// <summary>
        /// Default FPS of the animation
        /// </summary>
        Byte DefaultFPS { get; set; }
        /// <summary>
        /// Main animation data
        /// </summary>
        TwinAnimation MainAnimation { get; set; }
        /// <summary>
        /// Facial animation data
        /// </summary>
        TwinMorphAnimation FacialAnimation { get; set; }
    }
}
