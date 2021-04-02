using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

using Piglet.Parser.Configuration.Generic;
using Piglet.Parser.Configuration;
using Piglet.Parser;
using Piglet.Lexer.Construction;
using Piglet.Lexer;

using Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework;
using Unknown6656.AutoIt3.Extensibility.Plugins.Internals;
using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.CLI;

using Unknown6656.Common;

using static Unknown6656.AutoIt3.Parser.ExpressionParser.AST;

namespace Unknown6656.AutoIt3.Runtime
{
    // TODO : covariant return for 'CurrentFunction'

    /// <summary>
    /// Represents an abstract call frame inside the call stack of an <see cref="AU3Thread"/>.
    /// </summary>
    public abstract class CallFrame
        : IDisposable
    {
        /// <summary>
        /// The thread associated with the current call frame.
        /// </summary>
        public AU3Thread CurrentThread { get; }

        /// <summary>
        /// The currently executed function.
        /// </summary>
        public virtual ScriptFunction CurrentFunction { get; }

        /// <summary>
        /// The variable scope associated with the current call frame.
        /// </summary>
        public VariableScope VariableResolver { get; }

        /// <summary>
        /// The raw array of arguments passed to this call frame.
        /// 'Raw' in this context means that the array is not filled with potential default values.
        /// </summary>
        public Variant[] PassedArguments { get; }

        /// <summary>
        /// The caller frame which called/created the current frame.
        /// This value is <see langword="null"/> if the current call frame is the bottom-most frame of the execution stack.
        /// </summary>
        public CallFrame? CallerFrame { get; }

        /// <summary>
        /// Gets or sets the return value of the current call frame. The default return value is <see cref="Variant.Zero"/>.
        /// </summary>
        public Variant ReturnValue { protected set; get; } = Variant.Zero;

        /// <summary>
        /// The current <see cref="SourceLocation"/> associated with the current function execution.
        /// This value usually represents the location of the currently executed source code line.
        /// </summary>
        public virtual SourceLocation CurrentLocation => CurrentThread.CurrentLocation ?? CurrentFunction.Location;

        /// <summary>
        /// The interpreter associated with the current call frame.
        /// </summary>
        public Interpreter Interpreter => CurrentThread.Interpreter;


        internal CallFrame(AU3Thread thread, CallFrame? caller, ScriptFunction function, Variant[] args)
        {
            CurrentThread = thread;
            CallerFrame = caller;
            CurrentFunction = function;
            PassedArguments = args;

            // TODO : the following line is wrong - it should be interpreter as parent, not the previous frame
            VariableResolver = function.IsMainFunction ? thread.CurrentVariableResolver : thread.CurrentVariableResolver.CreateChildScope(this);
        }

        public void Dispose()
        {
            if (!VariableResolver.IsGlobalScope)
                VariableResolver.Dispose();
        }

        /// <summary>
        /// Executes the code stored inside the current call frame with the given arguments.
        /// </summary>
        /// <param name="args">Arguments to passed to the internal code execution logic. 
        /// These are not in their raw form (unlike <see cref="PassedArguments"/>), but are padded with the potential optional parameter values.</param>
        /// <returns>The return value of the code execution. This data may also contain fatal execution errors.</returns>
        protected abstract FunctionReturnValue InternalExec(Variant[] args);

        internal FunctionReturnValue Execute(Variant[] args)
        {
            ScannedScript script = CurrentFunction.Script;

            SetError(0, 0);

            (int min_argc, int max_argc) = CurrentFunction.ParameterCount;

            if (args.Length < min_argc)
                return InterpreterError.WellKnown(CurrentThread.CurrentLocation, "error.not_enough_args", CurrentFunction.Name, min_argc, args.Length);
            else if (args.Length > max_argc)
                return InterpreterError.WellKnown(CurrentThread.CurrentLocation, "error.too_many_args", CurrentFunction.Name, max_argc, args.Length);
            else if (!CurrentFunction.Metadata.SupportedPlatforms.HasFlag(NativeInterop.OperatingSystem))
                return InterpreterError.WellKnown(
                    CurrentLocation,
                    "error.unsupported_platform",
                    CurrentFunction.Name,
                    NativeInterop.OperatingSystem,
                    new[] { OS.Windows, OS.Linux, OS.MacOS }.Where(os => CurrentFunction.Metadata.SupportedPlatforms.HasFlag(os)).StringJoin("', '")
                );
            else
            {
                FunctionReturnValue result = Variant.True;

                if (CurrentFunction.IsMainFunction)
                    result = Interpreter.Telemetry.Measure(TelemetryCategory.OnAutoItStart, () => script.LoadScript(this));

                if (result.IsNonFatal(out _, out _, out _))
                    if (CurrentThread.IsRunning)
                    {
                        if (CurrentFunction.Metadata.IsDeprecated)
                            IssueWarning("warning.deprecated_function", CurrentFunction.Name);

                        MainProgram.PrintfDebugMessage("debug.au3thread.executing", CurrentFunction);

                        result = Interpreter.Telemetry.Measure(TelemetryCategory.ScriptExecution, () => InternalExec(args));
                    }
                    else
                        result = Variant.False;

                if (CurrentFunction.IsMainFunction && result.IsNonFatal(out _, out _, out _))
                    if (Interpreter.Telemetry.Measure(TelemetryCategory.OnAutoItExit, () => script.UnLoadScript(this)).IsFatal(out InterpreterError? error))
                        result = error;

                if (result.IsNonFatal(out Variant ret, out _, out _))
                    ReturnValue = ret;

                return result;
            };
        }

        /// <summary>
        /// <b>[UNSAFE!]</b>
        /// Invokes the given <paramref name="ScriptFunction"/> with the given arguments. A call to this function is considered to be unsafe, as any non-concurrent call may result into undefined behavior.
        /// This method is intended to only be called from <i>inside<i/> a call frame.
        /// <para/>
        /// This function is blocking and returns only after the given function has been invoked.
        /// </summary>
        /// <param name="function">The function to be invoked.</param>
        /// <param name="args">The arguments to be passed to the function.</param>
        /// <returns>The functions return value or execution error.</returns>
        public FunctionReturnValue Call(ScriptFunction function, Variant[] args) => CurrentThread.Call(function, args, (this as AU3CallFrame)?.InterpreterRunContext ?? InterpreterRunContext.Regular);

        public Variant SetError(int error, Variant? extended = null, in Variant @return = default)
        {
            Interpreter.ErrorCode = error;

            return SetExtended(extended, in @return);
        }

        public Variant SetExtended(Variant? extended, in Variant @return = default)
        {
            Interpreter.ExtendedValue = extended ?? Variant.Null;

            return @return;
        }

        /// <summary>
        /// Prints the given value to the standard console output stream.
        /// A line-break will only be appended if the interpreter is launched with a verbosity higher than <see cref="Verbosity.q"/>.
        /// <para/>
        /// Use this function if you intend to simulate printing behavior from inside an AutoIt script.
        /// <br/>
        /// Do not use <see cref="Console"/>.Write[...] as they do not handle multi-threading very well.
        /// </summary>
        /// <param name="value">The value to be printed.</param>
        public void Print(Variant value) => Interpreter.Print(this, value);

        /// <summary>
        /// Prints the given value to the standard console output stream.
        /// A line-break will only be appended if the interpreter is launched with a verbosity higher than <see cref="Verbosity.q"/>.
        /// <para/>
        /// Use this function if you intend to simulate printing behavior from inside an AutoIt script.
        /// <br/>
        /// Do not use <see cref="Console"/>.Write[...] as they do not handle multi-threading very well.
        /// </summary>
        /// <param name="value">The value to be printed.</param>
        public void Print(object? value) => Interpreter.Print(this, value);

        public override string ToString() => $"[0x{CurrentThread.ThreadID:x4}]";

        internal void IssueWarning(string key, params object?[] args) => MainProgram.PrintWarning(CurrentLocation, Interpreter.CurrentUILanguage[key, args]);
    }

