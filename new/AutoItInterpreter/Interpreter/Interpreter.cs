using System.Collections.Generic;
using System.IO;
using System;

namespace Unknown6656.AutoIt3.Interpreter
{
    public sealed class Interpreter
        : IDisposable
    {
        private readonly List<ILineProcessor> _line_processors = new List<ILineProcessor>();
        private readonly List<IDirectiveProcessor> _directive_processors = new List<IDirectiveProcessor>();
        private readonly List<IIncludeResolver> _resolvers = new List<IIncludeResolver>();


        public LineParser Parser { get; }

        public IReadOnlyList<ILineProcessor> LineProcessors => _line_processors;

        public IReadOnlyList<IDirectiveProcessor> DirectiveProcessors => _directive_processors;

        public IReadOnlyList<IIncludeResolver> IncludeResolvers => _resolvers;


        public Interpreter(LineParser parser) => Parser = parser;

        public void Dispose() => Parser.Dispose();

        public void RegisterLineProcessor(ILineProcessor proc) => _line_processors.Add(proc);

        public void RegisterDirectiveProcessor(IDirectiveProcessor proc) => _directive_processors.Add(proc);

        public void RegisterIncludeResolver(IIncludeResolver resolver) => _resolvers.Add(resolver);

        public InterpreterResult Run()
        {
            InterpreterResult? result = null;

            Parser.MoveToStart();

            do
            {
                result = Parser.ParseCurrentLine();

                if (result?.OptionalError is { } || (result?.ProgramExitCode ?? 0) != 0)
                    break;
            }
            while (Parser.MoveNext());

            return result ?? InterpreterResult.OK;
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
                return new InterpreterResult(-1, new InterpreterError(new Location(input, -1), $"The script file '{opt.FilePath}' could not be found."));
        }
    }

    public sealed class InterpreterResult
    {
        public static InterpreterResult OK { get; } = new InterpreterResult(0, null);

        public int ProgramExitCode { get; }
        public InterpreterError? OptionalError { get; }


        public InterpreterResult(int programExitCode, InterpreterError? err = null)
        {
            ProgramExitCode = programExitCode;
            OptionalError = err;
        }

        public static implicit operator InterpreterResult(InterpreterError err) => new InterpreterResult(-1, err);
    }

    public sealed class InterpreterError
    {
        public Location Location { get; }
        public string Message { get; }


        public InterpreterError(Location location, string message)
        {
            Location = location;
            Message = message;
        }

        public static InterpreterError WellKnown(Location loc, string key, params object[] args) => new InterpreterError(loc, Program.CurrentLanguage[key, args]);
    }
}

