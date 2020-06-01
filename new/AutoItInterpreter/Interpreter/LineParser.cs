using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Common;
using Unknown6656.IO;
using Unknown6656.Imaging;
using System.Diagnostics.CodeAnalysis;

namespace Unknown6656.AutoIt3.Interpreter
{
    public sealed class LineParser
        : IEnumerator<string?>
        , IEnumerable<string?>
    {
        private int _line_number;


        public FileInfo File { get; }

        public string[] Lines { get; }

        public Location CurrentLocation => new Location(File, _line_number, -1);

        public string? CurrentLine => _line_number < Lines.Length ? Lines[_line_number] : null;

        string? IEnumerator<string?>.Current => CurrentLine;

        object? IEnumerator.Current => (this as IEnumerator<string?>).Current;


        public LineParser(FileInfo file)
        {
            File = file;
            _line_number = 0;
            Lines = From.File(file).To.Lines();
        }

        public void Dispose()
        {
        }

        public bool MoveNext() => ++_line_number < Lines.Length;

        public void MoveToStart() => MoveTo(0);

        public bool MoveTo(int line) => (_line_number = Math.Min(line, Lines.Length)) < Lines.Length;

        public void Reset() => _line_number = 0;

        public IEnumerator<string?> GetEnumerator() => this;

        IEnumerator IEnumerable.GetEnumerator() => this;




        private LineParserState _state;
        private int _blockcomment_level;
        private ScopeStack _scopestack = new ScopeStack();


        public void ResetParser()
        {
            _state = LineParserState.Regular;

            MoveToStart();
        }

        private InterpreterResult WellKnownError(string key, params object[] args) => InterpreterError.WellKnown(CurrentLocation, key, args);

        public InterpreterResult? ParseCurrentLine(Interpreter interpreter)
        {
            string? line = CurrentLine;
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
        InterpreterResult? ProcessDirective(LineParser parser, string directive);
    }

    public interface ILineProcessor
    {
        bool CanProcessLine(string line);
        
        InterpreterResult? ProcessLine(LineParser parser, string line);


        public static ILineProcessor FromDelegate(Predicate<string> canparse, Func<LineParser, string, InterpreterResult?> process) => new __from_delegate(canparse, process);

        private sealed class __from_delegate
            : ILineProcessor
        {
            private readonly Predicate<string> _canparse;
            private readonly Func<LineParser, string, InterpreterResult?> _process;


            public __from_delegate(Predicate<string> canparse, Func<LineParser, string, InterpreterResult?> process)
            {
                _canparse = canparse;
                _process = process;
            }

            public bool CanProcessLine(string line) => _canparse(line);

            public InterpreterResult? ProcessLine(LineParser parser, string line) => _process(parser, line);
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
