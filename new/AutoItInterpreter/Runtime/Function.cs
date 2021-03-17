using System.Collections.Concurrent;
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


    public sealed class AU3Function
        : ScriptFunction
    {
        private readonly ConcurrentDictionary<SourceLocation, List<string>> _lines = new();
        private readonly ConcurrentDictionary<string, JumpLabel> _jumplabels = new();


        public PARAMETER_DECLARATION[] Parameters { get; }

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

        public bool IsVolatile { get; internal set; }

        public int LineCount => _lines.Values.Select(l => l.Count).Append(0).Sum();

        public override (int MinimumCount, int MaximumCount) ParameterCount { get; }

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

    public class NativeFunction
        : ScriptFunction
    {
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

        public FunctionReturnValue Execute(NativeCallFrame frame, Variant[] args) => _execute(frame, args);

        /// <inheritdoc/>
        public override string ToString() => "[native] " + base.ToString();
    }

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
