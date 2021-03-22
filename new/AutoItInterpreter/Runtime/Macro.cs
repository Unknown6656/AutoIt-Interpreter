using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;
using System.Collections.Generic;
using System;

using Unknown6656.AutoIt3.Extensibility;

namespace Unknown6656.AutoIt3.Runtime
{
    /// <summary>
    /// Represents an AutoIt3 macro.
    /// Macros are identified using their case-insensitive name and a '@'-prefix.
    /// </summary>
    public class Macro
        : IEquatable<Macro>
    {
        /// <summary>
        /// The macros's upper-case name without the '@'-prefix.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The interpreter instance with which the current macro is associated.
        /// </summary>
        public Interpreter Interpreter { get; }

        public virtual bool IsKnownMacro => this is KnownMacro;


        internal Macro(Interpreter interpreter, string name)
        {
            Interpreter = interpreter;
            Name = name.TrimStart('@').ToUpperInvariant();
        }

        public virtual Variant GetValue(CallFrame frame)
        {
            Interpreter.MacroResolver.GetTryValue(frame, this, out Variant value, out _);

            return value;
        }

        public override string ToString() => '@' + Name;

        public override int GetHashCode() => Name.GetHashCode();

        public override bool Equals(object? obj) => obj is Macro macro && Equals(macro);

        public bool Equals(Macro? other) => string.Equals(Name, other?.Name);
    }

    public sealed class KnownMacro
        : Macro
    {
        private readonly Func<CallFrame, Variant> _value_provider;

        public Metadata Metadata { get; init; } = Metadata.Default;


        internal KnownMacro(Interpreter interpreter, string name, Func<CallFrame, Variant> value_provider)
            : base(interpreter, name) => _value_provider = value_provider;

        public override Variant GetValue(CallFrame frame) => _value_provider(frame);
    }

    public sealed class MacroResolver
    {
        private readonly HashSet<KnownMacro> _macros = new();

        
        public Interpreter Interpreter { get; }

        public int KnownMacroCount => _macros.Count;

        public ImmutableHashSet<KnownMacro> KnownMacros => _macros.ToImmutableHashSet();


        internal MacroResolver(Interpreter interpreter) => Interpreter = interpreter;

        internal void AddKnownMacro(KnownMacro macro) => _macros.Add(macro);

        public bool HasMacro(CallFrame frame, string macro_name) => GetTryValue(frame, macro_name, out _, out _);

        public bool HasMacro(CallFrame frame, Macro macro) => GetTryValue(frame, macro, out _, out _);
 
        public bool GetTryValue(CallFrame frame, Macro macro, out Variant value, [NotNullWhen(true)] out Metadata? metadata) =>
            GetTryValue(frame, macro.Name, out value, out metadata);

        public bool GetTryValue(CallFrame frame, string macro_name, out Variant value, [NotNullWhen(true)] out Metadata? metadata)
        {
            bool result;
            macro_name = macro_name.TrimStart('@');

            (value, metadata, result) = Interpreter.Telemetry.Measure(TelemetryCategory.MacroResolving, delegate
            {
                foreach (KnownMacro macro in _macros)
                    if (macro.Name.Equals(macro_name, StringComparison.InvariantCultureIgnoreCase))
                        return (macro.GetValue(frame), macro.Metadata, true);

                foreach (AbstractMacroProvider provider in Interpreter.PluginLoader.MacroProviders)
                    if (provider.ProvideMacroValue(frame, macro_name, out (Variant value, Metadata meta)? v) && v.HasValue)
                        return (v.Value.value, v.Value.meta, true);

                return (Variant.Null, null!, false);
            });

            return result;
        }

        public override string ToString() => $"{KnownMacroCount} known macros.";
    }
}
