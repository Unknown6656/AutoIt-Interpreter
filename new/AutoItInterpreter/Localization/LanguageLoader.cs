using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Localization
{
    using static Program;

    public sealed class LanguageLoader
    {
        public static ImmutableDictionary<string, LanguagePack> LanguagePacks { get; }


        static LanguageLoader()
        {
            const string @namespace = nameof(Localization);
            Assembly asm = typeof(LanguageLoader).Assembly;
            Dictionary<string, LanguagePack> langs = new Dictionary<string, LanguagePack>();
            Regex regex_json = new Regex($@"^.+\.{@namespace}\.(lang)?-*(?<code>\w+)-*(lang)?\.json$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            Match? match = null;

            foreach ((string? code, string name) in from r in asm.GetManifestResourceNames()
                                                   where r.Match(regex_json, out match)
                                                   select (match?.Groups["code"].Value.ToLower(), r))
                try
                {
                    using Stream? resource = asm.GetManifestResourceStream(name);

                    if (resource is { })
                        using (StreamReader? rd = new StreamReader(resource))
                            langs[code] = JsonConvert.DeserializeObject<LanguagePack>(rd.ReadToEnd());
                }
                catch
                {
                    Telemetry.Measure(TelemetryCategory.Exceptions, delegate { });
                }

            foreach (FileInfo nfo in LANG_DIR.EnumerateFiles("./*.json"))
                try
                {
                    dynamic lang = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(nfo.FullName));
                    string code = lang.meta.code.ToLower();

                    if (!langs.ContainsKey(code))
                        langs[code] = lang;
                }
                catch
                {
                    Telemetry.Measure(TelemetryCategory.Exceptions, delegate { });
                }

            LanguagePacks = langs.ToImmutableDictionary();
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
                string fmt_str = _strings?.FirstOrDefault(kvp => kvp.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase)).Value ?? $"[{key.ToUpper()}]";
                int argc = Regex.Matches(fmt_str, @"\{(?<num>\d+)\}").Select(m => byte.TryParse(m.Groups["num"].Value, out byte b) ? b + 1 : 0).Append(0).Max();

                if (args.Length < argc)
                    Array.Resize(ref args, argc);

                return string.Format(fmt_str, args);
            }
        }

        [JsonProperty("meta.code")]
        public string LanguageCode { get; private set; } = "";
        [JsonProperty("meta.name")]
        public string LanguageName { get; private set; } = "";
        [JsonProperty("meta.beta")]
        public bool IsBeta { get; private set; } = false;
        [JsonProperty("strings"), DebuggerBrowsable(DebuggerBrowsableState.Never), EditorBrowsable(EditorBrowsableState.Never), DebuggerHidden]
        public dynamic? __DO_NOT_USE__strings
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
                        string path = prefix + DELIMITER + kvp.Key.ToLower();

                        if (kvp.Value is JObject jo)
                            traverse(path, jo);
                        else
                            _strings[path[1..]] = kvp.Value?.ToString() ?? $"[{path[1..].ToUpper()}]";
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
