using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Interpreter
{
    public sealed class LineParser
        : IEnumerator<string?>
        , IEnumerable<string?>
    {
        private int _line_number;


        public FileInfo File { get; }

        public string[] Lines { get; }

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

        public void Reset() => _line_number = 0;

        public IEnumerator<string?> GetEnumerator() => this;
        
        IEnumerator IEnumerable.GetEnumerator() => this;
    }
}
