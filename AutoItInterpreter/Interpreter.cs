// #define PRE_BUILD
// #define USE_PUBLISHER

using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System;

using Microsoft.FSharp.Collections;

using Piglet.Parser.Configuration;

using AutoItInterpreter.Preprocessed;
using AutoItInterpreter.PartialAST;
using AutoItExpressionParser;
using AutoItCoreLibrary;

namespace AutoItInterpreter
{
    using static InterpreterConstants;
    using static ExpressionAST;
    using static PInvoke;
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
        public InterpreterContext RootContext { get; }
        private InterpreterOptions Options { get; }
        public string ProjectName { get; }


        public Interpreter(string path, InterpreterOptions options)
        {
            RootContext = new InterpreterContext(path);
            Options = options;
            Options. Settings.IncludeDirectories = Options.Settings.IncludeDirectories.Select(x => x.Trim().Replace('\\', '/')).Distinct().ToArray();

            if (RootContext.Content is null)
                RootContext = new InterpreterContext(path + ".au3");

            if (RootContext.Content is null)
                throw new FileNotFoundException(Options.Language["errors.general.file_nopen"], path);
            else
                path += ".au3";

            string projectname = RootContext.SourcePath.Name;
            string ext = RootContext.SourcePath.Extension;

            if (projectname.EndsWith(ext) && (projectname.Length > ext.Length))
                projectname = projectname.Remove(projectname.Length - ext.Length);

            ProjectName = projectname.Replace(' ', '_');
        }

        public InterpreterState Interpret()
        {
            DirectoryInfo subdir = RootContext.SourcePath.Directory.CreateSubdirectory(".autoit++-compiler");
            DebugPrintUtil.FinalResult fr;
            InterpreterState state;
            bool success = false;

            try
            {
                subdir.Attributes |= FileAttributes.Hidden | FileAttributes.System;

                if (Options.UseVerboseOutput)
                    Console.WriteLine($"Pre-compiling internal methods for {Win32.System} ({Environment.OSVersion}, {RuntimeInformation.OSArchitecture})");

                foreach (Type t in new[] { typeof(ISymbol<>), typeof(AutoItFunctions), typeof(ExpressionParser), typeof(Interpreter) })
                    t.Assembly.PreJIT();

                if (Options.UseVerboseOutput)
                    Console.WriteLine("Finished pre-compiling.");

                state = LinePreprocessor.PreprocessLines(RootContext, Options);
                state.RootDocument = RootContext.SourcePath;

                ASTProcessor.ParseExpressionAST(state, Options);

                string cs_code = ApplicationGenerator.GenerateCSharpCode(state, Options);
                int ret = ApplicationGenerator.GenerateDotnetProject(ref subdir, ProjectName);
                void cmperr(string msg, params object[] args) => state.ReportKnownError(msg, new DefinitionContext(null, 0), args);

                if (ret != 0)
                {
                    cmperr("errors.generator.cannot_create_dotnet", ret);

                    fr = DebugPrintUtil.FinalResult.Errors_Failed;
                }
                else
                {
                    TargetSystem target = new TargetSystem(state.CompileInfo.Compatibility, state.CompileInfo.TargetArchitecture);

                    if (target.Compatibility == Compatibility.android)
                        target = new TargetSystem(Compatibility.android, null);

                    ApplicationGenerator.EditDotnetProject(state, target, subdir, ProjectName);

                    if (target.Compatibility == Compatibility.winxp || target.Compatibility == Compatibility.vista)
                        cmperr("errors.generator.target_deprecated", target.Compatibility);

                    File.WriteAllText($"{subdir.FullName}/{ProjectName}.cs", cs_code);
                    File.WriteAllText($"{subdir.FullName}/{ProjectName}.log", string.Join("\n", state.Errors.Select(err => err.ToString())));

                    if (Options.UseVerboseOutput && (!state.Fatal || Options.GenerateCodeEvenWithErrors))
                        DebugPrintUtil.DisplayGeneratedCode(cs_code);

                    DebugPrintUtil.PrintSeperator("ROSLYN COMPILER OUTPUT");
#if PRE_BUILD || !USE_PUBLISHER
                    ret = ApplicationGenerator.BuildDotnetProject(subdir);
#endif
#if USE_PUBLISHER
                    if (ret == 0)
                        ret = ApplicationGenerator.PublishDotnetProject(subdir);
#endif
                    if (ret != 0)
                    {
                        cmperr("errors.generator.build_failed", ret);

                        fr = DebugPrintUtil.FinalResult.Errors_Failed;
                    }
                    else
                    {
#if USE_PUBLISHER
                        DirectoryInfo bindir = subdir.CreateSubdirectory($"bin/{target.Identifier}/publish");
#else
                        DirectoryInfo bindir = subdir.CreateSubdirectory($"bin/{target.Identifier}");
#endif
                        DirectoryInfo targetdir = Options.TargetDirectory is string s ? new DirectoryInfo(s) : RootContext.SourcePath.Directory.CreateSubdirectory(ProjectName + "-compiled");

                        foreach (FileInfo file in bindir.GetFiles("*.pdb"))
                            file.Delete();

                        if (!targetdir.Exists)
                            targetdir.Create();

                        FileInfo[] ovf = targetdir.EnumerateFiles().ToArray();

                        if ((ovf.Length > 0) && (!Options.CleanTargetFolder))
                            state.ReportKnownWarning("warnings.generator.failed_clean_output", new DefinitionContext(null, 0), targetdir.FullName);
                        else
                            foreach (FileInfo file in ovf)
                                file.Delete();

                        foreach (FileInfo file in bindir.EnumerateFiles())
                            file.CopyTo($"{targetdir.FullName}/{file.Name}", true);

                        fr = state.Errors.Any(err => err.Type == ErrorType.Fatal) ? DebugPrintUtil.FinalResult.Errors_Compiled :
                             state.Errors.Any(err => err.Type == ErrorType.Warning) ? DebugPrintUtil.FinalResult.OK_Warnings :
                             state.Errors.Length > 0 ? DebugPrintUtil.FinalResult.OK_Notes : DebugPrintUtil.FinalResult.OK;
                    }
                }

                if (Options.TreatWarningsAsErrors)
                    state.ElevateWarningsToErrors();

                if (Options.UseVerboseOutput)
                {
                    if (state.Errors.Length > 0)
                        DebugPrintUtil.DisplayCodeAndErrors(state);

                    DebugPrintUtil.DisplayFinalResult(fr);
                }

                if (state.Errors.Length > 0)
                {
                    DebugPrintUtil.DisplayErrors(state, Options);
                    DebugPrintUtil.PrintSeperator(null);
                }

                success = true;
            }
            finally
            {
                if (!success || Options.DeleteTempFilesAfterSuccess)
                {
                    subdir.Delete(true);
                    subdir = subdir.Parent;

                    if (subdir.GetDirectories().Length == 0)
                        try
                        {
                            subdir.Delete(true);
                        }
                        catch
                        {
                        }
                }
            }

            return state;
        }


        private static class LinePreprocessor
        {
            private static int _λcount;


            internal static InterpreterState PreprocessLines(InterpreterContext context, InterpreterOptions options)
            {
                Stack<(FunctionDeclarationState, FunctionScope)> stack = new Stack<(FunctionDeclarationState, FunctionScope)>();
                PreInterpreterState pstate = new PreInterpreterState
                {
                    Language = options.Language,
                    CurrentContext = context,
                    GlobalFunction = new FunctionScope(new DefinitionContext(context.SourcePath, -1), "")
                };
                List<RawLine> lines = new List<RawLine>();
                int locindx = 0;

                pstate.CompileInfo.Compatibility = options.Compatibility;
                pstate.CompileInfo.TargetArchitecture = options.TargetArchitecture;

                stack.Push((FunctionDeclarationState.RegularFunction, null));
                lines.AddRange(FetchLines(pstate, context, options));

                while (locindx < lines.Count)
                {
                    string Line = lines[locindx].Content;
                    DefinitionContext defcntx = new DefinitionContext(
                        lines[locindx].File,
                        lines[locindx].OriginalLineNumbers[0],
                        lines[locindx].OriginalLineNumbers.Length > 1 ? (int?)lines[locindx].OriginalLineNumbers.Last() : null
                    );
                    void err(string name, params object[] args) => pstate.ReportKnownError(name, defcntx, args);

                    if (Line.StartsWith('#'))
                    {
                        string path = ProcessDirective(Line.Substring(1), pstate, options.Settings, err);

                        try
                        {
                            FileInfo inclpath = path.Length > 0 ? new FileInfo(path) : default;

                            if (inclpath?.Exists ?? false)
                                using (StreamReader rd = inclpath.OpenText())
                                {
                                    lines.RemoveAt(locindx);
                                    lines.InsertRange(locindx, FetchLines(pstate, new InterpreterContext(inclpath), options));

                                    --locindx;
                                }
                        }
                        catch
                        {
                            err("errors.preproc.include_nfound", path);
                        }
                    }
                    else if (ProcessFunctionDeclaration(Line, stack, defcntx, pstate, err))
                        (stack.Peek().Item2 ?? pstate.GlobalFunction).Lines.Add((Line, defcntx));

                    ++locindx;
                }

                DefinitionContext eofctx = new DefinitionContext(lines.Last().File, lines.Last().OriginalLineNumbers[0], null);

                if (stack.Count > 1)
                    pstate.ReportKnownError("errors.preproc.missing_endfunc", eofctx, stack.Count);
                else if (stack.Count == 0)
                    pstate.ReportKnownError("errors.preproc.fatal_internal_funcparsing_error", eofctx);

                if (options.UseVerboseOutput)
                    DebugPrintUtil.DisplayPreState(pstate);

                Dictionary<string, FUNCTION> ppfuncdir = PreprocessFunctions(pstate, options);
                InterpreterState state = InterpreterState.Convert(pstate);

                foreach (string func in ppfuncdir.Keys)
                    state.Functions[func] = ppfuncdir[func];

                return state;
            }

