using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Unknown6656.Common;
using Unknown6656.IO;

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
        public ConcurrentDictionary<string, (SourceLocation start, SourceLocation end)> FunctionLocations { get; }

        public (SourceLocation start, SourceLocation end) GlobalFunctionLocation
        {
            get
            {
                FunctionLocations.TryGetValue("", out (SourceLocation, SourceLocation) loc);

                return loc;
            }
        }

    }






    public sealed class ScannedScript
    {
        private readonly Dictionary<string, ScannedFunction> _functions = new Dictionary<string, ScannedFunction>();


        public ImmutableDictionary<string, ScannedFunction> Functions => _functions.ToImmutableDictionary();

        public FileInfo Location { get; }


        public ScannedScript(FileInfo location) => Location = location;

        internal void AddFunction(string name, ScannedFunction func) => _functions[name] = func;

        public override int GetHashCode() => base.GetHashCode();
    }

    public sealed class ScannedFunction
    {
        internal const string GLOBAL_FUNC = "$GLOBAL";

        public List<(SourceLocation LineLocation, string LineContent)> Instructions { get; } = new List<(SourceLocation, string)>();

        public ScannedScript Script { get; }

        public bool IsMainFunction => Name.Equals(GLOBAL_FUNC, StringComparison.InvariantCultureIgnoreCase);

        public string Name { get; }


        internal ScannedFunction(ScannedScript script, string name)
        {
            Name = name;
            Script = script;
            Script.AddFunction(name, this);
        }

        public override int GetHashCode() => HashCode.Combine(Name.ToLower(), Script);

        public override bool Equals(object? obj) => obj is ScannedFunction f && f.GetHashCode() == GetHashCode();

        public override string ToString() => $"[{Script}] Func {Name}(...)";
    }

    public class Scanner
    {
        private string TrimComment(string? line)
        {
            Match m;

            if (string.IsNullOrWhiteSpace(line))
                return "";
            else if (line.Contains(';'))
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






        public Union<InterpreterError, ScannedScript> ScanScriptFile(FileInfo file)
        {
            ScannedScript script = new ScannedScript(file);
            string[] lines = From.File(file).To.Lines();
            string? curr_func = null;
            int comment_lvl = 0;

            for (int i = 0; i < lines.Length; ++i)
            {
                SourceLocation loc = new SourceLocation(file, i);
                string line = TrimComment(lines[i].TrimStart());
                bool handled = line.Match(
                    (@"^#(comments\-start|cs)(\b|$)", _ => ++comment_lvl),
                    (@"^#(comments\-end|ce)(\b|$)", _ => comment_lvl = Math.Max(0, comment_lvl - 1))
                );

                if (handled || comment_lvl > 0)
                    continue;

                if (line.Match(@"^func\s+(?<name>[a-z_]\w*)\s*\(.*\)\s*$", out Match m))
                {
                    curr_func = m.Groups["name"].Value;

                    if (funcs.ContainsKey(curr_func))
                        return InterpreterError.WellKnown(loc, , );
                    else
                        funcs[curr_func] = new List<(SourceLocation, string)>();

                    continue;
                }
                else if (line.Match(@"^endfunc$", out Match _))
                {
                    curr_func = null;

                    continue;
                }

                string key = curr_func ?? GLOBAL_FUNC;

                funcs[key].Add((loc, line));
            }

            return ;
        }
    }
}
