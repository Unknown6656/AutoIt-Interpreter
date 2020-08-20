using System.Collections.Generic;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Common;
using Unknown6656.Controls.Console;
using Unknown6656.Imaging;
using System.Windows.Forms;
using Unknown6656.AutoIt3.Parser.ExpressionParser;

namespace Unknown6656.AutoIt3.CLI
{
    public sealed class InteractiveShell
        : IDisposable
    {
        public const int MIN_WIDTH = 120;

        internal static readonly string[] KNOWN_MACROS =
        {
            "@APPDATACOMMONDIR", "@APPDATADIR", "@AUTOITEXE", "@AUTOITPID", "@AUTOITVERSION", "@AUTOITX64", "@COMMONFILESDIR", "@COMPILED", "@COMPUTERNAME", "@COMSPEC", "@CR", "@CRLF",
            "@CPUARCH", "@DESKTOPCOMMONDIR", "@DESKTOPDIR", "@DOCUMENTSCOMMONDIR", "@EXITCODE", "@ERROR", "@EXTENDED", "@FAVORITESCOMMONDIR", "@FAVORITESDIR", "@HOMEDRIVE", "@HOMEPATH",
            "@HOUR", "@LOCALAPPDATADIR", "@LOGONDOMAIN", "@LOGONSERVER", "@MDAY", "@MIN", "@MON", "@MSEC", "@MYDOCUMENTSDIR", "@NUMPARAMS", "@MUILANG", "@TAB", "@SW_DISABLE", "@SW_ENABLE",
            "@SW_HIDE", "@SW_LOCK", "@SW_MAXIMIZE", "@SW_MINIMIZE", "@SW_RESTORE", "@SW_SHOW", "@SW_SHOWDEFAULT", "@SW_SHOWMAXIMIZED", "@SW_SHOWMINIMIZED", "@SW_SHOWMINNOACTIVE",
            "@SW_SHOWNA", "@SW_SHOWNOACTIVATE", "@SW_SHOWNORMAL", "@SW_UNLOCK", "@TEMPDIR", "@OSSERVICEPACK", "@OSBUILD", "@OSTYPE", "@OSVERSION", "@PROGRAMFILESDIR", "@PROGRAMSCOMMONDIR",
            "@PROGRAMSDIR", "@SCRIPTDIR", "@SCRIPTFULLPATH", "@SCRIPTLINENUMBER", "@SCRIPTNAME", "@STARTMENUCOMMONDIR", "@STARTMENUDIR", "@STARTUPCOMMONDIR", "@STARTUPDIR", "@SYSTEMDIR",
            "@WINDOWSDIR", "@SEC", "@USERNAME", "@YDAY", "@YEAR", "@WDAY", "@WORKINGDIR", "@ESC", "@VTAB", "@NUL", "@DATE", "@DATE_TIME", "@E", "@NL", "@PHI", "@PI", "@TAU", "@LF", "@DISCARD"
        };
        internal static readonly string[] KNOWN_OPERATORS = { "+", "-", "*", "/", "+=", "-=", "*=", "/=", "&", "&=", "^", "<=", "<", ">", ">=", "<>", "=", "==" };

        private static readonly RGBAColor COLOR_HELP_FG = 0xffff;
        private static readonly RGBAColor COLOR_SEPARATOR = 0xfaaa;
        private static readonly RGBAColor COLOR_PROMPT = 0xffff;
        private static readonly string HELP_TEXT = $@"
Keyboard shortcuts:                                                 [PAGE UP/DOWN]     Scroll history up/down
                        [F5]    Repeat previous line                [ARROW LEFT/RIGHT] Navigate inside the text
                        [F6]    Repeat next line                    [ARROW UP/DOWN]    Select code suggestion
                        [ENTER] Execute current input               [TAB]              Insert selected code suggestion
                        ""EXIT""  Exit the interactive environment
".Trim();
        private static readonly int HISTORY_PADDING_LEFT = 6;
        private static readonly int MAX_SUGGESTIONS = 8;
        private static readonly int MARGIN_RIGHT = 40;
        private static readonly int MARGIN_TOP = HELP_TEXT.Count(c => c is '\n') + 1;
        private int MARGIN_BOTTOM => MAX_SUGGESTIONS + 5; // Suggestions.Count == 0 ? 2 : 5 + Math.Min(MAX_SUGGESTIONS, Suggestions.Count);
        private static int WIDTH => Console.WindowWidth;
        private static int HEIGHT => Console.WindowHeight;


        private Index _current_cursor_pos = ^0;
        private bool _isdisposed;


        public List<(ScriptToken[] content, InteractiveShellStreamDirection stream)> History { get; } = new();

        public List<ScriptToken[]> Suggestions { get; } = new();

        public string CurrentInput { get; private set; } = "";

        public Index CurrentCursorPosition
        {
            get => _current_cursor_pos;
            private set
            {
                _current_cursor_pos = value;

                UpdateSuggestions();
            }
        }

        public int CurrentSuggestionIndex { get; private set; }

        public int HistoryScrollIndex { get; private set; }

        public Interpreter Interpreter { get; }

        public bool IsRunning { get; set; } = true;

        public AU3Thread Thread { get; }

        public AU3CallFrame CallFrame { get; }


        public InteractiveShell(Interpreter interpreter)
        {
            Interpreter = interpreter;
            Thread = interpreter.CreateNewThread();
            CallFrame = Thread.PushAnonymousCallFrame();
        }

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
                    Thread.Dispose();

                    MainProgram.PausePrinter = false;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null

                _isdisposed = true;
            }
        }

        public bool Initialize()
        {
            if (WIDTH < MIN_WIDTH)
            {
                MainProgram.PrintError(Interpreter.CurrentUILanguage["error.min_width_interactive", MIN_WIDTH, WIDTH]);

                return false;
            }

            MainProgram.PausePrinter = true;

            return true;
        }

        public void Run()
        {
            if (Console.CursorTop > 0)
                Console.Clear();

            MainLoop();
        }

        private void MainLoop()
        {
            UpdateSuggestions();

            int sugg_count = 0;
            int hist_count = 0;
            int width = 0;
            int height = 0;

            while (IsRunning)
            {
                if (width != WIDTH || height != HEIGHT)
                {
                    RedrawHelp();

                    sugg_count = 0;
                    hist_count = 0;
                }

                if (sugg_count != Suggestions.Count || hist_count != History.Count)
                {
                    RedrawHistoryArea();

                    sugg_count = Suggestions.Count;
                    hist_count = History.Count;
                }

                (int Left, int Top) cursor = RedrawInputArea(true);

                Console.CursorLeft = cursor.Left;
                Console.CursorTop = cursor.Top;

                HandleKeyPress();
            }
        }

        private void HandleKeyPress()
        {
            ConsoleKeyInfo k = Console.ReadKey();
            int cursor_pos = CurrentCursorPosition.GetOffset(CurrentInput.Length);

            switch (k.Key)
            {
                case ConsoleKey.RightArrow when k.Modifiers.HasFlag(ConsoleModifiers.Control):
                    if (cursor_pos < CurrentInput.Length)
                    {
                        ScriptToken? token = CurrentlyTypedToken;

                        if (token is null)
                            CurrentCursorPosition = cursor_pos + 1;
                        else if (token.CharIndex + token.TokenLength == cursor_pos)
                        {
                            CurrentCursorPosition = cursor_pos + 1;
                            token = CurrentlyTypedToken;

                            if (token is { })
                                CurrentCursorPosition = token.CharIndex + token.TokenLength;
                        }
                        else
                            CurrentCursorPosition = token.CharIndex + token.TokenLength;
                    }
                    break;
                case ConsoleKey.LeftArrow when k.Modifiers.HasFlag(ConsoleModifiers.Control):
                    if (cursor_pos > 0)
                        CurrentCursorPosition = CurrentlyTypedToken?.CharIndex ?? cursor_pos - 1;

                    break;
                case ConsoleKey.RightArrow:
                    if (cursor_pos < CurrentInput.Length)
                        CurrentCursorPosition = cursor_pos + 1;

                    break;
                case ConsoleKey.LeftArrow:
                    if (cursor_pos > 0)
                        CurrentCursorPosition = cursor_pos - 1;

                    break;
                case ConsoleKey.UpArrow when k.Modifiers.HasFlag(ConsoleModifiers.Control):
                    CurrentSuggestionIndex = 0;

                    break;
                case ConsoleKey.DownArrow when k.Modifiers.HasFlag(ConsoleModifiers.Control):
                    CurrentSuggestionIndex = Suggestions.Count - 1;

                    break;
                case ConsoleKey.UpArrow:
                    if (CurrentSuggestionIndex > 0)
                        --CurrentSuggestionIndex;

                    break;
                case ConsoleKey.DownArrow:
                    if (CurrentSuggestionIndex < Suggestions.Count - 1)
                        ++CurrentSuggestionIndex;

                    break;
                case ConsoleKey.PageDown when k.Modifiers.HasFlag(ConsoleModifiers.Control):
                    HistoryScrollIndex = 0;

                    break;
                case ConsoleKey.PageUp when k.Modifiers.HasFlag(ConsoleModifiers.Control):
                    HistoryScrollIndex = History.Count - 1;

                    break;
                case ConsoleKey.PageUp:
                    if (HistoryScrollIndex < History.Count - 1)
                        ++HistoryScrollIndex;

                    break;
                case ConsoleKey.PageDown:
                    if (HistoryScrollIndex > 0)
                        --HistoryScrollIndex;

                    break;
                case ConsoleKey.Delete when k.Modifiers.HasFlag(ConsoleModifiers.Control):
                    break;
                case ConsoleKey.Delete:
                    if (cursor_pos < CurrentInput.Length)
                    {
                        CurrentInput = CurrentInput.Remove(cursor_pos, 1);
                        UpdateSuggestions();
                    }

                    break;
                case ConsoleKey.Backspace when k.Modifiers.HasFlag(ConsoleModifiers.Control):
                    break;
                case ConsoleKey.Backspace:
                    if (cursor_pos > 0)
                    {
                        CurrentInput = CurrentInput.Remove(cursor_pos - 1, 1);
                        CurrentCursorPosition = cursor_pos - 1;
                    }

                    break;
                case ConsoleKey.Home:
                    CurrentCursorPosition = 0;

                    break;
                case ConsoleKey.End:
                    CurrentCursorPosition = ^0;

                    break;
                case ConsoleKey.Enter:
                    ProcessInput();
                    UpdateSuggestions();

                    break;
                case ConsoleKey.Tab:
                    string insertion = Suggestions[CurrentSuggestionIndex].Select(t => t.Content).StringConcat();
                    ScriptToken? curr_token = CurrentlyTypedToken;
                    int insertion_index = curr_token?.CharIndex ?? cursor_pos;
                    int deletion_index = curr_token is null ? cursor_pos : curr_token.CharIndex + curr_token.TokenLength;

                    while (deletion_index < CurrentInput.Length && CurrentInput[deletion_index] == ' ')
                        ++deletion_index;

                    CurrentInput = CurrentInput[..insertion_index] + insertion + CurrentInput[deletion_index..];
                    CurrentCursorPosition = insertion_index + insertion.Length;

                    break;
                default:
                    if (k.KeyChar is char c and >= ' ' and <= 'ÿ')
                    {
                        CurrentInput = CurrentInput[..cursor_pos] + c + CurrentInput[cursor_pos..];
                        CurrentCursorPosition = cursor_pos + 1;
                    }

                    break;
            }
        }

        private static void RedrawHelp()
        {
            ConsoleExtensions.RGBForegroundColor = COLOR_HELP_FG;
            Console.CursorTop = 0;
            Console.CursorLeft = 0;

            int width = WIDTH;
            int height = HEIGHT;

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
            int width = WIDTH;
            int height = HEIGHT;
            int cursor_pos = Math.Min(width - MARGIN_RIGHT - 1, CurrentCursorPosition.GetOffset(CurrentInput.Length));

            Console.CursorLeft = 0;
            Console.CursorTop = height - MARGIN_BOTTOM - 1;
            ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
            Console.WriteLine(new string('─', width - MARGIN_RIGHT - 1) + '┤');
            ConsoleExtensions.RGBForegroundColor = COLOR_PROMPT;
            Console.CursorLeft = 0;
            Console.CursorTop = height - MARGIN_BOTTOM;
            Console.Write(" > " + ScriptVisualizer.TokenizeScript(CurrentInput).ConvertToVT100(false));

            (int l, int t) cursor = (3 + cursor_pos, height - MARGIN_BOTTOM);

            if (blink && !Console.CursorVisible)
            {
                Console.CursorTop = cursor.t;
                Console.CursorLeft = cursor.l;

                int idx = CurrentCursorPosition.GetOffset(CurrentInput.Length);

                Console.Write($"\x1b[7m{(idx < CurrentInput.Length ? CurrentInput[idx] : ' ')}\x1b[27m");
            }

            Console.Write(new string(' ', width - MARGIN_RIGHT - HISTORY_PADDING_LEFT - 1));

            string pad_full = new string(' ', width - MARGIN_RIGHT - 1);

            if (Suggestions.Count > 0)
            {
                if (CurrentSuggestionIndex > Suggestions.Count)
                    CurrentSuggestionIndex = Suggestions.Count - 1;
                else if (CurrentSuggestionIndex < 0)
                    CurrentSuggestionIndex = 0;

                int start_index = CurrentSuggestionIndex < MAX_SUGGESTIONS / 2 ? 0
                                : CurrentSuggestionIndex > Suggestions.Count - MAX_SUGGESTIONS / 2 ? Suggestions.Count - MAX_SUGGESTIONS
                                : CurrentSuggestionIndex - MAX_SUGGESTIONS / 2;

                ScriptToken[][] suggestions = Suggestions.Skip(start_index).Take(MAX_SUGGESTIONS).ToArray();

                int sugg_width = suggestions.Select(s => s.Sum(t => t.TokenLength)).Append(0).Max() + 2;
                int sugg_left = Math.Min(width - MARGIN_RIGHT - sugg_width - 2, cursor_pos + 3);
                int i = 0;

                ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
                Console.CursorTop = height - MARGIN_BOTTOM + 2 + i;
                Console.CursorLeft = 0;

                string pad_left = new string(' ', sugg_left);
                string pad_right = new string(' ', WIDTH - MARGIN_RIGHT - sugg_left - 3 - sugg_width);

                Console.Write(pad_left + '┌' + new string('─', sugg_width) + '┐' + pad_right);
                Console.CursorLeft = 0;
                Console.CursorTop = height - MARGIN_BOTTOM + 1;

                string indicator = $"│ {CurrentSuggestionIndex + 1}/{Suggestions.Count}";

                Console.Write(new string(' ', 3 + cursor_pos) + indicator + new string(' ', WIDTH - MARGIN_RIGHT - cursor_pos - 4 - indicator.Length));
                Console.CursorTop++;
                Console.CursorLeft = 3 + cursor_pos;
                Console.Write(cursor_pos + 3 == sugg_left ? '├' : cursor_pos + 2 == sugg_left + sugg_width ? '┤' : '┴');

                foreach (ScriptToken[] suggestion in suggestions)
                {
                    Console.CursorTop = height - MARGIN_BOTTOM + 3 + i;
                    Console.CursorLeft = 0;
                    Console.Write(pad_left + '│');

                    if (i == CurrentSuggestionIndex - start_index)
                        Console.Write("\x1b[7m");

                    Console.Write(' ' + suggestion.ConvertToVT100(false) + COLOR_SEPARATOR.ToVT100ForegroundString());
                    Console.Write(new string(' ', sugg_width + sugg_left - Console.CursorLeft + 1));

                    if (i == CurrentSuggestionIndex - start_index)
                        Console.Write("\x1b[27m");

                    Console.Write('│' + pad_right);

                    ++i;
                }

                Console.CursorLeft = 0;
                Console.CursorTop = height - MARGIN_BOTTOM + 3 + i;
                Console.Write(pad_left + '└' + new string('─', sugg_width) + '┘' + pad_right);

                while (i < MAX_SUGGESTIONS)
                {
                    Console.CursorTop = height - MARGIN_BOTTOM + 4 + i;
                    Console.CursorLeft = 0;
                    Console.Write(pad_full);

                    ++i;
                }
            }
            else
                for (int i = 1; i < MAX_SUGGESTIONS + 4; ++i)
                {
                    Console.CursorTop = height - MARGIN_BOTTOM + i;
                    Console.CursorLeft = 0;
                    Console.Write(pad_full);
                }

            return cursor;
        }

        private void RedrawHistoryArea()
        {
            int line_width = WIDTH - HISTORY_PADDING_LEFT - MARGIN_RIGHT;
            string[] history = History.SelectMany(entry =>
            {
                List<string> lines = new List<string>();
                string line = COLOR_PROMPT.ToVT100ForegroundString() + (entry.stream switch
                {
                    InteractiveShellStreamDirection.Input => "IN",
                    InteractiveShellStreamDirection.Output => "OUT",
                    InteractiveShellStreamDirection.Error => "ERR",
                }).PadLeft(HISTORY_PADDING_LEFT - 2) + "> ";
                int len = 0;

                foreach (ScriptToken token in entry.content)
                {
                    if (len + token.TokenLength > line_width)
                    {
                        lines.Add(line);
                        line = COLOR_PROMPT.ToVT100ForegroundString() + "> ".PadLeft(HISTORY_PADDING_LEFT);
                        len = 0;
                    }

                    line += token.ConvertToVT100(false);
                    len += token.TokenLength;
                }

                lines.Add(line);

                return lines;
            }).ToArray();

            int height = Console.WindowHeight - MARGIN_BOTTOM - MARGIN_TOP - 2;

            if (history.Length > height)
            {
                HistoryScrollIndex = Math.Max(0, Math.Min(HistoryScrollIndex, history.Length - height));
                history = history[^(HistoryScrollIndex + height)..^HistoryScrollIndex];
            }

            for (int y = 0; y < height; ++y)
            {
                Console.CursorTop = MARGIN_TOP + 1 + y;
                Console.CursorLeft = 0;

                if (height - history.Length <= y)
                    Console.Write(history[history.Length - height + y]);

                Console.Write(new string(' ', line_width + HISTORY_PADDING_LEFT - 1 - Console.CursorLeft));
            }
        }

        private void ProcessInput()
        {
            if (!string.IsNullOrWhiteSpace(CurrentInput))
            {
                string input = CurrentInput.Trim();

                CurrentInput = "";
                CurrentCursorPosition = 0;
                History.Add((ScriptVisualizer.TokenizeScript(input), InteractiveShellStreamDirection.Input));

                if (AU3CallFrame.REGEX_EXIT.IsMatch(input))
                    IsRunning = false;
                else
                    try
                    {
                        CallFrame.InsertReplaceSourceCode(CallFrame.CurrentInstructionPointer, input);

                        InterpreterResult? result = CallFrame.ParseCurrentLine();

                        if (result?.OptionalError is { Message: string error })
                            History.Add((new[] { new ScriptToken(0, 0, error.Length, error, TokenType.UNKNOWN) }, InteractiveShellStreamDirection.Error));
                        else if (CallFrame.VariableResolver.TryGetVariable(AST.VARIABLE.Discard, VariableSearchScope.Global, out Variable? variable))
                        {
                            string text = variable.Value.ToDebugString(Interpreter);

                            History.Add((new[] { new ScriptToken(0, 0, text.Length, text, TokenType.DirectiveOption) }, InteractiveShellStreamDirection.Output));
                        }
                    }
                    catch
                    {
                    }
            }
        }

        public ScriptToken? CurrentlyTypedToken
        {
            get
            {
                ScriptToken[] tokens = ScriptVisualizer.TokenizeScript(CurrentInput);
                int cursor = CurrentCursorPosition.GetOffset(CurrentInput.Length);

                return cursor == 0 ? tokens[0] : tokens.FirstOrDefault(t => t.CharIndex < cursor && cursor <= t.CharIndex + t.TokenLength);
            }
        }

        public void UpdateSuggestions()
        {
            Suggestions.Clear();

            string[] ops = KNOWN_OPERATORS;
            string[] vars = Thread.CurrentVariableResolver.LocalVariables.Concat(Interpreter.VariableResolver.GlobalVariables).ToArray(v => '$' + v.Name);
            string[] funcs = Interpreter.ScriptScanner.CachedFunctions.ToArray(f => f.Name);
            ScriptToken? curr_token = CurrentlyTypedToken;
            IEnumerable<string> suggestions = string.IsNullOrEmpty(CurrentInput)
                ? KNOWN_MACROS.Concat(vars).Concat(funcs).Concat(ScriptFunction.RESERVED_NAMES) // TODO : suggest all funcs, variables, macros, directives, and statements
                : curr_token switch
                {
                    { Type: TokenType.Keyword } => ScriptFunction.RESERVED_NAMES,
                    { Type: TokenType.Identifier or TokenType.FunctionCall } => funcs.Concat(ScriptFunction.RESERVED_NAMES),
                    { Type: TokenType.Directive } => new string[0], // suggest directive
                    { Type: TokenType.DirectiveOption } => new string[0], // suggest directive option
                    { Type: TokenType.Operator } => ops,
                    { Type: TokenType.Symbol or TokenType.Comment or TokenType.Number or TokenType.String } => Array.Empty<string>(), // suggest nothing
                    { Type: TokenType.Variable } => vars,
                    { Type: TokenType.Macro } => KNOWN_MACROS,
                    { Type: TokenType.UNKNOWN or TokenType.Whitespace or TokenType.NewLine } or null or _ =>
                        KNOWN_MACROS.Concat(vars).Concat(ops).Concat(funcs),
                        // suggest all functions, variables, and macros
                };

            string? filter = curr_token?.Content[..(CurrentCursorPosition.GetOffset(CurrentInput.Length) - curr_token.CharIndex)];

            if (string.IsNullOrWhiteSpace(filter))
                filter = null;

            Suggestions.AddRange(from s in suggestions.Distinct()
                                 let text = s.Trim() + ' '
                                 where text.Length > 1
                                 let tokens = ScriptVisualizer.TokenizeScript(text)[..^1]
                                 let first = tokens[0]
                                 where filter is null || first.Content.StartsWith(filter, StringComparison.InvariantCultureIgnoreCase)
                                 orderby first.Type, text ascending
                                 select tokens);
        }
    }

    public enum InteractiveShellStreamDirection
    {
        Input,
        Output,
        Error,
    }
}

/* ┌─┬┐
 ╭╮│├┼┤
 ╰╯ └┴┘
 *      +----------------------------------+---------------+
 *      | help text                        |  variable     |
 *      +--------------------------------+-+  monitor (?)  |
 *      |                                |^|               |
 *      |     ^ ^ ^ ^ ^ ^ ^ ^ ^ ^ ^      | |               |
 *      |      text moves upwards        | |               |
 *      |                                | |               |
 *      | > input                        | |               |
 *      | result                         | |               |
 *      | > input                        | |               |
 *      | result                         | |               |
 *      | > input                        | |               |
 *      | result                         |#|               |
 *      | > input                        |#|               |
 *      | result                         |#|               |
 *      | > input                        |#|               |
 *      | result                         |V|               |
 *      +--------------------------------+-+               |
 *      | > ~~~~~~~~~ I                    |               |
 *      |             |--------------+     |               |
 *      |             | autocomplete |     |               |
 *      |             | suggestions  |     |               |
 *      |             +--------------+     |               |
 *      +----------------------------------+---------------+
 */