            private static RawLine[] FetchLines(PreInterpreterState state, InterpreterContext context, InterpreterOptions options)
            {
                string raw = context.Content;
                List<(string c, int[] ln)> lines = new List<(string, int[])>();
                List<int> lnmbrs = new List<int>();
                LineState ls = LineState.Regular;
                (string code, int start) csharp = ("", 0);
                string prev = "";
                int lcnt = 0;

                foreach (string line in raw.SplitIntoLines())
                {
                    string tline = line.Trim();

                    if (tline.Match(@"^\#cs\[csharp\](\b|\s+|$)", out _))
                    {
                        if (!options.AllowUnsafeCode)
                            state.ReportKnownError("errors.preproc.csharp_requires_unsafe", new DefinitionContext(context.SourcePath, lcnt));

                        ls |= LineState.CSharp;
                        csharp.start = lcnt;
                    }
                    else if (tline.Match(@"^\#ce\[csharp\](\b|\s+|$)", out _))
                    {
                        ls &= ~LineState.CSharp;

                        string b64 = Convert.ToBase64String(Encoding.Default.GetBytes(csharp.code));

                        lines.Add(($"{CSHARP_INLINE} {b64}", new int[] { csharp.start, lcnt }));

                        csharp = ("", 0);
                    }
                    else if (tline.Match(@"^\#(comments\-start|cs)(\b|\s+|$)", out _))
                        ls |= LineState.Comment;
                    else if (tline.Match(@"^\#(comments\-end|ce)(\b|\s+|$)", out _))
                        ls &= ~LineState.Comment;
                    else if (ls == LineState.Regular)
                    {
                        if (tline.Match(@"\;[^\""]*$", out Match m))
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
                    else if (ls == LineState.CSharp)
                        csharp.code += '\n' + line;

                    ++lcnt;
                }

                if (ls.HasFlag(LineState.Comment))
                    state.ReportKnownWarning("warnings.preproc.no_closing_comment", new DefinitionContext(context.SourcePath, lcnt - 1));
                else if (ls.HasFlag(LineState.CSharp))
                    state.ReportKnownWarning("errors.preproc.no_closing_csharp", new DefinitionContext(context.SourcePath, lcnt - 1));

                lcnt = 0;

                while (lcnt < lines.Count)
                {
                    int[] lnr = lines[lcnt].ln;

                    if (lines[lcnt].c.Match(@"^if\s+(?<cond>.+)\s+then\s+(?<iaction>.+)\s+else\s+(?<eaction>)$", out Match m))
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
                    else if (lines[lcnt].c.Match(@"^if\s+(?<cond>.+)\s+then\s+(?<then>.+)$", out m))
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
                        where ln.c.Length > 0
                        select new RawLine(ln.c, ln.ln, context.SourcePath)).ToArray();
            }

            private static Dictionary<string, FUNCTION> PreprocessFunctions(PreInterpreterState state, InterpreterOptions options)
            {
                Dictionary<string, FUNCTION> funcdir = new Dictionary<string, FUNCTION>
                {
                    [GLOBAL_FUNC_NAME] = PreprocessFunction(state.GlobalFunction, GLOBAL_FUNC_NAME, true)
                };

                foreach (string name in state.Functions.Keys)
                    if (name != GLOBAL_FUNC_NAME)
                        funcdir[name] = PreprocessFunction(state.Functions[name], name, false);

                return funcdir;

                FUNCTION PreprocessFunction(FunctionScope func, string name, bool global)
                {
                    Stack<(Entity Entity, ControlBlock CB)> eblocks = new Stack<(Entity, ControlBlock)>();
                    var lines = func.Lines.ToArray();
                    int locndx = 0;

                    Entity curr = new FUNCTION(name, global, func) { DefinitionContext = func.Context };

                    eblocks.Push((curr, __NONE__));

                    while (locndx < lines.Length)
                    {
                        DefinitionContext defctx = lines[locndx].Context;
                        void err(string msg, bool fatal, params object[] args)
                        {
                            int errnum = Language.GetErrorNumber(msg);

                            msg = state.Language[msg, args];

                            if (fatal)
                                state.ReportError(msg, defctx, errnum);
                            else
                                state.ReportWarning(msg, defctx, errnum);
                        }
                        void Conflicts(Action f, params ControlBlock[] cbs)
                        {
                            if (cbs.Contains(eblocks.Peek().CB))
                                err("errors.preproc.block_confl", true, string.Join("', '", cbs));
                            else
                                f();
                        }
                        void TryCloseBlock(ControlBlock ivb)
                        {
                            (Entity et, ControlBlock CB) = eblocks.Pop();

                            if (CB == __NONE__)
                            {
                                eblocks.Push((et, CB));

                                return;
                            }
                            else if ((ivb == IfElifElseBlock)
                                  || (CB == ivb)
                                  || ((CB == Case) && (ivb == Select || ivb == Switch)
                                  || (ClosingInstruction.ContainsKey(CB) && ClosingInstruction.ContainsKey(ivb) && ClosingInstruction[CB] == ClosingInstruction[ivb])))
                            {
                                curr = eblocks.Peek().Entity;

                                return;
                            }
                            else
                                eblocks.Push((et, CB));

                            if (CB == __NONE__)
                                err("errors.preproc.block_invalid_close", true, ivb);
                            else
                                err("errors.preproc.block_conflicting_close", true, CB, ClosingInstruction[ivb]);
                        }
                        void ForceCloseBlock()
                        {
                            eblocks.Pop();

                            curr = eblocks.Peek().Entity;
                        }
                        int AnyParentCount(params ControlBlock[] cb) => eblocks.Count(x => cb.Contains(x.CB));
                        void Append(params Entity[] es)
                        {
                            foreach (Entity e in es)
                            {
                                e.DefinitionContext = defctx;
                                e.Parent = curr;

                                curr.Append(e);
                            }
                        }
                        T OpenBlock<T>(ControlBlock cb, T e) where T : Entity
                        {
                            e.Parent = curr;
                            e.DefinitionContext = defctx;

                            curr.Append(e);
                            curr = e;

                            eblocks.Push((e, cb));

                            return e;
                        }

                        string line = lines[locndx].Line;

                        line.Match(new(string, ControlBlock[], Action<Match>)[]
                        {
                            ($@"^{CSHARP_INLINE}\s+(?<b64>[0-9a-z\+\/\=]+)$", new ControlBlock[0], m =>
                            {
                                if (options.AllowUnsafeCode)
                                    Append(new CS_INLINE(curr, Encoding.Default.GetString(Convert.FromBase64String(m.Get("b64")))));
                            }),
                            (@"^(?<optelse>else)?if\s+(?<cond>.+)\s+then$", new[] { Switch, Select }, m =>
                            {
                                string cond = m.Get("cond").Trim();

                                if (m.Get("optelse").Length > 0)
                                {
                                    ControlBlock cb = eblocks.Peek().CB;

                                    if (cb == If || cb == ElseIf)
                                    {
                                        ForceCloseBlock();

                                        IF par = (IF)curr;
                                        ELSEIF_BLOCK b = OpenBlock(ElseIf, new ELSEIF_BLOCK(par, cond));

                                        par.AddElseIf(b);
                                    }
                                    else
                                        err("errors.preproc.misplaced_elseif", true);
                                }
                                else
                                {
                                    IF b = OpenBlock(IfElifElseBlock, new IF(curr));
                                    IF_BLOCK ib = OpenBlock(If, new IF_BLOCK(b, cond));

                                    b.SetIf(ib);
                                }
                            }),
                            (@"^(else)?if\s+.+$", new[] { Switch, Select }, _ => err("errors.preproc.missing_then", true)),
                            ("^else$", new[] { Switch, Select }, _ =>
                            {
                                ControlBlock cb = eblocks.Peek().CB;

                                if (cb == If || cb == ElseIf)
                                {
                                    ForceCloseBlock();

                                    IF par = (IF)curr;
                                    ELSE_BLOCK eb = OpenBlock(Else, new ELSE_BLOCK(par));

                                    par.SetElse(eb);
                                }
                                else
                                    err("errors.preproc.misplaced_else", true);
                            }),
                            ("^endif$", new[] { Switch, Select }, _ =>
                            {
                                TryCloseBlock(If);
                                TryCloseBlock(IfElifElseBlock);
                            }),
                            ("^select$", new[] { Switch, Select }, _ => OpenBlock(Select, new SELECT(curr))),
                            ("^endselect$", new[] { Switch }, _ =>
                            {
                                if (eblocks.Peek().CB == Case)
                                    TryCloseBlock(Case);

                                SELECT sw = eblocks.Peek().Entity as SELECT;

                                sw.Cases.AddRange(sw.RawLines.Select(x => x as SELECT_CASE));

                                TryCloseBlock(Select);
                            }),
                            (@"^switch\s+(?<cond>.+)$", new[] { Switch, Select }, m => OpenBlock(Switch, new SWITCH(curr, m.Get("cond")))),
                            ("^endswitch$", new[] { Select }, _ =>
                            {
                                if (eblocks.Peek().CB == Case)
                                    TryCloseBlock(Case);

                                SWITCH sw = eblocks.Peek().Entity as SWITCH;

                                sw.Cases.AddRange(sw.RawLines.Select(x => x as SWITCH_CASE));

                                TryCloseBlock(Switch);
                            }),
                            (@"^case\s+(?<cond>.+)$", new ControlBlock[0], m =>
                            {
                                var b = eblocks.Peek();
                                string cond = m.Get("cond");

                                if (b.CB == Case)
                                {
                                    TryCloseBlock(Case);

                                    b = eblocks.Peek();
                                }

                                if (b.CB == Switch)
                                    OpenBlock(Case, new SWITCH_CASE(null, cond));
                                else if (b.CB == Select)
                                    OpenBlock(Case, new SELECT_CASE(null, cond));
                                else
                                {
                                    err("errors.preproc.misplaced_case", true);

                                    return;
                                }
                            }),
                            ("^continuecase$", new[] { Switch, Select }, _ =>
                            {
                                if (AnyParentCount(Switch, Select) > 0)
                                    Append(new CONTINUECASE(curr));
                                else
                                    err("errors.preproc.misplaced_continuecase", true);
                            }),
                            (@"^for\s+(?<var>\$[a-z_]\w*)\s*\=\s*(?<start>.+)\s+to\s+(?<stop>.+)(\s+step\s+(?<step>.+))$", new[] { Switch, Select }, m => OpenBlock(For, new FOR(curr, m.Get("var"), m.Get("start"), m.Get("stop"), m.Get("step")))),
                            (@"^for\s+(?<var>\$[a-z_]\w*)\s*\=\s*(?<start>.+)\s+to\s+(?<stop>.+)$", new[] { Switch, Select }, m => OpenBlock(For, new FOR(curr, m.Get("var"), m.Get("start"), m.Get("stop")))),
                            (@"^for\s+(?<var>\$[a-z_]\w*)\s+in\s+(?<range>.+)$", new[] { Switch, Select }, m => OpenBlock(For, new FOREACH(curr, m.Get("var"), m.Get("range")))),
                            ("^next$", new[] { Switch, Select }, _ => TryCloseBlock(For)),
                            (@"^while\s+(?<cond>.+)$", new[] { Switch, Select }, m => OpenBlock(While, new WHILE(curr, m.Get("cond")))),
                            (@"^exitloop(\s+(?<levels>\-?[0-9]+))?$", new[] { Switch, Select }, m =>
                            {
                                int cnt = AnyParentCount (For, Do, While);

                                if (cnt == 0)
                                    err("errors.preproc.misplaced_exitloop", true);
                                else if (int.TryParse(m.Get("levels"), out int levels) && levels > 0)
                                {
                                    if (levels > cnt)
                                    {
                                        err("warnings.preproc.exit_level_truncated", false, levels, cnt);

                                        levels = cnt;
                                    }

                                    Append(new BREAK(curr, levels));
                                }
                                else
                                    err("warnings.preproc.exit_level_invalid", false, m.Get("levels"));
                            }),
                            (@"^continueloop(\s+(?<levels>\-?[0-9]+))?$", new[] { Switch, Select }, m =>
                            {
                                int cnt = AnyParentCount (For, Do, While);

                                if (cnt == 0)
                                    err("errors.preproc.misplaced_continueloop", true);
                                else if (int.TryParse(m.Get("levels"), out int levels) && levels > 0)
                                {
                                    if (levels > cnt)
                                    {
                                        err("warnings.preproc.continue_level_truncated", false, levels, cnt);

                                        levels = cnt;
                                    }

                                    Append(new CONTINUE(curr, levels));
                                }
                                else
                                    err("warnings.preproc.continue_level_invalid", false, m.Get("levels"));
                            }),
                            ("^wend$", new[] { Switch, Select }, _ => TryCloseBlock(While)),
                            ("^do$", new[] { Switch, Select }, _ => OpenBlock(Do, new DO_UNTIL(null))),
                            (@"^until\s+(?<cond>.+)$", new[] { Switch, Select }, m =>
                            {
                                (curr as DO_UNTIL)?.SetCondition(m.Get("cond"));

                                TryCloseBlock(Do);
                            }),
                            (@"^with\s+(?<expr>.+)$", new[] { Switch, Select }, m => OpenBlock(With, new WITH(curr, m.Get("expr")))),
                            ("^endwith$", new[] { Switch, Select }, _ => TryCloseBlock(Do)),
                            (@"^(?<modifier>(static|const)\s+(local|global|dim)?|(global|local|dim)\s+(const|static)?)\s*(?<expr>.+)\s*$", new[] { Switch, Select }, m =>
                            {
                                string[] modf = m.Get("modifier").ToLower().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                string expr = m.Get("expr");

                                if (modf.Contains("local") && global)
                                    err("warnings.preproc.invalid_local", false);
                                else if (modf.Contains("global") && !global)
                                    err("errors.preproc.invalid_global", true);

                                Append(new DECLARATION(curr, expr, modf));
                            }),
                            (@"^return\s+(?<val>.+)$", new[] { Switch, Select }, m => Append(new RETURN(curr, m.Get("val")))),
                            (@"^redim\s+(?<var>\$[a-z_]\w*)(?<dim>(\s*\[.+\])+\s*)$",  new[] { Switch, Select }, m => Append(new REDIM(curr, m.Get("var"), m.Get("dim").Split('[').Skip(1).Select(d => d.TrimEnd(']').Trim()).ToArray()))),
                            (@"^(?<var>\$[a-z_]\w*.*)\s*=\s*(?<func>[a-zλ_]\w*)$",  new[] { Switch, Select }, m => Append(new λ_ASSIGNMENT(curr, m.Get("var"), m.Get("func")))),
                            (".*", new[] { Switch, Select }, _ => Append(new RAWLINE(curr, line))),
                        }.Select<(string, ControlBlock[], Action<Match>), (string, Action<Match>)>(x => (x.Item1, m => Conflicts(() => x.Item3(m), x.Item2))).ToArray());

                        ++locndx;
                    }

                    List<string> ci = new List<string>();
                    ControlBlock pb;

                    while ((pb = eblocks.Pop().CB) != __NONE__)
                        ci.Add(ClosingInstruction[pb == IfElifElseBlock ? If : pb]);

                    if (ci.Count > 0)
                    {
                        const string errpath = "errors.preproc.blocks_unclosed";

                        state.ReportError((global ? $"[{ name}]  " : "") + state.Language[errpath, string.Join("', '", ci)], new DefinitionContext(func.Context.FilePath, locndx), Language.GetErrorNumber(errpath));

                        while (curr.Parent is Entity e)
                            curr = e;
                    }

                    return (FUNCTION)curr;
                }
            }

            private static unsafe bool ProcessFunctionDeclaration(string Line, Stack<(FunctionDeclarationState fds, FunctionScope sc)> stack, DefinitionContext defctx, PreInterpreterState st, ErrorReporter err)
            {
                (FunctionDeclarationState fds, FunctionScope current) = stack.Peek();

                if (Line.Match(@"^func\s+(?<name>[a-z_]\w*)\s*\(\s*(?<params>.*)\s*\)\s*->\s*(?<expr>[^\s]+.*)*\s*$", out Match m))
                {
                    string name = m.Get("name");
                    string lname = name.ToLower();

                    if (st.Functions.ContainsKey(lname))
                        err("errors.preproc.function_exists", name, st.Functions[lname].Context);
                    else if (st.PInvokeFunctions.ContainsKey(lname))
                        err("errors.preproc.function_exists", name, st.PInvokeFunctions[lname].Context);
                    else if (current != null)
                        err("errors.preproc.function_nesting");
                    else
                    {
                        st.Functions[lname] = new FunctionScope(defctx, m.Get("params").Trim());
                        st.Functions[lname].Lines.Add(($"Return {m.Get("expr").Trim()}", defctx));
                    }
                }
                else if (Line.Match(@"^func\s+(?<name>[a-z_]\w*)\s*\(\s*(?<params>.*)\s*\)$", out m))
                {
                    FunctionDeclarationState fds_n;

                    if (current is null)
                    {
                        string lname = m.Get("name").ToLower();

                        fds_n = FunctionDeclarationState.InvalidOpening;

                        if (IsReservedName(lname))
                            err("errors.general.reserved_name", m.Get("name"));
                        else if (st.Functions.ContainsKey(lname))
                            err("errors.preproc.function_exists", m.Get("name"), st.Functions[lname].Context);
                        else if (st.PInvokeFunctions.ContainsKey(lname))
                            err("errors.preproc.function_exists", m.Get("name"), st.PInvokeFunctions[lname].Context);
                        else
                        {
                            fds_n = FunctionDeclarationState.RegularFunction;

                            FunctionScope curr = new FunctionScope(defctx, m.Get("params"));
                            st.Functions[lname] = curr;

                            stack.Push((fds_n, curr));

                            return false;
                        }
                    }
                    else
                    {
                        fds_n = FunctionDeclarationState.InvalidNesting;

                        err("errors.preproc.function_nesting");
                    }

                    stack.Pop();
                    stack.Push((fds_n, current));
                }
                else if (Line.Match(@"^func\s+(?<name>[a-z_]\w*)\s+as\s*""(?<sig>.*)""\s*from\s*""(?<lib>.*)""$", out m))
                {
                    string name = m.Get("name");
                    string sig = m.Get("sig").Trim();
                    string lib = m.Get("lib").Trim();
                    string lname = name.ToLower();

                    if (string.IsNullOrWhiteSpace(lib))
                        err("errors.preproc.pinvoke_no_lib", name);
                    else if (string.IsNullOrWhiteSpace(sig))
                        err("errors.preproc.pinvoke_no_sig", name);
                    else if (st.Functions.ContainsKey(lname))
                        err("errors.preproc.function_exists", name, st.Functions[lname].Context);
                    else if (st.PInvokeFunctions.ContainsKey(lname))
                        err("errors.preproc.function_exists", name, st.PInvokeFunctions[lname].Context);
                    else if (current != null)
                        err("errors.preproc.function_nesting");
                    else
                        st.PInvokeFunctions[lname] = (sig, lib, defctx);
                }
                else if (Line.Match(@"^\$(?<var>[_a-z]\w*)(?<indexer>.*)\s*\=\s*func\s*\(\s*(?<params>.*)\s*\)$", out m))
                {
                    string var = m.Get("var");
                    string par = m.Get("params");
                    string idx = m.Get("indexer");
                    string name = $"λ__{_λcount++:x8}";
                    FunctionScope curr = new FunctionScope(defctx, par);

                    st.Functions[name] = curr;
                    stack.Push((FunctionDeclarationState.InsideLambda, curr));

                    (current ?? st.GlobalFunction).Lines.Add(($"${var}{idx} = {name}", defctx));
                }
                else if (Line.Match("^endfunc$", out _))
                {
                    if (current is null)
                        err("errors.preproc.unexpected_endfunc");
                    else
                    {
                        stack.Pop();

                        if ((fds != FunctionDeclarationState.RegularFunction) && (fds != FunctionDeclarationState.InsideLambda))
                            stack.Push((FunctionDeclarationState.RegularFunction, current));
                    }
                }
                else
                    return fds == FunctionDeclarationState.RegularFunction || fds == FunctionDeclarationState.InsideLambda;

                return false;
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
                                    ProcessPragmaCompileOption(name, value, st.CompileInfo, err);

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

            private static void ProcessPragmaCompileOption(string name, string value, CompileInfo ci, ErrorReporter err)
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
                    ["compatibility"] = () => ci.Compatibility = (Compatibility)Enum.Parse(typeof(Compatibility), value.ToLower(), true),
                    ["architecture"] = () => ci.TargetArchitecture = (Architecture)Enum.Parse(typeof(Architecture), value.ToLower(), true),
                    ["x64"] = () => {
                        bool x64 = bool.Parse(value);

                        switch (ci.TargetArchitecture)
                        {
                            case Architecture.X86:
                            case Architecture.X64:
                                ci.TargetArchitecture = x64 ? Architecture.X64 : Architecture.X86;

                                return;
                            case Architecture.Arm:
                            case Architecture.Arm64:
                                ci.TargetArchitecture = x64 ? Architecture.Arm64 : Architecture.Arm;

                                return;
                        }
                    },
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


            private struct RawLine
            {
                public int[] OriginalLineNumbers { get; }
                public string Content { get; }
                public FileInfo File { get; }


                public RawLine(string c, int[] l, FileInfo f) =>
                    (Content, OriginalLineNumbers, File) = (c, l, f);
            }

            private enum FunctionDeclarationState
                : byte
            {
                RegularFunction = 0x00,
                InvalidOpening = 0x01,
                InvalidNesting = 0x02,
                InsideLambda = 0x03,
            }

            [Flags]
            private enum LineState
                : byte
            {
                Regular = 0b_0000_0000,
                Comment = 0b_0000_0001,
                CSharp = 0b_0000_0010,
            }
        }

        private static class ASTProcessor
        {
            internal static void ParseExpressionAST(InterpreterState state, InterpreterOptions options)
            {
                List<(DefinitionContext, string, EXPRESSION[])> funccalls = new List<(DefinitionContext, string, EXPRESSION[])>();
                Dictionary<string, List<DefinitionContext>> λ_assignments = new Dictionary<string, List<DefinitionContext>>();
                FunctionparameterParser p_funcparam = new FunctionparameterParser(options.Settings.UseOptimization);
                ExpressionParser p_delcaration = new ExpressionParser(ExpressionParserMode.Declaration);
                ExpressionParser p_assignment = new ExpressionParser(ExpressionParserMode.Assignment);
                ExpressionParser p_expression = new ExpressionParser(ExpressionParserMode.Regular);
                Stack<List<string>> constants = new Stack<List<string>>();
                AST_FUNCTION _currfunc = null;

                foreach (dynamic parser in new dynamic[] { p_funcparam, p_expression, p_assignment, p_delcaration })
                    parser.Initialize();

                foreach ((string name, FUNCTION func) in new[] { (GLOBAL_FUNC_NAME, state.Functions[GLOBAL_FUNC_NAME]) }.Concat(state.Functions.WhereSelect(x => x.Key != GLOBAL_FUNC_NAME, x => (x.Key, x.Value))))
                    state.ASTFunctions[name] = ProcessWhileBlocks(state, process(func)[0]);

                state.PInvokeSignatures = ParsePinvokeFunctions(state, funccalls.Where(call => call.Item2.ToLower() == "dllcall").ToArray()).ToArray();

                ValidateFunctionCalls(state, options, λ_assignments, funccalls.ToArray());

                dynamic process(Entity e)
                {
                    void err(string path, params object[] args) => state.ReportKnownError(path, e.DefinitionContext, args);
                    void warn(string path, params object[] args) => state.ReportKnownWarning(path, e.DefinitionContext, args);
                    void note(string path, params object[] args) => state.ReportKnownNote(path, e.DefinitionContext, args);
                    EXPRESSION optimize(EXPRESSION expr) => Analyzer.ProcessExpression(expr);
                    AST_STATEMENT[] process_lines() => e.RawLines.SelectMany(rl => process(rl) as AST_STATEMENT[]).Where(l => l != null).ToArray();
                    AST_CONDITIONAL_BLOCK process_condition()
                    {
                        EXPRESSION expr = try_parse_expression((e as ConditionalEntity)?.RawCondition, false);

                        return new AST_CONDITIONAL_BLOCK
                        {
                            Condition = expr,
                            Statements = process_lines(),
                            Context = e.DefinitionContext
                        };
                    }
                    MULTI_EXPRESSION[] parse_multi_expressions(string expr, ExpressionParser p, bool suppress = false)
                    {
                        expr = expr.Trim();

                        try
                        {
                            MULTI_EXPRESSION[] mes = p.Parse(expr);

                            funccalls.AddRange(from exp in mes.SelectMany(me =>
                                               {
                                                   switch (me)
                                                   {
                                                       case MULTI_EXPRESSION.SingleValue sv:
                                                           return new[] { sv.Item };
                                                       case MULTI_EXPRESSION.ValueRange vr:
                                                           return new[] { vr.Item1, vr.Item2 };
                                                   }

                                                   return new EXPRESSION[0];
                                               })
                                               from fce in Analyzer.GetFunctionCallExpressions(exp)
                                               where fce.Item1 != null
                                               select (e.DefinitionContext, fce.Item1, fce.Item2?.ToArray() ?? new EXPRESSION[0]));

                            return mes;
                        }
                        catch (Exception ex)
                        {
                            if (!suppress)
                                err("errors.astproc.parser_error", expr, ex.Message);

                            return null;
                        }
                    }
                    EXPRESSION parse_expression(string expr, bool assign, bool suppress = false)
                    {
                        if (parse_multi_expressions(expr, assign ? p_assignment : p_expression, suppress) is MULTI_EXPRESSION[] m)
                            if (m.Length > 1)
                            {
                                if (!suppress)
                                    err("errors.astproc.no_comma_allowed", expr);
                            }
                            else if (m[0].IsValueRange)
                            {
                                if (!suppress)
                                    err("errors.astproc.no_range_allowed", expr);
                            }
                            else
                                return (m[0] as MULTI_EXPRESSION.SingleValue)?.Item;

                        return null;
                    }
                    EXPRESSION try_parse_expression(string exprstr, bool assign)
                    {
                        EXPRESSION expr;

                        if ((expr = parse_expression(exprstr, assign)) is null)
                        {
                            expr = parse_expression($"{DISCARD_VARIBLE} = ({exprstr})", true, true);

                            if (expr != null)
                            {
                                state.RemoveLastErrorOrWarning();

                                switch ((expr as EXPRESSION.AssignmentExpression)?.Item)
                                {
                                    case ASSIGNMENT_EXPRESSION.ArrayAssignment a:
                                        expr = a.Item4;

                                        break;
                                    case ASSIGNMENT_EXPRESSION.ScalarAssignment a:
                                        expr = a.Item3;

                                        break;
                                }
                            }
                        }

                        return expr;
                    }

                    void addconstants(IEnumerable<string> ie) => constants.Peek().AddRange(ie);
                    bool isconstant(string v) => (from c in constants
                                                  from x in c
                                                  where x.Equals(v, StringComparison.InvariantCultureIgnoreCase)
                                                  select x).Any();

                    constants.Push(new List<string>());

                    dynamic __inner()
                    {
                        switch (e)
                        {
                            case FUNCTION i:
                                {
                                    FUNCTION_PARAMETER[] @params = new FUNCTION_PARAMETER[0];

                                    try
                                    {
                                        @params = p_funcparam.Parse(i.RawParameters);
                                    }
                                    catch (Exception ex)
                                    {
                                        if (!string.IsNullOrWhiteSpace(i.RawParameters))
                                            err("errors.astproc.parser_error_funcdecl", i.RawParameters, ex.Message);
                                    }

                                    _currfunc = new AST_FUNCTION
                                    {
                                        Context = i.DefinitionContext,
                                        Name = i.Name,
                                        Parameters = @params.Select(p =>
                                        {
                                            switch (p.Type)
                                            {
                                                case FUNCTION_PARAMETER_TYPE.Mandatory m:
                                                    FUNCTION_PARAMETER_MODIFIER mod = m.Item;

                                                    return new AST_FUNCTION_PARAMETER(p.Variable, mod.IsByRef, mod.IsConst);
                                                case FUNCTION_PARAMETER_TYPE.Optional o:
                                                    EXPRESSION expr = null;

                                                    switch (o.Item)
                                                    {
                                                        case FUNCTION_PARAMETER_DEFVAL.Lit l:
                                                            expr = EXPRESSION.NewLiteral(l.Item);

                                                            break;
                                                        case FUNCTION_PARAMETER_DEFVAL.Mac l:
                                                            expr = EXPRESSION.NewMacro(l.Item);

                                                            break;
                                                    }

                                                    return new AST_FUNCTION_PARAMETER_OPT(p.Variable, expr);
                                            }

                                            return null;
                                        }).ToArray(),
                                    };

                                    addconstants(_currfunc.Parameters.WhereSelect(p => p.Const, p => p.Name.Name));

                                    if (i.Name == GLOBAL_FUNC_NAME)
                                        state.ASTFunctions[GLOBAL_FUNC_NAME] = _currfunc;

                                    if ((_currfunc.Statements = process_lines()).Length == 0)
                                        note("notes.empty_function", i.Name);

                                    if (i.Name != GLOBAL_FUNC_NAME)
                                        _currfunc.Statements = _currfunc.Statements.Concat(process(new RETURN(i)) as AST_STATEMENT[]).ToArray();

                                    return _currfunc;
                                }
                            case IF i:
                                return new AST_IF_STATEMENT
                                {
                                    If = process(i.If),
                                    ElseIf = i.ElseIfs.Select(x => process(x) as AST_CONDITIONAL_BLOCK).ToArray(),
                                    OptionalElse = i.Else is ELSE_BLOCK eb && process(eb) is AST_STATEMENT[] el && el.Length > 0 ? el : null,
                                    Context = e.DefinitionContext,
                                };
                            case IF_BLOCK _:
                            case ELSEIF_BLOCK _:
                                return process_condition();
                            case ELSE_BLOCK i:
                                return process_lines();
                            case WHILE i:
                                return (AST_WHILE_STATEMENT)process_condition();
                            case DO_UNTIL i:
                                return new AST_WHILE_STATEMENT
                                {
                                    WhileBlock = new AST_CONDITIONAL_BLOCK
                                    {
                                        Condition = EXPRESSION.NewLiteral(LITERAL.True),
                                        Statements = new AST_STATEMENT[]
                                        {
                                            new AST_IF_STATEMENT
                                            {
                                                If = process_condition(),
                                                OptionalElse = new AST_STATEMENT[]
                                                {
                                                    new AST_BREAK_STATEMENT { Level = 1 }
                                                }
                                            }
                                        }
                                    }
                                };
                            case SELECT i:
                                {
                                    IEnumerable<AST_CONDITIONAL_BLOCK> cases = i.Cases.Select(x => (process(x)[0] as AST_SELECT_CASE)?.CaseBlock);

                                    if (cases.Any())
                                        return new AST_IF_STATEMENT
                                        {
                                            If = cases.First(),
                                            ElseIf = cases.Skip(1).ToArray()
                                        };
                                    else
                                        break;
                                }
                            case SWITCH i:
                                {
                                    AST_LOCAL_VARIABLE exprvar = new AST_LOCAL_VARIABLE
                                    {
                                        Variable = VARIABLE.NewTemporary,
                                        InitExpression = parse_expression(i.Expression, false)
                                    };
                                    EXPRESSION exprvare = EXPRESSION.NewVariableExpression(exprvar.Variable);
                                    dynamic[] cases = i.Cases.Select(x => process(x)[0]).ToArray();
                                    IEnumerable<AST_CONDITIONAL_BLOCK> condcases = cases.Select(x =>
                                    {
                                        AST_CONDITIONAL_BLOCK cblock = new AST_CONDITIONAL_BLOCK
                                        {
                                            Context = x.Context,
                                            Statements = x.Statements,
                                        };

                                        switch (x)
                                        {
                                            case AST_SWITCH_CASE_EXPRESSION ce:
                                                IEnumerable<EXPRESSION> expr = ce.Expressions.Select(ex =>
                                                {
                                                    if (ex is MULTI_EXPRESSION.SingleValue sv)
                                                        return EXPRESSION.NewBinaryExpression(OPERATOR_BINARY.EqualCaseSensitive, exprvare, optimize(sv.Item));
                                                    else if (ex is MULTI_EXPRESSION.ValueRange vr)
                                                        return EXPRESSION.NewBinaryExpression(
                                                            OPERATOR_BINARY.And,
                                                            EXPRESSION.NewBinaryExpression(
                                                            OPERATOR_BINARY.GreaterEqual,
                                                                exprvare,
                                                                optimize(vr.Item1)
                                                            ),
                                                            EXPRESSION.NewBinaryExpression(
                                                                OPERATOR_BINARY.LowerEqual,
                                                                exprvare,
                                                                optimize(vr.Item2)
                                                            )
                                                        );
                                                    else
                                                        return null;
                                                });

                                                if (expr.Any())
                                                    cblock.Condition = expr.Aggregate((a, b) => EXPRESSION.NewBinaryExpression(OPERATOR_BINARY.Or, a, b));
                                                else
                                                    cblock.Condition = EXPRESSION.NewLiteral(LITERAL.False);

                                                break;
                                            case AST_SWITCH_CASE_ELSE _:
                                                cblock.Condition = EXPRESSION.NewLiteral(LITERAL.True);

                                                break;
                                        }

                                        cblock.ExplicitLocalVariables.AddRange(x.ExplicitLocalsVariables);

                                        return cblock;
                                    });
                                    AST_SCOPE scope = new AST_SCOPE();

                                    if (condcases.Any())
                                        scope.Statements = new AST_STATEMENT[]
                                        {
                                            new AST_IF_STATEMENT
                                            {
                                                Context = i.DefinitionContext,
                                                If = condcases.First(),
                                                ElseIf = condcases.Skip(1).ToArray()
                                            }
                                        };

                                    if (cases.Count(x => x is AST_SWITCH_CASE_ELSE) > 1)
                                        err("errors.astproc.multiple_switch_case_else");

                                    scope.ExplicitLocalVariables.Add(exprvar);

                                    addconstants(condcases.SelectMany(x => x.ExplicitLocalVariables).Concat(scope.ExplicitLocalVariables).WhereSelect(x => x.Constant, x => x.Variable.Name));

                                    return scope;
                                }
                            case SELECT_CASE i:
                                return (AST_SELECT_CASE)new AST_CONDITIONAL_BLOCK
                                {
                                    Condition = i.RawCondition.ToLower() == "else" ? EXPRESSION.NewLiteral(LITERAL.True) : parse_expression(i.RawCondition, false),
                                    Statements = process_lines(),
                                    Context = i.DefinitionContext
                                };
                            case SWITCH_CASE i:
                                {
                                    string expr = i.RawCondition;

                                    if (expr.ToLower() == "else")
                                        return new AST_SWITCH_CASE_ELSE();
                                    else
                                        return new AST_SWITCH_CASE_EXPRESSION
                                        {
                                            Statements = process_lines(),
                                            Expressions = parse_multi_expressions(expr, p_expression)?.ToArray() ?? new MULTI_EXPRESSION[0]
                                        };
                                }
                            case RETURN i:
                                {
                                    if (_currfunc.Name == GLOBAL_FUNC_NAME)
                                        warn("warnings.astproc.global_return");

                                    return new AST_RETURN_STATEMENT
                                    {
                                        Expression = i.Expression is null ? EXPRESSION.NewLiteral(LITERAL.Default) : parse_expression(i.Expression, false)
                                    };
                                }
                            case CONTINUECASE _:
                                warn("warnings.not_impl"); // TODO

                                return new AST_CONTINUECASE_STATEMENT();
                            case CONTINUE i:
                                return new AST_CONTINUE_STATEMENT
                                {
                                    Level = (uint)i.Level
                                };
                            case BREAK i:
                                return new AST_BREAK_STATEMENT
                                {
                                    Level = (uint)i.Level
                                };
                            case WITH i:
                                {
                                    if (parse_expression(i.Expression, true) is EXPRESSION expr)
                                    {
                                        if (!expr.IsVariableExpression)
                                            err("errors.astproc.obj_expression_required", i.Expression);

                                        warn("warnings.not_impl");

                                        return new AST_WITH_STATEMENT
                                        {
                                            WithExpression = expr,
                                            WithLines = null // TODO
                                        };
                                    }
                                    else
                                        break;
                                }
                            case FOR i:
                                {
                                    DefinitionContext defctx = i.DefinitionContext;
                                    EXPRESSION start = parse_expression(i.StartExpression, false);
                                    EXPRESSION stop = parse_expression(i.StopExpression, false);
                                    EXPRESSION step = i.OptStepExpression is string stepexpr ? parse_expression(stepexpr, false) : EXPRESSION.NewLiteral(LITERAL.NewNumber(1));
                                    AST_LOCAL_VARIABLE cntvar = new AST_LOCAL_VARIABLE
                                    {
                                        Variable = new VARIABLE(i.VariableExpression),
                                        InitExpression = start,
                                    };
                                    AST_LOCAL_VARIABLE upvar = new AST_LOCAL_VARIABLE
                                    {
                                        Variable = VARIABLE.NewTemporary,
                                        InitExpression = optimize(EXPRESSION.NewBinaryExpression(
                                            OPERATOR_BINARY.LowerEqual,
                                            start,
                                            stop
                                        ))
                                    };

                                    addconstants(new[] { cntvar.Variable.Name, upvar.Variable.Name });

                                    EXPRESSION upcond = EXPRESSION.NewBinaryExpression(
                                        OPERATOR_BINARY.LowerEqual,
                                        EXPRESSION.NewVariableExpression(
                                            cntvar.Variable
                                        ),
                                        stop
                                    );
                                    EXPRESSION downcond = EXPRESSION.NewBinaryExpression(
                                        OPERATOR_BINARY.GreaterEqual,
                                        EXPRESSION.NewVariableExpression(
                                            cntvar.Variable
                                        ),
                                        stop
                                    );
                                    EXPRESSION cond = Analyzer.EvaluatesToFalse(upvar.InitExpression) ? downcond
                                                    : Analyzer.EvaluatesToTrue(upvar.InitExpression) ? upcond
                                                    : EXPRESSION.NewBinaryExpression(
                                                        OPERATOR_BINARY.Or,
                                                        EXPRESSION.NewBinaryExpression(
                                                            OPERATOR_BINARY.And,
                                                            EXPRESSION.NewVariableExpression(
                                                                upvar.Variable
                                                            ),
                                                            upcond
                                                        ),
                                                        EXPRESSION.NewBinaryExpression(
                                                            OPERATOR_BINARY.And,
                                                            EXPRESSION.NewUnaryExpression(
                                                                OPERATOR_UNARY.Not,
                                                                EXPRESSION.NewVariableExpression(
                                                                    upvar.Variable
                                                                )
                                                            ),
                                                            downcond
                                                        )
                                                    );
                                    AST_SCOPE scope = new AST_SCOPE
                                    {
                                        Context = defctx,
                                        Statements = new AST_STATEMENT[]
                                        {
                                            new AST_WHILE_STATEMENT
                                            {
                                                Context = defctx,
                                                WhileBlock = new AST_CONDITIONAL_BLOCK
                                                {
                                                    Context = defctx,
                                                    Condition = EXPRESSION.NewLiteral(LITERAL.True),
                                                    Statements = new AST_STATEMENT[]
                                                    {
                                                        new AST_IF_STATEMENT
                                                        {
                                                            Context = defctx,
                                                            If = new AST_CONDITIONAL_BLOCK
                                                            {
                                                                Context = defctx,
                                                                Condition = cond,
                                                                Statements = process_lines().Concat(new AST_STATEMENT[]
                                                                {
                                                                    new AST_ASSIGNMENT_EXPRESSION_STATEMENT
                                                                    {
                                                                        Context = defctx,
                                                                        Expression = ASSIGNMENT_EXPRESSION.NewScalarAssignment(
                                                                            OPERATOR_ASSIGNMENT.AssignAdd,
                                                                            cntvar.Variable,
                                                                            EXPRESSION.NewLiteral(LITERAL.NewNumber(1))
                                                                        )
                                                                    }
                                                                }).ToArray()
                                                            },
                                                            OptionalElse = new AST_STATEMENT[]
                                                            {
                                                                new AST_BREAK_STATEMENT { Level = 1 }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    };

                                    scope.ExplicitLocalVariables.AddRange(new[] { cntvar, upvar });

                                    return scope;
                                }
                            case FOREACH i:
                                {
                                    DefinitionContext defctx = i.DefinitionContext;
                                    AST_LOCAL_VARIABLE elemvar = new AST_LOCAL_VARIABLE
                                    {
                                        Variable = new VARIABLE(i.VariableExpression),
                                        InitExpression = EXPRESSION.NewLiteral(LITERAL.Null)
                                    };

                                    if (parse_expression(i.RangeExpression, false) is EXPRESSION collexpr)
                                    {
                                        AST_LOCAL_VARIABLE collvar = new AST_LOCAL_VARIABLE
                                        {
                                            Variable = VARIABLE.NewTemporary,
                                            InitExpression = collexpr
                                        };
                                        AST_LOCAL_VARIABLE cntvar = new AST_LOCAL_VARIABLE
                                        {
                                            Variable = VARIABLE.NewTemporary,
                                            InitExpression = EXPRESSION.NewLiteral(LITERAL.NewNumber(0))
                                        };

                                        addconstants(new[] { cntvar.Variable.Name, elemvar.Variable.Name, collvar.Variable.Name });

                                        AST_SCOPE scope = new AST_SCOPE
                                        {
                                            Context = defctx,
                                            Statements = new AST_STATEMENT[]
                                            {
                                                new AST_ASSIGNMENT_EXPRESSION_STATEMENT
                                                {
                                                    Context = defctx,
                                                    Expression = ASSIGNMENT_EXPRESSION.NewScalarAssignment(
                                                        OPERATOR_ASSIGNMENT.Assign,
                                                        elemvar.Variable,
                                                        EXPRESSION.NewArrayAccess(
                                                            EXPRESSION.NewVariableExpression(
                                                                collvar.Variable
                                                            ),
                                                            EXPRESSION.NewVariableExpression(
                                                                cntvar.Variable
                                                            )
                                                        )
                                                    )
                                                },
                                            }
                                            .Concat(process_lines())
                                            .Concat(new AST_STATEMENT[]
                                            {
                                                new AST_IF_STATEMENT
                                                {
                                                    Context = defctx,
                                                    If = new AST_CONDITIONAL_BLOCK
                                                    {
                                                        Context = defctx,
                                                        Condition = EXPRESSION.NewBinaryExpression(
                                                            OPERATOR_BINARY.GreaterEqual,
                                                            EXPRESSION.NewVariableExpression(
                                                                cntvar.Variable
                                                            ),
                                                            EXPRESSION.NewFunctionCall(
                                                                new Tuple<string, FSharpList<EXPRESSION>>(
                                                                    "ubound",
                                                                    new FSharpList<EXPRESSION>(
                                                                        EXPRESSION.NewVariableExpression(
                                                                            collvar.Variable
                                                                        ),
                                                                        FSharpList<EXPRESSION>.Empty
                                                                    )
                                                                )
                                                            )
                                                        ),
                                                        Statements = new AST_STATEMENT[]
                                                        {
                                                            new AST_BREAK_STATEMENT { Level = 1 }
                                                        }
                                                    },
                                                    OptionalElse = new AST_STATEMENT[]
                                                    {
                                                        new AST_ASSIGNMENT_EXPRESSION_STATEMENT
                                                        {
                                                            Context = defctx,
                                                            Expression = ASSIGNMENT_EXPRESSION.NewScalarAssignment(
                                                                OPERATOR_ASSIGNMENT.AssignAdd,
                                                                cntvar.Variable,
                                                                EXPRESSION.NewLiteral(
                                                                    LITERAL.NewNumber(1)
                                                                )
                                                            )
                                                        }
                                                    }
                                                }
                                            })
                                            .ToArray()
                                        };

                                        scope.ExplicitLocalVariables.AddRange(new[] { cntvar, elemvar, collvar });

                                        return scope;
                                    }

                                    break;
                                }
                            case CS_INLINE i:
                                return new AST_INLINE_CSHARP { Code = i.SourceCode };
                            case RAWLINE i:
                                {
                                    if (i.RawContent is string exprstr)
                                    {
                                        EXPRESSION expr = try_parse_expression(exprstr, true);

                                        // TODO : with-statement rawline ?

                                        if (expr is EXPRESSION ex)
                                            if ((ex as EXPRESSION.AssignmentExpression)?.Item is ASSIGNMENT_EXPRESSION aexpr)
                                            {
                                                string varname = (aexpr as ASSIGNMENT_EXPRESSION.ScalarAssignment)?.Item2?.Name ?? (aexpr as ASSIGNMENT_EXPRESSION.ArrayAssignment)?.Item2?.Name ?? DISCARD_VARIBLE;

                                                if (isconstant(varname))
                                                    err("errors.astproc.variable_is_const", varname);
                                                else
                                                    return new AST_ASSIGNMENT_EXPRESSION_STATEMENT
                                                    {
                                                        Expression = aexpr
                                                    };
                                            }
                                            else
                                            {
                                                if (!ex.IsFunctionCall && !ex.IsΛFunctionCall)
                                                    if (Analyzer.IsStatic(ex))
                                                    {
                                                        note("notes.optimized_away");

                                                        break;
                                                    }
                                                    else
                                                        warn("warnings.astproc.expression_result_discarded");

                                                return new AST_EXPRESSION_STATEMENT
                                                {
                                                    Expression = ex,
                                                };
                                            }
                                    }

                                    break;
                                }
                            case DECLARATION i:
                                {
                                    AST_FUNCTION targetfunc = i.Modifiers.Contains("global") ? state.ASTFunctions[GLOBAL_FUNC_NAME] : _currfunc;
                                    List<AST_STATEMENT> statements = new List<AST_STATEMENT>();
                                    bool @const = i.Modifiers.Contains("const");

                                    if (parse_multi_expressions(i.Expression, p_delcaration) is MULTI_EXPRESSION[] expressions)
                                    {
                                        VARIABLE[] vars = expressions.Select(mexpr =>
                                        {
                                            if (mexpr is MULTI_EXPRESSION.SingleValue sv)
                                                return sv.Item;
                                            else
                                                err("errors.astproc.no_range_as_init");

                                            return null;
                                        })
                                        .Where(expr => expr != null)
                                        .Select(expr =>
                                        {
                                            if ((expr as EXPRESSION.AssignmentExpression)?.Item is ASSIGNMENT_EXPRESSION.ScalarAssignment scaexpr && scaexpr.Item3 != null)
                                                if (scaexpr.Item1 == OPERATOR_ASSIGNMENT.Assign)
                                                {
                                                    VARIABLE var = scaexpr.Item2;

                                                    if (targetfunc.ExplicitLocalVariables.Find(lv => lv.Variable.Equals(var)) is AST_LOCAL_VARIABLE prev)
                                                        err("errors.astproc.variable_exists", prev);
                                                    else
                                                    {
                                                        if (scaexpr.Item3 is EXPRESSION.ArrayInitExpression arrinit)
                                                        {
                                                            bool stat = true;
                                                            decimal[] dims = (from EXPRESSION ex in arrinit.Item1
                                                                              let cst = Analyzer.GetConstantValue(ex)
                                                                              let iscst = cst.IsSome()
                                                                              let _ = stat &= iscst
                                                                              where iscst
                                                                              select (decimal)cst.Value).ToArray();
                                                            FSharpList<INIT_EXPRESSION> vals = arrinit.Item2;

                                                            if ((vals.Length > 0) && stat)
                                                                if (dims.Contains(0m))
                                                                    note("notes.init_0_dim", dims.Length);
                                                                else
                                                                {
                                                                    int[] valdims = Analyzer.GetMatrixDimensions(vals);

                                                                    if ((valdims.Length % 2) == 0)
                                                                    {
                                                                        err("errors.astproc.init_internal_error");

                                                                        return null;
                                                                    }
                                                                    else
                                                                        for (int j = 0; j < valdims.Length; ++j)
                                                                            if (((j % 2) == 0) && (valdims[j] != 1))
                                                                            {
                                                                                err("errors.astproc.init_internal_error");

                                                                                return null;
                                                                            }
                                                                            else if (((j % 2) == 1) && (valdims[j] != (int)dims[j / 2]))
                                                                            {
                                                                                err("errors.astproc.init_dimension_mismatch", j / 2, (int)dims[j / 2], valdims[j]);

                                                                                return null;
                                                                            }
                                                                            else
                                                                                continue;
                                                                }
                                                            else if (vals.Length > 0)
                                                            {
                                                                err("errors.astproc.init_no_dynamic_dimensions");

                                                                return null;
                                                            }
                                                        }

                                                        addconstants(new[] { var.Name });

                                                        targetfunc.ExplicitLocalVariables.Add(new AST_LOCAL_VARIABLE
                                                        {
                                                            Context = i.DefinitionContext,
                                                            Constant = @const,
                                                            Variable = var,
                                                        });
                                                        statements.Add(new AST_ASSIGNMENT_EXPRESSION_STATEMENT
                                                        {
                                                            Context = i.DefinitionContext,
                                                            Expression = ASSIGNMENT_EXPRESSION.NewScalarAssignment(
                                                                OPERATOR_ASSIGNMENT.Assign,
                                                                var,
                                                                scaexpr.Item3
                                                            )
                                                        });

                                                        return var;
                                                    }
                                                }
                                                else
                                                    err("errors.astproc.init_invalid_operator", scaexpr.Item1);
                                            else if (@const)
                                                err("errors.astproc.missing_value");
                                            else
                                                err("errors.astproc.init_invalid_expression");

                                            return null;
                                        })
                                        .Where(expr => expr != null)
                                        .ToArray();

                                        return new AST_SCOPE
                                        {
                                            Statements = statements.ToArray(),
                                        };
                                    }
                                    else if (i.Expression.Trim().EndsWith(','))
                                        err("errors.astproc.trailing_comma");

                                    break;
                                }
                            case REDIM i:
                                if (isconstant(i.VariableName))
                                    err("errors.astproc.variable_is_const", i.VariableName);
                                else
                                    return new AST_REDIM_STATEMENT
                                    {
                                        DimensionExpressions = i.Dimensions.Select(x =>
                                        {
                                            EXPRESSION expr = parse_expression($"{DISCARD_VARIBLE} = ({x})", true);
                                            EXPRESSION vexpr = ((expr as EXPRESSION.AssignmentExpression)?.Item as ASSIGNMENT_EXPRESSION.ScalarAssignment)?.Item3;

                                            if (vexpr is null)
                                                ; // TODO : report error

                                            return vexpr;
                                        }).ToArray(),
                                        Variable = new VARIABLE(i.VariableName)
                                    };

                                break;
                            case λ_ASSIGNMENT i:
                                {
                                    if (isconstant(i.VariableName))
                                        err("errors.astproc.variable_is_const", i.VariableName);
                                    else if (parse_expression(i.VariableName.Trim(), false, false) is EXPRESSION expr)
                                    {
                                        string fname = i.FunctionName.ToLower();

                                        if (fname == "dllcall")
                                            err("errors.astproc.dllcall_assign");
                                        else
                                        {
                                            if (!λ_assignments.ContainsKey(fname))
                                                λ_assignments[fname] = new List<DefinitionContext>();

                                            λ_assignments[fname].Add(i.DefinitionContext);

                                            return new AST_Λ_ASSIGNMENT_STATEMENT
                                            {
                                                VariableExpression = expr,
                                                Function = fname,
                                            };
                                        }
                                    }
                                    else
                                        ; // TODO : error

                                    break;
                                }
                            default:
                                err("errors.astproc.unknown_entity", e?.GetType()?.FullName ?? "<null>");

                                return null;
                        }

                        return null;
                    }

                    dynamic res = __inner();

                    if (res is AST_STATEMENT s)
                        s.Context = e.DefinitionContext;

                    constants.Pop();

                    return res is AST_CONDITIONAL_BLOCK cb ? (dynamic)cb : res is IEnumerable<AST_STATEMENT> @enum ? @enum.ToArray() : new AST_STATEMENT[] { res };
                }
            }

            private static (string, PINVOKE_SIGNATURE)[] ParsePinvokeFunctions(InterpreterState state, (DefinitionContext, string, EXPRESSION[])[] dllcalls)
            {
                bool getstr(EXPRESSION e, out string s)
                {
                    s = null;

                    if (e is EXPRESSION.Literal lit && lit.Item is LITERAL.String str)
                        s = str.Item;
                    else
                        return false;

                    return true;
                }
                List<(string, PINVOKE_SIGNATURE)> used_sigs = new List<(string, PINVOKE_SIGNATURE)>();
                PInvokeParser parser = new PInvokeParser();

                parser.Initialize();

                foreach (string name in state.PInvokeFunctions.Keys)
                {
                    (string s, string lib, DefinitionContext ctx) = state.PInvokeFunctions[name];
                    PINVOKE_SIGNATURE sig = null;

                    try
                    {
                        sig = parser.Parse(s);
                    }
                    catch
                    {
                    }

                    if (sig is null)
                        state.ReportKnownError("errors.astproc.pinvoke_sig_invalid", ctx, s, name);
                    else if (used_sigs.Contains((lib, sig)))
                        state.ReportKnownWarning("warnings.astproc.pinvoke_already_declared", ctx, s, lib);
                    else if (Array.Find(BUILT_IN_FUNCTIONS, x => x.Name.ToLower() == name).Name is string builtin)
                        state.ReportKnownError("errors.general.reserved_name", ctx, builtin);
                    else
                    {
                        used_sigs.Add((lib, sig));

                        int cnt = 0;
                        (VARIABLE, PINVOKE_TYPE)[] pars = sig.Paramters.Select(t => (new VARIABLE("_p" + cnt++), t)).ToArray();
                        EXPRESSION iexpr = EXPRESSION.NewFunctionCall(
                            new Tuple<string, FSharpList<EXPRESSION>>(
                                nameof(AutoItFunctions.TryConvertFrom),
                                new FSharpList<EXPRESSION>(
                                    EXPRESSION.NewFunctionCall(
                                        new Tuple<string, FSharpList<EXPRESSION>>(
                                            AutoItFunctions.GeneratePInvokeWrapperName(lib, sig.Name),
                                            ListModule.OfSeq(pars.Select(x => EXPRESSION.NewFunctionCall(
                                                new Tuple<string, FSharpList<EXPRESSION>>(
                                                    nameof(AutoItFunctions.TryConvertTo),
                                                    ListModule.OfSeq(
                                                        new EXPRESSION[2]
                                                        {
                                                            EXPRESSION.NewVariableExpression(x.Item1),
                                                            EXPRESSION.NewLiteral(
                                                                LITERAL.NewString(x.Item2.ToString())
                                                            )
                                                        }
                                                    )
                                                )
                                            )))
                                        )
                                    ),
                                    new FSharpList<EXPRESSION>(
                                        EXPRESSION.NewLiteral(
                                            LITERAL.NewString(sig.ReturnType.ToString())
                                        ),
                                        FSharpList<EXPRESSION>.Empty
                                    )
                                )
                            )
                        );

                        state.ASTFunctions[name] = new AST_FUNCTION
                        {
                            Context = ctx,
                            Name = name,
                            Parameters = pars.Select(v => new AST_FUNCTION_PARAMETER(v.Item1, false, false)).ToArray(),
                            Statements = new AST_STATEMENT[1]
                            {
                                sig.ReturnType.IsVoid ? new AST_EXPRESSION_STATEMENT { Expression = iexpr } as AST_STATEMENT : new AST_RETURN_STATEMENT { Expression = iexpr }
                            }
                        };
                    }
                }

                foreach ((DefinitionContext ctx, _, EXPRESSION[] args) in dllcalls)
                    if (args.Length < 3)
                        state.ReportKnownError("errors.astproc.not_enough_args", ctx, "DllCall", 3, args.Length);
                    else if ((args.Length % 2) == 0)
                        state.ReportKnownError("errors.astproc.dllcall_odd_argc", ctx);
                    else if (getstr(args[0], out string lib) && getstr(args[1], out string ret) && getstr(args[2], out string name))
                    {
                        int index = 0;
                        string pstr = null;
                        PINVOKE_SIGNATURE sig = null;
                        IEnumerable<string> ptypes = from arg in args.Skip(3)
                                                     let i = index++
                                                     where (i % 2) == 0
                                                     where getstr(arg, out pstr)
                                                     select pstr;
                        string sigstr = $"{ret} {name}({string.Join(", ", ptypes)})";

                        try
                        {
                            sig = parser.Parse(sigstr);
                        }
                        catch
                        {
                        }

                        if (sig is null)
                            state.ReportKnownError("errors.astproc.pinvoke_sig_invalid", ctx, sigstr, name);
                        else if (!used_sigs.Contains((lib, sig)))
                            used_sigs.Add((lib, sig));
                    }
                    else
                        state.ReportKnownError("errors.astproc.dllcall_const_args", ctx);

                return used_sigs.ToArray();
            }

            private static void ValidateFunctionCalls(InterpreterState state, InterpreterOptions options, Dictionary<string, List<DefinitionContext>> λassignments, params (DefinitionContext, string, EXPRESSION[])[] calls)
            {
                OS target = options.Compatibility.GetOperatingSystem();

                foreach ((DefinitionContext context, string func, EXPRESSION[] args) in calls)
                {
                    void err(string name, params object[] argv) => state.ReportKnownError(name, context, argv);
                    string lfunc = func.ToLower();

                    if (BUILT_IN_FUNCTIONS.Any(x => x.Name == lfunc))
                    {
                        (string f, int mac, int oac, OS[] os, bool us, CompilerIntrinsicMessage[] msgs) = BUILT_IN_FUNCTIONS.First(x => x.Name == lfunc);

                        if (!os.Contains(target))
                            err("errors.astproc.invalid_system", target, string.Join(",", os));

                        if (us && !options.AllowUnsafeCode)
                            err("errors.astproc.unsafe_func", f);

                        if (f != "dllcall")
                            if (args.Length < mac)
                                err("errors.astproc.not_enough_args", f, mac, args.Length);
                            else if (args.Length > mac + oac)
                                err("errors.astproc.too_many_args", f, mac + oac);

                        foreach (CompilerIntrinsicMessage msg in msgs)
                            if (msg is WarningAttribute w)
                                state.ReportKnownWarning(w.MessageName, context, w.Arguments);
                            else if (msg is NoteAttribute n)
                                state.ReportKnownNote(n.MessageName, context, n.Arguments);
                    }
                    else if (!state.ASTFunctions.ContainsKey(lfunc))
                        err("errors.astproc.func_not_declared", func);
                    else if (IsReservedCall(lfunc))
                        err("errors.astproc.reserved_call", func);
                    else
                    {
                        AST_FUNCTION f = state.ASTFunctions[lfunc];
                        int index = 0;

                        foreach (EXPRESSION arg in args)
                            if (index >= f.Parameters.Length)
                            {
                                err("errors.astproc.too_many_args", func, f.Parameters.Length);

                                break;
                            }
                            else
                                ++index;

                        int mpcnt = f.Parameters.Count(p => !(p is AST_FUNCTION_PARAMETER_OPT));

                        if (index < mpcnt)
                            err("errors.astproc.not_enough_args", f, mpcnt, index);

                        if (func.Equals("execute", StringComparison.InvariantCultureIgnoreCase))
                            state.ReportKnownNote("notes.unsafe_execute", context);
                    }
                }

                foreach (string uncalled in state.ASTFunctions.Keys.Except(calls.Select(x => x.Item2).Distinct()))
                    if ((uncalled != GLOBAL_FUNC_NAME) && !uncalled.Contains('λ') && !λassignments.ContainsKey(uncalled.ToLower()))
                        state.ReportKnownNote("notes.uncalled_function", state.ASTFunctions[uncalled].Context, uncalled);
            }

            private static AST_FUNCTION ProcessWhileBlocks(InterpreterState state, AST_FUNCTION func)
            {
                ReversedLabelStack ls_cont = new ReversedLabelStack();
                ReversedLabelStack ls_exit = new ReversedLabelStack();

                return process(func) as AST_FUNCTION;

                AST_STATEMENT process(AST_STATEMENT e)
                {
                    if (e is null)
                        return null;

                    T[] procas<T>(T[] instr) where T : AST_STATEMENT => instr?.Select(x => process(x) as T)?.ToArray();

                    AST_STATEMENT __inner()
                    {
                        switch (e)
                        {
                            case AST_CONTINUE_STATEMENT s:
                                return new AST_GOTO_STATEMENT { Label = ls_cont[s.Level] };
                            case AST_BREAK_STATEMENT s:
                                return new AST_GOTO_STATEMENT { Label = ls_exit[s.Level] };
                            case AST_WHILE_STATEMENT s:
                                {
                                    if (s.WhileBlock?.Condition is null)
                                        return s;

                                    if (Analyzer.EvaluatesToFalse(s.WhileBlock.Condition))
                                    {
                                        state.ReportKnownNote("notes.optimized_away", s.Context);

                                        return new AST_SCOPE { Statements = new AST_STATEMENT[0] };
                                    }

                                    ls_cont.Push(AST_LABEL.NewLabel);
                                    ls_exit.Push(AST_LABEL.NewLabel);

                                    AST_WHILE_STATEMENT w = new AST_WHILE_STATEMENT
                                    {
                                        WhileBlock = s.WhileBlock,
                                        ContinueLabel = ls_cont[1],
                                        ExitLabel = ls_exit[1],
                                        Context = e.Context,
                                    };
                                    AST_SCOPE sc = new AST_SCOPE
                                    {
                                        Statements = new AST_STATEMENT[]
                                        {
                                            w,
                                            ls_exit[1]
                                        }
                                    };

                                    w.WhileBlock.Statements = new AST_STATEMENT[] { ls_cont[1] }.Concat(procas(w.WhileBlock.Statements)).ToArray();

                                    ls_cont.Pop();
                                    ls_exit.Pop();

                                    return sc;
                                }
                            case AST_SCOPE s:
                                s.Statements = procas(s.Statements);

                                return s;
                            case AST_IF_STATEMENT s:
                                {
                                    s.If = process(s.If) as AST_CONDITIONAL_BLOCK;
                                    s.ElseIf = procas(s.ElseIf);
                                    s.OptionalElse = procas(s.OptionalElse);

                                    AST_CONDITIONAL_BLOCK[] conditions =
                                        new[] { s.If }
                                        .Concat(s.ElseIf ?? new AST_CONDITIONAL_BLOCK[0])
                                        .Concat(new[]
                                        {
                                            new AST_CONDITIONAL_BLOCK
                                            {
                                                Context = s.OptionalElse?.FirstOrDefault()?.Context ?? default,
                                                Condition = EXPRESSION.NewLiteral(LITERAL.True),
                                                Statements = s.OptionalElse ?? new AST_STATEMENT[0]
                                            }
                                        })
                                        .Where(b =>
                                        {
                                            if (b.Context.FilePath is null)
                                                return true;
                                            else
                                            {
                                                bool skippable = Analyzer.EvaluatesToFalse(b.Condition);

                                                if (b.IsEmpty)
                                                    state.ReportKnownNote("notes.empty_block", b.Context);

                                                if (skippable)
                                                    state.ReportKnownNote("notes.optimized_away", b.Context);

                                                return !skippable;
                                            }
                                        })
                                        .ToArray();

                                    int lastok = conditions.Length;

                                    for (int i = 0; i < conditions.Length; ++i)
                                        if (Analyzer.EvaluatesToTrue(conditions[i].Condition))
                                            lastok = i;
                                        else if (i > lastok)
                                            state.ReportKnownNote("notes.optimized_away", conditions[i].Context);

                                    conditions = conditions.Take(lastok + 1).ToArray();

                                    if (conditions.Length > 0)
                                    {
                                        s.If = conditions[0];
                                        s.ElseIf = conditions.Skip(1).ToArray();
                                        s.OptionalElse = new AST_STATEMENT[0];

                                        return s;
                                    }
                                    else
                                        return new AST_SCOPE
                                        {
                                            Statements = s.OptionalElse
                                        };
                                }
                            case AST_WITH_STATEMENT s:
                                s.WithLines = procas(s.WithLines);

                                return s;
                            case AST_SWITCH_STATEMENT s:
                                s.Cases = procas(s.Cases);

                                return s;
                            default:
                                return e;
                        }
                    }
                    AST_STATEMENT res = __inner();

                    res.Context = e.Context;

                    return res;
                }
            }
        }
    }
}
