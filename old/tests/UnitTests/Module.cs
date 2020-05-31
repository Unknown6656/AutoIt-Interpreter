using System.Linq;
using System;

namespace UnitTests
{
    public static class Module
    {
        private static readonly string[] vstr;

        public static Version Version { get; }
        public static string GitHash { get; }


        static Module()
        {
            vstr = Properties.Resources.version.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

            Version = Version.TryParse(vstr[0], out Version v) ? v : null;
            GitHash = vstr.Length > 0 ? vstr[1] : "";

            if ((GitHash?.Length ?? 0) == 0)
                GitHash = "<unknown git commit hash>";
        }
    }
}
