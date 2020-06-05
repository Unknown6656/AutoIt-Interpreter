using System.IO;
using System;

namespace Unknown6656.AutoIt3
{
    using static Program;


    public readonly struct SourceLocation
        : IEquatable<SourceLocation>
        , IComparable<SourceLocation>
    {
        public static SourceLocation Unknown { get; } = new SourceLocation(new FileInfo($"<{CurrentLanguage["general.unknown"]}>"), 0);

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
        public readonly FileInfo FileName { get; }

        public bool IsUnknown => Equals(Unknown);

        public bool IsSingleLine => EndLineNumber == StartLineNumber;


        public SourceLocation(FileInfo file, int line)
            : this(file, line, line)
        {
        }

        public SourceLocation(FileInfo file, int start, int end)
        {
            if (start > end)
                throw new ArgumentException("The end line number must not be smaller than the start line number", nameof(end));

            FileName = file;
            StartLineNumber = start;
            EndLineNumber = end;
        }

        public bool Equals(SourceLocation other) => Equals(StartLineNumber, other.StartLineNumber) && Equals(EndLineNumber, other.EndLineNumber) && Equals(FileName?.FullName, other.FileName?.FullName);

        public override bool Equals(object? obj) => obj is SourceLocation loc && Equals(loc);

        public override int GetHashCode() => HashCode.Combine(StartLineNumber, EndLineNumber, FileName?.FullName);

        public override string ToString()
        {
            string s = $"\"{FileName}\", ";

            if (IsSingleLine)
                return $"{s}{CurrentLanguage["general.line"]} {StartLineNumber + 1}";
            else
                return $"{s}{CurrentLanguage["general.lines"]} {StartLineNumber + 1}..{EndLineNumber + 1}";
        }

        public int CompareTo(SourceLocation other) =>
            FileName?.FullName != other.FileName?.FullName ? -1 :
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
