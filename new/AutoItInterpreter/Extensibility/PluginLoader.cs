using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System;

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
        private readonly List<ILineProcessor> _line_processors = new List<ILineProcessor>();
        private readonly List<IDirectiveProcessor> _directive_processors = new List<IDirectiveProcessor>();
        private readonly List<IStatementProcessor> _statement_processors = new List<IStatementProcessor>();
        private readonly List<IIncludeResolver> _resolvers = new List<IIncludeResolver>();
        private readonly List<FileInfo> _plugin_files = new List<FileInfo>();


        public DirectoryInfo PluginDirectory { get; }

        public IReadOnlyList<FileInfo> LoadedPlugins => _plugin_files;

        public IReadOnlyList<ILineProcessor> LineProcessors => _line_processors;

        public IReadOnlyList<IDirectiveProcessor> DirectiveProcessors => _directive_processors;

        public IReadOnlyList<IStatementProcessor> StatementProcessors => _statement_processors;

        public IReadOnlyList<IIncludeResolver> IncludeResolvers => _resolvers;


        // TODO : mutex ?

        public PluginLoader(DirectoryInfo dir)
        {
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
            ClearLoadedPlugins();

            foreach (FileInfo file in PluginDirectory.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Directory }))
                try
                {
                    Assembly asm = Assembly.LoadFrom(file.FullName);

                    if (asm.GetCustomAttribute<AutoIt3Plugin>() is { })
                    {
                        _plugin_files.Add(file);

                        foreach (Type type in asm.GetTypes())
                        {
                            TryRegister<ILineProcessor>(type, RegisterLineProcessor);
                            TryRegister<IDirectiveProcessor>(type, RegisterDirectiveProcessor);
                            TryRegister<IStatementProcessor>(type, RegisterStatementProcessor);
                            TryRegister<IIncludeResolver>(type, RegisterIncludeResolver);
                        }
                    }
                }
                catch
                {
                }
        }

        private void TryRegister<T>(Type type, Action<T> register_func)
        {
            if (typeof(T).IsAssignableFrom(type))
                register_func(Activator.CreateInstance<T>());
        }

        public void RegisterLineProcessor(ILineProcessor proc) => _line_processors.Add(proc);

        public void RegisterDirectiveProcessor(IDirectiveProcessor proc) => _directive_processors.Add(proc);

        public void RegisterStatementProcessor(IStatementProcessor proc) => _statement_processors.Add(proc);

        public void RegisterIncludeResolver(IIncludeResolver resolver) => _resolvers.Add(resolver);
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
