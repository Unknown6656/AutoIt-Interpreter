using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System;

using CommandLine.Text;
using CommandLine;

using Newtonsoft.Json;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.AutoIt3.Localization;
using Unknown6656.Controls.Console;
using Unknown6656.Imaging;

namespace Unknown6656.AutoIt3
{
    public sealed class CommandLineOptions
    {
        [Option('B', "nobanner", Default = false, HelpText = "Suppress the banner.")]
        public bool HideBanner { get; }

        [Option('N', "no-plugins", Default = false, HelpText = "Prevent the loading of interpreter plugins.")]
        public bool DontLoadPlugins { get; }

        [Option('s', "strict", Default = false, HelpText = "Support only strict Au3-features and -syntax.")]
        public bool StrictMode { get; }

        [Option('e', "ignore-errors", Default = false, HelpText = "Ignores syntax and evaluation errors during parsing (unsafe!).")]
        public bool IgnoreErrors { get; }

        [Option('v', "verbosity", Default = Verbosity.n, HelpText = "The interpreter's verbosity level. (q=quiet, n=normal, v=verbose)")]
        public Verbosity Verbosity { get; }

        [Option('l', "lang", Default = "en", HelpText = "The CLI language code to be used by the compiler.")]
        public string Language { get; }

        [Value(0, HelpText = "The file path to the AutoIt-3 srcript.", Required = true)]
        public string FilePath { get; }


        public CommandLineOptions(bool hideBanner, bool dontLoadPlugins, bool strictMode, bool ignoreErrors, Verbosity verbosity, string language, string filePath)
        {
            HideBanner = hideBanner;
            DontLoadPlugins = dontLoadPlugins;
            StrictMode = strictMode;
            IgnoreErrors = ignoreErrors;
            Verbosity = verbosity;
            Language = language;
            FilePath = filePath;
        }
    }

    public static class Program
    {
        public static readonly FileInfo ASM = new FileInfo(typeof(Program).Assembly.Location);
        public static readonly DirectoryInfo ASM_DIR = ASM.Directory!;
        public static readonly DirectoryInfo PLUGIN_DIR = ASM_DIR.CreateSubdirectory("plugins/");
        public static readonly DirectoryInfo LANG_DIR = ASM_DIR.CreateSubdirectory("lang/");
        public static readonly DirectoryInfo INCLUDE_DIR = ASM_DIR.CreateSubdirectory("include/");

        private static readonly ConcurrentQueue<Action> _print_queue = new ConcurrentQueue<Action>();
        private static volatile bool _isrunning = true;
        private static volatile bool _finished = false;
#nullable disable
        public static LanguagePack CurrentLanguage { get; private set; }
        public static CommandLineOptions CommandLineOptions { get; private set; }
#nullable enable


