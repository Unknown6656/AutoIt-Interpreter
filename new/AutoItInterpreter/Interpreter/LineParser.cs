using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Common;
using Unknown6656.IO;
using Unknown6656.Imaging;

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


        public void ResetParser()
        {
            _state = LineParserState.Regular;

            MoveToStart();
        }

        public InterpreterResult? ParseCurrentLine()
        {
            string? line = CurrentLine;
            InterpreterResult? result = null;

            if (string.IsNullOrEmpty(line))
                if (!_state.HasFlag(LineParserState.LineContinuation))
                    return InterpreterResult.OK;
                else
                {

                    // TODO : line continuation
                }

            line = TrimComment(line);

            int index = line.Length;

            line = line.TrimStart();
            index -= line.Length;

            if (line.StartsWith('#'))
                result ??= ProcessDirective(line[1..]);

            if (_state.HasFlag(LineParserState.InsideBlockComment))
                return InterpreterResult.OK;

            // TODO : parse statement
            // TODO : parse expression

            throw new NotImplementedException();
        }

        private string TrimComment(string line)
        {
            Match m;

            if (line.Contains(';'))
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

        private InterpreterResult? ProcessDirective(string directive)
        {
            if (directive.Match(
                (@"^(comments\-start|cs)(\b|$)", _ =>
                {
                    _state |= LineParserState.InsideBlockComment;
                    ++_blockcomment_level;
                }),
                (@"^(comments\-end|ce)(\b|$)", _ =>
                {
                    _blockcomment_level = Math.Max(0, _blockcomment_level - 1);

                    if (_blockcomment_level <= 0)
                        _state &= ~LineParserState.InsideBlockComment;
                })
            ) || _state.HasFlag(LineParserState.InsideBlockComment))
                return InterpreterResult.OK;

            return directive.Match(
                null, // InterpreterResult.WellKnownError,
                (@"^include\s+""(?<path>[^""]+)""", (Match m) => ProcessInclude(m.Groups["path"].Value, true, false)),
                (@"^include\s+<(?<path>[^>]+)>", (Match m) => ProcessInclude(m.Groups["path"].Value, false, false)),
                (@"^include-once\s+""(?<path>[^""]+)""", (Match m) => ProcessInclude(m.Groups["path"].Value, true, true)),
                (@"^include-once\s+<(?<path>[^>]+)>", (Match m) => ProcessInclude(m.Groups["path"].Value, false, true))
            );
        }

        private InterpreterResult ProcessInclude(string path, bool relative, bool once)
        {
            throw new NotImplementedException();
        }
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
