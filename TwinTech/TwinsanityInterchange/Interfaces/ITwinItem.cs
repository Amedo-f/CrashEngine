using System;
using System.IO;

namespace Twinsanity.TwinsanityInterchange.Interfaces
{
    public interface ITwinItem : ITwinSerializable
    {
        /// <summary>
        /// Get item's ID
        /// </summary>
        /// <returns></returns>
        UInt32 GetID();

        /// <summary>
        /// Set item's ID
        /// </summary>
        /// <param name="id">New ID</param>
        void SetID(UInt32 id);

        /// <summary>
        /// Get item's name
        /// </summary>
        /// <returns></returns>
        String GetName();

        /// <summary>
        /// Get item's hash. Doesn't recompute
        /// </summary>
        /// <returns></returns>
        String GetHash();
        /// <summary>
        /// Computes item's hash
        /// </summary>
        /// <param name="stream">Data to read from</param>
        void ComputeHash(Stream stream);
        /// <summary>
        /// Computes item's hash for a certain length instead of using the entire stream
        /// </summary>
        /// <param name="stream">Data to read from</param>
        /// <param name="length">Amount of data to read</param>
        void ComputeHash(Stream stream, UInt32 length);
        /// <summary>
        /// If the item is loaded
        /// </summary>
        /// <returns></returns>
        bool GetIsLoaded();
        /// <summary>
        /// Turn on/off lazy loading
        /// </summary>
        /// <param name="isLazy"></param>
        void SetIsLazy(bool isLazy);
        /// <summary>
        /// Get the item's root
        /// </summary>
        /// <returns></returns>
        ITwinSection GetRoot();
        /// <summary>
        /// Set the item's root
        /// </summary>
        /// <param name="root"></param>
        void SetRoot(ITwinSection root);
        /// <summary>
        /// Gets the item's parent
        /// </summary>
        /// <returns></returns>
        ITwinSection GetParent();
        /// <summary>
        /// Sets the item's parent
        /// </summary>
        /// <param name="parent"></param>
        void SetParent(ITwinSection parent);
        /// <summary>
        /// Get item's data stream
        /// </summary>
        /// <returns></returns>
        MemoryStream GetStream();
        /// <summary>
        /// Set item's data stream
        /// </summary>
        /// <param name="stream"></param>
        void SetStream(MemoryStream stream);
    }
}
