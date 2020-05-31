using CommandLine;

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
            int code = 0;

            Parser.Default.ParseArguments<CommandLineOptions>(argv).WithParsed(opt =>
            {
                if (!opt.HideBanner)
                    PrintBanner();

                code = Interpreter.Interpreter.Run(opt);
            }).WithNotParsed(errs => code = -1);

            return code;
        }

        private static void PrintBanner()
        {
            Conosle.
        }
    }
}
