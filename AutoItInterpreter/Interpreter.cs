using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

using AutoItInterpreter.Preprocessed;
using AutoItInterpreter.PartialAST;
using AutoItExpressionParser;

/* ====================== GLOBAL VARIABLE TRANSFORMATION =======================

 AutoIt .--
        |
        | Global $a
        | Global Const $b = 1
        | Const $c = "42", $d = func()
        | Global $e[5] = [1, "2", False, 4.2, "0"]
        | $f = 88
        | Dim $g
        |
        '--
     C# .--
        |
        |   static class globals
        |   {
        |       static Variant __global_error = default;
        |
        |       static Variant _a;
        |       static Variant _b; // use 'readonly' ?
        |       static Variant _c;
        |       static Variant _d;
        |       static Variant[] _e;
        |       static Variant _f;
        |       static Variant _g;
        |
        |       static void init()
        |       {
        |           globals._a = default;
        |           globals._b = 1m;
        |           globals._c = "42";
        |
        |           __func(ref _d, ref __global_error);
        |
        |           globals._e = new Variant[5];
        |           globals._e[0] = 1m;
        |           globals._e[1] = "2";
        |           globals._e[2] = false;
        |           globals._e[3] = 4.2m;
        |           globals._e[4] = "0";
        |
        |           globals._f = 88m;
        |           globals._g = default;
        |       }
        |
        |       ...
        |   }
        |
        '--
*/
/* ============================ FUNCTION GENERATION ============================

 AutoIt .--
        |
        |   Func MyFunc($a, ByRef $b, Const $c, Const ByRef $d, $e = 315, $f = "42")
        |       $b = $a + $c 
        |       MsgBox($d)
        |       $b *= $e - $f
        |       Return $a
        |   EndFunc
        |
        |   $x = "99"
        |   $y = "abc"
        |   $z = 0.88
        |   $w = MyFunc(0, $x, 1, $y, $z)
        |
        '--
     C# .--
        |
        |   void __myfunc(Variant _a, ref Variant _b, Variant _c, ref Variant _d, Variant _e, Variant _f, ref Variant @return, ref Variant @error)
        |   {
        |       _b = _a + _c;
        |       Util.MsgBox(_d);
        |       _b *= _e - _f;
        |       @return = _a;
        |   }
        |
        |   Variant _x = "99";
        |   Variant _y = "abc";
        |   Variant _z = 0.88m;
        |   Variant _w = default;
        |
        |   __myfunc((Variant)0m, ref _x, (Variant)1m, ref _y, _z, (Variant)"42", ref _w, ref __global_error);
        |   // do some error checking 
        |
        '--
 */

namespace AutoItInterpreter
{
    using static ExpressionAST;
    using static ControlBlock;

    public delegate void ErrorReporter(string name, params object[] args);

    public sealed class Interpreter
    {
        private static Dictionary<ControlBlock, string> ClosingInstruction { get; } = new Dictionary<ControlBlock, string>
        {
            [__NONE__] = "EndFunc",
            [If] = "EndIf",
            [ElseIf] = "EndIf",
            [Else] = "EndIf",
            [Select] = "EndSelect",
            [Switch] = "EndSwitch",
            [For] = "Next",
            [While] = "WEnd",
            [Do] = "Until ...",
            [With] = "EndWith",
        };
        private InterpreterSettings Settings { get; }
        public InterpreterContext RootContext { get; }
        internal Language Language { get; }


        public Interpreter(string path, Language lang, InterpreterSettings settings)
        {
            RootContext = new InterpreterContext(path);
            Language = lang;
            Settings = settings;
            Settings.IncludeDirectories = Settings.IncludeDirectories.Select(x => x.Trim().Replace('\\', '/')).Distinct().ToArray();

            if (RootContext.Content is null)
                throw new FileNotFoundException(lang["errors.general.file_nopen"], path);
        }

