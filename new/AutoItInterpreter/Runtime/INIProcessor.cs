using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.IO;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    public static class INIProcessor
    {
        private const char COMMENT_CHAR = '#';
        private static readonly Regex REGEX_SECTION = new Regex(@"^\s*\[\s*(?<sec>[\w\-]+)\s*\]", RegexOptions.Compiled);
        private static readonly Regex REGEX_PROPERTY = new Regex(@"^\s*(?<prop>[\w\-]+)\s*\=\s*(?<val>.*)\s*$", RegexOptions.Compiled);


        public static Dictionary<string, Dictionary<string, string>> ReadINI(string path)
        {
            Dictionary<string, Dictionary<string, string>> ini = new Dictionary<string, Dictionary<string, string>>();
            string section = "";

            foreach (string line in File.ReadAllLines(path))
            {
                string ln = (line.Contains(COMMENT_CHAR) ? line[..line.LastIndexOf(COMMENT_CHAR)] : line).Trim();

                if (ln.Match(REGEX_SECTION, out Match m))
                    section = m.Groups["sec"].ToString();
                else if (ln.Match(REGEX_PROPERTY, out m))
                {
                    if (!ini.ContainsKey(section))
                        ini[section] = new Dictionary<string, string>();

                    ini[section][m.Groups["prop"].ToString()] = m.Groups["val"].ToString();
                }
            }

            return ini;
        }

        public static void WriteINI(string path, IDictionary<string, IDictionary<string, string>> ini)
        {
            StringBuilder sb = new StringBuilder();

            foreach (string section in ini.Keys)
            {
                sb.AppendLine($"[{section}]");

                foreach (string property in ini[section].Keys)
                    sb.AppendLine($"{property}={ini[section][property]}");
            }

            File.WriteAllText(path, sb.ToString());
        }
    }
}
