using System;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code
{
    /// <summary>
    /// Generic Twinsanity's sound effect interface.
    /// However implementations are completely different for PS2 and XBOX.
    /// <para></para>
    /// PS2 implementation <seealso cref="Implementations.PS2.Items.RM2.Code.PS2AnySound"/>
    /// <para></para>
    /// XBox implementation <seealso cref="Implementations.Xbox.Items.RMX.Code.XboxAnySound"/>
    /// </summary>
    public interface ITwinSound : ITwinItem
    {
        /// <summary>
        /// Header value
        /// </summary>
        UInt32 Header { get; set; }
        /// <summary>
        /// Unknown flag. Unused for XBox sounds
        /// </summary>
        Byte UnkFlag { get; set; }
        /// <summary>
        /// Frequency factor. Unused for XBox sounds use GetFreq()/SetFreq()
        /// </summary>
        Byte FreqFac { get; set; }
        /// <summary>
        /// Unknown param1. Unused for XBox sounds
        /// </summary>
        UInt16 Param1 { get; set; }
        /// <summary>
        /// Unknown param2. Unused for XBox sounds
        /// </summary>
        UInt16 Param2 { get; set; }
        /// <summary>
        /// Unknown param3. Unused for XBox sounds
        /// </summary>
        UInt16 Param3 { get; set; }
        /// <summary>
        /// Unknown param4. Unused for XBox sounds
        /// </summary>
        UInt16 Param4 { get; set; }
        /// <summary>
        /// Raw sound data
        /// </summary>
        Byte[] Sound { get; set; }

        /// <summary>
        /// Set the internal frequency value or factor
        /// </summary>
        /// <param name="freq">Frequency amount</param>
        void SetFreq(UInt16 freq);
        /// <summary>
        /// 
        /// </summary>
        /// <returns>Frequency of the sound</returns>
        UInt16 GetFreq();
        /// <summary>
        /// Converts raw sound data to its PCM format
        /// </summary>
        /// <returns></returns>
        Byte[] ToPCM();
        /// <summary>
        /// Converts PCM format to raw sound data and sets it
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        void SetDataFromPCM(Byte[] data);
    }
}
