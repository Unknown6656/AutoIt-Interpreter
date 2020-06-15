using Piglet.Parser.Configuration.Generic;

using Unknown6656.AutoIt3.ExpressionParser;

namespace Unknown6656.AutoIt3.Runtime
{
    using exprparser = ExpressionParser.ExpressionParser;


    public static class ParserProvider
    {
        public static ParserConstructor<AST.PARSABLE_EXPRESSION>.ParserWrapper ExpressionParser { get; }
        public static ParserConstructor<AST.PARSABLE_EXPRESSION>.ParserWrapper ParameterParser { get; }
        public static ParserConstructor<AST.PARSABLE_EXPRESSION>.ParserWrapper MultiDeclarationParser { get; }


        static ParserProvider()
        {
            ParameterParser = new exprparser(ParserMode.FunctionParameters).CreateParser();
            ExpressionParser = new exprparser(ParserMode.ArbitraryExpression).CreateParser();
            MultiDeclarationParser = new exprparser(ParserMode.MultiDeclaration).CreateParser();
        }
    }
}
