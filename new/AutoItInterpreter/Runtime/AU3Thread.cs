using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

using Piglet.Parser.Configuration.Generic;

using Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework;
using Unknown6656.AutoIt3.Extensibility.Plugins.Internals;
using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.Common;

using static Unknown6656.AutoIt3.ExpressionParser.AST;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class AU3Thread
        : IDisposable
    {
        private static volatile int _tid = 0;
        private readonly ConcurrentStack<CallFrame> _callstack = new ConcurrentStack<CallFrame>();
        private volatile bool _running = false;
        private int? _override_exitcode = null;


        public Interpreter Interpreter { get; }

        public bool IsRunning => _running;

        public CallFrame? CurrentFrame => _callstack.TryPeek(out CallFrame? lp) ? lp : null;

        public SourceLocation? CurrentLocation => CallStack.OfType<AU3CallFrame>().FirstOrDefault()?.CurrentLocation ?? SourceLocation.Unknown;

        public ScriptFunction? CurrentFunction => CurrentFrame?.CurrentFunction;

        public VariableScope CurrentVariableResolver => CurrentFrame?.VariableResolver ?? Interpreter.VariableResolver;

        public CallFrame[] CallStack => CurrentFrame.Propagate(frame => (frame?.CallerFrame, frame is { })).ToArrayWhere(frame => frame is { })!;

        public bool IsDisposed { get; private set; }

        public bool IsMainThread => ReferenceEquals(this, Interpreter.MainThread);

        public int ThreadID { get; }


        internal AU3Thread(Interpreter interpreter)
        {
            ThreadID = ++_tid;
            Interpreter = interpreter;
            Interpreter.AddThread(this);

            MainProgram.PrintfDebugMessage("debug.au3thread.created", this);
        }

        public Union<InterpreterError, Variant> Start(ScriptFunction function, Variant[] args) =>
            Interpreter.Telemetry.Measure<Union<InterpreterError, Variant>>(TelemetryCategory.ThreadRun, delegate
            {
                if (_running)
                    return InterpreterError.WellKnown(CurrentLocation, "error.thread_already_running", ThreadID);
                else
                    _running = true;
            
                Union<InterpreterError, Variant> result = Call(function, args);
            
                _running = false;
            
                if (_override_exitcode is int code)
                    return Variant.FromNumber(code);
            
                return result;
            });

        public Union<InterpreterError, Variant> Call(ScriptFunction function, Variant[] args)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            CallFrame? old = CurrentFrame;
            CallFrame frame = function switch
            {
                AU3Function f => new AU3CallFrame(this, old, f, args),
                NativeFunction f => new NativeCallFrame(this, old, f, args),
                _ => throw new ArgumentException($"A function of the type '{function}' cannot be handled by the current thread '{this}'.", nameof(function)),
            };

            _callstack.Push(frame);

            Union<InterpreterError, Variant> result = frame.Execute(args);

            while (!ReferenceEquals(CurrentFrame, old))
                ExitCall();

            return result;
        }

        public void Stop()
        {
            _running = false;

            Dispose();
        }

        public void Stop(int exitcode)
        {
            Stop();

            _override_exitcode = exitcode;
        }

        internal SourceLocation? ExitCall()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            _callstack.TryPop(out CallFrame? frame);
            frame?.Dispose();


            return CurrentLocation;
        }

        public override string ToString() => $"0x{_tid:x4}{(IsMainThread ? " (main)" : "")} @ {CurrentLocation}";

        public void Dispose()
        {
            _running = false;

            if (IsDisposed)
                return;
            else
                IsDisposed = true;

            Interpreter.RemoveThread(this);
            _callstack.TryPop(out _);

            MainProgram.PrintfDebugMessage("debug.au3thread.disposed", this);

            if (!_callstack.IsEmpty)
                throw new InvalidOperationException("The execution stack is not empty.");
        }
    }

#pragma warning disable CA1063
    // TODO : covariant return for 'CurrentFunction'
    public abstract class CallFrame
        : IDisposable
    {
        public AU3Thread CurrentThread { get; }

        public virtual ScriptFunction CurrentFunction { get; }

        public VariableScope VariableResolver { get; }

        public Variant[] PassedArguments { get; }

        public CallFrame? CallerFrame { get; }

        public Variant ReturnValue { protected set; get; } = Variant.Zero;

        public virtual SourceLocation CurrentLocation => CurrentThread.CurrentLocation ?? CurrentFunction.Location;

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

        public void Dispose() => VariableResolver.Dispose();

        protected abstract Union<InterpreterError, Variant> InternalExec(Variant[] args);

        internal Union<InterpreterError, Variant> Execute(Variant[] args)
        {
            Union<InterpreterError, Variant> result = Variant.Zero;
            ScannedScript script = CurrentFunction.Script;

            SetError(0, 0);

            Interpreter.Telemetry.Measure(TelemetryCategory.OnAutoItStart, delegate
            {
                if (CurrentFunction.IsMainFunction && script.LoadScript(this) is InterpreterError load_error)
                    result = load_error;
            });

            (int min_argc, int max_argc) = CurrentFunction.ParameterCount;

            if (args.Length < min_argc)
                return InterpreterError.WellKnown(CurrentThread.CurrentLocation, "error.not_enough_args", CurrentFunction.Name, min_argc, args.Length);
            else if (args.Length > max_argc)
                return InterpreterError.WellKnown(CurrentThread.CurrentLocation, "error.too_many_args", CurrentFunction.Name, max_argc, args.Length);
            else if (result.Is<Variant>())
                if (CurrentThread.IsRunning)
                {
                    MainProgram.PrintfDebugMessage("debug.au3thread.executing", CurrentFunction);

                    result = Interpreter.Telemetry.Measure(TelemetryCategory.ScriptExecution, () => InternalExec(args));
                }

            Interpreter.Telemetry.Measure(TelemetryCategory.OnAutoItExit, delegate
            {
                if (CurrentFunction.IsMainFunction && result.Is<Variant>() && script.UnLoadScript(this) is InterpreterError unload_error)
                    result = unload_error;
            });

            if (result.Is(out Variant @return))
                ReturnValue = @return;

            return result;
        }

        public Union<InterpreterError, Variant> Call(ScriptFunction function, Variant[] args) => CurrentThread.Call(function, args);

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

        public void Print(Variant value) => Interpreter.Print(this, value);

        public void Print(object? value) => Interpreter.Print(this, value);

        public override string ToString() => $"[0x{CurrentThread.ThreadID:x4}]";

        internal void IssueWarning(string key, params object?[] args) => MainProgram.PrintWarning(CurrentLocation, Interpreter.CurrentUILanguage[key, args]);
    }
