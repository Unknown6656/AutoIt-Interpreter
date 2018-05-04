using System.Collections.Generic;
using System.Globalization;
using System.Collections;
using System.Linq;
using System;
using System.Runtime.InteropServices;

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
                if (IsString || (index < 0) || (index >= Length))
                    throw new InvalidArrayAccessExcpetion();
                else
                    _data.VariantData[index] = value.Clone();
            }
            get => IsArray && (index >= 0) && (index < Length) ? _data.VariantData[index] : throw new InvalidArrayAccessExcpetion();
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

        public bool IsArray => !IsString;

        public bool IsString => _data.IsString;

        public long BinaryLength => BinaryData.LongLength;

        public long Length => IsString ? ToString().Length : _data.Sizes[0];

        public long Dimensions => _data.Dimensions;

        #endregion
        #region STATIC PROPERTIES

        public static AutoItVariantType False { get; } = false;

        public static AutoItVariantType True { get; } = true;

        public static AutoItVariantType Default { get; } = "";

        public static AutoItVariantType Null { get; } = (void*)null;

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
            else
            {
                AutoItVariantType m = NewMatrix(_data.Sizes);

                for (int i = 0; i < _data.Sizes[0]; ++i)
                    m[i] = this[i].Clone();

                return m;
            }
        }

        object ICloneable.Clone() => Clone();

        public override string ToString() => IsString ? _data.StringData : "";

        public override int GetHashCode()
        {
            if (IsString)
                return ToString().GetHashCode();
            else
            {
                int code = 0x3d10f062 << (int)(Length % 27);

                foreach (AutoItVariantType v in this)
                    code ^= v.GetHashCode();

                return code;
            }
        }

        public string ToDebugString()
        {
            if (IsString)
                return _data.StringData;
            else
                return $"[ {string.Join(", ", _data.VariantData.Select(x => x.ToDebugString()))} ]";
        }

        public override bool Equals(object obj) => obj is AutoItVariantType o && Equals(o);

        public bool Equals(AutoItVariantType other) => this == other;

        public int CompareTo(AutoItVariantType other) => this == other ? 0 : this > other ? -1 : 1;

        public IEnumerator<AutoItVariantType> GetEnumerator()
        {
            if (IsArray)
                return _data.VariantData.Cast<AutoItVariantType>().GetEnumerator();
            else
                throw new InvalidArrayAccessExcpetion();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
        #region INSTANCE FUNCTIONS

        public AutoItVariantType ToLower() => ToString().ToLower();

        public AutoItVariantType ToUpper() => ToString().ToUpper();

        public bool ToBool() => this;

        public decimal ToDecimal() => this;

        public int ToInt() => (int)ToLong();

        public long ToLong() => (long)this;

        public void* ToVoidPtr() => this;

        public IntPtr ToIntPtr() => this;

        public double ToDouble() => (double)this;

        public GCHandle ToGCHandle() => this;

        public void UseGCHandledData(Action<object> func)
        {
            if (func != null)
            {
                GCHandle gch = this;

                func(gch.Target);
            }
        }

        public void UseDisposeGCHandledData(Action<object> func)
        {
            UseGCHandledData(func);
            ToGCHandle().Free();
        }

        public void UseGCHandledData<T>(Action<T> func) where T : class => UseGCHandledData(o => func(o as T));

        public void UseDisposeGCHandledData<T>(Action<T> func) where T : class => UseDisposeGCHandledData(o => func(o as T));

        #endregion
        #region STATIC FUNCTIONS

        public static bool Equals(AutoItVariantType v1, AutoItVariantType v2) => Equals(v1, v2, true);
        public static bool Equals(AutoItVariantType v1, AutoItVariantType v2, bool ignorecase) => ignorecase ? string.Equals(v1, v2, StringComparison.InvariantCultureIgnoreCase) : v1 == v2;

        public static AutoItVariantType NewGCHandledData(object gc) => gc is null ? (AutoItVariantType)(void*)null : (AutoItVariantType)GCHandle.Alloc(gc);
        public static AutoItVariantType NewArray(params AutoItVariantType[] vars)
        {
            vars = vars ?? new AutoItVariantType[0];

            if (vars.Select(v => v.Dimensions).Distinct().Count() != 1)
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
        public static AutoItVariantType FromDouble(double v) => (decimal)v;
        public static AutoItVariantType FromString(string v) => v;
        public static AutoItVariantType FromBool(bool v) => v;
        public static AutoItVariantType FromDecimal(decimal v) => v;
        public static AutoItVariantType FromLong(long v) => v;
        public static AutoItVariantType FromInt(int v) => v;
        public static AutoItVariantType FromIntPtr(IntPtr v) => v;
        public static AutoItVariantType FromVoidPtr(void* v) => v;
        public static AutoItVariantType Not(AutoItVariantType v) => !(bool)v;
        public static AutoItVariantType Or(AutoItVariantType v1, AutoItVariantType v2) => v1 || v2;
        public static AutoItVariantType And(AutoItVariantType v1, AutoItVariantType v2) => (bool)v1 && (bool)v2;
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
        public static AutoItVariantType Power(AutoItVariantType v1, AutoItVariantType v2) => v1 ^ v2;
        public static AutoItVariantType Concat(AutoItVariantType v1, AutoItVariantType v2) => v1 & v2;
        public static AutoItVariantType Concat(IEnumerable<AutoItVariantType> v) => v.Aggregate(Concat);

        #endregion
        #region OPERATORS

        public static implicit operator bool(AutoItVariantType v) => string.IsNullOrEmpty(v) ? false : bool.TryParse(v, out bool b) ? b : (long)v != 0;
        public static implicit operator AutoItVariantType(bool b) => b.ToString();
        public static implicit operator string(AutoItVariantType v) => v.ToString();
        public static implicit operator AutoItVariantType(string s) => new AutoItVariantType(s);
        public static implicit operator decimal(AutoItVariantType v) => decimal.TryParse(v, out decimal d) ? d : (long)v;
        public static implicit operator AutoItVariantType(decimal d) => d.ToString();
        public static explicit operator long(AutoItVariantType v) => long.TryParse(v, out long l) || long.TryParse(v, NumberStyles.HexNumber, null, out l) ? l : 0L;
        public static implicit operator AutoItVariantType(long l) => l.ToString();
        public static implicit operator void* (AutoItVariantType v) => (void*)(long)v;
        public static implicit operator AutoItVariantType(void* l) => (long)l;
        public static implicit operator IntPtr(AutoItVariantType v) => (IntPtr)(void*)v;
        public static implicit operator AutoItVariantType(IntPtr p) => (void*)p;
        public static implicit operator GCHandle(AutoItVariantType v) => GCHandle.FromIntPtr(v);
        public static implicit operator AutoItVariantType(GCHandle h) => (IntPtr)h;
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
        public static bool operator ==(AutoItVariantType v1, AutoItVariantType v2) => v1.ToDebugString() == v2.ToDebugString();
        public static bool operator !=(AutoItVariantType v1, AutoItVariantType v2) => !(v1 == v2);
        public static bool operator <(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 < (decimal)v2;
        public static bool operator >(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 > (decimal)v2;
        public static bool operator <=(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 <= (decimal)v2;
        public static bool operator >=(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 >= (decimal)v2;

        #endregion
        #region INTERNAL DATATYPES

        private sealed class AutoItVariantData
        {
            public bool IsString { get; }
            public string StringData { get; }
            public long[] Sizes { get; }
            public long Dimensions => Sizes.LongLength;
            public AutoItVariantType[] VariantData { get; }


            public AutoItVariantData(string s)
            {
                IsString = true;
                StringData = s ?? "";
                Sizes = new long[0];
                VariantData = new AutoItVariantType[0];
            }

            public AutoItVariantData(long[] sizes)
            {
                StringData = "";

                if (sizes is null || sizes.Length == 0)
                {
                    IsString = true;
                    Sizes = new long[0];
                }
                else
                {
                    long tsz = sizes[0];

                    Sizes = sizes;
                    IsString = false;
                    VariantData = new AutoItVariantType[tsz];

                    for (long i = 0; i < tsz; ++i)
                        VariantData[i] = AutoItVariantType.NewMatrix(sizes.Skip(1).ToArray());
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
