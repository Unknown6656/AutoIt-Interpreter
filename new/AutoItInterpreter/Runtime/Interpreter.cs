using System.Diagnostics.CodeAnalysis;
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
        private readonly List<IStatementProcessor> _statement_processors = new List<IStatementProcessor>();
        private readonly List<IIncludeResolver> _resolvers = new List<IIncludeResolver>();
        private readonly ConcurrentDictionary<AU3Thread, __empty> _threads = new ConcurrentDictionary<AU3Thread, __empty>();
        private readonly object _main_thread_mutex = new object();

        public AU3Thread? MainThread { get; private set; }

        public AU3Thread[] Threads => _threads.Keys.ToArray();

        public IReadOnlyList<ILineProcessor> LineProcessors => _line_processors;

        public IReadOnlyList<IDirectiveProcessor> DirectiveProcessors => _directive_processors;

        public IReadOnlyList<IStatementProcessor> StatementProcessors => _statement_processors;

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

        public void RegisterStatementProcessor(IStatementProcessor proc) => _statement_processors.Add(proc);

        public void RegisterIncludeResolver(IIncludeResolver resolver) => _resolvers.Add(resolver);

        public AU3Thread CreateNewThread(SourceLocation location)
        {
            AU3Thread thread = new AU3Thread(this);

            thread.Create(location);

            return thread;
        }

        internal void AddThread(AU3Thread thread) => _threads.TryAdd(thread, default);

        internal void RemoveThread(AU3Thread thread) => _threads.TryRemove(thread, out _);

        public InterpreterResult Run(SourceLocation entry_point)
        {
            try
            {
                using AU3Thread thread = CreateNewThread(entry_point);

                lock (_main_thread_mutex)
                    MainThread = thread;

                return thread.Run();
            }
            finally
            {
                lock (_main_thread_mutex)
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
        public SourceLocation? Location { get; }
        public string Message { get; }


        public InterpreterError(SourceLocation? location, string message)
        {
            Location = location;
            Message = message;
        }

        public static InterpreterError WellKnown(SourceLocation? loc, string key, params object?[] args) => new InterpreterError(loc, Program.CurrentLanguage[key, args]);
    }

    public interface IDirectiveProcessor
    {
        InterpreterResult? ProcessDirective(CallFrame frame, string directive);
    }

    public interface IStatementProcessor
    {
        string Regex { get; }

        InterpreterResult? ProcessStatement(CallFrame frame, string directive);
    }

    public interface ILineProcessor
    {
        bool CanProcessLine(string line);

        InterpreterResult? ProcessLine(CallFrame frame, string line);


        public static ILineProcessor FromDelegate(Predicate<string> canparse, Func<CallFrame, string, InterpreterResult?> process) => new __from_delegate(canparse, process);

        private sealed class __from_delegate
            : ILineProcessor
        {
            private readonly Predicate<string> _canparse;
            private readonly Func<CallFrame, string, InterpreterResult?> _process;


            public __from_delegate(Predicate<string> canparse, Func<CallFrame, string, InterpreterResult?> process)
            {
                _canparse = canparse;
                _process = process;
            }

            public bool CanProcessLine(string line) => _canparse(line);

            public InterpreterResult? ProcessLine(CallFrame parser, string line) => _process(parser, line);
        }
    }

    public interface IIncludeResolver
    {
        bool TryResolve(string path, [MaybeNullWhen(false), NotNullWhen(true)] out (FileInfo physical_file, string content)? resolved);
    }
}

