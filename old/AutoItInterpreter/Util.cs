using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.IO;
using System;

using Microsoft.FSharp.Core;

using Newtonsoft.Json.Linq;

using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Text;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;
using SixLabors.Primitives;
using SixLabors.Fonts;

using AutoItExpressionParser.SyntaxHighlightning;
using AutoItCoreLibrary;

namespace AutoItInterpreter
{
    public static class Util
    {
        public static string[] SplitIntoLines(this string s) => (s ?? "").Replace("\r\n", "\n").Split('\n');

        public static bool Match(this string s, string p, out Match m, RegexOptions o = RegexOptions.IgnoreCase | RegexOptions.Compiled) => (m = new Regex(p, o, TimeSpan.FromSeconds(5)).Match(s)).Success;

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

        public static IEnumerable<T> Concat<T>(this IEnumerable<T> e, params T[] args) => e.Concat((args ?? new T[0]) as IEnumerable<T>);

        public static IEnumerable<T> DistinctBy<T, U>(this IEnumerable<T> e, Func<T, U> sel)
        {
            HashSet<U> keys = new HashSet<U>();

            foreach (T x in e)
                if (keys.Add(sel(x)))
                    yield return x;
        }

        public static IEnumerable<U> SelectWhere<T, U>(this IEnumerable<T> e, Func<T, (bool, U)> f) => from x in e
                                                                                                       let r = f(x)
                                                                                                       where r.Item1
                                                                                                       select r.Item2;

        public static IEnumerable<U> WhereSelect<T, U>(this IEnumerable<T> e, Func<T, (bool, U)> f) => SelectWhere(e, f);

        public static IEnumerable<U> SelectWhere<T, U>(this IEnumerable<T> e, Func<T, U> s, Func<U, bool> w) => e.Select(s).Where(w);

        public static IEnumerable<U> WhereSelect<T, U>(this IEnumerable<T> e, Func<T, bool> w, Func<T, U> s) => e.Where(w).Select(s);

        public static IEnumerable<U> Filter<T, U>(this IEnumerable<T> e) where U : T => e.WhereSelect(x => x is U, x => (U)x);

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

        public static bool ArePathsEqual(FileInfo nfo1, FileInfo nfo2) => nfo1 == nfo2 || ArePathsEqual(nfo1?.FullName, nfo2?.FullName);

        public static OS GetOperatingSystem(this Compatibility comp)
        {
            switch (comp)
            {
                case Compatibility.winxp:
                case Compatibility.vista:
                case Compatibility.win7:
                case Compatibility.win8:
                case Compatibility.win81:
                case Compatibility.win10:
                case Compatibility.win:
                    return OS.Windows;
                case Compatibility.linux:
                case Compatibility.centos:
                case Compatibility.debian:
                case Compatibility.fedora:
                case Compatibility.gentoo:
                case Compatibility.opensuse:
                case Compatibility.ol:
                case Compatibility.rhel:
                case Compatibility.tizen:
                case Compatibility.ubuntu:
                case Compatibility.linuxmint:
                    return OS.Linux;
                case Compatibility.osx:
                    return OS.MacOS;
                case Compatibility.android:
                    // return something else?
                default:
                    return OS.Linux;
            }
        }

