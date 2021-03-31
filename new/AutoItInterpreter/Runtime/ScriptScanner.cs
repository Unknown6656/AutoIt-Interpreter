using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

using Unknown6656.Common;
using Unknown6656.IO;

using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.CLI;

namespace Unknown6656.AutoIt3.Runtime
{
    using static AST;

    /// <summary>
    /// Represents an AutoIt3 script scanning and caching module.
    /// </summary>
    public sealed class ScriptScanner
    {
        private const RegexOptions REGEX_OPTIONS = RegexOptions.IgnoreCase | RegexOptions.Compiled;
        private const string REGEX_FUNC_MODIFIERS = /*lang=regex*/@"((\s+|\b)(?<modifiers>volatile(\s+cached)?|cached(\s+volatile)?)(\s+|\b))?";
        private static readonly Regex REGEX_COMMENT = new(@"\;[^\""\']*$", REGEX_OPTIONS);
        private static readonly Regex REGEX_COMMENT_AFTER_STRING1 = new(@"^([^\""\;]*\""[^\""]*\""[^\""\;]*)*(?<cmt>\;).*$", REGEX_OPTIONS);
        private static readonly Regex REGEX_COMMENT_AFTER_STRING2 = new(@"^([^'\;]*'[^']*'[^'\;]*)*(?<cmt>\;).*$", REGEX_OPTIONS);
        internal static readonly Regex REGEX_CS = new(@"^#(comments\-start|cs)(\b|$)", REGEX_OPTIONS);
        internal static readonly Regex REGEX_CE = new(@"^#(comments\-end|ce)(\b|$)", REGEX_OPTIONS);
        private static readonly Regex REGEX_REGION = new(@"^#(end-?)?region\b", REGEX_OPTIONS);
        private static readonly Regex REGEX_PRAGMA = new(@"^#pragma\s+(?<option>[a-z_]\w+)\b\s*(\((?<params>.*)\))?\s*", REGEX_OPTIONS);
        private static readonly Regex REGEX_LINECONT = new(@"(\s|^)_$", REGEX_OPTIONS);
        private static readonly Regex REGEX_1L_IF = new(@"^\s*(?<if>if\b\s*.+\s*\bthen)\b\s*(?<then>.+)$", REGEX_OPTIONS);
        private static readonly Regex REGEX_1L_FUNC = new($@"^(?<decl>{REGEX_FUNC_MODIFIERS}\s*\bfunc\b\s*([a-z_]\w*)\s*\(.*\))\s*->\s*(?<body>.+)$", REGEX_OPTIONS);
        private static readonly Regex REGEX_FUNC_ASSG = new($@"^(?<target>.+)=\s*(?<func>{REGEX_FUNC_MODIFIERS}\s*\bfunc)\s*(?<args>\(.*\))$", REGEX_OPTIONS);
        private static readonly Regex REGEX_FUNC = new($@"^{REGEX_FUNC_MODIFIERS}\s*\bfunc\s+(?<name>[a-z_]\w*)\s*\((?<args>.*)\)$", REGEX_OPTIONS);
        private static readonly Regex REGEX_ENDFUNC = new(@"^endfunc$", REGEX_OPTIONS);
        private static readonly Regex REGEX_LABEL = new(@"^(?<name>[a-z_]\w*)\s*:$", REGEX_OPTIONS);
        private static readonly Regex REGEX_INCLUDEONCE = new(@"^#include-once(\b|$)", REGEX_OPTIONS);
        private static readonly Regex REGEX_AUSTARTREGISTER = new(@"^#(onautoitstartregister\s+""(?<func>[^""]+)"")", REGEX_OPTIONS);
        private static readonly Regex REGEX_AUEXITREGISTER = new(@"^#(onautoitexitregister\s+""(?<func>[^""]+)"")", REGEX_OPTIONS);
        private static readonly Regex REGEX_REQADMIN = new(@"^#requireadmin\b", REGEX_OPTIONS);
        private static readonly Regex REGEX_NOTRYICON = new(@"^#notrayicon\b", REGEX_OPTIONS);

        private static readonly Func<string, (FileInfo physical, string content)?>[] _existing_resolvers =
        {
            ResolveUNC,
            ResolveHTTP,
            ResolveFTP,
            ResolveSSH,
        };
        private readonly ConcurrentDictionary<string, ScriptFunction> _cached_functions = new();
        private readonly ConcurrentDictionary<int, ScannedScript> _cached_scripts = new();


