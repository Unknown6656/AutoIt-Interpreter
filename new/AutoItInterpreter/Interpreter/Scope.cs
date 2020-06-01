using System.Collections.Generic;
using System.IO;
using System;

namespace Unknown6656.AutoIt3.Interpreter
{
    using static Program;


    public readonly struct Location
        : IEquatable<Location>
    {
        /// <summary>
        /// The zero-based line number.
        /// </summary>
        public readonly int LineNumber { get; }
        /// <summary>
        /// The zero-based char index.
        /// </summary>
        public readonly int CharIndex { get; }
        /// <summary>
        /// The source file path.
        /// </summary>
        public readonly FileInfo FileName { get; }


        public Location(FileInfo file, int line, int index = -1)
        {
            FileName = file;
            LineNumber = line;
            CharIndex = index;
        }

        public bool Equals(Location other) => Equals(LineNumber, other.LineNumber) && Equals(CharIndex, other.CharIndex) && Equals(FileName?.FullName, other.FileName?.FullName);

        public override bool Equals(object? obj) => obj is Location loc && Equals(loc);

        public override int GetHashCode() => HashCode.Combine(LineNumber, CharIndex, FileName?.FullName);

        public override string ToString() => $"\"{FileName}\", {CurrentLanguage["general.line"]} {LineNumber + 1}:{CharIndex + 1}";

        public static bool operator ==(Location left, Location right) => left.Equals(right);

        public static bool operator !=(Location left, Location right) => !(left == right);
    }

    public sealed class Variable
    {
        public string FullName { get; }
        public Location DeclaredAt { get; }
        public object? Value { set; get; }


        public Variable(Location declaredAt, string full_name)
        {
            DeclaredAt = declaredAt;
            FullName = full_name;
        }

        public override int GetHashCode() => FullName.ToLower().GetHashCode();

        public override bool Equals(object? obj) => obj is Variable { FullName: string n } && string.Equals(FullName, n, StringComparison.InvariantCultureIgnoreCase);
    }

    public sealed class ScopeStack
    {
        private readonly Stack<Scope> _scopes = new Stack<Scope>();
        private readonly HashSet<Variable> _variables = new HashSet<Variable>();



        public Scope? CurrentScope => _scopes.Count > 0 ? _scopes.Peek() : null;

        public Scope Push(ScopeType type, string name, Location location)
        {
            Scope scope = new Scope(this, CurrentScope, type, name, location);

            _scopes.Push(scope);

            return scope;
        }

        public void Pop(ScopeType type)
        {
            if (CurrentScope?.Type != type)
                ; // error
            else if (_scopes.Count == 0)
                ; // error
            else
                _scopes.Pop();
        }
    }

    public sealed class Scope
    {
        public const string DELIMITER = "::";


        public string Name { get; }

        public ScopeType Type { get; }

        public ScopeStack Stack { get; }

        public Scope? Parent { get; }

        public string FullName => string.Concat(Parent?.FullName, DELIMITER, Name.ToLower());

        public Location OpeningLocation { get; }



        internal Scope(ScopeStack stack, Scope? parent, ScopeType type, string name, Location openingLocation)
        {
            Name = name;
            Type = type;
            Stack = stack;
            Parent = parent;
            OpeningLocation = openingLocation;
        }
    }

    public enum ScopeType
    {
        Global,
        Func,
        With,
        For,
        ForIn,
        While,
        Do,
        If,
        ElseIf,
        Else,
        Select,
        Switch,
    }
}
