using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Mathematics.LinearAlgebra;
using Unknown6656.AutoIt3.Runtime;

namespace Unknown6656.AutoIt3.Extensibility
{
    public sealed class PluginLoader
    {
        private readonly List<AbstractLineProcessor> _line_processors = new List<AbstractLineProcessor>();
        private readonly List<AbstractDirectiveProcessor> _directive_processors = new List<AbstractDirectiveProcessor>();
        private readonly List<AbstractStatementProcessor> _statement_processors = new List<AbstractStatementProcessor>();
        private readonly List<AbstractPragmaProcessor> _pragma_processors = new List<AbstractPragmaProcessor>();
        private readonly List<AbstractFunctionProvider> _func_providers = new List<AbstractFunctionProvider>();
        private readonly List<AbstractIncludeResolver> _resolvers = new List<AbstractIncludeResolver>();
        private readonly List<AbstractMacroProvider> _macro_providers = new List<AbstractMacroProvider>();
        private readonly List<FileInfo> _plugin_files = new List<FileInfo>();


        public Interpreter Interpreter { get; }

        public DirectoryInfo PluginDirectory { get; }

        public int PluginModuleCount => new IEnumerable<AbstractInterpreterPlugin>[]
        {
            _line_processors,
            _directive_processors,
            _statement_processors,
            _pragma_processors,
            _func_providers,
            _resolvers,
            _macro_providers,
        }.Sum(p => p.Count());

        public IReadOnlyList<FileInfo> LoadedPlugins => _plugin_files;

        public IReadOnlyList<AbstractLineProcessor> LineProcessors => _line_processors;

        public IReadOnlyList<AbstractDirectiveProcessor> DirectiveProcessors => _directive_processors;

        public IReadOnlyList<AbstractStatementProcessor> StatementProcessors => _statement_processors;

        public IReadOnlyList<AbstractPragmaProcessor> PragmaProcessors => _pragma_processors;

        public IReadOnlyList<AbstractFunctionProvider> FunctionProviders => _func_providers;

        public IReadOnlyList<AbstractIncludeResolver> IncludeResolvers => _resolvers.OrderByDescending(r => ((Scalar)r.RelativeImportance).Clamp()).ToList();

        public IReadOnlyList<AbstractMacroProvider> MacroProviders => _macro_providers;


        public PluginLoader(Interpreter interpreter, DirectoryInfo dir)
        {
            Interpreter = interpreter;
            PluginDirectory = dir;

            if (!dir.Exists)
                dir.Create();
        }

        public override string ToString() => $"{_plugin_files.Count} plugin files found in \"{PluginDirectory.FullName}\" ({PluginModuleCount} modules)";

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

            List<Type> types = new List<Type>();
            IEnumerable<FileInfo> assemblies = Program.CommandLineOptions.StrictMode ? Array.Empty<FileInfo>() : PluginDirectory.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Directory });

            foreach (FileInfo file in assemblies.Append(Program.ASM))
                try
                {
                    Assembly asm = Assembly.LoadFrom(file.FullName);

                    if (asm.GetCustomAttribute<AutoIt3PluginAttribute>() is { })
                    {
                        _plugin_files.Add(file);
                        types.AddRange(asm.GetTypes());
                    }
                }
                catch
                {
                }

            foreach (Type type in types)
                if (!type.IsAbstract && typeof(AbstractInterpreterPlugin).IsAssignableFrom(type))
                {
                    TryRegister<AbstractLineProcessor>(type, RegisterLineProcessor);
                    TryRegister<AbstractDirectiveProcessor>(type, RegisterDirectiveProcessor);
                    TryRegister<AbstractStatementProcessor>(type, RegisterStatementProcessor);
                    TryRegister<AbstractPragmaProcessor>(type, RegisterPragmaProcessors);
                    TryRegister<AbstractIncludeResolver>(type, RegisterIncludeResolver);
                    TryRegister<AbstractFunctionProvider>(type, RegisterFunctionProvider);
                    TryRegister<AbstractMacroProvider>(type, RegisterMacroProvider);
                }
        }

        private void TryRegister<T>(Type type, Action<T> register_func)
            where T : AbstractInterpreterPlugin
        {
            if (typeof(T).IsAssignableFrom(type))
                register_func((T)Activator.CreateInstance(type, Interpreter)!);
        }

        public void RegisterLineProcessor(AbstractLineProcessor proc) => _line_processors.Add(proc);

        public void RegisterDirectiveProcessor(AbstractDirectiveProcessor proc) => _directive_processors.Add(proc);

        public void RegisterStatementProcessor(AbstractStatementProcessor proc) => _statement_processors.Add(proc);

        public void RegisterPragmaProcessors(AbstractPragmaProcessor proc) => _pragma_processors.Add(proc);

        public void RegisterIncludeResolver(AbstractIncludeResolver resolver) => _resolvers.Add(resolver);

        public void RegisterFunctionProvider(AbstractFunctionProvider provider) => _func_providers.Add(provider);

        public void RegisterMacroProvider(AbstractMacroProvider provider) => _macro_providers.Add(provider);
    }
}
