using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System;

using Newtonsoft.Json;

namespace AutoItInterpreter
{
    public static class Program
    {
        public static readonly FileInfo ASM_FILE = new FileInfo(typeof(Program).Assembly.Location);


        public static int Main(string[] argv)
        {
            int __inner__()
            {
                Dictionary<string, List<string>> dic = ParseParameters(argv,
                    ("i", "input"),
                    ("h", "help"),
                    ("?", "help"),
                    ("l", "lang"),
                    ("ll", "list-languages"),
                    ("s", "settings"),
                    ("rs", "reset-settings"),
                    ("v", "verbose"),
                    ("mef", "msbuild-error-format"),
                    ("msberr", "msbuild-error-format")
                );
                bool Cont(string arg) => dic.ContainsKey(arg);
                List<string> Get(string arg) => Cont(arg) ? dic[arg] : new List<string>();
                string GetF(string arg, string def = "") => Get(arg).FirstOrDefault() ?? def;

                if (Cont("help") || Cont("?"))
                {
                    PrintUsage();

                    return 0;
                }
                if (Cont("list-languages"))
                {
                    PrintLanguages();

                    return 0;
                }

                #region SETTINGS

                string stgpath = GetF("settings", InterpreterSettings.DefaultSettingsPath);
                InterpreterSettings settings;
                Language lang;

                if ((settings = InterpreterSettings.FromFile(stgpath)) is null)
                {
                    $"The settings file '{stgpath}' could not be found or parsed - the default settings will be used.".Warn();

                    stgpath = InterpreterSettings.DefaultSettingsPath;
                }

                if ((settings = InterpreterSettings.FromFile(stgpath)) is null || GetF("reset-settings", null) != null)
                    settings = InterpreterSettings.DefaultSettings;

                string lcode = GetF("lang", settings.LanguageCode ?? "").ToLower().Trim();

                if (Language.LanugageCodes.Contains(lcode))
                    lang = Language.FromLanguageCode(lcode);
                else
                {
                    $"The language code '{lcode}' is not associated with any known language. The language code 'en' will be used instead.".Warn();

                    lang = Language.FromLanguageCode("en");
                }

                if (!Cont("lang"))
                    settings.LanguageCode = lang.Code;

                settings.ToFile(stgpath);

                #endregion
                #region DO MAGIC

                Interpreter intp;
                InterpreterOptions opt = new InterpreterOptions(settings)
                {
                    Language = lang,
                    UseVerboseOutput = Cont("verbose"),
                    UseMSBuildErrorOutput = Cont("msbuild-error-format")
                };

                try
                {
                    intp = new Interpreter(GetF("input"), opt);
                }
                catch
                {
                    if (GetF("input", null) is string s)
                        lang["errors.general.inputfile_nfound", s].Error();
                    else
                        lang["errors.general.no_input"].Error();

                    return -1;
                }

                InterpreterState result = intp.DoMagic();

                #endregion

                return result.Errors.Count(x => x.Type == ErrorType.Fatal);
            }

            int ret = __inner__();

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press any key to exit ...");
                Console.ReadKey(true);
            }

            return ret;
        }

        public static Dictionary<string, List<string>> ParseParameters(string[] argv, params (string Short, string Long)[] translator)
        {
            Dictionary<string, string> trans = translator.ToDictionary(x => x.Short, x => x.Long);
            Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>();

            foreach ( string arg in argv)
            {
                string name = "", value = null;

                arg.Match(
                    (@"^\/(?<n>[a-z\?][\w\-]*)(:(?<v>.*))?$", m => (name, value) = (m.Get("n"), m.Get("v"))),
                    (@"^--(?<n>[a-z\?][\w\-]*)(=(?<v>.*))?$", m => (name, value) = (m.Get("n"), m.Get("v"))),
                    (@"^-(?<n>[a-z\?]\w*)(=(?<v>.*))?$", m => (name, value) = (trans.ContainsKey(m.Get("n")) ? trans[m.Get("n")] : m.Get("n"), m.Get("v"))),
                    (".*", _ => value = arg)
                );

                if (!dic.ContainsKey(name))
                    dic[name] = new List<string>();

                dic[name].Add(value);
            }

            return dic;
        }

