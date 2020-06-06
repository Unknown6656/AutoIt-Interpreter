using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.Common;
using Unknown6656.IO;
using Unknown6656.Mathematics.Analysis;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class AU3Thread
        : IDisposable
    {
        private static volatile int _tid = 0;
        private readonly ConcurrentStack<CallFrame> _callstack = new ConcurrentStack<CallFrame>();
        private volatile bool _running = false;


        public Interpreter Interpreter { get; }

        public CallFrame? CurrentFrame => _callstack.TryPeek(out CallFrame? lp) ? lp : null;

        public SourceLocation? CurrentLocation => CurrentFrame switch {
            AU3CallFrame f => f.CurrentLocation,
            _ => SourceLocation.Unknown
        };

        public ScriptFunction? CurrentFunction => CurrentFrame?.CurrentFunction;

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

        public InterpreterError? Start(ScriptFunction function)
        {
            if (_running)
                return InterpreterError.WellKnown(CurrentLocation, "error.thread_already_running", ThreadID);
            else
                _running = true;

            InterpreterError? res = Call(function);

            _running = false;

            return res;
        }

        internal InterpreterError? Call(ScriptFunction function)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            CallFrame? old = CurrentFrame;
            CallFrame frame = function switch
            {
                AU3Function f => new AU3CallFrame(this, f),
                NativeFunction f => new NativeCallFrame(this, f),
                _ => throw new ArgumentException($"A function of the type '{function}' cannot be handled by the current thread '{this}'.", nameof(function)),
            };

            _callstack.Push(frame);

            InterpreterError? result = frame.Exec();

            while (!ReferenceEquals(CurrentFrame, old))
                _callstack.TryPop(out _);

            return result;
        }

        internal SourceLocation? ExitCall()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            _callstack.TryPop(out _);

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

    public abstract class CallFrame
    {
        public AU3Thread CurrentThread { get; }

        public ScriptFunction CurrentFunction { get; }

        public Interpreter Interpreter => CurrentThread.Interpreter;


        internal CallFrame(AU3Thread thread, ScriptFunction function)
        {
            CurrentThread = thread;
            CurrentFunction = function;
        }

        internal abstract InterpreterError? Exec();

        public InterpreterError? Call(ScriptFunction function) => CurrentThread.Call(function);
    }

    public sealed class NativeCallFrame
        : CallFrame
    {
        internal NativeCallFrame(AU3Thread thread, NativeFunction function)
            : base(thread, function)
        {
        }

        internal override InterpreterError? Exec() => (CurrentFunction as NativeFunction)?.Execute(this);
    }

    public sealed class AU3CallFrame
        : CallFrame
    {
        private volatile int _instruction_pointer = 0;
        private (SourceLocation LineLocation, string LineContent)[] _line_cache;


        public SourceLocation CurrentLocation => _line_cache[_instruction_pointer].LineLocation;

        public string CurrentLineContent => _line_cache[_instruction_pointer].LineContent;



        internal AU3CallFrame(AU3Thread thread, AU3Function function)
            : base(thread, function)
        {
            _line_cache = function.Lines;
            _instruction_pointer = 0;
        }

        internal override InterpreterError? Exec()
        {
            ScannedScript script = CurrentFunction.Script;
            InterpreterError? result = null;

            if (CurrentFunction.IsMainFunction)
                result = script.LoadScript(this);

            Program.PrintDebugMessage($"Executing {CurrentFunction}");

            while (_instruction_pointer < _line_cache.Length)
                if (result is null)
                {
                    result = ParseCurrentLine()?.OptionalError;

                    if (!MoveNext())
                        break;
                }
                else
                    break;

            if (CurrentFunction.IsMainFunction)
                result ??= script.UnLoadScript(this);

            return result;
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

        public InterpreterResult? ParseCurrentLine()
        {
            (SourceLocation loc, string line) = _line_cache[_instruction_pointer];
            InterpreterResult? result = null;

            if (string.IsNullOrWhiteSpace(line))
                return InterpreterResult.OK;

            Program.PrintDebugMessage($"({loc}) {line}");

            result ??= ProcessDirective(line);
            result ??= ProcessStatement(line);
            result ??= ProcessExpressionStatement(line);
            result ??= UseExternalLineProcessors(line);

            return result ?? WellKnownError("error.unparsable_line", line);
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

                return Interpreter.ScriptScanner.ScanScriptFile(CurrentLocation, g["path"], options).Match(err => err, script => Call(script.MainFunction));
            }

            InterpreterResult? result = null;

            foreach (AbstractDirectiveProcessor? proc in Interpreter.PluginLoader.DirectiveProcessors)
                result ??= proc?.ProcessDirective(this, directive);

            return result?.IsOK ?? false ? null : WellKnownError("error.unparsable_dirctive", directive);
        }

        private InterpreterResult? ProcessStatement(string line)
        {
            InterpreterResult? result = null;

            //InterpreterResult? result = line.Match(null, new Dictionary<string, Func<Match, InterpreterResult?>>
            //{
            //    ["^next$"] = _ => ScopeStack.Pop(ScopeType.For, ScopeType.ForIn),
            //    ["^wend$"] = _ => ScopeStack.Pop(ScopeType.While),
            //    [@"^continueloop\s*(?<level>\d+)?\s*$"] = m =>
            //    {
            //        int level = int.TryParse(m.Groups["level"].Value, out int l) ? l : 1;
            //        InterpreterResult? result = InterpreterResult.OK;

            //        while (level-- > 1)
            //            result = ScopeStack.Pop(ScopeType.For, ScopeType.ForIn, ScopeType.While, ScopeType.Do);

            //         // TODO : continue


            //         throw new NotImplementedException();
            //    },
            //    [@"^exitloop\s*(?<level>\d+)?\s*$"] = m =>
            //    {
            //        int level = int.TryParse(m.Groups["level"].Value, out int l) ? l : 1;
            //        InterpreterResult? result = InterpreterResult.OK;

            //        while (level-- > 0)
            //            result = ScopeStack.Pop(ScopeType.For, ScopeType.ForIn, ScopeType.While, ScopeType.Do);

            //        return result;
            //    },
            //});


            foreach (AbstractStatementProcessor? proc in Interpreter.PluginLoader.StatementProcessors)
                if (proc is { Regex: string pat } sp && line.Match(pat, out Match _))
                    result ??= sp.ProcessStatement(this, line);

            return result;
        }

        private InterpreterResult? ProcessExpressionStatement(string line)
        {
            throw new NotImplementedException();
        }

        private InterpreterResult? UseExternalLineProcessors(string line)
        {
            foreach (AbstractLineProcessor? proc in Interpreter.PluginLoader.LineProcessors)
                if ((proc?.CanProcessLine(line) ?? false) && proc?.ProcessLine(this, line) is { } res)
                    return res;

            return null;
        }

        private InterpreterError WellKnownError(string key, params object[] args) => InterpreterError.WellKnown(CurrentLocation, key, args);
    }
}
