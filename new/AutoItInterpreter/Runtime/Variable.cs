using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System;

using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Runtime
{
    using static Generics;
    using static AST;


    public enum VariantType
        : int
    {
        Null = 0,
        Boolean,
        Number,
        String,
        Binary,
        Array,
        Map,
        Function,
        //NETObject,
        COMObject,
        Handle,
        Reference,
        Default = -1,
    }

    [DebuggerDisplay("{" + nameof(ToDebugString) + "(),nq}")]
    public readonly struct Variant
        : IEquatable<Variant>
        , IComparable<Variant>
    {
        #region STATIC PROPERTIES

        public static Variant Null { get; } = GetTypeDefault(VariantType.Null);

        public static Variant Default { get; } = GetTypeDefault(VariantType.Default);

        public static Variant EmptyString { get; } = FromString("");

        public static Variant EmptyBinary { get; } = FromBinary(Array.Empty<byte>());

        public static Variant True { get; } = FromBoolean(true);

        public static Variant False { get; } = FromBoolean(false);

        public static Variant Zero { get; } = FromNumber(0m);

        #endregion
        #region INSTANCE PROPERTIES

        public readonly VariantType Type { get; }

        /// <summary>
        /// This value <b>must</b> have one of the following types:
        /// <para/>
        /// <list type="table">
        ///     <!--
        ///     <listheader>
        ///         <term><see cref="VariantType"/> value of <see cref="Type"/></term>
        ///         <description>.NET Type</description>
        ///     </listheader>
        ///     -->
        ///     <item>
        ///         <term><see cref="VariantType.Null"/></term>
        ///         <description>&lt;<see langword="null"/>&gt;</description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.Default"/></term>
        ///         <description>&lt;<see langword="null"/>&gt;</description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.Reference"/></term>
        ///         <description><see cref="Variable"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.Boolean"/></term>
        ///         <description><see cref="bool"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.Number"/></term>
        ///         <description><see cref="decimal"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.String"/></term>
        ///         <description><see cref="string"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.Binary"/></term>
        ///         <description><see cref="byte"/>[]</description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.Array"/></term>
        ///         <description><see cref="Variant"/>[]</description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.Map"/></term>
        ///         <description><see cref="Dictionary{K,V}"/> with &lt;<see cref="Variant"/>,<see cref="Variant"/>&gt;</description>
        ///     <item>
        ///     </item>
        ///         <term><see cref="VariantType.Function"/></term>
        ///         <description><see cref="ScriptFunction"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.Handle"/></term>
        ///         <description><see cref="int"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.NETObject"/></term>
        ///         <description><see cref="object"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.COMObject"/></term>
        ///         <description><see cref="uint"/></description>
        ///     </item>
        /// </list>
        /// </summary>
        internal readonly object? RawData { get; }

        public readonly Variable? AssignedTo { get; }

        public readonly bool IsIndexable => RawData is IEnumerable || IsObject;

        public readonly bool IsReference => Type is VariantType.Reference;

        public readonly Variable? ReferencedVariable => IsReference ? RawData as Variable : null;

        public readonly bool IsNull => Type is VariantType.Null;

        public readonly bool IsDefault => Type is VariantType.Default;

        public readonly bool IsHandle => Type is VariantType.Handle;

        public readonly int Length => (RawData as IEnumerable)?.Count() ?? 0; // TODO : com object

        public readonly bool IsBinary => Type is VariantType.Binary;

        public readonly bool IsObject => Type is VariantType.COMObject; // TODO : or .netobject

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

        public readonly bool IsFunction([MaybeNullWhen(false), NotNullWhen(true)] out ScriptFunction? function) => (function = RawData as ScriptFunction) is { };

        public readonly int CompareTo(Variant other) =>
            RawData is string s1 && other.RawData is string s2 ? string.Compare(s1, s2, StringComparison.InvariantCultureIgnoreCase) : ToNumber().CompareTo(other.ToNumber());

        public readonly bool NotEquals(Variant other) => !EqualsCaseInsensitive(other); // TODO : unit tests

        public readonly bool EqualsCaseInsensitive(Variant other)
        {
            if (Type is VariantType.String && other.Type is VariantType.String)
                return string.Equals(ToString(), other.ToString(), StringComparison.InvariantCultureIgnoreCase);

            // TODO : binary compare

            return EqualsCaseSensitive(other);
        }

        public readonly bool EqualsCaseSensitive(Variant other) => Equals(other); // TODO : unit tests

        public readonly bool Equals(Variant other) => Type.Equals(other.Type) && Equals(RawData, other.RawData);

        public readonly override bool Equals(object? obj) => obj is Variant variant && Equals(variant);

        public readonly override int GetHashCode() => HashCode.Combine(Type, RawData);

        public readonly override string ToString() => Type switch
        {
            VariantType.Default => "Default",
            VariantType.Boolean or VariantType.Number or VariantType.String or VariantType.Handle => RawData?.ToString() ?? "",
            _ when RawData is Variable var => var.Value.ToString(),
            _ when RawData is byte[] { Length: > 0 } bytes => "0x" + From.Bytes(bytes).To.Hex(),
            _ => "",
        };

        public readonly string ToDebugString(Interpreter interpreter)
        {
            static string sanitize(char c) => c switch
            {
                '\0' => "\\0",
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                '\v' => "\\v",
                '\b' => "\\b",
                '\x1b' => "\\e",
                < ' ' or (> '~' and < '¡') => $"\\{(int)c + 0:x2}",
                '\\' or '"' => "\\" + c,
                _ => c.ToString()
            };

            if (IsNull || IsDefault)
                return Type.ToString();
            else if (RawData is Variant[] arr)
                return $"[{string.Join(", ", arr.Select(e => e.ToDebugString(interpreter)))}]";
            else if (Type is VariantType.Map)
                return $"[{string.Join(", ", ToMap(interpreter).Select(kvp => $"{kvp.Key.ToDebugString(interpreter)}={kvp.Value.ToDebugString(interpreter)}"))}]";
            else if (RawData is string or StringBuilder)
                return '"' + string.Concat(ToString().ToArray(sanitize)) + '"';
            else if (RawData is Variable v)
                return $"${v.Name}:{v.Value.ToDebugString(interpreter)}";
            else if (RawData is ScriptFunction func)
                return $"<{func.Location.FileName}>{func.Name}{func.ParameterCount}";
            else if (Type is VariantType.Handle && RawData is int id)
            {
                string data = "invalid";

                if (interpreter.GlobalObjectStorage.TryGet(id, out object? obj))
                    data = obj?.GetType().FullName ?? "null";

                return $"hnd:0x{id:x8} ({data})";
            }
            else if (Type is VariantType.COMObject && RawData is uint com)
                return $"COM:0x{com:x8}"; // todo : type ?
            else
                return ToString();
        }

        public readonly bool ToBoolean() => RawData switch
        {
            _ when Type is VariantType.Null or VariantType.Default => false,
            byte[] arr => arr.FirstOrDefault() != 0,
            string s => s.Length > 0,
            decimal d => d != 0m,
            int l => l != 0,
            bool b => b,
            null => false,
            _ => true,
        };

        public readonly decimal ToNumber() => RawData switch
        {
            _ when Type is VariantType.Default => -1m,
            true => 1m,
            false => 0m,
            int i => i,
            decimal d => d,
            string s when s.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) => long.TryParse(s[2..], NumberStyles.HexNumber, null, out long l) ? l : 0m,
            string s => decimal.TryParse(s, out decimal d) ? d : 0m,
            VariantType.Null or _ => 0m,
        };

        public readonly byte[] ToBinary() => RawData switch
        {
            bool b => new[] { (byte)(b ? 1 : 0) },
            _ when Type is VariantType.Default => From.Unmanaged(-1),
            null => From.Unmanaged(0),
            int i => From.Unmanaged(i),
            decimal d when d <= 2147483647m && d >= -2147483648 && d == (int)d => From.Unmanaged((int)d),
            decimal d when d <= 9223372036854775807m && d >= -9223372036854775808m && d == (long)d => From.Unmanaged((long)d),
            decimal d => From.Unmanaged((double)d), // TODO : allow 128bit numbers
            string s when s.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) => From.Hex(s[2..]),
            string s => From.String(s, BytewiseEncoding.Instance),
            _ => System.Array.Empty<byte>(),
        };

        public readonly Variant[] ToArray(Interpreter interpreter)
        {
            if (RawData is Array arr)
                return arr.Cast<object>().ToArray(o => FromObject(interpreter, o));
            else if (RawData is string s)
                return s.Cast<char>().ToArray(c => FromObject(interpreter, c));

            // TODO : COM objects
            // TODO : NET objects
            // TODO : maps

            else
                return Array.Empty<Variant>();
        }

        public readonly (Variant key, Variant value)[] ToOrderedMap(Interpreter interpreter)
        {
            List<(Variant, Variant)> output = new();

            if (RawData is Array arr)
                for (int i = 0; i < arr.Length; ++i)
                    output.Add((i, FromObject(interpreter, arr.GetValue(i))));
            else if (RawData is IDictionary<Variant, Variant> dic)
                foreach (Variant key in dic.Keys)
                    output.Add((key, dic[key]));
            else if (RawData is string s)
                for (int i = 0; i < s.Length; ++i)
                    output.Add((i, FromObject(interpreter, s[i])));
            else

                // TODO : COM objects
                // TODO : NET objects

                ;

            return output.ToArray();
        }

        public readonly IDictionary<Variant, Variant> ToMap(Interpreter interpreter) => ToOrderedMap(interpreter).ToDictionary();

        public readonly Variant AssignTo(Variable? parent) => new Variant(Type, RawData, parent);

        public readonly bool TrySetIndexed(Interpreter interpreter, Variant index, Variant value)
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
                    if (arr is Variant[] varr)
                        varr[idx] = value;
                    else if (arr is byte[] barr)
                        barr[idx] = (byte)value;
                    else
                        return false;

                    return true;
                }
            }
            else if (RawData is IDictionary<Variant, Variant> dic)
            {
                dic[index] = value;

                return true;
            }
            else if (Type is VariantType.COMObject && RawData is uint id)
                return interpreter.COMConnector?.TrySetIndex(id, index, value) ?? false;
            else
                return false;
        }

        public readonly bool TryGetIndexed(Interpreter interpreter, Variant index, out Variant value)
        {
            if (RawData is IDictionary<Variant, Variant> dic)
                return dic.TryGetValue(index, out value);

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
                    value = FromObject(interpreter, arr.GetValue(idx));

                    return true;
                }
            }
            else if (Type is VariantType.COMObject && RawData is uint id)
                return interpreter.COMConnector?.TryGetIndex(id, index, out value) ?? false;
            else if (index.EqualsCaseInsensitive(nameof(Length)))
            {
                value = Length;

                return true;
            }
            else
                return false;
        }

        public readonly bool TrySetMember(Interpreter interpreter, string member, Variant value)
        {
            if (Type is VariantType.COMObject && RawData is uint id)
                return interpreter.COMConnector?.TrySetMember(id, member, value) ?? false;
            else if (RawData is IDictionary<Variant, Variant> dic)
            {
                dic[member] = value;

                return true;
            }

            return false;
        }

        public readonly bool TryGetMember(Interpreter interpreter, string member, out Variant value)
        {
            value = default;

            if (Type is VariantType.COMObject && RawData is uint id)
                return interpreter.COMConnector?.TryGetMember(id, member, out value) ?? false;
            else if (RawData is IDictionary<Variant, Variant> dic)
                return dic.TryGetValue(member, out value);
            else if (string.Equals(member, nameof(Length), StringComparison.InvariantCultureIgnoreCase))
            {
                value = Length;

                return true;
            }

            return false;
        }

        public readonly bool ResizeArray(Interpreter interpreter, int new_size, [MaybeNullWhen(false), NotNullWhen(true)] out Variant? new_array)
        {
            if (RawData is byte[] bytes)
            {
                Array.Resize(ref bytes, new_size);

                new_array = FromBinary(bytes);
            }
            else if (RawData is Variant[] arr)
            {
                Array.Resize(ref arr, new_size);

                new_array = FromArray(interpreter, arr);
            }
            else
                new_array = null;
            
            return new_array is { };
        }

        public readonly bool TryResolveHandle(Interpreter interpreter, [MaybeNullWhen(false), NotNullWhen(true)] out object? value) =>
            interpreter.GlobalObjectStorage.TryGet((int)this, out value);

        public readonly bool TryResolveHandle<T>(Interpreter interpreter, [MaybeNullWhen(false), NotNullWhen(true)] out T? value) where T : class
        {
            value = null;
            TryResolveHandle(interpreter, out object? val);

            if (val is T t)
                value = t;

            return value is T;
        }

        #endregion
        #region STATIC METHODS

        internal static Variant GetTypeDefault(VariantType type) => type switch
        {
            VariantType.Boolean => False,
            VariantType.Number => Zero,
            VariantType.Binary => EmptyBinary,
            VariantType.String => EmptyString,
            VariantType.Array => NewArray(0),
            VariantType.Map => NewMap(),
            _ => new Variant(type, null, null),
        };

        public static Variant NewMap() => new Variant(VariantType.Map, new Dictionary<Variant, Variant>(), null);

        public static Variant NewArray(int length) => new Variant(VariantType.Array, new Variant[length], null);

        public static Variant FromObject(Interpreter interpreter, object? obj) => obj switch
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
            IEnumerable<byte> bytes => FromBinary(bytes),
            IEnumerable<Variant> arr => FromArray(interpreter, arr),
            IDictionary<Variant, Variant> dic => FromMap(interpreter, dic),
            ScriptFunction func => FromFunction(func),

            _ => throw new NotImplementedException(obj.ToString()),
            //_ => FromNETObject(obj),
        };

        public static Variant FromMap(Interpreter interpreter, params (Variant key, Variant value)[] pairs) => FromMap(interpreter, pairs.ToDictionary());

        public static Variant FromMap(Interpreter interpreter, IDictionary<Variant, Variant> dic)
        {
            Variant v = NewMap();
            bool error = true;

            foreach (Variant key in dic.Keys)
                error &= v.TrySetIndexed(interpreter, key, dic[key]);

            error ^= true;

            // TODO : report error?

            return v;
        }

        public static Variant FromArray(Interpreter interpreter, IEnumerable<Variant>? collection) => FromArray(interpreter, collection?.ToArray());

        public static Variant FromArray(Interpreter interpreter, params Variant[]? array)
        {
            Variant v = NewArray(array?.Length ?? 0);
            Variant i = Zero;

            foreach (object? element in array ?? Array.Empty<Variant>())
            {
                v.TrySetIndexed(interpreter, i, FromObject(interpreter, element));
                ++v;
            }

            return v;
        }

        public static Variant FromBinary(IEnumerable<byte> bytes) => FromBinary(bytes.ToArray());

        public static Variant FromBinary(byte[] bytes) => new Variant(VariantType.Binary, bytes, null);

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

        public static Variant FromHandle(uint handle) => new Variant(VariantType.Handle, handle, null);

        // public static Variant FromNETObject(object obj) => new Variant(VariantType.NETObject, obj, null);

        public static Variant FromNumber(decimal d) => new Variant(VariantType.Number, d, null);

        public static Variant FromString(string? s) => s is null ? Null : new Variant(VariantType.String, s, null);

        public static Variant FromBoolean(bool b) => new Variant(VariantType.Boolean, b, null);

        public static Variant FromFunction(ScriptFunction func) => new Variant(VariantType.Function, func, null);

        internal static Variant FromCOMObject(uint id) => new Variant(VariantType.COMObject, id, null);

        public static bool TryCreateCOM(Interpreter interpreter, string classname, string? server, string? username, string? passwd, [MaybeNullWhen(false), NotNullWhen(true)] out Variant? com_object)
        {
            com_object = null;

            try
            {
                if (interpreter.COMConnector?.TryCreateCOMObject(classname, server, username, passwd) is uint id)
                    com_object = FromCOMObject(id);
            }
            catch
            {
            }

            return com_object is { Type: VariantType.COMObject };
        }

        public static Variant BitwiseAnd(Variant v1, Variant v2) => (int)v1 & (int)v2;

        public static Variant BitwiseOr(Variant v1, Variant v2) => (int)v1 | (int)v2;

        public static Variant BitwiseXor(Variant v1, Variant v2) => (int)v1 ^ (int)v2;

        public static Variant BitwiseNot(Variant v) => ~(int)v;

        #endregion
        #region ARITHMETIC OPERATORS

        public static Variant operator ++(Variant v) => v + 1;

        public static Variant operator --(Variant v) => v - 1;

        public static Variant operator +(Variant v) => v;

        public static Variant operator ~(Variant v) => BitwiseNot(v);

        public static Variant operator !(Variant v) => FromBoolean(!v.ToBoolean());

        public static Variant operator -(Variant v) => v.IsDefault || v.IsNull ? v : FromNumber(-v.ToNumber());

        public static Variant operator +(Variant v1, Variant v2) => FromNumber(v1.ToNumber() + v2.ToNumber());

        public static Variant operator -(Variant v1, Variant v2) => FromNumber(v1.ToNumber() - v2.ToNumber());

        public static Variant operator *(Variant v1, Variant v2) => FromNumber(v1.ToNumber() * v2.ToNumber());

        public static Variant operator /(Variant v1, Variant v2) => FromNumber(v1.ToNumber() / v2.ToNumber());

        public static Variant operator %(Variant v1, Variant v2) => FromNumber(v1.ToNumber() % v2.ToNumber());

        public static Variant operator <<(Variant v, int offs) => offs < 0 ? v >> -offs : (int)v << offs;

        public static Variant operator >>(Variant v, int offs) => offs < 0 ? v << -offs : (int)v << offs;

        /// <summary>This is <b>not</b> XOR - this is the mathematical power operator!</summary>
        public static Variant operator ^(Variant v1, Variant v2) => FromNumber((decimal)Math.Pow((double)v1.ToNumber(), (double)v2.ToNumber()));

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

        public static implicit operator Variant(bool b) => FromBoolean(b);

        public static implicit operator Variant(sbyte n) => FromNumber(n);

        public static implicit operator Variant(byte n) => FromNumber(n);

        public static implicit operator Variant(short n) => FromNumber(n);

        public static implicit operator Variant(ushort n) => FromNumber(n);

        public static implicit operator Variant(int n) => FromNumber(n);

        public static implicit operator Variant(uint n) => FromNumber(n);

        public static implicit operator Variant(long n) => FromNumber(n);

        public static implicit operator Variant(ulong n) => FromNumber(n);

        public static implicit operator Variant(float n) => FromNumber((decimal)n);

        public static implicit operator Variant(double n) => FromNumber((decimal)n);

        public static implicit operator Variant(decimal n) => FromNumber(n);

        public static implicit operator Variant(char n) => FromString(n.ToString());

        public static implicit operator Variant(string? str) => FromString(str);

        public static implicit operator Variant(StringBuilder? sb) => FromString(sb?.ToString());

        // public static implicit operator Variant(Variant[]? n) => FromArray(n);

        public static implicit operator Variant(byte[] n) => FromBinary(n);

        public static explicit operator bool(Variant v) => v.ToBoolean();

        public static explicit operator sbyte(Variant v) => Convert.ToSByte(v.ToNumber());

        public static explicit operator byte(Variant v) => Convert.ToByte(v.ToNumber());

        public static explicit operator short(Variant v) => Convert.ToInt16(v.ToNumber());

        public static explicit operator ushort(Variant v) => Convert.ToUInt16(v.ToNumber());

        public static explicit operator int(Variant v) => Convert.ToInt32(v.ToNumber());

        public static explicit operator uint(Variant v) => Convert.ToUInt32(v.ToNumber());

        public static explicit operator long(Variant v) => Convert.ToInt64(v.ToNumber());

        public static explicit operator ulong(Variant v) => Convert.ToUInt64(v.ToNumber());

        public static explicit operator float(Variant v) => Convert.ToSingle(v.ToNumber());

        public static explicit operator double(Variant v) => Convert.ToDouble(v.ToNumber());

        public static explicit operator decimal(Variant v) => v.ToNumber();

        public static explicit operator char(Variant v) => v.ToString().FirstOrDefault();

        public static explicit operator string(Variant v) => v.ToString();

        public static explicit operator StringBuilder(Variant v) => new StringBuilder(v.ToString());

        public static explicit operator byte[](Variant v) => v.ToBinary();

        #endregion
    }

    public sealed class Variable
        : IEquatable<Variable>
    {
        private readonly object _mutex = new object();
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

                lock (_mutex)
                    target._value = value.AssignTo(target);
            }
        }

        public Interpreter Interpreter => DeclaredScope.Interpreter;

        public bool IsGlobal => DeclaredScope.IsGlobalScope;


        internal Variable(VariableScope scope, SourceLocation location, string name, bool isConst)
        {
            Name = name.TrimStart('$').ToLowerInvariant();
            DeclaredLocation = location;
            DeclaredScope = scope;
            IsConst = isConst;
            Value = Variant.Null;
        }

        public override string ToString() => $"${Name}: {Value.ToDebugString(Interpreter)}";

        public override int GetHashCode() => Name.GetHashCode(StringComparison.InvariantCultureIgnoreCase);

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

        public Variable CreateTemporaryVariable() => CreateVariable((CallFrame as AU3CallFrame)?.CurrentLocation ?? SourceLocation.Unknown, $"tmp__{Guid.NewGuid():N}", false);

        public Variable CreateVariable(SourceLocation location, VARIABLE variable, bool isConst) => CreateVariable(location, variable.Name, isConst);

        public Variable CreateVariable(SourceLocation location, string name, bool isConst) => Interpreter.Telemetry.Measure(TelemetryCategory.VariableCreation, delegate
        {
            if (!TryGetVariable(name, VariableSearchScope.Local, out Variable? var))
            {
                var = new Variable(this, location, name, isConst);

                _variables.TryAdd(var, default);
            }

            return var;
        });

        public bool HasVariable(string name, VariableSearchScope scope) => TryGetVariable(name, scope, out _);

        public bool HasVariable(VARIABLE variable, VariableSearchScope scope) => HasVariable(variable.Name, scope);

        public bool TryGetVariable(VARIABLE input, VariableSearchScope scope, [MaybeNullWhen(false), NotNullWhen(true)] out Variable? variable) =>
            TryGetVariable(input.Name, scope, out variable);

        public bool TryGetVariable(string name, VariableSearchScope scope, [MaybeNullWhen(false), NotNullWhen(true)] out Variable? variable)
        {
            Variable? v = null;
            bool resolved = Interpreter.Telemetry.Measure(TelemetryCategory.VariableResolution, delegate
            {
                foreach (Variable var in _variables.Keys)
                    if (var.Equals(name))
                    {
                        v = var;

                        return true;
                    }

                return Parent is { } && scope != VariableSearchScope.Local ? Parent.TryGetVariable(name, scope, out v) : false;
            });

            variable = v;

            return resolved && variable is { };
        }

        public bool DestroyVariable(string name, VariableSearchScope scope)
        {
            if (TryGetVariable(name, scope, out Variable? var) && _variables.TryRemove(var, out _))
                return true;
            else if (scope != VariableSearchScope.Local)
                return Parent?.DestroyVariable(name, scope) ?? false;

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

    public sealed class GlobalObjectStorage
        : IDisposable
    {
        private readonly ConcurrentDictionary<uint, object> _objects = new ConcurrentDictionary<uint, object>();


        public Interpreter Interpreter { get; }

        public Variant[] HandlesInUse => _objects.Keys.Select(Variant.FromHandle).ToArray();

        public int ObjectCount => _objects.Count;


        internal GlobalObjectStorage(Interpreter interpreter)
        {
            Interpreter = interpreter;
        }

        private Variant GetFreeId()
        {
            uint id = 1;

            foreach (uint key in _objects.Keys.OrderBy(Generics.id))
                if (key == id)
                    ++id;
                else
                    break;

            while (_objects.Keys.Contains(id))
                ++id;

            return Variant.FromHandle(id);
        }

        public Variant Store<T>(T item) where T : class
        {
            Variant handle = GetFreeId();

            TryUpdate(handle, item);

            return handle;
        }

        public bool TryGet(Variant handle, [MaybeNullWhen(false), NotNullWhen(true)] out object? item)
        {
            item = null;

            return handle.Type is VariantType.Handle && _objects.TryGetValue((uint)handle, out item);
        }

        public bool TryGet<T>(Variant handle, out T? item) where T : class
        {
            bool res = TryGet(handle, out object? value);

            item = value as T;

            return res;
        }

        public bool TryUpdate<T>(Variant handle, T item)
            where T : class
        {
            bool success;

            if (success = (handle.Type is VariantType.Handle))
                _objects[(uint)handle] = item;

            return success;
        }

        public void Dispose()
        {
            foreach (Variant handle in HandlesInUse)
                Delete(handle);
        }

        public bool Delete(Variant handle) => handle.Type is VariantType.Handle && Delete((uint)handle);

        private bool Delete(uint id)
        {
            bool success = _objects.TryRemove(id, out object? obj);

            if (success && obj is IDisposable disp)
                disp.Dispose();

            return success;
        }
    }

    public enum VariableSearchScope
    {
        Local,
        Global
    }
}
