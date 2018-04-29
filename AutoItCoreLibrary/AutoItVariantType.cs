using System.Globalization;
using System;

namespace AutoItCoreLibrary
{
    public unsafe struct AutoItVariantType
    {
        private readonly string _sdata;

        public int InternalLength => ToString().Length;

        public static AutoItVariantType False { get; } = false;
        public static AutoItVariantType True { get; } = true;
        public static AutoItVariantType Default { get; } = "";
        public static AutoItVariantType Null { get; } = "";


        public AutoItVariantType(string s) => _sdata = s ?? "";

        public override string ToString() => _sdata ?? "";
        public override int GetHashCode() => _sdata.GetHashCode();
        public override bool Equals(object obj) => obj is AutoItVariantType o ? this == o : false;

        public static AutoItVariantType Not(AutoItVariantType v) => !(bool)v;
        public static AutoItVariantType Or(AutoItVariantType v1, AutoItVariantType v2) => v1 || v2;
        public static AutoItVariantType And(AutoItVariantType v1, AutoItVariantType v2) => (bool)v1 && (bool)v2;
        public static AutoItVariantType Xor(AutoItVariantType v1, AutoItVariantType v2) => (bool)v1 ^ (bool)v2;
        public static AutoItVariantType Nor(AutoItVariantType v1, AutoItVariantType v2) => Not(Or(v1, v2));
        public static AutoItVariantType Nand(AutoItVariantType v1, AutoItVariantType v2) => Not(And(v1, v2));
        public static AutoItVariantType Nxor(AutoItVariantType v1, AutoItVariantType v2) => Not(Xor(v1, v2));

        public static AutoItVariantType BitwiseNot(AutoItVariantType v) => ~(long)v;
        public static AutoItVariantType BitwiseOr(AutoItVariantType v1, AutoItVariantType v2) => (long)v1 | v2;
        public static AutoItVariantType BitwiseAnd(AutoItVariantType v1, AutoItVariantType v2) => (long)v1 & (long)v2;
        public static AutoItVariantType BitwiseXor(AutoItVariantType v1, AutoItVariantType v2) => (long)v1 ^ (long)v2;
        public static AutoItVariantType BitwiseNand(AutoItVariantType v1, AutoItVariantType v2) => BitwiseNot(BitwiseAnd(v1, v2));
        public static AutoItVariantType BitwiseNor(AutoItVariantType v1, AutoItVariantType v2) => BitwiseNot(BitwiseOr(v1, v2));
        public static AutoItVariantType BitwiseNxor(AutoItVariantType v1, AutoItVariantType v2) => BitwiseNot(BitwiseXor(v1, v2));
        public static AutoItVariantType BitwiseShr(AutoItVariantType v1, AutoItVariantType v2) => v1 >> ((int)(v2 % 64));
        public static AutoItVariantType BitwiseShl(AutoItVariantType v1, AutoItVariantType v2) => v1 << ((int)(v2 % 64));
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
        public static implicit operator long(AutoItVariantType v) => long.TryParse(v, out long l) || long.TryParse(v, NumberStyles.HexNumber, null, out l) ? l : 0L;
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