        public static int Main(string[] argv)
        {
            ConsoleState state = ConsoleExtensions.SaveConsoleState();
            using Task printer = Task.Factory.StartNew(PrinterTask);
            int code = 0;

            try
            {
                Console.WindowWidth = Math.Max(Console.WindowWidth, 120);
                Console.BufferWidth = Math.Max(Console.BufferWidth, Console.WindowWidth);
                Console.OutputEncoding = Encoding.Unicode;
                Console.InputEncoding = Encoding.Unicode;
                // Console.BackgroundColor = ConsoleColor.Black;
                ConsoleExtensions.RGBForegroundColor = RGBAColor.White;

                using Parser parser = new Parser(p => p.HelpWriter = null);
                ParserResult<CommandLineOptions> result = parser.ParseArguments<CommandLineOptions>(argv);

                result.WithNotParsed(err =>
                {
                    HelpText help = HelpText.AutoBuild(result, h =>
                    {
                        h.AdditionalNewLineAfterOption = false;
                        h.MaximumDisplayWidth = 119;
                        h.Heading = $"AutoIt3 Interpreter v.{__module__.InterpreterVersion} ({__module__.GitHash})";
                        h.Copyright = __module__.Copyright;
                        h.AddDashesToOption = true;
                        h.AutoHelp = true;
                        h.AutoVersion = true;

                        return HelpText.DefaultParsingErrorsHandler(result, h);
                    }, e => e);

                    if (err.FirstOrDefault() is UnknownOptionError { StopsProcessing: false, Token: "version" })
                    {
                        Console.WriteLine(help.Heading);
                        Console.WriteLine(help.Copyright);
                    }
                    else
                    {
                        Console.WriteLine(help);

                        code = -1;
                    }
                });
                result.WithParsed(opt =>
                {
                    CommandLineOptions = opt;

                    if (LanguageLoader.LanguagePacks.TryGetValue(opt.Language.ToLowerInvariant(), out LanguagePack? lang))
                        CurrentLanguage = lang;
                    else
                    {
                        code = -1;
                        PrintError($"Unknown language pack '{opt.Language}'. Available languages: '{string.Join("', '", LanguageLoader.LanguagePacks.Values.Select(p => p.LanguageCode))}'");

                        return;
                    }

                    PrintBanner();
                    PrintDebugMessage(JsonConvert.SerializeObject(opt));
                    PrintInterpreterMessage(CurrentLanguage["general.langpack_found", LanguageLoader.LanguagePacks.Count]);
                    PrintInterpreterMessage(CurrentLanguage["general.loaded_langpack", CurrentLanguage]);

                    InterpreterResult result = Interpreter.Run(opt);

                    if (result.OptionalError is InterpreterError err)
                        PrintError($"{CurrentLanguage["error.error_in", err.Location ?? SourceLocation.Unknown]}:\n    {err.Message}");

                    code = result.ProgramExitCode;
                });
            }
            catch (Exception ex)
            // when (!Debugger.IsAttached)
            {
                code = ex.HResult;

                PrintException(ex);
            }
            finally
            {
                _isrunning = false;

                ConsoleExtensions.RestoreConsoleState(state);
            }

            while (!_finished)
                printer.Wait();

            return code;
        }

        private static async Task PrinterTask()
        {
            while (_isrunning)
                if (_print_queue.TryDequeue(out Action? func))
                    func();
                else
                    await Task.Delay(50);

            while (_print_queue.TryDequeue(out Action? func))
                func();

            _finished = true;
        }

        private static void SubmitPrint(Verbosity min_lvl, string prefix, string msg, bool from_script)
        {
            if (CommandLineOptions.Verbosity < min_lvl)
                return;

            DateTime now = DateTime.Now;

            _print_queue.Enqueue(delegate
            {
                ConsoleExtensions.RGBForegroundColor = RGBAColor.DarkGray;
                Console.Write('[');
                ConsoleExtensions.RGBForegroundColor = RGBAColor.Gray;
                Console.Write(now.ToString("HH:mm:ss.fff"));
                ConsoleExtensions.RGBForegroundColor = RGBAColor.DarkGray;
                Console.Write("][");
                ConsoleExtensions.RGBForegroundColor = from_script ? RGBAColor.PaleTurquoise : RGBAColor.Cyan;
                Console.Write(prefix);
                ConsoleExtensions.RGBForegroundColor = RGBAColor.DarkGray;
                Console.Write("]  ");
                ConsoleExtensions.RGBForegroundColor = from_script ? RGBAColor.White : RGBAColor.Cyan;
                Console.WriteLine(msg);
                ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            });
        }

        internal static void PrintInterpreterMessage(string message) => SubmitPrint(Verbosity.n, "Interpreter", message, false);

        internal static void PrintDebugMessage(string message) => SubmitPrint(Verbosity.v, "Interpreter-Debug", message, false);

