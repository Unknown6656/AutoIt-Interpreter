using System;

namespace Unknown6656.AutoIt3.Runtime
{
    /// <summary>
    /// Represents a variable capable of holding a value of the type <see cref="Variant"/>.
    /// Variables are identified using their case-insensitive name and a '$'-prefix.
    /// </summary>
    public sealed class Variable
        : IEquatable<Variable>
    {
        private readonly object _mutex = new();
        private Variant _value;

        /// <summary>
        /// The variable's lower-case name without the '$'-prefix.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Indicates whether the variable has been declared or marked as <see langword="Const"/>.
        /// </summary>
        public bool IsConst { get; }

        /// <summary>
        /// Indicates whether the variable is a <see langword="ByRef"/>-reference pointing to an other variable.
        /// </summary>
        public bool IsReference => _value.IsReference;

        /// <summary>
        /// Returns the variable (if any) to which the current instance is pointing to (Only relevant for <see langword="ByRef"/>-variables).
        /// </summary>
        public Variable? ReferencedVariable => _value.ReferencedVariable;

        /// <summary>
        /// The scope in which the current variable has been declared.
        /// </summary>
        public VariableScope DeclaredScope { get; }

        /// <summary>
        /// The source location at which the current variable has been declared.
        /// </summary>
        public SourceLocation DeclaredLocation { get; }

        /// <summary>
        /// Sets or gets the value stored inside the current variable.
        /// </summary>
        public Variant Value
        {
            get => ReferencedVariable?._value ?? _value;
            set
            {
                Variable? target = ReferencedVariable ?? this;

                lock (_mutex)
                    target._value = value.AssignTo(target);
            }
        }

        /// <summary>
        /// The interpreter instance with which the current variable is associated.
        /// </summary>
        public Interpreter Interpreter => DeclaredScope.Interpreter;

        /// <summary>
        /// Indicates whether the variable has been declared <see langword="Global"/> or resides inside the global scope.
        /// </summary>
        public bool IsGlobal => DeclaredScope.IsGlobalScope;


        internal Variable(VariableScope scope, SourceLocation location, string name, bool isConst)
        {
            Name = name.TrimStart('$').ToLowerInvariant();
            DeclaredLocation = location;
            DeclaredScope = scope;
            IsConst = isConst;
            Value = Variant.Null;
        }

        /// <inheritdoc/>
        public override string ToString() => $"${Name}: {Value.ToDebugString(Interpreter)}";

        /// <inheritdoc/>
        public override int GetHashCode() => Name.GetHashCode(StringComparison.InvariantCultureIgnoreCase);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => (obj is Variable v && Equals(v))
                                                 || (obj is string s && Name.Equals(s, StringComparison.InvariantCultureIgnoreCase));

        /// <inheritdoc/>
        public bool Equals(Variable? other) => Name.Equals(other?.Name, StringComparison.InvariantCultureIgnoreCase);
    }
}
