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


        public ScopeStack ScopeStack { get; }

        public Interpreter Interpreter { get; }

        public CallFrame? CurrentFrame => _callstack.TryPeek(out CallFrame? lp) ? lp : null;

        public SourceLocation? CurrentLocation => CurrentFrame?.CurrentLocation;

        public string? CurrentLineContent => CurrentFrame?.CurrentLineContent;

        public bool IsDisposed { get; private set; }

        public bool IsMainThread => ReferenceEquals(this, Interpreter.MainThread);

        public int ThreadID { get; }


        internal AU3Thread(Interpreter interpreter, SourceLocation target)
        {
            ThreadID = ++_tid;
            Interpreter = interpreter;
            ScopeStack = new ScopeStack(this);

            Interpreter.AddThread(this);

            Push(target, null, ScopeType.Global);
        }

        public override string ToString() => $"0x{_tid:x4}{(IsMainThread ? " (main)" : "")} @ {CurrentLocation}";

        public CallFrame PushFrame(SourceLocation target, string? name) => Push(target, name, ScopeType.Func);

        private CallFrame Push(SourceLocation target, string? name, ScopeType type)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            CallFrame parser = new CallFrame(this, target);

            name ??= target.FileName.FullName;

            _callstack.Push(parser);
            ScopeStack.Push(type, name, target);

            return parser;
        }

        public SourceLocation? PopFrame()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            _callstack.TryPop(out _);
            ScopeStack.Pop(ScopeType.Func);

            return CurrentLocation;
        }

        public InterpreterResult Run()
        {
            InterpreterResult? result = null;

            while (CurrentFrame is CallFrame frame)
            {
                result = frame.ParseCurrentLine();

                if (result?.OptionalError is { } || (result?.ProgramExitCode ?? 0) != 0)
                    break;
                else if (!frame.MoveNext())
                    break;
            }

            return result ?? InterpreterResult.OK;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            else
                IsDisposed = true;

            Interpreter.RemoveThread(this);
            _callstack.TryPop(out _);
            ScopeStack.Pop(ScopeType.Global);
        }
    }

    public sealed class CallFrame
    {
        private int _line_number = 0;


        public FileInfo File { get; }

        public string[] Lines { get; }

        public AU3Thread CurrentThread { get; }

        public ScopeStack ScopeStack => CurrentThread.ScopeStack;

        public Interpreter Interpreter => CurrentThread.Interpreter;

        public SourceLocation CurrentLocation => new SourceLocation(File, _line_number);

        public string? CurrentLineContent => _line_number < Lines.Length ? Lines[_line_number] : null;


        internal CallFrame(AU3Thread thread, SourceLocation start)
        {
            File = start.FileName;
            _line_number = 0;
            CurrentThread = thread;
            Lines = From.File(File).To.Lines();

            _line_number = start.LineNumber;
        }

        public bool MoveNext() => ++_line_number < Lines.Length;

        // public void MoveToStart() => MoveTo(0);

        // public bool MoveTo(int line) => (_line_number = Math.Max(0, Math.Min(line, Lines.Length))) < Lines.Length;



        private LineParserState _state;
        private int _blockcomment_level;


        // public void ResetParser()
        // {
        //     _state = LineParserState.Regular;
        //     _line_number = 0;
        // }

        private InterpreterResult WellKnownError(string key, params object[] args) => InterpreterError.WellKnown(CurrentLocation, key, args);

        public InterpreterResult? ParseCurrentLine()
        {
            string? line = CurrentLineContent;
            InterpreterResult? result = null;

            if (string.IsNullOrWhiteSpace(line) && !_state.HasFlag(LineParserState.LineContinuation))
                return InterpreterResult.OK;

            line = TrimComment(line);

            int index = line.Length;

            line = line.TrimStart();
            index -= line.Length;

            if (line.Match(@"(\s|^)_$", out Match m))
            {
                _state |= LineParserState.LineContinuation;
                line = line[..m.Index];

                // TODO : line continuation
            }
            else
                _state &= ~LineParserState.LineContinuation;

            if (_state.HasFlag(LineParserState.LineContinuation))
            {
                // TODO : line continuation
            }

            if (string.IsNullOrWhiteSpace(line))
                return InterpreterResult.OK;

            result ??= ProcessDirective(line);

            if (_state.HasFlag(LineParserState.InsideBlockComment))
                return InterpreterResult.OK;

            result ??= ProcessStatement(line);
            result ??= ProcessExpressionStatement(line);

            foreach (ILineProcessor? proc in Interpreter.PluginLoader.LineProcessors)
                if (result is { })
                    return result;
                else if (proc?.CanProcessLine(line) ?? false)
                    result ??= proc?.ProcessLine(this, line);

            return result ?? WellKnownError("error.unparsable_line");
        }

        private string TrimComment(string? line)
        {
            Match m;

            if (string.IsNullOrWhiteSpace(line))
                return "";
            else if (line.Contains(';'))
                if (line.Match(@"\;[^\""\']*$", out m))
                    line = line[..m.Index];
                else
                {
                    string before = line[..line.IndexOf(';')];

                    if (!line.Contains("$\"") && ((before.CountOccurences("\"") % 2) == 0) && (before.CountOccurences("'") % 2) == 0)
                        line = before.Trim();
                    else if (line.Match(@"^([^\""\;]*\""[^\""]*\""[^\""\;]*)*(?<cmt>\;).*$", out m))
                        line = line[..m.Groups["cmt"].Index];
                    else if (line.Match(@"^([^'\;]*'[^']*'[^'\;]*)*(?<cmt>\;).*$", out m))
                        line = line[..m.Groups["cmt"].Index];
                }

            return line.TrimEnd();
        }

        private InterpreterResult ProcessInclude(string path, bool relative, bool once)
        {
            throw new NotImplementedException();
        }

        private InterpreterResult? ProcessDirective(string directive)
        {
            InterpreterResult? result = null;

            if (!directive.StartsWith('#'))
                return result;
            else
                directive = directive[1..];

            if (directive.Match(
                (@"^(comments\-start|cs)(\b|$)", _ =>
                {
                    _state |= LineParserState.InsideBlockComment;
                    ++_blockcomment_level;
                }), (@"^(comments\-end|ce)(\b|$)", _ =>
                {
                    _blockcomment_level = Math.Max(0, _blockcomment_level - 1);

                    if (_blockcomment_level <= 0)
                        _state &= ~LineParserState.InsideBlockComment;
                }
            )) || _state.HasFlag(LineParserState.InsideBlockComment))
                result = InterpreterResult.OK;

            result ??= directive.Match(
                null,
                (@"^include\s+""(?<path>[^""]+)""", (Match m) => ProcessInclude(m.Groups["path"].Value, true, false)),
                (@"^include\s+<(?<path>[^>]+)>", (Match m) => ProcessInclude(m.Groups["path"].Value, false, false)),
                (@"^include-once\s+""(?<path>[^""]+)""", (Match m) => ProcessInclude(m.Groups["path"].Value, true, true)),
                (@"^include-once\s+<(?<path>[^>]+)>", (Match m) => ProcessInclude(m.Groups["path"].Value, false, true)),
                (@"^notrayicon", _ => ProcessExpressionStatement(@"Opt(""TrayIconHide"", 1)")),
                (@"^onautoitstartregister\s+""(?<func>[^""]+)""", (Match m) => ProcessExpressionStatement(m.Groups["func"] + "()")),
                (@"^pragma\s+(?<option>[a-z_]\w+)\b\s*(\((?<params>.*)\))?\s*", (Match m) =>
                {
                    string opt = m.Groups["option"].Value;
                    string pars = m.Groups["params"].Value;

                    // compiler opt

                    throw new NotImplementedException();
                }),
                (@"^requireadmin", _ =>
                {
                    // compiler opt

                    throw new NotImplementedException();
                })
            );

            foreach (IDirectiveProcessor? proc in Interpreter.PluginLoader.DirectiveProcessors)
                result ??= proc?.ProcessDirective(this, directive);

            return result ?? WellKnownError("error.unparsable_dirctive", directive);
        }

        private InterpreterResult? ProcessStatement(string line)
        {
            InterpreterResult? result = line.Match(null, new Dictionary<string, Func<Match, InterpreterResult?>>
            {
                [@"^func\s+(?<name>[a-z_]\w*)\s*\(\s*(?<args>.*)\s*\)\s*$"] = m =>
                {
                    string name = m.Groups["name"].Value;
                    string args = m.Groups["args"].Value;

                    _state |= LineParserState.InsideBlockComment;


                    throw new NotImplementedException();
                },
                ["^endfunc$"] = _ => ScopeStack.Pop(ScopeType.Func),
                ["^next$"] = _ => ScopeStack.Pop(ScopeType.For, ScopeType.ForIn),
                ["^wend$"] = _ => ScopeStack.Pop(ScopeType.While),
                [@"^continueloop\s*(?<level>\d+)?\s*$"] = m =>
                {
                    int level = int.TryParse(m.Groups["level"].Value, out int l)? l : 1;
                    InterpreterResult? result = InterpreterResult.OK;

                    while (level-- > 1)
                        result = ScopeStack.Pop(ScopeType.For, ScopeType.ForIn, ScopeType.While, ScopeType.Do);

                    // TODO : continue


                    throw new NotImplementedException();
                },
                [@"^exitloop\s*(?<level>\d+)?\s*$"] = m =>
                {
                    int level = int.TryParse(m.Groups["level"].Value, out int l) ? l : 1;
                    InterpreterResult? result = InterpreterResult.OK;

                    while (level-- > 0)
                        result = ScopeStack.Pop(ScopeType.For, ScopeType.ForIn, ScopeType.While, ScopeType.Do);

                    return result;
                },
            });

            foreach (IStatementProcessor? proc in Interpreter.PluginLoader.StatementProcessors)
                if (proc is { Regex: string pat } sp && line.Match(pat, out Match _))
                    result ??= sp.ProcessStatement(this, line);

            return result;
        }

        private InterpreterResult? ProcessExpressionStatement(string line) => throw new NotImplementedException();
    }

    [Flags]
    internal enum LineParserState
         : uint
    {
        Regular = 0,
        InsideBlockComment = 1,
        LineContinuation = 2,
        InsideFunc = 4,
    }
}
