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

        public static void DisplayGeneratedSymbolTable(Dictionary<long, (DefinitionContext, string)> debugsymbols)
        {
            PrintSeperator("GENERATED DEBUG SYMBOL TABLE");

            Console.WriteLine($"Generated {debugsymbols.Count} debug symbols:");

            foreach (long idx in debugsymbols.Keys.OrderBy(i => i))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"    {idx:x16}h ({idx})");
                Console.CursorLeft = 45;
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"{debugsymbols[idx].Item1} {debugsymbols[idx].Item2}");
            }
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

                if (lastlnr < lines.Length - 1)
                    Console.WriteLine("       :");
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
            PrintSeperator("COMMAND LINE ARGUMENTS");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{options.RawArguments.Count} argument(s):");

            foreach (string key in options.RawArguments.Keys)
            {
                string darg = ' ' + ("--" + key).PadLeft(options.RawArguments.Keys.Max(arg => arg.Length + 2));
                string vals = string.Join(", ", options.RawArguments[key].Where(arg => arg.Length > 0).Select(arg => $"'{arg}'"));

                Console.WriteLine(vals.Length > 0 ? $"{darg}: {vals}" : darg);
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static Image<Argb32> VisuallyPrintCodeAndErrors(InterpreterState state)
        {
            var filesources = (from fi in state.Errors.Select(e => e.ErrorContext.FilePath).Concat(new[] { state.RootDocument }).Distinct(new PathEqualityComparer())
                               where fi != null
                               let lines = (File.ReadAllText(fi.FullName) + "\n").SplitIntoLines()
                               let lerrs = state.Errors.Where(e => e.ErrorContext.FilePath is FileInfo nfo && Util.ArePathsEqual(nfo, fi)).ToArray()
                               select new
                               {
                                   Path = fi,
                                   Lines = (from line in lines.Zip(Enumerable.Range(1, lines.Length), (l, i) => (Index: i, Content: l))
                                            let errs = from err in lerrs
                                                       let start = err.ErrorContext.StartLine
                                                       let end = err.ErrorContext.EndLine ?? start
                                                       where (line.Index >= start) && (line.Index <= end)
                                                       select (Error: err, Line: end - start + 1)
                                            select new
                                            {
                                                line.Index,
                                                line.Content,
                                                HasErrors = errs.Any(),
                                                Errors = errs.ToArray()
                                            }),
                                   LocalErrors = lerrs
                               }).ToArray();
            double height = (from source in filesources
                             from line in source.Lines
                             let ec = line.Errors.Length
                             select ec > 0 ? 2 + ec : 1).Sum() + (filesources.Length * 3) + 2;
            double width = Math.Min(50, (from source in filesources
                                         from line in source.Lines
                                         select Math.Max(line.Content.Length, line.Errors.Max(err => err.Error.ErrorMessage.Length))).Max() + 12);

            FontCollection collection = new FontCollection();

            if (!collection.TryFind("Courier new", out FontFamily fam))
                fam = collection.Install(PATH_COURIER_TTF);

            RendererOptions opt = new RendererOptions(new Font(fam, 12));
            SizeF charsize = TextMeasurer.Measure("W", opt);

            width *= charsize.Width;
            height *= charsize.Height;

            Image<Argb32> img = new Image<Argb32>((int)width, (int)height);
            TextGraphicsOptions texopt = new TextGraphicsOptions(true)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                BlenderMode = PixelBlenderMode.Atop,
                TabWidth = 4,
            };

            img.Mutate(i => i.Fill())
            img.Mutate(i => i.DrawText(texopt, "text", , ));
            
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

    public sealed class VisualDisplayOptions
    {
        // TODO
        //public static VisualDisplayOptions ThemeLight { get; } = new VisualDisplayOptions(
        //    "Courier New",
        //    0xFFD0D0D0,
        //    0x,
        //    ,
        //    ,
        //    ,
        //    ,
        //    ,
        //    ,
        //    ,
        //    ,
        //    ,
        //    '^'
        //);
        public static VisualDisplayOptions ThemeDark { get; } = new VisualDisplayOptions(
            "Courier New",
            0xFF202020,
            0xFFFFFFFF,
            0xFF707070,
            0xFFE83030,
            0xFFE89030,
            0xFF3080F0,
            0xFF666666,
            0xFFEEBB22,
            0xFF68B0FF,
            0xFF50D0C0,
            0xFFBB60FA,
            0xFF14D81A,
            '^'
        );

        public string FontName { get; }
        public Argb32 Background { get; }
        public Argb32 ForegroundCode { get; }
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


        public VisualDisplayOptions(
            string fontName,
            uint background,
            uint foregroundCode,
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
            char charSquiggly
        )
        {
            FontName = fontName;
            Background = new Argb32(background);
            ForegroundCode = new Argb32(foregroundCode);
            ForegroundIndent = new Argb32(foregroundIndent);
            ForegroundError = new Argb32(foregroundError);
            ForegroundWarning = new Argb32(foregroundWarning);
            ForegroundNote = new Argb32(foregroundNote);
            ForegroundErrorMessage = new Argb32(foregroundErrorMessage);
            ForegroundWarningMessage = new Argb32(foregroundWarningMessage);
            ForegroundNoteMessage = new Argb32(foregroundNoteMessage);
            ForegroundLineNumber = new Argb32(foregroundLineNumber);
            ForegroundFileHeader = new Argb32(foregroundFileHeader);
            ForegroundComment = new Argb32(foregroundComment);
            CharSquiggly = charSquiggly;
        }
    }
}
