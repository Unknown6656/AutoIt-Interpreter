using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

using Newtonsoft.Json.Linq;

namespace AutoItInterpreter
{
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
        public static void PrintSeperator(string title, int width = 200)
        {
            Console.ForegroundColor = ConsoleColor.Gray;

            if (title is null)
                Console.WriteLine(new string('=', 200));
            else
            {
                title = title.Trim();

                int w = (width - title.Length - 1) / 2;

                Console.WriteLine($"{new string('=', w)} {title} {new string('=', w)}");
            }
        }

        public static void DisplayGeneratedCode(string code)
        {
            PrintSeperator("GENERATED CODE");

            int lastpadl = 0;
            int linecnt = 0;

            foreach (string line in code.SplitIntoLines())
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

                    Console.Write(pad);

                    int pleft = Console.CursorLeft;

                    Console.CursorLeft = left;

                    lastpadl = 0;

                    while (Console.CursorLeft < pleft)
                    {
                        Console.Write("|   ");

                        ++lastpadl;
                    }

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(tline);
                }
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void DisplayCodeAndErrors(InterpreterState state)
        {
            PrintSeperator("ERRORS, WARNINGS AND NOTES");

            foreach (FileInfo path in state.Errors.Select(e => e.ErrorContext.FilePath).Concat(new[] { state.RootDocument }).Distinct(new PathEqualityComparer()))
            {
                if (path is null)
                    continue;

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"   ___ |> {path.FullName}");

                InterpreterError[] localerrors = state.Errors.Where(e => e.ErrorContext.FilePath is FileInfo fi && Util.ArePathsEqual(fi, path)).ToArray();
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
            PrintSeperator("PRE-INTERPRETER STATE");

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

        public static void DisplayErrors(InterpreterState state, InterpreterOptions options)
        {
            PrintSeperator("ERROR LIST");

            string root = state.RootDocument.FullName;

            Console.WriteLine($"{state.Errors.Length} Errors, warnings and notes:\n");

            foreach (IGrouping<ErrorType, InterpreterError> g in state.Errors.GroupBy(err => err.Type).OrderBy(g => g.Key))
            {
                // Console.ForegroundColor = g.Key == ErrorType.Fatal ? ConsoleColor.DarkRed : g.Key == ErrorType.Warning ? ConsoleColor.DarkYellow : ConsoleColor.Blue;

                foreach (InterpreterError err in g)
                    if (options.UseMSBuildErrorOutput)
                    {
                        string tstr = err.Type == ErrorType.Fatal ? "error" : err.Type == ErrorType.Warning ? "warning" : "information";

                        Console.WriteLine($"{err.ErrorContext.FilePath?.FullName ?? "<unknown>"}({err.ErrorContext.StartLine},0): {tstr} AU{err.ErrorNumber}: {err.ErrorMessage} [{root}]");
                    }
                    else
                        Console.WriteLine($"[{(err.Type == ErrorType.Fatal ? "ERR." : err.Type == ErrorType.Warning ? "WARN" : "NOTE")}]  {err}");
            }
        }
    }

    public sealed class PathEqualityComparer
        : IEqualityComparer<FileInfo>
    {
        public bool Equals(FileInfo x, FileInfo y) => Util.ArePathsEqual(x, y);

        public int GetHashCode(FileInfo obj) => obj is null ? 0 : Path.GetFullPath(obj.FullName).GetHashCode();
    }
}
