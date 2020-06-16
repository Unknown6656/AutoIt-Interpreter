using System.Diagnostics.CodeAnalysis;
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


        public abstract InterpreterError? Execute(NativeCallFrame frame);
    }

    public abstract class AbstractMacroProvider
        : AbstractInterpreterPlugin
    {
        protected AbstractMacroProvider(Interpreter interpreter)
            : base(interpreter)
        {
        }

        public abstract bool ProvideMacroValue(string name, out Variant? value);
    }
}
