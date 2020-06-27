using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
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
using Unknown6656.Common;

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

        [Option('t', "telemetry", Default = false, HelpText = "Prints the interpreter telemetry. A verbosity level of 'n' or 'v' will automatically set this flag.")]
        public bool PrintTelemetry { get; }

        [Option('v', "verbosity", Default = Verbosity.n, HelpText = "The interpreter's verbosity level. (q=quiet, n=normal, v=verbose)")]
        public Verbosity Verbosity { get; }

        [Option('l', "lang", Default = "en", HelpText = "The CLI language code to be used by the compiler.")]
        public string Language { get; }

        [Value(0, HelpText = "The file path to the AutoIt-3 srcript.", Required = true)]
        public string FilePath { get; }


        public CommandLineOptions(bool hideBanner, bool dontLoadPlugins, bool strictMode, bool ignoreErrors, bool printTelemetry, Verbosity verbosity, string language, string filePath)
        {
            HideBanner = hideBanner;
            DontLoadPlugins = dontLoadPlugins;
            PrintTelemetry = printTelemetry;
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
        public static CommandLineOptions CommandLineOptions { get; private set; }
        public static LanguagePack CurrentLanguage { get; private set; }
#nullable enable
        public static Telemetry Telemetry { get; } = new Telemetry();


        public static int Main(string[] argv)
        {
            Stopwatch sw = new Stopwatch();

            sw.Start();

            ConsoleState state = ConsoleExtensions.SaveConsoleState();
            using Task printer = Task.Factory.StartNew(PrinterTask);
            int code = 0;

            Telemetry.Measure(TelemetryCategory.ProgramRuntime, delegate
            {
                try
                {
                    Console.WindowWidth = Math.Max(Console.WindowWidth, 100);
                    Console.BufferWidth = Math.Max(Console.BufferWidth, Console.WindowWidth);
                    Console.OutputEncoding = Encoding.Unicode;
                    Console.InputEncoding = Encoding.Unicode;
                    ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
                    // Console.BackgroundColor = ConsoleColor.Black;
                    // Console.Clear();

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

                        if (opt.Verbosity > Verbosity.q)
                        {
                            Console.WindowWidth = Math.Max(Console.WindowWidth, 180);
                            Console.BufferWidth = Math.Max(Console.BufferWidth, Console.WindowWidth);
                        }

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
                        PrintDebugMessage("Loading interpreter ...");

                        using Interpreter interpreter = Telemetry.Measure(TelemetryCategory.InterpreterInitialization, () => new Interpreter(opt, Telemetry));

                        PrintDebugMessage($"Interpreter loaded. Running script '{opt.FilePath}' ...");

                        InterpreterResult result = Telemetry.Measure(TelemetryCategory.InterpreterRuntime, interpreter.Run);

                        if (result.OptionalError is InterpreterError err)
                            PrintError($"{CurrentLanguage["error.error_in", err.Location ?? SourceLocation.Unknown]}:\n    {err.Message}");

                        code = result.ProgramExitCode;
                    });
                }
                catch (Exception ex)
                // when (!Debugger.IsAttached)
                {
                    Telemetry.Measure(TelemetryCategory.Exceptions, delegate
                    {
                        code = ex.HResult;

                        PrintException(ex);
                    });
                }
            });

            if (CommandLineOptions is { Verbosity: > Verbosity.q } or { PrintTelemetry: true })
            {
                while (_print_queue.Count > 0)
                    Thread.Sleep(100);

                sw.Stop();
                Telemetry.Submit(TelemetryCategory.ProgramRuntimeAndPrinting, sw.ElapsedTicks);

                PrintTelemetry(Telemetry);
            }

            _isrunning = false;

            while (!_finished)
                printer.Wait();

            ConsoleExtensions.RestoreConsoleState(state);

            return code;
        }

        private static async Task PrinterTask()
        {
            while (_isrunning)
                if (_print_queue.TryDequeue(out Action? func))
                    Telemetry.Measure(TelemetryCategory.Printing, func);
                else
                    await Task.Delay(50);

            while (_print_queue.TryDequeue(out Action? func))
                Telemetry.Measure(TelemetryCategory.Printing, func);

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

        public static void PrintInterpreterMessage(string message) => SubmitPrint(Verbosity.n, "Interpreter", message, false);

        public static void PrintDebugMessage(string message) => SubmitPrint(Verbosity.v, "Interpreter-Debug", message, false);

        public static void PrintScriptMessage(FileInfo? script, string message) => Telemetry.Measure(TelemetryCategory.ScriptConsoleOut, delegate
        {
            if (CommandLineOptions.Verbosity < Verbosity.n)
                Console.Write(message);
            else
                SubmitPrint(Verbosity.n, script?.Name ?? "<unknown>", message.Trim(), true);
        });

        public static void PrintException(this Exception? ex)
        {
            StringBuilder sb = new StringBuilder();

            while (ex is { })
            {
                sb.Insert(0, $"[{ex.GetType()}] \"{ex.Message}\":\n{ex.StackTrace}\n");
                ex = ex.InnerException;
            }

            PrintError(sb.ToString());
        }

        public static void PrintError(this string message) => _print_queue.Enqueue(delegate
        {
            bool extensive = !CommandLineOptions.HideBanner && CommandLineOptions.Verbosity > Verbosity.n;

            if (!extensive && Console.CursorLeft > 0)
                Console.WriteLine();

            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            Console.WriteLine(new string('_', Console.WindowWidth - 1));
            ConsoleExtensions.RGBForegroundColor = RGBAColor.Orange;

            if (extensive)
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

        public static void PrintTelemetry(Telemetry telemetry) => _print_queue.Enqueue(delegate
        {
            int width = Math.Min(Console.WindowWidth, Console.BufferWidth) - 1;

            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            Console.WriteLine(new string('_', width));
            ConsoleExtensions.RGBForegroundColor = RGBAColor.Yellow;
            Console.WriteLine("\n\t\tTELEMETRY REPORT");

            RGBAColor col_table = RGBAColor.LightGray;
            RGBAColor col_text = RGBAColor.White;
            RGBAColor col_backg = RGBAColor.DarkSlateGray;
            RGBAColor col_hotpath = RGBAColor.IndianRed;

            string[] headers = {
                "Path",
                "Count",
                "Total Time",
                "Average Time",
                "Minimum Time",
                "Maximum Time",
                "Time % of Parent",
                "Time % of Total",
            };
            List<(string[] cells, TelemetryNode node)> rows = new();
            void traverse(TelemetryNode node, string prefix = "", bool last = true)
            {
                rows.Add((new[]
                {
                    prefix.Length switch
                    {
                        0 => " ·─ " + node.Name,
                        _ => string.Concat(prefix.Select(c => c is 'x' ? " │  " : "    ").Append(last ? " └─ " : " ├─ ").Append(node.Name))
                    },
                    node.Timings.Length.ToString(),
                    node.Total.ToString(),
                    node.Average.ToString(),
                    node.Min.ToString(),
                    node.Max.ToString(),
                    $"{node.PercentageOfParent * 100,9:F5} %",
                    $"{node.PercentageOfTotal * 100,9:F5} %",
                }, node));

                TelemetryNode[] children = node.Children.OrderByDescending(c => c.PercentageOfTotal).ToArray();

                for (int i = 0; i < children.Length; i++)
                {
                    TelemetryNode child = children[i];

                    traverse(child, prefix + (last ? ' ' : 'x'), i == children.Length - 1);
                }
            }

            traverse(TelemetryNode.FromTelemetry(telemetry));

            int[] widths = headers.ToArray(h => h.Length + 2);

            foreach (string[] cells in rows.Select(r => r.cells))
                for (int i = 0; i < widths.Length; i++)
                    widths[i] = Math.Max(widths[i], cells[i].Length + 2);

            ConsoleExtensions.RGBForegroundColor = col_table;

            for (int i = 0, l = widths.Length; i < l; i++)
            {
                if (i == 0)
                {
                    ConsoleExtensions.WriteVertical("┌│├");
                    Console.CursorTop -= 2;
                }

                int yoffs = Console.CursorTop;
                int xoffs = Console.CursorLeft;

                Console.Write(new string('─', widths[i]));
                ConsoleExtensions.RGBForegroundColor = col_text;
                ConsoleExtensions.Write($" {headers[i].PadRight(widths[i] - 2)} ", (xoffs, yoffs + 1));
                ConsoleExtensions.RGBForegroundColor = col_table;
                ConsoleExtensions.Write(new string('─', widths[i]), (xoffs, yoffs + 2));
                ConsoleExtensions.WriteVertical(i == l - 1 ? "┐│┤" : "┬│┼", (xoffs + widths[i], yoffs));
                Console.CursorTop = yoffs;
                
                if (i == l - 1)
                {
                    Console.CursorTop += 2;
                    Console.WriteLine();
                }
            }

            foreach ((string[] cells, TelemetryNode node) in rows)
            {
                for (int i = 0, l = cells.Length; i < l; i++)
                {
                    ConsoleExtensions.RGBForegroundColor = col_table;

                    if (i == 0)
                        Console.Write('│');
                    
                    ConsoleExtensions.RGBForegroundColor = node.IsHot ? col_hotpath : col_text;

                    string cell = cells[i];

                    if (i == 0)
                    {
                        Console.Write(' ' + cell);
                        ConsoleExtensions.RGBForegroundColor = col_backg;
                        Console.Write(new string('─', widths[i] - cell.Length - 1));
                    }
                    else
                    {
                        int xoffs = Console.CursorLeft;

                        ConsoleExtensions.RGBForegroundColor = col_backg;
                        Console.Write(new string('─', widths[i]));
                        ConsoleExtensions.RGBForegroundColor = node.IsHot ? col_hotpath : col_text;
                        Console.CursorLeft = xoffs + 1;

                        for (int j = 0, k = Math.Min(widths[i] - 2, cell.Length); j < k; ++j)
                            if (char.IsWhiteSpace(cell[j]))
                                ++Console.CursorLeft;
                            else
                                Console.Write(cell[j]);

                       Console.CursorLeft = xoffs + widths[i];
                    }

                    ConsoleExtensions.RGBForegroundColor = col_table;
                    Console.Write('│');
                }

                Console.WriteLine();
            }

            ConsoleExtensions.RGBForegroundColor = col_table;

            for (int i = 0, l = widths.Length; i < l; i++)
            {
                if (i == 0)
                    Console.Write('└');

                Console.Write(new string('─', widths[i]));
                Console.Write(i == l - 1 ? '┘' : '┴');
            }

            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            Console.WriteLine();
            Console.WriteLine(new string('_', width));
        });

        public static void PrintBanner()
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
