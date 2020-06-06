using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Mathematics.LinearAlgebra;
using Unknown6656.AutoIt3.Runtime;

namespace Unknown6656.AutoIt3.Extensibility
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = true)]
    public sealed class AutoIt3Plugin
        : Attribute
    {
    }

    public sealed class PluginLoader
    {
        private readonly List<AbstractLineProcessor> _line_processors = new List<AbstractLineProcessor>();
        private readonly List<AbstractDirectiveProcessor> _directive_processors = new List<AbstractDirectiveProcessor>();
        private readonly List<AbstractStatementProcessor> _statement_processors = new List<AbstractStatementProcessor>();
        private readonly List<AbstractPragmaProcessor> _pragma_processors = new List<AbstractPragmaProcessor>();
        private readonly List<AbstractIncludeResolver> _resolvers = new List<AbstractIncludeResolver>();
        private readonly List<FileInfo> _plugin_files = new List<FileInfo>();


        public Interpreter Interpreter { get; }

        public DirectoryInfo PluginDirectory { get; }

        public IReadOnlyList<FileInfo> LoadedPlugins => _plugin_files;

        public IReadOnlyList<AbstractLineProcessor> LineProcessors => _line_processors;

        public IReadOnlyList<AbstractDirectiveProcessor> DirectiveProcessors => _directive_processors;

        public IReadOnlyList<AbstractStatementProcessor> StatementProcessors => _statement_processors;

        public IReadOnlyList<AbstractPragmaProcessor> PragmaProcessors => _pragma_processors;

        public IReadOnlyList<AbstractIncludeResolver> IncludeResolvers => _resolvers.OrderByDescending(r => ((Scalar)r.RelativeImportance).Clamp()).ToList();


        // TODO : mutex ?

        public PluginLoader(Interpreter interpreter, DirectoryInfo dir)
        {
            Interpreter = interpreter;
            PluginDirectory = dir;

            if (!dir.Exists)
                dir.Create();
        }

        public override string ToString() => $"{_plugin_files.Count} plugins in \"{PluginDirectory.FullName}\"";

        public void ClearLoadedPlugins()
        {
            _plugin_files.Clear();
            _line_processors.Clear();
            _directive_processors.Clear();
            _statement_processors.Clear();
            _resolvers.Clear();
        }

        public void LoadPlugins()
        {
            if (!Program.CommandLineOptions.StrictMode)
                return;

            ClearLoadedPlugins();

            List<Type> types = new List<Type>();

            foreach (FileInfo file in PluginDirectory.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Directory })
                                                     .Append(new FileInfo(Assembly.GetExecutingAssembly().Location)))
                try
                {
                    Assembly asm = Assembly.LoadFrom(file.FullName);

                    if (asm.GetCustomAttribute<AutoIt3Plugin>() is { })
                    {
                        _plugin_files.Add(file);
                        types.AddRange(asm.GetTypes());
                    }
                }
                catch
                {
                }

            foreach (Type type in types)
                if (!type.IsAbstract)
                {
                    TryRegister<AbstractLineProcessor>(type, RegisterLineProcessor);
                    TryRegister<AbstractDirectiveProcessor>(type, RegisterDirectiveProcessor);
                    TryRegister<AbstractStatementProcessor>(type, RegisterStatementProcessor);
                    TryRegister<AbstractPragmaProcessor>(type, RegisterPragmaProcessors);
                    TryRegister<AbstractIncludeResolver>(type, RegisterIncludeResolver);
                }
        }

        private void TryRegister<T>(Type type, Action<T> register_func)
        {
            if (typeof(T).IsAssignableFrom(type) && Activator.CreateInstance(typeof(T), Interpreter) is T t)
                register_func(t);
        }

        public void RegisterLineProcessor(AbstractLineProcessor proc) => _line_processors.Add(proc);

        public void RegisterDirectiveProcessor(AbstractDirectiveProcessor proc) => _directive_processors.Add(proc);

        public void RegisterStatementProcessor(AbstractStatementProcessor proc) => _statement_processors.Add(proc);

        public void RegisterPragmaProcessors(AbstractPragmaProcessor proc) => _pragma_processors.Add(proc);

        public void RegisterIncludeResolver(AbstractIncludeResolver resolver) => _resolvers.Add(resolver);
    }

    public abstract class AbstractInterpreterPlugin
    {
        public Interpreter Interpreter { get; }


        protected AbstractInterpreterPlugin(Interpreter interpreter) => Interpreter = interpreter;
    }

    public abstract class AbstractDirectiveProcessor
        : AbstractInterpreterPlugin
    {
        protected AbstractDirectiveProcessor(Interpreter interpreter) : base(interpreter)
        {
        }

        public abstract InterpreterResult? ProcessDirective(CallFrame frame, string directive);
    }

    public abstract class AbstractStatementProcessor
        : AbstractInterpreterPlugin
    {
        public abstract string Regex { get; }


        protected AbstractStatementProcessor(Interpreter interpreter) : base(interpreter)
        {
        }

        public abstract InterpreterResult? ProcessStatement(CallFrame frame, string directive);
    }

    public abstract class AbstractLineProcessor
        : AbstractInterpreterPlugin
    {
        protected AbstractLineProcessor(Interpreter interpreter) : base(interpreter)
        {
        }

        public abstract bool CanProcessLine(string line);

        public abstract InterpreterResult? ProcessLine(CallFrame frame, string line);
    }

    public abstract class AbstractIncludeResolver
        : AbstractInterpreterPlugin
    {
        /// <summary>
        /// The resolver's relative importance between 0 and 1. (0 = low, 1 = high)
        /// </summary>
        public abstract float RelativeImportance { get; }


        protected AbstractIncludeResolver(Interpreter interpreter) : base(interpreter)
        {
        }

        public abstract bool TryResolve(string path, [MaybeNullWhen(false), NotNullWhen(true)] out (FileInfo physical_file, string content)? resolved);
    }

    public abstract class AbstractPragmaProcessor
        : AbstractInterpreterPlugin
    {
        public abstract string PragmaName { get; }


        protected AbstractPragmaProcessor(Interpreter interpreter) : base(interpreter)
        {
        }

        public abstract bool CanProcessPragmaKey(string key);

        public abstract InterpreterError? ProcessPragma(SourceLocation loc, string key, string? value);
    }
}
