using System.Diagnostics.CodeAnalysis;
using System.IO;
using System;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;
using System.Linq;
using System.Collections.Generic;

[assembly: AutoIt3Plugin]

namespace Unknown6656.AutoIt3.Extensibility
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = true)]
    public sealed class AutoIt3PluginAttribute
        : Attribute
    {
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

    public abstract class AbstractFunctionProvider
        : AbstractInterpreterPlugin
    {
        public abstract ProvidedNativeFunction[] ProvidedFunctions { get; }


        protected AbstractFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
        }
    }

    public abstract class ProvidedNativeFunction
    {
        public abstract string Name { get; }

        public abstract (int MinimumCount, int MaximumCount) ParameterCount { get; }


        public abstract Union<InterpreterError, Variant>? Execute(NativeCallFrame frame, Variant[] args);

        public override string ToString() => Name;

        public static ProvidedNativeFunction Create(string name, int param_count, Func<NativeCallFrame, Variant[], Union<InterpreterError, Variant>?> @delegate) =>
            Create(name, param_count, param_count, @delegate);

        public static ProvidedNativeFunction Create(string name, int min_param_count, int max_param_count, Func<NativeCallFrame, Variant[], Union<InterpreterError, Variant>?> @delegate, params Variant[] default_values) =>
            new FromDelegate(@delegate, name, min_param_count, max_param_count, default_values);


        internal sealed class FromDelegate
            : ProvidedNativeFunction
        {
            private readonly Func<NativeCallFrame, Variant[], Union<InterpreterError, Variant>?> _exec;


            public override string Name { get; }

            public Variant[] DefaultValues { get; }

            public override (int MinimumCount, int MaximumCount) ParameterCount { get; }


            public FromDelegate(Func<NativeCallFrame, Variant[], Union<InterpreterError, Variant>?> exec, string name, int min_param_count, int max_param_count, params Variant[] default_values)
            {
                _exec = exec;
                Name = name;
                DefaultValues = default_values;
                ParameterCount = (min_param_count, max_param_count);
            }

            public override Union<InterpreterError, Variant>? Execute(NativeCallFrame frame, Variant[] args)
            {
                List<Variant> a = new List<Variant>();

                a.AddRange(args);
                a.AddRange(DefaultValues.Skip(args.Length - ParameterCount.MinimumCount));

                if (a.Count < ParameterCount.MaximumCount)
                    a.AddRange(Enumerable.Repeat(Variant.Null, ParameterCount.MaximumCount - a.Count));
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
