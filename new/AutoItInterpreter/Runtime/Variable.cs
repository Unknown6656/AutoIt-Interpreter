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


        internal Variable(string name, bool isConst)
        {
            Name = name.TrimStart('$').ToLowerInvariant();
            IsConst = isConst;
        }

        public override string ToString() => $"${Name}: {Value}";

        public override int GetHashCode() => Name.ToLowerInvariant().GetHashCode();

        public override bool Equals(object? obj) => (obj is Variable v && Equals(v))
                                                 || (obj is string s && Name.Equals(s, StringComparison.InvariantCultureIgnoreCase));

        public bool Equals(Variable? other) => Name.Equals(other?.Name, StringComparison.InvariantCultureIgnoreCase);
    }

    public sealed class VariableResolver
        : IDisposable
    {
        private readonly ConcurrentDictionary<VariableResolver, __empty> _children = new ConcurrentDictionary<VariableResolver, __empty>();
        private readonly ConcurrentDictionary<Variable, __empty> _variables = new ConcurrentDictionary<Variable, __empty>();


        public VariableResolver[] ChildScopes => _children.Keys.ToArray();

        public bool IsGlobalScope => Parent is null;

        public Interpreter Interpreter { get; }

        public VariableResolver? Parent { get; }

        public VariableResolver? GlobalRoot { get; }


        private VariableResolver(Interpreter interpreter, VariableResolver? parent)
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

        public Variable CreateVariable(string name, bool isConst)
        {
            if (!TryGetVariable(name, out Variable? var))
            {
                var = new Variable(name, isConst);

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

        public VariableResolver CreateChildScope()
        {
            VariableResolver res = new VariableResolver(Interpreter, this);

            while (!_children.TryAdd(res, default))
                ;

            return res;
        }

        public static VariableResolver CreateGlobalScope(Interpreter interpreter) => new VariableResolver(interpreter, null);
    }
}
