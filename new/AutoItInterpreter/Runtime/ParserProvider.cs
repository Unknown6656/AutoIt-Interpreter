using Piglet.Parser.Configuration.Generic;

using Unknown6656.AutoIt3.ExpressionParser;

namespace Unknown6656.AutoIt3.Runtime
{
    using ExpressionParser = ExpressionParser.ExpressionParser;


    public static class ParserProvider
    {
        public static ParserConstructor<AST.EXPRESSION>.ParserWrapper ExprParser { get; }


        static ParserProvider()
        {
            ExpressionParser generator = new ExpressionParser();

            ExprParser = generator.CreateParser();

            // TODO
        }
    }
}