        public void DoMagic()
        {
            InterpreterState state = InterpretScript(RootContext, Settings, Language);

            ParseExpressionAST(state);







            ///////////////////////////////////////////// DEBUGGING /////////////////////////////////////////////

            Console.WriteLine(new string('=', 200));

            foreach (var fn in state.Functions.Keys)
            {
                var func = state.Functions[fn];

                Console.WriteLine($"---------------------------------------- {state.GetFunctionSignature(fn)} ----------------------------------------");

                foreach (var l in func.RawLines)
                {
                    Console.CursorLeft = 10;
                    Console.Write(l.DefinitionContext);
                    Console.CursorLeft = 40;
                    Console.WriteLine(l);
                }
            }

            Console.WriteLine(new string('=', 200));

            foreach (var e in state.Errors)
                Console.WriteLine(e);

            Console.WriteLine(new string('=', 200));
        }

        private static InterpreterState InterpretScript(InterpreterContext context, InterpreterSettings settings, Language lang)
        {
            List<(string Line, int[] OriginalLineNumbers, FileInfo File)> lines = new List<(string, int[], FileInfo)>();
            PreInterpreterState pstate = new PreInterpreterState
            {
                Language = lang,
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
                void err(string name, params object[] args) => pstate.ReportError(lang[name, args], defcntx);

                if (Line.StartsWith('#'))
                {
                    string path = ProcessDirective(Line.Substring(1), pstate, settings, err);

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
                        err("errors.preproc.include_nfound", path);
                    }
                }
                else if (!ProcessFunctionDeclaration(Line, defcntx, pstate, err))
                    (pstate.CurrentFunction is FunctionScope f ? f : pstate.GlobalFunction).Lines.Add((Line, defcntx));

                ++locindx;
            }

            Dictionary<string, FUNCTION> ppfuncdir = PreProcessFunctions(pstate);
            InterpreterState state = InterpreterState.Convert(pstate);

            foreach (string func in ppfuncdir.Keys)
                state.Functions[func] = ppfuncdir[func];

            return state;
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

            lcnt = 0;

            while (lcnt < lines.Count)
            {
                int[] lnr = lines[lcnt].Item2;

                if (lines[lcnt].Item1.Match(@"^if\s+(?<cond>.+)\s+then\s+(?<iaction>.+)\s+else\s+(?<eaction>)$", out Match m))
                {
                    lines.RemoveAt(lcnt);
                    lines.AddRange(new(string, int[])[]
                    {
                        ($"If ({m.Get("cond")}) Then", lnr),
                        (m.Get("iaction"), lnr),
                        ("Else", lnr),
                        (m.Get("eaction"), lnr),
                        ("EndIf", lnr)
                    });
                }
                else if (lines[lcnt].Item1.Match(@"^if\s+(?<cond>.+)\s+then\s+(?<then>.+)$", out m))
                {
                    lines.RemoveAt(lcnt);
                    lines.AddRange(new(string, int[])[]
                    {
                        ($"If ({m.Get("cond")}) Then", lnr),
                        (m.Get("then"), lnr),
                        ("EndIf", lnr)
                    });
                }

                ++lcnt;
            }

            return (from ln in lines
                    where ln.Item1.Length > 0
                    select (ln.Item1, ln.Item2, context.SourcePath)).ToArray();
        }

