using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Linq;
using System;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class Variable
        : IEquatable<Variable>
    {
        public string Name { get; }
        public bool IsConst { get; }
        public object? Value { get; set; }
        public VariableScope DeclaredScope { get; }
        public SourceLocation DeclaredLocation { get; }
        public bool IsGlobal => DeclaredScope.IsGlobalScope;


        internal Variable(VariableScope scope, SourceLocation location, string name, bool isConst)
        {
            Name = name.TrimStart('$').ToLowerInvariant();
            DeclaredLocation = location;
            DeclaredScope = scope;
            IsConst = isConst;
        }

        public override string ToString() => $"${Name}: {Value}";

        public override int GetHashCode() => Name.ToLowerInvariant().GetHashCode();

        public override bool Equals(object? obj) => (obj is Variable v && Equals(v))
                                                 || (obj is string s && Name.Equals(s, StringComparison.InvariantCultureIgnoreCase));

        public bool Equals(Variable? other) => Name.Equals(other?.Name, StringComparison.InvariantCultureIgnoreCase);
    }

    public sealed class VariableScope
        : IDisposable
    {
        private readonly ConcurrentDictionary<VariableScope, __empty> _children = new ConcurrentDictionary<VariableScope, __empty>();
        private readonly ConcurrentDictionary<Variable, __empty> _variables = new ConcurrentDictionary<Variable, __empty>();


        public VariableScope[] ChildScopes => _children.Keys.ToArray();

        public bool IsGlobalScope => Parent is null;

        public Interpreter Interpreter { get; }

        public VariableScope? Parent { get; }

        public VariableScope? GlobalRoot { get; }


        private VariableScope(Interpreter interpreter, VariableScope? parent)
        {
            Interpreter = interpreter;
            Parent = parent;
            GlobalRoot = parent?.GlobalRoot ?? this;
        }

        public void Dispose()
        {
            if (Parent is { _children: { } chd })
            {
                DestroyAllVariables(false);
                chd.TryRemove(this, out _);
            }
        }

        public override string ToString() => $"{(IsGlobalScope ? "(Global) " : "")}{_variables.Count} Variables, {_children.Count} Child scopes";

        public Variable CreateVariable(SourceLocation location, string name, bool isConst)
        {
            if (!TryGetVariable(name, out Variable? var))
            {
                var = new Variable(this, location, name, isConst);

                _variables.TryAdd(var, default);
            }

            return var;
        }

        public bool HasVariable(string name) => TryGetVariable(name, out _);

        public bool TryGetVariable(string name, [NotNullWhen(true)] out Variable? variable)
        {
            variable = null;

            foreach (Variable var in _variables.Keys)
                if (var.Equals(name))
                {
                    variable = var;

                    return true;
                }

            return Parent?.TryGetVariable(name, out variable) ?? false;
        }

        public bool DestroyVariable(string name, bool recursive)
        {
            if (TryGetVariable(name, out Variable? var))
                if (_variables.TryRemove(var, out _))
                    return true;
                else if (recursive)
                    return Parent?.DestroyVariable(name, recursive) ?? false;

            return false;
        }

        public void DestroyAllVariables(bool recursive)
        {
            _children.Clear();

            if (recursive && Parent is { } p)
                p.DestroyAllVariables(recursive);
        }

        public VariableScope CreateChildScope()
        {
            VariableScope res = new VariableScope(Interpreter, this);

            while (!_children.TryAdd(res, default))
                ;

            return res;
        }

        public static VariableScope CreateGlobalScope(Interpreter interpreter) => new VariableScope(interpreter, null);
    }
}