        public ScannedScript[] ActiveScripts => (from thread in Interpreter.Threads
                                                 from frame in thread.CallStack
                                                 select frame.CurrentFunction.Script).Append(SystemScript).Distinct().ToArray();

        internal ScannedScript SystemScript { get; }

        internal AU3Function AnonymousFunction { get; }

        public Interpreter Interpreter { get; }

        public IEnumerable<ScriptFunction> CachedFunctions => _cached_functions.Values;

        public IEnumerable<ScannedScript> CachedScripts => _cached_scripts.Values;


        internal ScriptScanner(Interpreter interpreter)
        {
            Interpreter = interpreter;
            SystemScript = new ScannedScript(MainProgram.ASM_FILE, "");
            AnonymousFunction = new AU3Function(SystemScript, "<anonymous au3>", null);
        }

        internal void ScanNativeFunctions() => Interpreter.Telemetry.Measure(TelemetryCategory.ScanScript, delegate
        {
            foreach (AbstractFunctionProvider provider in Interpreter.PluginLoader.FunctionProviders)
                foreach (NativeFunction function in provider.ProvidedFunctions)
                    SystemScript.AddFunction(function);

            foreach ((string name, ScriptFunction func) in SystemScript.Functions)
                _cached_functions.TryAdd(name.ToUpperInvariant(), func);
        });

        public ScriptFunction? TryResolveFunction(string name)
        {
            _cached_functions.TryGetValue(name.ToUpperInvariant(), out ScriptFunction? func);

            return func;
        }

