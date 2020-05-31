using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unknown6656.AutoIt3.Interpreter
{
    public sealed class ScopeStack
    {
        private readonly Stack<Scope> _scopes = new Stack<Scope>();
        private readonly Dictionary<string, object> _variables = new Dictionary<string, object>();
    }

    public sealed class Scope
    {
        public ScopeType Type { get; }
        public (FileInfo file, int line) OpeningLocation { get; }


        public Scope(ScopeType type, (FileInfo file, int line) openingLocation)
        {
            Type = type;
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
