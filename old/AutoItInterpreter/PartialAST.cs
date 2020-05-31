using System.Collections.Generic;
using System.Linq;
using System;

using AutoItExpressionParser;
using AutoItCoreLibrary;

namespace AutoItInterpreter.PartialAST
{
    using static ExpressionAST;


    public interface AST_BREAKABLE_SKIPPABLE
    {
        AST_LABEL ContinueLabel { set; get; }
        AST_LABEL ExitLabel { set; get; }
    }

    public sealed class AST_LOCAL_VARIABLE
    {
        public bool Constant { set; get; }
        public VARIABLE Variable { set; get; }
        public EXPRESSION InitExpression { set; get; }
        public DefinitionContext? Context { set; get; }


        public override string ToString() => $"{(Constant ? "const " : "")}{Variable}{(InitExpression is null ? "" : " = " + InitExpression.Print())}";
    }

    public abstract class AST_STATEMENT
    {
        public abstract bool IsEmpty { get; }
        public DefinitionContext Context { set; get; }
    }

    public class AST_SCOPE
        : AST_STATEMENT
    {
        private AST_STATEMENT[] _st = new AST_STATEMENT[0];

        public List<AST_LOCAL_VARIABLE> ExplicitLocalVariables { get; } = new List<AST_LOCAL_VARIABLE>();
        public override bool IsEmpty => (Statements?.Length ?? 0) == 0;
        public bool UseExplicitLocalScoping { set; get; }
        public AST_STATEMENT[] Statements
        {
            set
            {
                if (value is AST_STATEMENT[] arr)
                    _st = arr;
            }
            get => _st;
        }

        public AST_LOCAL_VARIABLE this[string name] => ExplicitLocalVariables.Find(lv => lv.Variable.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
    }

    public sealed class AST_FUNCTION
        : AST_SCOPE
    {
        public AST_FUNCTION_PARAMETER[] Parameters { set; get; }
        public new bool UseExplicitLocalScoping { get; }
        public string Name { set; get; }


        public AST_FUNCTION()
        {
            UseExplicitLocalScoping = true;
            base.UseExplicitLocalScoping = true;
        }
    }

    public class AST_FUNCTION_PARAMETER
    {
        public VARIABLE Name { get; }
        public bool ByRef { get; }
        public bool Const { get; }


        public AST_FUNCTION_PARAMETER(VARIABLE var, bool bref, bool cnst)
        {
            Name = var;
            ByRef = bref;
            Const = cnst;
        }

        public override string ToString() => $"{(Const ? "const " : "")}{(ByRef ? "byref " : "")}{Name}";
    }

    public sealed class AST_FUNCTION_PARAMETER_OPT
        : AST_FUNCTION_PARAMETER
    {
        public EXPRESSION InitExpression { get; }


        public AST_FUNCTION_PARAMETER_OPT(VARIABLE var, EXPRESSION initexpr)
            : base(var, false, false) => InitExpression = initexpr;

        public override string ToString() => $"{base.ToString()} = {InitExpression.Print()}";
    }

    public sealed class AST_ASSIGNMENT_STATEMNT
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
        public ASSIGNMENT_EXPRESSION Expression { set; get; }
    }

