using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Text;
using System;

using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Runtime
{
    using static LINQ;
    using static AST;


    /// <summary>
    /// An enum containing all possible data types that an instance of <see cref="Variant"/> can represent.
    /// </summary>
    public enum VariantType
        : int
    {
        /// <summary>
        /// The constant Null-value (typically only used using the static member <see cref="Variant.Null"/>.
        /// </summary>
        Null = 0,
        /// <summary>
        /// Represents a boolean data value.
        /// </summary>
        Boolean,
        /// <summary>
        /// Represents a numerical data value.
        /// </summary>
        Number,
        /// <summary>
        /// Represents a data value of the type <see cref="string"/>.
        /// </summary>
        String,
        /// <summary>
        /// Represents a binary data value.
        /// </summary>
        Binary,
        /// <summary>
        /// Represents an array of zero or more elements.
        /// </summary>
        Array,
        /// <summary>
        /// Represents an injective map.
        /// </summary>
        Map,
        /// <summary>
        /// Represents a pointer to a native or user function.
        /// </summary>
        Function,
        /// <summary>
        /// Represents an <see cref="uint"/>-handle pointing towards a COM object instance.
        /// </summary>
        COMObject,
        /// <summary>
        /// Represents an <see cref="uint"/>-handle pointing towards a managed object instance.
        /// </summary>
        Handle,
        /// <summary>
        /// Represents a reference to an instance of <see cref="Variable"/> (This is only used in <see langword="ByRef"/>-parameters).
        /// </summary>
        Reference,
        /// <summary>
        /// The constant Default-value (typically only used using the static member <see cref="Variant.Default"/>.
        /// </summary>
        Default = -1,
    }

    /// <summary>
    /// Represents the <see cref="Variant"/> data type which is the fundamental data type for all AutoIt <see cref="Variable"/>s.
    /// The default value of this type resolves to <see cref="Null"/>.
    /// <para/>
    /// Data is internally stored using a <see cref="VariantType"/> (to resolve the semantic type), a <see cref="object"/> containing the actual data, and an optional <see cref="Variable"/> reference.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToDebugString__Internal__) + "(),nq}")]
    public readonly struct Variant
        : IEquatable<Variant>
        , IComparable<Variant>
    {
        #region STATIC PROPERTIES

        /// <summary>
        /// Represents the constant <see cref="Null"/>-value which is accessible using the AutoIt keyword '<see langword="Null"/>'.
        /// <para/>
        /// The internally stored data is (<see cref="VariantType.Null"/>, <see langword="null"/>).
        /// </summary>
        public static Variant Null { get; } = GetTypeDefault(VariantType.Null);

        /// <summary>
        /// Represents the constant <see cref="Default"/>-value which is accessible using the AutoIt keyword '<see langword="Default"/>'.
        /// <para/>
        /// The internally stored data is (<see cref="VariantType.Default"/>, <see langword="null"/>).
        /// </summary>
        public static Variant Default { get; } = GetTypeDefault(VariantType.Default);

        /// <summary>
        /// Represents an empty string.
        /// <para/>
        /// The internally stored data is (<see cref="VariantType.String"/>, <see cref="string.Empty"/>).
        /// </summary>
        public static Variant EmptyString { get; } = FromString(string.Empty);

        /// <summary>
        /// Represents an empty binary string / empty byte array.
        /// <para/>
        /// The internally stored data is (<see cref="VariantType.Binary"/>, <see langword="new byte"/>[<see cref="0"/>]).
        /// </summary>
        public static Variant EmptyBinary { get; } = FromBinary(Array.Empty<byte>());

        /// <summary>
        /// Represents the constant boolean <see cref="True"/>-value which is accessible using the AutoIt keyword '<see langword="True"/>'.
        /// <para/>
        /// The internally stored data is (<see cref="VariantType.Boolean"/>, <see langword="true"/>).
        /// </summary>
        public static Variant True { get; } = FromBoolean(true);

        /// <summary>
        /// Represents the constant boolean <see cref="False"/>-value which is accessible using the AutoIt keyword '<see langword="False"/>'.
        /// <para/>
        /// The internally stored data is (<see cref="VariantType.Boolean"/>, <see langword="true"/>).
        /// </summary>
        public static Variant False { get; } = FromBoolean(false);

        /// <summary>
        /// Represents the constant numeric <see cref="Zero"/>-value.
        /// <para/>
        /// The internally stored data is (<see cref="VariantType.Number"/>, <see cref="0m"/>).
        /// </summary>
        public static Variant Zero { get; } = FromNumber(0);

        #endregion
        #region INSTANCE PROPERTIES

        /// <summary>
        /// Returns the semantic data type represent this instance.
        /// </summary>
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
        ///         <description><see cref="double"/></description>
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
        ///         <description><see cref="IntPtr"/> (<see langword="nint"/>)</description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="VariantType.COMObject"/></term>
        ///         <description><see cref="uint"/></description>
        ///     </item>
        /// </list>
        /// The application's behavior is undefined should the value does not match the criteria above.
        /// </summary>
        private readonly object? RawData { get; }

        internal Type RawType => RawData?.GetType() ?? typeof(void);

        /// <summary>
        /// Returns the optional <see cref="Variable"/> to which this value has been assigned.
        /// </summary>
        public readonly Variable? AssignedTo { get; }

        /// <summary>
        /// Indicates whether this value is indexable.
        /// </summary>
        public readonly bool IsIndexable => RawData is IEnumerable || IsObject;

        /// <summary>
        /// Indicates whether this value is a reference to an instance of <see cref="Variable"/> (This is only used in <see langword="ByRef"/>-parameters).
        /// </summary>
        public readonly bool IsReference => Type is VariantType.Reference;

        /// <summary>
        /// Returns the referenced <see cref="Variable"/> if the current <see cref="Type"/> has the value "<see cref="VariantType.Reference"/>" - otherwise returns <see langword="null"/>.
        /// </summary>
        public readonly Variable? ReferencedVariable => IsReference ? RawData as Variable : null;

        /// <summary>
        /// Indicates whether the current instance is equal to <see cref="Null"/>.
        /// </summary>
        public readonly bool IsNull => Type is VariantType.Null;

        /// <summary>
        /// Indicates whether the current instance is equal to <see cref="Default"/>.
        /// </summary>
        public readonly bool IsDefault => Type is VariantType.Default;
        
        public readonly bool IsNumber => Type is VariantType.Number;

        public readonly bool IsNumeric => Type is VariantType.Number or VariantType.Handle or VariantType.COMObject;

        /// <summary>
        /// Indicates whether the current instance contains an handle to an (internally managed) object. This is not to be confused with an handle of the type "<see cref="VariantType.COMObject"/>".
        /// </summary>
        public readonly bool IsHandle => Type is VariantType.Handle;

        /// <summary>
        /// Indicates whether the current instance is a valid C++/C#/C pointer address. This requires the current value to be a positive non-zero integer smaller or equal to <see cref="ulong.MaxValue"/>.
        /// </summary>
        public readonly bool IsPtr => ToNumber() is double d and > 0 and <= ulong.MaxValue && (ulong)d == d;

        /// <summary>
        /// Returns the semantic length of this value (e.g. elements in an array/map, length of a regular or binary string, etc.)
        /// </summary>
        public readonly int Length => (RawData as IEnumerable)?.Count() ?? 0; // TODO : com object

        /// <summary>
        /// Indicates whether this value represents a binary string.
        /// </summary>
        public readonly bool IsBinary => Type is VariantType.Binary;

        /// <summary>
        /// Indicates whether this value represents a non-binary string.
        /// </summary>
        public readonly bool IsString => Type is VariantType.String;

        /// <summary>
        /// Indicates whether the current instance represents an object (namely "<see cref="VariantType.COMObject"/>" or "<see cref="VariantType.NETObject"/>"). This is not to be confused with <see cref="IsHandle"/>.
        /// </summary>
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

        /// <summary>
        /// Returns whether the current instance is a pointer to a native or user-defined function.
        /// </summary>
        /// <param name="function">Sets this value with the internally stored function reference if the return value is <see cref="true"/>. Otherwise sets it to <see langword="null"/>.</param>
        /// <returns>Indicator whether the current instance represents a pointer to a function.</returns>
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

        /// <summary>
        /// Returns the string representation of the current instance in accordance with the official AutoIt specification.
        /// Use <see cref="ToDebugString(Interpreter)"/> for a more detailed string representation.
        /// </summary>
        /// <inheritdoc/>
        /// <returns>String representation</returns>
        public readonly override string ToString() => Type switch
        {
            VariantType.Default => "Default",
            VariantType.Boolean or VariantType.String or VariantType.Handle => RawData?.ToString() ?? "",
            VariantType.Number when RawData is { } raw => FunctionExtensions.Do(delegate
            {
                double value = Convert.ToDouble(raw);

                if (double.IsNaN(value))
                    return "-1.#IND";
                else if (double.IsPositiveInfinity(value))
                    return "1.#INF";
                else if (double.IsNegativeInfinity(value))
                    return "-1.#INF";
                else
                    return value.ToString();
            }),
            _ when RawData is Variable var => var.Value.ToString(),
            _ when RawData is byte[] { Length: > 0 } bytes => "0x" + From.Bytes(bytes).ToHexString(),
            _ => "",
        };

        private readonly string ToDebugString__Internal__() => ToDebugString(Interpreter.ActiveInstances.First());

        public readonly string ToDebugString(Interpreter interpreter) => ToDebugString(interpreter, new(), 0);

        private readonly string ToDebugString(Interpreter interpreter, HashSet<Variable> forbidden, int level)
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

            if (AssignedTo is Variable variable)
                forbidden.Add(variable);

            if (IsNull || IsDefault)
                return Type.ToString();
            else if (RawData is ScriptFunction func)
                return $"<{func.Location.FullFileName}>{func.Name}{func.ParameterCount}";
            else if (RawData is string or StringBuilder)
                return '"' + string.Concat(ToString().ToArray(sanitize)) + '"';
            else if (Type is VariantType.Handle)
            {
                string data = "invalid/hwnd";
                nint id = (nint)this;

                if (interpreter.GlobalObjectStorage.TryGet(id, out object? obj))
                    data = obj is StaticTypeReference(Type t) ? $"static {t.Name}" : obj?.GetType().Name ?? "null";

                return $"hnd:0x{id:x8} ({data})";
            }
            else if (Type is VariantType.COMObject && RawData is uint com)
                return $"COM:0x{com:x8}"; // TODO : type ?
            else if (level > 5)
                return "...";
            else if (RawData is Variant[] arr)
                return $"[{string.Join(", ", arr.Select(e => e.ToDebugString(interpreter, forbidden, level + 1)))}]";
            else if (Type is VariantType.Map)
                return $"[{string.Join(", ", ToMap(interpreter).Select(pair => $"{pair.Key.ToDebugString(interpreter, forbidden, level + 1)}={pair.Value.ToDebugString(interpreter, forbidden, level + 1)}"))}]";
            else if (RawData is Variable v)
                return forbidden.Contains(v) ? "ref $" + v.Name : $"ref ${v.Name}:{v.Value.ToDebugString(interpreter, forbidden, level + 1)}";
            else
                return ToString();
        }

        /// <summary>
        /// Returns the boolean representation of the current instance in accordance with the official AutoIt specification.
        /// </summary>
        /// <returns>Boolean representation</returns>
        public readonly bool ToBoolean() => RawData switch
        {
            _ when Type is VariantType.Null or VariantType.Default => false,
            byte[] arr => arr.FirstOrDefault() != 0,
            string s => s.Length > 0,
            double d => d != 0d,
            int l => l != 0,
            nint l => l != 0,
            uint l => l != 0,
            bool b => b,
            null => false,
            _ => true,
        };

        /// <summary>
        /// Returns the numerical representation of the current instance in accordance with the official AutoIt specification.
        /// </summary>
        /// <returns>Numerical representation</returns>
        public readonly double ToNumber() => RawData switch
        {
            _ when Type is VariantType.Default => -1d,
            true => 1d,
            false => 0d,
            int i => i,
            nint i => i,
            uint i => i,
            double d => d,
            string s when s.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) => long.TryParse(s[2..], NumberStyles.HexNumber, null, out long l) ? l : 0d,
            string s => double.TryParse(s, out double d) ? d : 0d,
            VariantType.Null or _ => 0d,
        };

        private readonly double ToNumber(double min, double max) => Math.Max(min, Math.Min(ToNumber(), max));

        /// <summary>
        /// Returns the binary representation of the current instance in accordance with the official AutoIt specification.
        /// </summary>
        /// <returns>Binary representation</returns>
        public readonly byte[] ToBinary() => RawData switch
        {
            bool b => new[] { (byte)(b ? 1 : 0) },
            _ when Type is VariantType.Default => From.Unmanaged(-1),
            null => From.Unmanaged(0),
            int i => From.Unmanaged(i),
            double d when d <= 2147483647 && d >= -2147483648 && d == (int)d => From.Unmanaged((int)d),
            double d when d <= 9223372036854775807 && d >= -9223372036854775808 && d == (long)d => From.Unmanaged((long)d),
            double d => From.Unmanaged(d), // TODO : allow 128bit numbers
            string s when s.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) => From.Hex(s[2..]),
            string s => From.String(s, BytewiseEncoding.Instance),
            _ => Array.Empty<byte>(),
        };

        /// <summary>
        /// Returns the <see cref="Variant"/>-array stored inside current instance (or an empty array if this instance could not be converted).
        /// </summary>
        /// <param name="interpreter">The interpreter instance, with which the array is associated.</param>
        /// <returns>The array.</returns>
        public readonly Variant[] ToArray(Interpreter interpreter)
        {
            if (RawData is Array arr)
                return arr.Cast<object>().ToArray(o => FromObject(interpreter, o));
            else if (RawData is string s)
                return s.Cast<char>().ToArray(c => FromObject(interpreter, c));
            // else if (Type is VariantType.Handle)

            // TODO : COM objects
            // TODO : NET objects
            // TODO : maps

            else
                return Array.Empty<Variant>();
        }

        /// <summary>
        /// Returns an ordered map of key-value-pairs stored inside current instance (or an empty map if this instance could not be converted).
        /// </summary>
        /// <param name="interpreter">The interpreter instance, with which the map is associated.</param>
        /// <returns>The ordered map.</returns>
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
            // else if (Type is VariantType.Handle)
            //     ;
            // else
            //     ;

            // TODO : NET objects
            // TODO : COM objects

            return output.ToArray();
        }

        /// <summary>
        /// Returns a map of key-value-pairs stored inside current instance (or an map array if this instance could not be converted).
        /// <para/>
        /// Note: This map might not be ordered. Use <see cref="ToOrderedMap(Interpreter)"/> if you need its ordered variant.
        /// </summary>
        /// <param name="interpreter">The interpreter instance, with which the map is associated.</param>
        /// <returns>The map.</returns>
        public readonly IDictionary<Variant, Variant> ToMap(Interpreter interpreter) => ToOrderedMap(interpreter).ToDictionary();

        public unsafe object? ToCPPObject(Type type, Interpreter interpreter)
        {
            if (IsNull)
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            else if(type.IsAssignableFrom(RawType))
                return RawData;
            else if (type == typeof(string))
                return ToString();
            else if (type == typeof(StringBuilder))
                return new StringBuilder(ToString());
            else if (type == typeof(bool))
                return (bool)this;
            else if (type == typeof(sbyte))
                return (sbyte)this;
            else if (type == typeof(byte))
                return (byte)this;
            else if (type == typeof(ushort))
                return (ushort)this;
            else if (type == typeof(short))
                return (short)this;
            else if (type == typeof(int))
                return (int)this;
            else if (type == typeof(uint))
                return (uint)this;
            else if (type == typeof(long))
                return (long)this;
            else if (type == typeof(ulong))
                return (ulong)this;
            else if (type == typeof(float))
                return (float)this;
            else if (type == typeof(double))
                return (double)this;
            else if (type == typeof(decimal))
                return (decimal)this;
            else if (type == typeof(char))
                return (char)this;
            else if (type == typeof(nint))
                return (nint)(ulong)this;
            else if (type == typeof(nuint))
                return (nuint)(ulong)this;
            else if (type.IsPointer)
                return Pointer.Box((void*)(ulong)this, type);
            else
            {
                if (!TryResolveHandle(interpreter, out object? value))
                    value = RawData;

                if (value is null ? type.IsClass : type.IsAssignableFrom(value.GetType()))
                    return value;

                // TODO : resolve handle, then type cast
                // TODO : general type cast
                // TODO : array conversions

                else
                    throw new NotImplementedException($"{this} --> {type}");
            }
        }

        /// <summary>
        /// Assigns a copy of the current instance to the given variable and returns the associated copy.
        /// <para/>
        /// The value of this instance's property "<see cref="AssignedTo"/>" will <i>not</i> be changed.
        /// </summary>
        /// <param name="parent">Variable to which a copy of this value will be assigned. A value of <see langword="null"/> removes any previous assignment.</param>
        /// <returns>Copy of the current instance with the given variable assigned to it.</returns>
        public readonly Variant AssignTo(Variable? parent) => new(Type, RawData, parent);

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
            else if (TryResolveHandle(interpreter, out object? instance))
                return interpreter.GlobalObjectStorage.TrySetNETIndex(instance, index, value);
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
            else if (TryResolveHandle(interpreter, out object? instance))
                return interpreter.GlobalObjectStorage.TryGetNETIndex(instance, index, out value);
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
            else if (TryResolveHandle(interpreter, out object? instance))
                return interpreter.GlobalObjectStorage.TrySetNETMember(instance, member, value);
            else if (RawData is IDictionary<Variant, Variant> dic)
            {
                dic[member] = value;

                return true;
            }
            else
                return false;
        }

        public readonly bool TryGetMember(Interpreter interpreter, string member, out Variant value)
        {
            value = default;

            if (Type is VariantType.COMObject && RawData is uint id)
                return interpreter.COMConnector?.TryGetMember(id, member, out value) ?? false;
            else if (TryResolveHandle(interpreter, out object? instance))
                return interpreter.GlobalObjectStorage.TryGetNETMember(instance, member, out value);
            else if (RawData is IDictionary<Variant, Variant> dic)
                return dic.TryGetValue(member, out value);
            else if (string.Equals(member, nameof(Length), StringComparison.InvariantCultureIgnoreCase))
            {
                value = Length;

                return true;
            }
            else
                return false;
        }

        public readonly bool TryInvoke(Interpreter interpreter, string member, Variant[] arguments, out Variant value)
        {
            value = Zero;

            // TODO : implement COM Object instance calls

            return TryResolveHandle(interpreter, out object? instance) && interpreter.GlobalObjectStorage.TryInvokeNETMember(instance, member, arguments, out value);
        }

        public readonly (Variant Name, bool IsMethod)[] EnumerateMemberNames(Interpreter interpreter)
        {
            if (RawData is IDictionary<Variant, Variant> { Keys: IEnumerable<Variant> keys })
                return keys.ToArray(k => (k, false));
            else if (RawData is Array array)
                return Enumerable.Range(0, array.Length).Select(i => (Variant)i).Append(nameof(Length)).ToArray(k => (k, false));
            else if (TryResolveHandle(interpreter, out object? instance))
                return interpreter.GlobalObjectStorage.TryListNETMembers(instance).ToArray(m => ((Variant)m.Name, m.IsMethod));
            else
                return new[] { ((Variant)nameof(Length), false) };
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

        /// <summary>
        /// Creates a new (empty) map.
        /// </summary>
        /// <returns>Instance containing the newly created map.</returns>
        public static Variant NewMap() => new(VariantType.Map, new Dictionary<Variant, Variant>(), null);

        /// <summary>
        /// Creates a new array with the given length.
        /// </summary>
        /// <param name="length">Length of the array. Must be non-negative.</param>
        /// <returns>Instance containing the newly created array.</returns>
        public static Variant NewArray(int length) => new(VariantType.Array, new Variant[length], null);

        /// <summary>
        /// Converts the given object into an appropriate instance of <see cref="Variant"/>.
        /// <para/>
        /// This method does not create a handle from a given .NET Object. Use <see cref="GlobalObjectStorage.Store{T}(T)"/> for that functionality.
        /// </summary>
        /// <param name="interpreter">The interpreter instance used to convert the given object.</param>
        /// <param name="obj">Object to be converted.</param>
        /// <exception cref="NotImplementedException"/>
        /// <returns>Converted object.</returns>
        public static unsafe Variant FromObject(Interpreter interpreter, object? obj) => obj switch
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
            nint n => FromHandle(n),
            nuint n => FromHandle((nint)n),
            uint n => FromNumber(n),
            long n => FromNumber(n),
            ulong n => FromNumber(n),
            float n => FromNumber(n),
            double n => FromNumber(n),
            decimal n => FromNumber((double)n),
            char c => FromString(c.ToString()),
            string str => FromString(str),
            StringBuilder builder => FromString(builder.ToString()),
            IEnumerable<byte> bytes => FromBinary(bytes),
            IEnumerable<Variant> array => FromArray(interpreter, array),
            IDictionary<Variant, Variant> dic => FromMap(interpreter, dic),

            // convert any ienumerable

            ScriptFunction func => FromFunction(func),
            _ when obj.GetType().IsPointer => FromNumber((ulong)Pointer.Unbox(obj)),
            _ => interpreter.GlobalObjectStorage.GetOrStore(obj),
            // _ => throw new NotImplementedException(obj.ToString()),
        };

        /// <summary>
        /// Creates a new map from the given key-value-pairs.
        /// </summary>
        /// <param name="interpreter">The interpreter instance used for the map creation.</param>
        /// <param name="pairs">Collection of key-value-pairs.</param>
        /// <returns>The newly created map.</returns>
        public static Variant FromMap(Interpreter interpreter, params (Variant key, Variant value)[] pairs) => FromMap(interpreter, pairs.ToDictionary());

        /// <summary>
        /// Creates a new map from the given key-value-pairs.
        /// </summary>
        /// <param name="interpreter">The interpreter instance used for the map creation.</param>
        /// <param name="dic">Collection of key-value-pairs.</param>
        /// <returns>The newly created map.</returns>
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

        /// <summary>
        /// Creates a new array from the given value collection.
        /// </summary>
        /// <param name="interpreter">The interpreter instance used to create the array.</param>
        /// <param name="collection">(Ordered) collection of values.</param>
        /// <returns>THe newly created array.</returns>
        public static Variant FromArray(Interpreter interpreter, IEnumerable<Variant>? collection) => FromArray(interpreter, collection?.ToArray());

        /// <summary>
        /// Creates a new array from the given value collection.
        /// </summary>
        /// <param name="interpreter">The interpreter instance used to create the array.</param>
        /// <param name="array">(Ordered) collection of values.</param>
        /// <returns>THe newly created array.</returns>
        public static Variant FromArray(Interpreter interpreter, params Variant[]? array)
        {
            Variant v = NewArray(array?.Length ?? 0);
            Variant i = Zero;

            foreach (object? element in array ?? Array.Empty<Variant>())
            {
                v.TrySetIndexed(interpreter, i, FromObject(interpreter, element));
                ++i;
            }

            return v;
        }

        /// <summary>
        /// Creates a new binary string from the given bytes.
        /// </summary>
        /// <param name="bytes">(Ordered) collection of bytes.</param>
        /// <returns>Newly created binary string.</returns>
        public static Variant FromBinary(IEnumerable<byte> bytes) => FromBinary(bytes.ToArray());

        /// <summary>
        /// Creates a new binary string from the given bytes.
        /// </summary>
        /// <param name="bytes">(Ordered) collection of bytes.</param>
        /// <returns>Newly created binary string.</returns>
        public static Variant FromBinary(byte[] bytes) => new(VariantType.Binary, bytes, null);

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
            else if (literal is LITERAL.Number { Item: double d })
                return FromNumber(d);
            else
                throw new InvalidCastException($"Unable to convert the value '{literal}' to an instance of the type '{typeof(Variant)}'");
        }

        public static Variant FromReference(Variable variable) => new(VariantType.Reference, variable, null);

        public static Variant FromHandle(nint handle) => new(VariantType.Handle, handle, null);

        // public static Variant FromNETObject(object obj) => new(VariantType.NETObject, obj, null);

        public static Variant FromNumber(double value) => new(VariantType.Number, value, null);

        public static Variant FromString(string? value) => value is null ? Null : new(VariantType.String, value, null);

        public static Variant FromBoolean(bool value) => new(VariantType.Boolean, value, null);

        public static Variant FromFunction(ScriptFunction function) => new(VariantType.Function, function, null);

        internal static Variant FromCOMObject(uint id) => new(VariantType.COMObject, id, null);

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

        /// <summary>
        /// Inverts the given 32bit value bitwise.
        /// </summary>
        /// <param name="v">Value to be bitwise inverted.</param>
        /// <returns>Bitwise inverted value.</returns>
        public static Variant BitwiseNot(Variant v) => ~(int)v;

        #endregion
        #region ARITHMETIC OPERATORS

        public static Variant operator ++(Variant v) => v + 1;

        public static Variant operator --(Variant v) => v - 1;

        public static Variant operator +(Variant v) => v;

        /// <inheritdoc cref="BitwiseNot"/>
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
        public static Variant operator ^(Variant v1, Variant v2) => FromNumber(Math.Pow(v1.ToNumber(), v2.ToNumber()));

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

        public static implicit operator Variant(bool value) => FromBoolean(value);

        public static implicit operator Variant(sbyte value) => FromNumber(value);

        public static implicit operator Variant(byte value) => FromNumber(value);

        public static implicit operator Variant(short value) => FromNumber(value);

        public static implicit operator Variant(ushort value) => FromNumber(value);

        public static implicit operator Variant(int value) => FromNumber(value);

        public static implicit operator Variant(uint value) => FromNumber(value);

        public static implicit operator Variant(nint value) => FromHandle(value);

        public static implicit operator Variant(nuint value) => FromHandle((nint)value);

        public static implicit operator Variant(long value) => FromNumber(value);

        public static implicit operator Variant(ulong value) => FromNumber(value);

        public static implicit operator Variant(float value) => FromNumber(value);

        public static implicit operator Variant(double value) => FromNumber(value);

        public static implicit operator Variant(decimal value) => FromNumber((double)value);

        public static implicit operator Variant(char value) => FromString(value.ToString());

        public static implicit operator Variant(string? value) => FromString(value);

        public static implicit operator Variant(StringBuilder? builder) => FromString(builder?.ToString());

        public static implicit operator Variant(ScriptFunction function) => FromFunction(function);

        // public static implicit operator Variant(Variant[]? n) => FromArray(n);

        public static implicit operator Variant(byte[] n) => FromBinary(n);

        public static explicit operator bool(Variant v) => v.ToBoolean();

        public static explicit operator sbyte(Variant v) => Convert.ToSByte(v.ToNumber(sbyte.MinValue, sbyte.MaxValue));

        public static explicit operator byte(Variant v) => Convert.ToByte(v.ToNumber(byte.MinValue, byte.MaxValue));

        public static explicit operator short(Variant v) => Convert.ToInt16(v.ToNumber(short.MinValue, short.MaxValue));

        public static explicit operator ushort(Variant v) => Convert.ToUInt16(v.ToNumber(ushort.MinValue, ushort.MaxValue));

        public static explicit operator int(Variant v) => v.RawData is int i ? i : Convert.ToInt32(v.ToNumber(int.MinValue, int.MaxValue));

        public static explicit operator uint(Variant v) => v.RawData is uint i ? i : Convert.ToUInt32(v.ToNumber(uint.MinValue, uint.MaxValue));

        public static explicit operator nint(Variant v) => v.RawData switch
        {
            nint n => n,
            nuint nu => (nint)nu,
            long l => (nint)l,
            ulong ul => (nint)ul,
            uint u => (nint)u,
            int i => i,
            _ => (nint)(long)v,
        };

        public static explicit operator nuint(Variant v) => (nuint)(nint)v;

        public static explicit operator long(Variant v) => Convert.ToInt64(v.ToNumber(long.MinValue, long.MaxValue));

        public static explicit operator ulong(Variant v) => Convert.ToUInt64(v.ToNumber(ulong.MinValue, ulong.MaxValue));

        public static explicit operator float(Variant v) => Convert.ToSingle(v.ToNumber());

        public static explicit operator double(Variant v) => Convert.ToDouble(v.ToNumber());

        public static explicit operator decimal(Variant v) => (decimal)v.ToNumber();

        public static explicit operator char(Variant v) => v.ToString().FirstOrDefault();

        public static explicit operator string(Variant v) => v.ToString();

        public static explicit operator StringBuilder(Variant v) => new(v.ToString());

        public static explicit operator byte[](Variant v) => v.ToBinary();

        #endregion
    }
}
