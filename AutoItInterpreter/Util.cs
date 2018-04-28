using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System;

using Newtonsoft.Json.Linq;

using AutoItInterpreter.PartialAST;
using AutoItExpressionParser;

namespace AutoItInterpreter
{
    using static InterpreterConstants;
    using static ExpressionAST;


    public static class Util
    {
        public static string[] SplitIntoLines(this string s) => (s ?? "").Replace("\r\n", "\n").Split('\n');

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

        public static bool ArePathsEqual(string path1, string path2) => string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.InvariantCultureIgnoreCase);

        public static bool ArePathsEqual(FileInfo nfo1, FileInfo nfo2) => ArePathsEqual(nfo1.FullName, nfo2.FullName);
    }

    internal static class DebugPrintUtil
    {
        public static void DisplayPartialAST(InterpreterState state, InterpreterSettings settings)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(new string('=', 200));

            int lastpadl = 0;
            int linecnt = 0;

            foreach (string line in Generator.Generate(state, settings).SplitIntoLines())
            {
                ++linecnt;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{linecnt,6} |  ");
                Console.ForegroundColor = ConsoleColor.DarkGray;

                if (line.Length == 0)
                {
                    for (int i = 0; i < lastpadl; ++i)
                        Console.Write("|   ");

                    Console.WriteLine();
                }
                else
                {
                    string tline = line.TrimStart();
                    string pad = line.Remove(line.Length - tline.Length);
                    int left = Console.CursorLeft;

                    Console.Write(pad.Replace("\t", "    "));

                    int pleft = Console.CursorLeft;

                    Console.CursorLeft = left;

                    lastpadl = 0;

                    do
                    {
                        Console.Write("|   ");

                        ++lastpadl;
                    }
                    while (Console.CursorLeft < pleft);

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(tline);
                }
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void DisplayCodeAndErrors(FileInfo root, InterpreterState state)
        {
            Console.WriteLine(new string('=', 200));

            foreach (FileInfo path in state.Errors.Select(e => e.ErrorContext.FilePath).Concat(new[] { root }).Distinct(new PathEqualityComparer()))
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"     _ |> {path.FullName}");

                InterpreterError[] localerrors = state.Errors.Where(e => Util.ArePathsEqual(e.ErrorContext.FilePath, path)).ToArray();
                string[] lines = (File.ReadAllText(path.FullName) + "\n").SplitIntoLines();
                List<int> vlines = new List<int>();

                const int DIST = 3;
                int lastlnr = -1;

                for (int lcnt = 1; lcnt <= lines.Length; ++lcnt)
                    if (localerrors.Any(err =>
                    {
                        int start = err.ErrorContext.StartLine;
                        int end = err.ErrorContext.EndLine ?? err.ErrorContext.StartLine;

                        return (lcnt >= start - DIST) && (lcnt <= end + DIST);
                    }))
                        vlines.Add(lcnt - 1);

                foreach (int linenr in vlines)
                    if (linenr >= lastlnr)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;

                        if ((linenr > lastlnr + 1) && (linenr > 1))
                            Console.WriteLine("       :");

                        int cnt = linenr + 1;
                        (InterpreterError err, int dist)[] errs = (from err in localerrors
                                                                   let start = err.ErrorContext.StartLine
                                                                   let end = err.ErrorContext.EndLine ?? start
                                                                   where (cnt >= start) && (cnt <= end)
                                                                   select (err, end - start + 1)).ToArray();

                        if (errs.Length == 0)
                        {
                            Console.WriteLine($"{cnt,6} |  {lines[linenr]}");

                            lastlnr = linenr;
                        }
                        else
                        {
                            ErrorType level = errs.Select(e => e.err.Type).Min();
                            int dist = errs.Select(e => e.dist).Max();
                            Console.ForegroundColor = level == ErrorType.Fatal ? ConsoleColor.Red
                                                    : level == ErrorType.Warning ? ConsoleColor.Yellow : ConsoleColor.Cyan;

                            for (int i = 0; i < dist; ++i)
                                Console.WriteLine($"{linenr + i + 1,6} |  {lines[linenr + i]}");

                            var alines = dist <= 1 ? new[] { lines[linenr] } : Enumerable.Range(linenr, dist + 1).Select(l => lines[l]);
                            var apads = alines.Select(al => al.Trim().Length > 0 ? al.Remove(al.Length - al.TrimStart().Length) : "");
                            string mpad = apads.OrderBy(al => al.Length).First();

                            Console.ForegroundColor = fgclr(level);
                            Console.WriteLine($"       |  {mpad}{new string('^', Math.Max(1, alines.Max(al => al.Length) - mpad.Length))}");

                            foreach (IGrouping<ErrorType, InterpreterError> g in errs.GroupBy(e => e.err.Type, e => e.err))
                            {
                                Console.ForegroundColor = fgclr(g.Key);

                                foreach (InterpreterError e in g)
                                    Console.WriteLine($"       |  {mpad}{e.ErrorMessage}");
                            }

                            lastlnr = linenr + dist;
                        }

                        ConsoleColor fgclr(ErrorType l) => l == ErrorType.Fatal ? ConsoleColor.DarkRed : l == ErrorType.Warning ? ConsoleColor.DarkYellow : ConsoleColor.DarkCyan;
                    }
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void DisplayPreState(PreInterpreterState state)
        {
            Console.WriteLine(new string('=', 200));

            foreach (string fn in state.Functions.Keys)
            {
                FunctionScope func = state.Functions[fn];

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"---------------------------------------- {state.GetFunctionSignature(fn)} ----------------------------------------");
                Console.ForegroundColor = ConsoleColor.Gray;

                foreach (var l in func.Lines)
                {
                    Console.CursorLeft = 10;
                    Console.Write(l.Context);
                    Console.CursorLeft = 50;
                    Console.WriteLine(l.Line);
                }
            }
        }
    }

    public sealed class PathEqualityComparer
        : IEqualityComparer<FileInfo>
    {
        public bool Equals(FileInfo x, FileInfo y) => Util.ArePathsEqual(x, y);

        public int GetHashCode(FileInfo obj) => obj is null ? 0 : Path.GetFullPath(obj.FullName).GetHashCode();
    }





    // only used inside the interpreted script
    internal unsafe struct AutoItVariantType
    {
        private readonly string _sdata;


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
