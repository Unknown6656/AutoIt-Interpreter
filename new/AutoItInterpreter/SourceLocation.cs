using System.IO;
using System;

using Unknown6656.AutoIt3.Localization;
using Unknown6656.AutoIt3.CLI;

namespace Unknown6656.AutoIt3;


/// <summary>
/// Represents the location of a line of code inside a (usually known) source file.
/// </summary>
public readonly struct SourceLocation
    : IEquatable<SourceLocation>
    , IComparable<SourceLocation>
{
    private static LanguagePack UILanguage => MainProgram.LanguageLoader.CurrentLanguage!;
    private static readonly string _unknown_path = $"<{UILanguage["general.unknown"]}>";
    public static SourceLocation Unknown { get; } = new SourceLocation(_unknown_path, -1);


    /// <summary>
    /// The zero-based start line number.
    /// </summary>
    public readonly int StartLineNumber { get; }

    /// <summary>
    /// The zero-based start end line number.
    /// </summary>
    public readonly int EndLineNumber { get; }

    /// <summary>
    /// The source file path. This path can also point to a web or remote resource.
    /// </summary>
    public readonly string FullFileName { get; }

    /// <summary>
    /// Returns whether the source location is equals to <see cref="Unknown"/>, which represents a unknown source code location.
    /// </summary>
    public bool IsUnknown => Equals(Unknown);

    /// <summary>
    /// Indicates whether the current location represents a single line - as opposed to multiple source code lines.
    /// </summary>
    public bool IsSingleLine => EndLineNumber == StartLineNumber;


    /// <summary>
    /// Creates a new instance of <see cref="SourceLocation"/> using the file path (local or remote), as well as the zero-based line number.
    /// </summary>
    /// <param name="file">File path (may also be a remote or non-existent/invalid file path).</param>
    /// <param name="line">Zero-based line number (i.e. a value of <c>0</c> represents the first line in the specified file).</param>
    public SourceLocation(string file, int line)
        : this(file, line, line)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="SourceLocation"/> using the file path (local or remote), as well as the zero-based start and end line number.
    /// </summary>
    /// <param name="file">File path (may also be a remote or non-existent/invalid file path).</param>
    /// <param name="start">Zero-based start line number (i.e. a value of <c>0</c> represents the first line in the specified file).</param>
    /// <param name="end">Zero-based end line number. This value must be greater or equals to <paramref name="start"/>.</param>
    public SourceLocation(string file, int start, int end)
    {
        if (start > end)
            throw new ArgumentException("The end line number must not be smaller than the start line number", nameof(end));

        FullFileName = file.Equals(_unknown_path, StringComparison.OrdinalIgnoreCase) ? file : Path.GetFullPath(new FileInfo(file).FullName);
        StartLineNumber = start;
        EndLineNumber = end;
    }

    public bool Equals(SourceLocation other) => Equals(StartLineNumber, other.StartLineNumber) && Equals(EndLineNumber, other.EndLineNumber) && Equals(FullFileName, other.FullFileName);

    public override bool Equals(object? obj) => obj is SourceLocation loc && Equals(loc);

    public override int GetHashCode() => HashCode.Combine(StartLineNumber, EndLineNumber, FullFileName);

    public override string ToString()
    {
        if (IsUnknown)
            return _unknown_path;

        string endline = IsSingleLine ? "" : $"..{EndLineNumber + 1}";

        return $"\"{FullFileName}\", {UILanguage[IsSingleLine ? "general.line": "general.lines"]} {StartLineNumber + 1}{endline}";
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
