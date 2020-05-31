using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System;

using AutoItInterpreter.PartialAST;
using AutoItInterpreter.Properties;
using AutoItExpressionParser;
using AutoItCoreLibrary;

namespace AutoItInterpreter
{
    using static InterpreterConstants;
    using static ExpressionAST;


    public static class ApplicationGenerator
    {
#pragma warning disable IDE1006
#pragma warning disable RSC1057
#pragma warning disable RCS1197
        internal const string DISP_SKIP_S = "___display_skip_start",
                              DISP_SKIP_E = "___display_skip_end";
        private const string NAMESPACE = "AutoIt";
        private const string APPLICATION_MODULE = "Program";
        private const string DEBUGSYMBOL_MODULE = "DebugSymbols";
        private const string TYPE_VAR_RPOVIDER = nameof(AutoItVariableDictionary);
        private const string TYPE_MAC_RPOVIDER = nameof(AutoItMacroDictionary);
        private const string REFTYPE = nameof(AutoItVariantTypeReference);
        private const string DEFCTXTYPE = "defctx";
        private const string TYPE = "v";
        private const string FUNC_MODULE = nameof(AutoItFunctions);
        private const string FUNC_PREFIX = AutoItFunctions.FUNC_PREFIX;
        private const string PARAM_PREFIX = "__param_";
        private const string DISCARD = "__discard";
        private const string SYMBOL = "__symbol";
        private const string MACROS = "__macros";
        private const string MACROS_GLOBAL = MACROS + "_g";
        private const string LAST_SYMBOL = "__lastsymbol";
        private const string VARS = "__vars";


