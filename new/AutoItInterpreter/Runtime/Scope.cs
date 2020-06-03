using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace Unknown6656.AutoIt3.Runtime
{
    using static Program;


    public readonly struct SourceLocation
        : IEquatable<SourceLocation>
    {
        public static SourceLocation Unknown { get; } = new SourceLocation(new FileInfo($"<{CurrentLanguage["general.unknown"]}>"), 0);

        /// <summary>
        /// The zero-based line number.
        /// </summary>
        public readonly int LineNumber { get; }
        /// <summary>
        /// The source file path.
        /// </summary>
        public readonly FileInfo FileName { get; }

        public bool IsUnknown => Equals(Unknown);


        public SourceLocation(FileInfo file, int line)
        {
            FileName = file;
            LineNumber = line;
        }

        public bool Equals(SourceLocation other) => Equals(LineNumber, other.LineNumber) && Equals(FileName?.FullName, other.FileName?.FullName);

        public override bool Equals(object? obj) => obj is SourceLocation loc && Equals(loc);

        public override int GetHashCode() => HashCode.Combine(LineNumber, FileName?.FullName);

        public override string ToString() => $"\"{FileName}\", {CurrentLanguage["general.line"]} {LineNumber + 1}";

        public static bool operator ==(SourceLocation left, SourceLocation right) => left.Equals(right);

        public static bool operator !=(SourceLocation left, SourceLocation right) => !(left == right);
    }

    //public sealed class Variable
    //{
    //    public string FullName { get; }
    //    public object? Value { set; get; }
    //    public SourceLocation DeclaredAt { get; }


    //    public Variable(SourceLocation declaredAt, string full_name)
    //    {
    //        DeclaredAt = declaredAt;
    //        FullName = full_name;
    //    }

    //    public override int GetHashCode() => FullName.ToLower().GetHashCode();

    //    public override bool Equals(object? obj) => obj is Variable { FullName: string n } && string.Equals(FullName, n, StringComparison.InvariantCultureIgnoreCase);
    //}

    //public sealed class ScopeStack
    //{
    //    private readonly ConcurrentStack<Scope> _scopes = new ConcurrentStack<Scope>();
    //    private readonly HashSet<Variable> _variables = new HashSet<Variable>();


    //    // TODO : var resolving

    //    public Scope? CurrentScope => _scopes.TryPeek(out Scope? scope) ? scope : null;

    //    public AU3Thread Thread { get; }


    //    internal ScopeStack(AU3Thread thread) => Thread = thread;

    //    public Scope Push(ScopeType type, string name, SourceLocation location)
    //    {
    //        Scope scope = new Scope(this, CurrentScope, type, name, location);

    //        _scopes.Push(scope);

    //        return scope;
    //    }

    //    public InterpreterResult Pop(params ScopeType[] types)
    //    {
    //        if (types.Length == 0)
    //            throw new ArgumentException("The collection of scope types must not be empty.", nameof(types));

    //        ScopeType curr = CurrentScope?.Type ?? ScopeType.Global;

    //        if (!types.Contains(curr))
    //            return InterpreterError.WellKnown(Thread.CurrentLocation, "error.no_matching_close", curr, CurrentScope?.OpeningLocation ?? SourceLocation.Unknown);
    //        else if (_scopes.Count == 0)
    //            return InterpreterError.WellKnown(Thread.CurrentLocation, "error.unexpected_close", Thread.CurrentLineContent, curr);
    //        else
    //        {
    //            _scopes.TryPop(out _);

    //            return InterpreterResult.OK;
    //        }
    //    }
    //}

    //public sealed class Scope
    //{
    //    public const string DELIMITER = "::";


    //    public string Name { get; }

    //    public ScopeType Type { get; }

    //    public ScopeStack Stack { get; }

    //    public Scope? Parent { get; }

    //    public string FullName => string.Concat(Parent?.FullName, DELIMITER, Name.ToLower());

    //    public SourceLocation OpeningLocation { get; }



    //    internal Scope(ScopeStack stack, Scope? parent, ScopeType type, string name, SourceLocation openingLocation)
    //    {
    //        Name = name;
    //        Type = type;
    //        Stack = stack;
    //        Parent = parent;
    //        OpeningLocation = openingLocation;
    //    }
    //}

    //public enum ScopeType
    //{
    //    Global,
    //    Func,
    //    With,
    //    For,
    //    ForIn,
    //    While,
    //    Do,
    //    If,
    //    ElseIf,
    //    Else,
    //    Select,
    //    Switch,
    //    Case,
    //}
}
