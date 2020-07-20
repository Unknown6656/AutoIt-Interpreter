using System.IO;
using System;

using Unknown6656.AutoIt3.Localization;

namespace Unknown6656.AutoIt3
{
    public readonly struct SourceLocation
        : IEquatable<SourceLocation>
        , IComparable<SourceLocation>
    {
        private static LanguagePack UILanguage => MainProgram.LanguageLoader.CurrentLanguage!;
        public static SourceLocation Unknown { get; } = new SourceLocation($"<{UILanguage["general.unknown"]}>", -1);

        private readonly FileInfo _file;

        /// <summary>
        /// The zero-based start line number.
        /// </summary>
        public readonly int StartLineNumber { get; }
        /// <summary>
        /// The zero-based start end line number.
        /// </summary>
        public readonly int EndLineNumber { get; }
        /// <summary>
        /// The source file path.
        /// </summary>
        // public readonly FileInfo FileName { get; }

        public readonly string FullFileName => Path.GetFullPath(_file.FullName);

        public bool IsUnknown => Equals(Unknown);

        public bool IsSingleLine => EndLineNumber == StartLineNumber;


        public SourceLocation(string file, int line)
            : this(file, line, line)
        {
        }

        public SourceLocation(string file, int start, int end)
        {
            if (start > end)
                throw new ArgumentException("The end line number must not be smaller than the start line number", nameof(end));

            _file = new FileInfo(file);
            StartLineNumber = start;
            EndLineNumber = end;
        }

        public bool Equals(SourceLocation other) => Equals(StartLineNumber, other.StartLineNumber) && Equals(EndLineNumber, other.EndLineNumber) && Equals(FullFileName, other.FullFileName);

        public override bool Equals(object? obj) => obj is SourceLocation loc && Equals(loc);

        public override int GetHashCode() => HashCode.Combine(StartLineNumber, EndLineNumber, FullFileName);

        public override string ToString()
        {
            string s = $"\"{FullFileName}\", ";

            if (IsSingleLine)
                return $"{s}{UILanguage["general.line"]} {StartLineNumber + 1}";
            else
                return $"{s}{UILanguage["general.lines"]} {StartLineNumber + 1}..{EndLineNumber + 1}";
        }

        public int CompareTo(SourceLocation other) =>
            FullFileName != FullFileName ? -1 :
            Equals(other) ? 0 :
            EndLineNumber <= other.StartLineNumber ? -1 :
            StartLineNumber >= other.EndLineNumber ? 1 :
            StartLineNumber.CompareTo(other.StartLineNumber);

        public static bool operator ==(SourceLocation left, SourceLocation right) => left.Equals(right);

        public static bool operator !=(SourceLocation left, SourceLocation right) => !(left == right);

        public static bool operator <(SourceLocation left, SourceLocation right) => left.CompareTo(right) < 0;

        public static bool operator <=(SourceLocation left, SourceLocation right) => left.CompareTo(right) <= 0;

        public static bool operator >(SourceLocation left, SourceLocation right) => left.CompareTo(right) > 0;

        public static bool operator >=(SourceLocation left, SourceLocation right) => left.CompareTo(right) >= 0;
    }
}
