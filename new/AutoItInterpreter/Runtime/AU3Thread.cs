using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class AU3Thread
        : IDisposable
    {
        private static int _tid = 0;
        private readonly ConcurrentStack<CallFrame> _callstack = new ConcurrentStack<CallFrame>();


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
        }

        public InterpreterResult? Start(ScannedFunction function)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            CallFrame frame = new CallFrame(this, function);

            _callstack.Push(frame);

            InterpreterResult? result = frame.Exec();

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

        }

        public SourceLocation Call(ScannedFunction function)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            CallFrame frame = new CallFrame(this, function);

            _callstack.Push(frame);

            // exec frame
            // TODO : check if script init



            InterpreterResult? result = null;

            while (CurrentFrame is CallFrame frame)
            {
                Interpreter.ScriptScanner.;
                frame.CurrentFunction.Script;


                result = frame.ParseCurrentLine();

                if (!(result?.IsOK ?? true))
                    break;
            }

            return result ?? InterpreterResult.OK;
        }




        public InterpreterResult? ParseCurrentLine()
        {
            //(SourceLocation loc, string line) = _line_cache[_instruction_pointer];
            //InterpreterResult? result = null;

            //if (string.IsNullOrWhiteSpace(line))
            //    return InterpreterResult.OK;

            //result ??= ProcessDirective(line);

            //if (_state.HasFlag(LineParserState.InsideBlockComment))
            //    return InterpreterResult.OK;

            //result ??= ProcessStatement(line);
            //result ??= ProcessExpressionStatement(line);

            //foreach (ILineProcessor? proc in Interpreter.PluginLoader.LineProcessors)
            //    if (result is { })
            //        return result;
            //    else if (proc?.CanProcessLine(line) ?? false)
            //        result ??= proc?.ProcessLine(this, line);

            //return result ?? WellKnownError("error.unparsable_line");

            throw new NotImplementedException();
        }

        private InterpreterResult ProcessInclude(string path, bool relative, bool once)
        {
            throw new NotImplementedException();
        }

        private InterpreterResult? ProcessDirective(string directive)
        {
            //InterpreterResult? result = null;

            //if (!directive.StartsWith('#'))
            //    return result;
            //else
            //    directive = directive[1..];

            //if (directive.Match(
            //    (@"^(comments\-start|cs)(\b|$)", _ =>
            //    {
            //        _state |= LineParserState.InsideBlockComment;
            //        ++_blockcomment_level;
            //    }), (@"^(comments\-end|ce)(\b|$)", _ =>
            //    {
            //        _blockcomment_level = Math.Max(0, _blockcomment_level - 1);

            //        if (_blockcomment_level <= 0)
            //            _state &= ~LineParserState.InsideBlockComment;
            //    }
            //)) || _state.HasFlag(LineParserState.InsideBlockComment))
            //    result = InterpreterResult.OK;

            //result ??= directive.Match(
            //    null,
            //    (@"^include\s+""(?<path>[^""]+)""", (Match m) => ProcessInclude(m.Groups["path"].Value, true, false)),
            //    (@"^include\s+<(?<path>[^>]+)>", (Match m) => ProcessInclude(m.Groups["path"].Value, false, false)),
            //    (@"^include-once\s+""(?<path>[^""]+)""", (Match m) => ProcessInclude(m.Groups["path"].Value, true, true)),
            //    (@"^include-once\s+<(?<path>[^>]+)>", (Match m) => ProcessInclude(m.Groups["path"].Value, false, true)),
            //    (@"^notrayicon", _ => ProcessExpressionStatement(@"Opt(""TrayIconHide"", 1)")),
            //    (@"^onautoitstartregister\s+""(?<func>[^""]+)""", (Match m) => ProcessExpressionStatement(m.Groups["func"] + "()")),
            //    (@"^pragma\s+(?<option>[a-z_]\w+)\b\s*(\((?<params>.*)\))?\s*", (Match m) =>
            //    {
            //        string opt = m.Groups["option"].Value;
            //        string pars = m.Groups["params"].Value;

            //        // compiler opt

            //        throw new NotImplementedException();
            //    }),
            //    (@"^requireadmin", _ =>
            //    {
            //        // compiler opt

            //        throw new NotImplementedException();
            //    })
            //);

            //foreach (IDirectiveProcessor? proc in Interpreter.PluginLoader.DirectiveProcessors)
            //    result ??= proc?.ProcessDirective(this, directive);

            //return result ?? WellKnownError("error.unparsable_dirctive", directive);

            throw new NotImplementedException();
        }

        private InterpreterResult? ProcessStatement(string line)
        {
            //InterpreterResult? result = line.Match(null, new Dictionary<string, Func<Match, InterpreterResult?>>
            //{
            //    [@"^func\s+(?<name>[a-z_]\w*)\s*\(\s*(?<args>.*)\s*\)\s*$"] = m =>
            //    {
            //        string name = m.Groups["name"].Value;
            //        string args = m.Groups["args"].Value;

            //        _state |= LineParserState.InsideBlockComment;


            //        throw new NotImplementedException();
            //    },
            //    ["^endfunc$"] = _ => ScopeStack.Pop(ScopeType.Func),
            //    ["^next$"] = _ => ScopeStack.Pop(ScopeType.For, ScopeType.ForIn),
            //    ["^wend$"] = _ => ScopeStack.Pop(ScopeType.While),
            //    [@"^continueloop\s*(?<level>\d+)?\s*$"] = m =>
            //    {
            //        int level = int.TryParse(m.Groups["level"].Value, out int l)? l : 1;
            //        InterpreterResult? result = InterpreterResult.OK;

            //        while (level-- > 1)
            //            result = ScopeStack.Pop(ScopeType.For, ScopeType.ForIn, ScopeType.While, ScopeType.Do);

            //        // TODO : continue


            //        throw new NotImplementedException();
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

            //foreach (IStatementProcessor? proc in Interpreter.PluginLoader.StatementProcessors)
            //    if (proc is { Regex: string pat } sp && line.Match(pat, out Match _))
            //        result ??= sp.ProcessStatement(this, line);

            //return result;

            throw new NotImplementedException();
        }

        private InterpreterResult? ProcessExpressionStatement(string line)
        {
            throw new NotImplementedException();
        }


        private InterpreterResult WellKnownError(string key, params object[] args) => InterpreterError.WellKnown(CurrentLocation, key, args);
    }
}
