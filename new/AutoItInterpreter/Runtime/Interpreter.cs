using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime.ExternalServices;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Localization;
using Unknown6656.Mathematics.Numerics;
using Unknown6656.Common;

using Random = Unknown6656.Mathematics.Numerics.Random;

namespace Unknown6656.AutoIt3.Runtime
{
    using static AST;


    public sealed class Interpreter
        : IDisposable
        , IDebugPrintingService
    {
        private readonly ConcurrentDictionary<AU3Thread, __empty> _threads = new ConcurrentDictionary<AU3Thread, __empty>();
        private readonly object _main_thread_mutex = new object();
        private volatile int _error;


        public Random Random { get; private set; }

        public AU3Thread? MainThread { get; private set; }

        public AU3Thread[] Threads => _threads.Keys.ToArray();

        public VariableScope VariableResolver { get; }

        public CommandLineOptions CommandLineOptions { get; }

        public GlobalObjectStorage GlobalObjectStorage { get; }

        public COMConnector? COMConnector { get; }

        //public GUIConnector GUIConnector { get; }

        public bool IsCOMAvailable => COMConnector is { };

        public ScriptScanner ScriptScanner { get; }

        public PluginLoader PluginLoader { get; }

        public ParserProvider ParserProvider { get; }

        public Telemetry Telemetry { get; }

        public LanguageLoader LanguageLoader { get; }

        public LanguagePack CurrentUILanguage => LanguageLoader.CurrentLanguage!;

        public int ExitCode { get; private set; } = 0;

        public int ErrorCode
        {
            get => _error;
            set => _error = value;
        }

        public Variant ExtendedValue { get; set; }


        public Interpreter(CommandLineOptions opt, LanguageLoader lang_loader)
            : this(opt, new Telemetry(), lang_loader)
        {
        }

        public Interpreter(CommandLineOptions opt, Telemetry telemetry, LanguageLoader lang_loader)
        {
            CommandLineOptions = opt;
            Telemetry = telemetry;
            LanguageLoader = lang_loader;
            ScriptScanner = new ScriptScanner(this);
            PluginLoader = new PluginLoader(this, MainProgram.PLUGIN_DIR);

            if (!opt.DontLoadPlugins)
                PluginLoader.LoadPlugins();

            if (PluginLoader.LoadedPlugins.Count is int i and > 0)
                MainProgram.PrintInterpreterMessage("general.plugins_loaded", i, PluginLoader.PluginDirectory.FullName, PluginLoader.PluginModuleCount);
            else
                MainProgram.PrintInterpreterMessage("general.no_plugins_loaded");

            ParserProvider = new ParserProvider(this);

            ScriptScanner.ScanNativeFunctions();

            GlobalObjectStorage = new GlobalObjectStorage(this);
            VariableResolver = VariableScope.CreateGlobalScope(this);
            VariableResolver.CreateVariable(SourceLocation.Unknown, VARIABLE.Discard.Name, false);

            //GUIConnector = new GUIConnector(this);

            if (NativeInterop.OperatingSystem is Native.OperatingSystem.Windows)
                COMConnector = new COMConnector(this);

            Random = new BuiltinRandom();
            ResetRandom();
        }

        public void ResetRandom() => ResetRandom(Guid.NewGuid().GetHashCode());

        public void ResetRandom(int seed)
        {
            lock (_main_thread_mutex)
                Random = new XorShift(seed); // BuiltinRandom
        }

        public void Dispose()
        {
            foreach (AU3Thread thread in Threads)
            {
                thread.Dispose();
                _threads.TryRemove(thread, out _);
            }

            GlobalObjectStorage.Dispose();
            GUIConnector.Dispose();
            COMConnector?.Dispose();
        }

        public void Stop(int exitcode)
        {
            foreach (AU3Thread thread in Threads)
                thread.Stop(exitcode);
        }

        public void AddFolderToEnvPath(string dir)
        {
            char separator = NativeInterop.DoPlatformDependent(';', ':');
            List<string> path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process)?
                                           .Split(separator, StringSplitOptions.RemoveEmptyEntries)
                                           .ToList() ?? new();

            path.Add(dir);

            Environment.SetEnvironmentVariable("PATH", string.Join(separator, path.Distinct()), EnvironmentVariableTarget.Process);
        }

        public AU3Thread CreateNewThread() => new AU3Thread(this);

        internal void AddThread(AU3Thread thread) => _threads.TryAdd(thread, default);

        internal void RemoveThread(AU3Thread thread) => _threads.TryRemove(thread, out _);

        public void Print(CallFrame current_frame, Variant value) => Print(current_frame, value as object);

        public void Print(CallFrame current_frame, object? value) => MainProgram.PrintScriptMessage(current_frame.CurrentThread.CurrentLocation?.FullFileName, value?.ToString() ?? "");

        void IDebugPrintingService.Print(string channel, string message) => MainProgram.PrintChannelMessage(channel, message);

        public InterpreterResult Run(ScriptFunction entry_point, Variant[] args)
        {
            try
            {
                using AU3Thread thread = CreateNewThread();

                lock (_main_thread_mutex)
                    MainThread = thread;

                InterpreterResult result = thread.Start(entry_point, args).Match(error => new InterpreterResult(-1, error), success => new InterpreterResult((int)success.ToNumber()));

                ExitCode = result.ProgramExitCode;

                return result;
            }
            finally
            {
                lock (_main_thread_mutex)
                    MainThread = null;
            }
        }

        public InterpreterResult Run(ScannedScript script) => Run(script.MainFunction, Array.Empty<Variant>());

        public InterpreterResult Run(string path) => ScriptScanner.ScanScriptFile(SourceLocation.Unknown, path, false).Match(err => new InterpreterResult(-1, err), Run);

        public InterpreterResult Run() => CommandLineOptions.FilePath is string s ? Run(s) : new InterpreterResult(-1, InterpreterError.WellKnown(null, "error.unresolved_script", "<null>"));
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
        public static InterpreterError Empty = new InterpreterError(SourceLocation.Unknown, "");

        public SourceLocation? Location { get; }
        public string Message { get; }


        public InterpreterError(SourceLocation? location, string message)
        {
            Location = location;
            Message = message;
        }

        public static InterpreterError WellKnown(SourceLocation? loc, string key, params object?[] args) => new InterpreterError(loc, MainProgram.LanguageLoader.CurrentLanguage?[key, args] ?? key);
    }
}

