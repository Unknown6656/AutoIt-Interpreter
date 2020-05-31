using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;

using Newtonsoft.Json;

using AutoItInterpreter.Properties;

namespace AutoItInterpreter
{
    public abstract class Localizable
    {
        public Language Language { get; }

        public string this[string name, params object[] args] => Language[name, args];

        public Localizable(Language lang) => Language = lang;
    }

    public sealed class Language
    {
        private readonly static Language _errcodes;
        private readonly dynamic _langobj;


        public static Dictionary<string, Language> Languages { get; }

        public static string[] LanugageCodes => Languages.Keys.ToArray();

        public string Code => this["meta.code"];

        public string this[string name]
        {
            get
            {
                try
                {
                    if (name is null)
                        throw null;

                    dynamic val = _langobj;

                    foreach (string path in name.Split('/', '.', '\\'))
                        val = val[path];

                    return val is string s ? s : val?.ToString();
                }
                catch
                {
                    $"The resource string '{name}' could not be found in the current resource file.".Error();

                    return $"[{name.ToUpper()}]";
                }
            }
        }

        public string this[string name, params object[] args]
        {
            get
            {
                string s = this[name];

                if (args?.Length > 0)
                {
                    int cnt = Regex.Matches(s, @"\{\s*(?<num>\d+)\s*(\:[^\}]+)?\}").SelectWhere(m => (int.TryParse(m.Get("num"), out int i), i + 1)).Concat(args.Length).Max();

                    if (cnt > args.Length)
                    {
                        int l = args.Length;

                        Array.Resize(ref args, cnt);

                        for (int i = 0; i < l; ++i)
                            args[i] = "[UNKNOWN_VALUE]";
                    }

                    return s.Format(args);
                }
                else
                    return s;
            }
        }


        static Language()
        {
            Assembly asm = typeof(Language).Assembly;
            DirectoryInfo dir = new DirectoryInfo(asm.Location + "/../lang");
            Dictionary<string, dynamic> langs = new Dictionary<string, dynamic>();
            Match m = default;

            using (MemoryStream ms = new MemoryStream(Resources.errorcodes))
            using (StreamReader rd = new StreamReader(ms))
                _errcodes = new Language(JsonConvert.DeserializeObject<dynamic>(rd.ReadToEnd()));

            foreach ((string code, string name) in from r in asm.GetManifestResourceNames()
                                                   where r.Match(@"^.+\.lang\.(?<code>\w+)\.json$", out m)
                                                   select (m.Get("code").ToLower(), r))
                try
                {
                    using (Stream resource = asm.GetManifestResourceStream(name))
                    using (StreamReader rd = new StreamReader(resource))
                        langs[code] = JsonConvert.DeserializeObject<dynamic>(rd.ReadToEnd());
                }
                catch
                {
                }

            if (dir.Exists)
                foreach (FileInfo nfo in dir.EnumerateFiles("./lang/*.json"))
                    try
                    {
                        dynamic lang = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(nfo.FullName));
                        string code = lang.meta.code.ToLower();

                        if (!langs.ContainsKey(code))
                            langs[code] = lang;
                    }
                    catch
                    {
                    }

            Languages = new Dictionary<string, Language>();

            foreach (string code in langs.Keys)
                Languages[code] = new Language(langs[code]);
        }

        private Language(dynamic langobj) => _langobj = langobj;

        public static int GetErrorNumber(string name) => int.Parse(_errcodes[name]);

        public static Language FromLanguageCode(string code) => Languages[(code ?? "en").ToLower().Trim()];
    }
}
