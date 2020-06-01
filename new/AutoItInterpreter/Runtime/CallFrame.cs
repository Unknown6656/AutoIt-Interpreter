using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class CallFrame
    {
        private int _line_number = 0;
        private int _char_index = -1;


        public FileInfo File { get; }

        public string[] Lines { get; }

        public AU3Thread CurrentThread { get; }

        public Interpreter Interpreter => CurrentThread.Interpreter;

        public SourceLocation CurrentLocation => new SourceLocation(File, _line_number, _char_index);

        public string? CurrentLineContent => _line_number < Lines.Length ? Lines[_line_number] : null;


        public CallFrame(AU3Thread thread, SourceLocation start)
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

        public InterpreterResult? ParseCurrentLine(Interpreter interpreter)
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
                (@"^notrayicon", (Match m) => ProcessExpressionStatement(@"Opt(""TrayIconHide"", 1)")),
                (@"^onautoitstartregister\s+""(?<func>[^""]+)""", (Match m) => ProcessExpressionStatement(m.Groups["func"] + "()")),
                (@"^pragma\s+(?<option>[a-z_]\w+)\b\s*(\((?<params>.*)\))?\s*", (Match m) =>
                {
                    string opt = m.Groups["option"].Value;
                    string pars = m.Groups["params"].Value;

                    // compiler opt

                    throw new NotImplementedException();
                }),
                (@"^requireadmin", (Match m) =>
                {
                    // compiler opt

                    throw new NotImplementedException();
                })
            );

            foreach (IDirectiveProcessor? proc in _directive_processors)
                result ??= proc?.ProcessDirective(this, directive);

            return result ?? WellKnownError("error.unparsable_dirctive", directive);
        }

        private InterpreterResult? ProcessStatement(string line) => throw new NotImplementedException();

        private InterpreterResult? ProcessExpressionStatement(string line) => throw new NotImplementedException();
    }

    public interface IDirectiveProcessor
    {
        InterpreterResult? ProcessDirective(CallFrame parser, string directive);
    }

    public interface ILineProcessor
    {
        bool CanProcessLine(string line);
        
        InterpreterResult? ProcessLine(CallFrame parser, string line);


        public static ILineProcessor FromDelegate(Predicate<string> canparse, Func<CallFrame, string, InterpreterResult?> process) => new __from_delegate(canparse, process);

        private sealed class __from_delegate
            : ILineProcessor
        {
            private readonly Predicate<string> _canparse;
            private readonly Func<CallFrame, string, InterpreterResult?> _process;


            public __from_delegate(Predicate<string> canparse, Func<CallFrame, string, InterpreterResult?> process)
            {
                _canparse = canparse;
                _process = process;
            }

            public bool CanProcessLine(string line) => _canparse(line);

            public InterpreterResult? ProcessLine(CallFrame parser, string line) => _process(parser, line);
        }
    }

    public interface IIncludeResolver
    {
        bool TryResolve(string path, [MaybeNullWhen(false), NotNullWhen(true)] out (FileInfo physical_file, string content)? resolved);
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
