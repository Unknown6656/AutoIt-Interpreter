﻿using System.Linq;
using System;

using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime.ExternalServices;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Localization;
using Unknown6656.AutoIt3.CLI;

using Unknown6656.Mathematics.Numerics;
using Unknown6656.Common;

using Random = Unknown6656.Mathematics.Numerics.Random;

namespace Unknown6656.AutoIt3.Runtime
{
    using static AST;

    /// <summary>
    /// Represents the core management logic of the AutoIt interpreter.
    /// <br/>
    /// The interpreter consists of threads, the global object storage, service connector, telemetry parser, the plugin loading system etc.
    /// </summary>
    public sealed class Interpreter
        : IDisposable
        , IDebugPrintingService
    {
        private static readonly ConcurrentHashSet<Interpreter> _instances = new();

        private readonly ConcurrentHashSet<AU3Thread> _threads = new();
        private readonly object _main_thread_mutex = new object();
        private volatile int _error;


        /// <summary>
        /// <b>[UNSAFE!]</b>
        /// Returns a collection of currently active interpreter instances.
        /// </summary>
        public static Interpreter[] ActiveInstances => _instances.ToArray();

        /// <summary>
        /// The interpreter's random number generator. The used generator can be reset and seeded using the methods <see cref="ResetRandom"/> and <see cref="ResetRandom(int)"/>.
        /// </summary>
        public Random Random { get; private set; }

        /// <summary>
        /// The interpreter's main thread.
        /// </summary>
        public AU3Thread? MainThread { get; private set; }

        /// <summary>
        /// A collection of currently active threads.
        /// </summary>
        public AU3Thread[] Threads => _threads.ToArray();

        /// <summary>
        /// The interpreter's <i>global</i> <see cref="VariableScope"/>. This scope contains all global variables.
        /// </summary>
        public VariableScope VariableResolver { get; }

        /// <summary>
        /// The command line options used to launch this interpreter.
        /// </summary>
        public CommandLineOptions CommandLineOptions { get; }

        /// <summary>
        /// The interpreter's global object storage.
        /// </summary>
        public GlobalObjectStorage GlobalObjectStorage { get; }

        public COMConnector? COMConnector { get; }

        /// <summary>
        /// Indicates whether COM interoperability is available to the interpreter.
        /// </summary>
        public bool IsCOMAvailable => COMConnector is { };

        //public WinAPIConnector? Win32APIConnector { get; }

        //public GUIConnector GUIConnector { get; }

        //public bool IsWin32APIAvailable => Win32APIConnector is { };

        /// <summary>
        /// The interpreter's script scanner and caching unit.
        /// </summary>
        public ScriptScanner ScriptScanner { get; }

        public PluginLoader PluginLoader { get; }

        public ParserProvider ParserProvider { get; }

        /// <summary>
        /// The interpreter's telemetry logger.
        /// </summary>
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


        /// <summary>
        /// Creates a new interpreter instance using the given command line options and language pack loader.
        /// </summary>
        /// <param name="opt">Command line options to initialize the interpreter.</param>
        /// <param name="lang_loader">Language pack loader instance.</param>
        public Interpreter(CommandLineOptions opt, LanguageLoader lang_loader)
            : this(opt, new Telemetry(), lang_loader)
        {
        }

        /// <summary>
        /// Creates a new interpreter instance using the given command line options, telemetry logger, and language pack loader.
        /// </summary>
        /// <param name="opt">Command line options to initialize the interpreter.</param>
        /// <param name="telemetry">Existing telemetry logger instance.</param>
        /// <param name="lang_loader">Language pack loader instance.</param>
        public Interpreter(CommandLineOptions opt, Telemetry telemetry, LanguageLoader lang_loader)
        {
            _instances.Add(this);

            CommandLineOptions = opt;
            Telemetry = telemetry;
            LanguageLoader = lang_loader;
            ScriptScanner = new ScriptScanner(this);
            PluginLoader = new PluginLoader(this, MainProgram.PLUGIN_DIR);

            if (!opt.DontLoadPlugins)
                PluginLoader.LoadPlugins();

            if (PluginLoader.LoadedPluginFiles.Count is int i and > 0)
                MainProgram.PrintfDebugMessage("general.plugins_loaded", i, PluginLoader.PluginDirectory.FullName, PluginLoader.PluginModuleCount);
            else
                MainProgram.PrintfDebugMessage("general.no_plugins_loaded");

            ParserProvider = new ParserProvider(this);

            ScriptScanner.ScanNativeFunctions();

            GlobalObjectStorage = new GlobalObjectStorage(this);
            VariableResolver = VariableScope.CreateGlobalScope(this);
            VariableResolver.CreateVariable(SourceLocation.Unknown, VARIABLE.Discard.Name, false);
            VariableResolver.CreateVariable(SourceLocation.Unknown, "$CmdLineRaw", true).Value = Variant.FromString(opt.ScriptArguments.StringJoin(" "));
            VariableResolver.CreateVariable(SourceLocation.Unknown, "$CmdLine", true).Value = Variant.FromArray(this, opt.ScriptArguments.Select(Variant.FromString).Prepend(Variant.FromNumber(opt.ScriptArguments.Length)));

            if (NativeInterop.OperatingSystem is Native.OS.Windows)
            {
                COMConnector = new COMConnector(this);
                //Win32APIConnector = new WinAPIConnector(this);
            }

            //GUIConnector = new GUIConnector(this);

            Random = new BuiltinRandom();
            ResetRandom();
        }

        /// <summary>
        /// Resets the random number generator (<see cref="Random"/>) to a new non-deterministic seed.
        /// </summary>
        public void ResetRandom() => ResetRandom(Guid.NewGuid().GetHashCode());

        /// <summary>
        /// Resets the random number generator (<see cref="Random"/>) to the given seed.
        /// </summary>
        /// <param name="seed">New seed</param>
        public void ResetRandom(int seed)
        {
            lock (_main_thread_mutex)
                Random = new XorShift(seed); // BuiltinRandom
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (AU3Thread thread in Threads)
            {
                thread.Dispose();
                _threads.Remove(thread);
            }

            _threads.Dispose();
            GlobalObjectStorage.Dispose();
            // GUIConnector.Dispose();
            COMConnector?.Dispose();
            //Win32APIConnector?.Dispose();

            _instances.Remove(this);
        }

        /// <inheritdoc/>
        public override string ToString() => $"[{_threads.Count} Threads] {CommandLineOptions}";

        /// <summary>
        /// Halts the interpreter and all currently running threads with the given exit code.
        /// </summary>
        /// <param name="exitcode">Exit code.</param>
        public void Stop(int exitcode)
        {
            foreach (AU3Thread thread in Threads)
                thread.Stop(exitcode);
        }

        /// <summary>
        /// Creates a new thread. Use <see cref="AU3Thread.Start(ScriptFunction, Variant[])"/> on the returned thread to invoke a method asynchronously from the current thread.
        /// </summary>
        /// <returns>The newly created thread.</returns>
        public AU3Thread CreateNewThread() => new AU3Thread(this);

        internal void AddThread(AU3Thread thread) => _threads.Add(thread);

        internal void RemoveThread(AU3Thread thread) => _threads.Remove(thread);

        /// <summary>
        /// Prints the given value to the STDOUT stream.
        /// The value will be printed under the name of the script associated with the given call frame (only relevant if the <see cref="CommandLineOptions.Verbose"/>-property of <see cref="CommandLineOptions"/> is configured to be higher than <see cref="Verbosity.q"/>).
        /// <para/>
        /// <i>Note:</i> No new line will be appended if the <see cref="CommandLineOptions.Verbose"/>-property of <see cref="CommandLineOptions"/> has the value <see cref="Verbosity.q"/>.
        /// </summary>
        /// <param name="current_frame">Call frame which invoked the print request.</param>
        /// <param name="value">Value to be printed.</param>
        public void Print(CallFrame current_frame, Variant value) => Print(current_frame, value as object);

        /// <summary>
        /// Prints the given value to the STDOUT stream.
        /// The value will be printed under the name of the script associated with the given call frame (only relevant if the <see cref="CommandLineOptions.Verbose"/>-property of <see cref="CommandLineOptions"/> is configured to be higher than <see cref="Verbosity.q"/>).
        /// <para/>
        /// <i>Note:</i> No new line will be appended if the <see cref="CommandLineOptions.Verbose"/>-property of <see cref="CommandLineOptions"/> has the value <see cref="Verbosity.q"/>.
        /// </summary>
        /// <param name="current_frame">Call frame which invoked the print request.</param>
        /// <param name="value">Value to be printed.</param>
        public void Print(CallFrame current_frame, object? value) => MainProgram.PrintScriptMessage(current_frame.CurrentThread.CurrentLocation?.FullFileName, value?.ToString() ?? "");

        /// <inheritdoc/>
        void IDebugPrintingService.Print(string channel, string message) => MainProgram.PrintChannelMessage(channel, message);

        /// <summary>
        /// Runs the global function of the script file, with which the interpreter has been initialized
        /// (This essentially executes the script stored in <see cref="CommandLineOptions.FilePath"/> of the interpreter's <see cref="CommandLineOptions"/>-property),
        /// </summary>
        /// <returns>The interpreter result of the script invocation.</returns>
        public InterpreterResult Run() => CommandLineOptions.FilePath is string s ? Run(s) : new InterpreterResult(-1, InterpreterError.WellKnown(null, "error.unresolved_script", "<null>"));

        /// <summary>
        /// Creates a new (anonymous) interpreter, which invokes the given function with the given arguments.
        /// </summary>
        /// <param name="entry_point">The function (entry point) to be invoked.</param>
        /// <param name="args">Arguments to be passed to the invoked function.</param>
        /// <returns>The interpreter result of the function invocation.</returns>
        public InterpreterResult Run(ScriptFunction entry_point, Variant[] args)
        {
            try
            {
                using AU3Thread thread = CreateNewThread();

                lock (_main_thread_mutex)
                    MainThread = thread;

                FunctionReturnValue result = thread.Start(entry_point, args);
                InterpreterError? error = null;

                result.IfNonFatal((ret, err, _) => Variant.FromNumber(ExitCode = err ?? (int)ret)).IsFatal(out error);

                return error is null ? new(ExitCode) : new(-1, error);
            }
            finally
            {
                lock (_main_thread_mutex)
                    MainThread = null;
            }
        }

        /// <summary>
        /// Creates a new (anonymous) interpreter, which invokes the global function of the given script. This essentially executes the given script.
        /// </summary>
        /// <param name="script">The script to be executed.</param>
        /// <returns>The interpreter result of the script invocation.</returns>
        public InterpreterResult Run(ScannedScript script) => Run(script.MainFunction, Array.Empty<Variant>());

        /// <summary>
        /// Creates a new (anonymous) interpreter, which invokes the global function of the given script. This essentially executes the given script.
        /// </summary>
        /// <param name="path">The path of the script to be executed.</param>
        /// <returns>The interpreter result of the script invocation.</returns>
        public InterpreterResult Run(string path) => ScriptScanner.ScanScriptFile(SourceLocation.Unknown, path, false).Match(err => new InterpreterResult(-1, err), Run);
    }
}
