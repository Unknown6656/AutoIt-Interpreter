using System.Collections.Generic;
using System.Globalization;
using System.Collections;
using System.Reflection;
using System.Linq;
using System.Text;
using System.IO;
using System;

namespace AutoItCoreLibrary
{
    public unsafe struct AutoItVariantType
        : IEquatable<AutoItVariantType>
        , IComparable<AutoItVariantType>
        , IEnumerable<AutoItVariantType>
        , ICloneable
    {
        #region PRIVATE FIELDS

        private readonly AutoItVariantData _data;

        #endregion
        #region PUBLIC PROPERTIES

        public byte[] BinaryData { get; }

        public AutoItVariantType this[long index]
        {
            set
            {
                if (!IsArray || (index < 0) || (index >= Length))
                    throw new InvalidArrayAccessExcpetion();
                else
                    ((AutoItVariantType[])_data.VariantData)[index] = value.Clone();
            }
            get => IsArray && (index >= 0) && (index < Length) ? ((AutoItVariantType[])_data.VariantData)[index] : throw new InvalidArrayAccessExcpetion();
        }

        public AutoItVariantType this[params long[] indices]
        {
            set
            {
                if (indices.Length == 0)
                    throw new InvalidArrayAccessExcpetion();
                else if (indices.Length == 1)
                    this[indices[0]] = value;
                else
                {
                    AutoItVariantType matr = this[indices[0]];

                    matr[indices.Skip(1).ToArray()] = value;
                }
            }
            get => indices.Length == 0 ? this : this[indices[0]][indices.Skip(1).ToArray()];
        }

        public AutoItVariantType this[AutoItVariantType index]
        {
            set => this[(long)index] = value;
            get => this[(long)index];
        }

        public AutoItVariantType this[params AutoItVariantType[] indices]
        {
            set => this[indices.Select(x => x.ToLong()).ToArray()] = value;
            get => this[indices.Select(x => x.ToLong()).ToArray()];
        }

        public bool IsNull => this == Null;

        public bool IsArray => _data.IsArray;

        public bool IsObject => _data.IsObject;

        public bool IsCOM => IsObject && UseGCHandledData<object, bool>(x => x is COM);

        public bool IsString => _data.IsString;

        public bool IsDefault => _data.IsDefault;

        public long BinaryLength => BinaryData.LongLength;

        public long Length => IsString ? ToString().Length : IsArray ? _data.Sizes[0] : 0;

        public long Dimensions => _data.Dimensions;

        #endregion
        #region STATIC PROPERTIES

        public static AutoItVariantType One { get; } = 1m;

        public static AutoItVariantType Zero { get; } = 0m;

        public static AutoItVariantType False { get; } = false;

        public static AutoItVariantType True { get; } = true;

        public static AutoItVariantType Default { get; } = new AutoItVariantType(AutoItVariantData.Default);

        public static AutoItVariantType Null { get; } = (void*)null;

        public static AutoItVariantType NullObject { get; } = NewGCHandledData(null);

        public static AutoItVariantType Empty { get; } = "";

        #endregion
        #region CONSTRUCTORS

        public AutoItVariantType(string s)
        {
            _data = new AutoItVariantData(s ?? "");
            s = s.ToLower();

            if (s.StartsWith("0x") && (s.Length % 2) == 0)
            {
                s = s.Substring(2);

                BinaryData = new byte[s.Length / 2];

                for (int i = 0; i <= BinaryData.Length; ++i)
                    try
                    {
                        if (i == BinaryData.Length)
                            return;

                        BinaryData[i] = byte.Parse(s.Substring(i * 2, 2), NumberStyles.HexNumber);
                    }
                    catch
                    {
                    }
            }

            BinaryData = new byte[s.Length];

            for (int i = 0; i < s.Length; ++i)
                BinaryData[i] = (byte)s[i];
        }

        private AutoItVariantType(AutoItVariantData data)
        {
            BinaryData = new byte[0];
            _data = data;
        }

        #endregion
        #region OVERRIDES + IMPLEMENTATIONS + CORE FUNCTIONS

        public AutoItVariantType Clone()
        {
            if (IsString)
                return ToString();
            else if (IsArray)
            {
                AutoItVariantType m = NewMatrix(_data.Sizes);

                for (int i = 0; i < _data.Sizes[0]; ++i)
                    m[i] = this[i].Clone();

                return m;
            }
            else
                return new AutoItVariantType(_data);
        }

        object ICloneable.Clone() => Clone();

        public override string ToString() => IsDefault ? "Default" : IsString ? (string)_data.VariantData : "";

        public override int GetHashCode() => _data.VariantData.Match(s => s.GetHashCode(), arr =>
        {
            int code = 0x3d10f062 << (arr.Length % 27);
            int shft = 0x31544a88;

            foreach (AutoItVariantType v in arr)
            {
                code ^= v.GetHashCode();
                shft = (int)((shft * 1103515245L + 12345L) & 0x7ffffffcL);

                if (((shft ^ code) % 3) == 1)
                {
                    code ^= ~shft;
                    ++shft;
                }
            }

            return code - (shft & 7);
        }, o => (o ?? new object()).GetHashCode());

        public string ToCOMString()
        {
            AutoItVariantType vt = this;

            return _data.VariantData.Match(s => s, _ => vt.ToArrayString(), o => o?.ToString() ?? "");
        }

        public string ToArrayString() => IsArray ? $"[{string.Join(", ", this.Select(x => x.ToArrayString()))}]" : ToCOMString();

        public string ToDebugString() => _data.VariantData.Match(s => s, a => $"{a.Length}: [| {string.Join(", ", a.Select(x => x.ToDebugString()))} |]", o => $"[{o?.GetType()?.Name ?? "<void*>"}]:  {o}");

        public override bool Equals(object obj) => obj is AutoItVariantType o && Equals(o);

        public bool Equals(AutoItVariantType other) => this == other;

        public int CompareTo(AutoItVariantType other) => this == other ? 0 : this > other ? -1 : 1;

        public IEnumerator<AutoItVariantType> GetEnumerator() =>
            _data.VariantData.Match<IEnumerable>(_ => null, a => a, o => o is IEnumerable i ? NewArray(i.Cast<object>().Select(FromCOMObject).ToArray()) : Empty) is IEnumerable iter ? iter.Cast<AutoItVariantType>().GetEnumerator() : throw new InvalidArrayAccessExcpetion();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
        #region CALLS + DELEGATES + CURRYING

        internal static AutoItVariantType CreateDelegate(MethodInfo m)
        {
            ParameterInfo[] pars = m.GetParameters();

            if (pars.Any(p => p.ParameterType != typeof(AutoItVariantType) && p.ParameterType != typeof(AutoItVariantType?)) || (m.ReturnType != typeof(AutoItVariantType)))
                throw new ArgumentException("A delegate cannot be created from this method type.", nameof(m));

            Assembly asm = typeof(AutoItVariantType).Assembly;
            int optcount = pars.Count(p => p.ParameterType != typeof(AutoItVariantType));
            Type t_cdel = asm.GetType($"{nameof(AutoItCoreLibrary)}.AutoItCurryingDelegate{pars.Length}Opt{optcount}");
            Type t_del = asm.GetType($"{nameof(AutoItCoreLibrary)}.AutoItDelegate{pars.Length}Opt{optcount}");

            Delegate d_om = m.CreateDelegate(t_del);
            object d_cd = Activator.CreateInstance(t_cdel, d_om);

            return NewGCHandledData(d_cd);
        }

        internal AutoItVariantType Call(params object[] argv)
        {
            if (argv.Length > AutoItCurryingDelegate.MAX_ARGC)
                throw new ArgumentOutOfRangeException(nameof(argv), $"Curryied invocation currently only allows a maximum of {AutoItCurryingDelegate.MAX_ARGC} parameters.");

            AutoItVariantType res = this;

            try
            {
                UseGCHandledData<AutoItCurryingDelegate>(del =>
                {
                    dynamic iacd = del;
                    Type type = del.GetType();
                    CurryableAttribute attr = type.GetCustomAttribute<CurryableAttribute>(true);

                    if ((argv.Length == 0) && (attr.RequiredArguments > 0))
                        return;

                    if (argv.Length >= attr.RequiredArguments)
                        argv = argv.Concat(Enumerable.Range(0, attr.TotalArguments - argv.Length).Select(_ => null as AutoItVariantType? as object)).ToArray();

                    int i = 0;
                    Type[] argtypes = argv.Select(_ => ++i > attr.RequiredArguments ? typeof(AutoItVariantType?) : typeof(AutoItVariantType)).ToArray();
                    MethodInfo call = type.GetMethod("Call", argtypes);
                    dynamic curry = call.Invoke(del, argv);

                    if (curry is AutoItCurryingDelegate)
                        res = AutoItVariantType.NewGCHandledData(curry);
                    else if (curry is AutoItVariantType v)
                        res = v;
                    else
                        res = AutoItVariantType.NewGCHandledData(curry);
                });
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException("This object instance is not a callable function");
            }

            return res;
        }

        public AutoItVariantType Call(params AutoItVariantType[] argv) => Call(argv.Select(x => x as object).ToArray());

        #endregion
        #region SERIALIZATION

        public byte[] Serialize()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                if (IsString)
                {
                    wr.Write((byte)0xff);
                    wr.Write(ToString());
                }
                else
                {
                    AutoItVariantType[] arr = this.ToArray();

                    wr.Write((byte)0x20);
                    wr.Write(arr.Length);

                    foreach (AutoItVariantType elem in arr)
                    {
                        byte[] ser = elem.Serialize();

                        wr.Write(ser.Length);
                        wr.Write(ser);
                    }
                }

                return ms.ToArray();
            }
        }

        public static AutoItVariantType Deserialize(byte[] arr)
        {
            if (arr is null)
                throw new ArgumentNullException(nameof(arr));

            using (MemoryStream ms = new MemoryStream(arr))
            using (BinaryReader rd = new BinaryReader(ms))
            {
                switch (rd.ReadByte())
                {
                    case 0xff:
                        return rd.ReadString();
                    case 0x20:
                        AutoItVariantType[] array = new AutoItVariantType[rd.ReadInt32()];

                        for (int i = 0; i < array.Length; ++i)
                        {
                            byte[] ser = new byte[rd.ReadInt32()];

                            rd.Read(ser, 0, ser.Length);
                            array[i] = Deserialize(ser);
                        }

                        return NewArray(array);
                    default:
                        throw new ArgumentException("Invalid data type descriptor.", nameof(arr));
                }
            }
        }

        public static AutoItVariantType Deserialize(byte[] arr, int offset) => Deserialize(arr, offset, (arr?.Length ?? 0) - offset);

        public static AutoItVariantType Deserialize(byte[] arr, int offset, int length) => Deserialize(arr?.Skip(offset)?.Take(length)?.ToArray());

        #endregion
        #region INSTANCE FUNCTIONS

        public AutoItVariantType ToLower() => ToString().ToLower();

        public AutoItVariantType ToUpper() => ToString().ToUpper();

        public AutoItVariantType OneBasedSubstring(AutoItVariantType start, AutoItVariantType count) => ToString().Substring(start.ToInt() - 1, count.ToInt());

        public byte Dereference() => *((byte*)ToVoidPtr());

        public void Dereference(byte value) => *((byte*)ToVoidPtr()) = value;

        public bool ToBool() => this;

        public decimal ToDecimal() => this;

        public byte ToByte() => (byte)ToLong();

        public short ToShort() => (short)ToLong();

        public int ToInt() => (int)ToLong();

        public long ToLong() => (long)this;

        public void* ToVoidPtr() => this;

        public IntPtr ToIntPtr() => this;

        public double ToDouble() => (double)this;

        public StringBuilder ToStringBuilder() => new StringBuilder(this);

        public AutoItVariantTypeReference MakeReference() => new AutoItVariantTypeReference(this);

        private void UseGCHandledData(Action<object> func) => _data.VariantData.Match(default, default, func);

        public COM GetCOM() => GetObject<COM>();

        public object GetObject() => GetObject<object>();

        public T GetObject<T>() => UseGCHandledData<object, T>(x => (T)x);

        public void UseGCHandledData<T>(Action<T> func) => UseGCHandledData(o => func((T)o));

        public void UseDisposeGCHandledData<T>(Action<T> func) where T : IDisposable => UseGCHandledData<T>(t =>
        {
            func?.Invoke(t);
            t.Dispose();
        });

        public void DisposeGCHandledData<T>() where T : IDisposable => UseGCHandledData<T>(t => t.Dispose());

        public U UseGCHandledData<T, U>(Func<T, U> func)
        {
            U res = default;

            UseGCHandledData(o => res = func((T)o));

            return res;
        }

        public AutoItVariantType InvokeCOM(string member, params object[] args) => IsCOM ? FromCOMObject(GetCOM().Invoke(member, args)) : NullObject;

        #endregion
        #region STATIC FUNCTIONS

        public static bool Equals(AutoItVariantType v1, AutoItVariantType v2) => Equals(v1, v2, false);
        public static bool Equals(AutoItVariantType v1, AutoItVariantType v2, bool ignorecase) => ignorecase ? string.Equals(v1, v2, StringComparison.InvariantCultureIgnoreCase) : v1 == v2;
        public static bool EqualsCaseInsensitive(AutoItVariantType v1, AutoItVariantType v2) => Equals(v1, v2, true);
        public static bool Unequals(AutoItVariantType v1, AutoItVariantType v2) => v1 != v2;
        public static bool Lower(AutoItVariantType v1, AutoItVariantType v2) => v1 < v2;
        public static bool LowerEquals(AutoItVariantType v1, AutoItVariantType v2) => v1 <= v2;
        public static bool Greater(AutoItVariantType v1, AutoItVariantType v2) => v1 > v2;
        public static bool GreaterEquals(AutoItVariantType v1, AutoItVariantType v2) => v1 >= v2;

        public static AutoItVariantType FromCOMObject(object com) => NewGCHandledData(new COM(com));
        public static AutoItVariantType CreateCOM(string name, string server = null)
        {
            Type t = Guid.TryParse(name.TrimStart('{').TrimEnd('}'), out Guid guid) ? Type.GetTypeFromCLSID(guid, server) : Type.GetTypeFromProgID(name, server);

            return NewGCHandledData(new COM(t));
        }
        public static AutoItVariantType NewDelegate<T>(T func) where T : Delegate => NewDelegate(func?.Method);
        public static AutoItVariantType NewDelegate(MethodInfo func) => func is null ? Null : CreateDelegate(func);
        public static AutoItVariantType NewGCHandledData(object gc) => gc is null ? Null : new AutoItVariantType(new AutoItVariantData(gc));
        public static AutoItVariantType NewArray(params AutoItVariantType[] vars)
        {
            vars = vars ?? new AutoItVariantType[0];

            if (vars.Select(v => v.Dimensions).Distinct().Count() > 1)
                throw new InvalidArrayAccessExcpetion();
            else
            {
                AutoItVariantType mat = NewMatrix(vars.Length);

                for (long i = 0; i < vars.Length; ++i)
                    mat[i] = vars[i];

                return mat;
            }
        }
        public static AutoItVariantType NewMatrix(params long[] sizes) => new AutoItVariantType(new AutoItVariantData(sizes));
        public static AutoItVariantType NewMatrix(params AutoItVariantType[] sizes) => NewMatrix(sizes.Select(x => x.ToLong()).ToArray());
        public static AutoItVariantType RedimMatrix(AutoItVariantType input, params long[] sizes)
        {
            if (input.IsString)
                throw new InvalidArrayAccessExcpetion();
            else
            {
                void CopyTo(AutoItVariantType from, ref AutoItVariantType to)
                {
                    if (from.IsString)
                        to = from.ToString();
                    else
                        for (long i = 0, l = Math.Min(from.Length, to.Length); i < l; ++i)
                        {
                            AutoItVariantType tf = to[i];

                            CopyTo(from[i], ref tf);

                            to[i] = tf;
                        }
                }
                AutoItVariantType @new = NewMatrix(sizes);

                CopyTo(input, ref @new);

                return @new;
            }
        }
        public static AutoItVariantType FromDouble(double v) => (decimal)v;
        public static AutoItVariantType FromString(string v) => v;
        public static AutoItVariantType FromBool(bool v) => v;
        public static AutoItVariantType FromDecimal(decimal v) => v;
        public static AutoItVariantType FromLong(long v) => v;
        public static AutoItVariantType FromInt(int v) => v;
        public static AutoItVariantType FromIntPtr(IntPtr v) => v;
        public static AutoItVariantType FromVoidPtr(void* v) => v;
        public static AutoItVariantType Not(AutoItVariantType v) => !(bool)v;
