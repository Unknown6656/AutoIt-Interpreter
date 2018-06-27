using System;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using AutoItExpressionParser.SyntaxHighlightning;
using AutoItCoreLibrary;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

namespace CoreTests
{
    using v = AutoItVariantType;


    public static unsafe class Program
    {
        public delegate int d(int i, int j);



        public static void Main(string[] args)
        {
            string au3 = @"
#cs
    comment
#ce still ignored
code() ; test

#warning This is a warning!
#using <system32/kernel32.dll>
#include ""\\8.8.8.8\test.au3""

If $a Or @macro @ macro() ..1-2 Then
    $test = $""Interpolated $var @lel and \@lol \$escaped \\$unescaped \\@kek \nhello, \""test "" << 2
    $test = ""quotes --> """" <-- and  --> ' <-- "" + 2
    $test = 'quotes --> '' <-- and  --> """" <-- ' - 4
Ifn't
    $com = .... ; dunno
    $com.xyz[$a].bc().d
EndIf

enum $x = 0, $y,$z, $w = ""test""#3

°$x = °°$y ~^^ °0
$b %= $""this is $b inside a text, e.g. '$x' or $a"" & ""test\n""
";
            int ll = 0;
            var fgc = new Dictionary<HighlightningStyle, ConsoleColor> {
                [HighlightningStyle.Code] = ConsoleColor.White,
                [HighlightningStyle.Number] = ConsoleColor.Gray,
                [HighlightningStyle.Directive] = ConsoleColor.Yellow,
                [HighlightningStyle.DirectiveParameters] = ConsoleColor.DarkYellow,
                [HighlightningStyle.Variable] = ConsoleColor.Cyan,
                [HighlightningStyle.Macro] = ConsoleColor.Magenta,
                [HighlightningStyle.String] = ConsoleColor.Red,
                [HighlightningStyle.StringEscapeSequence] = ConsoleColor.DarkCyan,
                [HighlightningStyle.Keyword] = ConsoleColor.Blue,
                [HighlightningStyle.Function] = ConsoleColor.White,
                [HighlightningStyle.Operator] = ConsoleColor.DarkGray,
                [HighlightningStyle.Symbol] = ConsoleColor.DarkGray,
                [HighlightningStyle.DotMember] = ConsoleColor.DarkMagenta,
                [HighlightningStyle.Comment] = ConsoleColor.Green,
                [HighlightningStyle.Error] = ConsoleColor.Black,
            };
            foreach (var sec in SyntaxHighlighter.ParseCode(au3.Trim())) {
                if (ll != sec.Line) {
                    Console.WriteLine();
                    ll = sec.Line;
                }
                Console.ForegroundColor = fgc[sec.Style];
                Console.Write(sec.StringContent);
            }
            Console.WriteLine();
            Console.ReadKey(true);
            return;
            v mat = v.NewMatrix(3, 3, 3);

            for (int z = 0; z < 3; ++z)
                for (int y = 0; y < 3; ++y)
                    for (int x = 0; x < 3; ++x)
                        mat[z, y, x] = $"({z}|{y}|{x})";

            mat[1, 1, 1] = v.NewDelegate<AutoItDelegate4Opt1>(TOP_KEK);

            Console.ForegroundColor = ConsoleColor.DarkYellow;

            AutoItFunctions.Debug(mat);

            //v server = AutoItFunctions.TCPListen("[::]", 41488);
            //v client = AutoItFunctions.TCPAccept(server);
            //v txt = AutoItFunctions.TCPRecv(client);

            AutoItFunctions.Debug(v.NewArray(v.NewArray(1,0,0), v.NewArray(0,1,0), v.NewArray(0,0,1)));

            v com = v.CreateCOM("shell.application");
            var tp = com.GetCOM().Type;

            AutoItFunctions.Debug(com);
        }

        public static v TOP_KEK(v v1, v v2, v v3, v? v4 = null) => $"v1={v1}, v2={v2}, v3={v3}, v4={v4 ?? v.Null}";
    }
}