        internal static void PrintScriptMessage(FileInfo? script, string message)
        {
            if (CommandLineOptions.Verbosity < Verbosity.n)
                Console.Write(message);
            else
                SubmitPrint(Verbosity.n, script?.Name ?? "<unknown>", message.Trim(), true);
        }

        internal static void PrintException(this Exception? ex)
        {
            StringBuilder sb = new StringBuilder();

            while (ex is { })
            {
                sb.Insert(0, $"[{ex.GetType()}] \"{ex.Message}\":\n{ex.StackTrace}\n");
                ex = ex.InnerException;
            }

            PrintError(sb.ToString());
        }

        internal static void PrintError(this string message) => _print_queue.Enqueue(delegate
        {
            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            Console.WriteLine(new string('_', Console.WindowWidth - 1));
            ConsoleExtensions.RGBForegroundColor = RGBAColor.Orange;

            if (!CommandLineOptions.HideBanner && CommandLineOptions.Verbosity > Verbosity.n)
            {
                Console.WriteLine(@"
                               ____
                       __,-~~/~    `---.
                     _/_,---(      ,    )
                 __ /        <    /   )  \___
  - ------===;;;'====------------------===;;;===----- -  -
                    \/  ~:~'~^~'~ ~\~'~)~^/
                    (_ (   \  (     >    \)
                     \_( _ <         >_>'
                        ~ `-i' ::>|--`'
                            I;|.|.|
                            | |: :|`
                         .-=||  | |=-.       ___  ____  ____  __  ___  __
                         `-=#$%&%$#=-'      / _ )/ __ \/ __ \/  |/  / / /
                           .| ;  :|        / _  / /_/ / /_/ / /|_/ / /_/
                          (`^':`-'.)      /____/\____/\____/_/  /_/ (_)
______________________.,-#%&$@#&@%#&#~,.___________________________________");
                ConsoleExtensions.RGBForegroundColor = RGBAColor.Yellow;
                Console.WriteLine("            AW SHIT -- THE INTERPRETER JUST BLEW UP!\n");
            }
            else
                Console.WriteLine();

            ConsoleExtensions.RGBForegroundColor = RGBAColor.Salmon;
            Console.WriteLine(message.TrimEnd());
            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            Console.WriteLine(new string('_', Console.WindowWidth - 1));
        });

        private static void PrintBanner()
        {
            if (CommandLineOptions.HideBanner || CommandLineOptions.Verbosity < Verbosity.n)
                return;
            else
                _print_queue.Enqueue(delegate
                {
                    ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
                    Console.WriteLine($@"
                        _       _____ _   ____
             /\        | |     |_   _| | |___ \
            /  \  _   _| |_ ___  | | | |_  __) |
           / /\ \| | | | __/ _ \ | | | __||__ <
          / ____ \ |_| | || (_) || |_| |_ ___) |
         /_/    \_\__,_|\__\___/_____|\__|____/
  _____       _                           _
 |_   _|     | |                         | |
   | |  _ __ | |_ ___ _ __ _ __  _ __ ___| |_ ___ _ __
   | | | '_ \| __/ _ \ '__| '_ \| '__/ _ \ __/ _ \ '__|
  _| |_| | | | ||  __/ |  | |_) | | |  __/ ||  __/ |
 |_____|_| |_|\__\___|_|  | .__/|_|  \___|\__\___|_|
                          | |
                          |_|  {CurrentLanguage["banner.written_by", __module__.Author, __module__.Year]}
{CurrentLanguage["banner.version"]} v.{__module__.InterpreterVersion} ({__module__.GitHash})
");
                    ConsoleExtensions.RGBForegroundColor = RGBAColor.Crimson;
                    Console.Write("    ");
                    ConsoleExtensions.WriteUnderlined("WARNING!");
                    ConsoleExtensions.RGBForegroundColor = RGBAColor.Salmon;
                    Console.WriteLine(" This may panic your CPU.\n");
                });
        }
    }

    public enum Verbosity
    {
        q,
        n,
        v,
    }
}
