using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
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
        Map = 5,
        NETObject = 6,
        Reference = -2,
        Default = -1,
    }

    public readonly struct Variant
        : IEquatable<Variant>
        , IComparable<Variant>
    {
        #region STATIC PROPERTIES

        public static Variant Null { get; } = GetTypeDefault(VariantType.Null);

        public static Variant Default { get; } = GetTypeDefault(VariantType.Default);

        public static Variant True { get; } = true;

        public static Variant False { get; } = false;

        public static Variant Zero { get; } = 0m;

        #endregion
        #region INSTANCE PROPERTIES

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
        ///         <term><see cref="Variable"/></term>
        ///         <description><see cref="VariantType.Reference"/></description>
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
        ///         <term><see cref="Dictionary{K,V}"/> with &lt;<see cref="Variant"/>,<see cref="Variant"/>&gt;</term>
        ///         <description><see cref="VariantType.Map"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="object"/></term>
        ///         <description><see cref="VariantType.NETObject"/></description>
        ///     </item>
        /// </list>
        /// </summary>
        internal readonly object? RawData { get; }

        public readonly Variable? AssignedTo { get; }

        public readonly bool IsIndexable => Type is VariantType.Array or VariantType.NETObject or VariantType.Map or VariantType.String;

        public readonly bool IsReference => Type is VariantType.Reference;

        public readonly Variable? ReferencedVariable => IsReference ? RawData as Variable : null;

        public readonly bool IsNull => Type is VariantType.Null;

        public readonly bool IsDefault => Type is VariantType.Default;

        public readonly int Length => (RawData as IEnumerable)?.Count() ?? 0;

        #endregion
        #region .CTOR

        private Variant(VariantType type, object? data, Variable? variable)
        {
            Type = type;
            RawData = data;
            AssignedTo = variable;
        }

        #endregion
        #region INSTANCE METHODS

        public readonly int CompareTo(Variant other) => Type is VariantType.String && other.Type is VariantType.String
                                             ? ToString().CompareTo(other.ToString())
                                             : ToNumber().CompareTo(other.ToNumber());

        public readonly bool NotEquals(Variant other) => !EqualsCaseInsensitive(other); // TODO : unit tests

        public readonly bool EqualsCaseInsensitive(Variant other)
        {
            if (Type is VariantType.String && other.Type is VariantType.String)
                return string.Equals(ToString(), other.ToString(), StringComparison.InvariantCultureIgnoreCase);

            return EqualsCaseSensitive(other);
        }

        public readonly bool EqualsCaseSensitive(Variant other) => Equals(other); // TODO : unit tests

        public readonly bool Equals(Variant other) => Type.Equals(other.Type) && Equals(RawData, other.RawData);

        public readonly override bool Equals(object? obj) => Equals(FromObject(obj));

        public readonly override int GetHashCode() => HashCode.Combine(Type, RawData);

        public readonly override string ToString() => IsDefault ? "Default" : RawData?.ToString() ?? "";

        public readonly bool ToBoolean() => Type switch
        {
            VariantType.Null or VariantType.Default => false,
            VariantType.Boolean => (bool?)RawData ?? false,
            VariantType.Number => !((decimal?)RawData is 0m or null),
            VariantType.String => ToString() != "",
            _ => true,
        };

        public readonly decimal ToNumber() => Type switch
        {
            VariantType.Default => -1m,
            VariantType.Boolean => (bool?)RawData is true ? 1 : 0,
            VariantType.Number => (decimal?)RawData ?? 0m,
            VariantType.String => decimal.TryParse(ToString(), out decimal d) ? d : 0m,
            VariantType.Null or _ => 0m,
        };

        public readonly IDictionary<Variant, Variant> ToMap()
        {
            IDictionary<Variant, Variant> output = new Dictionary<Variant, Variant>();

            if (RawData is Array arr)
                for (int i = 0; i < arr.Length; ++i)
                    output[i] = FromObject(arr.GetValue(i));
            else if (RawData is IDictionary<Variant, Variant> dic)
                foreach (Variant key in dic.Keys)
                    output[key] = dic[key];
            else
                ; // TODO : objects

            return output;
        }

        public readonly Variant AssignTo(Variable? parent) => new Variant(Type, RawData, parent);

        public readonly bool TrySetIndexed(Variant index, Variant value)
        {
            if (RawData is string _)
                return false; // RawData = s[..index] + value + s[(index + 1)..];
            else if (RawData is Array arr)
            {
                int idx = (int)index;

                if (idx < 0 || idx >= arr.Length)
                    return false;
                else
                {
                    arr.SetValue(value, idx);

                    return true;
                }
            }
            else if (RawData is IDictionary<Variant, Variant> dic)
            {
                dic[index] = value;

                return true;
            }
            else
                return false; // TODO : objects
        }

        public readonly bool TryGetIndexed(Variant index, out Variant value)
        {
            if (RawData is IDictionary<Variant, Variant> dic)
                return dic.TryGetValue(index, out value);
            else if (index.EqualsCaseInsensitive(nameof(Length)))
            {
                value = Length;

                return true;
            }

            int idx = (int)index;
            value = Null;

            if (RawData is string s)
            {
                if (idx < 0 || idx >= s.Length)
                    return false;
                else
                {
                    value = s[idx];

                    return true;
                }
            }
            else if (RawData is Array arr)
            {
                if (idx < 0 || idx >= arr.Length)
                    return false;
                else
                {
                    value = FromObject(arr.GetValue(idx));

                    return true;
                }
            }
            else
                return false; // TODO : objects
        }

        #endregion
        #region STATIC METHODS

        public static Variant GetTypeDefault(VariantType type) => type switch
        {
            VariantType.Boolean => FromBoolean(false),
            VariantType.Number => FromNumber(0m),
            VariantType.String => FromString(""),
            VariantType.Array => NewArray(0),
            VariantType.Map => NewMap(),
            _ => new Variant(type, null, null),
        };

        public static Variant NewMap() => new Variant(VariantType.Map, new Dictionary<Variant, Variant>(), null);

        public static Variant NewArray(int length) => new Variant(VariantType.Array, new Variant[length], null);

        public static Variant FromObject(object? obj) => obj switch
        {
            null or LITERAL => FromLiteral(obj as LITERAL),
            Variable v => FromReference(v),
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
            IDictionary<Variant, Variant> dic => FromMap(dic),
            _ => FromNETObject(obj),
        };

        public static Variant FromMap(params (Variant key, Variant value)[] pairs) => FromMap(pairs.ToDictionary());

        public static Variant FromMap(IDictionary<Variant, Variant> dic)
        {
            Variant v = NewMap();
            bool error = true;

            foreach (Variant key in dic.Keys)
                error &= v.TrySetIndexed(key, dic[key]);

            error ^= true;

            // TODO : report error?

            return v;
        }

        public static Variant FromArray(Array array)
        {
            Variant v = NewArray(array.Length);
            Variant i = Zero;

            foreach (object? element in array)
            {
                v.TrySetIndexed(i, FromObject(element));
                ++v;
            }

            return v;
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

        public static Variant FromReference(Variable variable) => new Variant(VariantType.Reference, variable, null);

        public static Variant FromNETObject(object obj) => new Variant(VariantType.NETObject, obj, null);

        public static Variant FromNumber(decimal d) => new Variant(VariantType.Number, d, null);

        public static Variant FromString(string s) => new Variant(VariantType.String, s, null);

        public static Variant FromBoolean(bool b) => new Variant(VariantType.Boolean, b, null);

        #endregion
        #region ARITHMETIC OPERATORS

        public static Variant operator ++(Variant v) => v + 1;

        public static Variant operator --(Variant v) => v - 1;

        public static Variant operator +(Variant v) => v;

        public static Variant operator !(Variant v) => FromBoolean(!v.ToBoolean());

        public static Variant operator -(Variant v) => v.IsDefault || v.IsNull ? v : FromNumber(-v.ToNumber());

        public static Variant operator +(Variant v1, Variant v2) => FromNumber(v1.ToNumber() + v2.ToNumber());

        public static Variant operator -(Variant v1, Variant v2) => FromNumber(v1.ToNumber() - v2.ToNumber());

        public static Variant operator *(Variant v1, Variant v2) => FromNumber(v1.ToNumber() * v2.ToNumber());

        public static Variant operator /(Variant v1, Variant v2) => FromNumber(v1.ToNumber() / v2.ToNumber());

        public static Variant operator %(Variant v1, Variant v2) => FromNumber(v1.ToNumber() % v2.ToNumber());

        /// <summary>This is <b>not</b> XOR - this is the mathematical power operator!</summary>
        public static Variant operator ^(Variant v1, Variant v2) => FromObject(Math.Pow((double)v1.ToNumber(), (double)v2.ToNumber()));

        /// <summary>This is <b>not</b> AND - this is string concat!</summary>
        public static Variant operator &(Variant v1, Variant v2) => FromString(v1.ToString() + v2.ToString());

        public static bool operator ==(Variant v1, Variant v2) => v1.EqualsCaseSensitive(v2);

        public static bool operator !=(Variant v1, Variant v2) => v1.NotEquals(v2);

        public static bool operator <(Variant v1, Variant v2) => v1.CompareTo(v2) < 0;

        public static bool operator <=(Variant v1, Variant v2) => v1.CompareTo(v2) <= 0;

        public static bool operator >(Variant v1, Variant v2) => v1.CompareTo(v2) > 0;

        public static bool operator >=(Variant v1, Variant v2) => v1.CompareTo(v2) >= 0;

        #endregion
        #region CASTING OPERATORS

        public static implicit operator Variant (bool b) => FromBoolean(b);

        public static implicit operator Variant (sbyte n) => FromNumber(n);

        public static implicit operator Variant (byte n) => FromNumber(n);

        public static implicit operator Variant (short n) => FromNumber(n);

        public static implicit operator Variant (ushort n) => FromNumber(n);

        public static implicit operator Variant (int n) => FromNumber(n);

        public static implicit operator Variant (uint n) => FromNumber(n);

        public static implicit operator Variant (long n) => FromNumber(n);

        public static implicit operator Variant (ulong n) => FromNumber(n);

        public static implicit operator Variant (float n) => FromObject(n);

        public static implicit operator Variant (double n) => FromObject(n);

        public static implicit operator Variant (decimal n) => FromNumber(n);

        public static implicit operator Variant (char n) => FromObject(n);

        public static implicit operator Variant (string? n) => FromString(n);

        public static implicit operator Variant (StringBuilder? n) => FromObject(n);

        public static implicit operator Variant (Array? n) => FromObject(n);

        public static explicit operator bool (Variant v) => v.ToBoolean();

        public static explicit operator sbyte (Variant v) => (sbyte)v.ToNumber();

        public static explicit operator byte (Variant v) => (byte)v.ToNumber();

        public static explicit operator short (Variant v) => (short)v.ToNumber();

        public static explicit operator ushort (Variant v) => (ushort)v.ToNumber();

        public static explicit operator int (Variant v) => (int)v.ToNumber();

        public static explicit operator uint (Variant v) => (uint)v.ToNumber();

        public static explicit operator long (Variant v) => (long)v.ToNumber();

        public static explicit operator ulong (Variant v) => (ulong)v.ToNumber();

        public static explicit operator float (Variant v) => (float)v.ToNumber();

        public static explicit operator double (Variant v) => (double)v.ToNumber();

        public static explicit operator decimal (Variant v) => v.ToNumber();

        public static explicit operator char (Variant v) => v.ToString().FirstOrDefault();

        public static explicit operator string (Variant v) => v.ToString();

        public static explicit operator StringBuilder (Variant v) => new StringBuilder(v.ToString());

        // public static explicit operator Array (Variant v) => ;

        #endregion
    }

    public sealed class Variable
        : IEquatable<Variable>
    {
        private Variant _value;


        public string Name { get; }

        public bool IsConst { get; }

        public bool IsReference => _value.IsReference;

        public Variable? ReferencedVariable => _value.ReferencedVariable;

        public VariableScope DeclaredScope { get; }

        public SourceLocation DeclaredLocation { get; }

        public Variant Value
        {
            get => ReferencedVariable?._value ?? _value;
            set
            {
                Variable? target = ReferencedVariable ?? this;

                lock (this) // TODO: is this necessary?
                    target._value = value.AssignTo(target);
            }
        }

        public bool IsGlobal => DeclaredScope.IsGlobalScope;


        internal Variable(VariableScope scope, SourceLocation location, string name, bool isConst)
        {
            Name = name.TrimStart('$').ToLowerInvariant();
            DeclaredLocation = location;
            DeclaredScope = scope;
            IsConst = isConst;
            Value = Variant.Null;
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

        public Variable[] LocalVariables => _variables.Keys.ToArray();

        public Variable[] GlobalVariables => GlobalRoot.LocalVariables;

        public bool IsGlobalScope => Parent is null;

        public string InternalName { get; }

        public CallFrame? CallFrame { get; }

        public Interpreter Interpreter { get; }

        public VariableScope? Parent { get; }

        public VariableScope GlobalRoot { get; }


        private VariableScope(Interpreter interpreter, CallFrame? frame, VariableScope? parent)
        {
            Parent = parent;
            CallFrame = frame;
            Interpreter = interpreter;
            GlobalRoot = parent?.GlobalRoot ?? this;
            InternalName = parent is null ? "/" : $"{parent.InternalName}/{frame?.CurrentFunction.Name.ToLowerInvariant() ?? "::"}-{parent._children.Count}";
        }

        public void Dispose()
        {
            if (Parent is { _children: { } chd })
            {
                DestroyAllVariables(false);
                chd.TryRemove(this, out _);
            }
        }

        public override string ToString() => $"\"{InternalName}\"{(IsGlobalScope ? " (global)" : "")}: {_variables.Count} Variables, {_children.Count} Child scopes";

        public Variable CreateTemporaryVariable() => CreateVariable(SourceLocation.Unknown, $"tmp__{Guid.NewGuid():N}", false);

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

        public VariableScope CreateChildScope(CallFrame target)
        {
            VariableScope res = new VariableScope(Interpreter, target, this);

            while (!_children.TryAdd(res, default))
                ;

            return res;
        }

        public static VariableScope CreateGlobalScope(Interpreter interpreter) => new VariableScope(interpreter, null, null);
    }
}
