using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;

using Newtonsoft.Json;

namespace AutoItInterpreter
{
    public sealed class Language
    {
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

        public string this[string name, params object[] args] => args?.Length > 0 ? this[name].Format(args) : this[name];


        static Language()
        {
            Assembly asm = typeof(Language).Assembly;
            DirectoryInfo dir = new DirectoryInfo(asm.Location + "/../lang");
            Dictionary<string, dynamic> langs = new Dictionary<string, dynamic>();
            Match m = default;

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

        public static Language FromLanguageCode(string code) => Languages[(code ?? "en").ToLower().Trim()];
    }
}
