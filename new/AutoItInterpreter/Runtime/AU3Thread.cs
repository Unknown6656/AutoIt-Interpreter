using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.Common;
using Unknown6656.IO;
using System.Threading;

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

        public SourceLocation? CurrentLocation => CurrentFrame?.CurrentLocation;

        public ScannedFunction? CurrentFunction => CurrentFrame?.CurrentFunction;

        public string? CurrentLineContent => CurrentFrame?.CurrentLineContent;

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

        public InterpreterResult Start(ScannedFunction function)
        {
            if (_running)
                return InterpreterError.WellKnown(CurrentLocation, "error.thread_already_running", ThreadID);
            else
                _running = true;

            InterpreterResult res = Call(function);

            _running = false;

            return res;
        }

        internal InterpreterResult Call(ScannedFunction function)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            CallFrame? old = CurrentFrame;
            CallFrame frame = new CallFrame(this, function);

            _callstack.Push(frame);

            InterpreterResult? result = frame.Exec();

            while (!ReferenceEquals(CurrentFrame, old))
                _callstack.TryPop(out _);

            return result ?? InterpreterResult.OK;
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

    public sealed class CallFrame
    {
        private volatile int _instruction_pointer = 0;
        private (SourceLocation LineLocation, string LineContent)[] _line_cache;


        public AU3Thread CurrentThread { get; }

        public ScannedFunction CurrentFunction { get; }

        public Interpreter Interpreter => CurrentThread.Interpreter;

        public SourceLocation CurrentLocation => _line_cache[_instruction_pointer].LineLocation;

        public string CurrentLineContent => _line_cache[_instruction_pointer].LineContent;


        internal CallFrame(AU3Thread thread, ScannedFunction function)
        {
            CurrentThread = thread;
            CurrentFunction = function;
            _line_cache = function.Lines;
            _instruction_pointer = 0;
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

        internal InterpreterResult? Exec()
        {
            ScannedScript script = CurrentFunction.Script;
            InterpreterResult? result = null;

            if (CurrentFunction.IsMainFunction)
                result = script.LoadScript(this);

            Program.PrintDebugMessage($"Executing {CurrentFunction}");

            while (_instruction_pointer < _line_cache.Length)
                if (result?.IsOK ?? true)
                {
                    result = ParseCurrentLine();

                    if (!MoveNext())
                        break;
                }
                else
                    break;

            if (CurrentFunction.IsMainFunction && (result?.IsOK ?? true))
                result = script.UnLoadScript(this);

            return result;
        }

        public InterpreterResult Call(ScannedFunction function) => CurrentThread.Call(function);

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

            InterpreterResult? result = directive.Match(
                null,
                (@"^include\s+""(?<path>[^""]+)""", (Match m) => ProcessInclude(m.Groups["path"].Value, true, false)),
                (@"^include\s+<(?<path>[^>]+)>", (Match m) => ProcessInclude(m.Groups["path"].Value, false, false)),
                (@"^include-once\s+""(?<path>[^""]+)""", (Match m) => ProcessInclude(m.Groups["path"].Value, true, true)),
                (@"^include-once\s+<(?<path>[^>]+)>", (Match m) => ProcessInclude(m.Groups["path"].Value, false, true))
            );

            foreach (AbstractDirectiveProcessor? proc in Interpreter.PluginLoader.DirectiveProcessors)
                result ??= proc?.ProcessDirective(this, directive);

            return result ?? WellKnownError("error.unparsable_dirctive", directive);
        }

        private InterpreterResult ProcessInclude(string path, bool relative, bool once)
        {
            throw new NotImplementedException();
        }

        private InterpreterResult? ProcessStatement(string line)
        {
            InterpreterResult? result = null;

            // InterpreterResult? result = line.Match(null, new Dictionary<string, Func<Match, InterpreterResult?>>
            // {
            //     [@"^func\s+(?<name>[a-z_]\w*)\s*\(\s*(?<args>.*)\s*\)\s*$"] = m =>
            //     {
            //         string name = m.Groups["name"].Value;
            //         string args = m.Groups["args"].Value;
            // 
            //         _state |= LineParserState.InsideBlockComment;
            // 
            // 
            //         throw new NotImplementedException();
            //     },
            //     ["^endfunc$"] = _ => ScopeStack.Pop(ScopeType.Func),
            //     ["^next$"] = _ => ScopeStack.Pop(ScopeType.For, ScopeType.ForIn),
            //     ["^wend$"] = _ => ScopeStack.Pop(ScopeType.While),
            //     [@"^continueloop\s*(?<level>\d+)?\s*$"] = m =>
            //     {
            //         int level = int.TryParse(m.Groups["level"].Value, out int l)? l : 1;
            //         InterpreterResult? result = InterpreterResult.OK;
            // 
            //         while (level-- > 1)
            //             result = ScopeStack.Pop(ScopeType.For, ScopeType.ForIn, ScopeType.While, ScopeType.Do);
            // 
            //         // TODO : continue
            // 
            // 
            //         throw new NotImplementedException();
            //     },
            //     [@"^exitloop\s*(?<level>\d+)?\s*$"] = m =>
            //     {
            //         int level = int.TryParse(m.Groups["level"].Value, out int l) ? l : 1;
            //         InterpreterResult? result = InterpreterResult.OK;
            // 
            //         while (level-- > 0)
            //             result = ScopeStack.Pop(ScopeType.For, ScopeType.ForIn, ScopeType.While, ScopeType.Do);
            // 
            //         return result;
            //     },
            // });
            // 

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

        private InterpreterResult WellKnownError(string key, params object[] args) => InterpreterError.WellKnown(CurrentLocation, key, args);
    }
}
