using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System;

using Newtonsoft.Json.Linq;

namespace CSAutoItInterpreter
{
    internal static class Util
    {
        public static bool Match(this string s, string p, out Match m, RegexOptions o = RegexOptions.IgnoreCase | RegexOptions.Compiled) => (m = Regex.Match(s, p, o)).Success;

        public static bool Match(this string s, params (string, Action<Match>)[] p)
        {
            foreach ((string pattern, Action<Match> f) in p ?? new(string, Action<Match>)[0])
                if (s.Match(pattern, out Match m))
                {
                    f(m);

                    return true;
                }

            return false;
        }

        public static bool Match(this string s, Dictionary<string, Action<Match>> p)
        {
            foreach (string pattern in (p ?? new Dictionary<string, Action<Match>>()).Keys)
                if (s.Match(pattern, out Match m))
                {
                    p[pattern](m);

                    return true;
                }

            return false;
        }

        public static string Get(this Match m, string g) => m.Groups[g]?.ToString() ?? "";

        public static U Switch<T, U>(this T t, Dictionary<T, Func<U>> d, Func<U> n) => d.Switch(t, n);

        public static void Switch<T>(this T t, Dictionary<T, Action> d, Action n) => d.Switch(t, n);

        public static U Switch<T, U>(this Dictionary<T, Func<U>> d, T t, Func<U> n) => d.ContainsKey(t) ? d[t]() : n();

        public static void Switch<T>(this Dictionary<T, Action> d, T t, Action n)
        {
            if (d.ContainsKey(t))
                d[t]();
            else
                n();
        }

        public static bool IsValidJson(this string str)
        {
            if (str is string s)
                try
                {
                    s = s.Trim();

                    if ((s.StartsWith("{") && s.EndsWith("}")) || (s.StartsWith("[") && s.EndsWith("]")))
                        return JToken.Parse(s) is JToken _;
                }
                catch
                {
                }

            return false;
        }

        public static string Format(this string s, params object[] args) => string.Format(s, args);
    }


    // only used inside the interpreted script
    internal unsafe struct AutoItVariantType
    {
        private readonly string _sdata;


        public AutoItVariantType(string s) => _sdata = s ?? "";

        public override string ToString() => _sdata ?? "";

        public static AutoItVariantType Not(AutoItVariantType v) => !v;
        public static AutoItVariantType Or(AutoItVariantType v1, AutoItVariantType v2) => v1 || v2;
        public static AutoItVariantType And(AutoItVariantType v1, AutoItVariantType v2) => (bool)v1 && (bool)v2;

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

        public static AutoItVariantType operator &(AutoItVariantType v1, AutoItVariantType v2) => v1.ToString() + v2.ToString();
        public static AutoItVariantType operator +(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 + (decimal)v2;
        public static AutoItVariantType operator -(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 - (decimal)v2;
        public static AutoItVariantType operator *(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 * (decimal)v2;
        public static AutoItVariantType operator /(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 / (decimal)v2;
        public static AutoItVariantType operator ^(AutoItVariantType v1, AutoItVariantType v2) => (decimal)Math.Pow((double)(decimal)v1, (double)(decimal)v2);
    }
}
