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

        public Union<Variant, InterpreterError> Start(ScriptFunction function, Variant[] args)
        {
            if (_running)
                return InterpreterError.WellKnown(CurrentLocation, "error.thread_already_running", ThreadID);
            else
                _running = true;

            Union<Variant, InterpreterError> result = Call(function, args);

            _running = false;

            if (_override_exitcode is int code)
                return Variant.FromNumber(code);

            return result;
        }

        public Union<Variant, InterpreterError> Call(ScriptFunction function, Variant[] args)
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

            Union<Variant, InterpreterError> result = frame.Execute(args);

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
    public abstract class CallFrame
        : IDisposable
    {
        public AU3Thread CurrentThread { get; }

        public ScriptFunction CurrentFunction { get; }

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

        protected abstract Union<Variant, InterpreterError> InternalExec(Variant[] args);

        internal Union<Variant, InterpreterError> Execute(Variant[] args)
        {
            Union<Variant, InterpreterError> result = Variant.Zero;
            ScannedScript script = CurrentFunction.Script;

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

        public Union<Variant, InterpreterError> Call(ScriptFunction function, Variant[] args) => CurrentThread.Call(function, args);

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

        protected override Union<Variant, InterpreterError> InternalExec(Variant[] args) => (CurrentFunction as NativeFunction)?.Execute(this, args) ?? ReturnValue;

        public override string ToString() => $"{base.ToString()} native call frame";
    }

    public sealed class AU3CallFrame
        : CallFrame
    {
        private volatile int _instruction_pointer = 0;
        private (SourceLocation LineLocation, string LineContent)[] _line_cache;


        public SourceLocation CurrentLocation => _instruction_pointer < 0 ? CurrentFunction.Location : _line_cache[_instruction_pointer].LineLocation;

        public string CurrentLineContent => _instruction_pointer < 0 ? "<unknown>" : _line_cache[_instruction_pointer].LineContent;


        internal AU3CallFrame(AU3Thread thread, CallFrame? caller, AU3Function function, Variant[] args)
            : base(thread, caller, function, args)
        {
            _line_cache = function.Lines;
            _instruction_pointer = 0;
        }

        protected override Union<Variant, InterpreterError> InternalExec(Variant[] args)
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
                    Union<Variant, InterpreterError> result = ProcessExpression(expr);

                    if (result.Is(out InterpreterError error))
                        return error;
                    else if (result.Is(out Variant value))
                        args[i] = value;
                }

                param_var.Value = args[i];
            }

            _instruction_pointer = 0;

            while (_instruction_pointer < _line_cache.Length && CurrentThread.IsRunning)
                if (ParseCurrentLine()?.OptionalError is InterpreterError error)
                    return error;
                else if (!MoveNext())
                    break;

            return ReturnValue;
        }

        private bool MoveNext()
        {
            if (_instruction_pointer < _line_cache.Length)
            {
                ++_instruction_pointer;

                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Copies the given value into the <see cref="CallFrame.ReturnValue"/>-field, and moves the instruction pointer to the end.
        /// </summary>
        /// <param name="value">Return value</param>
        public void Return(Variant value)
        {
            _instruction_pointer = _line_cache.Length;
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

            if (directive.Match(@"^include(?<once>-once)?\s+(?<open>[""'<])(?<path>(?:(?!\k<close>).)+)(?<close>[""'>])$", out ReadOnlyIndexer<string, string>? g))
            {
                char open = g["open"][0];
                char close = g["close"][0];

                if (open != close && open != '<' && close != '>')
                    return WellKnownError("error.mismatched_quotes", open, close);

                ScriptScanningOptions options = ScriptScanningOptions.Regular;

                if (g["once"].Contains('-'))
                    options |= ScriptScanningOptions.IncludeOnce;

                if (open != '<')
                    options |= ScriptScanningOptions.RelativePath;

                return Interpreter.ScriptScanner.ScanScriptFile(CurrentLocation, g["path"], options).Match(err => err, script =>
                {
                    if (Call(script.MainFunction, Array.Empty<Variant>()).Is(out InterpreterError err))
                        return err;

                    return null;
                });
            }

            InterpreterResult? result = null;

            foreach (AbstractDirectiveProcessor? proc in Interpreter.PluginLoader.DirectiveProcessors)
                result ??= proc?.ProcessDirective(this, directive);

            return result?.IsOK ?? false ? null : WellKnownError("error.unparsable_dirctive", directive);
        }

        public override string ToString() => $"{base.ToString()} {CurrentLocation}";




        // TODO
        private readonly ConcurrentStack<(BlockStatementType, SourceLocation)> _blockstatement_stack = new ConcurrentStack<(BlockStatementType, SourceLocation)>();
        private Variable? _current_with_context;



        private void PushBlockStatement(BlockStatementType statement) => _blockstatement_stack.Push((statement, CurrentLocation));

        private InterpreterError? PopBlockStatement(params BlockStatementType[] accepted)
        {
            if (accepted.Length == 0)
                _blockstatement_stack.TryPop(out _);
            else
            {
                _blockstatement_stack.TryPop(out (BlockStatementType type, SourceLocation loc) statement);

                if (!accepted.Contains(statement.type))
                    return WellKnownError("error.no_matching_close", statement.type, statement.loc);
            }

            return null;
        }

        private InterpreterResult? ProcessStatement(string line)
        {
            InterpreterResult? result = line.Match(null, new Dictionary<string, Func<Match, InterpreterResult?>>
            {
                [@"^exit(\b\s*(?<code>.+))?$"] = m =>
                {
                    string code = m.Groups["code"].Value;

                    if (string.IsNullOrWhiteSpace(code))
                        code = "0";

                    Union<Variant, InterpreterError> result = ProcessExpressionString(code);

                    if (result.Is(out Variant value))
                    {
                        Interpreter.Stop((int)value.ToNumber());

                        return InterpreterResult.OK;
                    }
                    else
                        return (InterpreterError)result;
                },
                [@"^return(\b\s*(?<value>.+))?$"] = m =>
                {
                    if (CurrentFunction.IsMainFunction)
                        return WellKnownError("error.invalid_return");

                    string optval = m.Groups["value"].Value;

                    if (string.IsNullOrWhiteSpace(optval))
                        optval = "0";

                    Union<Variant, InterpreterError> result = ProcessExpressionString(optval);

                    if (result.Is(out Variant value))
                        Return(value);
                    else
                        return (InterpreterError)result;

                    return InterpreterResult.OK;
                },
                [@"^for\s+(?<start>.+)\s+to\s+(?<stop>.+)(\s+step\s+(?<step>.+))?$"] = m =>
                {
                    throw new NotImplementedException();
                },
                [@"^for\s+(?<variable>.+)\s+in\s+(?<expression>.+)$"] = m =>
                {
                    throw new NotImplementedException();
                },
                [@"^with\s+(?<expression>.+)$"] = m =>
                {
                    throw new NotImplementedException();
                },
                [@"^redim\s+(?<expression>.+)$"] = m =>
                {
                    throw new NotImplementedException();
                },

                ["^next$"] = _ => PopBlockStatement(BlockStatementType.For, BlockStatementType.ForIn),
                ["^wend$"] = _ => PopBlockStatement(BlockStatementType.While),
                ["^endwith$"] = _ => PopBlockStatement(BlockStatementType.With),
                ["^endswitch$"] = _ => PopBlockStatement(BlockStatementType.Switch, BlockStatementType.Case),
                ["^endselect$"] = _ => PopBlockStatement(BlockStatementType.Select, BlockStatementType.Case),

                [@"^continueloop\s*(?<level>\d+)?\s*$"] = m =>
                {
                    int level = int.TryParse(m.Groups["level"].Value, out int l) ? l : 1;
                    InterpreterResult? result = InterpreterResult.OK;

                    while (level-- > 1)
                        result = PopBlockStatement(BlockStatementType.For, BlockStatementType.ForIn, BlockStatementType.While, BlockStatementType.Do);

                    // TODO : continue


                    throw new NotImplementedException();
                },
                [@"^exitloop\s*(?<level>\d+)?\s*$"] = m =>
                {
                    int level = int.TryParse(m.Groups["level"].Value, out int l) ? l : 1;
                    InterpreterResult? result = InterpreterResult.OK;

                    while (level-- > 0)
                        result = PopBlockStatement(BlockStatementType.For, BlockStatementType.ForIn, BlockStatementType.While, BlockStatementType.Do);

                    throw new NotImplementedException();

                    return result;
                },
            });

            // TODO

            foreach (AbstractStatementProcessor? proc in Interpreter.PluginLoader.StatementProcessors)
                if (proc is { Regex: string pat } sp && line.Match(pat, out Match _))
                    result ??= sp.ProcessStatement(this, line);

            return result;
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
                    return ProcessAssignmentStatement(expression, false);
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

        private Union<Variant, InterpreterError> ProcessExpressionString(string expression)
        {
            try
            {
                ParserConstructor<PARSABLE_EXPRESSION>.ParserWrapper? provider = ParserProvider.ExpressionParser;
                PARSABLE_EXPRESSION? expr = provider.Parse(expression).ParsedValue;

                if (expr is PARSABLE_EXPRESSION.AnyExpression { Item: EXPRESSION any })
                    return ProcessExpression(any);

                return WellKnownError("error.unparsable_line", expression);
            }
            catch (Exception ex)
            {
                return WellKnownError("error.unparsable_line", expression, ex.Message);
            }
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

            foreach ((VARIABLE variable, FSharpOption<EXPRESSION>? expression) in multi_decl.Item.Select(t => t.ToValueTuple()))
            {
                error ??= ProcessVariableDeclaration(variable, decltype);

                if (expression is { })
                {
                    // TODO : enum step handling

                    var assg_expr = (ASSIGNMENT_TARGET.NewVariableAssignment(variable), OPERATOR_ASSIGNMENT.Assign, expression.Value).ToTuple();
                    Union<Variant, InterpreterError> result = ProcessAssignmentStatement(PARSABLE_EXPRESSION.NewAssignmentExpression(assg_expr), true);

                    if (result.Is(out Variant value))
                        Program.PrintDebugMessage($"{variable} = {value}");
                    else
                        error ??= (InterpreterError)result;
                }
                else if (decltype.HasFlag(DeclarationType.Const))
                    return WellKnownError("error.uninitialized_constant", variable);
            }

            return null;
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

        private Union<Variant, InterpreterError> ProcessAssignmentStatement(PARSABLE_EXPRESSION assignment, bool force)
        {
            (ASSIGNMENT_TARGET target, EXPRESSION expression) = Cleanup.CleanUpExpression(assignment);
            Union<Variant, InterpreterError>? result = null;
            Variable? target_variable = null;

            switch (target)
            {
                case ASSIGNMENT_TARGET.VariableAssignment { Item: VARIABLE variable }:
                    if (VariableResolver.TryGetVariable(variable.Name, out target_variable))
                    {
                        if (target_variable.IsConst && !force)
                            return WellKnownError("error.constant_assignment", target_variable, target_variable.DeclaredLocation);
                    }
                    else
                        target_variable = VariableResolver.CreateVariable(CurrentLocation, variable.Name, false);

                    break;
                case ASSIGNMENT_TARGET.IndexedAssignment { Item: { } indexer }:
                    {
                        (EXPRESSION expr, EXPRESSION index) = indexer.ToValueTuple();
                        result = ProcessIndexer(expr, index);
                    }
                    break;
                case ASSIGNMENT_TARGET.MemberAssignemnt { Item: { } member }:
                    result = ProcessMember(member);

                    break;
                default:
                    return WellKnownError("error.invalid_assignment_target", target);
            }

            if (result?.Is<InterpreterError>() ?? false)
                return result;
            else
                target_variable ??= result?.As<Variant>().AssignedTo;

            if (target_variable is null)
                return WellKnownError("error.invalid_assignment_target", target);
            else
                return ProcessVariableAssignment(target_variable, expression);
        }

        private Union<Variant, InterpreterError> ProcessVariableAssignment(Variable variable, EXPRESSION expression)
        {
            foreach (VARIABLE v in expression.ReferencedVariables)
                if (!VariableResolver.HasVariable(v))
                    return WellKnownError("error.undeclared_variable", v);

            Union<Variant, InterpreterError> value = ProcessExpression(expression);

            if (value.Is(out Variant variant))
                (variable.ReferencedVariable ?? variable).Value = variant;

            return value;
        }

        private Union<Variant, InterpreterError> ProcessExpression(EXPRESSION? expression)
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

        private Union<Variant, InterpreterError> ProcessIndexer(EXPRESSION expr, EXPRESSION index)
        {
            throw new NotImplementedException();
        }

        private Union<Variant, InterpreterError> ProcessMember(MEMBER_EXPRESSION expr)
        {
            throw new NotImplementedException();
        }

        private Union<Variant, InterpreterError> ProcessUnary(OPERATOR_UNARY op, EXPRESSION expr)
        {
            Union<Variant, InterpreterError> result = ProcessExpression(expr);

            if (result.Is(out Variant value))
                if (op.IsIdentity)
                    result = +value;
                else if (op.IsNegate)
                    result = -value;
                else if (op.IsNegate)
                    result = !value;
                else
                    result = WellKnownError("error.unsupported_operator", op);

            return result;
        }

        private Union<Variant, InterpreterError> ProcessBinary(EXPRESSION expr1, OPERATOR_BINARY op, EXPRESSION expr2)
        {
            InterpreterError? evaluate(EXPRESSION expr, out Variant target)
            {
                Union<Variant, InterpreterError> result = ProcessExpression(expr);

                target = Variant.Zero;

                if (result.Is(out InterpreterError error))
                    return error;

                target = (Variant)result;

                return null;
            }

            Variant e1, e2;
            InterpreterError? err = evaluate(expr1, out e1);

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
                ;
            else if (op.IsEqualCaseInsensitive)
                ;
            else if (op.IsUnequal)
                ;
            else if (op.IsGreater)
                ;
            else if (op.IsGreaterEqual)
                ;
            else if (op.IsLower)
                ;
            else if (op.IsLowerEqual)
                ;
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
            // TODO : all other operators

            return WellKnownError("error.unsupported_operator", op);
        }

        private Union<Variant, InterpreterError> ProcessTernary(EXPRESSION expr1, EXPRESSION expr2, EXPRESSION expr3) =>
            ProcessExpression(expr1).Match<Union<Variant, InterpreterError>>(cond => ProcessExpression(cond.ToBoolean() ? expr2 : expr3), err => err);

        private Union<Variant, InterpreterError> ProcessFunctionCall(FUNCCALL_EXPRESSION funccall)
        {
            Union<Variant[], InterpreterError> ProcessRawArguments(FSharpList<EXPRESSION> raw_args)
            {
                Variant[] arguments = new Variant[raw_args.Length];
                int i = 0;

                foreach (EXPRESSION arg in raw_args)
                {
                    Union<Variant, InterpreterError>? res = ProcessExpression(arg);

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
                        return ProcessRawArguments(raw_args).Match<Union<Variant, InterpreterError>>(args => Call(func, args), err => err);
                    else
                        return WellKnownError("error.unresolved_func", func_name);
                case FUNCCALL_EXPRESSION.MemberCall member_call:
                    throw new NotImplementedException();
            }

            return WellKnownError("error.not_yet_implemented", funccall);
        }

        private Union<Variant, InterpreterError> ProcessMacro(MACRO macro)
        {
            string name = macro.Name;

            foreach (AbstractMacroProvider provider in Interpreter.PluginLoader.MacroProviders)
                if (provider.ProvideMacroValue(this, name, out Variant? value) && value.HasValue)
                    return value.Value;

            return WellKnownError("error.unknown_macro", macro.Name);
        }

        private Union<Variant, InterpreterError> ProcessVariable(VARIABLE variable)
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

        private InterpreterError WellKnownError(string key, params object[] args) => InterpreterError.WellKnown(CurrentLocation, key, args);
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
        Global,
        Func,
        With,
        For,
        ForIn,
        While,
        Do,
        If,
        ElseIf,
        Else,
        Select,
        Switch,
        Case,
    }
}
