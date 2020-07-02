using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3.Runtime
{
    using static AST;

    public sealed class ScriptScanner
    {
        private const RegexOptions _REGEX_OPTIONS = RegexOptions.IgnoreCase | RegexOptions.Compiled;
        private static readonly Regex REGEX_COMMENT = new Regex(@"\;[^\""\']*$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_COMMENT_AFTER_STRING1 = new Regex(@"^([^\""\;]*\""[^\""]*\""[^\""\;]*)*(?<cmt>\;).*$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_COMMENT_AFTER_STRING2 = new Regex(@"^([^'\;]*'[^']*'[^'\;]*)*(?<cmt>\;).*$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_CS = new Regex(@"^#(comments\-start|cs)(\b|$)", _REGEX_OPTIONS);
        private static readonly Regex REGEX_CE = new Regex(@"^#(comments\-end|ce)(\b|$)", _REGEX_OPTIONS);
        private static readonly Regex REGEX_REGION = new Regex(@"^#(end-?)?region\b", _REGEX_OPTIONS);
        private static readonly Regex REGEX_PRAGMA = new Regex(@"^#pragma\s+(?<option>[a-z_]\w+)\b\s*(\((?<params>.*)\))?\s*", _REGEX_OPTIONS);
        private static readonly Regex REGEX_LINECONT = new Regex(@"(\s|^)_$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_1LFUNC = new Regex(@"^(?<decl>(volatile)?\s*func\b\s*([a-z_]\w*)\s*\(.*\))\s*->\s*(?<body>.+)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_FUNC = new Regex(@"^(?<volatile>volatile)?\s*func\s+(?<name>[a-z_]\w*)\s*\((?<args>.*)\)$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDFUNC = new Regex(@"^endfunc$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_LABEL = new Regex(@"^(?<name>[a-z_]\w*)\s*:$", _REGEX_OPTIONS);
        private static readonly Regex REGEX_INCLUDEONCE = new Regex(@" ^#include-once(\b|$)", _REGEX_OPTIONS);
        private static readonly Regex REGEX_AUSTARTREGISTER = new Regex(@"^#(onautoitstartregister\s+""(?<func>[^""]+)"")", _REGEX_OPTIONS);
        private static readonly Regex REGEX_AUEXITREGISTER = new Regex(@"^#(onautoitexitregister\s+""(?<func>[^""]+)"")", _REGEX_OPTIONS);
        private static readonly Regex REGEX_REQADMIN = new Regex(@"^#requireadmin\b", _REGEX_OPTIONS);
        private static readonly Regex REGEX_NOTRYICON = new Regex(@"^#notrayicon\b", _REGEX_OPTIONS);

        private static readonly Func<string, (FileInfo physical, string content)?>[] _existing_resolvers =
        {
            ResolveUNC,
            ResolveHTTP,
            ResolveFTP,
        };
        private readonly ConcurrentDictionary<string, ScriptFunction> _cached_functions = new ConcurrentDictionary<string, ScriptFunction>();
        private readonly ConcurrentDictionary<string, ScannedScript> _cached_scripts = new ConcurrentDictionary<string, ScannedScript>();
        private readonly ScannedScript _system_script;


        public ScannedScript[] ActiveScripts => (from thread in Interpreter.Threads
                                                 from frame in thread.CallStack
                                                 select frame.CurrentFunction.Script).Distinct().ToArray();

        public Interpreter Interpreter { get; }


        public ScriptScanner(Interpreter interpreter)
        {
            Interpreter = interpreter;
            _system_script = new ScannedScript(Program.ASM);
        }

        internal void ScanNativeFunctions() => Interpreter.Telemetry.Measure(TelemetryCategory.ScanScript, delegate
        {
            foreach (AbstractFunctionProvider provider in Interpreter.PluginLoader.FunctionProviders)
                foreach (ProvidedNativeFunction function in provider.ProvidedFunctions)
                    _system_script.AddFunction(new NativeFunction(_system_script, function.Name, function.ParameterCount, function.Execute));

            foreach (KeyValuePair<string, ScriptFunction> func in _system_script.Functions)
                _cached_functions.TryAdd(func.Key.ToLower(), func.Value);
        });

        public ScriptFunction? TryResolveFunction(string name)
        {
            _cached_functions.TryGetValue(name.ToLowerInvariant(), out ScriptFunction? func);

            return func;
        }

        public Union<InterpreterError, (FileInfo physical_file, string content)> ResolveScriptFile(SourceLocation include_loc, string path, bool relative)
        {
            (FileInfo physical, string content)? file = Interpreter.Telemetry.Measure<(FileInfo, string)?>(TelemetryCategory.ResolveScript, delegate
            {
                if (Program.CommandLineOptions.StrictMode)
                    try
                    {
                        if (ResolveUNC(path) is { } res)
                            return res;
                    }
                    catch
                    {
                        Interpreter.Telemetry.Measure(TelemetryCategory.Exceptions, delegate { });
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
                            Interpreter.Telemetry.Measure(TelemetryCategory.Exceptions, delegate { });
                        }

                    foreach (AbstractIncludeResolver res in Interpreter.PluginLoader.IncludeResolvers)
                        if (res.TryResolve(path, out file))
                            return file;
                }

                return null;
            });

            if (file is { })
                return file;

            string combined = Path.Combine(Program.INCLUDE_DIR.FullName, path);

            if (!relative && combined != path && ResolveScriptFile(include_loc, combined, false).Is(out file) && file is { })
                return file;

            return InterpreterError.WellKnown(include_loc, "error.unresolved_script", path);
        }

        public Union<InterpreterError, ScannedScript> ScanScriptFile(SourceLocation include_loc, string path, bool relative) =>
            ResolveScriptFile(include_loc, path, relative).Match<Union<InterpreterError, ScannedScript>>(e => e, file => ProcessScriptFile(file.physical_file, file.content));

        private Union<InterpreterError, ScannedScript> ProcessScriptFile(FileInfo file, string content) =>
            Interpreter.Telemetry.Measure<Union<InterpreterError, ScannedScript>>(TelemetryCategory.ScanScript, delegate
            {
                string key = file.FullName;

                if (!_cached_scripts.TryGetValue(key, out ScannedScript? script))
                {
                    script = new ScannedScript(file);

                    AU3Function curr_func = script.GetOrCreateAU3Function(ScriptFunction.GLOBAL_FUNC, null);
                    List<(string line, SourceLocation loc)> lines = From.String(content)
                                                                        .To
                                                                        .Lines()
                                                                        .Select((l, i) => (l, new SourceLocation(file, i)))
                                                                        .ToList();
                    int comment_lvl = 0;
                    Match m;

                    for (int i = 0; i < lines.Count; ++i)
                    {
                        (string line, SourceLocation loc) = lines[i];

                        line = TrimComment(line.TrimStart());

                        if (line.Match(
                            (REGEX_CS, _ => ++comment_lvl),
                            (REGEX_CE, _ => comment_lvl = Math.Max(0, comment_lvl - 1)),
                            (REGEX_REGION, delegate { }
                        )) || comment_lvl > 0)
                            continue;

                        if (line.Match(
                            (REGEX_INCLUDEONCE, _ => script.IncludeOnlyOnce = true),
                            (REGEX_AUSTARTREGISTER, m => script.AddStartupFunction(m.Groups["func"].Value, loc)),
                            (REGEX_AUEXITREGISTER, m => script.AddExitFunction(m.Groups["func"].Value, loc))
                        ))
                            continue;
                        else if (line.Match(REGEX_REQADMIN, out m))
                            line = "#pragma compile(ExecLevel, requireAdministrator)";
                        else if (line.Match(REGEX_NOTRYICON, out m))
                            line = @"Opt(""TrayIconHide"", 1)";

                        if (line.Match(REGEX_PRAGMA, out m))
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

                        while (line.Match(REGEX_LINECONT, out m))
                            if (i++ == lines.Count - 1)
                                return InterpreterError.WellKnown(loc, "error.unexpected_line_cont");
                            else
                            {
                                line = line[..m.Index] + ' ' + TrimComment(lines[i].line.TrimStart());
                                loc = new SourceLocation(loc.FileName, loc.StartLineNumber, lines[i].loc.StartLineNumber);
                            }

                        if (line.Match(REGEX_1LFUNC, out m))
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
                        else if (line.Match(REGEX_FUNC, out m))
                        {
                            string name = m.Groups["name"].Value;
                            string args = m.Groups["args"].Value;
                            bool @volatile = m.Groups["volatile"].Length > 0;

                            if (ScriptFunction.RESERVED_NAMES.Contains(name.ToLower()))
                                return InterpreterError.WellKnown(loc, "error.reserved_name", name);
                            else if (!curr_func.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.unexpected_func", curr_func.Name);
                            else if (_cached_functions.TryGetValue(name.ToLower(), out ScriptFunction? existing) && !existing.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.duplicate_function", existing.Name, existing.Location);

                            IEnumerable<PARAMETER_DECLARATION> @params;
                            HashSet<VARIABLE> parnames = new HashSet<VARIABLE>();
                            bool optional = false;

                            try
                            {
                                @params = ((PARSABLE_EXPRESSION.ParameterDeclaration)Interpreter.ParserProvider.ParameterParser.Parse(args).ParsedValue).Item;
                            }
                            catch (Exception ex)
                            {
                                return Interpreter.Telemetry.Measure(TelemetryCategory.Exceptions, () => InterpreterError.WellKnown(loc, "error.unparsable_line", args, ex.Message));
                            }

                            foreach (PARAMETER_DECLARATION p in @params)
                                if (p.IsByRef && p.IsOptional)
                                    return InterpreterError.WellKnown(loc, "error.byref_default", p.Variable);
                                else if (optional && !p.IsOptional)
                                    return InterpreterError.WellKnown(loc, "error.missing_default", parnames.Count + 1, p.Variable);
                                else if (parnames.Contains(p.Variable))
                                    return InterpreterError.WellKnown(loc, "error.duplicate_param", p.Variable);
                                else
                                {
                                    optional = p.IsOptional;
                                    parnames.Add(p.Variable);
                                }

                            curr_func = script.GetOrCreateAU3Function(name, @params);
                            curr_func.IsVolatile = @volatile;
                            _cached_functions.TryAdd(name.ToLower(), curr_func);

                            Program.PrintDebugMessage($"Scanned {(@volatile ? "(vol) " : "")}func {name}({string.Join(", ", @params)})");
                        }
                        else if (line.Match(REGEX_ENDFUNC, out Match _))
                        {
                            if (curr_func.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.unexpected_endfunc");

                            curr_func = (AU3Function)script.MainFunction;
                        }
                        else if (line.Match(REGEX_LABEL, out m))
                        {
                            if (Program.CommandLineOptions.StrictMode)
                                return InterpreterError.WellKnown(loc, "error.experimental.goto_instructions");

                            string name = m.Groups["name"].Value;

                            if (curr_func.JumpLabels[name] is JumpLabel label)
                                return InterpreterError.WellKnown(loc, "error.duplicate_jumplabel", name, label.Location);

                            curr_func.AddJumpLabel(loc, name);
                            curr_func.AddLine(loc, "");
                        }
                        else if (!string.IsNullOrWhiteSpace(line))
                            curr_func.AddLine(loc, line);
                    }

                    if (!curr_func.IsMainFunction)
                        return InterpreterError.WellKnown(new SourceLocation(file, From.String(content).To.Lines().Length + 1), "error.unexpected_eof");

                    _cached_scripts.TryAdd(key, script);
                }

                return script;
            });

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
                if (line.Match(REGEX_COMMENT, out Match m))
                    line = line[..m.Index];
                else
                {
                    string before = line[..line.IndexOf(';')];

                    if (!line.Contains("$\"") && ((before.CountOccurences("\"") % 2) == 0) && (before.CountOccurences("'") % 2) == 0)
                        line = before.Trim();
                    else if (line.Match(REGEX_COMMENT_AFTER_STRING1, out m))
                        line = line[..m.Groups["cmt"].Index];
                    else if (line.Match(REGEX_COMMENT_AFTER_STRING2, out m))
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
        private readonly Dictionary<string, ScriptFunction> _functions = new Dictionary<string, ScriptFunction>();
        private readonly List<(string func, SourceLocation decl)> _startup = new List<(string, SourceLocation)>();
        private readonly List<(string func, SourceLocation decl)> _exit = new List<(string, SourceLocation)>();


        public ImmutableDictionary<string, ScriptFunction> Functions => _functions.ToImmutableDictionary();

        public ScriptFunction MainFunction => _functions[ScriptFunction.GLOBAL_FUNC];

        public ScannedScriptState State { get; private set; } = ScannedScriptState.Unloaded;

        public bool IncludeOnlyOnce { get; internal set; }

        public bool IsLoaded => State == ScannedScriptState.Loaded;

        public FileInfo Location { get; }


        public ScannedScript(FileInfo location) => Location = location;

        internal AU3Function GetOrCreateAU3Function(string name, IEnumerable<PARAMETER_DECLARATION>? @params)
        {
            _functions.TryGetValue(name.ToLower(), out ScriptFunction? func);

            return func as AU3Function ?? AddFunction(new AU3Function(this, name, @params));
        }

        internal T AddFunction<T>(T function) where T : ScriptFunction
        {
            _functions[function.Name.ToLower()] = function;

            return function;
        }

        internal void AddStartupFunction(string name, SourceLocation decl) => _startup.Add((name.ToLower(), decl));

        internal void AddExitFunction(string name, SourceLocation decl) => _exit.Add((name.ToLower(), decl));

        public InterpreterError? LoadScript(CallFrame frame) => HandleLoading(frame, false);

        public InterpreterError? UnLoadScript(CallFrame frame) => HandleLoading(frame, true);

        private InterpreterError? HandleLoading(CallFrame frame, bool unloading)
        {
            (ScannedScriptState state, List<(string, SourceLocation)>? funcs) = unloading ? (ScannedScriptState.Unloaded, _exit) : (ScannedScriptState.Loaded, _startup);
            InterpreterError? result = null;

            if (State == state)
                return null;

            State = state;

            foreach ((string name, SourceLocation loc) in funcs)
                if (!_functions.TryGetValue(name, out ScriptFunction? func))
                    return InterpreterError.WellKnown(loc, "error.unresolved_func", name);
                else if (func.ParameterCount.MinimumCount > 0)
                    return InterpreterError.WellKnown(func.Location, "error.register_func_argcount");
                else
                    result ??= (InterpreterError?)frame.Call(func, Array.Empty<Variant>());

            return result;
        }

        public override int GetHashCode() => Location.FullName.GetHashCode();

        public override bool Equals(object? obj) => Equals(obj as ScannedScript);

        public override string ToString() => Location.ToString();

        public bool Equals(ScannedScript? other) => other is ScannedScript script && GetHashCode() == script.GetHashCode();

        public bool HasFunction(string name) => _functions.ContainsKey(name.ToLower());


        public static bool operator ==(ScannedScript? s1, ScannedScript? s2) => s1?.Equals(s2) ?? s2 is null;

        public static bool operator !=(ScannedScript? s1, ScannedScript? s2) => !(s1 == s2);
    }

    public abstract class ScriptFunction
        : IEquatable<ScriptFunction>
    {
        internal const string GLOBAL_FUNC = "$global";

        public static string[] RESERVED_NAMES =
        {
            "_", "$_", VARIABLE.Discard.Name, "$global", "global", "static", "dim", "redim", "enum", "step", "local", "for", "in", "next", "default", "null",
            "func", "endfunc", "do", "until", "while", "wend", "if", "then", "else", "endif", "elseif", "select", "endselect", "case", "switch", "endswitch",
            "with", "endwith", "continuecase", "continueloop", "exit", "exitloop", "return", "volatile"
        };


        public string Name { get; }

        public ScannedScript Script { get; }

        public abstract SourceLocation Location { get; }

        public abstract (int MinimumCount, int MaximumCount) ParameterCount { get; }

        public bool IsMainFunction => Name.Equals(GLOBAL_FUNC, StringComparison.InvariantCultureIgnoreCase);


        internal ScriptFunction(ScannedScript script, string name)
        {
            Name = name;
            Script = script;
            Script.AddFunction(this);
        }

        public override int GetHashCode() => HashCode.Combine(Name.ToLower(), Script);

        public override bool Equals(object? obj) => Equals(obj as ScriptFunction);

        public bool Equals(ScriptFunction? other) => other is ScriptFunction f && f.GetHashCode() == GetHashCode();

        public override string ToString() => $"[{Script}] Func {Name}";


        public static bool operator ==(ScriptFunction? s1, ScriptFunction? s2) => s1?.Equals(s2) ?? s2 is null;

        public static bool operator !=(ScriptFunction? s1, ScriptFunction? s2) => !(s1 == s2);
    }

    public sealed class JumpLabel
        : IEquatable<JumpLabel?>
    {
        public AU3Function Function { get; }

        public SourceLocation Location { get; }
        
        public string Name { get; }


        internal JumpLabel(AU3Function function, SourceLocation location, string name)
        {
            Function = function;
            Location = location;
            Name = name;
        }

        public override bool Equals(object? obj) => Equals(obj as JumpLabel);

        public bool Equals(JumpLabel? other) => other != null && EqualityComparer<AU3Function>.Default.Equals(Function, other.Function) && Name == other.Name;

        public override int GetHashCode() => HashCode.Combine(Function, Name);

        public static bool operator ==(JumpLabel? left, JumpLabel? right) => EqualityComparer<JumpLabel>.Default.Equals(left, right);

        public static bool operator !=(JumpLabel? left, JumpLabel? right) => !(left == right);
    }

    public sealed class AU3Function
        : ScriptFunction
    {
        private readonly ConcurrentDictionary<string, JumpLabel> _jumplabels = new ConcurrentDictionary<string, JumpLabel>();
        private readonly ConcurrentDictionary<SourceLocation, string> _lines = new ConcurrentDictionary<SourceLocation, string>();


        public PARAMETER_DECLARATION[] Parameters { get; }

        public override SourceLocation Location
        {
            get
            {
                SourceLocation[] lines = _lines.Keys.OrderBy(Generics.id).ToArray();

                return new SourceLocation(lines[0].FileName, lines[0].StartLineNumber, lines[^1].EndLineNumber);
            }
        }

        public bool IsVolatile { get; internal set; }

        public int LineCount => _lines.Count;

        public override (int MinimumCount, int MaximumCount) ParameterCount { get; }

        public (SourceLocation LineLocation, string LineContent)[] Lines => _lines.OrderBy(k => k.Key).ToArray(k => (k.Key, k.Value));

        public ReadOnlyIndexer<string, JumpLabel?> JumpLabels { get; }


        internal AU3Function(ScannedScript script, string name, IEnumerable<PARAMETER_DECLARATION>? @params)
            : base(script, name)
        {
            Parameters = @params?.ToArray() ?? Array.Empty<PARAMETER_DECLARATION>();
            ParameterCount = (Parameters.Count(p => !p.IsOptional), Parameters.Length);
            JumpLabels = new ReadOnlyIndexer<string, JumpLabel?>(name => _jumplabels.TryGetValue(name.ToLower(), out JumpLabel? label) ? label : null);
        }

        public JumpLabel AddJumpLabel(SourceLocation location, string name)
        {
            name = name.Trim().ToLowerInvariant();

            JumpLabel label = new JumpLabel(this, location, name);

            _jumplabels.AddOrUpdate(name, label, (_, _) => label);

            return label;
        }

        public void AddLine(SourceLocation location, string content) => _lines.AddOrUpdate(location, content, (_, _) => content);

        public override string ToString() => $"{base.ToString()}({string.Join<PARAMETER_DECLARATION>(", ", Parameters)})  [{_lines.Count} Lines]";
    }

    public sealed class NativeFunction
        : ScriptFunction
    {
        private readonly Func<NativeCallFrame, Variant[], FunctionReturnValue> _execute;

        public override (int MinimumCount, int MaximumCount) ParameterCount { get; }

        public override SourceLocation Location { get; } = SourceLocation.Unknown;


        internal NativeFunction(ScannedScript script, string name, (int min, int max) param_count, Func<NativeCallFrame, Variant[], FunctionReturnValue> execute)
            : base(script, name)
        {
            _execute = execute;
            ParameterCount = param_count;
        }

        public FunctionReturnValue Execute(NativeCallFrame frame, Variant[] args) => _execute(frame, args);

        public override string ToString() => "[native] " + base.ToString();
    }

    public sealed class FunctionReturnValue
    {
        private readonly Union<InterpreterError, (Variant @return, int? error, int? extended)> _result;


        private FunctionReturnValue(InterpreterError error) => _result = error;

        private FunctionReturnValue(Variant @return, int? error = null, int? extended = null)
        {
            if (extended is int && error is null)
                error = -1;

            _result = (@return, error, extended);
        }

        public bool IsSuccess(out Variant value) => Is(out value, out int? err, out int? ext) && err is null && ext is null;

        public bool IsFatal([MaybeNullWhen(false), NotNullWhen(true)] out InterpreterError? error) => _result.Is(out error);

        public bool IsError(out int error) => IsError(out _, out error, out _);

        public bool IsError(out int error, out int? extended) => IsError(out _, out error, out extended);

        public bool IsError(out Variant value, out int error) => IsError(out value, out error, out _);

        public bool IsError(out Variant value, out int error, out int? extended)
        {
            bool res = Is(out value, out int? err, out extended);

            if (err is null)
                res = false;

            error = err ?? 0;

            return res;
        }

        private bool Is(out Variant @return, out int? error, out int? extended)
        {
            bool res = _result.Is(out (Variant @return, int? error, int? extended) tuple);

            (@return, error, extended) = tuple;

            return res;
        }

        public static FunctionReturnValue Success(Variant value) => new FunctionReturnValue(value);

        public static FunctionReturnValue Fatal(InterpreterError error) => new FunctionReturnValue(error);

        public static FunctionReturnValue Error(int error) => new FunctionReturnValue(Variant.False, error);

        public static FunctionReturnValue Error(int error, int extended) => new FunctionReturnValue(Variant.False, error, extended);

        public static FunctionReturnValue Error(Variant value, int error) => new FunctionReturnValue(value, error);

        public static FunctionReturnValue Error(Variant value, int error, int extended) => new FunctionReturnValue(value, error, extended);

        public static implicit operator FunctionReturnValue(Variant v) => Success(v);

        public static implicit operator FunctionReturnValue(InterpreterError err) => Fatal(err);

        public static implicit operator FunctionReturnValue(Union<InterpreterError, Variant> union) => union.Match(Fatal, Success);
    }

    public enum ScannedScriptState
    {
        Unloaded,
        Loaded
    }
}
