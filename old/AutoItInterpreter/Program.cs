using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Text;
using System.IO;
using System;

using Newtonsoft.Json;

using AutoItCoreLibrary;

namespace AutoItInterpreter
{
    public static class Program
    {
        public const string TITLE = "AutoIt3 Interpreter and Compiler by Unknown6656";
        public static readonly FileInfo ASM_FILE = new FileInfo(typeof(Program).Assembly.Location);
        private static readonly MemoryStream ms = new MemoryStream();
        private static TextWriter @out;


        public static int Main(string[] argv)
        {
            void resetstdout()
            {
                if (@out != null)
                {
                    Console.Out.Dispose();
                    Console.SetOut(@out);

                    ms.Dispose();
                }
            }

            int ret;

            try
            {
                Directory.SetCurrentDirectory(ASM_FILE.Directory.FullName);
                Console.Title = TITLE;

                if (Win32.System == OS.Windows)
                    Console.BufferWidth = Math.Max(201, Console.BufferWidth);

                ret = MainCore(ParseParameters(argv,
                    ("d", "debug"),
                    ("dp", "dependencies"),
                    ("o", "output"),
                    ("u", "unsafe"),
                    ("c", "clean-output"),
                    ("i", "input"),
                    ("h", "help"),
                    ("?", "help"),
                    ("l", "lang"),
                    ("q", "quiet"),
                    ("ll", "list-languages"),
                    ("kp", "key-pair"),
                    ("s", "settings"),
                    ("rs", "reset-settings"),
                    ("v", "verbose"),
                    ("vv", "visual"),
                    ("mef", "msbuild-error-format"),
                    ("ms", "msbuild-error-format"),
                    ("k", "keep-temp"),
                    ("g", "generate-always"),
                    ("t", "target-system"),
                    ("a", "architecture"),
                    ("wall", "warnings-as-errors"),
                    ("r", "run"),
                    ("w", "warm-up"),
                    ("wup", "warm-up")
                ), out _);

                resetstdout();
            }
            catch (Exception ex)
            when (!Debugger.IsAttached)
            {
                ret = -1;
                resetstdout();

                StringBuilder sb = new StringBuilder();

                do
                {
                    sb.Insert(0, $"[{ex.GetType().FullName}]  {ex.Message}:\n{ex.StackTrace}\n");

                    ex = ex.InnerException;
                }
                while (ex != null);

                DebugPrintUtil.PrintSeperator("FATAL ERROR");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(sb.ToString());
                Console.ForegroundColor = ConsoleColor.Gray;

                DebugPrintUtil.DisplayFinalResult(DebugPrintUtil.FinalResult.Errors_Failed);
            }

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press any key to exit ...");
                Console.ReadKey(true);
            }

            return ret;
        }

        public static int MainCore(Dictionary<string, List<string>> args, out InterpreterState result)
        {
            bool Cont(string arg) => args.ContainsKey(arg);
            List<string> Get(string arg) => Cont(arg) ? args[arg] : new List<string>();
            string GetF(string arg, string def = "") => Get(arg).FirstOrDefault() ?? def;

            result = null;

            if (Cont("help") || Cont("?"))
            {
                PrintUsage();

                return 0;
            }
            else if (Cont("list-languages"))
            {
                PrintLanguages();

                return 0;
            }
            else if (Cont("version"))
            {
                PrintVersion();

                return 0;
            }
            
            if (Cont("quiet"))
            {
                @out = Console.Out;

                Console.SetOut(new StreamWriter(ms));
            }

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


            if (!Cont("keep-temp") && Cont("debug") && Cont("run"))
                args["keep-temp"] = new List<string>();

            Interpreter intp;
            InterpreterOptions opt = new InterpreterOptions(settings)
            {
                Language = lang,
                RawArguments = args,
                UseVerboseOutput = Cont("verbose"),
                IncludeDebugSymbols = Cont("debug"),
                VisualOutputPath = GetF("visual", null),
                UseMSBuildErrorOutput = Cont("msbuild-error-format"),
                DeleteTempFilesAfterSuccess = !Cont("keep-temp"),
                GenerateCodeEvenWithErrors = Cont("generate-always"),
                KeyPairPath = GetF("key-pair", null),
                AllowUnsafeCode = Cont("unsafe"),
                RawCommandLine = Environment.CommandLine,
                TargetDirectory = GetF("output", null),
                CleanTargetFolder = Cont("clean-output"),
                TreatWarningsAsErrors = Cont("warnings-as-errors"),
                UseJITWarmup = Cont("warm-up"),
            };

            if (Cont("target-system"))
                try
                {
                    opt.Compatibility = (Compatibility)Enum.Parse(typeof(Compatibility), GetF("target-system").ToLower(), true);
                }
                catch
                {
                    lang["invalid_targetsys", GetF("target-system")].Error();

                    return -1;
                }

            if (Cont("architecture"))
                try
                {
                    opt.TargetArchitecture = (Architecture)Enum.Parse(typeof(Architecture), GetF("architecture").ToLower(), true);
                }
                catch
                {
                    lang["invalid_architecture", GetF("target-system")].Error();

                    return -1;
                }

            List<Assembly> deps = new List<Assembly>();

            foreach (string dep in Get("dependencies"))
                try
                {
                    deps.Add(Assembly.LoadFrom(new FileResolver(lang, dep).Resolve().FullName));
                }
                catch
                {
                    lang["errors.preproc.dependency_invalid", dep].Error();
                }

            opt.Dependencies.AddRange(deps);

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

            result = intp.Interpret();

            if (result.Result > DebugPrintUtil.FinalResult.OK_Warnings)
                return result.Errors.Count(x => x.Type == ErrorType.Fatal);
            else if (Cont("run"))
            {
                Console.WriteLine();
                DebugPrintUtil.PrintSeperator(lang["cli.sep.exec_result"]);
                Console.WriteLine();

                return ApplicationGenerator.RunApplication(result.OutputFile, Cont("debug"));
            }
            else
                return 0;
        }