        public Union<InterpreterError, (FileInfo physical_file, string content)> ResolveScriptFile(SourceLocation include_loc, string path, bool relative)
        {
            (FileInfo physical, string content)? file = Interpreter.Telemetry.Measure<(FileInfo, string)?>(TelemetryCategory.ResolveScript, delegate
            {
                if (MainProgram.CommandLineOptions.StrictMode)
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

            string combined = Path.Combine(MainProgram.INCLUDE_DIR.FullName, path);

            if (relative && combined != path && ResolveScriptFile(include_loc, combined, false).Is(out file) && file is { })
                return file;

            return InterpreterError.WellKnown(include_loc, "error.unresolved_script", path);
        }

        public Union<InterpreterError, ScannedScript> ScanScriptFile(SourceLocation include_loc, string path, bool relative) =>
            ResolveScriptFile(include_loc, path, relative).Match<Union<InterpreterError, ScannedScript>>(e => e, file => ProcessScriptFile(file.physical_file, file.content));

        internal Union<InterpreterError, ScannedScript> ProcessScriptFile(FileInfo file, string content) =>
            Interpreter.Telemetry.Measure<Union<InterpreterError, ScannedScript>>(TelemetryCategory.ScanScript, delegate
            {
                if (!_cached_scripts.TryGetValue(ScannedScript.GetHashCode(file, content), out ScannedScript? script))
                {
                    script = new ScannedScript(file, content);

                    AU3Function curr_func = script.GetOrCreateAU3Function(ScriptFunction.GLOBAL_FUNC, null);
                    List<(string line, SourceLocation loc)> lines = From.String(content)
                                                                        .ToLines()
                                                                        .Select((l, i) => (l, new SourceLocation(file.FullName, i)))
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
                                @params = @params[..idx].ToUpperInvariant().Trim();
                                value = @params[(idx + 1)..].Trim();
                            }

                            if (ProcessPragma(loc, option.ToUpperInvariant(), @params, value) is InterpreterError err)
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
                                loc = new SourceLocation(loc.FullFileName, loc.StartLineNumber, lines[i].loc.StartLineNumber);
                            }

                        if (line.Match(REGEX_1L_IF, out m))
                            lines.InsertRange(i + 1, new[]
                            {
                                (m.Groups["if"].Value, loc),
                                (m.Groups["then"].Value, loc),
                                ("endif", loc),
                            });
                        else if (line.Match(REGEX_FUNC_ASSG, out m))
                        {
                            if (MainProgram.CommandLineOptions.StrictMode)
                                return InterpreterError.WellKnown(loc, "error.experimental.function_assignment");

                            string name = $"__internal{Interpreter.Random.NextInt() ^ loc.GetHashCode():x8}";

                            lines.InsertRange(i + 1, new[]
                            {
                                ($"{m.Groups["target"]} = {name}", loc),
                                ($"{m.Groups["func"]} {name}{m.Groups["args"]}", loc),
                            });
                        }
                        else if (line.Match(REGEX_1L_FUNC, out m))
                        {
                            if (MainProgram.CommandLineOptions.StrictMode)
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
                            string mods = m.Groups["modifiers"].Value;

                            if (ScriptFunction.RESERVED_NAMES.Contains(name.ToUpperInvariant()))
                                return InterpreterError.WellKnown(loc, "error.reserved_name", name);
                            else if (!curr_func.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.unexpected_func", curr_func.Name);
                            else if (_cached_functions.TryGetValue(name.ToUpperInvariant(), out ScriptFunction? existing) && !existing.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.duplicate_function", existing.Name, existing.Location);

                            IEnumerable<PARAMETER_DECLARATION> @params;
                            HashSet<VARIABLE> parnames = new();
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
                            curr_func.IsVolatile = mods.Contains("volatile", StringComparison.InvariantCultureIgnoreCase);
                            curr_func.IsCached = mods.Contains("cached", StringComparison.InvariantCultureIgnoreCase);
                            _cached_functions.TryAdd(name.ToUpperInvariant(), curr_func);

                            if (MainProgram.CommandLineOptions.StrictMode && curr_func.IsCached)
                                return InterpreterError.WellKnown(loc, "error.experimental.cached_functions");

                            MainProgram.PrintDebugMessage($"Scanned {(curr_func.IsVolatile ? "volatile " : "")}{(curr_func.IsCached ? "cached " : "")}func {name}({string.Join(", ", @params)})");
                        }
                        else if (line.Match(REGEX_ENDFUNC, out Match _))
                        {
                            if (curr_func.IsMainFunction)
                                return InterpreterError.WellKnown(loc, "error.unexpected_endfunc");

                            curr_func = (AU3Function)script.MainFunction;
                        }
                        else if (line.Match(REGEX_LABEL, out m))
                        {
                            if (MainProgram.CommandLineOptions.StrictMode)
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
                        return InterpreterError.WellKnown(new SourceLocation(file.FullName, From.String(content).ToLines().Length + 1), "error.unexpected_eof");

                    _cached_scripts.TryAdd(script.GetHashCode(), script);

                    if (file.Directory is { Exists: true, FullName: string dir })
                        NativeInterop.AddFolderToEnvPath(Path.GetFullPath(dir));
                }

                return script;
            });

        private InterpreterError? ProcessPragma(SourceLocation loc, string option, string key, string? value)
        {
            if (option == "COMPILE")
            {
                switch (key)
                {
                    case "OUT": break;
                    case "ICON": break;
                    case "EXECLEVEL": break;
                    case "UPX": break;
                    case "AUTOITEXECUTEALLOWED": break;
                    case "CONSOLE": break;
                    case "COMPRESSION": break;
                    case "COMPATIBILITY": break;
                    case "X64": break;
                    case "INPUTBOXRES": break;
                    case "COMMENTS": break;
                    case "COMPANYNAME": break;
                    case "FILEDESCRIPTION": break;
                    case "FILEVERSION": break;
                    case "INTERNALNAME": break;
                    case "LEGALCOPYRIGHT": break;
                    case "LEGALTRADEMARKS": break;
                    case "ORIGINALFILENAME": break;
                    case "PRODUCTNAME": break;
                    case "PRODUCTVERSION": break;
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

        public static string TrimComment(string? line)
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

            foreach (string ext in new[] { "", ".au3", ".au2", ".au", ".aupp", ".au++" })
                if (fi?.Exists ?? false)
                    break;
                else
                    fi = new FileInfo(path + ext);

            if (fi is { Exists: true })
            {
                using FileStream stream = new(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using StreamReader reader = new(stream);
                string content = reader.ReadToEnd();

                reader.Close();
                stream.Close();

                return (fi, content);
            }

            return null;
        }

        private static (FileInfo physical, string content)? ResolveHTTP(string path) => (new FileInfo(path), From.WebResource(new Uri(path)).ToString());

        private static (FileInfo physical, string content)? ResolveFTP(string path) => (new FileInfo(path), From.FTP(path).ToString());

        private static (FileInfo physical, string content)? ResolveSSH(string path) => (new FileInfo(path), From.SSH(path).ToString());
    }

    /// <summary>
    /// Represents a scanned AutoIt3 script.
    /// </summary>
    public sealed class ScannedScript
        : IEquatable<ScannedScript>
    {
        private readonly Dictionary<string, ScriptFunction> _functions = new(new CustomEqualityComparer<string>((s1, s2) => s1.Equals(s2, StringComparison.InvariantCultureIgnoreCase)));
        private readonly List<(string func, SourceLocation decl)> _startup = new();
        private readonly List<(string func, SourceLocation decl)> _exit = new();


        public ImmutableDictionary<string, ScriptFunction> Functions => _functions.ToImmutableDictionary();

        public ScriptFunction MainFunction => _functions[ScriptFunction.GLOBAL_FUNC];

        public ScannedScriptState State { get; private set; } = ScannedScriptState.Unloaded;

        public bool IncludeOnlyOnce { get; internal set; }

        public bool IsLoaded => State == ScannedScriptState.Loaded;

        public string OriginalContent { get; }

        public FileInfo Location { get; }


        public ScannedScript(FileInfo location, string original_content)
        {
            Location = location;
            OriginalContent = original_content;
        }

        public AU3Function GetOrCreateAU3Function(string name, IEnumerable<PARAMETER_DECLARATION>? @params)
        {
            _functions.TryGetValue(name.ToUpperInvariant(), out ScriptFunction? func);

            return func as AU3Function ?? AddFunction(new AU3Function(this, name, @params));
        }

        public T AddFunction<T>(T function)
            where T : ScriptFunction
        {
            _functions[function.Name.ToUpperInvariant()] = function;

            return function;
        }

        public bool AddStartupFunction(string name, SourceLocation decl) => AddFunction(name, decl, in _startup);

        public bool AddExitFunction(string name, SourceLocation decl) => AddFunction(name, decl, in _startup);

        public bool RemoveStartupFunction(string name) => RemoveFunction(name, in _startup);

        public bool RemoveExitFunction(string name) => RemoveFunction(name, in _startup);

        private bool AddFunction(string name, SourceLocation decl, in List<(string, SourceLocation)> list)
        {
            if (!list.Any(t => string.Equals(t.Item1, name, StringComparison.InvariantCultureIgnoreCase)))
            {
                list.Add((name.ToUpperInvariant(), decl));

                return true;
            }
            else
                return false;
        }

        private bool RemoveFunction(string name, in List<(string, SourceLocation)> list) => list.RemoveAll(t => string.Equals(t.Item1, name, StringComparison.InvariantCultureIgnoreCase)) != 0;

        public FunctionReturnValue LoadScript(CallFrame frame) => HandleLoading(frame, false);

        public FunctionReturnValue UnLoadScript(CallFrame frame) => HandleLoading(frame, true);

        private FunctionReturnValue HandleLoading(CallFrame frame, bool unloading)
        {
            (ScannedScriptState state, List<(string, SourceLocation)>? funcs) = unloading ? (ScannedScriptState.Unloaded, _exit) : (ScannedScriptState.Loaded, _startup);

            if (State != state)
            {
                State = state;

                foreach ((string name, SourceLocation loc) in funcs)
                    if (!_functions.TryGetValue(name, out ScriptFunction? func))
                        return InterpreterError.WellKnown(loc, "error.unresolved_func", name);
                    else if (func.ParameterCount.MinimumCount > 0)
                        return InterpreterError.WellKnown(func.Location, "error.register_func_argcount");
                    else if (frame.Call(func, Array.Empty<Variant>()).IsFatal(out InterpreterError? error))
                        return error;
            }

            return Variant.True;
        }

        public override int GetHashCode() => GetHashCode(Location, OriginalContent);

        public override bool Equals(object? obj) => Equals(obj as ScannedScript);

        public override string ToString() => Location.ToString();

        public bool Equals(ScannedScript? other) => other is ScannedScript script && GetHashCode() == script.GetHashCode();

        public bool HasFunction(string name) => _functions.ContainsKey(name.ToUpperInvariant());

        public static int GetHashCode(FileInfo location, string content) => HashCode.Combine(Path.GetFullPath(location.FullName), content);

        public static bool operator ==(ScannedScript? s1, ScannedScript? s2) => s1?.Equals(s2) ?? s2 is null;

        public static bool operator !=(ScannedScript? s1, ScannedScript? s2) => !(s1 == s2);
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

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as JumpLabel);

        /// <inheritdoc/>
        public bool Equals(JumpLabel? other) => other != null && EqualityComparer<AU3Function>.Default.Equals(Function, other.Function) && Name == other.Name;

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Function, Name);

        public static bool operator ==(JumpLabel? left, JumpLabel? right) => EqualityComparer<JumpLabel>.Default.Equals(left, right);

        public static bool operator !=(JumpLabel? left, JumpLabel? right) => !(left == right);
    }

    public enum ScannedScriptState
    {
        Unloaded,
        Loaded
    }
}
