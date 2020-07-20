using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.IO;
using System;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

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
                            LoadLanguagePackFromJSON(assembly.Location, rd.ReadToEnd(), overwrite_existing);
                }
                catch
                {
                }
        }

        public void LoadLanguagePackFromJSONFile(FileInfo file, bool overwrite_existing = true)
        {
            string path = Path.GetFullPath(file.FullName);

            LoadLanguagePackFromJSON(path, File.ReadAllText(path), overwrite_existing);
        }

        private void LoadLanguagePackFromJSON(string path, string json, bool overwrite_existing = true)
        {
            try
            {
                LanguagePack lang = JsonConvert.DeserializeObject<LanguagePack>(json);
                string code = lang.LanguageCode.ToLowerInvariant();

                lang.FilePath = path;

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
                LoadLanguagePackFromJSONFile(file, overwrite_existing);
        }
    }

    [JsonConverter(typeof(JsonPathConverter))]
    public sealed class LanguagePack
    {
        public const string DELIMITER = ".";

        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();


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

        [JsonProperty("meta.code")]
        public string LanguageCode { get; private set; } = "";

        [JsonProperty("meta.name")]
        public string LanguageName { get; private set; } = "";

        [JsonProperty("meta.beta")]
        public bool IsBeta { get; private set; } = false;

        [JsonProperty("strings"), DebuggerBrowsable(DebuggerBrowsableState.Never), EditorBrowsable(EditorBrowsableState.Never), DebuggerHidden]
        public object? __DO_NOT_USE__strings
        {
            get => _strings;
            private set
            {
                _strings.Clear();

                if ((JObject?)value is JObject dic)
                    traverse("", dic);

                void traverse(string prefix, JObject dic)
                {
                    foreach (KeyValuePair<string, JToken?> kvp in dic)
                    {
                        string path = prefix + DELIMITER + kvp.Key.ToLowerInvariant();

                        if (kvp.Value is JObject jo)
                            traverse(path, jo);
                        else
                            _strings[path.Substring(1)] = kvp.Value?.ToString() ?? $"[{path.Substring(1).ToUpperInvariant()}]";
                    }
                }
            }
        }


        public override string ToString() => $"{LanguageCode} - {LanguageName}";
    }

    internal sealed class JsonPathConverter
        : JsonConverter
    {
        public override bool CanWrite => false;


        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject jobj = JObject.Load(reader);
            object? target = Activator.CreateInstance(objectType);

            foreach (PropertyInfo prop in objectType.GetProperties().Where(p => p.CanRead && p.CanWrite))
            {
                JsonPropertyAttribute? att = prop.GetCustomAttributes(true).OfType<JsonPropertyAttribute>().FirstOrDefault();
                string jsonPath = att?.PropertyName ?? prop.Name;

                if (jobj.SelectToken(jsonPath) is { } token && token.Type != JTokenType.Null)
                {
                    object? value = token.ToObject(prop.PropertyType, serializer);

                    prop.SetValue(target, value, null);
                }
            }

            return target;
        }

        // CanConvert is not called when [JsonConverter] attribute is used
        public override bool CanConvert(Type objectType) => false;

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => throw new NotImplementedException();
    }
}
