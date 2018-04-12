using System.IO;

namespace CSAutoItInterpreter
{
    public static class Program
    {
        public static int Main(string[] argv)
        {
            InterpreterSettings settings = InterpreterSettings.DefaultSettings;
            Interpreter intp = new Interpreter("./test.au3", settings);

            intp.DoMagic();

            return 0;
        }
    }

    public class InterpreterSettings
    {
        public static InterpreterSettings DefaultSettings { get; } = new InterpreterSettings
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
            }
        };

        public string[] IncludeDirectories { set; get; }
    }
}
