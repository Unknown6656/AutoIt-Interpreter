using System.Linq;
using System;

namespace AutoItEditor
{
    public static class Module
    {
        public static Version EditorVersion { get; }
        public static string GitHash { get; }


        static Module()
        {
            string[] _vstr = Properties.Resources.version.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

            EditorVersion = Version.TryParse(_vstr[0], out Version v) ? v : null;
            GitHash = _vstr.Length > 0 ? _vstr[1] : "";

            if ((GitHash?.Length ?? 0) == 0)
                GitHash = "<unknown git commit hash>";
        }
    }
}