        public static void PreJIT(this Assembly assembly)
        {
            foreach (MethodBase method in assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.DeclaredOnly
                                                                                         | BindingFlags.NonPublic
                                                                                         | BindingFlags.Public
                                                                                         | BindingFlags.Instance
                                                                                         | BindingFlags.Static)))
                try
                {
                    if (method is MethodInfo nfo && (nfo.IsGenericMethodDefinition || nfo.IsGenericMethod))
                    {
                        Type[] gentypeargs = nfo.GetGenericArguments().Select(_ => typeof(object)).ToArray();
                        MethodInfo genins = nfo.MakeGenericMethod(gentypeargs);

                        RuntimeHelpers.PrepareMethod(genins.MethodHandle);
                    }
                    else if (!method.Name.Contains("<>") && !method.DeclaringType.FullName.Contains("<>"))
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
                }
                catch
                {
                }
        }

        public static bool IsSome<T>(this FSharpOption<T> opt) => FSharpOption<T>.get_IsSome(opt);

        public static bool IsNone<T>(this FSharpOption<T> opt) => FSharpOption<T>.get_IsNone(opt);

        public static bool IsSome<T>(this FSharpOption<T> opt, out T val)
        {
            if (opt.IsSome())
            {
                val = opt.Value;

                return true;
            }
            else
                val = default;

            return false;
        }

        public static string Print(this Exception ex)
        {
            if (ex is null)
                return "[unknown]  An unknown exception occured.";

            StringBuilder sb = new StringBuilder();

            while (ex != null)
            {
                sb.Insert(0, $"[{ex.GetType()}]  {ex.Message}:\n{ex.StackTrace}");

                ex = ex.InnerException;
            }

            return sb.ToString();
        }

        public static FileInfo[] FindFont(string name) => (from dir in new[] {
                                                                         "%SYSTEMROOT%/Fonts/",
                                                                         "~/.fonts/",
                                                                         "/usr/local/share/fonts/",
                                                                         "/usr/share/fonts/",
                                                                         "~/Library/Fonts/",
                                                                         "/Library/Fonts/",
                                                                         "/Network/Library/Fonts/",
                                                                         "/System/Library/Fonts/",
                                                                         "/System Folder/Fonts/"
                                                                    }
                                                           let di = new DirectoryInfo(Environment.ExpandEnvironmentVariables(dir))
                                                           where di.Exists
                                                           from fi in di.EnumerateFiles($"*{name}*.ttf")
                                                           where fi.Exists
                                                           orderby fi.FullName.Length ascending
                                                           select fi).ToArray();

        public static FontFamily FindFontFamily(string name, params string[] hints)
        {
            if (hints.Select(FindFont).FirstOrDefault(fi => fi != null) is FileInfo[] nfos && nfos.Length > 0)
            {
                FontCollection coll = new FontCollection();

                foreach (FileInfo nfo in nfos)
                    using (FileStream fs = nfo.OpenRead())
                        coll.Install(fs);

                return coll.Find(name);
            }
            else
                return SystemFonts.Families.AsParallel().FirstOrDefault(ff => ff.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        public static int CountOccurences(this string input, string search) => (input.Length - input.Replace(search, "").Length) / search.Length;
    }

    internal static class DebugPrintUtil
    {
        public static readonly string PATH_COURIER_TTF = typeof(DebugPrintUtil).Assembly.Location + "/../courier.ttf";


        public static void PrintSeperator(string title = null, int width = -1)
        {
            Console.ForegroundColor = ConsoleColor.Gray;

            if (width < 0)
                width = Console.WindowWidth - 2;

            if (title is null)
                Console.WriteLine(new string('=', width));
            else
            {
                title = title.Trim();

                int w = (width - title.Length - 1) / 2;

                if (w > 0)
                    Console.WriteLine($"{new string('=', w)} {title} {new string('=', w)}");
                else
                    Console.WriteLine(title);
            }
        }

        public static void DisplayGeneratedCode(Language lang, string code)
        {
            PrintSeperator(lang["cli.sep.gen_code"]);

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
                    string pad = (line + ' ').Remove(line.Length - tline.Length);
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

        public static void DisplayGeneratedSymbolTable(Language lang, Dictionary<long, (DefinitionContext, string)> debugsymbols)
        {
            PrintSeperator(lang["cli.sep.gen_dbgtable"]);

            Console.WriteLine(lang["cli.generated_dbgsymb", debugsymbols.Count]);

            foreach (long idx in debugsymbols.Keys.OrderBy(i => i))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"    {idx:x16}h ({idx})");
                Console.CursorLeft = 45;
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"{debugsymbols[idx].Item1}  {debugsymbols[idx].Item2}");
            }
        }

        public static void DisplayCodeAndErrors(Language lang, InterpreterState state)
        {
            PrintSeperator(lang["cli.sep.err_warn_note"]);

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

                if (lastlnr < lines.Length - 1)
                    Console.WriteLine("       :");
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void DisplayPreState(Language lang, PreInterpreterState state)
        {
            PrintSeperator(lang["cli.sep.pre_state"]);

            foreach (string fn in state.Functions.Keys)
            {
                FunctionScope func = state.Functions[fn];

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"---------------------------------------- {state.GetFunctionSignature(fn)} ----------------------------------------");
                Console.ForegroundColor = ConsoleColor.Gray;

                foreach ((string Line, DefinitionContext Context) in func.Lines)
                {
                    Console.CursorLeft = 10;
                    Console.Write(Context);
                    Console.CursorLeft = 50;
                    Console.WriteLine(Line);
                }
            }
        }

        public static void DisplayErrors(InterpreterState state, InterpreterOptions options)
        {
            PrintSeperator(options.Language["cli.sep.err_list"]);

            string root = state.RootDocument.FullName;

            Console.WriteLine(options.Language["cli.sep.err_list", state.Errors.Length]);

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

        public static void DisplayFinalResult(FinalResult res)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;

            const int iwdith = 60;
            int iheight = 12 + GetSmallText().Length;
            int w = Math.Max(Console.WindowWidth - 1, 64);
            int sdist = (w + 1 - iwdith) / 2;
            Random rand = new Random(Guid.NewGuid().GetHashCode());
            byte[] rline = new byte[w];

            for (int y = 0; y < iheight + 4; ++y)
            {
                rand.NextBytes(rline);

                for (int x = 0; x < w; ++x)
                    Console.Write((char)((rline[x] % 0x4f) + ' '));

                if (y == 1)
                {
                    Console.CursorLeft = 5;
                    Console.Write("Bush did 9/11");
                }

                Console.WriteLine();
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.CursorLeft = sdist;
            Console.CursorTop -= iheight + 1;
            Console.Write($"O{new string('=', iwdith - 2)}O");

            foreach (string line in GetBigText())
            {
                Console.CursorTop++;
                Console.CursorLeft = sdist;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write('|');
                Console.ForegroundColor = GetFGColor();
                Console.Write(line);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write('|');

            }

            foreach (string line in GetSmallText())
            {
                Console.CursorTop++;
                Console.CursorLeft = sdist;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write('|');
                Console.ForegroundColor = GetFGColor();
                Console.Write(line);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write('|');
            }

            Console.CursorTop++;
            Console.CursorLeft = sdist;
            Console.Write($"|{new string(' ', iwdith - 2)}|");
            Console.CursorTop++;
            Console.CursorLeft = sdist;
            Console.Write($"O{new string('=', iwdith - 2)}O");
            Console.CursorTop += 4;
            Console.CursorLeft = 0;
            Console.ForegroundColor = ConsoleColor.Gray;

            string[] GetBigText()
            {
                string bigtxt;

                switch (res)
                {
                    case FinalResult.OK:
                    case FinalResult.OK_Notes:
                    case FinalResult.OK_Warnings:
                        bigtxt = @"
   @@@@@  @@  @@   @@@@    @@@@   @@@@@@   @@@@@   @@@@@
  @@      @@  @@  @@  @@  @@  @@  @@      @@      @@   
   @@@@   @@  @@  @@      @@      @@@@     @@@@    @@@@
      @@  @@  @@  @@  @@  @@  @@  @@          @@      @@
  @@@@@    @@@@    @@@@    @@@@   @@@@@@  @@@@@   @@@@@
";
                        break;
                    case FinalResult.Errors_Compiled:
                        bigtxt = @"
      @@@@@    @@@     @@@@@   @@@@@  @@@@@@  @@@@@
      @@  @@  @@ @@   @@      @@      @@      @@  @@
      @@@@@   @@ @@    @@@@    @@@@   @@@@    @@  @@
      @@     @@@@@@@      @@      @@  @@      @@  @@
      @@     @@   @@  @@@@@   @@@@@   @@@@@@  @@@@@
";
                        break;
                    default:
                        bigtxt = @"
    @@@@@@   @@@    @@  @@      @@  @@  @@@@@   @@@@@@
    @@      @@ @@   @@  @@      @@  @@  @@  @@  @@
    @@@@    @@ @@   @@  @@      @@  @@  @@@@@   @@@@
    @@     @@@@@@@  @@  @@      @@  @@  @@ @@   @@
    @@     @@   @@  @@  @@@@@@   @@@@   @@  @@  @@@@@@
";
                        break;
                }

                return bigtxt.Split("\n").Select(x => x.Trim('\r').PadRight(iwdith - 2, ' ')).ToArray();
            }
            string[] GetSmallText()
            {
                string smalltxt;

                switch (res)
                {
                    case FinalResult.OK:
                    case FinalResult.OK_Notes:
                        smalltxt = @"
|        THE SCRIPT COULD BE COMPILED SUCCESSFULLY.        |
";
                        break;
                    case FinalResult.OK_Warnings:
                        smalltxt = @"
|     THE SCRIPT COULD BE COMPILED WITH SOME WARNINGS.     |
";
                        break;
                    case FinalResult.Errors_Compiled:
                        smalltxt = @"
| THE SCRIPT COULDN'T NORMALLY HAVE BEEN COMPILED, HOWEVER |
|      SOME ERRORS HAVE BEEN SUPRESSED. THE RESULTING      |
|      APPLICATION WILL PROBABLY NOT WORK AS INTENDED.     |
";
                        break;
                    default:
                        smalltxt = @"
|   THE SCRIPT COULDN'T BE COMPILED DUE TO SOME CRITICAL   |
|                     COMPILER ERRORS.                     |
";
                        break;
                }

                return smalltxt.Trim().Split("\n").Select(x => x.Trim('\r').Trim('|')).ToArray();
            }
            ConsoleColor GetFGColor()
            {
                switch (res)
                {
                    case FinalResult.OK:
                    case FinalResult.OK_Notes:
                    case FinalResult.OK_Warnings:
                        return ConsoleColor.Green;
                    case FinalResult.Errors_Compiled:
                        return ConsoleColor.DarkYellow;
                    default:
                        return ConsoleColor.Red;
                }
            }

            /*
            
          | . . . . . . . . . . . . . . . | . . . . . . . . . . . . . . . |
          | . . . . . . . | . . . . . . . | . . . . . . . | . . . . . . . |
          | . . . | . . . | . . . | . . . | . . . | . . . | . . . | . . . |
          | . | . | . | . | . | . | . | . | . | . | . | . | . | . | . | . |
          1   5   9   13  17  21  25  29  33  37  41  45  49  53  57  61  65
          '   '   '   '   '   '   '   '   '   '   '   '   '   '   '   '   '
   1 ---  O==========================================================O
       -  |                                                          |
   3  --  |    @@@@@@   @@@    @@  @@      @@  @@  @@@@@   @@@@@@    |
       -  |    @@      @@ @@   @@  @@      @@  @@  @@  @@  @@        |
   5 ---  |    @@@@    @@ @@   @@  @@      @@  @@  @@@@@   @@@@      |
       -  |    @@     @@@@@@@  @@  @@      @@  @@  @@ @@   @@        |
   7  --  |    @@     @@   @@  @@  @@@@@@   @@@@   @@  @@  @@@@@@    |
       -  |                                                          |
   9 ---  O==========================================================O
       -  |                                                          |
      --  |      @@@@@    @@@     @@@@@   @@@@@  @@@@@@  @@@@@       |
       -  |      @@  @@  @@ @@   @@      @@      @@      @@  @@      |
     ---  |      @@@@@   @@ @@    @@@@    @@@@   @@@@    @@  @@      |
       -  |      @@     @@@@@@@      @@      @@  @@      @@  @@      |
      --  |      @@     @@   @@  @@@@@   @@@@@   @@@@@@  @@@@@       |
       -  |                                                          |
     ---  O==========================================================O
       -  |                                                          |
      --  |   @@@@@  @@  @@   @@@@    @@@@   @@@@@@   @@@@@   @@@@@  |
       -  |  @@      @@  @@  @@  @@  @@  @@  @@      @@      @@      |
     ---  |   @@@@   @@  @@  @@      @@      @@@@     @@@@    @@@@   |
       -  |      @@  @@  @@  @@  @@  @@  @@  @@          @@      @@  |
      --  |  @@@@@    @@@@    @@@@    @@@@   @@@@@@  @@@@@   @@@@@   |
       -  |                                                          |
          O==========================================================O
          |        THE SCRIPT COULD BE COMPILED SUCCESSFULLY.        |
          O==========================================================O
          |     THE SCRIPT COULD BE COMPILED WITH SOME WARNINGS.     |
          O==========================================================O
          | THE SCRIPT COULDN'T NORMALLY HAVE BEEN COMPILED, HOWEVER |
          | SOME ERRORS HAVE BEEN SUPRESSED. BINARY MIGHT BE BROKEN. |
          O==========================================================O
          |   THE SCRIPT COULDN'T BE COMPILED DUE TO SOME CRITICAL   |
          |                     COMPILER ERRORS.                     |
          O==========================================================O                                                                           

             */

        }

        public static void DisplayArguments(InterpreterOptions options)
        {
            PrintSeperator(options.Language["cli.sep.cmd_args"]);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(options.Language["cli.cmd_args_desc", options.RawArguments.Count]);

            foreach (string key in options.RawArguments.Keys)
            {
                string darg = ' ' + ("--" + key).PadLeft(options.RawArguments.Keys.Max(arg => arg.Length + 2));
                string vals = string.Join(", ", options.RawArguments[key].Where(arg => arg.Length > 0).Select(arg => $"'{arg}'"));

                Console.WriteLine(vals.Length > 0 ? $"{darg}: {vals}" : darg);
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static Image<Argb32> VisuallyPrintCodeAndErrors(InterpreterState state, Language lang, VisualDisplayOptions style)
        {
            const int FontSizePX = 24;
            int voffs = 7;

            var sourcelesserrors = state.Errors.Where(err => err.ErrorContext.FilePath is null).ToArray();
            var filesources = (from fi in state.Errors.Select(e => e.ErrorContext.FilePath).Concat(new[] { state.RootDocument }).Distinct(new PathEqualityComparer())
                               where fi != null
                               let rawc = File.ReadAllText(fi.FullName).Replace("\t", "    ")
                               let lines = (rawc.Length > 0 ? rawc : "\n").SplitIntoLines()
                               let lerrs = state.Errors.Where(e => e.ErrorContext.FilePath is FileInfo nfo && Util.ArePathsEqual(nfo, fi)).ToArray()
                               let sects = SyntaxHighlighter.ParseCode(string.Join("\n", lines))
                               select new
                               {
                                   Path = fi,
                                   Lines = (from line in lines.Zip(Enumerable.Range(1, lines.Length), (l, i) => (Index: i, Content: l))
                                            let sections = sects.Where(s => s.Line == line.Index)
                                            let errs = from err in lerrs
                                                       let start = err.ErrorContext.StartLine
                                                       let end = err.ErrorContext.EndLine ?? start
                                                       where (line.Index >= start) && (line.Index <= end)
                                                       select (Error: err, Line: end - start + 1)
                                            select new
                                            {
                                                line.Index,
                                                HighlightningSections = sections.ToArray(),
                                                Content = line.Content.TrimEnd(),
                                                HasErrors = errs.Any(),
                                                Errors = errs.ToArray()
                                            }).ToArray(),
                                   LocalErrors = lerrs
                               }).ToArray();
            int height = (from source in filesources
                          from line in source.Lines
                          let ec = line.Errors.Length
                          select ec > 0 ? 2 + ec : 1).Sum() + (filesources.Length * 4) + voffs - 1;
            int width = Math.Max(50, (from source in filesources
                                      from line in source.Lines
                                      from ll in new int[] {
                                          line.Content.Length,
                                          source.Path.FullName.Length,
                                      }.Concat(line.Errors.SelectMany(err => err.Error.ErrorMessage.SplitIntoLines()).Select(s => s.Length))
                                      select ll).Max() + 12);

            if (sourcelesserrors.Length > 0)
                height += 3 + sourcelesserrors.Length;

            FontFamily fam = Util.FindFontFamily(style.FontName, style.FontPathHints);
            Font fnt_rg = new Font(fam, FontSizePX, FontStyle.Regular);
            Font fnt_bi = new Font(fam, FontSizePX, FontStyle.BoldItalic);
            Font fnt_it = new Font(fam, FontSizePX, FontStyle.Italic);
            Font fnt_bl = new Font(fam, FontSizePX, FontStyle.Bold);

            string measurement_string = new string('@', 20);
            RendererOptions renderopt = new RendererOptions(fnt_rg);
            SizeF rawcharsize = TextMeasurer.Measure(measurement_string, renderopt);
            float charwitdh = rawcharsize.Width / measurement_string.Length;
            float lineheight = rawcharsize.Height - 3;

            Image<Argb32> img = new Image<Argb32>(
                (int)(width * charwitdh + style.BorderLeft + style.BorderRight),
                (int)(height * lineheight + style.BorderTop + style.BorderBottom + 100)
            );
            TextGraphicsOptions texopt = new TextGraphicsOptions(true)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                BlenderMode = PixelBlenderMode.Atop,
                TabWidth = 4,
            };

            int ecnt = state.Errors.Count(err => err.Type == ErrorType.Fatal);
            int wcnt = state.Errors.Count(err => err.Type == ErrorType.Warning);
            int ncnt = state.Errors.Length - ecnt - wcnt;

            int drawtxt(string s, Font f, Argb32 c, int x, int y)
            {
                img.Mutate(i => i.DrawText(texopt, s, f, c, new PointF((x * charwitdh) + style.BorderLeft, (y * lineheight) + style.BorderTop + 100)));

                return s.SplitIntoLines().Length;
            }
            string pluralize(int cnt, string word) => $"{cnt} {word}{(cnt != 1 ? "s" : "")}";

            using (Image<Argb32> banner = Image.Load<Argb32>(Properties.Resources.banner_small))
                img.Mutate(i => i.DrawImage(banner, PixelBlenderMode.Atop, 1, new Point(style.BorderLeft, style.BorderTop)));

            img.Mutate(i => i.Fill(style.Background, new RectangleF(0, 0, img.Width, img.Height)));

            drawtxt("// Compiled using:", fnt_it, style.ForegroundComment, 0, 0);
            drawtxt($"//    AutoIt++ Interpreter        v.{Module.InterpreterVersion}", fnt_it, style.ForegroundComment, 0, 1);
            drawtxt($"//    AutoIt++ Expression Parser  v.{AutoItExpressionParser.Module.Version}", fnt_it, style.ForegroundComment, 0, 2);
            drawtxt($"//    AutoIt++ Core Library       v.{AutoItCoreLibrary.Module.LibraryVersion}", fnt_it, style.ForegroundComment, 0, 3);
            drawtxt($"// {DateTime.Now:yyyy-MM-dd HH:mm:ss.ffffff}", fnt_it, style.ForegroundComment, 0, 4);
            drawtxt($"// {pluralize(ecnt, "Error")}, {pluralize(wcnt, "Warning")} and {pluralize(ncnt, "Note")}", fnt_it, style.ForegroundComment, 0, 5);

            foreach (var source in filesources)
            {
                int linenr = 0;

                voffs += drawtxt("      | " + source.Path.FullName, fnt_rg, style.ForegroundFileHeader, 0, voffs);
                voffs += drawtxt("------+" + new string('-', width - 7), fnt_rg, style.ForegroundIndent, 0, voffs);

                foreach (var line in source.Lines)
                {
                    ++linenr;

                    Argb32 txtcol;
                    Argb32 lnrcol = style.ForegroundLineNumber;
                    Argb32 indcol = style.ForegroundIndent;

                    switch (line.HasErrors ? line.Errors.Min(err => (int)err.Error.Type) : -1)
                    {
                        case 0:
                            lnrcol = style.ForegroundError;
                            indcol = style.ForegroundErrorMessage;

                            break;
                        case 1:
                            lnrcol = style.ForegroundWarning;
                            indcol = style.ForegroundWarningMessage;

                            break;
                        case 2:
                            lnrcol = style.ForegroundNote;
                            indcol = style.ForegroundNoteMessage;

                            break;
                    }

                    drawtxt(linenr.ToString().PadLeft(5), line.HasErrors ? fnt_bl : fnt_rg, lnrcol, 0, voffs);
                    drawtxt(" | ", fnt_rg, indcol, 5, voffs);

                    if (line.HasErrors)
                    {
                        voffs += drawtxt(line.Content, line.HasErrors ? fnt_bl : fnt_rg, lnrcol, 8, voffs);

                        string pad = new string(' ', Math.Max(0, line.Content.Length - line.Content.TrimStart().Length));

                        drawtxt(" | ", fnt_rg, indcol, 5, voffs);

                        voffs += drawtxt(new string(style.CharSquiggly, Math.Max(0, line.Content.Length - pad.Length)), fnt_bl, indcol, 8 + pad.Length, voffs);

                        foreach ((InterpreterError err, _) in line.Errors)
                        {
                            switch (err.Type)
                            {
                                case ErrorType.Warning:
                                    txtcol = style.ForegroundWarningMessage;

                                    break;
                                case ErrorType.Note:
                                    txtcol = style.ForegroundNoteMessage;

                                    break;
                                default:
                                    txtcol = style.ForegroundErrorMessage;

                                    break;
                            }

                            drawtxt("|", fnt_rg, indcol, 6, voffs);

                            voffs += drawtxt(err.ErrorMessage, fnt_bl, txtcol, 8 + pad.Length, voffs);
                        }
                    }
                    else
                    {
                        foreach (Section section in line.HighlightningSections)
                        {
                            CodeStyle cstyle = style.Foreground[section.Style];
                            Font fnt = cstyle.IsBold && cstyle.IsItalic ? fnt_bi : cstyle.IsBold ? fnt_bl : cstyle.IsItalic ? fnt_it : fnt_rg;

                            drawtxt(section.StringContent, fnt, cstyle.Color, section.Index + 8, voffs);
                        }

                        ++voffs;
                    }
                }

                ++voffs;
            }

            if (sourcelesserrors.Length > 0)
            {
                voffs += drawtxt("      | " + lang["errors.general.no_src_avail"], fnt_rg, style.ForegroundFileHeader, 0, voffs);
                voffs += drawtxt("------+" + new string('-', width - 7), fnt_rg, style.ForegroundIndent, 0, voffs);

                foreach (InterpreterError err in sourcelesserrors)
                {
                    // TODO
                }
            }

            return img;
        }


        public enum FinalResult
            : byte
        {
            OK,
            OK_Notes,
            OK_Warnings,
            Errors_Compiled,
            Errors_Failed
        }
    }

    public sealed class PathEqualityComparer
        : IEqualityComparer<FileInfo>
    {
        public bool Equals(FileInfo x, FileInfo y) => Util.ArePathsEqual(x, y);

        public int GetHashCode(FileInfo obj) => obj is null ? 0 : Path.GetFullPath(obj.FullName).GetHashCode();
    }

    public struct CodeStyle
    {
        public Argb32 Color { get; }
        public bool IsItalic { get; }
        public bool IsBold { get; }


        public CodeStyle(uint clr, bool italic = false, bool bold = false)
        {
            Color = VisualDisplayOptions.ToArgb32(clr);
            IsItalic = italic;
            IsBold = bold;
        }

        public static implicit operator CodeStyle(uint c) => new CodeStyle(c);
        public static implicit operator CodeStyle((uint c, bool i) t) => new CodeStyle(t.c, t.i);
        public static implicit operator CodeStyle((uint c, bool i, bool b) t) => new CodeStyle(t.c, t.i, t.b);
    }

    public sealed class VisualDisplayOptions
    {
        public static VisualDisplayOptions ThemeDark { get; } = new VisualDisplayOptions(
            "Courier New",
            new[] { "cour" },
            0xFF202020,
            new Dictionary<HighlightningStyle, CodeStyle>
            {
                [HighlightningStyle.Code] = 0xFFFFFFFF,
                [HighlightningStyle.Number] = 0xFF99A88E,
                [HighlightningStyle.Directive] = 0xFFAB9769,
                [HighlightningStyle.DirectiveParameters] = 0xFF82894C,
                [HighlightningStyle.Variable] = 0xFFD2BC6F,
                [HighlightningStyle.Macro] = 0xFFCC7BFF,
                [HighlightningStyle.String] = 0xFFD09175,
                [HighlightningStyle.StringEscapeSequence] = 0xFF2FECC1,
                [HighlightningStyle.Keyword] = 0xFF4496F0,
                [HighlightningStyle.Function] = 0xFFDDDDDD,
                [HighlightningStyle.Operator] = 0xFFB4B4FF,
                [HighlightningStyle.Symbol] = 0xFFB4B4B4,
                [HighlightningStyle.DotMember] = 0xFFF2C2CD,
                [HighlightningStyle.Comment] = (0xFF14D81A, true),
                [HighlightningStyle.Error] = (0xFFFF6666, false, true),
            },
            0xFF707070,
            0xFFFF6666,
            0xFFEEBB22,
            0xFF68B0FF,
            0xFFE83030,
            0xFFE89030,
            0xFF3080F0,
            0xFF50D0C0,
            0xFFBB60FA,
            0xFF14D81A,
            '~',
            10,
            10,
            10,
            10
        );

        public Dictionary<HighlightningStyle, CodeStyle> Foreground { get; }
        public string[] FontPathHints { get; }
        public string FontName { get; }
        public Argb32 Background { get; }
        public Argb32 ForegroundIndent { get; }
        public Argb32 ForegroundError { get; }
        public Argb32 ForegroundWarning { get; }
        public Argb32 ForegroundNote { get; }
        public Argb32 ForegroundErrorMessage { get; }
        public Argb32 ForegroundWarningMessage { get; }
        public Argb32 ForegroundNoteMessage { get; }
        public Argb32 ForegroundLineNumber { get; }
        public Argb32 ForegroundFileHeader { get; }
        public Argb32 ForegroundComment { get; }
        public char CharSquiggly { get; }
        public int BorderTop { get; }
        public int BorderLeft { get; }
        public int BorderRight { get; }
        public int BorderBottom { get; }


        public VisualDisplayOptions(
            string fontName,
            string[] fontHints,
            uint background,
            Dictionary<HighlightningStyle, CodeStyle> foreground,
            uint foregroundIndent,
            uint foregroundError,
            uint foregroundWarning,
            uint foregroundNote,
            uint foregroundErrorMessage,
            uint foregroundWarningMessage,
            uint foregroundNoteMessage,
            uint foregroundLineNumber,
            uint foregroundFileHeader,
            uint foregroundComment,
            char charSquiggly,
            int borderTop,
            int borderLeft,
            int borderRight,
            int borderBottom
        )
        {
            FontName = fontName;
            FontPathHints = fontHints ?? new string[0];
            Background = ToArgb32(background);
            Foreground = foreground;
            ForegroundIndent = ToArgb32(foregroundIndent);
            ForegroundError = ToArgb32(foregroundError);
            ForegroundWarning = ToArgb32(foregroundWarning);
            ForegroundNote = ToArgb32(foregroundNote);
            ForegroundErrorMessage = ToArgb32(foregroundErrorMessage);
            ForegroundWarningMessage = ToArgb32(foregroundWarningMessage);
            ForegroundNoteMessage = ToArgb32(foregroundNoteMessage);
            ForegroundLineNumber = ToArgb32(foregroundLineNumber);
            ForegroundFileHeader = ToArgb32(foregroundFileHeader);
            ForegroundComment = ToArgb32(foregroundComment);
            CharSquiggly = charSquiggly;
            BorderTop = borderTop;
            BorderLeft = borderLeft;
            BorderRight = borderRight;
            BorderBottom = borderBottom;
        }

        internal static Argb32 ToArgb32(uint v) => new Argb32(
            (byte)(v >> 16),
            (byte)(v >> 8),
            (byte)v,
            (byte)(v >> 24)
        );
    }
}