        private static void PrintUsage()
        {
            PrintCopyrightHeader(ConsoleColor.Cyan, true);

            $@"
|                                                                                                       |
|    Usage:                                                                                             |
|    {ASM_FILE.Name,18} <options>                                                                       |
|                                                                                                       |
+-------------------+-----------------------+-----------------------------------------------------------+
| OPTION (short)    | OPTION (long)         | Effect                                                    |
+-------------------+-----------------------+-----------------------------------------------------------+
| -h, -?            | --help                | Displays this help menu.                                  |
| -i=...            | --input=...           | The input .au3 AutoIt Script file.             [required] |
| -s=...            | --settings=...        | The path to the .json settings file.                      |
| -rs               | --reset-settings      | Resets the .json settings file to its defaults.           |
| -l=....           | --lang=...            | Sets the language for the current session using the given |
|                   |                       | language code. (Doesn't affect the stored settings)       |
| -ll               | --list-languages      | Displays a list of all available display languages.       |
| -v                | --verbose             | Displays verbose compilation output (instead of only the  |
|                   |                       | compiler errors and warnings).                            |
| -mef, -msberr     | --msbuild-error-format| Displays the errors, notes and warnings using the MSBuild |
|                   |                       | error string format.                                      |
+-------------------+-----------------------+-----------------------------------------------------------+
|                                                                                                       |
| Most options can be used as follows:                                                                  |
|     x                   simple argument                                                               |
|     -x                  short variant without value                                                   |
|     -x=abc              short variant                                                                 |
|     --xy-z=abc          long variant                                                                  |
|     /xy-z:abc           alternative long variant                                                      |
|                                                                                                       |
+-------------------------------------------------------------------------------------------------------+
| If the compiler's return code is positive, it indicates how many fatal compiler errors exist. Zero    |
| represents a successful operation.                                                                    |
|                                                                                                       |
|    Example:                                                                                           |
|    {ASM_FILE.Name,18} -i=script.au3                                                                   |
|                                                                                                       |
+-------------------------------------------------------------------------------------------------------+
".TrimStart().PrintC(ConsoleColor.Cyan);
        }

        private static void PrintCopyrightHeader(ConsoleColor c, bool open = false) => PrintC($@"
+--------------------------------------- C#/F# AutoIt Interpreter --------------------------------------+
|                         AutoIt Interpreter : Copyright (c) Unknown6656, 2018{(DateTime.Now.Year > 2018 ? "-" + DateTime.Now.Year : "     ")}                     |
|                      Piglet Parser Library : Copyright (c) Dervall, 2012                              |
{(open ? "" : "+-------------------------------------------------------------------------------------------------------+")}".TrimEnd(), c);

        private static void PrintLanguages()
        {
            string[] lcodes = Language.LanugageCodes;

            PrintCopyrightHeader(ConsoleColor.Cyan);

            $"\n    Avaialable Languages ({lcodes.Length}):".PrintC(ConsoleColor.Cyan);

            foreach (string code in lcodes)
            {
                Language lang = Language.Languages[code];

                $"        {code} : {lang["meta.name"]} ({lang["meta.name_e"]}) by {lang["meta.author"]}".PrintC(ConsoleColor.Cyan);
            }
        }

        private static void PrintC(this string msg, ConsoleColor c)
        {
            ConsoleColor fg = Console.ForegroundColor;

            Console.ForegroundColor = c;
            Console.WriteLine(msg);
            Console.ForegroundColor = fg;
        }

        public static void Error(this string msg) => msg.PrintC(ConsoleColor.Red);

        public static void Warn(this string msg) => msg.PrintC(ConsoleColor.Yellow);

        public static void OK(this string msg) => msg.PrintC(ConsoleColor.Green);
    }

    public sealed class InterpreterSettings
    {
        public static string DefaultSettingsPath => "./settings.json";

        public static InterpreterSettings DefaultSettings => new InterpreterSettings
        {
            IncludeDirectories = new string[]
            {
                new FileInfo(typeof(Program).Assembly.Location).Directory.FullName,
                new FileInfo(typeof(Program).Assembly.Location).Directory.FullName + "/include",
                Directory.GetCurrentDirectory(),
                "/usr/local/include",
                "/usr/include",
                "C:/progra~1/autoit3/include",
                "C:/progra~2/autoit3/include",
            },
            IndentationStyle = IndentationStyle.AllmanStyle,
            UseOptimization = true,
            LanguageCode = "en"
        };

        public IndentationStyle IndentationStyle { set; get; }
        public string[] IncludeDirectories { set; get; }
        public bool UseOptimization { set; get; }
        public string LanguageCode { set; get; }


        public bool ToFile(string path)
        {
            try
            {
                File.WriteAllText(path, SerializeToJSON());

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal string SerializeToJSON() => JsonConvert.SerializeObject(this, Formatting.Indented);

        public static InterpreterSettings FromFile(string path)
        {
            try
            {
                return FromJSONString(File.ReadAllText(path));
            }
            catch
            {
                return default;
            }
        }

        internal static InterpreterSettings FromJSONString(string json) => JsonConvert.DeserializeObject<InterpreterSettings>(json);
    }

    public sealed class InterpreterOptions
    {
        public InterpreterSettings Settings { get; }
        public bool UseMSBuildErrorOutput { set; get; }
        public bool UseVerboseOutput { set; get; }
        public Language Language { set; get; }


        public InterpreterOptions(InterpreterSettings settings) => Settings = settings;
    }

    public enum IndentationStyle
        : byte
    {
        AllmanStyle,
        Shit
    }
}
