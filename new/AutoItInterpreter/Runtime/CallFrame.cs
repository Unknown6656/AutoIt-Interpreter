using System.Text.RegularExpressions;
using System.IO;
using System;

using Unknown6656.Common;
using Unknown6656.IO;
using System.Collections.Generic;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class CallFrame
    {
        private int _line_number = 0;
        private int _char_index = -1;


        public FileInfo File { get; }

        public string[] Lines { get; }

        public AU3Thread CurrentThread { get; }

        public ScopeStack ScopeStack => CurrentThread.ScopeStack;

        public Interpreter Interpreter => CurrentThread.Interpreter;

        public SourceLocation CurrentLocation => new SourceLocation(File, _line_number, _char_index);

        public string? CurrentLineContent => _line_number < Lines.Length ? Lines[_line_number] : null;


        internal CallFrame(AU3Thread thread, SourceLocation start)
        {
            File = start.FileName;
            _line_number = 0;
            CurrentThread = thread;
            Lines = From.File(File).To.Lines();

            MoveTo(start.LineNumber);
        }

        public bool MoveNext() => ++_line_number < Lines.Length;

        public void MoveToStart() => MoveTo(0);

        public bool MoveTo(int line)
        {
            bool cm = (_line_number = Math.Max(0, Math.Min(line, Lines.Length))) < Lines.Length;

            _char_index = 0;

            return cm;
        }



        private LineParserState _state;
        private int _blockcomment_level;


        public void ResetParser()
        {
            _state = LineParserState.Regular;

            MoveToStart();
        }

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

            if (line.StartsWith('#'))
                result ??= ProcessDirective(line[1..]);

            if (_state.HasFlag(LineParserState.InsideBlockComment))
                return InterpreterResult.OK;

            result ??= ProcessStatement(line);
            result ??= ProcessExpressionStatement(line);

            foreach (ILineProcessor? proc in Interpreter.LineProcessors)
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

            foreach (IDirectiveProcessor? proc in Interpreter.DirectiveProcessors)
                result ??= proc?.ProcessDirective(this, directive);

            return result ?? WellKnownError("error.unparsable_dirctive", directive);
        }

        private InterpreterResult? ProcessStatement(string line)
        {
            InterpreterResult? result = line.Match(null, new Dictionary<string, Func<Match, InterpreterResult?>>
            {
                [@"^func\s+(?<name>[a-z_]\w*)\s*\(\s*(?<args>)\s*\)\s*$"] = m =>
                {
                    string name = m.Groups["name"].Value;
                    string args = m.Groups["args"].Value;



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
    }
}
