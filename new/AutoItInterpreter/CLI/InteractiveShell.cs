using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;
using Unknown6656.Controls.Console;
using Unknown6656.Imaging;

namespace Unknown6656.AutoIt3.CLI
{
    public sealed class InteractiveShell
        : IDisposable
    {
        public const int MIN_WIDTH = 120;

        private bool _isdisposed;


        public Interpreter Interpreter { get; }



        public InteractiveShell(Interpreter interpreter) => Interpreter = interpreter;

        ~InteractiveShell() => Dispose(disposing: false);

        public void Dispose()
        {
            Dispose(disposing: true);

            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_isdisposed)
            {
                if (disposing)
                {
                    MainProgram.PausePrinter = false;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null

                _isdisposed = true;
            }
        }

        public bool Initialize()
        {
            if (Width < MIN_WIDTH)
            {
                MainProgram.PrintError(Interpreter.CurrentUILanguage["error.min_width_interactive", MIN_WIDTH, Width]);

                return false;
            }

            MainProgram.PausePrinter = true;

            RedrawTotalArea();
            ResetCursor();

            return true;
        }


        private static readonly RGBAColor COLOR_HELP_FG = 0xffff;
        private static readonly RGBAColor COLOR_SEPARATOR = 0xfaaa;
        private static readonly RGBAColor COLOR_PROMPT = 0xffff;
        private static readonly string HELP_TEXT = @"
Keyboard shortcuts:     [PG UP]  Scroll history up       [ARRW UP]  Repeat previous line
                        [PG DWN] Scroll history down     [ARRW DWN] Repeat next line
                                                         [ENTER]    Execute command
".Trim();
        private static readonly int MARGIN_TOP = HELP_TEXT.Count(c => c is '\n') + 1;
        private static readonly int MARGIN_RIGHT = 40;
        private int Width => Console.WindowWidth;
        private readonly string _current_input = "TOP KEK $test";
        private readonly Index _current_cursor_pos = ^0;
        private readonly List<ScriptToken[]> _suggestions = new()
            { new[] { new ScriptToken(0, 0, 2, "$i", TokenType.Variable) }, ScriptVisualizer.TokenizeScript("@crlf + fun(3.9)") };
        private readonly List<(ScriptToken[] content, InteractiveShellStream stream)> _history = new()
        {
            (ScriptVisualizer.TokenizeScript("for $i = 0 to 10000"), InteractiveShellStream.Input),
            (ScriptVisualizer.TokenizeScript("ConsoleWriteLine($i)"), InteractiveShellStream.Output),
            (ScriptVisualizer.TokenizeScript("nextexit"), InteractiveShellStream.Input),
            (ScriptVisualizer.TokenizeScript("ConsoleWrite($res & @crlf)"), InteractiveShellStream.Output),
            (ScriptVisualizer.TokenizeScript("DllCallbackFree($callback)"), InteractiveShellStream.Input),
            (ScriptVisualizer.TokenizeScript("func test($a, $b)"), InteractiveShellStream.Output),
            (ScriptVisualizer.TokenizeScript("return $a + $b"), InteractiveShellStream.Output),
            (ScriptVisualizer.TokenizeScript("endfunc"), InteractiveShellStream.Input),
            (ScriptVisualizer.TokenizeScript("exit"), InteractiveShellStream.Error),
            (ScriptVisualizer.TokenizeScript("$ptr = DllCallbackGetPtr($callback)"), InteractiveShellStream.Error),
            (ScriptVisualizer.TokenizeScript("ConsoleWrite($callback & @crlf)"), InteractiveShellStream.Input),
            (ScriptVisualizer.TokenizeScript("ConsoleWrite($ptr & @crlf)"), InteractiveShellStream.Error),
            (ScriptVisualizer.TokenizeScript("top kek + 3λ #topkek $&\\~"), InteractiveShellStream.Output),
        };
        private int MARGIN_BOTTOM => _suggestions.Count == 0 ? 2 : 5 + _suggestions.Count;
        private int VERTICAL_SCROLL_INDEX = 0;


        enum InteractiveShellStream
        {
            Input,
            Output,
            Error,
        }


        private void RedrawTotalArea()
        {
            if (Console.CursorTop > 0)
                Console.Clear();

            RedrawHelp();
            RedrawHistoryArea();

            (int Left, int Top) cursor = RedrawInputArea(true);

            Console.CursorLeft = cursor.Left;
            Console.CursorTop = cursor.Top;

        }

        private void RedrawHelp()
        {
            ConsoleExtensions.RGBForegroundColor = COLOR_HELP_FG;
            Console.CursorTop = 0;
            Console.CursorLeft = 0;

            int width = Width;
            int height = Console.WindowHeight;

            foreach (string line in HELP_TEXT.Split('\n'))
            {
                Console.Write(line.TrimEnd());
                Console.WriteLine(new string(' ', width - Console.CursorLeft - 1));
            }

            ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
            ConsoleExtensions.WriteVertical(new string('│', height), (width - MARGIN_RIGHT - 1, 0));
            Console.CursorTop = MARGIN_TOP;
            Console.CursorLeft = 0;
            Console.WriteLine(new string('─', width - MARGIN_RIGHT - 1) + '┤');
        }

        private (int Left, int Top) RedrawInputArea(bool blink)
        {
            int width = Width;
            int height = Console.WindowHeight;
            int cursor_pos = Math.Min(width - MARGIN_RIGHT - 1, _current_cursor_pos.GetOffset(_current_input.Length));

            Console.CursorLeft = 0;
            Console.CursorTop = height - MARGIN_BOTTOM;
            ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
            Console.WriteLine(new string('─', width - MARGIN_RIGHT - 1) + '┤');

            for (int j = 0; j < _suggestions.Count - 1; ++j)
                Console.WriteLine(new string(' ', width - MARGIN_RIGHT - 1));

            ConsoleExtensions.RGBForegroundColor = COLOR_PROMPT;
            Console.CursorLeft = 0;
            Console.CursorTop = height - MARGIN_BOTTOM + 1;
            Console.Write(" > " + ScriptVisualizer.TokenizeScript(_current_input).ConvertToVT100(false));

            (int l, int t) cursor = (3 + cursor_pos, height - MARGIN_BOTTOM + 1);

            if (blink && !Console.CursorVisible)
            {
                Console.CursorTop = cursor.t;
                Console.CursorLeft = cursor.l;

                int idx = _current_cursor_pos.GetOffset(_current_input.Length);

                Console.Write($"\x1b[7m{(idx < _current_input.Length ? _current_input[idx] : ' ')}\x1b[27m");
            }

            if (_suggestions.Count > 0)
            {
                int sugg_width = _suggestions.Select(s => s.Sum(t => t.TokenLength)).Append(0).Max() + 2;
                int sugg_left = Math.Min(width - MARGIN_RIGHT - sugg_width - 2, cursor_pos + 3);
                int i = 0;

                ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
                Console.CursorTop = height - MARGIN_BOTTOM + 3 + i;
                Console.CursorLeft = sugg_left;
                Console.Write('┌' + new string('─', sugg_width) + '┐');

                foreach (ScriptToken[] suggestion in _suggestions)
                {
                    Console.CursorLeft = sugg_left;
                    Console.CursorTop = height - MARGIN_BOTTOM + 4 + i;
                    Console.Write("│ " + suggestion.ConvertToVT100(false));
                    ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
                    Console.Write(new string(' ', sugg_width + sugg_left - Console.CursorLeft + 1) + '│');

                    ++i;
                }

                Console.CursorTop = height - 1;
                Console.CursorLeft = sugg_left;
                Console.Write('└' + new string('─', sugg_width) + '┘');
                Console.CursorLeft = 3 + cursor_pos;
                Console.CursorTop = height - MARGIN_BOTTOM + 2;
                Console.Write('│');
                Console.CursorTop++;
                Console.CursorLeft--;
                Console.Write(cursor_pos + 3 == sugg_left ? '├' : cursor_pos + 3 == sugg_left + sugg_width ? '┤' : '┴');
            }

            return cursor;
        }

        private void RedrawHistoryArea()
        {
            int padding_left = 6;
            int line_width = Width - padding_left - MARGIN_RIGHT;
            string[] history = _history.SelectMany(entry =>
            {
                List<string> lines = new List<string>();
                string line = COLOR_PROMPT.ToVT100ForegroundString() + (entry.stream switch
                {
                    InteractiveShellStream.Input => "IN",
                    InteractiveShellStream.Output => "OUT",
                    InteractiveShellStream.Error => "ERR",
                }).PadLeft(padding_left - 2) + "> ";
                int len = 0;

                foreach (ScriptToken token in entry.content)
                {
                    if (len + token.TokenLength > line_width)
                    {
                        lines.Add(line);
                        line = COLOR_PROMPT.ToVT100ForegroundString() + "> ".PadLeft(padding_left);
                        len = 0;
                    }

                    line += token.ConvertToVT100(false);
                    len += token.TokenLength;
                }

                lines.Add(line);

                return lines;
            }).ToArray();

            int height = Console.WindowHeight - MARGIN_BOTTOM - MARGIN_TOP - 1;

            if (history.Length > height)
            {
                VERTICAL_SCROLL_INDEX = Math.Max(0, Math.Min(VERTICAL_SCROLL_INDEX, history.Length - height));
                history = history[^(VERTICAL_SCROLL_INDEX + height)..^VERTICAL_SCROLL_INDEX];
            }

            for (int y = 0; y < height; ++y)
            {
                Console.CursorTop = MARGIN_TOP + 1 + y;
                Console.CursorLeft = 0;

                if (height - history.Length <= y)
                    Console.Write(history[history.Length - height + y]);

                Console.Write(new string(' ', line_width + padding_left - 1 - Console.CursorLeft));
            }
        }

        private void ResetCursor()
        {

        }
    }
}

/* ┌─┬┐
 ╭╮│├┼┤
 ╰╯ └┴┘
 */

/*
 *  +----------------------------------+---------+
 *  | help text                        |         |
 *  +--------------------------------+-+         |
 *  |                                |^|         |
 *  |     ^ ^ ^ ^ ^ ^ ^ ^ ^ ^ ^      | |         |
 *  |      text moves upwards        | |         |
 *  |                                | |         |
 *  | > input                        | |         |
 *  | result                         | |         |
 *  | > input                        | |         |
 *  | result                         | |         |
 *  | > input                        | |         |
 *  | result                         |#|         |
 *  | > input                        |#|         |
 *  | result                         |#|         |
 *  | > input                        |#|         |
 *  | result                         |V|         |
 *  +--------------------------------+-+         |
 *  | > ~~~~~~~~~ I                    |         |
 *  |             |--------------+     |         |
 *  |             | autocomplete |     |         |
 *  |             | suggestions  |     |         |
 *  |             +--------------+     |         |
 *  +----------------------------------+---------+
 *
 */