        public static Dictionary<string, List<string>> ParseParameters(string[] argv, params (string Short, string Long)[] translator)
        {
            Dictionary<string, string> trans = translator.ToDictionary(x => x.Short, x => x.Long);
            Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>();

            foreach (string arg in argv)
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

        private static void PrintCopyrightHeader(ConsoleColor c, bool open = false)
        {
            string vstr = $"|     Core Library Version: {AutoItCoreLibrary.Module.LibraryVersion}, Interpreter Version: {Module.InterpreterVersion}".PadRight(112) + '|';

            PrintC($@"
+------------------------------------ C#/F# AutoIt3 Interpreter and Compiler -----------------------------------+
{vstr}
|                             AutoIt Interpreter : Copyright (c) Unknown6656, 2018{(DateTime.Now.Year > 2018 ? "-" + DateTime.Now.Year : "     ")}                         |
|                          Piglet Parser Library : Copyright (c) Dervall, 2012                                  |
|            ImageSharp Image Processing Library : Copyright (c) SixLabors, 2014-2018                           |
|                                SSH.NET Library : Copyright (c) NDepend, 2016                                  |
|                                                                                                               |
| Visit https://github.com/Unknown6656/AutoIt-Interpreter/blob/master/readme.md for an expanded documentation.  |
| Visit https://github.com/Unknown6656/AutoIt-Interpreter/blob/master/doc/usage.md for an usage reference.      |
{(open ? "" : "+---------------------------------------------------------------------------------------------------------------+")}".TrimEnd(), c);
        }

        private static void PrintUsage()
        {
            PrintCopyrightHeader(ConsoleColor.Cyan, true);

            $@"
|                                                                                                               |
|    Usage:                                                                                                     |
|    {ASM_FILE.Name,18} <options>                                                                               |
|                                                                                                               |
+-------------------+-----------------------+-------------------------------------------------------------------+
| OPTION (short)    | OPTION (long)         | Effect                                                            |
+-------------------+-----------------------+-------------------------------------------------------------------+
| -h, -?            | --help                | Displays this help menu.                                          |
|                   | --version             | Prints the interpreter's build version string.                    |
| -i=...            | --input=...           | The input .au3 AutoIt Script file.                     [required] |
| -o=...            | --output=...          | The output directory, to which the application will be written.   |
|                   |                       | If no output directory is given, the directory will be created in |
|                   |                       | the same directory as the input source file and named accordingly.|
| -c                | --clean-output        | Cleans-up the output folder before compiling.       [recommended] |
| -u                | --unsafe              | Allows unsafe code blocks, such as inline-C# etc.                 |
| -wall             | --warnings-as-errors  | Treats all warnings as errors (and all notes as warnings).        |
| -s=...            | --settings=...        | The path to the .json settings file.                              |
| -rs               | --reset-settings      | Resets the .json settings file to its defaults.                   |
| -v                | --verbose             | Displays verbose compilation output (instead of only the compiler |
|                   |                       | errors and warnings).                                             |
| -vv=...           | --visual=...          | Generates a bitmap with the compiled AutoIt++ code (including     |
|                   |                       | syntax highlightning and error/warning listing). The bitmap will  |
|                   |                       | be written to the given path.                                     |
| -q                | --quiet               | Displays no output (Returns only the exit code).                  |
| -l=....           | --lang=...            | Sets the language for the current session using the given language|
|                   |                       | code. (Doesn't affect the stored settings)                        |
| -ll               | --list-languages      | Displays a list of all available display languages.               |
| -k                | --keep-temp           | Keeps temporary generated code files.                             |
| -g                | --generate-always     | Generates always temporary code files. (Even if some fatal errors |
|                   |                       | have occured)                                                     |
| -d                | --debug               | Includes the debugging symbols into the resulting application.    |
| -r                | --run                 | Runs the compiled application after a successful build process.   |
| -t=...            | --target-system=...   | Compiles the application against the given target system.         |
|                   |                       | Possible values are:                                              |
|                   |                       |   win7, win8, win81, win10, win, linux, osx, android, centos, ol, |
|                   |                       |   debian, fedora, gentoo, opensuse, rhel, tizen, ubuntu, linuxmint|
|                   |                       | The default value for this system is '{new InterpreterOptions(null).Compatibility,5}'.                     |
| -a=...            | --architecture=...    | Compiles the application against the given target architecture.   |
|                   |                       | Possible values are:                                              |
|                   |                       |   x86, x64, arm, arm64                                            |
|                   |                       | The default value for this system is '{new InterpreterOptions(null).TargetArchitecture,5}'.                     |
| -kp=...           | --key-pair=...        | Signs the generated application with the given public/private key-|
|                   |                       | pair. Web paths are also accepted as source paths.                |
| -mef, -ms         | --msbuild-error-format| Displays the errors, notes and warnings using the MSBuild error   |
|                   |                       | string format.                                                    |
| -wup, -w          | --warm-up             | Warm-Up internal methods in the JIT before firing up the compiler.|
+-------------------+-----------------------+-------------------------------------------------------------------+
|                                                                                                               |
| Most options can be used as follows:                                                                          |
|     x                   simple argument                                                                       |
|     -x                  short variant without value                                                           |
|     -x=abc              short variant                                                                         |
|     --xy-z=abc          long variant                                                                          |
|     /xy-z:abc           alternative long variant                                                              |
|                                                                                                               |
+---------------------------------------------------------------------------------------------------------------+
| If the compiler's return code is positive, it indicates how many fatal compiler errors exist. Zero represents |
| a successful operation.                                                                                       |
|                                                                                                               |
|    Examples:                                                                                                  |
|    {ASM_FILE.Name,18} -i=myapp.au3 -t=/bin/myapp/ -c                                                          |
|    {ASM_FILE.Name,18} -i=/usr/scripts/my_script1.au3 -v -k -l=de -mef                                         |
|    {ASM_FILE.Name,18} -i=//192.168.0.2/C:/file.au3 -g -k -ms -t=win10 --unsafe                                |
|                                                                                                               |
| When compiling against an different runtime environment/system/architecture from the current one, be aware    |
| that the compiler might require the target runtime also to be installed, e.g. the Debian distribution of the  |
| .NET SDK should be installed on a Windows host, if the target machine also runs on Debian. This is usually    |
| handled automatically by the ROSLYN compiler engine, however, sometimes a manual installation is required.    |
+---------------------------------------------------------------------------------------------------------------+
".TrimStart().PrintC(ConsoleColor.Cyan);
        }

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

        private static void PrintVersion()
        {
            PrintCopyrightHeader(ConsoleColor.Cyan);

            $@"
         AutoIt++ Core Library version: {AutoItCoreLibrary.Module.LibraryVersion}
    AutoIt++ Expression Parser version: {AutoItExpressionParser.Module.Version}
          AutoIt++ Interpreter version: {Module.InterpreterVersion}".PrintC(ConsoleColor.Cyan);

            if (Module.GitHash != AutoItCoreLibrary.Module.GitHash || Module.GitHash != AutoItExpressionParser.Module.GitHash)
            {
                $"        AutoIt++ Core Library Git hash: {AutoItCoreLibrary.Module.GitHash}".PrintC(ConsoleColor.Cyan);
                $"   AutoIt++ Expression Parser Git hash: {AutoItExpressionParser.Module.GitHash}".PrintC(ConsoleColor.Cyan);
                $"         AutoIt++ Interpreter Git hash: {Module.GitHash}".PrintC(ConsoleColor.Cyan);
            }
            else
                $"                       Git commit hash: {Module.GitHash}".PrintC(ConsoleColor.Cyan);
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

    public static class DirectoryProvider
    {
        public static DirectoryInfo SettingsFolder { get; }


        static DirectoryProvider()
        {
            SettingsFolder = new DirectoryInfo(Win32.System == OS.Windows ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) : "~");
            SettingsFolder = SettingsFolder.CreateSubdirectory(".autoit3");
        }
    }

    public sealed class InterpreterSettings
    {
        public static string DefaultSettingsPath => $"{DirectoryProvider.SettingsFolder.FullName}/settings.json";

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
        public Compatibility Compatibility { set; get; } = Win32.System == OS.Windows ? Compatibility.win
                                                         : Win32.System == OS.Linux ? Compatibility.linux : Compatibility.osx;
        public Architecture TargetArchitecture { set; get; } = RuntimeInformation.OSArchitecture;
        public Dictionary<string, List<string>> RawArguments { set; get; }
        public List<Assembly> Dependencies { get; } = new List<Assembly>();
        public bool IncludeDebugSymbols { get; set; }
        public bool TreatWarningsAsErrors { get; set; }
        public bool CleanTargetFolder { set; get; }
        public string RawCommandLine { set; get; }
        public string TargetDirectory { set; get; }
        public string VisualOutputPath { get; set; }
        public string KeyPairPath { get; set; }
        public bool AllowUnsafeCode { set; get; }
        public bool UseMSBuildErrorOutput { set; get; }
        public bool DeleteTempFilesAfterSuccess { set; get; } = true;
        public bool GenerateCodeEvenWithErrors { set; get; }
        public bool UseVerboseOutput { set; get; }
        public bool UseJITWarmup { set; get; }
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
