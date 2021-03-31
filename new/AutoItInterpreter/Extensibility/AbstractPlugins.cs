using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;

[assembly: AutoIt3Plugin]

namespace Unknown6656.AutoIt3.Extensibility
{
    /// <summary>
    /// This attribute is used on an assembly to notify the <see cref="PluginLoader"/> that the marked assembly may contain classes deriving from <see cref="AbstractInterpreterPlugin"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
    public sealed class AutoIt3PluginAttribute
        : Attribute
    {
    }

    public enum PluginCategory
    {
        Unkown,
        DirectiveProcessor,
        StatementProcessor,
        LineProcessor,
        IncludeResolver,
        PragmaProcessor,
        FunctionProvider,
        MacroProvider,
    }

    /// <summary>
    /// The base class for all interpreter plugins.
    /// </summary>
    public abstract class AbstractInterpreterPlugin
    {
        /// <summary>
        /// The interpreter which has loaded the current plugin instance.
        /// </summary>
        public Interpreter Interpreter { get; }

        /// <summary>
        /// Returns the semantic category of this plugin.
        /// </summary>
        public PluginCategory PluginCategory { get; }

        /// <summary>
        /// Returns the file location, from which the plugin has been loaded.
        /// </summary>
        public FileInfo Location => Interpreter.PluginLoader._plugin_locations[this];

        /// <summary>
        /// Returns the type name of the plugin.
        /// </summary>
        public string TypeName => GetType().Name;


        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="interpreter">The interpreter which has loaded the current plugin instance.</param>
        protected AbstractInterpreterPlugin(Interpreter interpreter, PluginCategory category)
        {
            Interpreter = interpreter;
            PluginCategory = category;
        }

        public override string ToString() => $"{GetType().Assembly.Location}: {TypeName} ({PluginCategory})";
    }

    public abstract class AbstractDirectiveProcessor
        : AbstractInterpreterPlugin
    {
        protected AbstractDirectiveProcessor(Interpreter interpreter)
            : base(interpreter, PluginCategory.DirectiveProcessor)
        {
        }

        public abstract FunctionReturnValue? TryProcessDirective(CallFrame frame, string directive, string arguments);
    }

    public abstract class AbstractStatementProcessor
        : AbstractInterpreterPlugin
    {
        public abstract Regex Regex { get; }


        protected AbstractStatementProcessor(Interpreter interpreter)
            : base(interpreter, PluginCategory.StatementProcessor)
        {
        }

        public abstract FunctionReturnValue ProcessStatement(CallFrame frame, string statement);
    }

    public abstract class AbstractLineProcessor
        : AbstractInterpreterPlugin
    {
        protected AbstractLineProcessor(Interpreter interpreter)
            : base(interpreter, PluginCategory.LineProcessor)
        {
        }

        public abstract bool CanProcessLine(string line);

        public abstract FunctionReturnValue ProcessLine(CallFrame frame, string line);
    }

    public abstract class AbstractIncludeResolver
        : AbstractInterpreterPlugin
    {
        /// <summary>
        /// The resolver's relative importance between 0 and 1. (0 = low, 1 = high)
        /// </summary>
        public abstract float RelativeImportance { get; }


        protected AbstractIncludeResolver(Interpreter interpreter)
            : base(interpreter, PluginCategory.IncludeResolver)
        {
        }

        public abstract bool TryResolve(string path, [MaybeNullWhen(false), NotNullWhen(true)] out (FileInfo physical_file, string content)? resolved);
    }

    public abstract class AbstractPragmaProcessor
        : AbstractInterpreterPlugin
    {
        public abstract string PragmaName { get; }


        protected AbstractPragmaProcessor(Interpreter interpreter)
            : base(interpreter, PluginCategory.PragmaProcessor)
        {
        }

        public abstract bool CanProcessPragmaKey(string key);

        public abstract InterpreterError? ProcessPragma(SourceLocation loc, string key, string? value);
    }

    public abstract class AbstractFunctionProvider
        : AbstractInterpreterPlugin
    {
        private readonly Dictionary<string, NativeFunction> _known_functions;


        public NativeFunction[] ProvidedFunctions => _known_functions.Values.ToArray();


        protected AbstractFunctionProvider(Interpreter interpreter)
            : base(interpreter, PluginCategory.FunctionProvider) => _known_functions = new(StringComparer.InvariantCultureIgnoreCase);

        public FunctionReturnValue? TryExecute(string name, NativeCallFrame frame, Variant[] args)
        {
            NativeFunction? function = null;

            return _known_functions?.TryGetValue(name, out function) is true ? (function?.Execute(frame, args)) : null;
        }

        protected void RegisterFunction(string name, int param_count, Func<CallFrame, Variant[], FunctionReturnValue> @delegate) =>
            RegisterFunction(name, param_count, @delegate, OS.Any);

        protected void RegisterFunction(string name, int param_count, Func<CallFrame, Variant[], FunctionReturnValue> @delegate, OS os) =>
            _known_functions[name] = NativeFunction.FromDelegate(Interpreter, name, param_count, @delegate, os);

        protected void RegisterFunction(string name, int min, int max, Func<CallFrame, Variant[], FunctionReturnValue> @delegate, params Variant[] defaults) =>
            _known_functions[name] = NativeFunction.FromDelegate(Interpreter, name, min, max, @delegate, defaults);

        protected void RegisterFunction(string name, int min, int max, Func<CallFrame, Variant[], FunctionReturnValue> @delegate, OS os, params Variant[] defaults) =>
            _known_functions[name] = NativeFunction.FromDelegate(Interpreter, name, min, max, @delegate, os, defaults);
    }

    public abstract class AbstractMacroProvider
        : AbstractInterpreterPlugin
    {
        protected AbstractMacroProvider(Interpreter interpreter)
            : base(interpreter, PluginCategory.MacroProvider)
        {
        }

        public abstract bool ProvideMacroValue(CallFrame frame, string name, out (Variant value, Metadata metadata)? macro);
    }

    public abstract class AbstractKnownMacroProvider
        : AbstractMacroProvider
    {
        internal readonly Dictionary<string, (Func<CallFrame, Variant> function, Metadata metadata)> _known_macros;


        public AbstractKnownMacroProvider(Interpreter interpreter)
            : base(interpreter) => _known_macros = new(StringComparer.InvariantCultureIgnoreCase);

        protected void RegisterMacro(string name, Variant value) => RegisterMacro(name, _ => value);

        protected void RegisterMacro(string name, Variant value, Metadata metadata) => RegisterMacro(name, _ => value, metadata);

        protected void RegisterMacro(string name, Func<CallFrame, Variant> provider) => RegisterMacro(name, provider, Metadata.Default);

        protected void RegisterMacro(string name, Func<CallFrame, Variant> provider, Metadata metadata) => _known_macros[name] = (provider, metadata);

        public override bool ProvideMacroValue(CallFrame frame, string name, out (Variant value, Metadata metadata)? macro)
        {
            macro = null;
            name = name.TrimStart('@');

            if (_known_macros.TryGetValue(name, out (Func<CallFrame, Variant> provider, Metadata metadata) value))
            {
                macro = (value.provider(frame), value.metadata);

                return true;
            }

            return false;
        }
    }
}
