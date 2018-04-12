using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace CSAutoItInterpreter
{
    public sealed class Interpreter
    {
        private InterpreterSettings Settings { get; }
        public InterpreterContext RootContext { get; }


        public Interpreter(string path, InterpreterSettings settings)
        {
            RootContext = new InterpreterContext(path);
            Settings = settings;
            Settings.IncludeDirectories = Settings.IncludeDirectories.Select(x => x.Trim().Replace('\\', '/')).Distinct().ToArray();

            if (RootContext.Content is null)
                throw new FileNotFoundException("The given file could not be found, accessed or read.", path);
        }

        public void DoMagic()
        {
            InterpreterState state = InterpretScript(RootContext, Settings);



            ///////////////////////////////////////////// DEBUGGING /////////////////////////////////////////////

            Console.WriteLine(new string('=', 200));

            foreach (var fn in state.Functions.Keys)
            {
                var func = state.Functions[fn];

                Console.WriteLine($"---------------------------------------- function {fn} ----------------------------------------");

                foreach (var l in func.Lines)
                {
                    Console.CursorLeft = 10;
                    Console.Write(l.Context);
                    Console.CursorLeft = 40;
                    Console.WriteLine(l.Line);
                }
            }

            Console.WriteLine(new string('=', 200));

            foreach (var e in state.Errors)
                Console.WriteLine(e);

            Console.WriteLine(new string('=', 200));
        }

        private static (string Content, int[] OriginalLineNumbers, FileInfo File)[] FetchLines(InterpreterContext context)
        {
            string raw = context.Content;

            List<(string, int[])> lines = new List<(string, int[])>();
            List<int> lnmbrs = new List<int>();
            bool comment = false;
            string prev = "";
            int lcnt = 0;

            foreach (string line in raw.Replace("\r\n", "\n").Split('\n'))
            {
                string tline = line.Trim();

                if (tline.Match(@"^\#(comments\-start|cs)", out _))
                    comment = true;
                else if (tline.Match(@"^\#(comments\-end|ce)", out _))
                    comment = false;
                else if (!comment)
                {
                    if (tline.Match(@"\;[^\""]$", out Match m))
                        tline = tline.Remove(m.Index).Trim();
                    else if (tline.Match(@"^([^\""\;]*\""[^\""]*\""[^\""\;]*)*(?<cmt>\;).*$", out m))
                        tline = tline.Remove(m.Groups["cmt"].Index).Trim();

                    if (tline.Match(@"\s+_\s*$", out m))
                    {
                        prev = $"{prev} {tline.Remove(m.Index).Trim()}";
                        lnmbrs.Add(lcnt);
                    }
                    else
                    {
                        lnmbrs.Add(lcnt);
                        lines.Add(($"{prev} {tline}".Trim(), lnmbrs.ToArray()));
                        lnmbrs.Clear();

                        prev = "";
                    }
                }

                ++lcnt;
            }

            return (from ln in lines
                    where ln.Item1.Length > 0
                    select (ln.Item1, ln.Item2, context.SourcePath)).ToArray();
        }

        private static InterpreterState InterpretScript(InterpreterContext context, InterpreterSettings settings)
        {
            List<(string Line, int[] OriginalLineNumbers, FileInfo File)> lines = new List<(string, int[], FileInfo)>();
            InterpreterState state = new InterpreterState
            {
                CurrentContext = context,
                GlobalFunction = new FunctionScope(new DefinitionContext(context.SourcePath, -1))
            };
            int locindx = 0;

            lines.AddRange(FetchLines(context));

            while (locindx < lines.Count)
            {
                string Line = lines[locindx].Line;
                DefinitionContext defcntx = new DefinitionContext(
                    lines[locindx].File,
                    lines[locindx].OriginalLineNumbers[0],
                    lines[locindx].OriginalLineNumbers.Length > 1 ? (int?)lines[locindx].OriginalLineNumbers.Last() : null
                );
                void fail(string msg) => state.ReportError(msg, defcntx);

                if (Line.StartsWith('#'))
                {
                    string path = ProcessDirective(Line.Substring(1), state, settings, fail);

                    try
                    {
                        FileInfo inclpath = path.Length > 0 ? new FileInfo(path) : default;

                        if (inclpath?.Exists ?? false)
                            using (StreamReader rd = inclpath.OpenText())
                            {
                                lines.RemoveAt(locindx);
                                lines.InsertRange(locindx, FetchLines(new InterpreterContext(inclpath)));

                                --locindx;
                            }
                    }
                    catch
                    {
                        fail($"The include file '{path}' could not be found or is inaccessible.");
                    }
                }
                else if (!ProcessFunctionDeclaration(Line, defcntx, state, fail))
                    (state.CurrentFunction is FunctionScope f ? f : state.GlobalFunction).Lines.Add((Line, defcntx));

                ++locindx;
            }

            ProcessFunctions(state);

            return state;
        }

        private static void ProcessFunctions(InterpreterState state)
        {













        }

        private static bool ProcessFunctionDeclaration(string Line, DefinitionContext defcntx, InterpreterState st, Action<string> err)
        {
            void __procfunc(string name, string[] par, string[] opar)
            {
                if (st.CurrentFunction is null)
                {
                    string lname = name.ToLower();

                    if (st.Functions.ContainsKey(lname))
                        err($"A function named '{name}' has already been declared (See {st.Functions[lname].Context}).");
                    else
                    {

                        // todo : parameter parsing


                        st.Functions[lname] = st.CurrentFunction = new FunctionScope(defcntx)
                        {
                            // Parameters = ,
                        };
                    }
                }
                else
                    err("A function cannot be declared inside an other function (yet).");
            }

            if (Line.Match(@"^func\s+(?<name>[a-z_]\w*)\s*\(\s*(?<params>((const\s)?\s*(byref\s)?\s*\$[a-z]\w*\s*)(,\s*(const\s)?\s*(byref\s)?\s*\$[a-z]\w*\s*)*)?\s*(?<optparams>(,\s*\$[a-z]\w*\s*=\s*.+\s*)*)\s*\)$", out Match m))
                __procfunc(
                    m.Get("name"),
                    m.Get("params").Split(',').Select(s => s.Trim()).Where(Enumerable.Any).ToArray(),
                    m.Get("optparams").Split(',').Select(s => s.Trim()).Where(Enumerable.Any).ToArray()
                );
            else if (Line.Match(@"^func\s+(?<name>[a-z_]\w*)\s*\(\s*(?<optparams>(\$[a-z]\w*\s*=\s*.+\s*)(,\s*\$[a-z]\w*\s*=\s*.+\s*)*)?\s*\)$", out m))
                __procfunc(
                    m.Get("name"),
                    new string[0],
                    m.Get("optparams").Split(',').Select(s => s.Trim()).Where(Enumerable.Any).ToArray()
                );
            else if (Line.Match("^endfunc$", out m))
                if (st.CurrentFunction is null)
                    err("A function has to be declared with 'Func ...' before it can be closed wih 'EndFunc'.");
                else
                    st.CurrentFunction = null;
            else
                return false;

            return true;
        }

        private static void ProcessPrgamaCompileOption(string name, string value, CompileInfo ci, Action<string> err)
        {
            value = value.Trim('\'', '"', ' ', '\t', '\r', '\n', '\v');

            name.ToLower().Switch(new Dictionary<string, Action>
            {
				["out"] = () => ci.FileName = value,
				["icon"] = () => ci.IconPath = value,
				["execlevel"] = () => ci.ExecLevel = (ExecutionLevel)Enum.Parse(typeof(ExecutionLevel), value, true),
				["upx"] = () => ci.UPX = bool.Parse(value),
				["autoitexecuteallowed"] = () => ci.AutoItExecuteAllowed = bool.Parse(value),
				["console"] = () => ci.ConsoleMode = bool.Parse(value),
				["compression"] = () => ci.Compression = byte.TryParse(value, out byte b) && (b % 2) == 1 && b < 10 ? b : throw null,
				["compatibility"] = () => ci.Compatibility = (Compatibility)Enum.Parse(typeof(Compatibility), value, true),
                ["x64"] = () => ci.X64 = bool.Parse(value),
				["inputboxres"] = () => ci.InputBoxRes = bool.Parse(value),
				["comments"] = () => ci.AssemblyComment = value,
				["companyname"] = () => ci.AssemblyCompanyName = value,
				["filedescription"] = () => ci.AssemblyFileDescription = value,
				["fileversion"] = () => ci.AssemblyFileVersion = Version.Parse(value.Contains(',') ? value.Remove(value.IndexOf(',')).Trim() : value),
				["internalname"] = () => ci.AssemblyInternalName = value,
				["legalcopyright"] = () => ci.AssemblyCopyright = value,
				["legaltrademarks"] = () => ci.AssemblyTrademarks = value,
				["originalfilename"] = () => { /* do nothing */ },
				["productname"] = () => ci.AssemblyProductName = value,
				["productversion"] = () => ci.AssemblyProductVersion = Version.Parse(value.Contains(',') ? value.Remove(value.IndexOf(',')).Trim() : value),
            },
            () => err($"The directive '{name}' is either invalid or currently unsupported."));
        }

        private static string ProcessDirective(string line, InterpreterState st, InterpreterSettings settings, Action<string> err)
        {
            string inclpath = "";

            line.Match(
                ("^notrayicon$", _ => st.TrayIcon = false),
                ("^requireadmin$", _ => st.RequireAdmin = true),
                ("^include-once$", _ => st.IsIncludeOnce = true),
                (@"^include(\s|\b)\s*(\<(?<rel>.*)\>|\""(?<abs1>.*)\""|\'(?<abs2>.*)\')$", m => {
                    string path = m.Get("abs1");

                    if (path.Length == 0)
                        path = m.Get("abs2");

                    path = path.Replace('\\', '/');

                    FileInfo nfo = new FileInfo($"{st.CurrentContext.SourcePath.FullName}/../{path}");

                    if (path.Length > 0)
                        if (!nfo.Exists)
                            nfo = new FileInfo(path);
                        else
                            try
                            {
                                include();

                                return;
                            }
                            catch
                            {
                            }
                    else
                        path = m.Get("rel").Replace('\\', '/');

                    foreach (string dir in settings.IncludeDirectories)
                        try
                        {
                            string ipath = $"{dir}/{path}";

                            if ((nfo = new FileInfo(ipath)).Exists && !Directory.Exists(ipath))
                            {
                                include();

                                return;
                            }
                        }
                        catch
                        {
                        }

                    err($"The include file '{path}' could not be found.");


                    void include()
                    {
                        if (!st.IncludeOncePaths.Contains(nfo.FullName))
                            inclpath = nfo.FullName;

                        if (inclpath.Match(@"^#include\-once$", out _, RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase))
                            st.IncludeOncePaths.Add(nfo.FullName);
                    }
                }),
                (@"^onautoitstartregister\b\s*\""(?<func>.*)\""$", m => st.StartFunctions.Add(m.Groups["func"].ToString().Trim())),
                (@"^pragma\b\s*(?<opt>[a-z]\w*)\s*\((?<name>[a-z]\w*)\s*\,\s*(?<value>.+)\s*\)$", m =>
                {
                    string opt = m.Get("opt");
                    string name = m.Get("name");
                    string value = m.Get("value");

                    try
                    {
                        switch (opt.ToLower())
                        {
                            case "compile":
                                ProcessPrgamaCompileOption(name, value, st.CompileInfo, err);

                                break;
                            default:
                                err($"The pragma option '{opt}' is currently not supported.");

                                break;
                        }
                    }
                    catch
                    {
                        err($"The value '{value}' is invalid for the directive '{name}'");
                    }
                })
            );

            return inclpath;
        }
    }

    public sealed class InterpreterState
    {
        private const string GLOBAL_FUNC_NAME = "__global<>";

        private List<InterpreterError> _errors;


        public InterpreterContext CurrentContext { set; get; }
        public Dictionary<string, FunctionScope> Functions { get; }
        public FunctionScope CurrentFunction { set; get; }
        public List<string> IncludeOncePaths { get; }
        public List<string> StartFunctions { get; }
        public CompileInfo CompileInfo { get; }
        public bool IsIncludeOnce { set; get; }
        public bool RequireAdmin { set; get; }
        public bool TrayIcon { set; get; }

        public FunctionScope GlobalFunction
        {
            set => Functions[GLOBAL_FUNC_NAME] = value;
            get => Functions[GLOBAL_FUNC_NAME];
        }

        public InterpreterError[] Errors => _errors.ToArray();


        public InterpreterState()
        {
            Functions = new Dictionary<string, FunctionScope> { [GLOBAL_FUNC_NAME] = null };
            _errors = new List<InterpreterError>();
            IncludeOncePaths = new List<string>();
            StartFunctions = new List<string>();
            CompileInfo = new CompileInfo();
            TrayIcon = true;
        }

        public void ReportError(string msg, DefinitionContext ctx) => _errors.Add(new InterpreterError(msg, ctx));
    }

    public sealed class GlobalScope
    {
        public List<(string, DefinitionContext)> Globals { get; }
    }

    public sealed class FunctionScope
    {
        public Dictionary<string, (DefinitionContext context, bool Constant, int ArrayDim)> Locals { get; }
        public List<(string Name, bool ByRef, bool Constant)> Parameters { get; }
        public List<(string Line, DefinitionContext Context)> Lines { get; }
        public DefinitionContext Context { get; }


        public FunctionScope(DefinitionContext ctx)
        {
            Locals = new Dictionary<string, (DefinitionContext, bool, int)>();
            Lines = new List<(string, DefinitionContext)>();
            Parameters = new List<(string, bool, bool)>();
            Context = ctx;
        }
    }

    public sealed class InterpreterContext
    {
        public FileInfo SourcePath { get; }
        public string Content { get; }


        public InterpreterContext(string path)
            : this(new FileInfo(path))
        {
        }

        public InterpreterContext(FileInfo path)
        {
            SourcePath = path;

            if (SourcePath.Exists)
                using (StreamReader rd = SourcePath.OpenText())
                    Content = rd.ReadToEnd();
        }
    }

    public sealed class CompileInfo
    {
        public string FileName { set; get; } = "AutoItApplication.exe";
        public string IconPath { set; get; }
        public ExecutionLevel ExecLevel { set; get; }
        public Compatibility Compatibility { set; get; }
        public bool AutoItExecuteAllowed { set; get; }
        public bool ConsoleMode { set; get; }
        public byte Compression { set; get; }
        public bool UPX { set; get; }
        public bool X64 { set; get; }
        public bool InputBoxRes { set; get; }
        public string AssemblyComment { set; get; }
        public string AssemblyCompanyName { set; get; }
        public string AssemblyFileDescription { set; get; }
        public Version AssemblyFileVersion { set; get; }
        public string AssemblyInternalName { set; get; }
        public string AssemblyCopyright { set; get; }
        public string AssemblyTrademarks { set; get; }
        public string AssemblyProductName { set; get; }
        public Version AssemblyProductVersion { set; get; }


        internal CompileInfo()
        {
        }
    }

    public sealed class InterpreterError
    {
        public DefinitionContext ErrorContext { get; }
        public string ErrorMessage { get; }


        internal InterpreterError(string msg, DefinitionContext line)
        {
            ErrorMessage = msg;
            ErrorContext = line;
        }

        public void @throw() => throw (InvalidProgramException)this;

        public override string ToString() => $"{ErrorContext}: {ErrorMessage}";


        public static implicit operator InvalidProgramException(InterpreterError err) => new InvalidProgramException(err.ToString())
        {
            Source = err.ErrorContext.FilePath.FullName
        };
    }

    public struct DefinitionContext
    {
        public FileInfo FilePath { get; }
        public int StartLine { get; }
        public int? EndLine { get; }


        public DefinitionContext(FileInfo path, int line)
            : this(path, line, null)
        {
        }

        public DefinitionContext(FileInfo path, int start, int? end)
        {
            ++start;

            FilePath = path;
            StartLine = start;
            EndLine = end is int i && i > start ? (int?)(i + 1) : null;
        }

        public override string ToString() => $"[{FilePath.Name}] l. {StartLine}{(EndLine is int i ? $"-{i}" : "")}";
    }

    public enum ExecutionLevel
    {
        None,
        AsInvoker,
        HighestAvailable,
        RequireAdministrator
    }

    public enum Compatibility
    {
        vista,
        win7,
        win8,
        win81,
        win10
    }
}
