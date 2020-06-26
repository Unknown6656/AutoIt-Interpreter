using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

using Piglet.Parser.Configuration.Generic;

using Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework;
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

        public SourceLocation? CurrentLocation => CurrentFrame switch {
            AU3CallFrame f => f.CurrentLocation,
            _ => SourceLocation.Unknown
        };

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

            Program.PrintDebugMessage($"Created thread {this}");
        }

        public Union<InterpreterError, Variant> Start(ScriptFunction function, Variant[] args)
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
        }

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
            if (IsDisposed)
                return;
            else
                IsDisposed = true;

            Interpreter.RemoveThread(this);
            _callstack.TryPop(out _);

            Program.PrintDebugMessage($"Disposed thread {this}");

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

            if (CurrentFunction.IsMainFunction && script.LoadScript(this) is InterpreterError load_error)
                result = load_error;

            (int min_argc, int max_argc) = CurrentFunction.ParameterCount;

            if (args.Length < min_argc)
                return InterpreterError.WellKnown(CurrentThread.CurrentLocation, "error.not_enough_args", min_argc, args.Length);
            else if (args.Length > max_argc)
                return InterpreterError.WellKnown(CurrentThread.CurrentLocation, "error.too_many_args", max_argc, args.Length);
            else if (result.Is<Variant>())
                if (CurrentThread.IsRunning)
                {
                    Program.PrintDebugMessage($"Executing {CurrentFunction} ...");

                    result = InternalExec(args);
                }

            if (CurrentFunction.IsMainFunction && result.Is<Variant>() && script.UnLoadScript(this) is InterpreterError unload_error)
                result = unload_error;

            if (result.Is(out Variant @return))
                ReturnValue = @return;

            return result;
        }

        public Union<InterpreterError, Variant> Call(ScriptFunction function, Variant[] args) => CurrentThread.Call(function, args);

        public Variant SetError(int error, int extended = 0, in Variant @return = default)
        {
            Interpreter.ErrorCode = error;

            return SetExtended(extended, in @return);
        }

        public Variant SetExtended(int extended, in Variant @return = default)
        {
            Interpreter.ExtendedErrorCode = extended;

            return @return;
        }

        public void Print(Variant value) => Interpreter.Print(this, value);

        public void Print(object? value) => Interpreter.Print(this, value);

        public override string ToString() => $"[0x{CurrentThread.ThreadID:x4}]";
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
            FunctionReturnValue result = ((NativeFunction)CurrentFunction).Execute(this, args);

            if (result.IsFatal(out InterpreterError? fatal))
                return fatal;
            else if (result.IsSuccess(out Variant variant))
                return variant;
            else if (result.IsError(out variant, out int error, out int? extended))
                return SetError(error, extended ?? 0, in variant);
            else
                throw new InvalidOperationException("Return value could not be processed");
        }

        public override string ToString() => $"{base.ToString()} native call frame";
    }

    public sealed class AU3CallFrame
        : CallFrame
    {
        private volatile int _instruction_pointer = 0;
        private List<(SourceLocation LineLocation, string LineContent)> _line_cache;


        public SourceLocation CurrentLocation => _instruction_pointer < 0 ? CurrentFunction.Location : _line_cache[_instruction_pointer].LineLocation;

        public string CurrentLineContent => _instruction_pointer < 0 ? "<unknown>" : _line_cache[_instruction_pointer].LineContent;


        internal AU3CallFrame(AU3Thread thread, CallFrame? caller, AU3Function function, Variant[] args)
            : base(thread, caller, function, args)
        {
            _line_cache = function.Lines.ToList();
            _instruction_pointer = 0;
        }

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

                    if (result.Is(out InterpreterError error))
                        return error;
                    else if (result.Is(out Variant value))
                        args[i] = value;
                }

                param_var.Value = args[i];
            }

            _instruction_pointer = 0;

            while (_instruction_pointer < _line_cache.Count && CurrentThread.IsRunning)
                if (ParseCurrentLine()?.OptionalError is InterpreterError error)
                    return error;
                else if (!MoveNext())
                    break;

            return ReturnValue;
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
            if ((CurrentFunction as AU3Function)?.JumpLabels[jump_label] is JumpLabel label)
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

        public InterpreterResult? ParseCurrentLine()
        {
            (SourceLocation loc, string line) = _line_cache[_instruction_pointer];
            InterpreterResult? result = null;

            if (string.IsNullOrWhiteSpace(line))
                return InterpreterResult.OK;

            Program.PrintDebugMessage($"({loc}) {line}");

            result ??= ProcessDirective(line);
            result ??= ProcessStatement(line);
            result ??= UseExternalLineProcessors(line);
            result ??= ProcessExpressionStatement(line);

            if (Interpreter.CommandLineOptions.IgnoreErrors && !(result?.IsOK ?? true))
                result = null;

            return result ?? InterpreterResult.OK;
        }

        private InterpreterResult? ProcessDirective(string directive)
        {
            if (!directive.StartsWith('#'))
                return null;

            directive = directive[1..];

            if (directive.Match(/*language=regex*/@"^include?\s+(?<open>[""'<])(?<path>(?:(?!\k<close>).)+)(?<close>[""'>])$", out ReadOnlyIndexer<string, string>? g))
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
                    else if (Call(script.MainFunction, Array.Empty<Variant>()).Is(out InterpreterError err))
                        return err;
                    else
                        return InterpreterResult.OK;
                });
            }

            InterpreterResult? result = null;

            foreach (AbstractDirectiveProcessor? proc in Interpreter.PluginLoader.DirectiveProcessors)
                result ??= proc?.ProcessDirective(this, directive);

            return result?.IsOK ?? false ? null : WellKnownError("error.unparsable_dirctive", directive);
        }

        public override string ToString() => $"{base.ToString()} {CurrentLocation}";






        // TODO
        private readonly ConcurrentStack<(BlockStatementType BlockType, SourceLocation Location)> _blockstatement_stack = new ConcurrentStack<(BlockStatementType, SourceLocation)>();
        private readonly ConcurrentStack<Variable> _withcontext_stack = new ConcurrentStack<Variable>();
        private readonly ConcurrentStack<int> _forloop_eip_stack = new ConcurrentStack<int>();
        private readonly ConcurrentStack<bool> _if_stack = new ConcurrentStack<bool>();


        private InterpreterResult PushBlockStatement(BlockStatementType statement)
        {
            _blockstatement_stack.Push((statement, CurrentLocation));

            return InterpreterResult.OK;
        }

        private InterpreterError? ExpectBlockStatementType(params BlockStatementType[] expected)
        {
            if (expected.Length == 0)
                return null;

            _blockstatement_stack.TryPeek(out (BlockStatementType type, SourceLocation) statement);

            if (!expected.Contains(statement.type))
                return WellKnownError("error.expected_statement_block", statement.type, string.Join(", ", expected));

            return null;
        }

        //private InterpreterError? PopBlockStatement(params BlockStatementType[] accepted)
        //{
        //    if (accepted.Length == 0)
        //        _blockstatement_stack.TryPop(out _);
        //    else
        //    {
        //        _blockstatement_stack.TryPop(out (BlockStatementType type, SourceLocation loc) statement);

        //        if (!accepted.Contains(statement.type))
        //            return WellKnownError("error.no_matching_close", statement.type, statement.loc);
        //    }

        //    return null;
        //}

        private const string REGEX_GOTO = /*language=regex*/@"^goto\s+(?<label>.+)$";
        private const string REGEX_WHILE = /*language=regex*/@"^while\s+(?<expression>.+)$";
        private const string REGEX_WEND = /*language=regex*/@"^wend$";
        private const string REGEX_NEXT = /*language=regex*/@"^next$";
        private const string REGEX_EXIT = /*language=regex*/@"^exit(\b\s*(?<code>.+))?$";
        private const string REGEX_RETURN = /*language=regex*/@"^return(\b\s*(?<value>.+))?$";
        private const string REGEX_FOR = /*language=regex*/@"^for\s+.+$";
        private const string REGEX_FORTO = /*language=regex*/@"^for\s+(?<start>.+)\s+to\s+(?<stop>.+?)(\s+step\s+(?<step>.+))?$";
        private const string REGEX_FORIN = /*language=regex*/@"^for\s+(?<variable>.+)\s+in\s+(?<expression>.+)$";
        private const string REGEX_WITH = /*language=regex*/@"^with\s+(?<variable>.+)$";
        private const string REGEX_ENDWITH = /*language=regex*/@"^endwith$";
        private const string REGEX_REDIM = /*language=regex*/@"^redim\s+(?<expression>.+)$";
        private const string REGEX_DO = /*language=regex*/@"^do$";
        private const string REGEX_UNTIL = /*language=regex*/@"^until\s+(?<expression>.+)$";
        private const string REGEX_IF = /*language=regex*/@"^(?<elif>else)?if\s+(?<condition>.+)\s+then$";
        private const string REGEX_ELSE = /*language=regex*/@"^else$";
        private const string REGEX_ENDIF = /*language=regex*/@"^endif$";

        private InterpreterResult? ProcessStatement(string line)
        {
            InterpreterResult? result = line.Match(null, new Dictionary<string, Func<Match, InterpreterResult?>>
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
                    Union<InterpreterError, PARSABLE_EXPRESSION> start = ProcessRawExpression(m.Groups["start"].Value);
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
                                Variable temp = VariableResolver.CreateTemporaryVariable();
                                string last = '$' + temp.Name;

                                temp.Value = Variant.False;

                                _line_cache.RemoveAt(_instruction_pointer);
                                _line_cache.InsertRange(_instruction_pointer, new[] {
                                    (CurrentLocation, $"If {last} Then"),
                                    (CurrentLocation, $"ExitLoop"),
                                    (CurrentLocation, $"Else"),
                                    (CurrentLocation, $"{counter} += {step}"),
                                    (CurrentLocation, $"If {counter} == {m.Groups["stop"]} Then"),
                                    (CurrentLocation, $"{last} = True"),
                                    (CurrentLocation, $"EndIf"),
                                    (CurrentLocation, $"EndIf"),
                                    (CurrentLocation, $"Until False"),
                                });
                                _instruction_pointer = eip_for;
                                _line_cache[_instruction_pointer] = (CurrentLocation, "Do");
                                _instruction_pointer--;

                                return InterpreterResult.OK;
                            }
                        }

                        return WellKnownError("error.no_matching_close", BlockStatementType.For, loc_for);
                    }
                    else
                        return WellKnownError("error.malformed_for_expr", m.Groups["start"]);
                },
                [REGEX_FORIN] = m =>
                {
                    throw new NotImplementedException();
                },
                [REGEX_WITH] = m =>
                {
                    Union<InterpreterError, PARSABLE_EXPRESSION> parsed = ProcessRawExpression(m.Groups["variable"].Value);

                    if (parsed.Is(out InterpreterError? error))
                        return error;
                    else if (parsed.Is(out PARSABLE_EXPRESSION? pexpr) && pexpr is PARSABLE_EXPRESSION.AnyExpression
                    {
                        Item: EXPRESSION.Variable
                        {
                            Item: VARIABLE variable
                        }
                    } && VariableResolver.TryGetVariable(variable, out Variable? withctx) && !withctx.IsConst)
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
                    throw new NotImplementedException();
                },
                [REGEX_DO] = _ => PushBlockStatement(BlockStatementType.Do),
                [REGEX_UNTIL] = m =>
                {
                    _blockstatement_stack.TryPeek(out (BlockStatementType type, SourceLocation loc) topmost);

                    if (topmost.type != BlockStatementType.Do)
                        return WellKnownError("error.unexpected_until");

                    Union<InterpreterError, Variant> result = ProcessAsVariant(m.Groups["expression"].Value);

                    if (result.Is(out InterpreterError? error))
                        return error;
                    else if (!result.As<Variant>().ToBoolean())
                        MoveAfter(topmost.loc);
                    else
                        _blockstatement_stack.TryPop(out _);

                    return InterpreterResult.OK;
                },
                [REGEX_WHILE] = m =>
                {
                    Union<InterpreterError, Variant> result = ProcessAsVariant(m.Groups["expression"].Value);

                    if (result.Is(out InterpreterError? error))
                        return error;

                    if (!result.As<Variant>().ToBoolean())
                        return MoveToEndOf(BlockStatementType.While);
                    else
                        PushBlockStatement(BlockStatementType.While);

                    return InterpreterResult.OK;
                },
                [REGEX_WEND] = _ =>
                {
                    _blockstatement_stack.TryPop(out (BlockStatementType type, SourceLocation loc) topmost);

                    if (topmost.type != BlockStatementType.While)
                        return WellKnownError("error.unexpected_wend");

                    MoveBefore(topmost.loc);

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

                        if (!cond)
                            MoveToEndOf(BlockStatementType.If);
                    }

                    return InterpreterResult.OK;
                },
                [REGEX_ELSE] = _ =>
                {
                    if (_if_stack.TryPop(out bool cond))
                    {
                        if (cond)
                            MoveToEndOf(BlockStatementType.If);
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


                //[/*language=regex*/@"^endswitch$"] = _ => PopBlockStatement(BlockStatementType.Switch, BlockStatementType.Case),
                //[/*language=regex*/@"^endselect$"] = _ => PopBlockStatement(BlockStatementType.Select, BlockStatementType.Case),
                //[/*language=regex*/@"^continueloop\s*(?<level>\d+)?\s*$"] = m =>
                //{
                //    int level = int.TryParse(m.Groups["level"].Value, out int l) ? l : 1;
                //    InterpreterResult? result = InterpreterResult.OK;
                //
                //    while (level-- > 1)
                //        result = PopBlockStatement(BlockStatementType.For, BlockStatementType.ForIn, BlockStatementType.While, BlockStatementType.Do);
                //
                //    // TODO : continue
                //
                //
                //    throw new NotImplementedException();
                //},
                //[/*language=regex*/@"^exitloop\s*(?<level>\d+)?\s*$"] = m =>
                //{
                //    int level = int.TryParse(m.Groups["level"].Value, out int l) ? l : 1;
                //    InterpreterResult? result = InterpreterResult.OK;
                //
                //    while (level-- > 0)
                //        result = PopBlockStatement(BlockStatementType.For, BlockStatementType.ForIn, BlockStatementType.While, BlockStatementType.Do);
                //
                //    throw new NotImplementedException();
                //
                //    return result;
                //},
            });

            // TODO

            foreach (AbstractStatementProcessor? proc in Interpreter.PluginLoader.StatementProcessors)
                if (proc is { Regex: string pat } sp && line.Match(pat, out Match _))
                    result ??= sp.ProcessStatement(this, line);

            return result;
        }

        private InterpreterResult? MoveToEndOf(BlockStatementType type)
        {
            SourceLocation init = CurrentLocation;
            int depth = 1;

            if (type is BlockStatementType.While)
                while (MoveNext())
                {
                    if (CurrentLineContent.Match(REGEX_WEND, out Match _))
                        --depth;
                    else if (CurrentLineContent.Match(REGEX_WHILE, out Match _))
                        ++depth;

                    if (depth == 0)
                        break;
                }
            else if (type is BlockStatementType.If)
                while (MoveNext())
                {
                    bool @if = false;

                    if (CurrentLineContent.Match(REGEX_IF, out Match m))
                    {
                        @if = true;

                        if (m.Groups["elif"].Length > 0)
                            --depth;
                        else
                            ++depth;
                    }
                    else if (CurrentLineContent.Match(REGEX_ELSE, out Match _))
                        --depth;
                    else if (CurrentLineContent.Match(REGEX_ENDIF, out Match _))
                        --depth;

                    if (depth == 0)
                    {
                        if (@if)
                            --_instruction_pointer;

                        break;
                    }
                }

            // TODO : other block types

            else
                return WellKnownError("error.not_yet_implemented", type);

            return depth == 0 ? InterpreterResult.OK : WellKnownError("error.no_matching_close", type, init);
        }

        private InterpreterError? ProcessExpressionStatement(string line)
        {
            try
            {
                if (ProcessDeclarationModifiers(ref line, out DeclarationType declaration_type, out (char op, int amount)? enum_step) is { } err)
                    return err;

                ParserConstructor<PARSABLE_EXPRESSION>.ParserWrapper? provider = declaration_type is DeclarationType.None ? ParserProvider.ExpressionParser : ParserProvider.MultiDeclarationParser;
                PARSABLE_EXPRESSION? expression = provider.Parse(line).ParsedValue;

                Program.PrintDebugMessage($"Parsed \"{expression}\"");

                if (declaration_type == DeclarationType.None)
                    return ProcessAssignmentStatement(expression, false).Match(Generics.id, _ => null);
                else if (expression is PARSABLE_EXPRESSION.MultiDeclarationExpression multi_decl)
                    return ProcessMultiDeclarationExpression(multi_decl, declaration_type, enum_step);
                else
                    return WellKnownError("error.invalid_multi_decl", line);
            }
            catch (Exception ex)
            {
                return WellKnownError("error.unparsable_line", line, ex.Message);
            }
        }

        private Union<InterpreterError, PARSABLE_EXPRESSION> ProcessRawExpression(string expression)
        {
            try
            {
                ParserConstructor<PARSABLE_EXPRESSION>.ParserWrapper? provider = ParserProvider.ExpressionParser;

                return provider.Parse(expression).ParsedValue;
            }
            catch (Exception ex)
            {
                return WellKnownError("error.unparsable_line", expression, ex.Message);
            }
        }

        internal Union<InterpreterError, Variant> ProcessAsVariant(string expression)
        {
            Union<InterpreterError, PARSABLE_EXPRESSION>? result = ProcessRawExpression(expression);

            if (result.Is(out PARSABLE_EXPRESSION.AnyExpression? any))
                return ProcessExpression(any.Item);
            else
                return (InterpreterError?)result ?? WellKnownError("error.unparsable_line", expression);
        }

        private InterpreterError? ProcessDeclarationModifiers(ref string line, out DeclarationType declaration_type, out (char op, int amount)? enum_step)
        {
            declaration_type = DeclarationType.None;
            enum_step = null;

            while (line.Match(@"^(local|static|global|const|dim|enum|step)\b", out Match m_modifier))
            {
                DeclarationType modifier = (DeclarationType)Enum.Parse(typeof(DeclarationType), m_modifier.Value, true);

                if (declaration_type.HasFlag(modifier))
                    return WellKnownError("error.duplicate_modifier", modifier);

                if (modifier is DeclarationType.Step)
                    if (line.Match(@"^(?<op>[+\-*]?)(?<step>\d+)\b", out Match m_step))
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

        private InterpreterError? ProcessMultiDeclarationExpression(PARSABLE_EXPRESSION.MultiDeclarationExpression multi_decl, DeclarationType decltype, (char op, int amount)? enum_step)
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
                                Program.PrintDebugMessage($"{variable_ast} = {value}");
                            else
                                error ??= (InterpreterError)result;
                        }
                        break;
                    case VARIABLE_DECLARATION.Array { Item1: int size, Item2: FSharpList<EXPRESSION> items }:
                        if (size < 0)
                            return WellKnownError("error.invalid_array_size", variable_ast, size);
                        else if (items.Length > size)
                            return WellKnownError("error.too_many_array_items", variable_ast, size, items.Length);
                        else if (VariableResolver.TryGetVariable(variable_ast, out variable))
                        {
                            variable.Value = Variant.NewArray(size);

                            int index = 0;

                            foreach (EXPRESSION item in items)
                            {
                                Union<InterpreterError, Variant> result = ProcessExpression(item);

                                if (result.Is(out error))
                                    return error;

                                variable.Value.TrySetIndexed(index, result);
                                ++index;
                            }

                            break;
                        }
                        else
                            return WellKnownError("error.undeclared_variable", variable_ast);
                    case VARIABLE_DECLARATION.Map { }: // TODO
                        if (VariableResolver.TryGetVariable(variable_ast, out variable))
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
        }

        private InterpreterError? ProcessVariableDeclaration(VARIABLE variable, DeclarationType decltype)
        {
            bool constant = decltype.HasFlag(DeclarationType.Const) || decltype.HasFlag(DeclarationType.Enum);
            bool global = decltype.HasFlag(DeclarationType.Global) && !decltype.HasFlag(DeclarationType.Local);

            if (decltype.HasFlag(DeclarationType.Static) && !global)
                return WellKnownError("error.not_yet_implemented", "static"); // TODO : static

            VariableScope scope = (global ? VariableResolver.GlobalRoot : null) ?? VariableResolver;

            // if (decltype.HasFlag(DeclarationType.Dim) || decltype == DeclarationType.None)
            //     global = CurrentFunction.IsMainFunction;

            if (scope.TryGetVariable(variable, out Variable? var))
            {
                if (!(var.IsGlobal && decltype.HasFlag(DeclarationType.Local))) // potential conflict
                    if (constant && !var.IsConst)
                        return WellKnownError("error.cannot_make_constant", var, var.DeclaredLocation);
                    else if (var.IsConst)
                        return WellKnownError("error.redefining_constant", var, var.DeclaredLocation);
            }
            else
                scope.CreateVariable(CurrentLocation, variable, constant);

            return null;
        }

        private Union<InterpreterError, Variant> ProcessAssignmentStatement(PARSABLE_EXPRESSION assignment, bool force)
        {
            (ASSIGNMENT_TARGET target, EXPRESSION expression) = Cleanup.CleanUpExpression(assignment);

            switch (target)
            {
                case ASSIGNMENT_TARGET.VariableAssignment { Item: VARIABLE variable }:
                    {
                        if (VariableResolver.TryGetVariable(variable.Name, out Variable? target_variable))
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
                               if (target.TrySetIndexed(key, value))
                                   return value;
                           
                               return WellKnownError("error.invalid_index_assg", target.AssignedTo, key);
                           })));
                case ASSIGNMENT_TARGET.MemberAssignemnt { Item: MEMBER_EXPRESSION.ExplicitMemberAccess { Item1: { } targ, Item2: { Item: string memb } } }:
                    return ProcessExpression(targ).Match<Union<InterpreterError, Variant>>(err => err, target =>
                           ProcessExpression(expression).Match<Union<InterpreterError, Variant>>(err => err, value =>
                           {
                               if (target.TrySetIndexed(memb, value))
                                   return value;

                               return WellKnownError("error.invalid_index_assg", target.AssignedTo, memb);
                           }));
                case ASSIGNMENT_TARGET.MemberAssignemnt { Item: MEMBER_EXPRESSION.ImplicitMemberAccess { Item: { Item: string member } } }:
                    {
                        if (_withcontext_stack.TryPeek(out Variable? variable))
                            return ProcessExpression(expression).Match<Union<InterpreterError, Variant>>(err => err, value =>
                            {
                                if (variable.Value.TrySetIndexed(member, value))
                                    return value;

                                return WellKnownError("error.invalid_index_assg", variable, member);
                            });
                        else
                            return WellKnownError("error.invalid_with_access", member);
                    }
                default:
                    return WellKnownError("error.invalid_assignment_target", target);
            }
        }

        private Union<InterpreterError, Variant> ProcessVariableAssignment(Variable variable, EXPRESSION expression)
        {
            foreach (VARIABLE v in expression.ReferencedVariables)
                if (!VariableResolver.HasVariable(v))
                    return WellKnownError("error.undeclared_variable", v);

            Union<InterpreterError, Variant> value = ProcessExpression(expression);

            if (value.Is(out Variant variant))
                (variable.ReferencedVariable ?? variable).Value = variant;

            return value;
        }

        private Union<InterpreterError, Variant> ProcessExpression(EXPRESSION? expression)
        {
            switch (expression)
            {
                case null:
                    return Variant.Null;
                case EXPRESSION.Literal literal:
                    return ProcessLiteral(literal.Item);
                case EXPRESSION.Variable variable:
                    return ProcessVariable(variable.Item);
                case EXPRESSION.Macro macro:
                    return ProcessMacro(macro.Item);
                case EXPRESSION.Unary unary:
                    {
                        (OPERATOR_UNARY op, EXPRESSION expr) = unary.Item.ToValueTuple();

                        return ProcessUnary(op, expr);
                    }
                case EXPRESSION.Binary binary:
                    {
                        (EXPRESSION expr1, OPERATOR_BINARY op, EXPRESSION expr2) = binary.Item.ToValueTuple();

                        return ProcessBinary(expr1, op, expr2);
                    }
                case EXPRESSION.Ternary ternary:
                    {
                        (EXPRESSION expr1, EXPRESSION expr2, EXPRESSION expr3) = ternary.Item.ToValueTuple();

                        return ProcessTernary(expr1, expr2, expr3);
                    }
                case EXPRESSION.Member member:
                    return ProcessMember(member.Item);
                case EXPRESSION.Indexer indexer:
                    {
                        (EXPRESSION expr, EXPRESSION index) = indexer.Item.ToValueTuple();

                        return ProcessIndexer(expr, index);
                    }
                case EXPRESSION.FunctionCall funccall:
                    return ProcessFunctionCall(funccall.Item);
            }

            return WellKnownError("error.not_yet_implemented", expression);
        }

        private Union<InterpreterError, Variant> ProcessIndexer(EXPRESSION expr, EXPRESSION index) =>
            ProcessExpression(expr).Match<Union<InterpreterError, Variant>>(err => err, collection =>
            ProcessExpression(index).Match<Union<InterpreterError, Variant>>(err => err, key =>
            {
                if (collection.TryGetIndexed(key, out Variant value))
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
                if (value.TryGetIndexed(member, out Variant v))
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

            if (VariableResolver.TryGetVariable(variable, out Variable? var))
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
            foreach (AbstractLineProcessor? proc in Interpreter.PluginLoader.LineProcessors)
                if ((proc?.CanProcessLine(line) ?? false) && proc?.ProcessLine(this, line) is { } res)
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
        For,
        While,
        Do,
        If,
        // With,
        // Select,
        // Switch,
        // Case,
    }
}
