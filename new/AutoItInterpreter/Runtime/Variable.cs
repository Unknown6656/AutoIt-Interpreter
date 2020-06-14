using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    using static Generics;
    using static AST;


    public enum VariantType
        : int
    {
        Null = 0,
        Boolean = 1,
        Number = 2,
        String = 3,
        Array = 4,
        NETObject = 5,
        // TODO : map type
        Default = -1,
    }

    public readonly struct Variant
    {
        public readonly VariantType Type { get; }

        /// <summary>
        /// This value <b>must</b> have one of the following types:
        /// <para/>
        /// <list type="table">
        ///     <listheader>
        ///         <term>.NET Type</term>
        ///         <description><see cref="VariantType"/> value of <see cref="Type"/></description>
        ///     </listheader>
        ///     <item>
        ///         <term>&lt;<see langword="null"/>&gt;</term>
        ///         <description><see cref="VariantType.Null"/> or <see cref="VariantType.Default"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="bool"/></term>
        ///         <description><see cref="VariantType.Boolean"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="decimal"/></term>
        ///         <description><see cref="VariantType.Number"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="string"/></term>
        ///         <description><see cref="VariantType.String"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="Variant"/>[]</term>
        ///         <description><see cref="VariantType.Array"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="Dictionary{K,V}"/> with &lt;<see cref="string"/>,<see cref="Variant"/>&gt;</term>
        ///         <description><see cref="VariantType.Map"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="object"/></term>
        ///         <description><see cref="VariantType.NETObject"/></description>
        ///     </item>
        /// </list>
        /// </summary>
        private readonly object? RawData { get; }

        public readonly bool IsNull => Type is VariantType.Null;

        public readonly bool IsDefault => Type is VariantType.Default;


        private Variant(VariantType type, object? data)
        {
            Type = type;
            RawData = data;
        }

        public readonly bool NotEquals(Variant other)
        {
            throw new NotImplementedException();
        }

        public readonly bool EqualsCaseSensitive(Variant other)
        {
            throw new NotImplementedException();
        }

        public readonly bool EqualsCaseInsensitive(Variant other)
        {
            throw new NotImplementedException();
        }

        public readonly override bool Equals(object? obj) => Equals(ToVariant(obj));


        public static Variant GetTypeDefault(VariantType type) => type switch
        {
            VariantType.Boolean => FromBoolean(false),
            VariantType.Number => FromNumber(0m),
            VariantType.String => FromString(""),
            VariantType.Array => FromArray(Array.Empty<Variant>()),
            _ => new Variant(type, null),
        };

        public static Variant ToVariant(object? obj) => obj switch
        {
            null or LITERAL => FromLiteral(obj as LITERAL),
            Variant v => v,
            bool b => FromBoolean(b),
            sbyte n => FromNumber(n),
            byte n => FromNumber(n),
            short n => FromNumber(n),
            ushort n => FromNumber(n),
            int n => FromNumber(n),
            uint n => FromNumber(n),
            long n => FromNumber(n),
            ulong n => FromNumber(n),
            float n => FromNumber((decimal)n),
            double n => FromNumber((decimal)n),
            decimal n => FromNumber(n),
            char c => FromString(c.ToString()),
            string str => FromString(str),
            StringBuilder strb => FromString(strb.ToString()),
            Array arr => FromArray(arr),
            IDictionary<string, Variant> dic => FromDictionaray(dic),
            _ => FromNETObject(obj),
        };

        private static Variant FromDictionaray(IDictionary<string, Variant> dic)
        {
            throw new NotImplementedException();
        }

        public static Variant FromArray(Array array)
        {
            throw new NotImplementedException();
        }

        public static Variant FromLiteral(LITERAL? literal)
        {
            if (literal?.IsNull ?? true)
                return GetTypeDefault(VariantType.Null);
            else if (literal.IsDefault)
                return GetTypeDefault(VariantType.Default);
            else if (literal.IsFalse)
                return FromBoolean(false);
            else if (literal.IsTrue)
                return FromBoolean(true);
            else if (literal is LITERAL.String { Item: string s })
                return FromString(s);
            else if (literal is LITERAL.Number { Item: decimal d })
                return FromNumber(d);
            else
                throw new InvalidCastException($"Unable to convert the value '{literal}' to an instance of the type '{typeof(Variant)}'");
        }

        public static Variant FromNETObject(object obj) => new Variant(VariantType.NETObject, obj);

        public static Variant FromNumber(decimal d) => new Variant(VariantType.Number, d);

        public static Variant FromString(string s) => new Variant(VariantType.String, s);

        public static Variant FromBoolean(bool b) => new Variant(VariantType.Boolean, b);

        //public static implicit operator bool
        //public static implicit operator sbyte
        //public static implicit operator byte
        //public static implicit operator short
        //public static implicit operator ushort
        //public static implicit operator int
        //public static implicit operator uint
        //public static implicit operator long
        //public static implicit operator ulong
        //public static implicit operator float
        //public static implicit operator double
        //public static implicit operator decimal
        //public static implicit operator char
        //public static implicit operator string
        //public static implicit operator StringBuilder
        //public static implicit operator Array

        public static implicit operator Variant (bool b) => ToVariant(b);

        public static implicit operator Variant (sbyte n) => ToVariant(n);

        public static implicit operator Variant (byte n) => ToVariant(n);

        public static implicit operator Variant (short n) => ToVariant(n);

        public static implicit operator Variant (ushort n) => ToVariant(n);

        public static implicit operator Variant (int n) => ToVariant(n);

        public static implicit operator Variant (uint n) => ToVariant(n);

        public static implicit operator Variant (long n) => ToVariant(n);

        public static implicit operator Variant (ulong n) => ToVariant(n);

        public static implicit operator Variant (float n) => ToVariant(n);

        public static implicit operator Variant (double n) => ToVariant(n);

        public static implicit operator Variant (decimal n) => ToVariant(n);

        public static implicit operator Variant (char n) => ToVariant(n);

        public static implicit operator Variant (string? n) => ToVariant(n);

        public static implicit operator Variant (StringBuilder? n) => ToVariant(n);

        public static implicit operator Variant (Array? n) => ToVariant(n);
    }

    public sealed class Variable
        : IEquatable<Variable>
    {
        public string Name { get; }
        public bool IsConst { get; }
        public Variant Value { get; set; }
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

        public Variable CreateVariable(SourceLocation location, VARIABLE variable, bool isConst) => CreateVariable(location, variable.Name, isConst);

        public bool HasVariable(string name) => TryGetVariable(name, out _);

        public bool HasVariable(VARIABLE variable) => HasVariable(variable.Name);

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

        public bool TryGetVariable(VARIABLE input, [NotNullWhen(true)] out Variable? variable) => TryGetVariable(input.Name, out variable);

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