    /// <summary>
    /// Represents a call frame for native code executions (e.g. framework or interop functions).
    /// This kind of call frames is used when a <see cref="NativeFunction"/> gets executed on the call stack of an <see cref="AU3Thread"/>.
    /// </summary>
    public sealed class NativeCallFrame
        : CallFrame
    {
        public override NativeFunction CurrentFunction { get; }


        internal NativeCallFrame(AU3Thread thread, CallFrame? caller, NativeFunction function, Variant[] args)
            : base(thread, caller, function, args) => CurrentFunction = function;

        /// <inheritdoc/>
        protected override FunctionReturnValue InternalExec(Variant[] args)
        {
            NativeFunction native = (NativeFunction)CurrentFunction;
            FunctionReturnValue result = Interpreter.Telemetry.Measure(TelemetryCategory.NativeScriptExecution, () => native.Execute(this, args));
            Variant? extended = null;
            int error = 0;

            if (result.IsSuccess(out Variant variant, out extended) || result.IsError(out variant, out error, out extended))
                SetError(error, extended, in variant);

            return result;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{base.ToString()} native call frame";
    }

    /// <summary>
    /// Represents a call frame for non-native code executions (regular AutoIt3 script code executions).
    /// This kind of call frames is used when a <see cref="AU3Function"/> gets executed on the call stack of an <see cref="AU3Thread"/>.
    /// </summary>
    public sealed class AU3CallFrame
        : CallFrame
    {
        private const RegexOptions _REGEX_OPTIONS = RegexOptions.IgnoreCase | RegexOptions.Compiled;
        private static readonly Regex REGEX_INTERNAL_LABEL = new(@"^§\w+$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_VARIABLE = new(@"\$([^\W\d]|[^\W\d]\w*)\b", _REGEX_OPTIONS);
        private static readonly Regex REGEX_GOTO = new(@"^goto\s+(?<label>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_WHILE = new(@"^while\s+(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_WEND = new(@"^wend$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_NEXT = new(@"^next$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_CLEAR = new(@"^clear$", _REGEX_OPTIONS);
        internal static readonly Regex REGEX_EXIT = new(@"^exit(\b\s*(?<code>.+))?$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_RETURN = new(@"^return(\b\s*(?<value>.+))?$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_DELETE = new(@"^delete\b\s*(?<value>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_FOR = new(@"^for\s+.+$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_FORTO = new(@"^for\s+(?<start>.+)\s+to\s+(?<stop>.+?)(\s+step\s+(?<step>.+))?$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_FORIN = new(@"^for\s+(?<variable>.+)\s+in\s+(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_FORIN_VARIABLES = new(@"^\$(?<key>[^\W\d]|[^\W\d]\w*)\s*,\s*\$(?<value>[^\W\d]|[^\W\d]\w*)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_WITH = new(@"^with\s+(?<variable>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDWITH = new(@"^endwith$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_REDIM = new(@"^redim\s+(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_DO = new(@"^do$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_UNTIL = new(@"^until\s+(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_IF = new(@"^(?<elif>else)?if\s+(?<condition>.+)\s+then$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ELSE = new(@"^else$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDIF = new(@"^endif$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_DECLARATION_MODIFIER = new(@"^(local|static|global|const|dim|enum|step)\b", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENUM_STEP = new(@"^(?<op>[+\-*]?)(?<step>\d+)\b", _REGEX_OPTIONS);
        private static readonly Regex REGEX_CONTINUELOOP_EXITLOOP = new(@"^(?<mode>continue|exit)loop\s*(?<level>.+)?\s*$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_SELECT = new(@"^select$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDSELECT = new(@"^endselect$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_SWITCH = new(@"^switch\b\s*(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDSWITCH = new(@"^endswitch$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_CASE = new(@"^case\b\s*(?<expression>.+)*$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_CONTINUECASE = new(@"^continuecase$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_DIRECTIVE = new(@"^#(?<directive>\w+)($|(\b|\s)\s*(?<arguments>.*))", _REGEX_OPTIONS);
        private static readonly Regex REGEX_INCLUDE = new(@"^(?<open>[""'<])(?<path>(?:(?!\k<close>).)+)(?<close>[""'>])$", _REGEX_OPTIONS);

        private readonly ConcurrentDictionary<string, IEnumerator<(Variant key, Variant value)>> _iterators = new();
        private readonly ConcurrentStack<Variable> _withcontext_stack = new();
        private readonly ConcurrentStack<bool> _if_stack = new();
        private readonly ConcurrentStack<string> _while_stack = new();
        private readonly ConcurrentStack<(Variant? switch_expression, bool case_handled)> _switchselect_stack = new();

        private volatile int _instruction_pointer = 0;
        private readonly List<(SourceLocation LineLocation, string LineContent)> _line_cache;


        public override AU3Function CurrentFunction { get; }

        public FunctionReturnValue LastStatementValue { get; private set; }

        public InterpreterRunContext InterpreterRunContext { get; }

        /// <summary>
        /// The current instruction pointer.
        /// <para/>
        /// This value does not coincide with the line number of the source code file. Use <see cref="CurrentLocation"/> for that information instead.
        /// </summary>
        public int CurrentInstructionPointer => _instruction_pointer;

        /// <summary>
        /// The current cache of ordered source code lines and their location.
        /// </summary>
        public (SourceLocation LineLocation, string LineContent)[] CurrentLineCache => _line_cache.ToArray();

        public override SourceLocation CurrentLocation => _instruction_pointer < 0 || _instruction_pointer >= _line_cache.Count ? CurrentFunction.Location : _line_cache[_instruction_pointer].LineLocation;

        /// <summary>
        /// Returns the raw string content of the currently executed source code line.
        /// </summary>
        public string CurrentLineContent => _instruction_pointer < 0 ? '<' + Interpreter.CurrentUILanguage["general.unknown"] + '>' : _line_cache[_instruction_pointer].LineContent;

        /// <summary>
        /// A dictionary of internally used jump label indices.
        /// </summary>
        public Dictionary<string, int> InternalJumpLabels => _line_cache.WithIndex().Where(l => REGEX_INTERNAL_LABEL.IsMatch(l.Item.LineContent)).ToDictionary(l => l.Item.LineContent, l => l.Index);


        internal AU3CallFrame(AU3Thread thread, CallFrame? caller, AU3Function function, Variant[] args, InterpreterRunContext context)
            : base(thread, caller, function, args)
        {
            LastStatementValue = (caller as AU3CallFrame)?.LastStatementValue ?? Variant.Null;
            CurrentFunction = function;
            InterpreterRunContext = context;
            _line_cache = function.Lines.ToList();
            _instruction_pointer = 0;
        }

        public override string ToString() => $"{base.ToString()} {CurrentLocation}";

        protected override FunctionReturnValue InternalExec(Variant[] args)
        {
            _instruction_pointer = -1;

            int argc = CurrentFunction.ParameterCount.MaximumCount;
            int len = args.Length;

            if (len < argc)
                Array.Resize(ref args, argc);

            FunctionReturnValue? return_value = null;

            if (!(CurrentFunction.IsCached && Interpreter.FunctionCache.TryFetch(CurrentFunction, args, out return_value)))
            {
                for (int i = 0; i < argc; ++i)
                {
                    PARAMETER_DECLARATION param = CurrentFunction.Parameters[i];
                    Variable param_var = VariableResolver.CreateVariable(CurrentFunction.Location, param.Variable.Name, param.IsConst);

                    if (i < len)
                    {
                        if (param.IsByRef && args[i].AssignedTo is Variable existing)
                            if (existing.IsConst)
                                return WellKnownError("error.constant_passed_as_ref", existing, i + 1);
                            else
                                args[i] = Variant.FromReference(existing);
                    }
                    else if (param.DefaultValue is null)
                        return WellKnownError("error.missing_argument", i + 1, param.Variable);
                    else
                    {
                        EXPRESSION expr = param.DefaultValue.Value;
                        FunctionReturnValue result = ProcessExpression(expr);

                        if (result.IfNonFatal(value => args[i] = value).IsFatal(out InterpreterError? error))
                            return error;
                    }

                    param_var.Value = args[i];
                }

                return_value = Interpreter.Telemetry.Measure(TelemetryCategory.Au3ScriptExecution, delegate
                {
                    _instruction_pointer = 0;

                    while (_instruction_pointer < _line_cache.Count && CurrentThread.IsRunning)
                        if (ParseCurrentLine().IsFatal(out InterpreterError? error))
                            return error;
                        else if (!MoveNext())
                            break;

                    if (InterpreterRunContext == InterpreterRunContext.Regular)
                        return ReturnValue;
                    else
                        return LastStatementValue;
                });
            }

            if (CurrentFunction.IsCached)
                Interpreter.FunctionCache.SetOrUpdate(CurrentFunction, args, return_value);

            return return_value;
        }

        private bool MoveNext()
        {
            if (_instruction_pointer < _line_cache.Count)
            {
                ++_instruction_pointer;

                return true;
            }
            else
                return false;
        }

        private bool MoveAfter(SourceLocation closest_location)
        {
            _instruction_pointer = (from ln in _line_cache.WithIndex()
                                    let loc = ln.Item.LineLocation
                                    where loc >= closest_location
                                    orderby loc ascending
                                    select ln.Index).FirstOrDefault();

            return _instruction_pointer < _line_cache.Count;
        }

        private bool MoveBefore(SourceLocation closest_location)
        {
            MoveAfter(closest_location);
            --_instruction_pointer;

            return _instruction_pointer < _line_cache.Count;
        }

        private bool MoveTo(JumpLabel jump_label) => MoveAfter(jump_label.Location);

        private FunctionReturnValue MoveTo(string jump_label)
        {
            if (InternalJumpLabels.TryGetValue(jump_label, out int index))
            {
                _instruction_pointer = index;

                return Variant.True;
            }
            else if (CurrentFunction.JumpLabels[jump_label] is JumpLabel label)
                return Variant.FromBoolean(MoveTo(label));

            return WellKnownError("error.unknown_jumplabel", jump_label);
        }

        /// <summary>
        /// Copies the given value into the <see cref="CallFrame.ReturnValue"/>-field, and moves the instruction pointer to the end.
        /// </summary>
        /// <param name="value">Return value</param>
        public Variant Return(Variant value)
        {
            _instruction_pointer = _line_cache.Count;
            ReturnValue = value;

            return value;
        }

        private bool RemoveInternalJumpLabel(string jump_label)
        {
            if (REGEX_INTERNAL_LABEL.IsMatch(jump_label))
                for (int i = 0; i < _line_cache.Count; i++)
                    if (jump_label.Equals(_line_cache[i].LineContent))
                    {
                        _line_cache.RemoveAt(i);

                        return true;
                    }

            return false;
        }

        private string InsertInternalJumpLabel()
        {
            string name = $"§{Guid.NewGuid():N}";

            if (_instruction_pointer < _line_cache.Count)
                _line_cache.Insert(_instruction_pointer, (CurrentLocation, name));
            else
                _line_cache.Add((SourceLocation.Unknown, name));

            ++_instruction_pointer;

            return name;
        }

        internal void InsertReplaceSourceCode(int instruction_ptr, params string[] lines)
        {
            int eip = _instruction_pointer;

            _instruction_pointer = instruction_ptr;

            if (lines.Length == 1)
            {
                if (_instruction_pointer < _line_cache.Count)
                    _line_cache[_instruction_pointer] = (CurrentLocation, lines[0]);
                else
                    _line_cache.Add((SourceLocation.Unknown, lines[0]));
            }
            else
            {
                _line_cache.RemoveAt(_instruction_pointer);

                if (_instruction_pointer < _line_cache.Count)
                    _line_cache.InsertRange(_instruction_pointer, lines.Select(l => (CurrentLocation, l)));
                else
                    _line_cache.AddRange(lines.Select(l => (SourceLocation.Unknown, l)));
            }

            _instruction_pointer = eip;
        }

        /// <summary>
        /// Parses the current line and returns the execution result without moving the current instruction pointer (except when processing loops, branches, or explicit jump instructions).
        /// </summary>
        /// <returns>Execution result. A value of <see langword="null"/> represents that the line might not have been processed. However, this does <b>not</b> imply a fatal execution error.</returns>
        public FunctionReturnValue ParseCurrentLine()
        {
            (SourceLocation loc, string line) = _line_cache[_instruction_pointer];

            line = line.Trim();

            if (Interpreter.CommandLineOptions.Verbose)
            {
                ScriptToken[] tokens = ScriptVisualizer.TokenizeScript(line);

                MainProgram.PrintDebugMessage("-----------------------------------------------------------------------------------------------");
                MainProgram.PrintfDebugMessage("debug.au3thread.location", loc);
                MainProgram.PrintfDebugMessage("debug.au3thread.content", tokens.ConvertToVT100(false));
            }

            if (string.IsNullOrEmpty(line) || REGEX_INTERNAL_LABEL.IsMatch(line))
                return Variant.Zero;

            FunctionReturnValue? result = null;

            Interpreter.Telemetry.Measure(TelemetryCategory.ProcessLine, delegate
            {
                void TryDo(Func<string, FunctionReturnValue?> func) => result ??= func(line);

                TryDo(ProcessDirective);
                TryDo(ProcessStatement);
                TryDo(UseExternalLineProcessors);
                TryDo(ProcessExpressionStatement);
            });

            LastStatementValue = result ?? Variant.Null;

            if (Interpreter.CommandLineOptions.IgnoreErrors && result is { } && result.IsFatal(out InterpreterError? error))
            {
                MainProgram.PrintWarning(CurrentLocation, error.Message);

                return Variant.False;
            }
            else
                return LastStatementValue ?? Variant.True;
        }

        private FunctionReturnValue? ProcessDirective(string directive)
        {
            if (directive.Match(REGEX_DIRECTIVE, out Match match))
            {
                directive = match.Groups["directive"].Value;

                string arguments = match.Groups["arguments"].Value;
                FunctionReturnValue? result = Interpreter.Telemetry.Measure(TelemetryCategory.ProcessDirective, delegate
                {
                    if (arguments.Match(REGEX_INCLUDE, out ReadOnlyIndexer<string, string>? groups))
                    {
                        char open = groups["open"][0];
                        char close = groups["close"][0];
                        bool relative = open != '<';

                        if (open != close && open != '<' && close != '>')
                            return WellKnownError("error.mismatched_quotes", open, close);

                        return Interpreter.ScriptScanner.ScanScriptFile(CurrentLocation, groups["path"], relative).Match(FunctionReturnValue.Fatal, script =>
                        {
                            ScannedScript[] active = Interpreter.ScriptScanner.ActiveScripts;

                            if (active.Contains(script))
                            {
                                if (script.IncludeOnlyOnce)
                                    return Variant.True;
                                else
                                    return WellKnownError("error.circular_include", script.Location.FullName);
                            }
                            else
                                return Call(script.MainFunction, Array.Empty<Variant>());
                        });
                    }
                    else
                        return null;
                });

                foreach (AbstractDirectiveProcessor proc in Interpreter.PluginLoader.DirectiveProcessors)
                    if (result is null)
                        result = Interpreter.Telemetry.Measure(TelemetryCategory.ProcessDirective, () => proc.TryProcessDirective(this, directive, arguments));
                    else
                        break;

                if (result is null)
                    IssueWarning("warning.unparsable_dirctive", directive);

                return result ?? Variant.False;
            }
            else
                return null;
        }

        private FunctionReturnValue MoveToEndOf(BlockStatementType type)
        {
            SourceLocation init = CurrentLocation;
            int depth = 1;

            if (type is BlockStatementType.If)
                while (MoveNext())
                {
                    string line = CurrentLineContent.Trim();
                    bool @if = false;

                    if (line.Match(REGEX_IF, out Match m))
                    {
                        @if = true;

                        if (m.Groups["elif"].Length > 0)
                            --depth;
                        else
                            ++depth;
                    }
                    else if (REGEX_ELSE.IsMatch(line))
                        --depth;
                    else if (REGEX_ENDIF.IsMatch(line))
                        --depth;

                    if (depth == 0)
                    {
                        if (@if)
                            --_instruction_pointer;

                        break;
                    }
                }
            else if (type is BlockStatementType.While or BlockStatementType.Select or BlockStatementType.Switch)
            {
                (Regex start, Regex end) = type switch
                {
                    BlockStatementType.Select => (REGEX_SELECT, REGEX_ENDSELECT),
                    BlockStatementType.Switch => (REGEX_SWITCH, REGEX_ENDSWITCH),
                    BlockStatementType.While => (REGEX_WHILE, REGEX_WEND),
                    _ => throw new ArgumentOutOfRangeException(nameof(type)),
                };

                while (MoveNext())
                {
                    string line = CurrentLineContent.Trim();

                    if (end.IsMatch(line))
                        --depth;
                    else if (type is not BlockStatementType.While && REGEX_CASE.IsMatch(line))
                        --depth;
                    else if (start.IsMatch(line))
                        ++depth;

                    if (depth == 0)
                        break;
                }
            }

            // TODO : other block types

            else
                return WellKnownError("error.not_yet_implemented", type);

            return depth == 0 ? (FunctionReturnValue)Variant.True : WellKnownError("error.no_matching_close", type, init);
        }

        private FunctionReturnValue? ProcessStatement(string line) => Interpreter.Telemetry.Measure(TelemetryCategory.ProcessStatement, delegate
        {
            FunctionReturnValue? result = line.Match(null!, new Dictionary<Regex, Func<Match, FunctionReturnValue>>
            {
                [REGEX_CLEAR] = _ =>
                {
                    if (InteractiveShell.Instances is { Count: > 0 } shells)
                    {
                        shells.Do(i => i.Clear());

                        return Variant.Zero;
                    }
                    else
                        return WellKnownError("error.unexpected_clear");
                },
                [REGEX_EXIT] = m =>
                {
                    string code = m.Groups["code"].Value;

                    if (string.IsNullOrWhiteSpace(code))
                        code = "0";

                    FunctionReturnValue result = ProcessAsVariant(code);

                    if (InteractiveShell.Instances is { Count: > 0 } shells)
                        shells.Do(i => i.Exit());

                    Interpreter.ExitMethod = InterpreterExitMethod.ByExit;

                    return result.IfNonFatal(value => Interpreter.Stop((int)value.ToNumber()));
                },
                [REGEX_DELETE] = m => ProcessExpressionStatement($"{nameof(NETInteropFunctionProvider.NETDelete)}({m.Groups["value"]})"),
                [REGEX_RETURN] = m =>
                {
                    if (CurrentFunction.IsMainFunction)
                        return WellKnownError("error.invalid_return");

                    string optval = m.Groups["value"].Value;

                    if (string.IsNullOrWhiteSpace(optval))
                        optval = "0";

                    return ProcessAsVariant(optval).IfNonFatal(v => Return(v));
                },
                [REGEX_FORTO] = m =>
                {
                    Union<InterpreterError, PARSABLE_EXPRESSION> start = ProcessAsRawExpression(m.Groups["start"].Value);
                    string step = m.Groups["step"].Value is { Length: > 0 } s ? s : "1";

                    if (start.Is(out InterpreterError? error))
                        return error;
                    else if (start.UnsafeItem is PARSABLE_EXPRESSION.AnyExpression { Item: EXPRESSION.Binary { Item: { Item1: EXPRESSION.Variable { Item: VARIABLE counter }, Item2: { IsEqualCaseInsensitive: true }, } } } assg)
                        return ProcessAssignmentStatement(assg, false).IfNonFatal(_ =>
                        {
                            SourceLocation loc_for = CurrentLocation;
                            int eip_for = _instruction_pointer;
                            int depth = 1;

                            while (MoveNext())
                            {
                                if (CurrentLineContent.Match(REGEX_NEXT, out Match _))
                                    --depth;
                                else if (CurrentLineContent.Match(REGEX_FOR, out Match _))
                                    ++depth;

                                if (depth == 0)
                                {
                                    Variable v_first = VariableResolver.CreateTemporaryVariable();
                                    string first = '$' + v_first.Name;
                                    string stop = '$' + VariableResolver.CreateTemporaryVariable().Name;

                                    v_first.Value = Variant.True;

                                    InsertReplaceSourceCode(_instruction_pointer, $"WEnd");
                                    InsertReplaceSourceCode(eip_for,
                                        $"While True",
                                        $"If {first} Then",
                                        $"{first} = False",
                                        $"Else",
                                        $"{stop} = Number({m.Groups["stop"]})",
                                        $"If {counter} = {stop} Then",
                                        $"ExitLoop",
                                        $"EndIf",
                                        $"{counter} += {step}",
                                        $"EndIf"
                                    );
                                    _instruction_pointer = eip_for - 1;

                                    return Variant.True;
                                }
                            }

                            return WellKnownError("error.no_matching_close", "For", loc_for);
                        });
                    else
                        return WellKnownError("error.malformed_for_expr", m.Groups["start"]);
                },
                [REGEX_FORIN] = m =>
                {
                    string counter_variables = m.Groups["variable"].Value.Trim();

                    if (counter_variables.Match(REGEX_VARIABLE, out Match match) && match.Index == 0 && match.Length == counter_variables.Length)
                        counter_variables = $"${VariableResolver.CreateTemporaryVariable().Name}, {counter_variables}";

                    if (!counter_variables.Match(REGEX_FORIN_VARIABLES, out Match m_iterator_variables))
                        return WellKnownError("error.unparsable_iteration_variable", m.Groups["variable"]);

                    Union<InterpreterError, Variable> get_or_create(string group)
                    {
                        if (!VariableResolver.TryGetVariable(m_iterator_variables.Groups[group].Value, VariableSearchScope.Local, out Variable? var))
                            var = VariableResolver.CreateVariable(CurrentLocation, m_iterator_variables.Groups[group].Value, false);

                        if (var.IsConst)
                            return WellKnownError("error.constant_assignment", var.Name, var.DeclaredLocation);
                        else
                            return var;
                    }

                    return get_or_create("key").Match(FunctionReturnValue.Fatal, iterator_key =>
                        get_or_create("value").Match(FunctionReturnValue.Fatal, iterator_value =>
                        {
                            SourceLocation loc_for = CurrentLocation;
                            int eip_for = _instruction_pointer;
                            int depth = 1;

                            while (MoveNext())
                            {
                                if (CurrentLineContent.Match(REGEX_NEXT, out Match _))
                                    --depth;
                                else if (CurrentLineContent.Match(REGEX_FOR, out Match _))
                                    ++depth;

                                if (depth == 0)
                                {
                                    string iterator = VariableResolver.CreateTemporaryVariable().Name;

                                    InsertReplaceSourceCode(_instruction_pointer,
                                        $"WEnd",
                                        $"{nameof(InternalsFunctionProvider.__iterator_destroy)}(\"{iterator}\")"
                                    );
                                    InsertReplaceSourceCode(eip_for,
                                        $"{nameof(InternalsFunctionProvider.__iterator_create)}(\"{iterator}\", {m.Groups["expression"]})",
                                        // TODO
                                        // else if (!(collection = (Variant)result_coll).IsIndexable)
                                        //     return WellKnownError("error.invalid_forin_source", collection);
                                        $"While {nameof(InternalsFunctionProvider.__iterator_canmove)}(\"{iterator}\")",
                                        $"${iterator_key.Name} = {nameof(InternalsFunctionProvider.__iterator_currentkey)}(\"{iterator}\")",
                                        $"${iterator_value.Name} = {nameof(InternalsFunctionProvider.__iterator_currentvalue)}(\"{iterator}\")",
                                        $"{nameof(InternalsFunctionProvider.__iterator_movenext)}(\"{iterator}\")"
                                    );
                                    _instruction_pointer = eip_for - 1;

                                    return Variant.True;
                                }
                            }

                            return WellKnownError("error.no_matching_close", "For", loc_for);
                        }));
                },
                [REGEX_WITH] = m =>
                {
                    Union<InterpreterError, PARSABLE_EXPRESSION> parsed = ProcessAsRawExpression(m.Groups["variable"].Value);

                    if (parsed.Is(out InterpreterError? error))
                        return error;
                    else if (parsed.Is(out PARSABLE_EXPRESSION? pexpr)
                        && pexpr is PARSABLE_EXPRESSION.AnyExpression { Item: EXPRESSION.Variable { Item: VARIABLE variable } }
                        && VariableResolver.TryGetVariable(variable, VariableSearchScope.Global, out Variable? withctx)
                        && !withctx.IsConst)
                    {
                        _withcontext_stack.Push(withctx);

                        return Variant.FromReference(withctx);
                    }
                    else
                        return WellKnownError("error.invalid_with_target");
                },
                [REGEX_ENDWITH] = _ =>
                {
                    if (_withcontext_stack.TryPop(out Variable variable))
                        return Variant.FromReference(variable);
                    else
                        return WellKnownError("error.unexpected_close", line, "With");
                },
                [REGEX_REDIM] = m =>
                {
                    Union<InterpreterError, PARSABLE_EXPRESSION>? parsed = ProcessAsRawExpression(m.Groups["expression"].Value);

                    if (parsed.Is(out PARSABLE_EXPRESSION? expr) && expr is PARSABLE_EXPRESSION.AnyExpression
                    {
                        Item: EXPRESSION.Indexer { Item: { Item1: EXPRESSION.Variable { Item: { Name: string varname } }, Item2: EXPRESSION index } }
                    })
                        if (VariableResolver.TryGetVariable(varname, VariableSearchScope.Global, out Variable? variable) && !variable.IsConst)
                            return ProcessExpression(index).IfNonFatal(size =>
                            {
                                if (variable.Value.ResizeArray(Interpreter, (int)size, out Variant? new_arr) && new_arr is Variant arr)
                                {
                                    variable.Value = arr;

                                    return Variant.FromReference(variable);
                                }
                                else
                                    return WellKnownError("error.invalid_redim_size", index);
                            });
                        else
                            return WellKnownError("error.invalid_redim_expression", varname);

                    return parsed.As<InterpreterError>() ?? WellKnownError("error.invalid_redim_expression", m.Groups["expression"]);
                },
                [REGEX_DO] = _ =>
                {
                    SourceLocation do_loc = CurrentLocation;
                    int eip_do = _instruction_pointer;
                    int depth = 1;

                    while (MoveNext())
                        if (CurrentLineContent.Match(REGEX_DO, out Match _))
                            ++depth;
                        else if (CurrentLineContent.Match(REGEX_UNTIL, out Match m))
                        {
                            --depth;

                            if (depth == 0)
                            {
                                InsertReplaceSourceCode(_instruction_pointer, "WEnd");

                                Variable temp = VariableResolver.CreateTemporaryVariable();
                                string first = '$' + temp.Name;

                                temp.Value = Variant.True;

                                InsertReplaceSourceCode(eip_do,
                                    $"While ({first} Or Not({m.Groups["expression"]}))",
                                    $"{first} = False"
                                );
                                _instruction_pointer = eip_do - 1;

                                return Variant.True;
                            }
                        }

                    return WellKnownError("error.no_matching_close", "Do", do_loc);
                },
                [REGEX_UNTIL] = _ => WellKnownError("error.unexpected_close", "Until", "Do"), // REGEX_UNTIL is handled by the REGEX_DO-case
                [REGEX_WHILE] = m =>
                {
                    FunctionReturnValue result = ProcessAsVariant(m.Groups["expression"].Value);

                    return result.IfNonFatal(condition =>
                    {
                        if (!condition.ToBoolean())
                            return MoveToEndOf(BlockStatementType.While);
                        else
                            _while_stack.Push(InsertInternalJumpLabel());

                        return condition;
                    });
                },
                [REGEX_WEND] = _ =>
                {
                    _while_stack.TryPop(out string? label);

                    if (label is null)
                        return WellKnownError("error.unexpected_close", "WEnd", "While");
                    else
                        return MoveTo(label).IfNonFatal(_ =>
                        {
                            RemoveInternalJumpLabel(label);

                            --_instruction_pointer;

                            return Variant.True;
                        });
                },
                [REGEX_IF] = m =>
                {
                    FunctionReturnValue condition = ProcessAsVariant(m.Groups["condition"].Value);
                    bool elif = m.Groups["elif"].Length > 0;

                    if (elif)
                        if (_if_stack.TryPop(out bool cond))
                        {
                            if (cond)
                                condition = Variant.False;
                        }
                        else
                            return WellKnownError("error.missing_if");

                    return condition.IfNonFatal(condition =>
                    {
                        bool cond = condition.ToBoolean();

                        _if_stack.Push(cond);

                        return cond ? Variant.True : MoveToEndOf(BlockStatementType.If).IfNonFatal(_ => Variant.False);
                    });
                },
                [REGEX_ELSE] = _ =>
                {
                    if (_if_stack.TryPop(out bool cond))
                    {
                        if (cond)
                            return MoveToEndOf(BlockStatementType.If);
                    }
                    else
                        return WellKnownError("error.missing_if");

                    return Variant.True;
                },
                [REGEX_ENDIF] = m => _if_stack.TryPop(out _) ? (FunctionReturnValue)Variant.True : WellKnownError("error.unexpected_close", m.Value, BlockStatementType.If),
                [REGEX_GOTO] = m =>
                {
                    if (Interpreter.CommandLineOptions.StrictMode)
                        return WellKnownError("error.experimental.goto_instructions");

                    return MoveTo(m.Groups["label"].Value);
                },
                [REGEX_CONTINUELOOP_EXITLOOP] = m =>
                {
                    bool exit = m.Groups["mode"].Value.Equals("exit", StringComparison.InvariantCultureIgnoreCase);
                    int level = 1;

                    if (m.Groups["level"].Length > 0)
                    {
                        FunctionReturnValue parsed = ProcessAsVariant(m.Groups["level"].Value).IfNonFatal(lvl =>
                        {
                            level = (int)lvl;

                            return lvl;
                        });

                        if (parsed.IsFatal(out _))
                            return parsed;
                    }

                    for (int i = 0; i < level; ++i)
                    {
                        if (!_while_stack.TryPop(out string? label))
                            return WellKnownError("error.unexpected_contexitloop", exit ? "ExitLoop" : "ContinueLoop");
                        else if ((exit ? MoveToEndOf(BlockStatementType.While) : MoveTo(label)).IsFatal(out InterpreterError? error))
                            return error;

                        RemoveInternalJumpLabel(label);

                        --_instruction_pointer;
                    }

                    return Variant.FromNumber(level);
                },
                [REGEX_SELECT] = _ =>
                {
                    _switchselect_stack.Push((null, false));

                    return MoveToEndOf(BlockStatementType.Select).IfNonFatal(_ =>
                    {
                        --_instruction_pointer;

                        return Variant.True;
                    });
                },
                [REGEX_SWITCH] = m =>
                {
                    FunctionReturnValue expression = ProcessAsVariant(m.Groups["expression"].Value);

                    return expression.IfNonFatal(expr =>
                    {
                        _switchselect_stack.Push((expr, false));

                        return MoveToEndOf(BlockStatementType.Switch).IfNonFatal(_ =>
                        {
                            --_instruction_pointer;

                            return expr;
                        });
                    });
                },
                [REGEX_ENDSELECT] = m =>
                {
                    if (_switchselect_stack.TryPop(out (Variant? expr, bool) topmost) && topmost.expr is null)
                        return Variant.True;
                    else
                        return WellKnownError("error.unexpected_close", m.Value, BlockStatementType.Select);
                },
                [REGEX_ENDSWITCH] = m =>
                {
                    if (_switchselect_stack.TryPop(out (Variant? expr, bool) topmost) && topmost.expr is Variant expr)
                        return expr;
                    else
                        return WellKnownError("error.unexpected_close", m.Value, BlockStatementType.Switch);
                },
                [REGEX_CASE] = m =>
                {
                    Variant? switch_expr = null;

                    if (!_switchselect_stack.TryPeek(out (Variant? switch_expr, bool handled) topmost))
                        return WellKnownError("error.unexpected_case");
                    else if (topmost.handled)
                        return MoveToEndOf(switch_expr is null ? BlockStatementType.Select : BlockStatementType.Switch).IfNonFatal(_ =>
                        {
                            --_instruction_pointer;

                            return Variant.True;
                        });
                    else if (REGEX_ELSE.IsMatch(m.Groups["expression"].Value))
                        return Variant.True;
                    else
                        switch_expr = topmost.switch_expr;

                    Union<InterpreterError, PARSABLE_EXPRESSION> case_expr = ProcessAsRawExpression(m.Groups["expression"].Value);
                    FunctionReturnValue process_success(bool success)
                    {
                        _switchselect_stack.TryPop(out topmost);
                        _switchselect_stack.Push((topmost.switch_expr, success));

                        return success ? Variant.True : MoveToEndOf(BlockStatementType.Switch).IfNonFatal(_ =>
                        {
                            --_instruction_pointer;

                            return Variant.True;
                        });
                    }

                    if (case_expr.Is(out PARSABLE_EXPRESSION? expression))
                        if (expression is PARSABLE_EXPRESSION.ToExpression { Item1: EXPRESSION from, Item2: EXPRESSION to })
                        {
                            if (switch_expr is Variant sw)
                                return ProcessExpression(from).IfNonFatal(from => ProcessExpression(to).IfNonFatal(to => process_success(from <= sw && sw <= to)));
                            else
                                return WellKnownError("error.invalid_case_range", expression);
                        }
                        else if (expression is PARSABLE_EXPRESSION.AnyExpression { Item: EXPRESSION expr })
                            return ProcessExpression(expr).IfNonFatal(expr => process_success(switch_expr is Variant sw ? expr.EqualsCaseInsensitive(sw) : expr.ToBoolean()));
                        else
                            return WellKnownError("error.invalid_case_expr", expression);

                    return (InterpreterError)case_expr!;
                },
                [REGEX_CONTINUECASE] = _ =>
                {
                    if (!_switchselect_stack.TryPeek(out (Variant? switch_expr, bool) topmost))
                        return WellKnownError("error.unexpected_continuecase");
                    else
                        return MoveToEndOf(topmost.switch_expr is null ? BlockStatementType.Select : BlockStatementType.Switch).IfNonFatal(result =>
                        {
                            if (!REGEX_CASE.IsMatch(CurrentLineContent))
                                _switchselect_stack.TryPop(out topmost);

                            return result;
                        });
                },
            });

            foreach (AbstractStatementProcessor? proc in Interpreter.PluginLoader.StatementProcessors)
                if (result is { })
                    break;
                else if (proc is { Regex: Regex pat } sp && line.Match(pat, out Match _))
                    result = sp.ProcessStatement(this, line);

            return result;
        });

        private FunctionReturnValue ProcessExpressionStatement(string line) =>
            Interpreter.Telemetry.Measure(TelemetryCategory.ProcessExpressionStatement, delegate
            {
                try
                {
                    if (ProcessDeclarationModifiers(ref line, out DeclarationModifiers modifiers, out (char op, long amount)? enum_step) is { } err)
                        return err;

                    ParserConstructor<PARSABLE_EXPRESSION>.ParserWrapper? provider = modifiers is DeclarationModifiers.None ? Interpreter.ParserProvider.ExpressionParser : Interpreter.ParserProvider.MultiDeclarationParser;
                    PARSABLE_EXPRESSION? expression = provider.Parse(line).ParsedValue;

                    //MainProgram.PrintfDebugMessage("debug.au3thread.expr_statement", expression);

                    if (modifiers == DeclarationModifiers.None)
                        return ProcessAssignmentStatement(expression, false);
                    else if (expression is PARSABLE_EXPRESSION.MultiDeclarationExpression multi_decl)
                        return ProcessMultiDeclarationExpression(multi_decl, modifiers, enum_step);
                    else
                        return WellKnownError("error.invalid_multi_decl", line);
                }
                catch (Exception ex)
                {
                    return Interpreter.Telemetry.Measure(TelemetryCategory.Exceptions, delegate
                    {
                        string key = ex is ParseException or ParserConfigurationException or LexerException or LexerConstructionException ? "error.unparsable_line" : "error.unprocessable_line";

                        if (Interpreter.CommandLineOptions.Verbose)
                            return new InterpreterError(CurrentLocation, $"{Interpreter.CurrentUILanguage[key, line, ex.Message]}\n\nStack trace:\n{ex.StackTrace}");
                        else
                            return WellKnownError(key, line, ex.Message);
                    });
                }
            });

        private Union<InterpreterError, PARSABLE_EXPRESSION> ProcessAsRawExpression(string expression) =>
            Interpreter.Telemetry.Measure<Union<InterpreterError, PARSABLE_EXPRESSION>>(TelemetryCategory.ProcessExpression, delegate
            {
                try
                {
                    ParserConstructor<PARSABLE_EXPRESSION>.ParserWrapper? provider = Interpreter.ParserProvider.ExpressionParser;
                    PARSABLE_EXPRESSION parsed = provider.Parse(expression).ParsedValue;

                    // MainProgram.PrintfDebugMessage("debug.au3thread.raw_expr", parsed);

                    return parsed;
                }
                catch (Exception ex)
                {
                    return Interpreter.Telemetry.Measure(TelemetryCategory.Exceptions, () => WellKnownError("error.unparsable_line", expression, ex.Message));
                }
            });

        internal FunctionReturnValue ProcessAsVariant(string expression)
        {
            Union<InterpreterError, PARSABLE_EXPRESSION>? result = ProcessAsRawExpression(expression);

            if (result.Is(out PARSABLE_EXPRESSION.AnyExpression? any))
            {
                EXPRESSION folded = Interpreter.Telemetry.Measure(TelemetryCategory.ExpressionCleanup, () => Cleanup.FoldConstants(any.Item));

                return ProcessExpression(folded);
            }
            else
                return (InterpreterError?)result ?? WellKnownError("error.unparsable_line", expression);
        }

        private InterpreterError? ProcessDeclarationModifiers(ref string line, out DeclarationModifiers modifiers, out (char op, long amount)? enum_step)
        {
            modifiers = DeclarationModifiers.None;
            enum_step = null;

            while (line.Match(REGEX_DECLARATION_MODIFIER, out Match m_modifier))
            {
                DeclarationModifiers modifier = (DeclarationModifiers)Enum.Parse(typeof(DeclarationModifiers), m_modifier.Value, true);

                line = line[m_modifier.Length..].TrimStart();

                if (modifiers.HasFlag(modifier))
                    return WellKnownError("error.duplicate_modifier", modifier);
                else if (modifier is DeclarationModifiers.Step)
                    if (line.Match(REGEX_ENUM_STEP, out Match m_step))
                    {
                        char op = '+';
                        long amount = long.Parse(m_step.Groups["step"].Value);

                        if (m_step.Groups["op"] is { Length: > 0, Value: string s })
                            op = s[0];

                        enum_step = (op, amount);
                        line = line[m_step.Length..].TrimStart();
                    }
                    else
                        return WellKnownError("error.invalid_step", new string(line.TakeWhile(c => !char.IsWhiteSpace(c)).ToArray()));

                modifiers |= modifier;
            }

            if (modifiers.HasFlag(DeclarationModifiers.Step) && !modifiers.HasFlag(DeclarationModifiers.Enum))
                return WellKnownError("error.unexpected_step");

            foreach ((DeclarationModifiers m1, DeclarationModifiers m2) in new[]
            {
                (DeclarationModifiers.Dim, DeclarationModifiers.Global),
                (DeclarationModifiers.Dim, DeclarationModifiers.Local),
                (DeclarationModifiers.Dim, DeclarationModifiers.Static),
                (DeclarationModifiers.Enum, DeclarationModifiers.Static),
                (DeclarationModifiers.Local, DeclarationModifiers.Global),
                (DeclarationModifiers.Static, DeclarationModifiers.Const),
#if !CHECK_GLOBAL_STATIC
                (DeclarationModifiers.Static, DeclarationModifiers.Global),
#endif
            })
                if (modifiers.HasFlag(m1) && modifiers.HasFlag(m2))
                    return WellKnownError("error.incomplatible_modifiers", m1, m2);

            return null;
        }

        private FunctionReturnValue ProcessMultiDeclarationExpression(PARSABLE_EXPRESSION.MultiDeclarationExpression multi_decl, DeclarationModifiers modifiers, (char op, long amount)? enum_step) =>
            Interpreter.Telemetry.Measure(TelemetryCategory.ProcessDeclaration, delegate
            {
                List<Variable> declared_variables = new();
                Variant last_enum_value = Variant.Zero;
                Variant next_enum_value() => enum_step switch
                {
                    ('*', long amount) => last_enum_value * amount,
                    ('+', long amount) => last_enum_value + amount,
                    ('-', long amount) => last_enum_value - amount,
                    _ => last_enum_value,
                };

                foreach ((VARIABLE variable_ast, VARIABLE_DECLARATION declaration) in multi_decl.Item.Select(t => t.ToValueTuple()))
                {
                    FunctionReturnValue result = ProcessVariableDeclaration(variable_ast, modifiers, out bool existing_static);
                    Variable? variable = null;

                    if (result.IsFatal(out _))
                        return result;

                    VariableResolver.TryGetVariable(variable_ast, VariableSearchScope.Global, out variable);

                    if (variable is null)
                        return WellKnownError("error.undeclared_variable", variable_ast);
                    else
                        switch (declaration)
                        {
                            case VARIABLE_DECLARATION.Scalar { Item: null }:
                                if (modifiers.HasFlag(DeclarationModifiers.Const))
                                    return WellKnownError("error.uninitialized_constant", variable_ast);
                                else if (enum_step != null)
                                {
                                    variable.Value = last_enum_value;
                                    last_enum_value = next_enum_value();
                                }

                                break;
                            case VARIABLE_DECLARATION.Scalar { Item: FSharpOption<EXPRESSION> { Value: EXPRESSION expression } }:
                                if (!existing_static)
                                {
                                    var assg_expr = (ASSIGNMENT_TARGET.NewVariableAssignment(variable_ast), OPERATOR_ASSIGNMENT.Assign, expression).ToTuple();

                                    result = ProcessAssignmentStatement(PARSABLE_EXPRESSION.NewAssignmentExpression(assg_expr), true);
                                    result.IfNonFatal(value =>
                                    {
                                        MainProgram.PrintDebugMessage($"{variable_ast} = {value}");

                                        variable.Value = value;
                                        last_enum_value = value;
                                        last_enum_value = next_enum_value();

                                        return last_enum_value;
                                    });

                                    if (result.IsFatal(out _))
                                        return result;
                                }
                                break;
                            case VARIABLE_DECLARATION.Array { Item1: int size, Item2: FSharpList<EXPRESSION> items }:
                                if (size < 0)
                                    return WellKnownError("error.invalid_array_size", variable_ast, size);
                                else if (items.Length > size)
                                    return WellKnownError("error.too_many_array_items", variable_ast, size, items.Length);
                                else
                                {
                                    variable.Value = Variant.NewArray(size);

                                    int index = 0;

                                    foreach (EXPRESSION item in items)
                                    {
                                        result = ProcessExpression(item);
                                        result.IfNonFatal(value =>
                                        {
                                            variable.Value.TrySetIndexed(Interpreter, index, value);
                                            ++index;

                                            return value;
                                        });

                                        if (result.IsFatal(out _))
                                            return result;
                                    }

                                    break;
                                }
                            case VARIABLE_DECLARATION.Map:
                                variable.Value = Variant.NewMap();

                                break;
                            default:
                                return WellKnownError("error.not_yet_implemented", declaration);
                        }

                    declared_variables.Add(variable);
                }

                return Variant.FromArray(Interpreter, declared_variables.ToArray(Variant.FromReference));
            });

        private FunctionReturnValue ProcessVariableDeclaration(VARIABLE variable, DeclarationModifiers decltype, out bool existing_static)
        {
            bool constant = decltype.HasFlag(DeclarationModifiers.Const) || decltype.HasFlag(DeclarationModifiers.Enum);
            bool global = decltype.HasFlag(DeclarationModifiers.Global) && !decltype.HasFlag(DeclarationModifiers.Local);
            VariableScope scope = (global ? VariableResolver.GlobalRoot : null) ?? VariableResolver;
            Variable? optional_static = null;

            existing_static = false;

            if (decltype.HasFlag(DeclarationModifiers.Static))
            {
#if CHECK_GLOBAL_STATIC
                if (global && scope.TryGetVariable(variable, VariableSearchScope.Global, out Variable? existing))
                    return WellKnownError("error.cannot_make_static", existing, existing.DeclaredLocation);
#endif
                string global_name = $"__staticref:{CurrentFunction.Name}{variable}";
                VariableScope root = scope.GlobalRoot;

                if (!root.TryGetVariable(global_name, VariableSearchScope.Local, out optional_static))
                {
                    optional_static = root.CreateVariable(CurrentLocation, global_name, false);
                    optional_static.Value = Variant.EmptyString;
                }
                else
                    existing_static = true;
            }

            // if (decltype.HasFlag(DeclarationType.Dim) || decltype == DeclarationType.None)
            //     global = CurrentFunction.IsMainFunction;

            if (scope.TryGetVariable(variable, VariableSearchScope.Global, out Variable? var))
            {
                if (!(var.IsGlobal && decltype.HasFlag(DeclarationModifiers.Local))) // potential conflict
                    if (constant && !var.IsConst)
                        return WellKnownError("error.cannot_make_constant", var, var.DeclaredLocation);
                    else if (var.IsConst)
                        return WellKnownError("error.redefining_constant", var, var.DeclaredLocation);
            }

            Variable created = scope.CreateVariable(CurrentLocation, variable, constant);

            if (optional_static is { })
                created.Value = Variant.FromReference(optional_static);

            return created.Value;
        }

        private FunctionReturnValue ProcessAssignmentStatement(PARSABLE_EXPRESSION assignment, bool force) =>
            Interpreter.Telemetry.Measure(TelemetryCategory.ProcessAssignment, delegate
            {
                (ASSIGNMENT_TARGET target, EXPRESSION expression) = Interpreter.Telemetry.Measure(TelemetryCategory.ExpressionCleanup, () => Cleanup.CleanUpExpression(assignment));

                switch (target)
                {
                    case ASSIGNMENT_TARGET.VariableAssignment { Item: VARIABLE variable }:
                        {
                            if (VariableResolver.TryGetVariable(variable.Name, VariableSearchScope.Global, out Variable? target_variable))
                            {
                                if (target_variable.IsConst && !force)
                                    return WellKnownError("error.constant_assignment", target_variable, target_variable.DeclaredLocation);
                            }
                            else
                                target_variable = VariableResolver.CreateVariable(CurrentLocation, variable.Name, false);

                            if (target_variable is null)
                                return WellKnownError("error.invalid_assignment_target", target);
                            else
                                return ProcessVariableAssignment(target_variable, expression);
                        }
                    case ASSIGNMENT_TARGET.IndexedAssignment { Item: { } indexer }:
                        return ProcessExpression(indexer.Item1).IfNonFatal(target =>
                               ProcessExpression(indexer.Item2).IfNonFatal(key =>
                               ProcessExpression(expression).IfNonFatal(value =>
                               {
                                   if (target.TrySetIndexed(Interpreter, key, value))
                                       return value;

                                   return WellKnownError("error.invalid_index_assg", target.AssignedTo, key);
                               })));
                    case ASSIGNMENT_TARGET.MemberAssignemnt { Item: MEMBER_EXPRESSION.ExplicitMemberAccess { Item1: { } targ, Item2: { Item: string memb } } }:
                        return ProcessExpression(targ).IfNonFatal(target =>
                               ProcessExpression(expression).IfNonFatal(value =>
                               {
                                   if (target.TrySetMember(Interpreter, memb, value) || target.TrySetIndexed(Interpreter, memb, value))
                                       return value;

                                   return WellKnownError("error.invalid_index_assg", target.AssignedTo, memb);
                               }));
                    case ASSIGNMENT_TARGET.MemberAssignemnt { Item: MEMBER_EXPRESSION.ImplicitMemberAccess { Item: { Item: string member } } }:
                        {
                            if (_withcontext_stack.TryPeek(out Variable? variable))
                                return ProcessExpression(expression).IfNonFatal(value =>
                                {
                                    if (variable.Value.TrySetMember(Interpreter, member, value) || variable.Value.TrySetIndexed(Interpreter, member, value))
                                        return value;

                                    return WellKnownError("error.invalid_index_assg", variable, member);
                                });
                            else
                                return WellKnownError("error.invalid_with_access", member);
                        }
                    default:
                        return WellKnownError("error.invalid_assignment_target", target);
                }
            });

        private FunctionReturnValue ProcessVariableAssignment(Variable variable, EXPRESSION expression)
        {
            foreach (VARIABLE v in expression.ReferencedVariables)
                if (!VariableResolver.HasVariable(v, VariableSearchScope.Global))
                    return WellKnownError("error.undeclared_variable", v);

            return ProcessExpression(expression).IfNonFatal(v => (variable.ReferencedVariable ?? variable).Value = v);
        }

        private FunctionReturnValue ProcessExpression(EXPRESSION? expression) =>
            Interpreter.Telemetry.Measure(TelemetryCategory.EvaluateExpression, delegate
            {
                FunctionReturnValue value = expression switch
                {
                    null => Variant.Null,
                    EXPRESSION.Literal { Item: LITERAL literal } => Variant.FromLiteral(literal),
                    EXPRESSION.Variable { Item: VARIABLE variable } => ProcessVariable(variable),
                    EXPRESSION.Macro { Item: MACRO macro } => ProcessMacro(macro),
                    EXPRESSION.FunctionName { Item: { Item: string func_name } } =>
                        Interpreter.ScriptScanner.TryResolveFunction(func_name) is ScriptFunction func
                        ? Variant.FromFunction(func)
                        : WellKnownError("error.unresolved_func", func_name),
                    EXPRESSION.Unary { Item: Tuple<OPERATOR_UNARY, EXPRESSION> unary } => ProcessUnary(unary.Item1, unary.Item2),
                    EXPRESSION.Binary { Item: Tuple<EXPRESSION, OPERATOR_BINARY, EXPRESSION> binary } => ProcessBinary(binary.Item1, binary.Item2, binary.Item3),
                    EXPRESSION.Ternary { Item: Tuple<EXPRESSION, EXPRESSION, EXPRESSION> ternary } => ProcessTernary(ternary.Item1, ternary.Item2, ternary.Item3),
                    EXPRESSION.Member { Item: MEMBER_EXPRESSION member } => ProcessMember(member).Match(FunctionReturnValue.Fatal, v => v.MemberValue),
                    EXPRESSION.Indexer { Item: Tuple<EXPRESSION, EXPRESSION> indexer } => ProcessIndexer(indexer.Item1, indexer.Item2),
                    EXPRESSION.FunctionCall { Item: FUNCCALL_EXPRESSION funccall } => ProcessFunctionCall(funccall),
                    EXPRESSION.ReferenceTo { Item: VARIABLE variable } => FunctionExtensions.Do(delegate
                    {
                        FunctionReturnValue result = ProcessVariable(variable);
                        Variable? var = null;

                        if (result.IsFatal(out _))
                            return result;
                        else if (result.IsSuccess(out Variant value, out _))
                            var = value.ReferencedVariable ?? value.AssignedTo;

                        if (var is null)
                            return WellKnownError("error.byref_invalid_variable", variable);
                        else
                            return Variant.FromReference(var);
                    }),
                    _ => WellKnownError("error.not_yet_implemented", expression),
                };

                if (value.IsSuccess(out Variant v, out _))
                    MainProgram.PrintfDebugMessage(
                        "debug.au3thread.expression",
                        ScriptVisualizer.TokenizeScript(expression?.ToString() ?? "Null").ConvertToVT100(false) + MainProgram.COLOR_DEBUG.ToVT100ForegroundString(),
                        v.ToDebugString(Interpreter),
                        v.Type
                    );

                return value;
            });

        private FunctionReturnValue ProcessIndexer(EXPRESSION expr, EXPRESSION index) =>
            ProcessExpression(expr).IfNonFatal(@object =>
            ProcessExpression(index).IfNonFatal(key =>
            {
                if (@object.TryGetIndexed(Interpreter, key, out Variant value))
                    return value;
                else
                    return WellKnownError("error.invalid_index", key);
            }));

        private Union<InterpreterError, (Variant Instance, string MemberName, Variant MemberValue)> ProcessMember(MEMBER_EXPRESSION expr)
        {
            Union<InterpreterError, (Variant, string, Variant)> result = WellKnownError("error.not_yet_implemented", expr);

            switch (expr)
            {
                case MEMBER_EXPRESSION.ExplicitMemberAccess { Item1: { } objexpr, Item2: { Item: string m } }:
                    if (ProcessExpression(objexpr).IfNonFatal(expr =>
                        {
                            result = (expr, m, default);

                            return expr;
                        }).IsFatal(out InterpreterError? error))
                        result = error;

                    break;
                case MEMBER_EXPRESSION.ImplicitMemberAccess { Item: { Item: string m } }:
                    if (_withcontext_stack.TryPeek(out Variable? variable))
                    {
                        result = (variable.Value, m, default);

                        break;
                    }
                    else
                        return WellKnownError("error.invalid_with_access", m);
            }

            if (result.Is(out (Variant instance, string name, Variant value) res))
                if (res.instance.TryGetMember(Interpreter, res.name, out Variant v))
                    result = (res.instance, res.name, v);
                else if (res.instance.TryGetIndexed(Interpreter, res.name, out v))
                    result = (res.instance, res.name, v);
                else if (res.name.Equals("length", StringComparison.InvariantCultureIgnoreCase))
                    result = (res.instance, "Length", res.instance.Length);
                else
                    return WellKnownError("error.unknown_member", res.name);

            return result;
        }

        private FunctionReturnValue ProcessUnary(OPERATOR_UNARY op, EXPRESSION expr)
        {
            FunctionReturnValue result = ProcessExpression(expr);

            if (result.IsSuccess(out Variant value, out _))
                if (op.IsIdentity)
                    result = +value;
                else if (op.IsNegate)
                    result = -value;
                else if (op.IsNot)
                    result = !value;
                else if (op is OPERATOR_UNARY.Cast { Item: { } cop })
                    if (cop.IsCBool)
                        result = (Variant)value.ToBoolean();
                    else if (cop.IsCInt)
                        result = (Variant)(int)value.ToNumber();
                    else if (cop.IsCNumber)
                        result = (Variant)value.ToNumber();
                    else if (cop.IsCString)
                        result = (Variant)value.ToString();
                    else if (cop.IsCBinary)
                        result = (Variant)value.ToBinary();
                    else
                        result = WellKnownError("error.unsupported_operator", cop);
                else
                    result = WellKnownError("error.unsupported_operator", op);

            return result;
        }

        private FunctionReturnValue ProcessBinary(EXPRESSION expr1, OPERATOR_BINARY op, EXPRESSION expr2)
        {
            InterpreterError? evaluate(EXPRESSION expr, out Variant target)
            {
                Box<Variant> box = Variant.Zero;

                ProcessExpression(expr).IfNonFatal(result => box.Data = result).IsFatal(out InterpreterError? error);

                target = box;

                return error;
            }

            InterpreterError? err = evaluate(expr1, out Variant e1);
            Variant e2;

            if (err is { })
                return err;
            else if (op.IsAnd)
            {
                if (!e1.ToBoolean())
                    return Variant.False;
                else if ((err = evaluate(expr2, out e2)) is { })
                    return err;
                else
                    return e2;
            }
            else if (op.IsOr)
            {
                if (e1.ToBoolean())
                    return Variant.True;
                else if ((err = evaluate(expr2, out e2)) is { })
                    return err;
                else
                    return e2;
            }
            else
                err = evaluate(expr2, out e2);

            if (err is { })
                return err;
            else if (op.IsStringConcat)
                return e1 & e2;
            else if (op.IsEqualCaseSensitive)
                return Variant.FromBoolean(e1.EqualsCaseSensitive(e2));
            else if (op.IsEqualCaseInsensitive)
                return Variant.FromBoolean(e1.EqualsCaseInsensitive(e2));
            else if (op.IsUnequal)
                return Variant.FromBoolean(e1.NotEquals(e2));
            else if (op.IsGreater)
                return Variant.FromBoolean(e1 > e2);
            else if (op.IsGreaterEqual)
                return Variant.FromBoolean(e1 >= e2);
            else if (op.IsLower)
                return Variant.FromBoolean(e1 < e2);
            else if (op.IsLowerEqual)
                return Variant.FromBoolean(e1 <= e2);
            else if (op.IsAdd)
                return e1 + e2;
            else if (op.IsSubtract)
                return e1 - e2;
            else if (op.IsMultiply)
                return e1 * e2;
            else if (op.IsDivide)
                return e1 / e2;
            else if (op.IsPower)
                return e1 ^ e2;

            // TODO : modulus

            return WellKnownError("error.unsupported_operator", op);
        }

        private FunctionReturnValue ProcessTernary(EXPRESSION expr1, EXPRESSION expr2, EXPRESSION expr3) =>
            ProcessExpression(expr1).IfNonFatal(cond => ProcessExpression(cond.ToBoolean() ? expr2 : expr3));

        private FunctionReturnValue ProcessFunctionCall(FUNCCALL_EXPRESSION funccall)
        {
            Union<Variant[], InterpreterError> ProcessRawArguments(FSharpList<EXPRESSION> raw_args)
            {
                Variant[] arguments = new Variant[raw_args.Length];
                int i = 0;

                foreach (EXPRESSION arg in raw_args)
                {
                    FunctionReturnValue res = ProcessExpression(arg);

                    if (res.IsFatal(out InterpreterError? error))
                        return error;
                    else
                        res.IfNonFatal(value => arguments[i++] = value);
                }

                return arguments;
            }

            switch (funccall)
            {
                case FUNCCALL_EXPRESSION.DirectFunctionCall { Item1: { Item: string func_name }, Item2: var raw_args }:
                    if (Interpreter.ScriptScanner.TryResolveFunction(func_name) is ScriptFunction func)
                        return ProcessRawArguments(raw_args).Match(args => Call(func, args), FunctionReturnValue.Fatal);
                    else
                        return WellKnownError("error.unresolved_func", func_name);
                case FUNCCALL_EXPRESSION.MemberCall { Item1: MEMBER_EXPRESSION member, Item2: var raw_args }:
                    return ProcessRawArguments(raw_args).Match(args =>
                        ProcessMember(member).Match(FunctionReturnValue.Fatal, member =>
                        {
                            if (member.Instance.TryInvoke(Interpreter, member.MemberName, args, out Variant result))
                                return result;

                            throw new NotImplementedException();
                        }), FunctionReturnValue.Fatal);
            }

            return WellKnownError("error.not_yet_implemented", funccall);
        }

        private FunctionReturnValue ProcessMacro(MACRO macro)
        {
            if (Interpreter.MacroResolver.GetTryValue(this, macro.Name, out Variant value, out _))
                return value;
            else
                return WellKnownError("error.unknown_macro", macro.Name);
        }

        private FunctionReturnValue ProcessVariable(VARIABLE variable)
        {
            if (variable.IsDiscard)
                return WellKnownError("error.invalid_discard_access", VARIABLE.Discard, FrameworkMacros.MACRO_DISCARD);
            else if (VariableResolver.TryGetVariable(variable, VariableSearchScope.Global, out Variable? var))
            {
                Variant value = var.Value;

                if (value.AssignedTo != var)
                    var.Value = value.AssignTo(var); // update parent var

                return value;
            }
            else
                return WellKnownError("error.undeclared_variable", variable);
        }

        private FunctionReturnValue? UseExternalLineProcessors(string line)
        {
            foreach (AbstractLineProcessor proc in Interpreter.PluginLoader.LineProcessors)
                if (proc.CanProcessLine(line))
                    return Interpreter.Telemetry.Measure(TelemetryCategory.ExternalProcessor, () => proc.ProcessLine(this, line));

            return null;
        }

        private InterpreterError WellKnownError(string key, params object?[] args) => InterpreterError.WellKnown(CurrentLocation, key, args);
    }

    [Flags]
    public enum DeclarationModifiers
        : byte
    {
        None = 0b_0000_0000,
        Dim = 0b_0000_0001,
        Local = 0b_0000_0010,
        Global = 0b_0000_0100,
        Const = 0b_0000_1000,
        Static = 0b_0001_0000,
        Enum = 0b_0010_0000,
        Step = 0b_0100_0000,
    }

    public enum BlockStatementType
        : int
    {
        __function__ = default,
        While,
        If,
        Select,
        Switch,
        // Case,
    }
}
