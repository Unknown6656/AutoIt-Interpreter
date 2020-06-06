using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.IO;
using System;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class ScriptScanner
    {
        private readonly ConcurrentDictionary<string, ScannedScript> _cached_scripts = new ConcurrentDictionary<string, ScannedScript>();
        private readonly ConcurrentDictionary<string, ScannedFunction> _cached_functions = new ConcurrentDictionary<string, ScannedFunction>();
        private static readonly Func<string, (FileInfo physical, string content)?>[] _existing_resolvers =
        {
            ResolveUNC,
            ResolveHTTP,
            ResolveFTP,
        };


        public Interpreter Interpreter { get; }


        public ScriptScanner(Interpreter interpreter) => Interpreter = interpreter;

        public Union<InterpreterError, (FileInfo physical_file, string content)> ResolveScriptFile(SourceLocation include_loc, string path)
        {
            (FileInfo physical, string content)? file = null;

            if (Program.CommandLineOptions.StrictMode)
                try
                {
                    if (ResolveUNC(path) is { } res)
                        return res;
                }
                catch
                {
                }
            else
            {
                foreach (Func<string, (FileInfo, string)?> resolver in _existing_resolvers)
                    try
                    {
                        if (resolver(path) is { } res)
                            return res;
                    }
                    catch
                    {
                    }

                foreach (AbstractIncludeResolver res in Interpreter.PluginLoader.IncludeResolvers)
                    if (res.TryResolve(path, out file))
                        return file;
            }

            return InterpreterError.WellKnown(include_loc, "error.unresolved_script", path);
        }

        public Union<InterpreterError, ScannedScript> ScanScriptFile(SourceLocation include_loc, string path, ScriptScanningOptions options) =>
            ResolveScriptFile(include_loc, path).Match<Union<InterpreterError, ScannedScript>>(err => err, file =>
            {
                string key = file.physical_file.FullName;

                if (!_cached_scripts.TryGetValue(key, out ScannedScript? script))
                {
                    script = new ScannedScript(file.physical_file);

                    ScannedFunction curr_func = script.GetOrCreateFunction(ScannedFunction.GLOBAL_FUNC);
                    List<(string line, SourceLocation loc)> lines = From.String(file.content)
                                                                        .To
                                                                        .Lines()
                                                                        .Select((l, i) => (l, new SourceLocation(file.physical_file, i)))
                                                                        .ToList();
                    int comment_lvl = 0;
                    Match m;

                    for (int i = 0; i < lines.Count; ++i)
                    {
                        (string line, SourceLocation loc) = lines[i];

                        line = TrimComment(line.TrimStart());

                        if (line.Match(
                            (@"^#(comments\-start|cs)(\b|$)", _ => ++comment_lvl),
                            (@"^#(comments\-end|ce)(\b|$)", _ => comment_lvl = Math.Max(0, comment_lvl - 1)),
                            (@"^#(end-?)?region\b", delegate { }
                        )) || comment_lvl > 0)
                            continue;

                        if (line.Match(
                            (@"^#(onautoitstartregister\s+""(?<func>[^""]+)"")", m => script.AddStartupFunction(m.Groups["func"].Value, loc)),
                            (@"^#(onautoitexitregister\s+""(?<func>[^""]+)"")", m => script.AddExitFunction(m.Groups["func"].Value, loc))
                        ))
                            continue;
                        else if (line.Match(@"^#requireadmin\b", out m))
                            line = "#pragma compile(ExecLevel, requireAdministrator)";
                        else if (line.Match(@"^#notrayicon\b", out m))
                            line = @"Opt(""TrayIconHide"", 1)";

                        if (line.Match(@"^#pragma\s+(?<option>[a-z_]\w+)\b\s*(\((?<params>.*)\))?\s*", out m))
                        {
                            string option = m.Groups["option"].Value.Trim();
                            string @params = m.Groups["params"].Value;
                            string? value = null;

                            if (@params.IndexOf(',') is int idx && idx > 0)
                            {
                                @params = @params[..idx].ToLower().Trim();
                                value = @params[(idx + 1)..].Trim();
                            }

                            if (ProcessPragma(loc, option.ToLower(), @params, value) is InterpreterError err)
                                return err;
                            else
                                continue;
                        }

                        while (line.Match(@"(\s|^)_$", out m))
                            if (i++ == lines.Count - 1)
                                return InterpreterError.WellKnown(loc, "error.unexpected_line_cont");
                            else
                            {
                                line = line[..m.Index] + ' ' + TrimComment(lines[i].line.TrimStart());
                                loc = new SourceLocation(loc.FileName, loc.StartLineNumber, lines[i].loc.StartLineNumber);
                            }

                        if (line.Match(@"^(?<decl>func\s+([a-z_]\w*)\s*\(.*\))\s*->\s*(?<body>.*)$", out m))
                        {
                            if (Program.CommandLineOptions.StrictMode)
                                return InterpreterError.WellKnown(loc, "error.experimental.one_liner");

                            lines.InsertRange(i + 1, new[]
                            {
                                (m.Groups["decl"].Value, loc),
                                (m.Groups["body"].Value, loc),
                                ("endfunc", loc),
                            });
                        }
                        else if (line.Match(@"^func\s+(?<name>[a-z_]\w*)\s*\(.*\)$", out m))
                        {
                            string name = m.Groups["name"].Value;

                            if (!curr_func.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.unexpected_func", curr_func.Name);
                            else if (_cached_functions.TryGetValue(name.ToLower(), out ScannedFunction existing) && !existing.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.duplicate_function", existing.Name, existing.Location);

                            curr_func = script.GetOrCreateFunction(name);
                            _cached_functions.TryAdd(name.ToLower(), curr_func);
                        }
                        else if (line.Match(@"^endfunc$", out Match _))
                        {
                            if (curr_func.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.unexpected_endfunc");

                            curr_func = script.MainFunction;
                        }
                        else if (!string.IsNullOrWhiteSpace(line))
                            curr_func.AddLine(loc, line);
                    }

                    _cached_scripts.TryAdd(key, script);
                }

                return script;
            });

        public ScannedFunction? TryResolveFunction(string name)
        {
            _cached_functions.TryGetValue(name.ToLower(), out ScannedFunction? func);

            return func;
        }

        private InterpreterError? ProcessPragma(SourceLocation loc, string option, string key, string? value)
        {
            if (option == "compile")
            {
                switch (key)
                {
                    case "out": break;
                    case "icon": break;
                    case "execlevel": break;
                    case "upx": break;
                    case "autoitexecuteallowed": break;
                    case "console": break;
                    case "compression": break;
                    case "compatibility": break;
                    case "x64": break;
                    case "inputboxres": break;
                    case "comments": break;
                    case "companyname": break;
                    case "filedescription": break;
                    case "fileversion": break;
                    case "internalname": break;
                    case "legalcopyright": break;
                    case "legaltrademarks": break;
                    case "originalfilename": break;
                    case "productname": break;
                    case "productversion": break;
                    default:
                        return InterpreterError.WellKnown(loc, "error.unhandled_pragma_key", key, option);
                }

                return InterpreterError.WellKnown(loc, "error.not_yet_implemented", "compile");
            }

            foreach (AbstractPragmaProcessor proc in Interpreter.PluginLoader.PragmaProcessors)
                if (proc.PragmaName.Equals(option, StringComparison.InvariantCultureIgnoreCase))
                    if (proc.CanProcessPragmaKey(key))
                        return proc.ProcessPragma(loc, key, value);
                    else
                        return InterpreterError.WellKnown(loc, "error.unhandled_pragma_key", key, option);

            return InterpreterError.WellKnown(loc, "error.unhandled_pragma_option", option);
        }

        private static string TrimComment(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return "";
            else if (line.Contains(';'))
                if (line.Match(@"\;[^\""\']*$", out Match m))
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

        private static (FileInfo physical, string content)? ResolveUNC(string path)
        {
            FileInfo? fi = null;

            foreach (string ext in new[] { "", ".au3", ".au2", ".au" })
                if (fi?.Exists ?? false)
                    break;
                else
                    fi = new FileInfo(path + ext);

            if (fi is { Exists: true })
                return (fi, From.File(fi).To.String());

            return null;
        }

        private static (FileInfo physical, string content)? ResolveHTTP(string path) => (new FileInfo(path), From.WebResource(path).To.String());

        private static (FileInfo physical, string content)? ResolveFTP(string path) => (new FileInfo(path), From.FTP(path).To.String());

        private static (FileInfo physical, string content)? ResolveSSH(string path) => (new FileInfo(path), From.SSH(path).To.String());
    }

    public sealed class ScannedScript
        : IEquatable<ScannedScript>
    {
        private readonly Dictionary<string, ScannedFunction> _functions = new Dictionary<string, ScannedFunction>();
        private readonly List<(string func, SourceLocation decl)> _startup = new List<(string, SourceLocation)>();
        private readonly List<(string func, SourceLocation decl)> _exit = new List<(string, SourceLocation)>();


        public ImmutableDictionary<string, ScannedFunction> Functions => _functions.ToImmutableDictionary();

        public ScannedFunction MainFunction => _functions[ScannedFunction.GLOBAL_FUNC];

        public ScannedScriptState State { get; private set; } = ScannedScriptState.Unloaded;

        public bool IsLoaded => State == ScannedScriptState.Loaded;

        public FileInfo Location { get; }


        public ScannedScript(FileInfo location) => Location = location;

        internal ScannedFunction GetOrCreateFunction(string name) =>
            _functions.TryGetValue(name.ToLower(), out ScannedFunction? func) ? func : AddFunction(new ScannedFunction(this, name));

        internal ScannedFunction AddFunction(ScannedFunction function) => _functions[function.Name.ToLower()] = function;

        internal void AddStartupFunction(string name, SourceLocation decl) => _startup.Add((name.ToLower(), decl));

        internal void AddExitFunction(string name, SourceLocation decl) => _exit.Add((name.ToLower(), decl));

        public InterpreterResult LoadScript(CallFrame frame)
        {
            if (State == ScannedScriptState.Loaded)
                return InterpreterResult.OK;

            State = ScannedScriptState.Loaded;

            InterpreterResult? result = null;

            foreach ((string name, SourceLocation loc) in _startup)
                if (_functions.TryGetValue(name, out ScannedFunction? func))
                {
                    if (result?.IsOK ?? false)
                        result = null;

                    result ??= frame.Call(func);
                }
                else
                    return InterpreterError.WellKnown(loc, "error.unresolved_func", name);

            return result ?? InterpreterResult.OK;
        }

        public InterpreterResult UnLoadScript(CallFrame frame)
        {
            if (State == ScannedScriptState.Unloaded)
                return InterpreterResult.OK;

            State = ScannedScriptState.Unloaded;

            InterpreterResult? result = null;

            foreach ((string name, SourceLocation loc) in _exit)
                if (_functions.TryGetValue(name, out ScannedFunction? func))
                {
                    if (result?.IsOK ?? false)
                        result = null;

                    result ??= frame.Call(func);
                }
                else
                    return InterpreterError.WellKnown(loc, "error.unresolved_func", name);

            return result ?? InterpreterResult.OK;
        }

        public override int GetHashCode() => Location.FullName.GetHashCode();

        public override bool Equals(object? obj) => Equals(obj as ScannedScript);

        public override string ToString() => Location.ToString();

        public bool Equals(ScannedScript? other) => other is ScannedScript script && GetHashCode() == script.GetHashCode();

        public bool HasFunction(string name) => _functions.ContainsKey(name.ToLower());


        public static bool operator ==(ScannedScript? s1, ScannedScript? s2) => s1?.Equals(s2) ?? s2 is null;

        public static bool operator !=(ScannedScript? s1, ScannedScript? s2) => !(s1 == s2);
    }

    public sealed class ScannedFunction
        : IEquatable<ScannedFunction>
    {
        internal const string GLOBAL_FUNC = "$global";

        private readonly ConcurrentDictionary<SourceLocation, string> _lines = new ConcurrentDictionary<SourceLocation, string>();


        public string Name { get; }

        public ScannedScript Script { get; }

        public SourceLocation Location
        {
            get
            {
                SourceLocation[] lines = _lines.Keys.OrderBy(Generics.id).ToArray();

                return new SourceLocation(lines[0].FileName, lines[0].StartLineNumber, lines[^1].EndLineNumber);
            }
        }


        public bool IsMainFunction => Name.Equals(GLOBAL_FUNC, StringComparison.InvariantCultureIgnoreCase);

        public (SourceLocation LineLocation, string LineContent)[] Lines => _lines.OrderBy(k => k.Key).ToArray(k => (k.Key, k.Value));

        public int LinesCount => _lines.Count;


        internal ScannedFunction(ScannedScript script, string name)
        {
            Name = name;
            Script = script;
            Script.AddFunction(this);
        }

        public void AddLine(SourceLocation location, string content) => _lines.AddOrUpdate(location, content, (l, c) => content);

        public override int GetHashCode() => HashCode.Combine(Name.ToLower(), Script);

        public override bool Equals(object? obj) => Equals(obj as ScannedFunction);

        public bool Equals(ScannedFunction? other) => other is ScannedFunction f && f.GetHashCode() == GetHashCode();

        public override string ToString() => $"[{Script}] Func {Name}(...)  ({_lines.Count} Lines)";


        public static bool operator ==(ScannedFunction? s1, ScannedFunction? s2) => s1?.Equals(s2) ?? s2 is null;

        public static bool operator !=(ScannedFunction? s1, ScannedFunction? s2) => !(s1 == s2);
    }

    public enum ScannedScriptState
    {
        Unloaded,
        Loaded
    }

    [Flags]
    public enum ScriptScanningOptions
        : byte
    {
        Regular = 0b_0000_0000,
        IncludeOnce = 0b_0000_0001,
        RelativePath = 0b_0000_0010,
    }
}
