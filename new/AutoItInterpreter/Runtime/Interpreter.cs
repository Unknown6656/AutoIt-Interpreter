using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class Interpreter
        : IDisposable
    {
        private readonly List<ILineProcessor> _line_processors = new List<ILineProcessor>();
        private readonly List<IDirectiveProcessor> _directive_processors = new List<IDirectiveProcessor>();
        private readonly List<IIncludeResolver> _resolvers = new List<IIncludeResolver>();
        private readonly ConcurrentDictionary<AU3Thread, __empty> _threads = new ConcurrentDictionary<AU3Thread, __empty>();


        public AU3Thread? MainThread { get; private set; }

        public AU3Thread[] Threads => _threads.Keys.ToArray();

        public IReadOnlyList<ILineProcessor> LineProcessors => _line_processors;

        public IReadOnlyList<IDirectiveProcessor> DirectiveProcessors => _directive_processors;

        public IReadOnlyList<IIncludeResolver> IncludeResolvers => _resolvers;



        public void Dispose()
        {
            foreach (AU3Thread thread in Threads)
            {
                thread.Dispose();
                _threads.TryRemove(thread, out _);
            }
        }

        public void RegisterLineProcessor(ILineProcessor proc) => _line_processors.Add(proc);

        public void RegisterDirectiveProcessor(IDirectiveProcessor proc) => _directive_processors.Add(proc);

        public void RegisterIncludeResolver(IIncludeResolver resolver) => _resolvers.Add(resolver);

        public AU3Thread StartNewThread(SourceLocation location)
        {
            AU3Thread thread = new AU3Thread(this);

            thread.Start(location);

            return thread;
        }

        internal void AddThread(AU3Thread thread) => _threads.TryAdd(thread, default);

        internal void RemoveThread(AU3Thread thread) => _threads.TryRemove(thread, out _);




        public InterpreterResult Run(SourceLocation entry_point)
        {
            try
            {
                using AU3Thread thread = StartNewThread(entry_point);

                lock (this)
                    MainThread = thread;


                // TODO



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
            finally
            {
                lock (this)
                    MainThread = null;
            }
        }

        public static InterpreterResult Run(CommandLineOptions opt)
        {
            FileInfo input = new FileInfo(opt.FilePath);

            if (input.Exists)
                using (Interpreter interpreter = new Interpreter())
                    return interpreter.Run(new SourceLocation(input, 0));
            else
                return new InterpreterResult(-1, new InterpreterError(new SourceLocation(input, -1), $"The script file '{opt.FilePath}' could not be found."));
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
        public SourceLocation Location { get; }
        public string Message { get; }


        public InterpreterError(SourceLocation location, string message)
        {
            Location = location;
            Message = message;
        }

        public static InterpreterError WellKnown(SourceLocation loc, string key, params object[] args) => new InterpreterError(loc, Program.CurrentLanguage[key, args]);
    }
}

