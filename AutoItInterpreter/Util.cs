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
    using static ExpressionAST;


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

        public static bool ArePathsEqual(string path1, string path2) => string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.InvariantCultureIgnoreCase);

        public static bool ArePathsEqual(FileInfo nfo1, FileInfo nfo2) => ArePathsEqual(nfo1.FullName, nfo2.FullName);
    }

    internal static class DebugPrintUtil
    {
        public static void DisplayPartialAST(InterpreterState state, InterpreterSettings settings)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(new string('=', 200));

            string[] glob = { PreInterpreterState.GLOBAL_FUNC_NAME };
            bool allman = settings.IndentationStyle == IndentationStyle.AllmanStyle;
            int linecnt = 0;

            foreach (string fn in state.ASTFunctions.Keys.Except(glob).OrderByDescending(fn => fn).Concat(glob).Reverse())
            {
                AST_FUNCTION func = state.ASTFunctions[fn];

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"       |  ------- {state.GetFunctionSignature(fn)} -------");

                _print(func, 1);
            }

            void _print(AST_STATEMENT e, int indent)
            {
                string tstr(EXPRESSION ex) => ex is null ? "«« error »»" : ex.Print();
                void println(string s, int i = -1)
                {
                    ++linecnt;

                    bool comment = s.StartsWith("//");

                    Console.ForegroundColor = comment ? ConsoleColor.DarkGreen : ConsoleColor.Yellow;
                    Console.Write($"{linecnt,6} |  ");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(string.Concat(Enumerable.Repeat("|   ", (i < 1 ? indent : i) - 1)));
                    Console.ForegroundColor = comment ? ConsoleColor.DarkGreen : ConsoleColor.White;
                    Console.WriteLine(s);
                }
                void print(AST_STATEMENT s) => _print(s, indent + 1);
                void printblock(AST_STATEMENT[] xs, string p = "", string s = "")
                {
                    xs = xs ?? new AST_STATEMENT[0];

                    if (xs.Length > 1 || (p + s).Length == 0 || !allman)
                    {
                        if (allman)
                        {
                            if (p.Length > 0)
                                println(p);

                            println("{");
                        }
                        else
                            println(p.Length > 0 ? $"{p} {{" : "{");

                        foreach (AST_STATEMENT x in xs)
                            print(x);

                        if (allman)
                        {
                            println("}");

                            if (s.Length > 0)
                                println(s);
                        }
                        else
                            println(s.Length > 0 ? $"}} {s}" : "}");
                    }
                    else
                    {
                        if (p.Length > 0)
                            println(p);

                        if (xs.Length > 0)
                            print(xs[0]);
                        else
                            println(";", indent + 1);

                        if (s.Length > 0)
                            println(s);
                    }
                }

                switch (e)
                {
                    case AST_IF_STATEMENT s:
                        printblock(s.If.Statements, $"if ({tstr(s.If.Condition)})");

                        foreach (AST_CONDITIONAL_BLOCK elif in s.ElseIf ?? new AST_CONDITIONAL_BLOCK[0])
                            printblock(elif.Statements, $"else if ({tstr(elif.Condition)})");

                        if (s.OptionalElse is AST_STATEMENT[] b)
                            printblock(b, "else");

                        return;
                    case AST_WHILE_STATEMENT s:
                        printblock(s.WhileBlock.Statements, $"while ({tstr(s.WhileBlock.Condition)})");

                        return;
                    case AST_SCOPE s:
                        println("{");

                        foreach (AST_LOCAL_VARIABLE ls in s.ExplicitLocalsVariables)
                            if (ls.InitExpression is null)
                                println($"{ls.Variable};", indent + 1);
                            else
                                println($"{ls.Variable} = {tstr(ls.InitExpression)};", indent + 1);

                        foreach (AST_STATEMENT ls in s.Statements ?? new AST_STATEMENT[0])
                            print(ls);

                        println("}");

                        return;
                    case AST_BREAK_STATEMENT s when s.Level == 1:
                        println("break;");

                        return;
                    case AST_LABEL s:
                        println(s.Name + ':', 1);

                        return;
                    case AST_GOTO_STATEMENT s:
                        println($"goto {s.Label.Name};");

                        return;
                    case AST_ASSIGNMENT_EXPRESSION_STATEMENT s:
                        println(tstr(EXPRESSION.NewAssignmentExpression(s.Expression)) + ';');

                        return;
                    case AST_EXPRESSION_STATEMENT s:
                        println(tstr(s.Expression) + ';');

                        return;


                        // TODO 


                    default:
                        println($"// TODO: {e}");

                        return;
                }
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void DisplayCodeAndErrors(FileInfo root, InterpreterState state)
        {
            Console.WriteLine(new string('=', 200));

            foreach (FileInfo path in state.Errors.Select(e => e.ErrorContext.FilePath).Concat(new[] { root }).Distinct(new PathEqualityComparer()))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"     _ |  {path.FullName}");
                Console.ForegroundColor = ConsoleColor.DarkGray;

                int cnt = 1;

                InterpreterError[] localerrors = state.Errors.Where(e => Util.ArePathsEqual(e.ErrorContext.FilePath, path)).ToArray();

                foreach (string line in File.ReadAllText(path.FullName).Replace("\r\n", "\n").Split('\n'))
                {
                    InterpreterError[] errs = localerrors.Where(e => e.ErrorContext.StartLine == cnt).ToArray();

                    Console.Write($"{cnt,6} |  ");

                    if (errs.Length > 0)
                    {
                        string pad = $"       |  {(line.Trim().Length > 0 ? line.Remove(line.Length - line.TrimStart().Length) : "")}";
                        bool crit = errs.Any(e => e.IsFatal);

                        Console.ForegroundColor = crit ? ConsoleColor.Red : ConsoleColor.Yellow;
                        Console.WriteLine(line);
                        Console.ForegroundColor = crit ? ConsoleColor.DarkRed : ConsoleColor.DarkYellow;
                        Console.WriteLine(pad + new string('^', Math.Max(1, line.Trim().Length)));

                        foreach (IGrouping<bool, InterpreterError> g in errs.GroupBy(e => e.IsFatal))
                        {
                            Console.ForegroundColor = g.Key ? ConsoleColor.DarkRed : ConsoleColor.DarkYellow;

                            foreach (InterpreterError e in g)
                                Console.WriteLine(pad + e.ErrorMessage);
                        }

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                    }
                    else
                        Console.WriteLine(line);

                    ++cnt;
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        public static void DisplayPreState(PreInterpreterState state)
        {
            Console.WriteLine(new string('=', 200));

            foreach (string fn in state.Functions.Keys)
            {
                FunctionScope func = state.Functions[fn];

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"---------------------------------------- {state.GetFunctionSignature(fn)} ----------------------------------------");
                Console.ForegroundColor = ConsoleColor.Gray;

                foreach (var l in func.Lines)
                {
                    Console.CursorLeft = 10;
                    Console.Write(l.Context);
                    Console.CursorLeft = 40;
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
        public static AutoItVariantType operator ^(AutoItVariantType v1, AutoItVariantType v2) => (decimal)Math.Pow((double)(decimal)v1, (double)(decimal)v2);
        public static bool operator !=(AutoItVariantType v1, AutoItVariantType v2) => !(v1 == v2);
        public static bool operator ==(AutoItVariantType v1, AutoItVariantType v2) => v1._sdata == v2._sdata;
        public static bool operator <(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 < (decimal)v2;
        public static bool operator >(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 > (decimal)v2;
        public static bool operator <=(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 <= (decimal)v2;
        public static bool operator >=(AutoItVariantType v1, AutoItVariantType v2) => (decimal)v1 >= (decimal)v2;
    }
}