#pragma warning disable RSC1104 // THIS IS BY DESIGN, IN ORDER TO AVOID UNNESSARY EVALUATIONS (-> BOOLEAN SHORTCUT)
        public static AutoItVariantType Or(AutoItVariantType v1, AutoItVariantType v2) => v1 ? (AutoItVariantType)true : v2;
        public static AutoItVariantType And(AutoItVariantType v1, AutoItVariantType v2) => v1 ? (bool)v2 : false;
#pragma warning restore RSC1104
        public static AutoItVariantType Xor(AutoItVariantType v1, AutoItVariantType v2) => (bool)v1 ^ (bool)v2;
        public static AutoItVariantType Nor(AutoItVariantType v1, AutoItVariantType v2) => Not(Or(v1, v2));
        public static AutoItVariantType Nand(AutoItVariantType v1, AutoItVariantType v2) => Not(And(v1, v2));
        public static AutoItVariantType Nxor(AutoItVariantType v1, AutoItVariantType v2) => Not(Xor(v1, v2));
        public static AutoItVariantType BitwiseNot(AutoItVariantType v) => ~(long)v;
        public static AutoItVariantType BitwiseOr(AutoItVariantType v1, AutoItVariantType v2) => (long)v1 | (long)v2;
        public static AutoItVariantType BitwiseAnd(AutoItVariantType v1, AutoItVariantType v2) => (long)v1 & (long)v2;
        public static AutoItVariantType BitwiseXor(AutoItVariantType v1, AutoItVariantType v2) => (long)v1 ^ (long)v2;
        public static AutoItVariantType BitwiseNand(AutoItVariantType v1, AutoItVariantType v2) => BitwiseNot(BitwiseAnd(v1, v2));
        public static AutoItVariantType BitwiseNor(AutoItVariantType v1, AutoItVariantType v2) => BitwiseNot(BitwiseOr(v1, v2));
        public static AutoItVariantType BitwiseNxor(AutoItVariantType v1, AutoItVariantType v2) => BitwiseNot(BitwiseXor(v1, v2));
        public static AutoItVariantType BitwiseShr(AutoItVariantType v1, AutoItVariantType v2) => v2 < 0 ? BitwiseShl(v1, -v2) : (long)v1 >> ((int)Math.Min(v2, 64));
        public static AutoItVariantType BitwiseShl(AutoItVariantType v1, AutoItVariantType v2) => v2 < 0 ? BitwiseShr(v1, -v2) : (long)v1 << ((int)Math.Min(v2, 64));
        public static AutoItVariantType BitwiseRor(AutoItVariantType v1, AutoItVariantType v2) => BitwiseOr(BitwiseShr(v1, v2), BitwiseShl(v1, 64 - v2));
        public static AutoItVariantType BitwiseRol(AutoItVariantType v1, AutoItVariantType v2) => BitwiseOr(BitwiseShl(v1, v2), BitwiseShr(v1, 64 - v2));
        public static AutoItVariantType IntegerDivide(AutoItVariantType v1, AutoItVariantType v2) => AutoItFunctions.Floor(AutoItFunctions.Floor(v1) / AutoItFunctions.Floor(v2));
        public static AutoItVariantType Power(AutoItVariantType v1, AutoItVariantType v2) => v1 ^ v2;
        public static AutoItVariantType Concat(AutoItVariantType v1, AutoItVariantType v2) => v1 & v2;
        public static AutoItVariantType Concat(IEnumerable<AutoItVariantType> v) => v.Aggregate(Concat);

        #endregion
        #region OPERATORS

        public static implicit operator bool(AutoItVariantType v) => string.IsNullOrEmpty(v) ? false : bool.TryParse(v, out bool b) ? b : (long)v != 0;
        public static implicit operator AutoItVariantType(bool b) => b.ToString();
        public static implicit operator string(AutoItVariantType v) => v.ToString();
        public static implicit operator AutoItVariantType(string s) => new AutoItVariantType(s);
        public static implicit operator decimal(AutoItVariantType v) => v.IsDefault ? -1m : decimal.TryParse(v, out decimal d) ? d : (long)v;
        public static implicit operator AutoItVariantType(decimal d)
        {
            string s = d.ToString().TrimStart('+').TrimEnd('m');
            bool neg = s.StartsWith('-');
            int dot;

            if (neg)
                s = s.Substring(1);

            if (s.Contains('.'))
            {
                dot = s.IndexOf('.');
                s = s.Substring(0, dot).TrimStart('0') + '.' + s.Substring(dot + 1).TrimEnd('0');

                if (s.StartsWith('.'))
                    s = '0' + s;

                if (s.EndsWith('.'))
                    s += '0';
            }
            else
                s = s.TrimStart('0');

            if (s.Length == 0)
                s = "0";
            else if (neg)
                s = '-' + s;

            dot = s.IndexOf('.');

            if (dot > 0 && (s.Substring(dot + 1).Trim('0').Length == 0))
                s = s.Remove(dot);

            return s;
        }
        public static explicit operator long(AutoItVariantType v) => v.IsDefault ? -1L : long.TryParse(v, out long l) || long.TryParse(v, NumberStyles.HexNumber, null, out l) ? l : bool.TryParse(v, out bool b) && b ? 1L : 0L;
        public static implicit operator AutoItVariantType(long l) => l.ToString();
        public static implicit operator void* (AutoItVariantType v) => (void*)(long)v;
        public static implicit operator AutoItVariantType(void* l) => (long)l;
        public static implicit operator IntPtr(AutoItVariantType v) => (IntPtr)(void*)v;
        public static implicit operator AutoItVariantType(IntPtr p) => (void*)p;
        public static bool operator true(AutoItVariantType v) => v == true;
        public static bool operator false(AutoItVariantType v) => v == false;
        public static AutoItVariantType operator !(AutoItVariantType v) => Not(v);
        public static AutoItVariantType operator ~(AutoItVariantType v) => BitwiseNot(v);
        public static AutoItVariantType operator -(AutoItVariantType v) => -(long)v;
        public static AutoItVariantType operator +(AutoItVariantType v) => v;
        public static AutoItVariantType operator &(AutoItVariantType v1, AutoItVariantType v2) => v1.ToString() + v2.ToString();
        public static AutoItVariantType operator +(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 + (decimal)v2;
        public static AutoItVariantType operator -(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 - (decimal)v2;
        public static AutoItVariantType operator *(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 * (decimal)v2;
        public static AutoItVariantType operator /(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 / (decimal)v2;
        public static AutoItVariantType operator %(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 % (decimal)v2;
        public static AutoItVariantType operator ^(AutoItVariantType v1, AutoItVariantType v2) => (decimal)Math.Pow((double)(decimal)v1, (double)(decimal)v2);
        public static bool operator ==(AutoItVariantType v1, AutoItVariantType v2)
        {
            if (v1.IsString && v2.IsString)
                return v1.ToString() == v2.ToString();
            else if (v1.IsObject && v2.IsObject)
                return v1.GetObject() == v2.GetObject();
            else if (v1.IsArray && v2.IsArray)
                return v1.SequenceEqual(v2);
            else
                return false;
        }
        public static bool operator !=(AutoItVariantType v1, AutoItVariantType v2) => !(v1 == v2);
        public static bool operator <(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 < (decimal)v2;
        public static bool operator >(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 > (decimal)v2;
        public static bool operator <=(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 <= (decimal)v2;
        public static bool operator >=(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 >= (decimal)v2;

        #endregion
        #region INTERNAL DATATYPES

        private sealed class AutoItVariantData
        {
            public Union3<string, AutoItVariantType[], object> VariantData { get; }
            public bool IsString => VariantData.IsA;
            public bool IsArray => VariantData.IsB;
            public bool IsObject => VariantData.IsC;
            public long[] Sizes { get; }
            public bool IsDefault => (Sizes.LongLength == 1) && (Sizes[0] == -1);
            public long Dimensions => Sizes.Count(l => l >= 0);


            public static AutoItVariantData Default => new AutoItVariantData();


            public AutoItVariantData()
            {
                VariantData = "-1";
                Sizes = new long[] { -1 };
            }

            public AutoItVariantData(object o)
            {
                VariantData = o;
                Sizes = new long[0];
            }

            public AutoItVariantData(string s)
            {
                VariantData = s ?? "";
                Sizes = new long[0];
            }

            public AutoItVariantData(long[] sizes)
            {
                if (sizes is null || sizes.Length == 0)
                {
                    VariantData = "";
                    Sizes = new long[0];
                }
                else
                {
                    long tsz = sizes[0];
                    AutoItVariantType[] arr = new AutoItVariantType[tsz];

                    for (long i = 0; i < tsz; ++i)
                        arr[i] = NewMatrix(sizes.Skip(1).ToArray());

                    Sizes = sizes;
                    VariantData = arr;
                }
            }
        }

        #endregion
    }

#pragma warning disable RCS1194
    public sealed class InvalidArrayAccessExcpetion
        : Exception
    {
    }
#pragma warning restore RCS1194
}
