using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unknown6656.AutoIt3.Interpreter
{
    public sealed class Interpreter
        : IDisposable
    {
        public LineParser Parser { get; }


        public Interpreter(LineParser parser) => Parser = parser;

        public void Dispose() => Parser.Dispose();




        public static int Run(CommandLineOptions opt)
        {
            if (new FileInfo(opt.FilePath) is { Exists: true } input)
            {
                using LineParser parser = new LineParser(input);
                using Interpreter interpeter = new Interpreter(parser);

                // TODO
            }

        }
    }
}
