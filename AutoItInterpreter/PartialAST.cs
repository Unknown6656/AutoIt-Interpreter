using System.Collections.Generic;
using System.Text;
using System;

using AutoItExpressionParser;

namespace AutoItInterpreter.PartialAST
{
    using static ExpressionAST;


    public sealed class AST_FUNCTION
    {
        public AST_STATEMENT[] Statements { set; get; }
    }

    public abstract class AST_STATEMENT
    {
    }

    public sealed class AST_ASSIGNMENT_STATEMNT
        : AST_STATEMENT
    {
        public ASSIGNMENT_EXPRESSION Expression { set; get; }
    }

    public sealed class AST_IF_STATEMENT
        : AST_STATEMENT
    {
        public AST_CONDITIONAL_BLOCK If { set; get; }
        public AST_CONDITIONAL_BLOCK[] ElseIf { set; get; }
        public AST_STATEMENT[] OptionalElse { set; get; }
    }

    public class AST_CONDITIONAL_BLOCK
    {
        public EXPRESSION Condition { set; get; }
        public AST_STATEMENT[] Statements { set; get; }
    }

    public sealed class AST_WHILE_STATEMENT
        : AST_STATEMENT
    {
        public AST_CONDITIONAL_BLOCK WhileBlock { set; get; }


        public static implicit operator AST_WHILE_STATEMENT(AST_CONDITIONAL_BLOCK b) => new AST_WHILE_STATEMENT { WhileBlock = b };
    }

    public sealed class AST_DO_STATEMENT
        : AST_STATEMENT
    {
        public AST_CONDITIONAL_BLOCK DoBlock { set; get; }


        public static implicit operator AST_DO_STATEMENT(AST_CONDITIONAL_BLOCK b) => new AST_DO_STATEMENT { DoBlock = b };
    }



    // TODO
}
