﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    using static AST;


    /// <summary>
    /// Represents an abstract script function. This could be an AutoIt3 or a native function.
    /// </summary>
    public abstract class ScriptFunction
        : IEquatable<ScriptFunction>
    {
        internal const string GLOBAL_FUNC = "$global";

        public static readonly string[] RESERVED_NAMES =
        {
            "_", "$_", VARIABLE.Discard.Name, "$GLOBAL", "GLOBAL", "STATIC", "CONST", "DIM", "REDIM", "ENUM", "STEP", "LOCAL", "FOR", "IN",
            "NEXT", "TO", "FUNC", "ENDFUNC", "DO", "UNTIL", "WHILE", "WEND", "IF", "THEN", "ELSE", "ENDIF", "ELSEIF", "SELECT", "ENDSELECT",
            "CASE", "SWITCH", "ENDSWITCH", "WITH", "ENDWITH", "CONTINUECASE", "CONTINUELOOP", "EXIT", "EXITLOOP", "RETURN", "VOLATILE", "TRUE",
            "FALSE", "DEFAULT", "NULL", "BYREF", "REF", "AND", "OR", "NOT"
        };


        public string Name { get; }

        public ScannedScript Script { get; }

        public Metadata Metadata { get; init; } = Metadata.Default;

        public abstract SourceLocation Location { get; }

        public abstract (int MinimumCount, int MaximumCount) ParameterCount { get; }

        public bool IsMainFunction => Name.Equals(GLOBAL_FUNC, StringComparison.InvariantCultureIgnoreCase);


        internal ScriptFunction(ScannedScript script, string name)
        {
            Name = name;
            Script = script;
            Script.AddFunction(this);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Name.ToUpperInvariant(), Script);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as ScriptFunction);

        /// <inheritdoc/>
        public bool Equals(ScriptFunction? other) => other is ScriptFunction f && f.GetHashCode() == GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => $"[{Script}] Func {Name}";


        public static bool operator ==(ScriptFunction? s1, ScriptFunction? s2) => s1?.Equals(s2) ?? s2 is null;

        public static bool operator !=(ScriptFunction? s1, ScriptFunction? s2) => !(s1 == s2);
    }

    /// <summary>
    /// Represents an AutoIt3 function. This is a function defined in a .au3 (user) script.
    /// </summary>
    public sealed class AU3Function
        : ScriptFunction
    {
        private readonly ConcurrentDictionary<SourceLocation, List<string>> _lines = new();
        private readonly ConcurrentDictionary<string, JumpLabel> _jumplabels = new();


        /// <summary>
        /// The abstract syntax tree representing the declaration of each function parameter.
        /// </summary>
        public PARAMETER_DECLARATION[] Parameters { get; }

        /// <summary>
        /// The source code location, at which the function has been defined.
        /// </summary>
        public override SourceLocation Location
        {
            get
            {
                SourceLocation[] lines = _lines.Keys.OrderBy(LINQ.id).ToArray();

                if (lines.Length > 0)
                    return new SourceLocation(lines[0].FullFileName, lines[0].StartLineNumber, lines[^1].EndLineNumber);
                else
                    return new SourceLocation(Script.Location.FullName, 0);
            }
        }

        /// <summary>
        /// Indicates whether the function has been declared as '<see langword="volatile"/>'.
        /// </summary>
        public bool IsVolatile { get; internal set; }

        /// <summary>
        /// Returns the number of source code lines in the function definition.
        /// </summary>
        public int LineCount => _lines.Values.Select(l => l.Count).Append(0).Sum();

        /// <summary>
        /// Returns the minimum and maximum parameter count of the function.
        /// When calling the function, a minimum of '<see cref="MinimumCount"/>' parameters is expected.
        /// The function accepts a maximum of '<see cref="MaximumCount"/>' parameters.
        /// The difference between the two integers is the count of optional function parameters.
        /// </summary>
        public override (int MinimumCount, int MaximumCount) ParameterCount { get; }

        /// <summary>
        /// Returns an array of the individual source code lines contained in the function definition.
        /// </summary>
        public (SourceLocation LineLocation, string LineContent)[] Lines => (from loc in _lines.Keys
                                                                             orderby loc ascending
                                                                             from line in _lines[loc]
                                                                             select (loc, line)).ToArray();

        public ReadOnlyIndexer<string, JumpLabel?> JumpLabels { get; }


        internal AU3Function(ScannedScript script, string name, IEnumerable<PARAMETER_DECLARATION>? @params)
            : base(script, name)
        {
            Parameters = @params?.ToArray() ?? Array.Empty<PARAMETER_DECLARATION>();
            ParameterCount = (Parameters.Count(p => !p.IsOptional), Parameters.Length);
            JumpLabels = new ReadOnlyIndexer<string, JumpLabel?>(name => _jumplabels.TryGetValue(name.ToUpperInvariant(), out JumpLabel? label) ? label : null);
        }

        public JumpLabel AddJumpLabel(SourceLocation location, string name)
        {
            name = name.Trim().ToUpperInvariant();

            JumpLabel label = new JumpLabel(this, location, name);

            _jumplabels.AddOrUpdate(name, label, (_, _) => label);

            return label;
        }

        public void AddLine(SourceLocation location, string content) => _lines.AddOrUpdate(location, new List<string>() { content }, (_, l) =>
        {
            l.Add(content);

            return l;
        });

        /// <inheritdoc/>
        public override string ToString() => $"{base.ToString()}({string.Join<PARAMETER_DECLARATION>(", ", Parameters)})  [{LineCount} Lines]";
    }

    /// <summary>
    /// Represents an unmanaged (native) function.
    /// A native function can either be a built-in function, provided by plugins, external libraries, or a .NET function fetched using reflection.
    /// </summary>
    public class NativeFunction
        : ScriptFunction
    {
        private static volatile int _id = 1;


        private readonly Func<NativeCallFrame, Variant[], FunctionReturnValue> _execute;

        public override (int MinimumCount, int MaximumCount) ParameterCount { get; }

        public override SourceLocation Location { get; } = SourceLocation.Unknown;


        internal NativeFunction(Interpreter interpreter, string name, (int min, int max) param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> execute, Metadata metadata)
            : base(interpreter.ScriptScanner.SystemScript, name)
        {
            _execute = execute;
            ParameterCount = param_count;
            Metadata = metadata;
        }

        private NativeFunction(Interpreter interpreter, int param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> execute, OS os)
            : this(interpreter, $"$delegate-0x{++_id:x8}", (param_count, param_count), execute, new Metadata(os, false))
        {
        }

        public FunctionReturnValue Execute(NativeCallFrame frame, Variant[] args) => _execute(frame, args);

        public override string ToString() => "[native] " + base.ToString();

        public override bool Equals(object? obj) => Name.Equals((obj as ScriptFunction)?.Name, StringComparison.InvariantCultureIgnoreCase);

        public override int GetHashCode() => Name.GetHashCode(StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// Creates a new (parameterless) native function using the given delegate.
        /// The function internally gets assigned an unique name, but will <b>not</b> be registered in the function resolver.
        /// </summary>
        /// <param name="interpreter">The interpreter instance.</param>
        /// <param name="execute">The delegate representing the function's internal logic.</param>
        /// <param name="os">The operating systems supported by the given delegate.</param>
        /// <returns>The newly created native function.</returns>
        public static NativeFunction FromDelegate(Interpreter interpreter, Func<NativeCallFrame, FunctionReturnValue> execute, OS os = OS.Any) =>
            FromDelegate(interpreter, 0, (f, _) => execute(f), os);

        /// <summary>
        /// Creates a new native function using the given delegate.
        /// The function internally gets assigned an unique name, but will <b>not</b> be registered in the function resolver.
        /// </summary>
        /// <param name="interpreter">The interpreter instance.</param>
        /// <param name="param_count">The number of parameters expected by the delegate.</param>
        /// <param name="execute">The delegate representing the function's internal logic.</param>
        /// <param name="os">The operating systems supported by the given delegate.</param>
        /// <returns>The newly created native function.</returns>
        public static NativeFunction FromDelegate(Interpreter interpreter, int param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> execute, OS os = OS.Any) =>
            new(interpreter, param_count, execute, os);
    }

    /// <summary>
    /// Represents an unmanaged .NET framework function.
    /// The refrence to the .NET function is provided via an instance of <see cref="MethodInfo"/>.
    /// </summary>
    public sealed class NETFrameworkFunction
        : NativeFunction
    {
        internal NETFrameworkFunction(Interpreter interpreter, MethodInfo method, object? instance)
            : this(interpreter, method, method.GetParameters(), instance)
        {
        }

        private NETFrameworkFunction(Interpreter interpreter, MethodInfo method, ParameterInfo[] parameters, object? instance)
            : base(
                interpreter,
                $"{method.DeclaringType?.FullName}.{method.Name}: {string.Join(", ", parameters.Select(p => p.ParameterType.FullName))} -> {method.ReturnType.FullName}",
                (parameters.Count(p => !p.HasDefaultValue), parameters.Length),
                (frame, args) =>
                {
                    if (interpreter.GlobalObjectStorage.TryInvokeNETMember(instance, method, args, out Variant result))
                        return result;
                    else
                        return FunctionReturnValue.Fatal(InterpreterError.WellKnown(null, "error.net_execution_error", method));
                },
                Metadata.Default
            )
        {
        }

        /// <inheritdoc/>
        public override string ToString() => $"[.NET] {Name}";
    }

    public abstract class ProvidedNativeFunction
    {
        public abstract string Name { get; }

        public Metadata Metadata { get; init; } = Metadata.Default;

        public abstract (int MinimumCount, int MaximumCount) ParameterCount { get; }


        public override string ToString() => Name;

        public abstract FunctionReturnValue Execute(NativeCallFrame frame, Variant[] args);

        public static ProvidedNativeFunction Create(string name, int param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> @delegate) =>
            Create(name, param_count, @delegate, null);

        public static ProvidedNativeFunction Create(string name, int min_param_count, int max_param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> @delegate, params Variant[] default_values) =>
            Create(name, min_param_count, max_param_count, @delegate, null, default_values);

        public static ProvidedNativeFunction Create(string name, int param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> @delegate, Metadata? metadata) =>
            Create(name, param_count, param_count, @delegate, metadata);

        public static ProvidedNativeFunction Create(string name, int min_param_count, int max_param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> @delegate, Metadata? metadata, params Variant[] default_values) =>
            new FromDelegate(@delegate, name, min_param_count, max_param_count, default_values)
            {
                Metadata = metadata ?? Metadata.Default
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
}