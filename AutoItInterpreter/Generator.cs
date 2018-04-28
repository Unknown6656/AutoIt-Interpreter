using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AutoItInterpreter.PartialAST;
using AutoItExpressionParser;

namespace AutoItInterpreter
{
    using static InterpreterConstants;
    using static ExpressionAST;


    public static class Generator
    {
#pragma warning disable RCS1197
        public static string Generate(InterpreterState state, InterpreterSettings settings)
        {
            const string TYPE = nameof(AutoItVariantType);
            const string FUNC_MODULE = "__functions";
            const string FUNC_PREFIX = "__userfunc_";
            const string MACROS = "__macros";
            const string VARS = "__vars";

            string[] glob = { GLOBAL_FUNC_NAME };
            StringBuilder sb = new StringBuilder();
            string funcresv(string func)
            {
                func = func.ToLower();

                if (state.ASTFunctions.ContainsKey(func))
                    return null;
                else
                    return FUNC_MODULE + '.' + func;
            }
            Serializer ser = new Serializer(new SerializerSettings(MACROS, VARS, TYPE, FUNC_PREFIX, funcresv));
            bool allman = settings.IndentationStyle == IndentationStyle.AllmanStyle;


            foreach (string fn in state.ASTFunctions.Keys.Except(glob).OrderByDescending(fn => fn).Concat(glob).Reverse())
            {
                sb.AppendLine($@"
    public static {TYPE} {FUNC_PREFIX}{fn}( /* TODO */ )
    {{
".TrimEnd());

                _print(state.ASTFunctions[fn], 3);

                sb.AppendLine($@"
        return {TYPE}.Default;
    }}
".TrimEnd());
            }

            void _print(AST_STATEMENT e, int indent)
            {
                string tstr(EXPRESSION ex) => ex is null ? "«« error »»" : ser.Serialize(ex);
                void println(string s, int i = -1) => sb.Append(new string(' ', 4 * ((i < 1 ? indent : i) - 1))).AppendLine(s);
                void print(AST_STATEMENT s) => _print(s, indent + 1);
                void printblock(AST_STATEMENT[] xs, string p = "", string s = "")
                {
                    xs = xs ?? new AST_STATEMENT[0];

                    if (xs.Length > 1 || (p + s).Length == 0 || !allman)
                    {
                        if (allman)
                        {
                            if (p.Length > 0)
                                println(p);

                            println("{");
                        }
                        else
                            println(p.Length > 0 ? $"{p} {{" : "{");

                        foreach (AST_STATEMENT x in xs)
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
                    else
                    {
                        if (p.Length > 0)
                            println(p);

                        if (xs.Length > 0)
                            print(xs[0]);
                        else
                            println(";", indent + 1);

                        if (s.Length > 0)
                            println(s);
                    }
                }

                switch (e)
                {
                    case AST_IF_STATEMENT s:
                        printblock(s.If.Statements, $"if ({tstr(s.If.Condition)})");

                        foreach (AST_CONDITIONAL_BLOCK elif in s.ElseIf ?? new AST_CONDITIONAL_BLOCK[0])
                            printblock(elif.Statements, $"else if ({tstr(elif.Condition)})");

                        if (s.OptionalElse is AST_STATEMENT[] b)
                            printblock(b, "else");

                        return;
                    case AST_WHILE_STATEMENT s:
                        printblock(s.WhileBlock.Statements, $"while ({tstr(s.WhileBlock.Condition)})");

                        return;
                    case AST_SCOPE s:
                        println("{");

                        foreach (AST_LOCAL_VARIABLE ls in s.ExplicitLocalVariables)
                            if (ls.InitExpression is null)
                                println($"{ls.Variable};", indent + 1);
                            else
                                println($"{ls.Variable} = {tstr(ls.InitExpression)};", indent + 1);

                        foreach (AST_STATEMENT ls in s.Statements ?? new AST_STATEMENT[0])
                            print(ls);

                        println("}");

                        return;
                    case AST_BREAK_STATEMENT s when s.Level == 1:
                        println("break;");

                        return;
                    case AST_LABEL s:
                        println(s.Name + ':', 1);

                        return;
                    case AST_GOTO_STATEMENT s:
                        println($"goto {s.Label.Name};");

                        return;
                    case AST_ASSIGNMENT_EXPRESSION_STATEMENT s:
                        println(tstr(EXPRESSION.NewAssignmentExpression(s.Expression)) + ';');

                        return;
                    case AST_EXPRESSION_STATEMENT s:
                        println(tstr(s.Expression) + ';');

                        return;
                    case AST_INLINE_CSHARP s:
                        println(s.Code);

                        return;
                    default:
                        println($"// TODO: {e}"); // TODO

                        return;
                }
            }

            return sb.ToString();
        }
#pragma warning restore RCS1197
    }
}
