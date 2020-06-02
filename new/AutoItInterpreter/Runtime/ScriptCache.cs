using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class ScriptCache
    {
        private readonly ConcurrentDictionary<string, CachedScript> _cached_files = new ConcurrentDictionary<string, CachedScript>();


        public Interpreter Interpreter { get; }


        public ScriptCache(Interpreter interpreter) => Interpreter = interpreter;
    }

    public sealed class CachedScript
    {
        // public string Location { get; }
        public ConcurrentDictionary<string, (SourceLocation start, SourceLocation end)> Functions { get; }
    }
}