    public sealed class AST_IF_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => If.IsEmpty && (ElseIf?.All(c => c.IsEmpty) ?? true) && (OptionalElse?.Length ?? 0) == 0;
        public AST_CONDITIONAL_BLOCK If { set; get; }
        public AST_CONDITIONAL_BLOCK[] ElseIf { set; get; }
        public AST_STATEMENT[] OptionalElse { set; get; }
    }

    public class AST_CONDITIONAL_BLOCK
        : AST_SCOPE
    {
        public override bool IsEmpty => base.IsEmpty && Analyzer.IsStatic(Condition);
        public EXPRESSION Condition { set; get; }


        public AST_CONDITIONAL_BLOCK() => UseExplicitLocalScoping = true;

        public override string ToString() => $"condition ({Condition.Print()}) {{ ... }}";
    }

    public sealed class AST_WHILE_STATEMENT
        : AST_STATEMENT
        , AST_BREAKABLE_SKIPPABLE
    {
        public override bool IsEmpty => WhileBlock.IsEmpty;
        public AST_CONDITIONAL_BLOCK WhileBlock { set; get; }
        public AST_LABEL ContinueLabel { set; get; }
        public AST_LABEL ExitLabel { set; get; }


        public static implicit operator AST_WHILE_STATEMENT(AST_CONDITIONAL_BLOCK b) => new AST_WHILE_STATEMENT { WhileBlock = b };
    }

    public sealed class AST_WITH_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => (WithLines?.Length ?? 0) == 0;
        public EXPRESSION WithExpression { set; get; }
        public AST_WITH_LINE[] WithLines { set; get; }
    }

    public sealed class AST_WITH_LINE
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
        public dynamic Expression { set; get; }

        // TODO
    }

    public sealed class AST_LABEL
        : AST_STATEMENT
    {
        private static long _tmp;

        public override bool IsEmpty => false;
        public string Name { set; get; }

        public static AST_LABEL NewLabel => new AST_LABEL { Name = $"__lb<>{++_tmp:x4}" };
    }

    public sealed class AST_GOTO_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
        public AST_LABEL Label { set; get; }
    }

    public sealed class AST_SWITCH_TRUE_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => (Cases?.Length ?? 0) == 0;
        public AST_SWITCH_CASE[] Cases { set; get; }
    }

    public abstract class AST_SWITCH_CASE
        : AST_SCOPE
    {
    }

    public sealed class AST_SWITCH_CASE_EXPRESSION
        : AST_SWITCH_CASE
    {
        public MULTI_EXPRESSION[] Expressions { set; get; }
    }

    public sealed class AST_SWITCH_CASE_ELSE
        : AST_SWITCH_CASE
    {
    }

    public abstract class AST_EXPR_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
    }

    public sealed class AST_EXPRESSION_STATEMENT
        : AST_EXPR_STATEMENT
    {
        public EXPRESSION Expression { set; get; }
    }

    public sealed class AST_ASSIGNMENT_EXPRESSION_STATEMENT
        : AST_EXPR_STATEMENT
    {
        public ASSIGNMENT_EXPRESSION Expression { set; get; }
    }

    public sealed class AST_CONTINUECASE_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
    }

    public sealed class AST_RETURN_STATEMENT
        : AST_STATEMENT
    {
        public EXPRESSION Expression { set; get; }

        public override bool IsEmpty => false;
    }

    public sealed class AST_BREAK_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
        public uint Level { set; get; }
    }

    public sealed class AST_CONTINUE_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
        public uint Level { set; get; }
    }

    public sealed class AST_REDIM_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
        public VARIABLE Variable { set; get; }
        public EXPRESSION[] DimensionExpressions { set; get; }
    }

    public sealed class AST_DECLARATION_STATEMENT
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
        public VARIABLE Variable { set; get; }
        public EXPRESSION InitExpression { set; get; }
        public EXPRESSION[] DimensionExpressions { set; get; }
    }

    public sealed class AST_INLINE_CSHARP
        : AST_STATEMENT
    {
        public override bool IsEmpty => false;
        public string Code { set; get; }
    }

    public sealed class AST_Λ_ASSIGNMENT_STATEMENT
        : AST_STATEMENT
    {
        public EXPRESSION VariableExpression { get; set; }
        public string Function { get; set; }
        public override bool IsEmpty => false;
    }

    public sealed class AST_FOREACH
        : AST_SCOPE
        , AST_BREAKABLE_SKIPPABLE
    {
        public override bool IsEmpty => Analyzer.IsStatic(CollectionVariable.InitExpression) && base.IsEmpty;
        public AST_LOCAL_VARIABLE CollectionVariable { set; get; }
        public AST_LOCAL_VARIABLE ElementVariable { set; get; }
        public AST_LABEL ContinueLabel { set; get; }
        public AST_LABEL ExitLabel { set; get; }
    }
}
