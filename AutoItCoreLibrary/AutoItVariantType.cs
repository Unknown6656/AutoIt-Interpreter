using System.Globalization;
using System;

namespace AutoItCoreLibrary
{
    public unsafe struct AutoItVariantType
    {
        private readonly string _sdata;
        private readonly byte[] _bin;

        public char this[int index] => _sdata?[index] ?? '\0';
        public int BinaryLength => _bin.Length;
        public int Length => ToString().Length;
        public byte[] BinaryData => _bin;

        public static AutoItVariantType False { get; } = false;
        public static AutoItVariantType True { get; } = true;
        public static AutoItVariantType Default { get; } = "";
        public static AutoItVariantType Null { get; } = "";


        public AutoItVariantType(string s)
        {
            _sdata = s ?? "";
            s = _sdata.ToLower();

            if (s.StartsWith("0x") && (s.Length % 2) == 0)
            {
                s = s.Substring(2);

                _bin = new byte[s.Length / 2];

                for (int i = 0; i <= _bin.Length; ++i)
                    try
                    {
                        if (i == _bin.Length)
                            return;

                        _bin[i] = byte.Parse(s.Substring(i * 2, 2), NumberStyles.HexNumber);
                    }
                    catch
                    {
                    }
            }

            _bin = new byte[s.Length];

            for (int i = 0; i < s.Length; ++i)
                _bin[i] = (byte)s[i];
        }

        public override string ToString() => _sdata ?? "";
        public override int GetHashCode() => _sdata.GetHashCode();
        public override bool Equals(object obj) => obj is AutoItVariantType o ? this == o : false;
        public AutoItVariantType ToLower() => ToString().ToLower();
        public AutoItVariantType ToUpper() => ToString().ToUpper();
        public bool ToBool() => this;
        public decimal ToDecimal() => this;
        public long ToLong() => (long)this;
        public void* ToVoidPtr() => this;
        public IntPtr ToIntPtr() => this;

        public static AutoItVariantType FromString(string v) => v;
        public static AutoItVariantType FromBool(bool v) => v;
        public static AutoItVariantType FromDecimal(decimal v) => v;
        public static AutoItVariantType FromLong(long v) => v;
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
        public static bool Equals(AutoItVariantType v1, AutoItVariantType v2) => Equals(v1, v2, true);
        public static bool Equals(AutoItVariantType v1, AutoItVariantType v2, bool ignorecase) => ignorecase ? string.Equals(v1, v2, StringComparison.InvariantCultureIgnoreCase) : v1 == v2;

        public static implicit operator bool(AutoItVariantType v) => string.IsNullOrEmpty(v) ? false : bool.TryParse(v, out bool b) ? true : b;
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
        public static bool operator !=(AutoItVariantType v1, AutoItVariantType v2) => !(v1 == v2);
        public static bool operator ==(AutoItVariantType v1, AutoItVariantType v2) => v1._sdata == v2._sdata;
        public static bool operator <(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 < (decimal)v2;
        public static bool operator >(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 > (decimal)v2;
        public static bool operator <=(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 <= (decimal)v2;
        public static bool operator >=(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 >= (decimal)v2;
    }
}
