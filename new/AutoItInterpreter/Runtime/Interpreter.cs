using System.Collections.Concurrent;
using System.Linq;
using System;

using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    using static Program;
    using static AST;

    public sealed class Interpreter
        : IDisposable
    {
        private readonly ConcurrentDictionary<AU3Thread, __empty> _threads = new ConcurrentDictionary<AU3Thread, __empty>();
        private readonly object _main_thread_mutex = new object();

        public AU3Thread? MainThread { get; private set; }

        public AU3Thread[] Threads => _threads.Keys.ToArray();

        public VariableScope VariableResolver { get; }

        public CommandLineOptions CommandLineOptions { get; }

        public ScriptScanner ScriptScanner { get; }

        public PluginLoader PluginLoader { get; }


        public Interpreter(CommandLineOptions opt)
        {
            CommandLineOptions = opt;
            ScriptScanner = new ScriptScanner(this);
            PluginLoader = new PluginLoader(this, PLUGIN_DIR);

            if (!opt.DontLoadPlugins)
                PluginLoader.LoadPlugins();

            PrintInterpreterMessage(PluginLoader.LoadedPlugins.Count switch {
                0 => CurrentLanguage["general.no_plugins_loaded"],
                int i => CurrentLanguage["general.plugins_loaded", i, PluginLoader.PluginDirectory.FullName],
            });

            ScriptScanner.ScanNativeFunctions();

            VariableResolver = VariableScope.CreateGlobalScope(this);
            VariableResolver.CreateVariable(SourceLocation.Unknown, VARIABLE.Discard.Name, false);
        }

        public void Dispose()
        {
            foreach (AU3Thread thread in Threads)
            {
                thread.Dispose();
                _threads.TryRemove(thread, out _);
            }
        }

        public void Stop(int exitcode)
        {
            foreach (AU3Thread thread in Threads)
                thread.Stop(exitcode);
        }

        public AU3Thread CreateNewThread() => new AU3Thread(this);

        internal void AddThread(AU3Thread thread) => _threads.TryAdd(thread, default);

        internal void RemoveThread(AU3Thread thread) => _threads.TryRemove(thread, out _);

        public void Print(CallFrame current_frame, Variant value) => Print(current_frame, value as object);

        public void Print(CallFrame current_frame, object? value) => PrintScriptMessage(current_frame.CurrentThread.CurrentLocation?.FileName, value?.ToString() ?? "");

        public InterpreterResult Run(ScriptFunction entry_point, Variant[] args)
        {
            try
            {
                using AU3Thread thread = CreateNewThread();

                lock (_main_thread_mutex)
                    MainThread = thread;

                return thread.Start(entry_point, args).Match(success => new InterpreterResult((int)success.ToNumber()), error => new InterpreterResult(-1, error));
            }
            finally
            {
                lock (_main_thread_mutex)
                    MainThread = null;
            }
        }

        public InterpreterResult Run(ScannedScript script) => Run(script.MainFunction, Array.Empty<Variant>());

        public InterpreterResult Run(string path) => ScriptScanner.ScanScriptFile(SourceLocation.Unknown, path, ScriptScanningOptions.IncludeOnce | ScriptScanningOptions.RelativePath)
                                                                  .Match(err => new InterpreterResult(-1, err), Run);

        public static InterpreterResult Run(CommandLineOptions opt)
        {
            using Interpreter interpreter = new Interpreter(opt);

            return interpreter.Run(opt.FilePath);
        }
    }

    public sealed class InterpreterResult
    {
        public static InterpreterResult OK { get; } = new InterpreterResult(0, null);

        public int ProgramExitCode { get; }

        public InterpreterError? OptionalError { get; }

        public bool IsOK => OptionalError is null && ProgramExitCode == 0;


        public InterpreterResult(int programExitCode, InterpreterError? err = null)
        {
            ProgramExitCode = programExitCode;
            OptionalError = err;
        }

        public static implicit operator InterpreterResult?(InterpreterError? err) => err is null ? null : new InterpreterResult(-1, err);
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
}

