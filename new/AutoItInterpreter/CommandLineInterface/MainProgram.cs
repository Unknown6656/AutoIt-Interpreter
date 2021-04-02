using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Text;
using System.IO;
using System;

using CommandLine.Text;
using CommandLine;

using Octokit;

using Unknown6656.AutoIt3.Localization;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;

using Unknown6656.Mathematics.Cryptography;
using Unknown6656.Controls.Console;
using Unknown6656.Imaging;
using Unknown6656.Common;
using Unknown6656.IO;

using OS = Unknown6656.AutoIt3.Runtime.Native.OS;
using CLParser = CommandLine.Parser;

[assembly: AssemblyUsage(@"
  Run the interpreter quietly (only print the script's output):
      autoit3 ~/Documents/my_script.au3
      autoit3 C:\User\Public\Script              (you can also omit the file extension)
  
  Run the interpreter in telemetry/full debugging mode:
      autoit3 -t ~/Documents/my_script.au3
      autoit3 -v ~/Documents/my_script.au3
  
  Run a script which is not on the local machine:
      autoit3 ""\\192.168.0.1\Public Documents\My Script.au3""
      autoit3 https://example.com/my-script.au3
      autoit3 ftp://username:password@example.com/path/to/script.au3
      autoit3 ssh://username:password@example.com/~/Documents/my_script.au3
      autoit3 scp://username:password@192.168.0.100:22/script.au3
  
  Run the interpreter in interactive mode:
      autoit3 -m interactive

  Run the interpreter in view-only mode:
      autoit3 -m view ~/Documents/my_script.au3

  Use an other display language than English for the interpreter:
      autoit3 -l fr C:\User\Public\Script.au3

  Visit " + "\x1b[4m" + __module__.RepositoryURL + "/wiki/Usage/\x1b[24m" + @" for more information.

-------------------------------------------------------------------------------

COMMAND LINE OPTIONS:")]

namespace Unknown6656.AutoIt3.CLI
{
    /// <summary>
    /// Represents a structure of command line options for the AutoIt interpreter.
    /// </summary>
    public sealed class CommandLineOptions
    {
        [Option('m', "mode", Default = ExecutionMode.normal, HelpText = "The program's execution mode. Possible values are 'normal' (n), 'interactive' (i), 'view' (v), and 'tidy' (t). The default value is 'normal'. This will run the specified script. The value 'view' indicates that the interpreter shall only display a syntax highlighted version of the script. The value 'interactive' starts the interactive AutoIt shell. The value 'tidy' formats the speicified script file.")]
        public ExecutionMode ProgramExecutionMode { get; set; } = ExecutionMode.normal;

        [Option('N', "no-plugins", Default = false, HelpText = "Prevents the loading of interpreter plugins/extensions.")]
        public bool DontLoadPlugins { set; get; } = false;

        [Option('s', "strict", Default = false, HelpText = "Indicates that only strict Au3-features and -syntaxes should be be supported (Extensions to the AutoIt language will be interpreted as errors).")]
        public bool StrictMode { set; get; } = false;

        [Option('e', "ignore-errors", Default = false, HelpText = "Ignores syntax and evaluation errors during parsing (unsafe!). This can lead to undefined and non-deterministic behaviour.")]
        public bool IgnoreErrors { set; get; } = false;

        [Option('t', "telemetry", Default = false, HelpText = "Prints the interpreter telemetry. A verbosity level of 'n' or 'v' will automatically set this flag.  NOTE: All telemetry data \x1b[4mstays\x1b[24m on this machine contrary to what this option might suggest. \x1b[4mNo part\x1b[24m of the telemetry will be uploaded to an external (web)server.")]
        public bool PrintTelemetry { set; get; } = false;

        [Option('v', "verbose", Default = false, HelpText = "Indicates that the interpreter should also print debug messages.")]
        public bool Verbose { set; get; } = false;

        [Option('u', "check-for-update", Default = UpdaterMode.release, HelpText = "Specifies how the interpreter should check for software updates. Possible values are 'release' (default), 'beta', and 'none'. 'none' indicates that no updates shall be downloaded; 'beta' indicates that beta-releases should be included in the search for the newest update. Updates will be downloaded from the GitHub repository (\x1b[4m" + __module__.RepositoryURL + "/releases\x1b[24m).")]
        public UpdaterMode UpdaterMode { set; get; } = UpdaterMode.release;

        [Option('l', "lang", Default = "en", HelpText = "The CLI language code to be used by the compiler shell. The default value is 'en' for the English language.")]
        public string Language { set; get; } = "en";

        [Value(0, HelpText = "The AutoIt-3 script path. This can be a local file or a web resource (HTTP/HTTPS/SMB/FTP/SFTP/SCP/SSH/...).")]
        public string? FilePath { set; get; } = null;

#pragma warning disable CA1819 // Properties should not return arrays
        public string[] ScriptArguments { get; set; } = Array.Empty<string>();
#pragma warning restore CA1819


        // TODO : -C --no-com  "Disable the COM service connector (Windows only)."
        // TODO : -G --no-gui  "Disable the GUI service connector (implied by --strict)."


        /// <inheritdoc/>
        public override string ToString() => CLParser.Default.FormatCommandLine(this);
    }

    /// <summary>
    /// The module containing the AutoIt Interpreter's main entry point.
    /// <para/>
    /// <b>NOTE:</b> The .NET runtime does not actually call this class directly. The actual entry point resides in the file "../EntryPoint.cs"
    /// </summary>
    public static class MainProgram
    {
        public static readonly Assembly ASM = typeof(MainProgram).Assembly;
        public static readonly FileInfo ASM_FILE = new(ASM.Location);
        public static readonly DirectoryInfo ASM_DIR = ASM_FILE.Directory!;
        public static readonly DirectoryInfo PLUGIN_DIR = ASM_DIR.CreateSubdirectory("plugins/");
        public static readonly DirectoryInfo LANG_DIR = ASM_DIR.CreateSubdirectory("lang/");
        public static readonly DirectoryInfo INCLUDE_DIR = ASM_DIR.CreateSubdirectory("include/");
        public static readonly FileInfo WINAPI_CONNECTOR = new(Path.Combine(ASM_DIR.FullName, "autoit3.win32apiserver.exe"));
        public static readonly FileInfo COM_CONNECTOR = new(Path.Combine(ASM_DIR.FullName, "autoit3.comserver.exe"));
        public static readonly FileInfo GUI_CONNECTOR = new(Path.Combine(ASM_DIR.FullName, "autoit3.guiserver.dll"));
        public static readonly FileInfo UPDATER = new(Path.Combine(ASM_DIR.FullName, "autoit3.updater.dll"));

        internal static readonly RGBAColor COLOR_TIMESTAMP = RGBAColor.Gray;
        internal static readonly RGBAColor COLOR_PREFIX_SCRIPT = RGBAColor.Cyan;
        internal static readonly RGBAColor COLOR_PREFIX_DEBUG = RGBAColor.PaleTurquoise;
        internal static readonly RGBAColor COLOR_SCRIPT = RGBAColor.White;
        internal static readonly RGBAColor COLOR_DEBUG = RGBAColor.LightSteelBlue;
        internal static readonly RGBAColor COLOR_ERROR = RGBAColor.Salmon;
        internal static readonly RGBAColor COLOR_WARNING = RGBAColor.Orange;

        private static readonly ConcurrentQueue<Action> _print_queue = new();
        private static volatile bool _isrunning = true;
        private static volatile bool _finished;

#nullable disable
        public static string[] RawCMDLineArguments { get; private set; }

        public static CommandLineOptions CommandLineOptions { get; private set; }
#nullable enable
        public static InteractiveShell? InteractiveShell { get; private set; }

        public static LanguageLoader LanguageLoader { get; } = new LanguageLoader();

        public static Telemetry Telemetry { get; } = new Telemetry();

        public static bool PausePrinter { get; set; }


        // TODO : clean up 'Start'-method

        /// <summary>
        /// The main entry point for this application.
        /// </summary>
        /// <param name="argv">Command line arguments.</param>
        /// <returns>Return/exit code.</returns>
        public static int Start(string[] argv)
        {
            RawCMDLineArguments = argv;

            Stopwatch sw = new();

            sw.Start();

            Console.CancelKeyPress += (_, e) =>
            {
                Interpreter[] instances = Interpreter.ActiveInstances;
                List<FunctionReturnValue> return_values = new();

                e.Cancel = instances.Length > 0;

                foreach (Interpreter interpreter in instances)
                {
                    interpreter.ExitMethod = InterpreterExitMethod.ByClick;
                    return_values.Add(interpreter.Stop(-1));
                }

                // TODO : exit?
                // TODO : print fatal error
            };

            ConsoleState state = ConsoleExtensions.SaveConsoleState();

            using Task printer_task = Task.Run(PrinterTask);
            using Task telemetry_task = Task.Run(Telemetry.StartPerformanceMonitorAsync);
            bool print_telemetry = false;
            int code = 0;

            Telemetry.Measure(TelemetryCategory.ProgramRuntime, delegate
            {
                try
                {
                    NativeInterop.DoPlatformDependent(delegate
                    {
#pragma warning disable CA1416 // Validate platform compatibility
                        Console.WindowWidth = Math.Max(Console.WindowWidth, 100);
                        Console.BufferWidth = Math.Max(Console.BufferWidth, Console.WindowWidth);
#pragma warning restore CA1416
                    }, OS.Windows);

                    // Console.OutputEncoding = Encoding.Unicode;
                    // Console.InputEncoding = Encoding.Unicode;
                    Console.ResetColor();
                    ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
                    // Console.BackgroundColor = ConsoleColor.Black;
                    // Console.Clear();

                    string[]? script_args = null;

                    Telemetry.Measure(TelemetryCategory.ParseCommandLine, delegate
                    {
                        if (argv.WithIndex().SkipWhile(i => i.Item != "--").FirstOrDefault() is { Index: int idx, Item: not null })
                        {
                            script_args = argv[(idx + 1)..];
                            argv = argv[..idx];
                        }

                        using CLParser parser = new(p =>
                        {
                            p.HelpWriter = null;
                            p.IgnoreUnknownArguments = false;
                        });

                        ParserResult<CommandLineOptions> result = parser.ParseArguments<CommandLineOptions>(argv);

                        result.WithNotParsed(err =>
                        {
                            HandleParserError(result, err, ref code, ref print_telemetry);
                            print_telemetry ^= true;
                        });

                        return result;
                    }).WithParsed(opt =>
                    {
                        if (opt.ProgramExecutionMode is ExecutionMode.normal)
                            print_telemetry = true;
                        else
                        {
                            opt.Verbose = false;
                            opt.PrintTelemetry = false;
                            opt.DontLoadPlugins = opt.ProgramExecutionMode is ExecutionMode.view or ExecutionMode.tidy;
                        }

                        opt.ScriptArguments = script_args ?? opt.ScriptArguments;
                        CommandLineOptions = opt;

                        Telemetry.Measure(TelemetryCategory.LoadLanguage, delegate
                        {
                            LanguageLoader.LoadLanguagePacksFromDirectory(LANG_DIR);
                            LanguageLoader.TrySetCurrentLanguagePack(opt.Language);
                        });

                        LanguagePack? lang = LanguageLoader.CurrentLanguage;

                        if (lang is null)
                        {
                            code = -1;
                            PrintError($"Unknown language pack '{opt.Language}'. Available languages: '{string.Join("', '", LanguageLoader.LoadedLanguageCodes)}'");

                            return;
                        }

                        Task<bool> update_task = UpdateTask();

                        PrintBanner();
                        PrintDebugMessage(opt.ToString());
                        PrintfDebugMessage("debug.langpack_found", LanguageLoader.LoadedLanguageCodes.Length);
                        PrintfDebugMessage("debug.loaded_langpack", lang);
                        PrintfDebugMessage("debug.interpreter_loading");

                        using Interpreter interpreter = Telemetry.Measure(TelemetryCategory.InterpreterInitialization, () => new Interpreter(opt, Telemetry, LanguageLoader));

                        if (update_task.GetAwaiter().GetResult())
                            code = 0; // update has been performed
                        else if (opt.ProgramExecutionMode is ExecutionMode.interactive)
                        {
                            using InteractiveShell shell = new(interpreter);

                            if (shell.Initialize())
                            {
                                InteractiveShell = shell;

                                shell.Run();
                            }

                            InteractiveShell = null;

                            PrintfDebugMessage("error.not_yet_implemented", opt.ProgramExecutionMode);
                        }
                        else if (opt.FilePath is string path)
                        {
                            Union<InterpreterError, ScannedScript> resolved = interpreter.ScriptScanner.ScanScriptFile(SourceLocation.Unknown, path, false);
                            InterpreterError? error = null;

                            if (resolved.Is(out ScannedScript? script))
                            {
                                PrintfDebugMessage("debug.interpreter_loaded", path);

                                if (opt.ProgramExecutionMode is ExecutionMode.view)
                                {
                                    ScriptToken[] tokens = ScriptVisualizer.TokenizeScript(script);

                                    Console.WriteLine(tokens.ConvertToVT100(true));
                                }
                                else if (opt.ProgramExecutionMode is ExecutionMode.tidy)
                                {
                                    // TODO : clean up the script

                                    throw new NotImplementedException();
                                }
                                else if (opt.ProgramExecutionMode is ExecutionMode.normal)
                                {
                                    FunctionReturnValue result = Telemetry.Measure(TelemetryCategory.InterpreterRuntime, () => interpreter.Run(InterpreterRunContext.Regular));

                                    result.IsFatal(out error);

                                    if (!result.IsError(out code))
                                        code = 0;
                                }
                            }
                            else
                                error = resolved.As<InterpreterError>();

                            if (error is InterpreterError err)
                                PrintError($"{lang["error.error_in", err.Location ?? SourceLocation.Unknown]}:\n    {err.Message}");
                        }
                        else
                            PrintError(lang["error.no_script_path_provided"]);
                    });
                }
                catch (Exception ex)
                when (!Debugger.IsAttached)
                {
                    Telemetry.Measure(TelemetryCategory.Exceptions, delegate
                    {
                        code = ex.HResult;

                        PrintException(ex);
                    });
                }
            });

            while (_print_queue.Count > 0)
                Thread.Sleep(100);

            sw.Stop();
            Telemetry.SubmitTimings(TelemetryCategory.ProgramRuntimeAndPrinting, sw.Elapsed);
            Telemetry.StopPerformanceMonitor();
            telemetry_task.Wait();

            if (print_telemetry)
                PrintReturnCodeAndTelemetry(code, Telemetry);

            _isrunning = false;

            while (!_finished)
                printer_task.Wait();

            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            ConsoleExtensions.RestoreConsoleState(state);

            return code;
        }

        private static void HandleParserError(ParserResult<CommandLineOptions> result, IEnumerable<Error> err, ref int code, ref bool help_requested)
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
                h.AddNewLineBetweenHelpSections = true;
                h.AddEnumValuesToHelpText = false;

                return HelpText.DefaultParsingErrorsHandler(result, h);
            }, e => e);

            if (err.FirstOrDefault() is VersionRequestedError or UnknownOptionError { StopsProcessing: false, Token: "version" })
            {
                Console.WriteLine(help.Heading);
                Console.WriteLine(help.Copyright);
                Console.WriteLine($"\x1b[4m{__module__.RepositoryURL}/\x1b[24m");

                help_requested = true;
            }
            else
            {
                Console.WriteLine(help + "  --                        All subsequent arguments will be passed to the AutoIt-3 script.");

                if (err.FirstOrDefault() is HelpRequestedError or HelpVerbRequestedError)
                    help_requested = true;
                else
                    code = -1;
            }
        }

        private static async Task<bool> UpdateTask()
        {
            if (CommandLineOptions.UpdaterMode is UpdaterMode.none)
                return false;

            GithubUpdater updater = new(Telemetry)
            {
                UpdaterMode = CommandLineOptions.UpdaterMode is UpdaterMode.beta ? GithubUpdaterMode.IncludeBetaVersions : GithubUpdaterMode.ReleaseOnly
            };

            bool success = await updater.FetchReleaseInformationAsync().ConfigureAwait(true);
            LanguagePack lang = LanguageLoader.CurrentLanguage!;

            if (!success && CommandLineOptions.Verbose)
            {
                PrintWarning(null, lang["warning.unable_to_update", __module__.RepositoryURL + "/releases"]);

                return false;
            }
            else
                success = false;

            if (updater.LatestReleaseAvailable is Release latest)
            {
                bool handled = false;
                bool confirmation = false;

                _print_queue.Enqueue(() => Task.Run(delegate
                {
                    ConsoleExtensions.RGBForegroundColor = COLOR_PREFIX_DEBUG;
                    Console.WriteLine("\n-------------------------------------------------------------------------------------------------------------\t");
                    ConsoleExtensions.WriteUnderlined(lang["general.update.header"]);
                    Console.WriteLine("\n-------------------------------------------------------------------------------------------------------------");
                    ConsoleExtensions.RGBForegroundColor = COLOR_DEBUG;
                    Console.WriteLine(lang["general.update.message", __module__.InterpreterVersion, latest.TagName, latest.Body.SplitIntoLines().Select(line => '\t' + line).StringJoin("\n")]);

                    confirmation = Console.ReadKey(true).Key == ConsoleKey.Y;

                    Console.WriteLine();

                    handled = true;
                }).GetAwaiter().GetResult());

                while (!handled)
                    await Task.Delay(20).ConfigureAwait(true);

                if (confirmation)
                {
                    success = await updater.TryUpdateTo(latest).ConfigureAwait(true);

                    if (!success)
                        PrintError(lang["error.update_failed", latest.TagName, latest.PublishedAt, __module__.RepositoryURL + "/releases"]);
                }
                else
                    Console.WriteLine(lang["general.update.update_cancelled"]);
            }

            return success;
        }

        private static async Task PrinterTask()
        {
            while (_isrunning)
                if (!PausePrinter && _print_queue.TryDequeue(out Action? func))
                    try
                    {
                        Telemetry.Measure(TelemetryCategory.Printing, func);
                    }
                    catch (Exception ex)
                    {
                        PrintException(ex);
                    }
                else
                    await Task.Delay(50);

            while (_print_queue.TryDequeue(out Action? func))
                try
                {
                    Telemetry.Measure(TelemetryCategory.Printing, func);
                }
                catch (Exception ex)
                {
                    PrintException(ex);
                }

            _finished = true;
        }

        private static void SubmitPrint(bool requires_verbose, string prefix, string msg, bool from_script)
        {
            if (!CommandLineOptions.Verbose && requires_verbose)
                return;

            DateTime now = DateTime.Now;

            _print_queue.Enqueue(delegate
            {
                ConsoleExtensions.RGBForegroundColor = RGBAColor.DarkGray;
                Console.Write('[');
                ConsoleExtensions.RGBForegroundColor = COLOR_TIMESTAMP;
                Console.Write(now.ToString("HH:mm:ss.fff"));
                ConsoleExtensions.RGBForegroundColor = RGBAColor.DarkGray;
                Console.Write("][");
                ConsoleExtensions.RGBForegroundColor = from_script ? COLOR_PREFIX_SCRIPT : COLOR_PREFIX_DEBUG;
                Console.Write(prefix);
                ConsoleExtensions.RGBForegroundColor = RGBAColor.DarkGray;
                Console.Write("] ");
                ConsoleExtensions.RGBForegroundColor = from_script ? COLOR_SCRIPT : COLOR_DEBUG;
                Console.WriteLine(msg);
                ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            });
        }

        /// <summary>
        /// Prints the given debug message asynchronously to STDOUT.
        /// </summary>
        /// <param name="message">The debug message to be printed.</param>
        public static void PrintDebugMessage(string message) => PrintChannelMessage("Debug", message);

        /// <summary>
        /// Prints the given localized debug message asynchronously to STDOUT.
        /// </summary>
        /// <param name="key">The language key of the message to be printed.</param>
        /// <param name="args">The arguments used to format the message to be printed.</param>
        public static void PrintfDebugMessage(string key, params object?[] args) => PrintDebugMessage(LanguageLoader.CurrentLanguage?[key, args] ?? key);

        internal static void PrintChannelMessage(string channel, string message) => SubmitPrint(true, channel, message, false);

        /// <summary>
        /// Prints the given message asynchronously to STDOUT.
        /// </summary>
        /// <param name="file">The (script) file which emitted the message.</param>
        /// <param name="message">The message to be printed.</param>
        public static void PrintScriptMessage(string? file, string message) => Telemetry.Measure(TelemetryCategory.ScriptConsoleOut, delegate
        {
            if (CommandLineOptions.ProgramExecutionMode is ExecutionMode.view)
                return;
            else if (InteractiveShell is InteractiveShell shell)
                shell.SubmitPrint(message);
            else if (!CommandLineOptions.Verbose)
                Console.Write(message);
            else
                SubmitPrint(true, file ?? '<' + LanguageLoader.CurrentLanguage?["general.unknown"] + '>', message.Trim(), true);
        });

        /// <summary>
        /// Prints the given exception asynchronously to STDOUT.
        /// </summary>
        /// <param name="exception">The exception to be printed.</param>
        public static void PrintException(this Exception? exception)
        {
            if (exception is { })
                if (!CommandLineOptions.Verbose)
                    PrintError(exception.Message);
                else
                {
                    StringBuilder sb = new();

                    while (exception is { })
                    {
                        sb.Insert(0, $"[{exception.GetType()}] \"{exception.Message}\":\n{exception.StackTrace}\n");
                        exception = exception.InnerException;
                    }

                    PrintError(sb.ToString());
                }
        }

        /// <summary>
        /// Prints the given error message asynchronously to STDOUT.
        /// </summary>
        /// <param name="message">The error message to be printed.</param>
        public static void PrintError(this string message) => _print_queue.Enqueue(() => Telemetry.Measure(TelemetryCategory.Exceptions, delegate
        {
            if (!CommandLineOptions.Verbose && Console.CursorLeft > 0)
                Console.WriteLine();

            if (CommandLineOptions.Verbose)
            {
                ConsoleExtensions.RGBForegroundColor = RGBAColor.Orange;
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

            ConsoleExtensions.RGBForegroundColor = COLOR_ERROR;
            Console.WriteLine($"{message.TrimEnd()}\nIf you believe that this is a bug, please report it to \x1b[4m{__module__.RepositoryURL}/issues/new?template=bug_report.md\x1b[24m.");

            if (CommandLineOptions.Verbose)
            {
                ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
                Console.WriteLine(new string('_', Console.WindowWidth - 1));
            }
        }));

        /// <summary>
        /// Prints the given warning message asynchronously to STDOUT.
        /// </summary>
        /// <param name="location">The source location at which the warning occurred.</param>
        /// <param name="message">The warning message to be printed.</param>
        public static void PrintWarning(SourceLocation? location, string message) => _print_queue.Enqueue(() => Telemetry.Measure(TelemetryCategory.Warnings, delegate
        {
            if (!CommandLineOptions.Verbose)
            {
                if (Console.CursorLeft > 0)
                    Console.WriteLine();

                ConsoleExtensions.RGBForegroundColor = COLOR_WARNING;
                Console.WriteLine(LanguageLoader.CurrentLanguage?[location is null ? "warning.warning" : "warning.warning_in", location] + ":\n    " + message.Trim());
            }
            else
            {
                ConsoleExtensions.RGBForegroundColor = RGBAColor.DarkGray;
                Console.Write('[');
                ConsoleExtensions.RGBForegroundColor = COLOR_TIMESTAMP;
                Console.Write(DateTime.Now.ToString("HH:mm:ss.fff", null));
                ConsoleExtensions.RGBForegroundColor = RGBAColor.DarkGray;
                Console.Write("][");
                ConsoleExtensions.RGBForegroundColor = COLOR_WARNING;
                Console.Write("warning");
                ConsoleExtensions.RGBForegroundColor = RGBAColor.DarkGray;
                Console.Write("] ");
                ConsoleExtensions.RGBForegroundColor = COLOR_WARNING;
                Console.WriteLine(message.Trim());
                ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            }
        }));

        /// <summary>
        /// Prints the given return code and telemetry data synchronously to STDOUT.
        /// </summary>
        /// <param name="retcode">Return code, e.g. from the interpreter execution.</param>
        /// <param name="telemetry">Telemetry data to be printed.</param>
        public static void PrintReturnCodeAndTelemetry(int retcode, Telemetry telemetry) => _print_queue.Enqueue(delegate
        {
            LanguagePack? lang = LanguageLoader.CurrentLanguage;

            if (lang is null)
                return;
            else if (Console.CursorLeft > 0)
                Console.WriteLine();

            bool print_telemetry = CommandLineOptions is { Verbose: true } or { PrintTelemetry: true };
            int width = Math.Min(Console.WindowWidth, Console.BufferWidth);

            if (print_telemetry)
            {
                const int MIN_WIDTH = 180;

                NativeInterop.DoPlatformDependent(delegate
                {
                    Console.WindowWidth = Math.Max(Console.WindowWidth, MIN_WIDTH);
                    Console.BufferWidth = Math.Max(Console.BufferWidth, Console.WindowWidth);
                }, OS.Windows);

                width = Math.Min(Console.WindowWidth, Console.BufferWidth);

                if (NativeInterop.OperatingSystem == OS.Windows && width < MIN_WIDTH)
                {
                    PrintError(lang["debug.telemetry.print_error", MIN_WIDTH]);

                    return;
                }
            }

            TelemetryTimingsNode root = TelemetryTimingsNode.FromTelemetry(telemetry);

            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            Console.WriteLine(new string('_', width - 1));
            ConsoleExtensions.RGBForegroundColor = retcode == 0 ? RGBAColor.SpringGreen : RGBAColor.Salmon;
            Console.WriteLine(lang["debug.telemetry.exit_code", retcode, root.Total, telemetry.TotalTime[TelemetryCategory.InterpreterRuntime]]);
            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;

            if (!print_telemetry)
                return;

            ConsoleExtensions.RGBForegroundColor = RGBAColor.Yellow;
            Console.WriteLine("\n\t\t" + lang["debug.telemetry.header"]);

            #region TIMTINGS : FETCH DATA, INIT

            RGBAColor col_table = RGBAColor.LightGray;
            RGBAColor col_text = RGBAColor.White;
            RGBAColor col_backg = RGBAColor.DarkSlateGray;
            RGBAColor col_hotpath = RGBAColor.Salmon;

            Regex regex_trimstart = new Regex(@"^(?<space>\s*)0(?<rest>\d[:\.].+)$", RegexOptions.Compiled);
            string[] headers = {
                lang["debug.telemetry.columns.category"],
                lang["debug.telemetry.columns.count"],
                lang["debug.telemetry.columns.total"],
                lang["debug.telemetry.columns.avg"],
                lang["debug.telemetry.columns.min"],
                lang["debug.telemetry.columns.max"],
                lang["debug.telemetry.columns.parent"],
                lang["debug.telemetry.columns.relative"],
            };
            List<(string[] cells, TelemetryTimingsNode node)> rows = new();
            static string ReplaceStart(string input, params (string search, string replace)[] substitutions)
            {
                int idx = 0;
                bool match;

                do
                {
                    match = false;

                    foreach ((string search, string replace) in substitutions)
                        if (input[idx..].StartsWith(search))
                        {
                            input = input[..idx] + replace + input[(idx + search.Length)..];
                            idx += replace.Length;
                            match = true;

                            break;
                        }
                }
                while (match);

                return input;
            }
            string PrintTime(TimeSpan time)
            {
                string s = ReplaceStart(time.ToString("h\\:mm\\:ss\\.ffffff"),
                    ("00:", "   "),
                    ("0:", "  "),
                    ("00.", " 0.")
                ).TrimEnd('0');

                if (s.Match(regex_trimstart, out ReadOnlyIndexer<string, string>? groups))
                    s = groups["space"] + ' ' + groups["rest"];

                if (s.EndsWith("0."))
                    s = s[..^1];
                else if (s.EndsWith('.'))
                    s += '0';

                return s;
            }
            void traverse(TelemetryTimingsNode node, string prefix = "", bool last = true)
            {
                rows.Add((new[]
                {
                    prefix.Length switch
                    {
                        0 => " ·─ " + node.Name,
                        _ => string.Concat(prefix.Select(c => c is 'x' ? " │  " : "    ").Append(last ? " └─ " : " ├─ ").Append(node.Name))
                    },
                    node.Timings.Length.ToString().PadLeft(5),
                    PrintTime(node.Total),
                    PrintTime(node.Average),
                    PrintTime(node.Min),
                    PrintTime(node.Max),
                    $"{node.PercentageOfParent * 100,9:F5} %",
                    $"{node.PercentageOfTotal * 100,9:F5} %",
                }, node));

                TelemetryTimingsNode[] children = node.Children.OrderByDescending(c => c.PercentageOfTotal).ToArray();

                for (int i = 0; i < children.Length; i++)
                {
                    TelemetryTimingsNode child = children[i];

                    traverse(child, prefix + (last ? ' ' : 'x'), i == children.Length - 1);
                }
            }

            traverse(root);

            int[] widths = headers.ToArray(h => h.Length);

            foreach (string[] cells in rows.Select(r => r.cells))
                for (int i = 0; i < widths.Length; i++)
                    widths[i] = Math.Max(widths[i], cells[i].Length);

            #endregion
            #region TIMINGS : PRINT HEADER

            //Console.CursorTop -= 2;
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
                ConsoleExtensions.Write(headers[i].PadRight(widths[i]), (xoffs, yoffs + 1));
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

            #endregion
            #region TIMINGS : PRINT DATA

            foreach ((string[] cells, TelemetryTimingsNode node) in rows)
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
                        Console.Write(cell);
                        ConsoleExtensions.RGBForegroundColor = col_backg;
                        Console.Write(new string('─', widths[i] - cell.Length));
                    }
                    else
                    {
                        int xoffs = Console.CursorLeft;

                        ConsoleExtensions.RGBForegroundColor = col_backg;
                        Console.Write(new string('─', widths[i]));
                        ConsoleExtensions.RGBForegroundColor = node.IsHot ? col_hotpath : col_text;
                        Console.CursorLeft = xoffs;

                        for (int j = 0, k = Math.Min(widths[i], cell.Length); j < k; ++j)
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

            #endregion
            #region TIMINGS : PRINT FOOTER

            ConsoleExtensions.RGBForegroundColor = col_table;

            for (int i = 0, l = widths.Length; i < l; i++)
            {
                if (i == 0)
                    Console.Write('└');

                Console.Write(new string('─', widths[i]));
                Console.Write(i == l - 1 ? '┘' : '┴');
            }

            Console.WriteLine();
            Console.WriteLine(lang["debug.telemetry.explanation"]);

            #endregion

            if (NativeInterop.OperatingSystem == OS.Windows)
            {
                #region PERFORMANCE : FETCH DATA

                const int PADDING = 22;
                List<(DateTime time, double total, double user, double kernel, long ram)> performance_data = new();
                int width_perf = width - 3 - PADDING;
                const int height_perf_cpu = 14;

                performance_data.AddRange(telemetry.PerformanceMeasurements);

                if (performance_data.Count > width_perf)
                {
                    int step = performance_data.Count / (performance_data.Count - width_perf);
                    int index = performance_data.Count - 1;

                    while (index > 0 && performance_data.Count > width_perf)
                    {
                        performance_data.RemoveAt(index);
                        index -= step;
                    }
                }

                width_perf = performance_data.Count + PADDING;

                #endregion
                #region PERFORMANCE : PRINT FRAME

                RGBAColor col_cpu_user = RGBAColor.Chartreuse;
                RGBAColor col_cpu_kernel = RGBAColor.LimeGreen;
                RGBAColor col_ram = RGBAColor.CornflowerBlue;

                ConsoleExtensions.RGBForegroundColor = col_table;
                Console.WriteLine('┌' + new string('─', width_perf) + '┐');

                int ypos = Console.CursorTop;

                for (int i = 0; i < height_perf_cpu + 2; ++i)
                {
                    Console.CursorLeft = 0;
                    Console.Write('│');
                    Console.CursorLeft = width_perf + 1;
                    Console.Write('│');
                    Console.CursorTop++;
                }

                Console.CursorLeft = 0;
                Console.WriteLine('└' + new string('─', width_perf) + '┘');

                Console.SetCursorPosition(2, ypos);
                ConsoleExtensions.RGBForegroundColor = col_text;
                ConsoleExtensions.WriteUnderlined("CPU Load");
                Console.SetCursorPosition(2, ypos + 2);
                ConsoleExtensions.RGBForegroundColor = col_cpu_user;
                Console.Write("███ ");
                ConsoleExtensions.RGBForegroundColor = col_text;
                Console.Write("User");
                Console.SetCursorPosition(2, ypos + 3);
                ConsoleExtensions.RGBForegroundColor = col_cpu_kernel;
                Console.Write("███ ");
                ConsoleExtensions.RGBForegroundColor = col_text;
                Console.Write("Kernel");

                string grid = "─┼";

                for (int j = 0; j < height_perf_cpu; ++j)
                {
                    ConsoleExtensions.RGBForegroundColor = col_text;
                    Console.SetCursorPosition(PADDING - 7, ypos + height_perf_cpu - j - 1);
                    Console.Write($"{100 * j / (height_perf_cpu - 1d),3:F0} %");
                    ConsoleExtensions.RGBForegroundColor = col_backg;
                    Console.Write('─' + Enumerable.Repeat(grid, (performance_data.Count + 1) / 2).StringConcat());

                    if (performance_data.Count % 2 == 0)
                        Console.Write('─');
                }

                ConsoleExtensions.RGBForegroundColor = col_text;
                Console.SetCursorPosition(2, ypos + height_perf_cpu + 1);
                Console.Write("time since start:");

                for (int j = 1; j < performance_data.Count - 8; j += 12)
                {
                    ConsoleExtensions.RGBForegroundColor = col_backg;
                    Console.SetCursorPosition(PADDING + j + 1, ypos + height_perf_cpu);
                    Console.Write('│');
                    ConsoleExtensions.RGBForegroundColor = col_text;
                    Console.SetCursorPosition(PADDING + j - 3, ypos + height_perf_cpu + 1);

                    TimeSpan diff = performance_data[j].time - performance_data[0].time;

                    Console.Write(diff switch
                    {
                        _ when diff.TotalSeconds < 1 => $"{diff.TotalMilliseconds,7:F3}ms",
                        _ when diff.TotalSeconds < 60 => $"{diff.Seconds:D2}:{diff.TotalMilliseconds % 1000,6:F2}ms",
                        _ when diff.TotalMinutes < 60 => $"{diff.Minutes:D2}:{diff.Seconds:D2}:{diff.Milliseconds:D3}ms",
                        _ => diff.ToString("HH:mm:ss:f")
                    });
                }

                // TODO : smthing with Environment.ProcessorCount?

                #endregion
                #region PERFORMANCE : PRINT DATA

                string bars = "_‗▄░▒▓█";

                for (int i = 0; i < performance_data.Count; i++)
                {
                    (_, double cpu, _, double kernel, _) = performance_data[i];

                    for (int j = 0; j < height_perf_cpu; ++j)
                    {
                        Console.SetCursorPosition(PADDING + i, ypos + height_perf_cpu - j - 1);

                        double lo = j / (height_perf_cpu - 1d);
                        double hi = (j + 1) / (height_perf_cpu - 1d);

                        if (cpu < lo)
                            break;
                        else if (cpu < hi)
                        {
                            ConsoleExtensions.RGBForegroundColor = col_cpu_user;
                            ConsoleExtensions.WriteUnderlined(bars[(int)(Math.Min(.99, (hi - cpu) / (hi - lo)) * bars.Length)].ToString());
                        }
                        else if (kernel < lo)
                        {
                            ConsoleExtensions.RGBForegroundColor = col_cpu_user;
                            Console.Write(bars[^1]);
                        }
                        else if (kernel < hi)
                        {
                            ConsoleExtensions.RGBForegroundColor = col_cpu_kernel;
                            ConsoleExtensions.RGBBackgroundColor = col_cpu_user;
                            ConsoleExtensions.WriteUnderlined(bars[(int)(Math.Min(.99, (hi - kernel) / (hi - lo)) * bars.Length)].ToString());
                            Console.Write("\x1b[0m");
                        }
                        else
                        {
                            ConsoleExtensions.RGBForegroundColor = col_cpu_kernel;
                            Console.Write(bars[^1]);
                        }
                    }
                }

                IEnumerable<double> c_total = performance_data.Select(p => p.total * 100);
                IEnumerable<double> c_user = performance_data.Select(p => p.user * 100);
                IEnumerable<double> c_kernel = performance_data.Select(p => p.kernel * 100);
                IEnumerable<double> c_ram = performance_data.Select(p => p.ram / 1024d / 1024d);

                Console.SetCursorPosition(0, ypos + height_perf_cpu + 1);
                ConsoleExtensions.RGBForegroundColor = col_table;
                Console.WriteLine($@"
├────────────┬──────────────┬──────────────┬
│ Category   │ Maximum Load │ Average Load │
├────────────┼──────────────┼──────────────┤
│ Total CPU  │ {c_total.Max(),10:F5} % │ {c_total.Average(),10:F5} % │
│ User CPU   │ {c_user.Max(),10:F5} % │ {c_user.Average(),10:F5} % │
│ Kernel CPU │ {c_kernel.Max(),10:F5} % │ {c_kernel.Average(),10:F5} % │
│ RAM        │ {c_ram.Max(),9:F3} MB │ {c_ram.Average(),9:F3} MB │
└────────────┴──────────────┴──────────────┘
");

                #endregion
            }

            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            Console.WriteLine(new string('_', width - 1));
        });

        /// <summary>
        /// Prints the banner synchronously to STDOUT.
        /// </summary>
        public static void PrintBanner()
        {
            if (CommandLineOptions.Verbose)
                _print_queue.Enqueue(delegate
                {
                    LanguagePack? lang = LanguageLoader.CurrentLanguage;

                    From hash = From.File(ASM_FILE).Hash(HashFunctions.SHA256);

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
                          |_|  {lang?["banner.written_by", __module__.Author, __module__.Year]}
{lang?["banner.version"]} v.{__module__.InterpreterVersion} ({__module__.GitHash})
   {'\x1b'}[4m{__module__.RepositoryURL}/{'\x1b'}[24m 
");
                    ConsoleExtensions.RGBForegroundColor = RGBAColor.Crimson;
                    Console.Write("    ");
                    ConsoleExtensions.WriteUnderlined("WARNING!");
                    ConsoleExtensions.RGBForegroundColor = RGBAColor.Salmon;
                    Console.WriteLine(" This may panic your CPU.\n\n");

                    PrintDebugMessage($"Visual signature (used to easily verify whether two AutoIt-versions are equal):\n{hash.ToDrunkBishop()}\n{hash.ToHexString()}");
                });
        }
    }

    /// <summary>
    /// An enumeration of different program execution modes.
    /// </summary>
    public enum ExecutionMode
    {
        normal,
        view,
        interactive,
        tidy,
    }

    public enum UpdaterMode
    {
        release,
        beta,
        none,
    }
}

