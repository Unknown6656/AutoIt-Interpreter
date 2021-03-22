using System.Collections.Immutable;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Mathematics.LinearAlgebra;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.AutoIt3.CLI;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Extensibility
{
    public sealed class PluginLoader
    {
        internal readonly Dictionary<AbstractInterpreterPlugin, FileInfo> _plugin_locations = new();

        private readonly List<AbstractLineProcessor> _line_processors = new();
        private readonly List<AbstractDirectiveProcessor> _directive_processors = new();
        private readonly List<AbstractStatementProcessor> _statement_processors = new();
        private readonly List<AbstractPragmaProcessor> _pragma_processors = new();
        private readonly List<AbstractFunctionProvider> _func_providers = new();
        private readonly List<AbstractIncludeResolver> _resolvers = new();
        private readonly List<AbstractMacroProvider> _macro_providers = new();
        private readonly HashSet<FileInfo> _plugin_files = new();


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

        public ImmutableHashSet<FileInfo> LoadedPluginFiles => _plugin_files.ToImmutableHashSet();

        public AbstractInterpreterPlugin[] LoadedPlugins => _line_processors.Cast<AbstractInterpreterPlugin>()
                                                                            .Concat(_directive_processors)
                                                                            .Concat(_statement_processors)
                                                                            .Concat(_pragma_processors)
                                                                            .Concat(_func_providers)
                                                                            .Concat(_resolvers)
                                                                            .Concat(_macro_providers)
                                                                            .ToArray();

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

        /// <inheritdoc/>
        public override string ToString() => Interpreter.CurrentUILanguage["debug.plugins_loaded", _plugin_files.Count, Path.GetFullPath(PluginDirectory.FullName), PluginModuleCount];

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

            List<(Type Type, FileInfo PluginLocation)> types = new();
            IEnumerable<FileInfo> assemblies = MainProgram.CommandLineOptions.StrictMode ? Array.Empty<FileInfo>() : PluginDirectory.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Directory });

            foreach (FileInfo file in assemblies.Append(MainProgram.ASM_FILE))
                try
                {
                    Interpreter.Telemetry.Measure(TelemetryCategory.LoadPluginFile, delegate
                    {
                        Assembly asm = Assembly.LoadFrom(file.FullName);

                        if (asm.GetCustomAttribute<AutoIt3PluginAttribute>() is { })
                        {
                            _plugin_files.Add(file);
                            asm.GetTypes().Do(t => types.Add((t, file)));
                        }
                    });
                }
                catch
                {
                    Interpreter.Telemetry.Measure(TelemetryCategory.Exceptions, delegate { });
                }

            foreach ((Type type, FileInfo location) in types)
                if (!type.IsAbstract && typeof(AbstractInterpreterPlugin).IsAssignableFrom(type))
                    Interpreter.Telemetry.Measure(TelemetryCategory.LoadPlugin, delegate
                    {
                        TryRegister(type, location, _line_processors);
                        TryRegister(type, location, _directive_processors);
                        TryRegister(type, location, _statement_processors);
                        TryRegister(type, location, _pragma_processors);
                        TryRegister(type, location, _resolvers);
                        TryRegister(type, location, _func_providers);
                        TryRegister(type, location, _macro_providers);
                    });

            foreach (AbstractMacroProvider plugin in _macro_providers)
                if (plugin is AbstractKnownMacroProvider provider)
                    foreach ((string name, (Func<CallFrame, Variant> func, Metadata meta)) in provider._known_macros)
                        Interpreter.MacroResolver.AddKnownMacro(new KnownMacro(Interpreter, name, func) { Metadata = meta });
        }

        private void TryRegister<T>(Type type, FileInfo location, List<T> plugin_list)
            where T : AbstractInterpreterPlugin
        {
            if (typeof(T).IsAssignableFrom(type))
            {
                T plugin = (T)Activator.CreateInstance(type, Interpreter)!;
                _plugin_locations[plugin] = location;

                if (_plugin_files.Contains(location))
                    _plugin_files.Add(location);

                plugin_list.Add(plugin);
            }
        }
    }
}