        public static string GenerateCSharpCode(InterpreterState state, InterpreterOptions options, Dictionary<long, (DefinitionContext Context, string LineContent)> debugsymbols)
        {
            StringBuilder sb = new StringBuilder();
            long debugindex = 0;
            int csvaridx = 0;

            sb.AppendLine($@"
/*
    {options.Language["gen.header._1", DateTime.Now]}
    {options.Language["gen.header._2", options.RawCommandLine]}
    {options.Language["gen.header._3", state.Errors.Length]}
{string.Concat(state.Errors.Select(err => $"        {err}\r\n"))}*/
".Trim());

            if (state.Fatal && !options.GenerateCodeEvenWithErrors)
            {
                state.ReportKnownError("errors.generator.cannot_create", new DefinitionContext(state.RootDocument, 0));

                return sb.ToString();
            }

            string[] glob = { GLOBAL_FUNC_NAME };
            var pins = state.PInvokeSignatures.Select(x => (Name: AutoItFunctions.GeneratePInvokeWrapperName(x.Item1, x.Item2.Name), x));
            Serializer ser = new Serializer(new SerializerSettings(MACROS, VARS, TYPE, DISCARD, (func, pars) =>
            {
                ResolvedFunctionParamter[] rparams = new ResolvedFunctionParamter[pars.Length];
                string lfunc = func.ToLower();
                string rname;

                if (state.ASTFunctions.ContainsKey(lfunc))
                {
                    rname = FUNC_PREFIX + lfunc;
                    rparams = state.ASTFunctions[lfunc].Parameters.Select(p => new ResolvedFunctionParamter(p is AST_FUNCTION_PARAMETER_OPT, p.ByRef)).ToArray();
                }
                else if (pins.Any(p => p.Name.ToLower() == lfunc))
                {
                    (string name, var sig) = pins.First(p => p.Name.ToLower() == lfunc);

                    rname = name;
                    rparams = sig.Item2.Paramters.Select(_ => new ResolvedFunctionParamter(false, false)).ToArray();
                }
                else if (BUILT_IN_FUNCTIONS.Any(bif => bif.Name.ToLower() == lfunc))
                {
                    BuiltinFunctionInformation method = BUILT_IN_FUNCTIONS.First(bif => bif.Name.ToLower() == lfunc);
                    int argc = method.OptionalArgumentCount + method.MandatoryArgumentCount;

                    if (argc > 0 && method.HasParamsArguments)
                        --argc;

                    argc = Math.Max(argc, pars.Length);
                    rname = $"{FUNC_MODULE}.{method.RealName}";

                    // TODO : dunno, is the following line correct??
                    if (!((pars.Length >= method.MandatoryArgumentCount) && (pars.Length < argc)))
                        rparams = new ResolvedFunctionParamter[argc];

                    for (int i = 0; i < rparams.Length; ++i)
                        rparams[i] = i < method.MandatoryArgumentCount ? new ResolvedFunctionParamter(false, false) : new ResolvedFunctionParamter(true, false);
                }
                else
                {
                    state.ReportKnownError("errors.astproc.func_not_declared", default, func);

                    rname = $"{FUNC_MODULE}.{nameof(AutoItFunctions.__InvalidFunction__)}";

                    for (int i = 0; i < rparams.Length; ++i)
                        rparams[i] = new ResolvedFunctionParamter(false, false);
                }

                return new ResolvedFunction(
                    rname,
                    rparams
                );
            }, (s, c, a) => state.ReportKnownWarning(s, (DefinitionContext)c, a)));
            bool allman = options.Settings.IndentationStyle == IndentationStyle.AllmanStyle;
            long adddbgsymbol(DefinitionContext ctx)
            {
                if (ctx.FilePath is null)
                    return 0L;
                else if ((from kvp in debugsymbols
                          let con = kvp.Value.Context
                          where con.Equals(ctx)
                          select kvp.Key).FirstOrDefault() is long l && l > 0)
                    return l;
                else
                {
                    int start = ctx.StartLine;
                    int count = (ctx.EndLine ?? ctx.StartLine) - ctx.StartLine + 1;
                    string path = Path.GetFullPath(ctx.FilePath.FullName).Replace('\\', '/');
                    bool winsys = Win32.System == OS.Windows;
                    string cont = options.Language["gen.unknown_src"];

                    IEnumerable<RawLine> raw = (from src in state.Sources
                                                where src.Path != null
                                                let pt = Path.GetFullPath(src.Path.FullName).Replace('\\', '/')
                                                where winsys ? path.Equals(pt, StringComparison.InvariantCultureIgnoreCase) : path == pt
                                                from ll in src.Lines
                                                let ls = ll.OriginalLineNumbers[0] + 1
                                                where ls >= start
                                                where ls < start + count
                                                select ll).DistinctBy(ll => ll.Context);

                    if (raw.Any())
                        cont = string.Join("\n", raw.Select(r => (r.Content.Match($@"^{CSHARP_INLINE}\s+(?<b64>[^\s]+)\s*$", out Match m)
                                                                  ? Encoding.Default.GetString(Convert.FromBase64String(m.Get("b64")))
                                                                  : r.Content).Trim()));

                    debugsymbols[++debugindex] = (ctx, cont);

                    return ++debugindex;
                }
            }
            string tstr(EXPRESSION ex, DefinitionContext ctx, bool allowdebug = true)
            {
                if (ex is null)
                    state.ReportKnownError("errors.astproc.fatal_codegen", ctx);
                else
                    try
                    {
                        string serialized = ser.Serialize(ex, ctx);

                        if (options.IncludeDebugSymbols && allowdebug)
                            return $"__lastsymbol/*<{TYPE}>*/({adddbgsymbol(ctx)}, () => {serialized})";
                        else
                            return serialized;
                    }
                    catch (FunctionParameterCountMismatchException e)
                    {
                        state.ReportKnownError("errors.astproc.mismatch_parcount", ctx, e.FunctionName, e.RecievedArgumentCount, e.ExpectedArgumentCount);
                    }

                return $"«« {options.Language["gen.fatal_error"]} »»";
            }

            sb.AppendLine($@"
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Linq;
using System.Data;
using System.IO;
using System;
{string.Concat(state.Namespaces.Select(ns => $"\nusing {ns};"))}

using {nameof(AutoItCoreLibrary)};

#pragma warning disable CS0162
#pragma warning disable CS0164
#pragma warning disable CS1522

namespace {NAMESPACE}
{{
    using {TYPE} = {nameof(AutoItVariantType)};
    using {DEFCTXTYPE} = {nameof(DefinitionContext)};

    public static unsafe class {APPLICATION_MODULE}
    {{
        private static {TYPE_MAC_RPOVIDER} {MACROS_GLOBAL};
        private static {TYPE_VAR_RPOVIDER} {VARS};
        private static long {SYMBOL} = 0;
        private static {TYPE} {DISCARD};
".TrimEnd());

            foreach (string fn in state.ASTFunctions.Keys.Except(glob).OrderByDescending(fn => fn).Concat(glob).Reverse())
            {
                AST_FUNCTION function = state.ASTFunctions[fn];
                var paramters = function.Parameters.Select(par =>
                {
                    bool opt = par is AST_FUNCTION_PARAMETER_OPT;

                    return $"{(par.ByRef ? REFTYPE : TYPE)}{(opt && !par.ByRef ? "?" : "")} {PARAM_PREFIX}{par.Name.Name}{(opt ? " = null" : "")}";
                });

                if (fn == GLOBAL_FUNC_NAME)
                {
                    sb.AppendLine($@"
/*{DISP_SKIP_S}*/
        public static void Main(string[] argv)
        {{  
            if (argv.Contains(""{AutoItFunctions.DBG_CMDARG}""))
            {{
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(""[AutoIt++] "");
".TrimEnd());
                    if (options.IncludeDebugSymbols)
                        sb.AppendLine($@"
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(""{options.Language["gen.dbg.starting"]}"");
                Debugger.Launch();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(""[AutoIt++] "");

                if (Debugger.IsAttached)
                {{
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(""{options.Language["gen.dbg.attached"]}"");
                }}
                else
                {{
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(""{options.Language["gen.dbg.failed"]}"");
                }}

                Console.ForegroundColor = ConsoleColor.White;
".TrimEnd());
                    else
                        sb.AppendLine($@"
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(""{options.Language["gen.dbg.missing_sym"]}"");
                Console.ForegroundColor = ConsoleColor.White;
".TrimEnd());
                    sb.AppendLine($@"
            }}

            try
            {{
                Environment.SetEnvironmentVariable(""COREHOST_TRACE"", ""1"", EnvironmentVariableTarget.Process);
                //AppDomain.CurrentDomain.AssemblyResolve += (_, a) =>
                //{{
                //    string dll = (a.Name.Contains("","") ? a.Name.Substring(0, a.Name.IndexOf(',')) : a.Name.Replace("".dll"", """")).Replace(""."", ""_"");
                //
                //    if (dll.EndsWith(""_resources""))
                //        return null;
                //
                //    ResourceManager rm = new ResourceManager(""{NAMESPACE}.Properties.Resources"", Assembly.GetExecutingAssembly());
                //
                //    return Assembly.Load(rm.GetObject(dll) as byte[]);
                //}};

                {TYPE} arguments = {TYPE}.Empty;

                if (argv.FirstOrDefault(arg => Regex.IsMatch(arg, ""{AutoItFunctions.MMF_CMDPARG}=.+"")) is string mmfinarg)
                    try
                    {{
                        using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(mmfinarg.Replace(""{AutoItFunctions.MMF_CMDPARG}="", """").Trim()))
                        using (MemoryMappedViewAccessor acc = mmf.CreateViewAccessor())
                        {{
                            int len = acc.ReadInt32(0);
                            byte[] ser = new byte[len];

                            acc.ReadArray(4, ser, 0, ser.Length);

                            arguments = {TYPE}.{nameof(AutoItVariantType.Deserialize)}(ser);
                        }}
                    }}
                    catch
                    {{
                    }}

                {MACROS_GLOBAL} = new {TYPE_MAC_RPOVIDER}({FUNC_MODULE}.{nameof(AutoItFunctions.StaticMacros)}, s =>
                {{
                    switch (s.ToLower())
                    {{
                        case ""arguments"": return arguments;
                        // TODO
                    }}
                    return null;
                }});
                {VARS} = new {TYPE_VAR_RPOVIDER}();
                {DISCARD} = {TYPE}.Empty;
                {TYPE} result = ___globalentrypoint();

                if (argv.FirstOrDefault(arg => Regex.IsMatch(arg, ""{AutoItFunctions.MMF_CMDRARG}=.+"")) is string mmfoutarg)
                    try
                    {{
                        MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(mmfoutarg.Replace(""{AutoItFunctions.MMF_CMDRARG}="", """").Trim());
                    
                        using (MemoryMappedViewAccessor acc = mmf.CreateViewAccessor())
                        {{
                            byte[] ser = result.Serialize();

                            acc.Write(0, ser.Length);
                            acc.WriteArray(4, ser, 0, ser.Length);
                            acc.Flush();
                        }}
                    }}
                    catch
                    {{
                    }}
            }}
            catch (Exception e)
            {{
                string msg = """";

                while (e != null)
                {{
                    msg = $""[{{e.GetType()}}] {{e.Message}}:\n{{e.StackTrace}}\n{{msg}}"";
                    e = e.InnerException;
                }}

                Console.Error.Write($""~~~~~~~~~~ {options.Language["gen.fatal_error"].ToUpper()} ~~~~~~~~~~\n{{msg}}"");
".TrimEnd());
                    if (options.IncludeDebugSymbols)
                        sb.AppendLine($@"
                Console.Error.WriteLine(""~~~~~~~~~~ {options.Language["gen.dbg.info"].ToUpper()} ~~~~~~~~~~~"");
                
                if ({SYMBOL} > 0)
                {{
                    ({DEFCTXTYPE} ctx, string cont) = {DEBUGSYMBOL_MODULE}.GetSymbol({SYMBOL});

                    if (ctx.StartLine > 1)
                        ctx = new {DEFCTXTYPE}(ctx.FilePath, ctx.StartLine - 1, ctx.EndLine);

                    Console.Error.WriteLine($""{options.Language["gen.dbg.trace"]}\n{{ctx}}    {{cont.Replace(""\n"", ""\n    "")}}"");
                }}
                else
                    Console.Error.WriteLine(""[{options.Language["gen.dbg.no_info_found"]}]"");

".TrimEnd());
                    sb.AppendLine($@"
                Console.Error.WriteLine(""~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"");
            }}
        }}
".TrimEnd());
                    if (options.IncludeDebugSymbols)
                        sb.AppendLine($@"
        public static T {LAST_SYMBOL}<T>(long dbgsym, Func<T> func)
        {{
            {SYMBOL} = dbgsym;

            return func();
        }}

        public static void {LAST_SYMBOL}(long dbgsym, Action func)
        {{
            {SYMBOL} = dbgsym;

            func();
        }}
".TrimEnd());
                    sb.AppendLine($@"
/*{DISP_SKIP_E}*/

        private static {TYPE} ___globalentrypoint()
        {{
            AutoItMacroDictionary {MACROS} = new AutoItMacroDictionary({MACROS_GLOBAL}, s =>
            {{
                switch (s.ToLower())
                {{
                    case ""numparams"":
                    case ""numoptparams"": return 0;
                    case ""function"": return """";
                }}
                return null;
            }});
".TrimEnd());

                    foreach (AST_LOCAL_VARIABLE v in function.ExplicitLocalVariables)
                        sb.AppendLine($@"            {VARS}[""{v.Variable.Name}""] = {(v.InitExpression is EXPRESSION e ? tstr(e, v.Context ?? function.Context) : TYPE + ".Empty")};");

                    _print(function, 4);

                    sb.AppendLine($@"
            return {TYPE}.Empty;
        }}
".TrimEnd());
                }
                else
                {
                    sb.AppendLine($@"
        private static {TYPE} {FUNC_PREFIX}{fn}({string.Join(", ", paramters)})
        {{
            AutoItMacroDictionary {MACROS} = new AutoItMacroDictionary({MACROS_GLOBAL}, s =>
            {{
                switch (s.ToLower())
                {{
                    case ""numparams"":
                        return {paramters.Count()};
                    case ""numoptparams"":
                        return {function.Parameters.Count(x => x is AST_FUNCTION_PARAMETER_OPT)};
                    case ""function"":
                        return ""{fn}"";
                }}
                return null;
            }});

            {TYPE} inner()
            {{
".TrimEnd());
                    _print(function, 5);

                    sb.AppendLine($@"
                return {TYPE}.Empty;
            }}
            {VARS}.{nameof(AutoItVariableDictionary.InitLocalScope)}();");

                    foreach (VARIABLE v in function.Parameters.Select(x => x.Name).Concat(function.ExplicitLocalVariables.Select(x => x.Variable)))
                        sb.AppendLine($@"            {VARS}.{nameof(AutoItVariableDictionary.PushLocalVariable)}(""{v.Name}"");");

                    foreach (AST_FUNCTION_PARAMETER par in function.Parameters)
                        if (par is AST_FUNCTION_PARAMETER_OPT optpar)
                            sb.AppendLine($@"            {VARS}[""{par.Name.Name}""] = ({TYPE})({PARAM_PREFIX}{par.Name.Name} ?? {tstr(optpar.InitExpression, function.Context)});");
                        else
                            sb.AppendLine($@"            {VARS}[""{par.Name.Name}""] = {PARAM_PREFIX}{par.Name.Name};");

                    sb.AppendLine($"            {TYPE} result = inner();");

                    foreach (VARIABLE par in function.Parameters.Where(x => x.ByRef).Select(x => x.Name))
                        sb.AppendLine($@"            {PARAM_PREFIX}{par.Name}.{nameof(AutoItVariantTypeReference.WriteBack)}({VARS}[""{par.Name}""]);");

                    sb.AppendLine($@"            {VARS}.{nameof(AutoItVariableDictionary.DestroyLocalScope)}();
            return result;
        }}
".TrimEnd());
                }
            }

            foreach ((string lib, PInvoke.PINVOKE_SIGNATURE sig) in state.PInvokeSignatures)
            {
                string wname = AutoItFunctions.GeneratePInvokeWrapperName(lib, sig.Name);

                sb.AppendLine($@"
        [DllImport(""{lib}"", EntryPoint = ""{sig.Name}"")]
        private static extern {sig.ReturnType} {wname}({string.Join(", ", sig.Paramters.Select((p, i) => $"{p} _param{i}"))});
".TrimEnd());
            }

            sb.AppendLine($@"
        private static {TYPE} __critical(string s) => throw new InvalidProgramException(s ?? """");
    }}
}}");

            void _print(AST_STATEMENT e, int indent)
            {
                string tmpcsvar() => $"__tmp_cs_{++csvaridx:x8}";
                void println(string s, int i = -1) => sb.Append(new string(' ', 4 * ((i < 1 ? indent : i) - 1))).AppendLine(s);
                void print(AST_STATEMENT s) => _print(s, indent + 1);
                void printblock(AST_STATEMENT[] xs, string p = "", string s = "")
                {
                    if (allman)
                    {
                        if (p.Length > 0)
                            println(p);

                        println("{");
                    }
                    else
                        println(p.Length > 0 ? $"{p} {{" : "{");

                    foreach (AST_STATEMENT x in xs ?? new AST_STATEMENT[0])
                        print(x);

                    if (allman)
                    {
                        println("}");

                        if (s.Length > 0)
                            println(s);
                    }
                    else
                        println(s.Length > 0 ? $"}} {s}" : "}");
                }

                DefinitionContext context = e.Context;

                if (options.IncludeDebugSymbols && !(e is AST_EXPRESSION_STATEMENT))
                    println($"{SYMBOL} = {adddbgsymbol(e.Context)};");

                switch (e)
                {
                    case AST_IF_STATEMENT s:
                        printblock(s.If.Statements, $"if ({tstr(s.If.Condition, s.If.Context)})");

                        foreach (AST_CONDITIONAL_BLOCK elif in s.ElseIf ?? new AST_CONDITIONAL_BLOCK[0])
                            printblock(elif.Statements, $"else if ({tstr(elif.Condition, elif.Context)})");

                        if (s.OptionalElse is AST_STATEMENT[] b && b.Length > 0)
                            printblock(b, "else");

                        return;
                    case AST_WHILE_STATEMENT s:
                        printblock(s.WhileBlock.Statements, $"while ({tstr(s.WhileBlock.Condition, s.WhileBlock.Context)})");

                        return;
                    case AST_FOREACH s:
                        string tmpcs = tmpcsvar();
                        EXPRESSION collexpr = EXPRESSION.NewVariableExpression(s.CollectionVariable.Variable);

                        // println($"{tstr(collexpr, s.Context, false)} = {tstr(s.CollectionVariable.InitExpression, s.Context)};");
                        println($"foreach ({TYPE} {tmpcs} in {tstr(collexpr, s.Context)})");
                        println("{");
                        println($"{tstr(EXPRESSION.NewVariableExpression(s.ElementVariable.Variable), s.Context, false)} = {tmpcs};", indent + 1);

                        foreach (AST_STATEMENT st in s.Statements)
                            _print(st, indent + 1);

                        println("}");

                        return;
                    case AST_BREAK_STATEMENT s when s.Level == 1:
                        println("break;");

                        return;
                    case AST_LABEL s:
                        println(s.Name.Replace("<>", "") + ":;", 1);

                        return;
                    case AST_GOTO_STATEMENT s:
                        if (s.Label is null)
                        {
                            state.ReportKnownError("errors.generator.invalid_jump", s.Context);

                            println("// called `goto´ on non-existent label ----> possible error?");
                        }
                        else
                            println($"goto {s.Label.Name.Replace("<>", "")};");

                        return;
                    case AST_ASSIGNMENT_EXPRESSION_STATEMENT s:
                        if (s.Expression is ASSIGNMENT_EXPRESSION.ArrayAssignment arrassg)
                        {
                            DefinitionContext ctx = s.Context;
                            string varexpr = tstr(EXPRESSION.NewVariableExpression(arrassg.Item2), ctx, false);
                            string varexprdbg = tstr(EXPRESSION.NewVariableExpression(arrassg.Item2), ctx);

                            println($"{DISCARD} = {varexprdbg};");
                            println($"{DISCARD}[{string.Join(", ", arrassg.Item3.Select(x => tstr(x, ctx)))}] = {tstr(arrassg.Item4, ctx)};");
                            println($"{varexpr} = {DISCARD};");
                        }
                        else if (s.Expression is ASSIGNMENT_EXPRESSION.ReferenceAssignment)
                            println(tstr(EXPRESSION.NewAssignmentExpression(s.Expression), s.Context) + ';');
                        else
                        {
                            (string left, EXPRESSION expr) = ser.GetPartialAssigment(s.Expression, s.Context).ToValueTuple();

                            println($"{left ?? DISCARD} = {tstr(expr, s.Context)};");
                        }

                        return;
                    case AST_EXPRESSION_STATEMENT s:
                        println($"{DISCARD} = {tstr(s.Expression, s.Context)};");

                        return;
                    case AST_INLINE_CSHARP s:
                        println(s.Code);

                        return;
                    case AST_RETURN_STATEMENT s:
                        println($"return {tstr(s.Expression, s.Context)};");

                        return;
                    case AST_Λ_ASSIGNMENT_STATEMENT s:
                        string fname = s.Function.Trim();
                        string del;

                        if (state.ASTFunctions.ContainsKey(fname))
                            del = $"typeof({APPLICATION_MODULE}).GetMethod(nameof({FUNC_PREFIX}{fname}), BindingFlags.NonPublic | BindingFlags.Static)";
                        else
                            del = $"typeof({FUNC_MODULE}).GetMethod(\"{fname}\", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase)";

                        println($"{tstr(s.VariableExpression, s.Context, false)} = {TYPE}.{nameof(AutoItVariantType.NewDelegate)}({del});");

                        return;
                    case AST_REDIM_STATEMENT s:
                        {
                            string varexpr = tstr(EXPRESSION.NewVariableExpression(s.Variable), s.Context, false);
                            string dimexpr = string.Concat(s.DimensionExpressions.Select(dim => $", ({tstr(dim, s.Context)}).{nameof(AutoItVariantType.ToLong)}()"));

                            println($"{varexpr} = {TYPE}.{nameof(AutoItVariantType.RedimMatrix)}({varexpr}{dimexpr});");

                            return;
                        }
                    case AST_SCOPE s:
                        println("{");

                        if (s.UseExplicitLocalScoping && s.ExplicitLocalVariables.Count > 0)
                        {
                            println($"    {VARS}.{nameof(AutoItVariableDictionary.InitLocalScope)}();");

                            foreach (VARIABLE v in s.ExplicitLocalVariables.Select(x => x.Variable))
                                println($@"    {VARS}.{nameof(AutoItVariableDictionary.PushLocalVariable)}(""{v.Name}"");");
                        }

                        foreach (AST_LOCAL_VARIABLE v in s.ExplicitLocalVariables)
                            println($@"    {VARS}[""{v.Variable.Name}""] = {tstr(v.InitExpression ?? EXPRESSION.NewLiteral(LITERAL.NewString("")), v.Context ?? s.Context)};");

                        foreach (AST_STATEMENT ls in s.Statements ?? new AST_STATEMENT[0])
                            print(ls);

                        if (s.UseExplicitLocalScoping && s.ExplicitLocalVariables.Count > 0)
                            println($"    {VARS}.{nameof(AutoItVariableDictionary.DestroyLocalScope)}();");

                        println("}");

                        return;
                    default:
                        println($"// TODO: {e}"); // TODO

                        return;
                }
            }

            return Regex.Replace(sb.ToString(), @"\s*«\s*(?<msg>.*)\s*»\s*", m => $"__critical(\"{m.Get("msg").Trim()}\")");
        }

        public static string GenerateCSharpDebugProviderCode(InterpreterState state, InterpreterOptions options, Dictionary<long, (DefinitionContext, string)> debugsymbols)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($@"
/*
    Autogenerated       {DateTime.Now:ddd yyyy-MM-dd, HH:mm:ss.ffffff}
    Using the command   {options.RawCommandLine}
    Error(s)/Warning(s) {state.Errors.Length}:
{string.Concat(state.Errors.Select(err => $"        {err}\r\n"))}*/

using System.Collections.Generic;
using System.Text;
using System.Linq;
using System;

using {nameof(AutoItCoreLibrary)};

#pragma warning disable CS0162
#pragma warning disable CS0164
#pragma warning disable CS1522

namespace {NAMESPACE}
{{
    using {DEFCTXTYPE} = {nameof(DefinitionContext)};
    
    public static class {DEBUGSYMBOL_MODULE}
    {{
        public static Dictionary<long, ({DEFCTXTYPE} ctx, string b64)> Symbols {{ get; }} = new Dictionary<long, ({DEFCTXTYPE}, string)>
        {{
".Trim());

            foreach (long l in debugsymbols.Keys)
            {
                (DefinitionContext Context, string LineContent) = debugsymbols[l];
                string path = Context.FilePath is FileInfo nfo ? Path.GetFullPath(nfo.FullName).Replace('\\', '/') : "";

                sb.Append($@"
            [0x{l:x16}L] = (new {DEFCTXTYPE}(""{path}"", {Context.StartLine}, {(Context.EndLine is int i ? i.ToString() : "null")}), ""{Convert.ToBase64String(Encoding.Default.GetBytes(LineContent))}""),
".TrimEnd());
            }

            sb.AppendLine($@"
        }};

        public static ({DEFCTXTYPE} ctx, string line) GetSymbol(long l)
        {{
            if (Symbols.ContainsKey(l))
            {{
                ({DEFCTXTYPE} ctx, string b64) = Symbols[l];
                
                return (ctx, Encoding.Default.GetString(Convert.FromBase64String(b64)));
            }}
            else
                return (default, """");
        }}
    }}
}}

#pragma warning restore CS0162
#pragma warning restore CS0164
#pragma warning restore CS1522
");


            return sb.ToString();
        }

        public static string GenerateCSharpAssemblyInfo(InterpreterState state) => $@"
// Autogenerated  {DateTime.Now:ddd yyyy-MM-dd, HH:mm:ss.ffffff}

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;


[assembly: AllowReversePInvokeCalls]
[assembly: AssemblyTitle(""{state.CompileInfo.AssemblyProductName}"")]
[assembly: AssemblyDescription(""{state.CompileInfo.AssemblyFileDescription}"")]
[assembly: AssemblyConfiguration("""")]
[assembly: AssemblyCompany(""{state.CompileInfo.AssemblyCompanyName}"")]
[assembly: AssemblyProduct(""{state.CompileInfo.AssemblyProductName}"")]
[assembly: AssemblyCopyright(""{state.CompileInfo.AssemblyCopyright}"")]
[assembly: AssemblyTrademark(""{state.CompileInfo.AssemblyTrademarks}"")]
[assembly: AssemblyCulture(""{state.CompileInfo.AssemblyComment}"")]
[assembly: ComVisible(true)]
[assembly: Guid(""{Guid.NewGuid():D}"")]
[assembly: AssemblyVersion(""{state.CompileInfo.AssemblyProductVersion}"")]
[assembly: AssemblyFileVersion(""{state.CompileInfo.AssemblyFileVersion}"")]
".TrimStart();

#pragma warning restore IDE1006
#pragma warning restore RSC1057
#pragma warning restore RCS1197

        public static string GetAssemblyName(InterpreterState state, string projname)
        {
            string asmname = state.CompileInfo.FileName?.Trim('.') ?? projname;

            if (asmname.Contains('.'))
                asmname = asmname.Remove(asmname.IndexOf('.')).Trim();

            if (asmname.Length == 0)
                asmname = projname;

            return asmname.Replace(' ', '_');
        }

        public static int GenerateDotnetProject(ref DirectoryInfo dir, string name, out string log)
        {
            DirectoryInfo ndir = new DirectoryInfo($"{dir.FullName}/{name}");

            if (ndir.Exists)
                try
                {
                    ndir.Delete(true);
                }
                catch
                {
                    ndir.Delete(true); // second time's a chrarm?
                }

            using (Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = dir.FullName,
                    Arguments = $"new console -n \"{name}\" -lang \"C#\" --force",
                    FileName = "dotnet",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                }
            })
            {
                proc.Start();
                proc.WaitForExit();

                using (StreamReader cout = proc.StandardOutput)
                using (StreamReader cerr = proc.StandardError)
                    log = cout.ReadToEnd() + '\n' + cerr.ReadToEnd();

                dir = dir.CreateSubdirectory(name);

                return proc.ExitCode;
            }
        }

        public static void GenerateAppConfig(DirectoryInfo dir)
        {
            string config = $@"
<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <runtime>
        <legacyCorruptedStateExceptionsPolicy enabled=""true""/>   
    </runtime>
</configuration>
".Trim();

            File.WriteAllText(Path.Combine(dir.FullName, "app.config"), config);
        }

        public static void EditDotnetProject(InterpreterState state, InterpreterOptions options, TargetSystem target, DirectoryInfo dir, FileInfo[] dependencies, string name, string keypath)
        {
            if (File.Exists($"{dir.FullName}/Program.cs"))
                File.Delete($"{dir.FullName}/Program.cs");

            string bstr(bool v) => v.ToString().ToLower();
            string dllpath = $"{dir.FullName}/../{nameof(Resources.autoitcorlib)}.dll";
            string respath = $"{dir.FullName}/resources.resx";
            string depsstr = string.Concat(dependencies.Concat(new[] { new FileInfo(dllpath) }).Select(x => $@"
    <ItemGroup>
        <EmbeddedResource Include=""{x.FullName}""/>
    </ItemGroup>"));

            File.WriteAllBytes(dllpath, Resources.autoitcorlib);
            File.WriteAllText($"{dir.FullName}/{name}.csproj", $@"
<Project Sdk=""Microsoft.NET.Sdk"">
    <!-- Autogen: {DateTime.Now:yyyy.MM.dd, HH:mm:ss.ffffff} -->
    <PropertyGroup>
        <OutputType>exe</OutputType>
        <AssemblyName>{GetAssemblyName(state, name)}</AssemblyName>
        <ApplicationIcon>{state.CompileInfo.IconPath ?? ""}</ApplicationIcon>
        <StartupObject>{NAMESPACE}.{APPLICATION_MODULE}</StartupObject>
        <TargetFramework>netcoreapp2.1</TargetFramework>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        <Optimize>{bstr(!options.IncludeDebugSymbols)}</Optimize>
        <LangVersion>latest</LangVersion>
        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
        <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
        <OutputPath>bin</OutputPath>
        <SelfContained>true</SelfContained>
        <RuntimeIdentifier>{target.Identifier}</RuntimeIdentifier>
        <Prefer32Bit>{(!target.Is64Bit).ToString().ToLower()}</Prefer32Bit>
        <DebugType>{(options.IncludeDebugSymbols ? "Full" : "None")}</DebugType>
        <DebugSymbols>{bstr(options.IncludeDebugSymbols)}</DebugSymbols>
        <CopyOutputSymbolsToPublishDirectory>{bstr(options.IncludeDebugSymbols)}</CopyOutputSymbolsToPublishDirectory>
        {(keypath is null ? "<!--" : "")}
        <SignAssembly>true</SignAssembly>
        <AssemblyOriginatorKeyFile>{keypath}</AssemblyOriginatorKeyFile>
        <DelaySign>false</DelaySign>
        {(keypath is null ? "-->" : "")}
    </PropertyGroup>
    <ItemGroup>
        <Reference Include=""{nameof(Resources.autoitcorlib)}"">
            <HintPath>{dllpath}</HintPath>
        </Reference>
    </ItemGroup>
    <!-- {depsstr} -->
    <ItemGroup>
        <Compile Include=""debugsymbols.cs""/>
        <Compile Include=""{name}.cs""/>
        <None Include=""app.config""/>
    </ItemGroup>
</Project>
");
        }

        public static int BuildDotnetProject(DirectoryInfo dir)
        {
            using (Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = dir.FullName,
                    Arguments = "build",
                    FileName = "dotnet",
                }
            })
            {
                proc.Start();
                proc.WaitForExit();

                return proc.ExitCode;
            }
        }

        public static int PublishDotnetProject(DirectoryInfo dir)
        {
            using (Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = dir.FullName,
                    Arguments = "publish -c Release --force --self-contained -v m",
                    FileName = "dotnet",
                }
            })
            {
                proc.Start();
                proc.WaitForExit();

                return proc.ExitCode;
            }
        }

        public static int RunApplication(FileInfo path, bool debug)
        {
            if (path is null)
                return -1;
            else
                using (Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WorkingDirectory = path.Directory.FullName,
                        Arguments = $"\"{path.FullName}\"{(debug ? ' ' + AutoItFunctions.DBG_CMDARG : "")}",
                        FileName = "dotnet",
                    }
                })
                {
                    proc.Start();
                    proc.WaitForExit();

                    Console.WriteLine();

                    return proc.ExitCode;
                }
        }
    }

    public sealed class TargetSystem
    {
        public Compatibility Compatibility { get; }
        public Architecture? TargetArchitecture { get; }
        public bool Is64Bit => TargetArchitecture is Architecture a ? a == Architecture.Arm64 || a == Architecture.X64 : false;
        public string Identifier => Compatibility.ToString().Replace('_', '.') + (TargetArchitecture is null ? "" : '-' + TargetArchitecture.ToString().ToLower());


        public TargetSystem(Compatibility comp, Architecture? arch) => (Compatibility, TargetArchitecture) = (comp, arch);
    }
}
