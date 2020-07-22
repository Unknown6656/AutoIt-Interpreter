using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;

namespace Unknown6656.AutoIt3.Localization
{
    public sealed class LanguageLoader
    {
        private readonly Dictionary<string, LanguagePack> _packs = new();

        public string[] LoadedLanguageCodes => _packs.Keys.ToArray();

        public LanguagePack? CurrentLanguage { get; private set; }

        public LanguagePack? this[string code] => TryGetLanguagePack(code, out LanguagePack? pack) ? pack : null;


        public void LoadLanguagePackFromAssembly(Assembly assembly, string @namespace, bool overwrite_existing = true)
        {
            Regex regex_json = new Regex($@"^.+\.{@namespace}\.(lang)?-*(?<code>\w+)-*(lang)?\.json$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach ((string? code, string name) in from res in assembly.GetManifestResourceNames()
                                                    let match = regex_json.Match(res)
                                                    where match.Success
                                                    select (match.Groups["code"].Value.ToLowerInvariant(), res))
                try
                {
                    using Stream? resource = assembly.GetManifestResourceStream(name);

                    if (resource is { })
                        using (StreamReader? rd = new StreamReader(resource))
                            LoadLanguagePackFromYAML(assembly.Location, rd.ReadToEnd(), overwrite_existing);
                }
                catch
                {
                }
        }

        public void LoadLanguagePackFromYAMLFile(FileInfo file, bool overwrite_existing = true)
        {
            string path = Path.GetFullPath(file.FullName);

            LoadLanguagePackFromYAML(path, File.ReadAllText(path), overwrite_existing);
        }

        private void LoadLanguagePackFromYAML(string path, string yaml, bool overwrite_existing = true)
        {
            try
            {
                LanguagePack lang = LanguagePack.FromYAML(yaml);
                string code = lang.LanguageCode.ToLowerInvariant();

                lang.FilePath = path;

                if (CurrentLanguage is null && _packs.Count == 0)
                    CurrentLanguage = lang;

                if (overwrite_existing || !_packs.ContainsKey(code))
                    _packs[code] = lang;
            }
            catch
            {
            }
        }

        public void HasLanguagePack(string code) => _packs.ContainsKey(code.ToLowerInvariant());

        public bool TryGetLanguagePack(string code, out LanguagePack? pack) => _packs.TryGetValue(code.ToLowerInvariant(), out pack);

        public bool TrySetCurrentLanguagePack(string code)
        {
            bool res = TryGetLanguagePack(code, out LanguagePack? pack);

            if (res)
                CurrentLanguage = pack;

            return res;
        }

        public void LoadLanguagePacksFromDirectory(DirectoryInfo directory, bool overwrite_existing = true)
        {
            foreach (FileInfo file in directory.EnumerateFiles())
                LoadLanguagePackFromYAMLFile(file, overwrite_existing);
        }
    }

    public sealed class LanguagePack
    {
        private static readonly Regex REGEX_YAML = new Regex(@"^(?<indent> *)(?<quote>""|)(?<key>[^"":]+)\k<quote> *:( *""(?<value>.*)"")? *(#.*)?$", RegexOptions.Compiled);

        private readonly IDictionary<string, string> _strings;


        public string this[string key, params object?[] args]
        {
            get
            {
                string fmt_str = _strings?.FirstOrDefault(kvp => kvp.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase)).Value ?? $"[{key.ToUpperInvariant()}]";
                int argc = Regex.Matches(fmt_str, @"\{(?<num>\d+)\}")
                                .Cast<Match>()
                                .Select(m => byte.TryParse(m.Groups["num"].Value, out byte b) ? b + 1 : 0)
                                .Append(0)
                                .Max();

                if (args.Length < argc)
                    Array.Resize(ref args, argc);

                return string.Format(fmt_str, args);
            }
        }

        internal string FilePath { get; set; } = "";

        public string LanguageCode => _strings["meta.code"];

        public string LanguageName => _strings["meta.name"];

        public bool IsBeta => !bool.TryParse(_strings["meta.beta"], out bool beta) || beta;

        public string Author => _strings.TryGetValue("meta.author", out string auth) ? auth : "unknown6656";


        private LanguagePack(IDictionary<string, string> strings) => _strings = strings;

        public override string ToString() => $"{LanguageCode} - {LanguageName}";

        internal static LanguagePack FromYAML(string yaml)
        {
            string[] lines = yaml.Replace("\t", "    ")
                                 .Replace("\r\n", "\n")
                                 .Replace("\v", "")
                                 .Replace("\0", "")
                                 .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            List<(int level, string path)> scope = new();
            Dictionary<string, string> dict = new();

            foreach (string line in lines)
                if (REGEX_YAML.Match(line) is { Success: true, Groups: GroupCollection groups })
                {
                    int indent = groups["indent"].Length;
                    string key = groups["key"].Value;

                    for (int i = scope.Count; i-- > 0;)
                        if (indent > scope[i].level)
                            break;
                        else
                            scope.RemoveAt(i);

                    if (groups["value"].Success)
                    {
                        string value = groups["value"].Value;

                        dict[string.Join(".", scope.Select(s => s.path).Append(key))] = value;
                    }
                    else
                        scope.Add((indent, key));
                }

            return new LanguagePack(dict);
        }
    }
}
