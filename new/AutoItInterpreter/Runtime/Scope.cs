using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System;

using Unknown6656.AutoIt3.Runtime;

namespace Unknown6656.AutoIt3.Runtime
{
    using static Program;


    public readonly struct SourceLocation
        : IEquatable<SourceLocation>
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


        public SourceLocation(FileInfo file, int line, int index = -1)
        {
            FileName = file;
            LineNumber = line;
            CharIndex = index;
        }

        public bool Equals(SourceLocation other) => Equals(LineNumber, other.LineNumber) && Equals(CharIndex, other.CharIndex) && Equals(FileName?.FullName, other.FileName?.FullName);

        public override bool Equals(object? obj) => obj is SourceLocation loc && Equals(loc);

        public override int GetHashCode() => HashCode.Combine(LineNumber, CharIndex, FileName?.FullName);

        public override string ToString() => $"\"{FileName}\", {CurrentLanguage["general.line"]} {LineNumber + 1}:{CharIndex + 1}";

        public static bool operator ==(SourceLocation left, SourceLocation right) => left.Equals(right);

        public static bool operator !=(SourceLocation left, SourceLocation right) => !(left == right);
    }

    public sealed class Variable
    {
        public string FullName { get; }
        public object? Value { set; get; }
        public SourceLocation DeclaredAt { get; }


        public Variable(SourceLocation declaredAt, string full_name)
        {
            DeclaredAt = declaredAt;
            FullName = full_name;
        }

        public override int GetHashCode() => FullName.ToLower().GetHashCode();

        public override bool Equals(object? obj) => obj is Variable { FullName: string n } && string.Equals(FullName, n, StringComparison.InvariantCultureIgnoreCase);
    }

    public sealed class AU3Thread
        : IDisposable
    {
        public ConcurrentStack<CallFrame> CallStack { get; } = new ConcurrentStack<CallFrame>();

        private ScopeStack ScopeStack { get; } = new ScopeStack();

        public Interpreter Interpreter { get; }

        public SourceLocation? CurrentLocation => CallStack.TryPeek(out CallFrame? lp) ? lp.CurrentLocation : (SourceLocation?)null;

        public bool IsDisposed { get; private set; }

        public bool IsMainThread => ReferenceEquals(this, Interpreter.MainThread);


        internal AU3Thread(Interpreter interpreter) => Interpreter = interpreter;

        public CallFrame Start(SourceLocation target)
        {
            Interpreter.AddThread(this);

            return Push(target, null, ScopeType.Global);
        }

        public CallFrame PushFrame(SourceLocation target, string? name) => Push(target, name, ScopeType.Func);

        private CallFrame Push(SourceLocation target, string? name, ScopeType type)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            CallFrame parser = new CallFrame(this, target);

            name ??= target.FileName.FullName;

            CallStack.Push(parser);
            ScopeStack.Push(type, name, target);

            return parser;
        }

        public SourceLocation? PopFrame()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AU3Thread));

            CallStack.TryPop(out _);
            ScopeStack.Pop(ScopeType.Func);

            return CurrentLocation;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            else
                IsDisposed = true;

            Interpreter.RemoveThread(this);
            CallStack.TryPop(out _);
            ScopeStack.Pop(ScopeType.Global);
        }
    }

    public sealed class ScopeStack
    {
        private readonly ConcurrentStack<Scope> _scopes = new ConcurrentStack<Scope>();
        private readonly HashSet<Variable> _variables = new HashSet<Variable>();



        public Scope? CurrentScope => _scopes.TryPeek(out Scope? scope) ? scope : null;

        public Scope Push(ScopeType type, string name, SourceLocation location)
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
                _scopes.TryPop(out _);
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

        public SourceLocation OpeningLocation { get; }



        internal Scope(ScopeStack stack, Scope? parent, ScopeType type, string name, SourceLocation openingLocation)
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
