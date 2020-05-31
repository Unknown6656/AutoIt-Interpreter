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

        public InterpreterResult Run()
        {




            throw new NotImplementedException();
        }


        public static InterpreterResult Run(CommandLineOptions opt)
        {
            FileInfo input = new FileInfo(opt.FilePath);

            if (input.Exists)
            {
                using LineParser parser = new LineParser(input);
                using Interpreter interpreter = new Interpreter(parser);

                return interpreter.Run();
            }
            else
                return new InterpreterResult(-1, new InterpreterError(input, -1, $"The script file '{opt.FilePath}' could not be found."));
        }
    }

    public sealed class InterpreterResult
    {
        public int ProgramExitCode { get; }
        public InterpreterError? OptionalError { get; }


        public InterpreterResult(int programExitCode, InterpreterError? err = null)
        {
            ProgramExitCode = programExitCode;
            OptionalError = err;
        }
    }

    public sealed class InterpreterError
    {
        public int Line { get; }
        public FileInfo File { get; }
        public string Message { get; }


        public InterpreterError(FileInfo file, int line, string message)
        {
            File = file;
            Line = line;
            Message = message;
        }
    }
}

