using System;

namespace AutoItCoreLibrary
{
    public static class Module
    {
        public static Version LibraryVersion { get; }


        static Module() => LibraryVersion = Version.TryParse(Properties.Resources.version.Trim(), out Version v) ? v : null;
    }
}
