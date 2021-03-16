using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Controls.Console;
using Unknown6656.Imaging;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.CLI
{
    public sealed class InteractiveShell
        : IDisposable
    {
        public const int MIN_WIDTH = 120;

        internal readonly string[] KNOWN_MACROS =
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
Commands and keyboard shortcuts:
                                                       [PAGE UP/DOWN]     Scroll history up/down
  [F5]             Repeat previous line                [ARROW LEFT/RIGHT] Navigate inside the text. Use
  [F6]             Repeat next line                                       the [CTRL]-key to jump by words
  [ENTER]          Execute current input               [ARROW UP/DOWN]    Select code suggestion
  [SHIFT]+[ENTER]  Enter a line break                  [TAB]              Insert selected code suggestion
  ""EXIT""           Exit the interactive environment    ""CLEAR""            Clear the history window
".Trim();
        private static readonly int MAX_SUGGESTIONS = 8;
        private static readonly int MARGIN_RIGHT = 50;
        private static readonly int MARGIN_TOP = HELP_TEXT.Count(c => c is '\n') + 1;
        private int MARGIN_BOTTOM => MAX_SUGGESTIONS + 5; // Suggestions.Count == 0 ? 2 : 5 + Math.Min(MAX_SUGGESTIONS, Suggestions.Count);
        private static int WIDTH => Console.WindowWidth;
        private static int HEIGHT => Console.WindowHeight;


        private Index _current_cursor_pos = ^0;
        private bool _isdisposed;


        public List<(ScriptToken[] Content, InteractiveShellStreamDirection Stream)> History { get; } = new();

        public List<(ScriptToken[] Display, string Content)> Suggestions { get; } = new();

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

        public VariableScope Variables { get; }

        public AU3CallFrame CallFrame { get; }

        public ScriptToken? CurrentlyTypedToken
        {
            get
            {
                ScriptToken[] tokens = ScriptVisualizer.TokenizeScript(CurrentInput);
                int cursor = CurrentCursorPosition.GetOffset(CurrentInput.Length);

                return cursor == 0 ? tokens[0] : tokens.FirstOrDefault(t => t.CharIndex < cursor && cursor <= t.CharIndex + t.TokenLength);
            }
        }


        public InteractiveShell(Interpreter interpreter)
        {
            Interpreter = interpreter;
            Thread = interpreter.CreateNewThread();
            CallFrame = Thread.PushAnonymousCallFrame();
            Variables = Thread.CurrentVariableResolver;
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
            try
            {
                MainProgram.PausePrinter = true;

                UpdateSuggestions();

                //int sugg_count = 0;
                int hist_count = 0;
                int width = 0;
                int height = 0;
                int input_y = -1;

                while (IsRunning)
                {
                    if (NativeInterop.OperatingSystem.HasFlag(OS.Windows))
                        Console.CursorVisible = false;

                    if (width != WIDTH || height != HEIGHT)
                    {
                        Console.Clear();
                        RedrawHelp();

                        width = WIDTH;
                        height = HEIGHT;
                        hist_count = -1;
                    }

                    (int Left, int Top, int InputAreaYOffset) cursor = RedrawInputArea(false);

                    if (hist_count != History.Count || cursor.InputAreaYOffset != input_y)
                    {
                        RedrawHistoryArea(cursor.InputAreaYOffset);
                        RedrawThreadAndVariableWatchers();

                        hist_count = History.Count;
                        input_y = cursor.InputAreaYOffset;
                    }
                    else if (Interpreter.Threads.Length > 1)
                        RedrawThreadAndVariableWatchers();

                    Console.CursorLeft = cursor.Left;
                    Console.CursorTop = cursor.Top;

                    if (NativeInterop.OperatingSystem.HasFlag(OS.Windows))
                        Console.CursorVisible = true;

                    HandleKeyPress();
                }
            }
            finally
            {
                if (NativeInterop.OperatingSystem.HasFlag(OS.Windows))
                    Console.CursorVisible = true;

                Console.Clear();

                MainProgram.PausePrinter = false;
            }
        }

        private void HandleKeyPress()
        {
            int w = WIDTH, h = HEIGHT;

            while (!Console.KeyAvailable)
                if (w != WIDTH || h != HEIGHT)
                    return;
                else
                    System.Threading.Thread.Sleep(10);

            ConsoleKeyInfo k = Console.ReadKey(true);
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
                case ConsoleKey.F5:
                    CurrentInput = History.Where(t => t.Stream is InteractiveShellStreamDirection.Input)
                                          .LastOrDefault()
                                          .Content
                                          ?.Select(t => t.Content)
                                          .StringJoin("")
                                          ?? CurrentInput;
                    CurrentCursorPosition = CurrentInput.Length;

                    break;
                case ConsoleKey.Enter:
                    ProcessInput();
                    UpdateSuggestions();

                    break;
                case ConsoleKey.Tab:
                    string insertion = Suggestions[CurrentSuggestionIndex].Content;
                    ScriptToken? curr_token = CurrentlyTypedToken;
                    int insertion_index = curr_token?.CharIndex ?? cursor_pos;
                    int deletion_index = curr_token is null ? cursor_pos : curr_token.CharIndex + curr_token.TokenLength;

                    while (deletion_index < CurrentInput.Length && CurrentInput[deletion_index] == ' ')
                        ++deletion_index;

                    if (deletion_index >= CurrentInput.Length)
                        insertion = insertion.TrimEnd();

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

            foreach (string line in HELP_TEXT.SplitIntoLines())
            {
                Console.Write(line.TrimEnd());
                Console.WriteLine(new string(' ', width - Console.CursorLeft - 1));
            }

            ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
            ConsoleExtensions.WriteVertical(new string('│', height - MARGIN_TOP), (width - MARGIN_RIGHT - 1, MARGIN_TOP));
            Console.CursorTop = MARGIN_TOP;
            Console.CursorLeft = 0;
            Console.WriteLine(new string('─', width - MARGIN_RIGHT - 1) + '┬' + new string('─', MARGIN_RIGHT));
        }

        private (int Left, int Top, int InputAreaYOffset) RedrawInputArea(bool blink)
        {
            int width = WIDTH;
            int height = HEIGHT;
            int input_area_width = width - MARGIN_RIGHT - 1;
            string[] input_lines = CurrentInput.PartitionByArraySize(input_area_width - 3).ToArray(c => new string(c));

            if (input_lines.Length == 0)
                input_lines = new[] { "" };

            int cursor_pos_x = Math.Min(input_area_width, CurrentCursorPosition.GetOffset(CurrentInput.Length) % (input_area_width - 3));
            int input_area_height = height - MARGIN_BOTTOM + 1 - input_lines.Length;

            ConsoleExtensions.WriteBlock(new string(' ', input_area_width * (MARGIN_BOTTOM + input_lines.Length - 1)), 0, input_area_height - 1, input_area_width, MARGIN_BOTTOM + input_lines.Length - 1, true);
            ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
            Console.CursorLeft = 0;
            Console.CursorTop = input_area_height - 1;
            Console.WriteLine(new string('─', width - MARGIN_RIGHT - 1) + '┤');

            int line_no = 0;

            foreach (string line in input_lines)
            {
                string txt = (line_no == 0 ? " > " : "   ") + ScriptVisualizer.TokenizeScript(line).ConvertToVT100(false);

                Console.CursorLeft = 0;
                Console.CursorTop = input_area_height + line_no;
                ConsoleExtensions.RGBForegroundColor = COLOR_PROMPT;
                Console.Write(txt);

                if (Console.CursorLeft < input_area_width)
                    Console.Write(new string(' ', input_area_width - Console.CursorLeft));

                ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
                Console.Write('│');

                ++line_no;
            }

            (int l, int t) cursor = (3 + cursor_pos_x, input_area_height + CurrentCursorPosition.GetOffset(CurrentInput.Length) / (input_area_width - 4));
#pragma warning disable CA1416 // Validate platform compatibility
            bool cursor_visible = NativeInterop.DoPlatformDependent(() => Console.CursorVisible, () => false);
#pragma warning restore CA1416

            if (blink && !cursor_visible)
            {
                Console.CursorTop = cursor.t;
                Console.CursorLeft = cursor.l;

                int idx = CurrentCursorPosition.GetOffset(CurrentInput.Length);

                Console.Write($"\x1b[7m{(idx < CurrentInput.Length ? CurrentInput[idx] : ' ')}\x1b[27m");
            }

            string pad_full = new(' ', input_area_width);

            if (Suggestions.Count > 0)
            {
                if (CurrentSuggestionIndex > Suggestions.Count)
                    CurrentSuggestionIndex = Suggestions.Count - 1;
                else if (CurrentSuggestionIndex < 0)
                    CurrentSuggestionIndex = 0;

                int start_index = CurrentSuggestionIndex < MAX_SUGGESTIONS / 2 ? 0
                                : CurrentSuggestionIndex > Suggestions.Count - MAX_SUGGESTIONS / 2 ? Suggestions.Count - MAX_SUGGESTIONS
                                : CurrentSuggestionIndex - MAX_SUGGESTIONS / 2;

                start_index = Math.Max(0, Math.Min(start_index, Suggestions.Count - MAX_SUGGESTIONS));

                ScriptToken[][] suggestions = Suggestions.Skip(start_index).Take(MAX_SUGGESTIONS).ToArray(s => s.Display);

                int sugg_width = suggestions.Select(s => s.Sum(t => t.TokenLength)).Append(0).Max() + 2;
                int sugg_left = Math.Min(input_area_width - 2 - sugg_width, cursor_pos_x + 3);
                int i = 0;

                ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
                Console.CursorTop = cursor.t + 2 + i;
                Console.CursorLeft = 0;

                string pad_left = new(' ', sugg_left);
                string pad_right = new(' ', input_area_width - 2 - sugg_left - sugg_width);

                Console.Write(pad_left + '┌' + new string('─', sugg_width) + '┐' + pad_right);
                Console.CursorLeft = 0;
                Console.CursorTop = cursor.t + 1;

                string indicator = $" {CurrentSuggestionIndex + 1}/{Suggestions.Count} ";

                if (input_area_width - 4 - cursor_pos_x - indicator.Length > 0)
                    indicator = new string(' ', 3 + cursor_pos_x) + '│' + indicator;
                else
                    indicator = new string(' ', cursor_pos_x - indicator.Length + 3) + indicator + '│';

                Console.Write(indicator);

                if (Console.CursorLeft < input_area_width - 1)
                    Console.Write(new string(' ', input_area_width - Console.CursorLeft - 1));

                Console.CursorTop++;
                Console.CursorLeft = 3 + cursor_pos_x;
                Console.Write(cursor_pos_x + 3 == sugg_left ? '├' : cursor_pos_x + 2 == sugg_left + sugg_width ? '┤' : '┴');

                foreach (ScriptToken[] suggestion in suggestions)
                {
                    Console.CursorTop = cursor.t + 3 + i;
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
                Console.CursorTop = cursor.t + 3 + i;
                Console.Write(pad_left + '└' + new string('─', sugg_width) + '┘' + pad_right);

                while (i < MAX_SUGGESTIONS)
                {
                    Console.CursorTop = cursor.t + 4 + i;
                    Console.CursorLeft = 0;
                    Console.Write(pad_full);

                    ++i;
                }
            }
            else
                for (int i = 1; i < MAX_SUGGESTIONS + 4; ++i)
                {
                    Console.CursorTop = cursor.t + i;
                    Console.CursorLeft = 0;
                    Console.Write(pad_full);
                }

            return (cursor.l, cursor.t, input_area_height);
        }

        private void RedrawHistoryArea(int input_area_y)
        {
            int width = WIDTH;
            string[] history = History.SelectMany(entry =>
            {
                List<string> lines = new();
                string line = COLOR_PROMPT.ToVT100ForegroundString() + (entry.Stream is InteractiveShellStreamDirection.Input ? " > " : "");
                int line_width = width - MARGIN_RIGHT - (entry.Stream is InteractiveShellStreamDirection.Input ? 4 : 1);
                int len = 0;

                foreach (ScriptToken token in entry.Content.SelectMany(c => c.SplitByLineBreaks()))
                    if (token.Type is TokenType.NewLine)
                    {
                        lines.Add(line);
                        line = "";
                        len = 0;
                    }
                    else if (len + token.TokenLength > line_width)
                        foreach (string partial in token.Content.PartitionByArraySize(line_width).ToArray(cs => new string(cs)))
                        {
                            if (line.Length > (entry.Stream is InteractiveShellStreamDirection.Input ? 3 : 0))
                                lines.Add(line);

                            line = (entry.Stream is InteractiveShellStreamDirection.Input ? "   " : "")
                                 + ScriptToken.FromString(partial, token.Type).ConvertToVT100(false);
                            len = token.TokenLength;
                        }
                    else
                    {
                        line += token.ConvertToVT100(false);
                        len += token.TokenLength;
                    }

                lines.Add(line);

                return lines;
            }).Where(line => !string.IsNullOrEmpty(line)).ToArray();

            int height = input_area_y - MARGIN_TOP - 2;

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

                Console.Write(new string(' ', width - MARGIN_RIGHT - 1 - Console.CursorLeft));
            }
        }

        private void RedrawThreadAndVariableWatchers()
        {
            AU3Thread[] threads = Interpreter.Threads;
            int left = WIDTH - MARGIN_RIGHT;
            int top = MARGIN_TOP + 1;
            StringBuilder sb = new();

            sb.AppendLine($"Threads ({threads.Length}):");

            foreach (AU3Thread thread in threads)
            {
                sb.AppendLine($" - Thread {thread.ThreadID} (0x{thread.ThreadID:x8})");
                sb.AppendLine($"   Status: {(thread.IsRunning ? "Active" : "Paused/Stopped/Interactive")} {(thread.IsMainThread ? " (Main)" : "")}");
                sb.AppendLine($"   Stack frames ({thread.CallStack.Length}):   <TODO>");

                //sb.AppendLine($"  Func: {thread.CurrentFunction}");
            }

            ConsoleExtensions.RGBForegroundColor = COLOR_PROMPT;
            ConsoleExtensions.WriteBlock(sb.ToString(), left, top, MARGIN_RIGHT, MARGIN_TOP);

            top = Console.CursorTop + 1;

            ConsoleExtensions.RGBForegroundColor = COLOR_SEPARATOR;
            ConsoleExtensions.Write('├' + new string('─', MARGIN_RIGHT), left - 1, top);
            ++top;

            string[] variables = Variables.LocalVariables
                                .Concat(Interpreter.VariableResolver.GlobalVariables)
                                .Select(v => ScriptVisualizer.ConvertToVT100(ScriptVisualizer.TokenizeScript($"${v.Name} = {v.Value.ToDebugString(Interpreter)}"), false))
                                .Take(HEIGHT - top - 2)
                                .ToArray();

            ConsoleExtensions.RGBForegroundColor = COLOR_PROMPT;
            ConsoleExtensions.Write($"Variables ({variables.Length}):", left, top);

            foreach (string line in variables)
            {
                ++top;

                Console.SetCursorPosition(left, top);

                foreach (char c in line)
                    if (Console.CursorLeft < WIDTH - 4)
                        Console.Write(c);
                    else
                    {
                        ConsoleExtensions.RGBForegroundColor = COLOR_PROMPT;
                        Console.Write(" ...");

                        break;
                    }

                if (Console.CursorLeft < WIDTH)
                    Console.Write(new string(' ', WIDTH - Console.CursorLeft));
            }

            //while (top < HEIGHT)

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
                else if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    History.Clear();
                    HistoryScrollIndex = 0;
                }
                else
                    try
                    {
                        CallFrame.InsertReplaceSourceCode(CallFrame.CurrentInstructionPointer, input);
                        Thread.UnsafeSetIsRunning(true);

                        FunctionReturnValue result = CallFrame.ParseCurrentLine();

                        Thread.UnsafeSetIsRunning(false);

                        if (result.IsFatal(out InterpreterError? error))
                            History.Add((new[] { ScriptToken.FromString(error.Message, TokenType.UNKNOWN) }, InteractiveShellStreamDirection.Error));

                        result.IfNonFatal((value, error, extended) =>
                        {
                            List<ScriptToken> tokens = new()
                            {
                                ScriptToken.FromString(value.ToDebugString(Interpreter), TokenType.DirectiveOption),
                            };

                            if (error is int err)
                            {
                                tokens.Add(ScriptToken.FromString("\n", TokenType.NewLine));
                                tokens.Add(ScriptToken.FromString("@error", TokenType.Macro));
                                tokens.Add(ScriptToken.FromString($": {err} (0x{err:x8})", TokenType.DirectiveOption));
                            }

                            if (extended is Variant ext)
                            {
                                tokens.Add(ScriptToken.FromString("\n", TokenType.NewLine));
                                tokens.Add(ScriptToken.FromString("@extended", TokenType.Macro));
                                tokens.Add(ScriptToken.FromString($": {ext.ToDebugString(Interpreter)}", TokenType.DirectiveOption));
                            }

                            History.Add((tokens.ToArray(), InteractiveShellStreamDirection.Output));

                            return value;
                        });
                    }
                    catch
                    {
                    }
            }
        }

        public void UpdateSuggestions()
        {
            OS os = NativeInterop.OperatingSystem;
            ScriptToken? curr_token = CurrentlyTypedToken;
            string? filter = curr_token?.Content[..(CurrentCursorPosition.GetOffset(CurrentInput.Length) - curr_token.CharIndex)];

            if (string.IsNullOrWhiteSpace(filter))
                filter = null;

            List<(ScriptToken[] tokens, string content)> suggestions = new()
            {
                (new[] { ScriptToken.FromString("CLEAR", TokenType.Keyword) }, "clear"),
            };

            void add_suggs(IEnumerable<string> suggs, TokenType type) => suggs.Select(s => (new[] { ScriptToken.FromString(s, type) }, s)).AppendToList(suggestions);
            bool suggest_all = string.IsNullOrEmpty(CurrentInput) || curr_token?.Type is TokenType.UNKNOWN or TokenType.Whitespace or TokenType.NewLine;
            string to_dbg_str(Variant value)
            {
                string str = value.ToDebugString(Interpreter);

                return str.Length > WIDTH - MARGIN_RIGHT - 30 ? str[..(WIDTH - MARGIN_RIGHT - 33)] + " ..." : str;
            }

            // if (suggest_all || curr_token?.Type is TokenType.Directive)
            //     ; // TODO
            //
            // if (suggest_all || curr_token?.Type is TokenType.DirectiveOption)
            //     ; // TODO

            if (suggest_all || curr_token?.Type is TokenType.Keyword or TokenType.Identifier or TokenType.FunctionCall)
                add_suggs(ScriptFunction.RESERVED_NAMES.Except(new[] { "_", "$_", "$GLOBAL" }), TokenType.Keyword);

            if (suggest_all || curr_token?.Type is TokenType.Operator)
                add_suggs(KNOWN_OPERATORS, TokenType.Operator);

            if (suggest_all || curr_token?.Type is TokenType.Macro)
                KNOWN_MACROS.Select(macro =>
                {
                    if (CallFrame.TryFetchMacroValue(macro, out Variant? value) && value is Variant v)
                        return (ScriptVisualizer.TokenizeScript($"{macro.PadRight(KNOWN_MACROS.Max(m => m.Length))} : {to_dbg_str(v)}"), macro);
                    else
                        return (new[] { ScriptToken.FromString(macro, TokenType.Macro) }, macro);
                }).AppendToList(suggestions);

            if (suggest_all || curr_token?.Type is TokenType.Variable)
            {
                Variable[] vars = Variables.LocalVariables.Concat(Interpreter.VariableResolver.GlobalVariables).ToArray();
                int name_length = vars.Max(v => v.Name.Length);

                vars.Select(variable =>
                {
                    string name = '$' + variable.Name.PadRight(name_length);
                    string type = variable.Value.Type.ToString().PadRight(9);
                    string value = to_dbg_str(variable.Value);
                    IEnumerable<ScriptToken> tokens = new[] {
                        ScriptToken.FromString(name, TokenType.Variable),
                        ScriptToken.FromString(" : ", TokenType.Operator),
                        ScriptToken.FromString(type, TokenType.Identifier),
                        ScriptToken.FromString(" = ", TokenType.Operator),
                    };

                    if (variable.IsConst)
                        tokens = tokens.Append(ScriptToken.FromString("CONST ", TokenType.Keyword));

                    return (tokens.Concat(ScriptVisualizer.TokenizeScript(value)).ToArray(), name);
                }).AppendToList(suggestions);
            }

            if (suggest_all || curr_token?.Type is TokenType.Identifier or TokenType.FunctionCall)
            {
                ScriptFunction[] functions = Interpreter.ScriptScanner.CachedFunctions.Where(f => !string.IsNullOrWhiteSpace(f.Name)).ToArray();
                int name_length = functions.Max(f => f.Name.Length);

                functions.Select(function =>
                {
                    bool supported = function.Metadata.SupportedPlatforms.HasFlag(os);

                    if (supported)
                        return (ScriptVisualizer.TokenizeScript(
                            $"{function.Name.PadRight(name_length)} ({Interpreter.CurrentUILanguage["interactive.argument_count", function.ParameterCount.MinimumCount, function.ParameterCount.MaximumCount]})"
                        ), function.Name);
                    else
                        return (new[] { ScriptToken.FromString($"{Interpreter.CurrentUILanguage["interactive.unsupported_platform", os]} {function.Name}", TokenType.UNKNOWN) }, function.Name);
                }).AppendToList(suggestions);
            }

            Suggestions.Clear();
            Suggestions.AddRange(from s in suggestions.Distinctby(s => s.content)
                                 where filter is null || s.content.StartsWith(filter, StringComparison.InvariantCultureIgnoreCase)
                                 orderby s.tokens[0].Type, s.content ascending
                                 select s);
        }

        public void SubmitPrint(string message) => History.Add((new[] { ScriptToken.FromString(message, TokenType.Comment) }, InteractiveShellStreamDirection.Output));
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
 ╰╯└┴┘
 *      +----------------------------------+---------------+
 *      | help text                        |  variable     |
 *      +--------------------------------+-+  monitor (?)  |
 *      |                                |^|               |
 *      |     ^ ^ ^ ^ ^ ^ ^ ^ ^ ^ ^      | | thread monitor|
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