        private static Dictionary<string, FUNCTION> PreProcessFunctions(PreInterpreterState state)
        {
            Dictionary<string, FUNCTION> funcdir = new Dictionary<string, FUNCTION>
            {
                [PreInterpreterState.GLOBAL_FUNC_NAME] = PreProcessFunction(state.GlobalFunction, PreInterpreterState.GLOBAL_FUNC_NAME, true)
            };

            foreach (string name in state.Functions.Keys)
                if (name != PreInterpreterState.GLOBAL_FUNC_NAME)
                    funcdir[name] = PreProcessFunction(state.Functions[name], name, false);

            return funcdir;

            FUNCTION PreProcessFunction(FunctionScope func, string name, bool global)
            {
                Stack<ControlBlock> blocks = new Stack<ControlBlock>(new[] { __NONE__ });
                var lines = func.Lines.ToArray();
                int locndx = 0;

                Entity curr = new FUNCTION(name, global, func) { DefinitionContext = func.Context };

                while (locndx < lines.Length)
                {
                    DefinitionContext defctx = new DefinitionContext(func.Context.FilePath, lines[locndx].Context.StartLine, lines[locndx].Context.EndLine);
                    //void Requires(Action f, params ControlBlock[] cbs)
                    //{
                    //    if (cbs.Contains(blocks.Peek()))
                    //        f();
                    //    else
                    //        err("errors.preproc.block_reqr", true, string.Join("', '", cbs));
                    //}
                    void Conflicts(Action f, params ControlBlock[] cbs)
                    {
                        if (cbs.Contains(blocks.Peek()))
                            err("errors.preproc.block_confl", true, string.Join("', '", cbs));
                        else
                            f();
                    }
                    void err(string msg, bool fatal, params object[] args)
                    {
                        msg = (global ? $"[{name}]  " : "") + state.Language[msg, args];

                        if (fatal)
                            state.ReportError(msg, defctx);
                        else
                            state.ReportWarning(msg, defctx);
                    }
                    bool trycloseblock(ControlBlock ivb)
                    {
                        ControlBlock top = blocks.Pop();

                        if (ClosingInstruction[top] == ClosingInstruction[ivb])
                            return true;
                        else
                            blocks.Push(top);

                        if (top == __NONE__)
                            err("errors.preproc.block_invalid_close", true, ivb);
                        else
                            err("errors.preproc.block_conflicting_close", true, top, ClosingInstruction[ivb]);

                        return false;
                    }
                    T AppendSet<T>(ControlBlock cb, T e) where T : Entity
                    {
                        e.Parent = curr;
                        e.DefinitionContext = defctx;

                        curr.Append(e);
                        curr = e;

                        blocks.Push(cb);

                        return e;
                    }

                    string line = lines[locndx].Line;

                    line.Match(new(string, ControlBlock[], Action<Match>)[]
                    {
                        (@"^(?<optelse>else)?if\s+(?<cond>.+)\s+then$", new[] { Switch, Select }, m =>
                        {
                            string cond = m.Get("cond").Trim();

                            if (m.Get("optelse").Length > 0)
                            {
                                ControlBlock cb = blocks.Peek();

                                if (cb == If || cb == ElseIf)
                                {
                                    blocks.Pop();
                                    blocks.Push(ElseIf);

                                    IF par = curr.Parent as IF;
                                    ELSEIF_BLOCK b = new ELSEIF_BLOCK(par, cond) { DefinitionContext = defctx };

                                    par.AddElseIf(b);
                                    curr = b;
                                }
                                else
                                    err("errors.preproc.misplaced_elseif", true);
                            }
                            else
                            {
                                blocks.Push(If);

                                IF b = new IF(curr) { DefinitionContext = defctx };
                                b.SetIf(new IF_BLOCK(b, cond) { DefinitionContext = defctx });

                                curr.Append(b);
                                curr = b.If;
                            }
                        }),
                        ("^else$", new[] { Switch, Select }, _ =>
                        {
                            ControlBlock cb = blocks.Peek();

                            if (cb == If || cb == ElseIf)
                            {
                                blocks.Pop();
                                blocks.Push(Else);

                                IF par = curr.Parent as IF;
                                ELSE_BLOCK b = new ELSE_BLOCK(par) { DefinitionContext = defctx };

                                par.SetElse(b);
                                curr = b;
                            }
                            else
                                err("errors.preproc.misplaced_else", true);
                        }),
                        ("^endif$", new[] { Switch, Select }, _ =>
                        {
                            if (trycloseblock(If))
                                curr = curr.Parent;
                        }),
                        ("^select$", new[] { Switch, Select }, _ => AppendSet(Select, new SELECT(null))),
                        ("^endselect$", new[] { Switch }, _ =>
                        {
                            if (blocks.Peek() == Case)
                                blocks.Pop();

                            if (trycloseblock(Select))
                                curr = curr.Parent;
                        }),
                        (@"^switch\s+(?<cond>.+)$", new[] { Switch, Select }, m => AppendSet(Switch, new SWITCH(curr, m.Get("cond")))),
                        ("^endswitch$", new[] { Select }, _ =>
                        {
                            if (blocks.Peek() == Case)
                                blocks.Pop();

                            if (trycloseblock(Switch))
                                curr = curr.Parent;
                        }),
                        (@"^case\s+(?<cond>.+)$", new ControlBlock[0], m =>
                        {
                            ControlBlock cb = blocks.Peek();
                            string cond = m.Get("cond");

                            if (cb == Case)
                            {
                                blocks.Pop();
                                cb = blocks.Peek();
                            }

                            if (cb == Switch)
                                AppendSet(Case, new SWITCH_CASE(null, cond));
                            else if (cb == Select)
                                AppendSet(Case, new SELECT_CASE(null, cond));
                            else
                            {
                                err("errors.preproc.misplaced_case", true);

                                return;
                            }

                            blocks.Push(Case);
                        }),
                        ("^continuecase$", new[] { Switch, Select }, _ =>
                        {
                            if (!blocks.Contains(Case))
                                err("errors.preproc.misplaced_continuecase", true);
                            else
                                curr.Append(new CONTINUECASE(curr) { DefinitionContext = defctx });
                        }),
                        (@"^for\s+(?<var>\$[a-z_]\w*)\s*\=\s*(?<start>.+)\s+to\s+(?<stop>.+)(\s+step\s+(?<step>.+))?$", new[] { Switch, Select }, m => AppendSet(For, new FOR(curr, m.Get("var"), m.Get("start"), m.Get("stop"), m.Get("step")))),
                        (@"^for\s+(?<var>\$[a-z_]\w*)\s+in\s+(?<range>.+)$", new[] { Switch, Select }, m => AppendSet(For, new FOREACH(curr, m.Get("var"), m.Get("range")))),
                        ("^next$", new[] { Switch, Select }, _ =>
                        {
                            if (trycloseblock(For))
                                curr = curr.Parent;
                        }),
                        (@"^while\s+(?<cond>.+)$", new[] { Switch, Select }, m => AppendSet(While, new WHILE(curr, m.Get("cond")))),
                        (@"^exitloop(\s+(?<levels>\-?[0-9]+))?$", new[] { Switch, Select }, m =>
                        {
                            int cnt = blocks.Count(x => x == For || x == Do || x == While);

                            if (cnt == 0)
                                err("errors.preproc.misplaced_exitloop", true);
                            else if (int.TryParse(m.Get("levels"), out int levels) && levels > 0)
                            {
                                if (levels > cnt)
                                {
                                    err("errors.preproc.exit_level_truncated", false, levels, cnt);

                                    levels = cnt;
                                }

                                curr.Append(new BREAK(curr, levels) { DefinitionContext = defctx });
                            }
                            else
                                err("errors.preproc.exit_level_invalid", false, m.Get("levels"));
                        }),
                        (@"^continueloop(\s+(?<levels>\-?[0-9]+))?$", new[] { Switch, Select }, m =>
                        {
                            int cnt = blocks.Count(x => x == For || x == Do || x == While);

                            if (cnt == 0)
                                err("errors.preproc.misplaced_continueloop", true);
                            else if (int.TryParse(m.Get("levels"), out int levels) && levels > 0)
                            {
                                if (levels > cnt)
                                {
                                    err("errors.preproc.continue_level_truncated", false, levels, cnt);

                                    levels = cnt;
                                }

                                curr.Append(new CONTINUE(curr, levels) { DefinitionContext = defctx });
                            }
                            else
                                err("errors.preproc.continue_level_invalid", false, m.Get("levels"));
                        }),
                        ("^wend$", new[] { Switch, Select }, _ =>
                        {
                            if (trycloseblock(While))
                                curr = curr.Parent;
                        }),
                        ("^do$", new[] { Switch, Select }, _ => AppendSet(Do, new DO_UNTIL(null))),
                        (@"^until\s+(?<cond>.+)$", new[] { Switch, Select }, m =>
                        {
                            if (trycloseblock(Do))
                            {
                                (curr as DO_UNTIL).SetCondition(m.Get("cond"));
                                curr = curr.Parent;
                            }
                        }),
                        (@"^with\s+(?<expr>.+)$", new[] { Switch, Select }, m => AppendSet(With, new WITH(curr, m.Get("expr")))),
                        ("^endwith$", new[] { Switch, Select }, _ =>
                        {
                            if (trycloseblock(Do))
                                curr = curr.Parent;
                        }),
                        (@"(?<modifier>(static|const)\s+(local|global|dim)?|(global|local|dim)\s+(const|static)?)\s+(?<expr>.+)", new[] { Switch, Select }, m =>
                        {
                            string[] modf = m.Get("modifier").ToLower().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            string expr = m.Get("expr");

                            if (modf.Contains("local") && global)
                                err("errors.preproc.invalid_local", true);
                            else if (modf.Contains("global") && !global)
                                err("errors.preproc.invalid_global", true);

                            curr.Append(new DECLARATION(curr, expr, modf) { DefinitionContext = defctx });
                        }),
                        (@"^return\s+(?<val>.+)$", new[] { Switch, Select }, m => curr.Append(new RETURN(curr, m.Get("val")) { DefinitionContext = defctx })),
                        (".*", new[] { Switch, Select }, _ => curr.Append(new RAWLINE(curr, line) { DefinitionContext = defctx })),
                    }.Select<(string, ControlBlock[], Action<Match>), (string, Action<Match>)>(x => (x.Item1, m => Conflicts(() => x.Item3(m), x.Item2))).ToArray());

                    ++locndx;
                }

                List<string> ci = new List<string>();

                while (blocks.Pop() is ControlBlock cb && cb != __NONE__)
                    if (cb != Case)
                        ci.Add(ClosingInstruction[cb]);

                if (ci.Count > 0)
                    state.ReportError((global ? $"[{ name}]  " : "") + state.Language["errors.preproc.blocks_unclosed", string.Join("', '", ci)], new DefinitionContext(func.Context.FilePath, locndx));

                while (!(curr is FUNCTION))
                    curr = curr.Parent;

                return curr as FUNCTION;
            }
        }

