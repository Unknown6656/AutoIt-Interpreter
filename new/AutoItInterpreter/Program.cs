using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

using CommandLine;

using Unknown6656.AutoIt3.Interpreter;
using Unknown6656.Controls.Console;
using Unknown6656.Imaging;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3
{
    public sealed class CommandLineOptions
    {
        [Option('B', "nobanner", Default = false, HelpText = "Suppresses the banner.")]
        public bool HideBanner { get; }

        [Value(0, HelpText = "The file path to the AutoIt-3 srcript.", Required = true)]
        public string FilePath { get; }


        public CommandLineOptions(bool hideBanner, string filePath)
        {
            HideBanner = hideBanner;
            FilePath = filePath;
        }
    }

    public static class Program
    {
        public static int Main(string[] argv)
        {
            ConsoleState state = ConsoleExtensions.SaveConsoleState();
            int code = 0;

            try
            {
                Console.OutputEncoding = Encoding.Unicode;
                Console.InputEncoding = Encoding.Unicode;

                Parser.Default.ParseArguments<CommandLineOptions>(argv).WithParsed(opt =>
                {
                    if (!opt.HideBanner)
                        PrintBanner();

                    ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
                    ConsoleExtensions.RGBBackgroundColor = RGBAColor.Black;

                    InterpreterResult result = Interpreter.Interpreter.Run(opt);

                    if (result.OptionalError is { } err)
                        PrintError($"ERROR in {err.Location}:\n    {err.Message}");

                    code = result.ProgramExitCode;
                }).WithNotParsed(errs => code = -1);
            }
            catch (Exception ex)
            when (!Debugger.IsAttached)
            {
                code = ex.HResult;

                PrintException(ex);
            }
            finally
            {
                ConsoleExtensions.RestoreConsoleState(state);
            }

            return code;
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

        internal static void PrintError(this string message)
        {
            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            Console.WriteLine('\n' + new string('_', Console.WindowWidth - 1));
            ConsoleExtensions.RGBForegroundColor = RGBAColor.Red;
            Console.WriteLine(message.TrimEnd());
            ConsoleExtensions.RGBForegroundColor = RGBAColor.White;
            Console.WriteLine(new string('_', Console.WindowWidth - 1));
        }

        private static void PrintBanner()
        {
            ConsoleExtensions.RGBBackgroundColor = RGBAColor.Black;
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
                          |_|  Written by Unknown6656, 2020
Version v.{Module.InterpreterVersion} ({Module.GitHash})
");
            ConsoleExtensions.RGBForegroundColor = RGBAColor.Crimson;
            Console.Write("    ");
            ConsoleExtensions.WriteUnderlined("WARNING!");
            ConsoleExtensions.RGBForegroundColor = RGBAColor.Salmon;
            Console.Write(" This may panic your CPU.\n");
        }
    }

    public static class Module
    {
        public static Version? InterpreterVersion { get; }
        public static string GitHash { get; }


        static Module()
        {
            string[] lines = Properties.Resources.version.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToArray(s => s.Trim());

            InterpreterVersion = Version.TryParse(lines[0], out Version? v) ? v : null;
            GitHash = lines.Length > 0 ? lines[1] : "";

            if ((GitHash?.Length ?? 0) == 0)
                GitHash = "<unknown git commit hash>";
        }
    }
}