#pragma warning restore CA1063

    public sealed class NativeCallFrame
        : CallFrame
    {
        internal NativeCallFrame(AU3Thread thread, CallFrame? caller, NativeFunction function, Variant[] args)
            : base(thread, caller, function, args)
        {
        }

        protected override Union<InterpreterError, Variant> InternalExec(Variant[] args)
        {
            FunctionReturnValue result = Interpreter.Telemetry.Measure(TelemetryCategory.NativeScriptExecution, () => ((NativeFunction)CurrentFunction).Execute(this, args));
            Variant? extended = null;
            int error = 0;

            if (result.IsFatal(out InterpreterError? fatal))
                return fatal;
            else if (result.IsSuccess(out Variant variant, out extended) || result.IsError(out variant, out error, out extended))
                return SetError(error, extended, in variant);
            else
                throw new InvalidOperationException("Return value could not be processed");
        }

        public override string ToString() => $"{base.ToString()} native call frame";
    }

    public sealed class AU3CallFrame
        : CallFrame
    {
        private const RegexOptions _REGEX_OPTIONS = RegexOptions.IgnoreCase | RegexOptions.Compiled;
        private static readonly Regex REGEX_INTERNAL_LABEL = new Regex(@"^§\w+$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_VARIABLE = new Regex(@"\$([^\W\d]|[^\W\d]\w*)\b", _REGEX_OPTIONS);
        private static readonly Regex REGEX_GOTO = new Regex(@"^goto\s+(?<label>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_WHILE = new Regex(@"^while\s+(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_WEND = new Regex(@"^wend$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_NEXT = new Regex(@"^next$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_EXIT = new Regex(@"^exit(\b\s*(?<code>.+))?$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_RETURN = new Regex(@"^return(\b\s*(?<value>.+))?$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_FOR = new Regex(@"^for\s+.+$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_FORTO = new Regex(@"^for\s+(?<start>.+)\s+to\s+(?<stop>.+?)(\s+step\s+(?<step>.+))?$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_FORIN = new Regex(@"^for\s+(?<variable>.+)\s+in\s+(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_FORIN_VARIABLES = new Regex(@"^\$(?<key>[^\W\d]|[^\W\d]\w*)\s*,\s*\$(?<value>[^\W\d]|[^\W\d]\w*)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_WITH = new Regex(@"^with\s+(?<variable>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDWITH = new Regex(@"^endwith$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_REDIM = new Regex(@"^redim\s+(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_DO = new Regex(@"^do$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_UNTIL = new Regex(@"^until\s+(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_IF = new Regex(@"^(?<elif>else)?if\s+(?<condition>.+)\s+then$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ELSE = new Regex(@"^else$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDIF = new Regex(@"^endif$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_DECLARATION_MODIFIER = new Regex(@"^(local|static|global|const|dim|enum|step)\b", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENUM_STEP = new Regex(@"^(?<op>[+\-*]?)(?<step>\d+)\b", _REGEX_OPTIONS);
        private static readonly Regex REGEX_INCLUDE = new Regex(@"^include?\s+(?<open>[""'<])(?<path>(?:(?!\k<close>).)+)(?<close>[""'>])$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_CONTINUELOOP_EXITLOOP = new Regex(@"^(?<mode>continue|exit)loop\s*(?<level>.+)?\s*$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_SELECT = new Regex(@"^select$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDSELECT = new Regex(@"^endselect$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_SWITCH = new Regex(@"^switch\b\s*(?<expression>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDSWITCH = new Regex(@"^endswitch$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_CASE = new Regex(@"^case\b\s*(?<expression>.+)*$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_CONTINUECASE = new Regex(@"^continuecase$", _REGEX_OPTIONS);

        private readonly ConcurrentDictionary<string, IEnumerator<(Variant key, Variant value)>> _iterators = new();
        private readonly ConcurrentStack<Variable> _withcontext_stack = new ConcurrentStack<Variable>();
        private readonly ConcurrentStack<bool> _if_stack = new ConcurrentStack<bool>();
        private readonly ConcurrentStack<string> _while_stack = new();
        private readonly ConcurrentStack<(Variant? switch_expression, bool case_handled)> _switchselect_stack = new();

        private volatile int _instruction_pointer = 0;
        private List<(SourceLocation LineLocation, string LineContent)> _line_cache;


        public int CurrentInstructionPointer => _instruction_pointer;

        public (SourceLocation LineLocation, string LineContent)[] CurrentLineCache => _line_cache.ToArray();

        public override SourceLocation CurrentLocation => _instruction_pointer < 0 ? CurrentFunction.Location : _line_cache[_instruction_pointer].LineLocation;

        public string CurrentLineContent => _instruction_pointer < 0 ? '<' + Interpreter.CurrentUILanguage["general.unknown"] + '>' : _line_cache[_instruction_pointer].LineContent;

        public Dictionary<string, int> InternalJumpLabels => _line_cache.WithIndex().Where(l => REGEX_INTERNAL_LABEL.IsMatch(l.Item.LineContent)).ToDictionary(l => l.Item.LineContent, l => l.Index);


        internal AU3CallFrame(AU3Thread thread, CallFrame? caller, AU3Function function, Variant[] args)
            : base(thread, caller, function, args)
        {
            _line_cache = function.Lines.ToList();
            _instruction_pointer = 0;
        }

        public override string ToString() => $"{base.ToString()} {CurrentLocation}";

        protected override Union<InterpreterError, Variant> InternalExec(Variant[] args)
        {
            _instruction_pointer = -1;

            AU3Function func = (AU3Function)CurrentFunction;
            int argc = func.ParameterCount.MaximumCount;
            int len = args.Length;

            if (len < argc)
                Array.Resize(ref args, argc);

            for (int i = 0; i < argc; ++i)
            {
                PARAMETER_DECLARATION param = func.Parameters[i];
                Variable param_var = VariableResolver.CreateVariable(func.Location, param.Variable.Name, param.IsConst);

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
                    Union<InterpreterError, Variant> result = ProcessExpression(expr);

                    if (result.Is(out InterpreterError? error))
                        return error;
                    else if (result.Is(out Variant value))
                        args[i] = value;
                }

                param_var.Value = args[i];
            }

            return Interpreter.Telemetry.Measure<Union<InterpreterError, Variant>>(TelemetryCategory.Au3ScriptExecution, delegate
            {
                _instruction_pointer = 0;

                while (_instruction_pointer < _line_cache.Count && CurrentThread.IsRunning)
                    if (ParseCurrentLine()?.OptionalError is InterpreterError error)
                        return error;
                    else if (!MoveNext())
                        break;

                return ReturnValue;
            });
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

        private InterpreterError? MoveTo(string jump_label)
        {
            if (InternalJumpLabels.TryGetValue(jump_label, out int index))
            {
                _instruction_pointer = index;

                return null;
            }
            else if ((CurrentFunction as AU3Function)?.JumpLabels[jump_label] is JumpLabel label)
            {
                MoveTo(label);

                return null;
            }

            return WellKnownError("error.unknown_jumplabel", jump_label);
        }

        /// <summary>
        /// Copies the given value into the <see cref="CallFrame.ReturnValue"/>-field, and moves the instruction pointer to the end.
        /// </summary>
        /// <param name="value">Return value</param>
        public void Return(Variant value)
        {
            _instruction_pointer = _line_cache.Count;
            ReturnValue = value;
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
        
        private void InsertReplaceSourceCode(int instruction_ptr, params string[] lines)
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

        public InterpreterResult? ParseCurrentLine()
        {
            (SourceLocation loc, string line) = _line_cache[_instruction_pointer];
            InterpreterResult? result = null;

            line = line.Trim();

            MainProgram.PrintDebugMessage("-----------------------------------------------------------------------------------------------");
            MainProgram.PrintfDebugMessage("debug.au3thread.location", loc);
            MainProgram.PrintfDebugMessage("debug.au3thread.content", line);

            if (string.IsNullOrEmpty(line) || REGEX_INTERNAL_LABEL.IsMatch(line))
                return InterpreterResult.OK;

            Interpreter.Telemetry.Measure(TelemetryCategory.ProcessLine, delegate
            {
                result ??= ProcessDirective(line);
                result ??= ProcessStatement(line);
                result ??= UseExternalLineProcessors(line);
                result ??= ProcessExpressionStatement(line);
            });

            if (Interpreter.CommandLineOptions.IgnoreErrors && result?.OptionalError?.Message is string msg)
            {
                MainProgram.PrintWarning(CurrentLocation, msg);

                result = null;
            }

            return result ?? InterpreterResult.OK;
        }

        private InterpreterResult? ProcessDirective(string directive)
        {
            if (!directive.StartsWith('#'))
                return null;

            directive = directive[1..];

            InterpreterResult? result = Interpreter.Telemetry.Measure(TelemetryCategory.ProcessDirective, delegate
            {
                if (directive.Match(REGEX_INCLUDE, out ReadOnlyIndexer<string, string>? g))
                {
                    char open = g["open"][0];
                    char close = g["close"][0];
                    bool relative = open != '<';

                    if (open != close && open != '<' && close != '>')
                        return WellKnownError("error.mismatched_quotes", open, close);

                    return Interpreter.ScriptScanner.ScanScriptFile(CurrentLocation, g["path"], relative).Match(err => err, script =>
                    {
                        ScannedScript[] active = Interpreter.ScriptScanner.ActiveScripts;

                        if (active.Contains(script))
                        {
                            if (script.IncludeOnlyOnce)
                                return InterpreterResult.OK;
                            else
                                return WellKnownError("error.circular_include", script.Location.FullName);
                        }
                        else if (Call(script.MainFunction, Array.Empty<Variant>()).Is(out InterpreterError? err))
                            return err;
                        else
                            return InterpreterResult.OK;
                    });
                }

                return null;
            });

            foreach (AbstractDirectiveProcessor proc in Interpreter.PluginLoader.DirectiveProcessors)
                result ??= Interpreter.Telemetry.Measure(TelemetryCategory.ProcessDirective, () => proc.ProcessDirective(this, directive));

            if (result is null)
                IssueWarning("warning.unparsable_dirctive", directive);

            return InterpreterResult.OK;
        }

        private InterpreterError? MoveToEndOf(BlockStatementType type)
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

            return depth == 0 ? null : WellKnownError("error.no_matching_close", type, init);
        }

        private InterpreterResult? ProcessStatement(string line) => Interpreter.Telemetry.Measure(TelemetryCategory.ProcessStatement, delegate
        {
            InterpreterResult? result = line.Match(null, new Dictionary<Regex, Func<Match, InterpreterResult?>>
            {
                [REGEX_EXIT] = m =>
                {
                    string code = m.Groups["code"].Value;

                    if (string.IsNullOrWhiteSpace(code))
                        code = "0";

                    Union<InterpreterError, Variant> result = ProcessAsVariant(code);

                    if (result.Is(out Variant value))
                    {
                        Interpreter.Stop((int)value.ToNumber());

                        return InterpreterResult.OK;
                    }
                    else
                        return (InterpreterError)result;
                },
                [REGEX_RETURN] = m =>
                {
                    if (CurrentFunction.IsMainFunction)
                        return WellKnownError("error.invalid_return");

                    string optval = m.Groups["value"].Value;

                    if (string.IsNullOrWhiteSpace(optval))
                        optval = "0";

                    Union<InterpreterError, Variant> result = ProcessAsVariant(optval);

                    if (result.Is(out Variant value))
                        Return(value);
                    else
                        return (InterpreterError)result;

                    return InterpreterResult.OK;
                },
                [REGEX_FORTO] = m =>
                {
                    Union<InterpreterError, PARSABLE_EXPRESSION> start = ProcessAsRawExpression(m.Groups["start"].Value);
                    string step = m.Groups["step"].Value is { Length: > 0 } s ? s : "1";

                    if (start.Is(out InterpreterError? error))
                        return error;
                    else if (start.UnsafeItem is PARSABLE_EXPRESSION.AnyExpression
                    {
                        Item: EXPRESSION.Binary
                        {
                            Item:
                            {
                                Item1: EXPRESSION.Variable { Item: VARIABLE counter },
                                Item2: { IsEqualCaseInsensitive: true },
                            }
                        }
                    } assg)
                    {
                        if (ProcessAssignmentStatement(assg, false).Is(out error))
                            return error;

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

                                return InterpreterResult.OK;
                            }
                        }

                        return WellKnownError("error.no_matching_close", "For", loc_for);
                    }
                    else
                        return WellKnownError("error.malformed_for_expr", m.Groups["start"]);
                },
                [REGEX_FORIN] = m =>
                {
                    string counter_variables = m.Groups["variable"].Value.Trim();

                    if (counter_variables.Match($"^{REGEX_VARIABLE}$", out Match _))
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

                    return get_or_create("key").Match(err => err, iterator_key =>
                        get_or_create("value").Match(err => err, iterator_value =>
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

                                    return InterpreterResult.OK;
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
                    else if (parsed.Is(out PARSABLE_EXPRESSION? pexpr) && pexpr is PARSABLE_EXPRESSION.AnyExpression
                    {
                        Item: EXPRESSION.Variable
                        {
                            Item: VARIABLE variable
                        }
                    } && VariableResolver.TryGetVariable(variable, VariableSearchScope.Global, out Variable? withctx) && !withctx.IsConst)
                    {
                        _withcontext_stack.Push(withctx);

                        return InterpreterResult.OK;
                    }
                    else
                        return WellKnownError("error.invalid_with_target");
                },
                [REGEX_ENDWITH] = _ =>
                {
                    if (_withcontext_stack.TryPop(out Variable _))
                        return InterpreterResult.OK;
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
                        {
                            Union<InterpreterError, Variant> size = ProcessExpression(index);

                            if (size.Is(out Variant size_value) && variable.Value.ResizeArray(Interpreter, (int)size_value, out Variant? new_arr) && new_arr is Variant arr)
                            {
                                variable.Value = arr;

                                return InterpreterResult.OK;
                            }

                            return WellKnownError("error.invalid_redim_size", index);
                        }
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

                                return InterpreterResult.OK;
                            }
                        }

                    return WellKnownError("error.no_matching_close", "Do", do_loc);
                },
                [REGEX_UNTIL] = _ => WellKnownError("error.unexpected_close", "Until", "Do"), // REGEX_UNTIL is handled by the REGEX_DO-case
                [REGEX_WHILE] = m =>
                {
                    Union<InterpreterError, Variant> result = ProcessAsVariant(m.Groups["expression"].Value);

                    if (result.Is(out InterpreterError? error))
                        return error;

                    if (!result.As<Variant>().ToBoolean())
                        return MoveToEndOf(BlockStatementType.While) ?? InterpreterResult.OK;
                    else
                        _while_stack.Push(InsertInternalJumpLabel());

                    return InterpreterResult.OK;
                },
                [REGEX_WEND] = _ =>
                {
                    _while_stack.TryPop(out string? label);

                    if (label is null)
                        return WellKnownError("error.unexpected_close", "WEnd", "While");
                    else if (MoveTo(label) is InterpreterError error)
                        return error;

                    RemoveInternalJumpLabel(label);

                    --_instruction_pointer;

                    return InterpreterResult.OK;
                },
                [REGEX_IF] = m =>
                {
                    Union<InterpreterError, Variant> condition = ProcessAsVariant(m.Groups["condition"].Value);
                    bool elif = m.Groups["elif"].Length > 0;

                    if (elif)
                        if (_if_stack.TryPop(out bool cond))
                        {
                            if (cond)
                                condition = Variant.False;
                        }
                        else
                            return WellKnownError("error.missing_if");

                    if (condition.Is(out InterpreterError? error))
                        return error;
                    else
                    {
                        bool cond = condition.As<Variant>().ToBoolean();

                        _if_stack.Push(cond);

                        if (!cond && MoveToEndOf(BlockStatementType.If) is InterpreterError err)
                            return err;
                    }

                    return InterpreterResult.OK;
                },
                [REGEX_ELSE] = _ =>
                {
                    if (_if_stack.TryPop(out bool cond))
                    {
                        if (cond && MoveToEndOf(BlockStatementType.If) is InterpreterError error)
                            return error;
                    }
                    else
                        return WellKnownError("error.missing_if");

                    return InterpreterResult.OK;
                },
                [REGEX_ENDIF] = m => _if_stack.TryPop(out _) ? InterpreterResult.OK : WellKnownError("error.unexpected_close", m.Value, BlockStatementType.If),
                [REGEX_GOTO] = m =>
                {
                    if (Interpreter.CommandLineOptions.StrictMode)
                        return WellKnownError("error.experimental.goto_instructions");

                    return MoveTo(m.Groups["label"].Value) ?? InterpreterResult.OK;
                },
                [REGEX_CONTINUELOOP_EXITLOOP] = m =>
                {
                    bool exit = m.Groups["mode"].Value.Equals("exit", StringComparison.InvariantCultureIgnoreCase);
                    int level = 1;

                    if (m.Groups["level"].Length > 0)
                    {
                        Union<InterpreterError, Variant> parsed = ProcessAsVariant(m.Groups["level"].Value);

                        if (parsed.Is(out InterpreterError? error))
                            return error;

                        level = (int)(Variant)parsed;
                    }

                    while (level-- > 0)
                    {
                        if (!_while_stack.TryPop(out string? label))
                            return WellKnownError("error.unexpected_contexitloop", exit ? "ExitLoop" : "ContinueLoop");
                        else
                        {
                            if ((exit ? MoveToEndOf(BlockStatementType.While) : MoveTo(label)) is InterpreterError error)
                                return error;
                        }

                        RemoveInternalJumpLabel(label);

                        --_instruction_pointer;
                    }

                    return InterpreterResult.OK;
                },
                [REGEX_SELECT] = _ =>
                {
                    _switchselect_stack.Push((null, false));

                    if (MoveToEndOf(BlockStatementType.Select) is InterpreterError error)
                        return error;

                    --_instruction_pointer;

                    return InterpreterResult.OK;
                },
                [REGEX_SWITCH] = m =>
                {
                    Union<InterpreterError, Variant> expression = ProcessAsVariant(m.Groups["expression"].Value);

                    if (expression.Is(out InterpreterError? error))
                        return error;
                    else
                        _switchselect_stack.Push(((Variant)expression, false));

                    error = MoveToEndOf(BlockStatementType.Switch);

                    if (error is null)
                        --_instruction_pointer;

                    return error ?? InterpreterResult.OK;
                },
                [REGEX_ENDSELECT] = m =>
                {
                    if (_switchselect_stack.TryPop(out (Variant? expr, bool) topmost) && topmost.expr is null)
                        return InterpreterResult.OK;
                    else
                        return WellKnownError("error.unexpected_close", m.Value, BlockStatementType.Select);
                },
                [REGEX_ENDSWITCH] = m =>
                {
                    if (_switchselect_stack.TryPop(out (Variant? expr, bool) topmost) && topmost.expr is Variant)
                        return InterpreterResult.OK;
                    else
                        return WellKnownError("error.unexpected_close", m.Value, BlockStatementType.Switch);
                },
                [REGEX_CASE] = m =>
                {
                    Variant? switch_expr = null;

                    if (!_switchselect_stack.TryPeek(out (Variant? switch_expr, bool handled) topmost))
                        return WellKnownError("error.unexpected_case");
                    else if (topmost.handled)
                    {
                        if (MoveToEndOf(switch_expr is null ? BlockStatementType.Select : BlockStatementType.Switch) is InterpreterError error)
                            return error!;
                        else
                            --_instruction_pointer;

                        return InterpreterResult.OK;
                    }
                    else if (REGEX_ELSE.IsMatch(m.Groups["expression"].Value))
                        return InterpreterResult.OK;
                    else
                        switch_expr = topmost.switch_expr;

                    Union<InterpreterError, PARSABLE_EXPRESSION> case_expr = ProcessAsRawExpression(m.Groups["expression"].Value);
                    InterpreterResult process_success(bool success)
                    {
                        _switchselect_stack.TryPop(out topmost);
                        _switchselect_stack.Push((topmost.switch_expr, success));

                        if (!success)
                            if (MoveToEndOf(BlockStatementType.Switch) is InterpreterError error)
                                return error!;
                            else
                                --_instruction_pointer;

                        return InterpreterResult.OK;
                    }

                    if (case_expr.Is(out PARSABLE_EXPRESSION? expression))
                        if (expression is PARSABLE_EXPRESSION.ToExpression { Item1: EXPRESSION from, Item2: EXPRESSION to })
                        {
                            if (switch_expr is Variant sw)
                                return ProcessExpression(from).Match(err => err!, from =>
                                       ProcessExpression(to).Match(err => err!, to => process_success(from <= sw && sw <= to)));
                            else
                                return WellKnownError("error.invalid_case_range", expression);
                        }
                        else if (expression is PARSABLE_EXPRESSION.AnyExpression { Item: EXPRESSION expr })
                            return ProcessExpression(expr).Match(err => err!, expr => process_success(switch_expr is Variant sw ? expr.EqualsCaseInsensitive(sw) : expr.ToBoolean()));
                        else
                            return WellKnownError("error.invalid_case_expr", expression);

                    return (InterpreterError)case_expr;
                },
                [REGEX_CONTINUECASE] = _ =>
                {
                    if (!_switchselect_stack.TryPeek(out (Variant? switch_expr, bool) topmost))
                        return WellKnownError("error.unexpected_continuecase");
                    else if (MoveToEndOf(topmost.switch_expr is null ? BlockStatementType.Select : BlockStatementType.Switch) is InterpreterError error)
                        return error;
                    else if (!REGEX_CASE.IsMatch(CurrentLineContent))
                        _switchselect_stack.TryPop(out topmost);

                    return InterpreterResult.OK;
                },
            });

            foreach (AbstractStatementProcessor? proc in Interpreter.PluginLoader.StatementProcessors)
                if (proc is { Regex: Regex pat } sp && line.Match(pat, out Match _))
                    result ??= sp.ProcessStatement(this, line);

            return result;
        });

        private InterpreterError? ProcessExpressionStatement(string line) => Interpreter.Telemetry.Measure(TelemetryCategory.ProcessExpressionStatement, delegate
        {
            try
            {
                if (ProcessDeclarationModifiers(ref line, out DeclarationType declaration_type, out (char op, int amount)? enum_step) is { } err)
                    return err;

                ParserConstructor<PARSABLE_EXPRESSION>.ParserWrapper? provider = declaration_type is DeclarationType.None ? Interpreter.ParserProvider.ExpressionParser : Interpreter.ParserProvider.MultiDeclarationParser;
                PARSABLE_EXPRESSION? expression = provider.Parse(line).ParsedValue;

                MainProgram.PrintfDebugMessage("debug.au3thread.expr_statement", expression);

                if (declaration_type == DeclarationType.None)
                    return ProcessAssignmentStatement(expression, false).Match(Generics.id, _ => null);
                else if (expression is PARSABLE_EXPRESSION.MultiDeclarationExpression multi_decl)
                    return ProcessMultiDeclarationExpression(multi_decl, declaration_type, enum_step);
                else
                    return WellKnownError("error.invalid_multi_decl", line);
            }
            catch (Exception ex)
            {
                return Interpreter.Telemetry.Measure(TelemetryCategory.Exceptions, delegate
                {
                    if (Interpreter.CommandLineOptions.Verbosity > Verbosity.q)
                        return new InterpreterError(CurrentLocation, $"{Interpreter.CurrentUILanguage["error.unparsable_line", line, ex.Message]}\n\nStack trace:\n{ex.StackTrace}");
                    else
                        return WellKnownError("error.unparsable_line", line, ex.Message);
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

                    MainProgram.PrintfDebugMessage("debug.au3thread.raw_expr", parsed);

                    return parsed;
                }
                catch (Exception ex)
                {
                    return Interpreter.Telemetry.Measure(TelemetryCategory.Exceptions, () => WellKnownError("error.unparsable_line", expression, ex.Message));
                }
            });

        internal Union<InterpreterError, Variant> ProcessAsVariant(string expression)
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

        private InterpreterError? ProcessDeclarationModifiers(ref string line, out DeclarationType declaration_type, out (char op, int amount)? enum_step)
        {
            declaration_type = DeclarationType.None;
            enum_step = null;

            while (line.Match(REGEX_DECLARATION_MODIFIER, out Match m_modifier))
            {
                DeclarationType modifier = (DeclarationType)Enum.Parse(typeof(DeclarationType), m_modifier.Value, true);

                if (declaration_type.HasFlag(modifier))
                    return WellKnownError("error.duplicate_modifier", modifier);

                if (modifier is DeclarationType.Step)
                    if (line.Match(REGEX_ENUM_STEP, out Match m_step))
                    {
                        char op = '+';
                        int amount = int.Parse(m_step.Groups["step"].Value);

                        if (m_step.Groups["op"] is { Length: > 0, Value: string s })
                            op = s[0];

                        enum_step = (op, amount);
                    }
                    else
                        return WellKnownError("error.invalid_step", new string(line.TakeWhile(c => !char.IsWhiteSpace(c)).ToArray()));

                declaration_type |= modifier;
                line = line[m_modifier.Length..].TrimStart();
            }

            if (declaration_type.HasFlag(DeclarationType.Step) && !declaration_type.HasFlag(DeclarationType.Enum))
                return WellKnownError("error.unexpected_step");

            foreach ((DeclarationType m1, DeclarationType m2) in new[]
            {
                (DeclarationType.Dim, DeclarationType.Global),
                (DeclarationType.Dim, DeclarationType.Local),
                (DeclarationType.Dim, DeclarationType.Static),
                (DeclarationType.Enum, DeclarationType.Static),
                (DeclarationType.Local, DeclarationType.Global),
                (DeclarationType.Static, DeclarationType.Const),
            })
                if (declaration_type.HasFlag(m1) && declaration_type.HasFlag(m2))
                    return WellKnownError("error.incomplatible_modifiers", m1, m2);

            return null;
        }

        private InterpreterError? ProcessMultiDeclarationExpression(PARSABLE_EXPRESSION.MultiDeclarationExpression multi_decl, DeclarationType decltype, (char op, int amount)? enum_step) =>
            Interpreter.Telemetry.Measure(TelemetryCategory.ProcessDeclaration, delegate
            {
                InterpreterError? error = null;
                Variable? variable;

                foreach ((VARIABLE variable_ast, VARIABLE_DECLARATION declaration) in multi_decl.Item.Select(t => t.ToValueTuple()))
                {
                    error ??= ProcessVariableDeclaration(variable_ast, decltype);

                    switch (declaration)
                    {
                        case VARIABLE_DECLARATION.Scalar { Item: null }:
                            if (decltype.HasFlag(DeclarationType.Const))
                                return WellKnownError("error.uninitialized_constant", variable_ast);
                            else
                                break;
                        case VARIABLE_DECLARATION.Scalar { Item: FSharpOption<EXPRESSION> { Value: EXPRESSION expression } }:
                            {
                                // TODO : enum step handling

                                var assg_expr = (ASSIGNMENT_TARGET.NewVariableAssignment(variable_ast), OPERATOR_ASSIGNMENT.Assign, expression).ToTuple();
                                Union<InterpreterError, Variant> result = ProcessAssignmentStatement(PARSABLE_EXPRESSION.NewAssignmentExpression(assg_expr), true);

                                if (result.Is(out Variant value))
                                    MainProgram.PrintDebugMessage($"{variable_ast} = {value}");
                                else
                                    error ??= (InterpreterError)result;
                            }
                            break;
                        case VARIABLE_DECLARATION.Array { Item1: int size, Item2: FSharpList<EXPRESSION> items }:
                            if (size < 0)
                                return WellKnownError("error.invalid_array_size", variable_ast, size);
                            else if (items.Length > size)
                                return WellKnownError("error.too_many_array_items", variable_ast, size, items.Length);
                            else if (VariableResolver.TryGetVariable(variable_ast, VariableSearchScope.Global, out variable))
                            {
                                variable.Value = Variant.NewArray(size);

                                int index = 0;

                                foreach (EXPRESSION item in items)
                                {
                                    Union<InterpreterError, Variant> result = ProcessExpression(item);

                                    if (result.Is(out error))
                                        return error;

                                    variable.Value.TrySetIndexed(Interpreter, index, result);
                                    ++index;
                                }

                                break;
                            }
                            else
                                return WellKnownError("error.undeclared_variable", variable_ast);
                        case VARIABLE_DECLARATION.Map:
                            if (VariableResolver.TryGetVariable(variable_ast, VariableSearchScope.Global, out variable))
                            {
                                variable.Value = Variant.NewMap();

                                break;
                            }
                            else
                                return WellKnownError("error.undeclared_variable", variable_ast);
                        default:
                            return WellKnownError("error.not_yet_implemented", declaration);
                    }
                }

                return error;
            });

        private InterpreterError? ProcessVariableDeclaration(VARIABLE variable, DeclarationType decltype)
        {
            bool constant = decltype.HasFlag(DeclarationType.Const) || decltype.HasFlag(DeclarationType.Enum);
            bool global = decltype.HasFlag(DeclarationType.Global) && !decltype.HasFlag(DeclarationType.Local);

            if (decltype.HasFlag(DeclarationType.Static) && !global)
                return WellKnownError("error.not_yet_implemented", "static"); // TODO : static

            VariableScope scope = (global ? VariableResolver.GlobalRoot : null) ?? VariableResolver;

            // if (decltype.HasFlag(DeclarationType.Dim) || decltype == DeclarationType.None)
            //     global = CurrentFunction.IsMainFunction;

            if (scope.TryGetVariable(variable, VariableSearchScope.Global, out Variable? var))
            {
                if (!(var.IsGlobal && decltype.HasFlag(DeclarationType.Local))) // potential conflict
                    if (constant && !var.IsConst)
                        return WellKnownError("error.cannot_make_constant", var, var.DeclaredLocation);
                    else if (var.IsConst)
                        return WellKnownError("error.redefining_constant", var, var.DeclaredLocation);
            }

            scope.CreateVariable(CurrentLocation, variable, constant);

            return null;
        }

        private Union<InterpreterError, Variant> ProcessAssignmentStatement(PARSABLE_EXPRESSION assignment, bool force) =>
            Interpreter.Telemetry.Measure<Union<InterpreterError, Variant>>(TelemetryCategory.ProcessAssignment, delegate
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
                        return ProcessExpression(indexer.Item1).Match<Union<InterpreterError, Variant>>(err => err, target =>
                               ProcessExpression(indexer.Item2).Match<Union<InterpreterError, Variant>>(err => err, key =>
                               ProcessExpression(expression).Match<Union<InterpreterError, Variant>>(err => err, value =>
                               {
                                   if (target.TrySetIndexed(Interpreter, key, value))
                                       return value;

                                   return WellKnownError("error.invalid_index_assg", target.AssignedTo, key);
                               })));
                    case ASSIGNMENT_TARGET.MemberAssignemnt { Item: MEMBER_EXPRESSION.ExplicitMemberAccess { Item1: { } targ, Item2: { Item: string memb } } }:
                        return ProcessExpression(targ).Match<Union<InterpreterError, Variant>>(err => err, target =>
                               ProcessExpression(expression).Match<Union<InterpreterError, Variant>>(err => err, value =>
                               {
                                   if (target.TrySetMember(Interpreter, memb, value) || target.TrySetIndexed(Interpreter, memb, value))
                                       return value;

                                   return WellKnownError("error.invalid_index_assg", target.AssignedTo, memb);
                               }));
                    case ASSIGNMENT_TARGET.MemberAssignemnt { Item: MEMBER_EXPRESSION.ImplicitMemberAccess { Item: { Item: string member } } }:
                        {
                            if (_withcontext_stack.TryPeek(out Variable? variable))
                                return ProcessExpression(expression).Match<Union<InterpreterError, Variant>>(err => err, value =>
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

        private Union<InterpreterError, Variant> ProcessVariableAssignment(Variable variable, EXPRESSION expression)
        {
            foreach (VARIABLE v in expression.ReferencedVariables)
                if (!VariableResolver.HasVariable(v, VariableSearchScope.Global))
                    return WellKnownError("error.undeclared_variable", v);

            Union<InterpreterError, Variant> value = ProcessExpression(expression);

            if (value.Is(out Variant variant))
                (variable.ReferencedVariable ?? variable).Value = variant;

            return value;
        }

        private Union<InterpreterError, Variant> ProcessExpression(EXPRESSION? expression) =>
            Interpreter.Telemetry.Measure(TelemetryCategory.EvaluateExpression, delegate
            {
                Union<InterpreterError, Variant> value = expression switch
                {
                    null => Variant.Null,
                    EXPRESSION.Literal { Item: LITERAL literal } => ProcessLiteral(literal),
                    EXPRESSION.Variable { Item: VARIABLE variable } => ProcessVariable(variable),
                    EXPRESSION.Macro { Item: MACRO macro } => ProcessMacro(macro),
                    EXPRESSION.FunctionName { Item: { Item: string func_name } } =>
                        Interpreter.ScriptScanner.TryResolveFunction(func_name) is ScriptFunction func
                        ? (Union<InterpreterError, Variant>)Variant.FromFunction(func)
                        : (Union<InterpreterError, Variant>)WellKnownError("error.unresolved_func", func_name),
                    EXPRESSION.Unary { Item: Tuple<OPERATOR_UNARY, EXPRESSION> unary } => ProcessUnary(unary.Item1, unary.Item2),
                    EXPRESSION.Binary { Item: Tuple<EXPRESSION, OPERATOR_BINARY, EXPRESSION> binary } => ProcessBinary(binary.Item1, binary.Item2, binary.Item3),
                    EXPRESSION.Ternary { Item: Tuple<EXPRESSION, EXPRESSION, EXPRESSION> ternary } => ProcessTernary(ternary.Item1, ternary.Item2, ternary.Item3),
                    EXPRESSION.Member { Item: MEMBER_EXPRESSION member } => ProcessMember(member),
                    EXPRESSION.Indexer { Item: Tuple<EXPRESSION, EXPRESSION> indexer } => ProcessIndexer(indexer.Item1, indexer.Item2),
                    EXPRESSION.FunctionCall { Item: FUNCCALL_EXPRESSION funccall } => ProcessFunctionCall(funccall),
                    _ => WellKnownError("error.not_yet_implemented", expression),
                };

                if (value.Is(out Variant v))
                    MainProgram.PrintfDebugMessage("debug.au3thread.expression", expression, v.ToDebugString(Interpreter));

                return value;
            });

        private Union<InterpreterError, Variant> ProcessIndexer(EXPRESSION expr, EXPRESSION index) =>
            ProcessExpression(expr).Match<Union<InterpreterError, Variant>>(err => err, @object =>
            ProcessExpression(index).Match<Union<InterpreterError, Variant>>(err => err, key =>
            {
                if (@object.TryGetIndexed(Interpreter, key, out Variant value))
                    return value;

                return WellKnownError("error.invalid_index", key);
            }));

        private Union<InterpreterError, Variant> ProcessMember(MEMBER_EXPRESSION expr)
        {
            Union<InterpreterError, Variant> result = WellKnownError("error.not_yet_implemented", expr);
            string? member = null;

            switch (expr)
            {
                case MEMBER_EXPRESSION.ExplicitMemberAccess { Item1: { } objexpr, Item2: { Item: string m } }:
                    result = ProcessExpression(objexpr);
                    member = m;

                    break;
                case MEMBER_EXPRESSION.ImplicitMemberAccess { Item: { Item: string m } }:
                    if (_withcontext_stack.TryPeek(out Variable? variable))
                    {
                        result = variable.Value;
                        member = m;

                        break;
                    }
                    else
                        return WellKnownError("error.invalid_with_access", m);
            }

            if (result.Is(out Variant value) && member is string)
            {
                if (value.TryGetMember(Interpreter, member, out Variant v))
                    result = v;
                else if (value.TryGetIndexed(Interpreter, member, out v))
                    result = v;
                else if (member.Equals("length", StringComparison.InvariantCultureIgnoreCase))
                    result = (Variant)value.Length;
                else
                    return WellKnownError("error.unknown_member", member);
            }
            
            return result;
        }

        private Union<InterpreterError, Variant> ProcessUnary(OPERATOR_UNARY op, EXPRESSION expr)
        {
            Union<InterpreterError, Variant> result = ProcessExpression(expr);

            if (result.Is(out Variant value))
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

        private Union<InterpreterError, Variant> ProcessBinary(EXPRESSION expr1, OPERATOR_BINARY op, EXPRESSION expr2)
        {
            InterpreterError? evaluate(EXPRESSION expr, out Variant target)
            {
                Union<InterpreterError, Variant> result = ProcessExpression(expr);

                target = Variant.Zero;

                if (result.Is(out InterpreterError? error))
                    return error;

                target = (Variant)result;

                return null;
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

        private Union<InterpreterError, Variant> ProcessTernary(EXPRESSION expr1, EXPRESSION expr2, EXPRESSION expr3) =>
            ProcessExpression(expr1).Match<Union<InterpreterError, Variant>>(e => e, cond => ProcessExpression(cond.ToBoolean() ? expr2 : expr3));

        private Union<InterpreterError, Variant> ProcessFunctionCall(FUNCCALL_EXPRESSION funccall)
        {
            Union<Variant[], InterpreterError> ProcessRawArguments(FSharpList<EXPRESSION> raw_args)
            {
                Variant[] arguments = new Variant[raw_args.Length];
                int i = 0;

                foreach (EXPRESSION arg in raw_args)
                {
                    Union<InterpreterError, Variant>? res = ProcessExpression(arg);

                    if (res.Is(out Variant value))
                        arguments[i++] = value;
                    else
                        return (InterpreterError)res;
                }

                return arguments;
            }

            switch (funccall)
            {
                case FUNCCALL_EXPRESSION.DirectFunctionCall { Item1: { Item: string func_name }, Item2: var raw_args }:
                    if (Interpreter.ScriptScanner.TryResolveFunction(func_name) is ScriptFunction func)
                        return ProcessRawArguments(raw_args).Match<Union<InterpreterError, Variant>>(args => Call(func, args), err => err);
                    else
                        return WellKnownError("error.unresolved_func", func_name);
                case FUNCCALL_EXPRESSION.MemberCall member_call:
                    throw new NotImplementedException();
            }

            return WellKnownError("error.not_yet_implemented", funccall);
        }

        private Union<InterpreterError, Variant> ProcessMacro(MACRO macro)
        {
            string name = macro.Name;

            foreach (AbstractMacroProvider provider in Interpreter.PluginLoader.MacroProviders)
                if (provider.ProvideMacroValue(this, name, out Variant? value) && value.HasValue)
                    return value.Value;

            return WellKnownError("error.unknown_macro", macro.Name);
        }

        private Union<InterpreterError, Variant> ProcessVariable(VARIABLE variable)
        {
            if (variable == VARIABLE.Discard)
                return WellKnownError("error.invalid_discard_access", VARIABLE.Discard, FrameworkMacros.MACRO_DISCARD);

            if (VariableResolver.TryGetVariable(variable, VariableSearchScope.Global, out Variable? var))
            {
                Variant value = var.Value;

                if (value.AssignedTo != var)
                    var.Value = value.AssignTo(var); // update parent var

                return value;
            }
            else
                return WellKnownError("error.undeclared_variable", variable);
        }

        private Variant ProcessLiteral(LITERAL literal) => Variant.FromLiteral(literal); // TODO : on error throw ?

        private InterpreterResult? UseExternalLineProcessors(string line)
        {
            foreach (AbstractLineProcessor proc in Interpreter.PluginLoader.LineProcessors)
                if (proc.CanProcessLine(line) && Interpreter.Telemetry.Measure(TelemetryCategory.ExternalProcessor, () => proc.ProcessLine(this, line)) is { } res)
                    return res;

            return null;
        }

        private InterpreterError WellKnownError(string key, params object?[] args) => InterpreterError.WellKnown(CurrentLocation, key, args);
    }

    [Flags]
    public enum DeclarationType
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
