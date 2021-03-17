using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

using Unknown6656.AutoIt3.Extensibility;
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
        protected AbstractInterpreterPlugin(Interpreter interpreter) => Interpreter = interpreter;

        /// <inheritdoc/>
        public override string ToString()
        {
            Type t = GetType();

            return $"{t.Assembly.Location}: {t.Name}";
        }
    }

    public abstract class AbstractDirectiveProcessor
        : AbstractInterpreterPlugin
    {
        protected AbstractDirectiveProcessor(Interpreter interpreter) : base(interpreter)
        {
        }

        public abstract FunctionReturnValue? TryProcessDirective(CallFrame frame, string directive);
    }

    public abstract class AbstractStatementProcessor
        : AbstractInterpreterPlugin
    {
        public abstract Regex Regex { get; }


        protected AbstractStatementProcessor(Interpreter interpreter) : base(interpreter)
        {
        }

        public abstract FunctionReturnValue ProcessStatement(CallFrame frame, string directive);
    }

    public abstract class AbstractLineProcessor
        : AbstractInterpreterPlugin
    {
        protected AbstractLineProcessor(Interpreter interpreter) : base(interpreter)
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

    public abstract class AbstractFunctionProvider
        : AbstractInterpreterPlugin
    {
        public abstract ProvidedNativeFunction[] ProvidedFunctions { get; }


        protected AbstractFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public FunctionReturnValue? TryExecute(string name, NativeCallFrame frame, Variant[] args) =>
            ProvidedFunctions?.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.InvariantCultureIgnoreCase))?.Execute(frame, args);
    }

    public abstract class AbstractMacroProvider
        : AbstractInterpreterPlugin
    {
        protected AbstractMacroProvider(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public abstract bool ProvideMacroValue(CallFrame frame, string name, out Variant? value);
    }

    public abstract class AbstractKnownMacroProvider
        : AbstractMacroProvider
    {
        public abstract Dictionary<string, Func<CallFrame, Variant>> KnownMacros { get; }


        public AbstractKnownMacroProvider(Interpreter interpreter)
            : base(interpreter)
        {
        }

        internal void RegisterAllMacros()
        {
            foreach ((string name, Func<CallFrame, Variant> provider) in KnownMacros)
                Interpreter.MacroResolver.AddKnownMacro(new KnownMacro(Interpreter, name, provider));
        }

        public override bool ProvideMacroValue(CallFrame frame, string name, out Variant? value)
        {
            value = null;
            name = name.TrimStart('@');

            foreach ((string key, Func<CallFrame, Variant> provider) in KnownMacros)
                if (key.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                {
                    value = provider(frame);

                    return true;
                }

            return false;
        }
    }
}
