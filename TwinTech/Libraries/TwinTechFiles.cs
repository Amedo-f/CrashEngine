using System;
using System.IO;

namespace Twinsanity.Libraries
{
    internal static class TwinTechFiles // Amedo 2026-11-08
    {
        public static string PathOf(params string[] parts) // Amedo 2026-11-08
        {
            string location = typeof(TwinTechFiles).Assembly.Location;
            string directory = Path.IsPathRooted(location) ? Path.GetDirectoryName(location) : AppContext.BaseDirectory;
            return Path.Combine(directory, Path.Combine(parts));
        }
    }
}