        private static bool ProcessFunctionDeclaration(string Line, DefinitionContext defcntx, PreInterpreterState st, ErrorReporter err)
        {
            void __procfunc(string name, string[] par, string[] opar)
            {
                if (st.CurrentFunction is null)
                {
                    string lname = name.ToLower();

                    if (st.Functions.ContainsKey(lname))
                        err("errors.preproc.function_exists", name, st.Functions[lname].Context);
                    else
                    {
                        st.CurrentFunction = new FunctionScope(defcntx);
                        st.CurrentFunction.Parameters.AddRange(from p in par
                                                               let ndx = p.IndexOf('$')
                                                               let attr = p.Remove(ndx).Trim().ToLower()
                                                               select (
                                                                    p.Substring(ndx + 1).Trim().ToLower(),
                                                                    attr.Contains("byref"),
                                                                    attr.Contains("const"),
                                                                    null as string
                                                               ));
                        st.CurrentFunction.Parameters.AddRange(from p in opar
                                                               let ndx = p.IndexOf('=')
                                                               let nm = p.Remove(ndx).Trim().ToLower().TrimStart('$')
                                                               select (
                                                                    nm,
                                                                    false,
                                                                    false,
                                                                    p.Substring(ndx + 1).Trim()
                                                               ));

                        st.Functions[lname] = st.CurrentFunction;
                    }
                }
                else
                    err("errors.preproc.function_nesting");
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
                    err("errors.preproc.unexpected_endfunc");
                else
                    st.CurrentFunction = null;
            else
                return false;

            return true;
        }

        private static void ProcessPrgamaCompileOption(string name, string value, CompileInfo ci, ErrorReporter err)
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
            () => err("errors.preproc.directive_invalid", name));
        }

        private static string ProcessDirective(string line, PreInterpreterState st, InterpreterSettings settings, ErrorReporter err)
        {
            string inclpath = "";

            line.Match(
                ("^notrayicon$", _ => st.UseTrayIcon = false),
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

                    err("errors.preproc.include_nfound", path);

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
                                err("errors.preproc.pragma_unsupported", opt);

                                break;
                        }
                    }
                    catch
                    {
                        err("errors.preproc.directive_invalid_value", value, name);
                    }
                })
            );

            return inclpath;
        }

        private static void ParseExpressionAST(InterpreterState state)
        {
            const string globnm = PreInterpreterState.GLOBAL_FUNC_NAME;

            foreach ((string name, FUNCTION func) in new[] { (globnm, state.Functions[globnm]) }.Concat(from kvp in state.Functions
                                                                                                        where kvp.Key != globnm
                                                                                                        select (kvp.Key, kvp.Value)))
            {
                var result = process(func);


            }

            dynamic process(Entity e)
            {
                void err(string path, params object[] args) => state.ReportError(state.Language[path, args], e.DefinitionContext);
                AST_STATEMENT[] proclines() => e.RawLines.Select(x => process(x) as AST_STATEMENT).ToArray();
                AST_CONDITIONAL_BLOCK proccond() => new AST_CONDITIONAL_BLOCK { Condition = parseexpr((e as ConditionalEntity).RawCondition), Statements = proclines() };
                EXPRESSION parseexpr(string expr)
                {
                    try
                    {
                        return ExpressionParser.Parse(expr);
                    }
                    catch (Exception ex)
                    {
                        err("errors.astproc.parser_error", expr, ex.Message);

                        return null;
                    }
                }

                switch (e)
                {
                    case FUNCTION i:
                        return new AST_FUNCTION
                        {
                            Statements = proclines(),
                            // TODO : parse params etc.
                        };
                    case IF i:
                        return new AST_IF_STATEMENT
                        {
                            If = process(i.If),
                            ElseIf = i.ElseIfs.Select(x => process(x) as AST_CONDITIONAL_BLOCK).ToArray(),
                            OptionalElse = i.Else is ELSE_BLOCK eb ? process(eb) : null
                        };
                    case IF_BLOCK _:
                    case ELSEIF_BLOCK _:
                        return proccond();
                    case ELSE_BLOCK i:
                        return proclines();
                    case WHILE i:
                        return (AST_WHILE_STATEMENT)proccond();
                    case DO_UNTIL i:
                        return (AST_DO_STATEMENT)proccond();
                    case SELECT i: break;
                    case SWITCH i: break;
                    case RAWLINE i: break;
                    case RETURN i: break;
                    case CONTINUECASE i: break;
                    case CONTINUE i: break;
                    case BREAK i: break;
                    case WITH i: break;
                    case FOR i: break;
                    case FOREACH i: break;
                    case DECLARATION i: break;
                    default:
                        err("errors.astproc.unknown_entity", e?.GetType()?.FullName ?? "<null>");

                        return null;
                }

                return null;
            }
        }
    }

    public abstract class AbstractParserState
    {
        private protected List<InterpreterError> _errors;

        public InterpreterError[] Errors => _errors.ToArray();
        public CompileInfo CompileInfo { private protected set; get; }
        public Language Language { get; set; }
        public bool IsIncludeOnce { set; get; }
        public bool RequireAdmin { set; get; }
        public bool UseTrayIcon { set; get; }


        public AbstractParserState()
        {
            _errors = new List<InterpreterError>();
            CompileInfo = new CompileInfo();
            UseTrayIcon = true;
        }

        public void ReportError(string msg, DefinitionContext ctx) => _errors.Add(new InterpreterError(msg, ctx));

        public void ReportWarning(string msg, DefinitionContext ctx) => _errors.Add(new InterpreterError(msg, ctx, false));
    }

    public sealed class InterpreterState
        : AbstractParserState
    {
        public Dictionary<string, FUNCTION> Functions { get; }
        public List<string> StartFunctions { get; }


        public InterpreterState()
        {
            Functions = new Dictionary<string, FUNCTION>();
            StartFunctions = new List<string>();
        }

        public static InterpreterState Convert(PreInterpreterState ps)
        {
            InterpreterState s = new InterpreterState
            {
                IsIncludeOnce = ps.IsIncludeOnce,
                RequireAdmin = ps.RequireAdmin,
                UseTrayIcon = ps.UseTrayIcon,
                CompileInfo = ps.CompileInfo,
                Language = ps.Language,
            };
            s.StartFunctions.AddRange(ps.StartFunctions);
            s._errors.AddRange(ps.Errors);

            return s;
        }

        public string GetFunctionSignature(string funcname) => $"func {funcname}({string.Join(", ", Functions[funcname].Parameters.Select(p => $"{(p.Const ? "const " : "")}{(p.ByRef ? "ref " : "")}${p.Name}{(p.RawInitExpression is string s ? $" = {s}" : "")}"))})";
    }

    public sealed class PreInterpreterState
        : AbstractParserState
    {
        internal const string GLOBAL_FUNC_NAME = "__global<>";

        public InterpreterContext CurrentContext { set; get; }
        public Dictionary<string, FunctionScope> Functions { get; }
        public FunctionScope CurrentFunction { set; get; }
        public List<string> IncludeOncePaths { get; }
        public List<string> StartFunctions { get; }

        public FunctionScope GlobalFunction
        {
            set => Functions[GLOBAL_FUNC_NAME] = value;
            get => Functions[GLOBAL_FUNC_NAME];
        }


        public PreInterpreterState()
        {
            Functions = new Dictionary<string, FunctionScope> { [GLOBAL_FUNC_NAME] = null };
            IncludeOncePaths = new List<string>();
            StartFunctions = new List<string>();
            UseTrayIcon = true;
        }

        public string GetFunctionSignature(string funcname) => $"func {funcname}({string.Join(", ", Functions[funcname].Parameters.Select(p => $"{(p.Constant ? "const " : "")}{(p.ByRef ? "ref " : "")}${p.Name}{(p.InitExpression is string s ? $" = {s}" : "")}"))})";
    }

    public sealed class FunctionScope
    {
        public List<(string Name, bool ByRef, bool Constant, string InitExpression)> Parameters { get; }
        public List<(string Line, DefinitionContext Context)> Lines { get; }
        public DefinitionContext Context { get; }


        public FunctionScope(DefinitionContext ctx)
        {
            Parameters = new List<(string, bool, bool, string)>();
            Lines = new List<(string, DefinitionContext)>();
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
        public bool IsFatal { get; }


        /// <summary>A new fatal error</summary>
        internal InterpreterError(string msg, DefinitionContext line)
            : this(msg, line, true)
        {
        }

        internal InterpreterError(string msg, DefinitionContext line, bool fatal)
        {
            IsFatal = fatal;
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

    public enum ControlBlock
    {
        __NONE__,
        If,
        ElseIf,
        Else,
        Select,
        Switch,
        Case,
        For,
        While,
        Do,
        With,
    }
}
