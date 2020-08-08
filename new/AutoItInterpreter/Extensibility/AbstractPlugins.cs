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

        public abstract InterpreterResult? ProcessDirective(CallFrame frame, string directive);
    }

    public abstract class AbstractStatementProcessor
        : AbstractInterpreterPlugin
    {
        public abstract Regex Regex { get; }


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

    public abstract class ProvidedNativeFunction
    {
        public abstract string Name { get; }

        public FunctionMetadata Metadata { get; init; } = FunctionMetadata.Default;

        public abstract (int MinimumCount, int MaximumCount) ParameterCount { get; }


        public override string ToString() => Name;

        public abstract FunctionReturnValue Execute(NativeCallFrame frame, Variant[] args);

        public static ProvidedNativeFunction Create(string name, int param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> @delegate) =>
            Create(name, param_count, @delegate, null);

        public static ProvidedNativeFunction Create(string name, int min_param_count, int max_param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> @delegate, params Variant[] default_values) =>
            Create(name, min_param_count, max_param_count, @delegate, null, default_values);

        public static ProvidedNativeFunction Create(string name, int param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> @delegate, FunctionMetadata? metadata) =>
            Create(name, param_count, param_count, @delegate, metadata);

        public static ProvidedNativeFunction Create(string name, int min_param_count, int max_param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> @delegate, FunctionMetadata? metadata, params Variant[] default_values) =>
            new FromDelegate(@delegate, name, min_param_count, max_param_count, default_values)
            {
                Metadata = metadata ?? FunctionMetadata.Default
            };


        internal sealed class FromDelegate
            : ProvidedNativeFunction
        {
            private readonly Func<NativeCallFrame, Variant[], FunctionReturnValue> _exec;


            public override string Name { get; }

            public Variant[] DefaultValues { get; }

            public override (int MinimumCount, int MaximumCount) ParameterCount { get; }


            public FromDelegate(Func<NativeCallFrame, Variant[], FunctionReturnValue> exec, string name, int min_param_count, int max_param_count, params Variant[] default_values)
            {
                _exec = exec;
                Name = name;
                DefaultValues = default_values;
                ParameterCount = (min_param_count, max_param_count);
            }

            public override FunctionReturnValue Execute(NativeCallFrame frame, Variant[] args)
            {
                List<Variant> a = new List<Variant>();

                a.AddRange(args);
                a.AddRange(DefaultValues.Skip(args.Length - ParameterCount.MinimumCount));

                if (a.Count < ParameterCount.MaximumCount)
                    a.AddRange(Enumerable.Repeat(Variant.Default, ParameterCount.MaximumCount - a.Count));
                else if (a.Count > ParameterCount.MaximumCount)
                    a.RemoveRange(ParameterCount.MaximumCount, a.Count - ParameterCount.MaximumCount);

                return _exec(frame, a.ToArray());
            }
        }
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
}
