using System.Runtime.CompilerServices;

using Piglet.Parser.Configuration.Generic;

using Unknown6656.AutoIt3.ExpressionParser;

namespace Unknown6656.AutoIt3.Runtime
{
    using exprparser = ExpressionParser.ExpressionParser;
    using wrapper = ParserConstructor<AST.PARSABLE_EXPRESSION>.ParserWrapper;


    public sealed class ParserProvider
    {
        public wrapper ExpressionParser { get; }
        public wrapper ParameterParser { get; }
        public wrapper MultiDeclarationParser { get; }
        public Interpreter Interpreter { get; }


        internal ParserProvider(Interpreter interpreter)
        {
            Interpreter = interpreter;

            wrapper create(ParserMode mode) => interpreter.Telemetry.Measure(TelemetryCategory.ParserInitialization, new exprparser(mode).CreateParser);

            ParameterParser = create(ParserMode.FunctionParameters);
            ExpressionParser = create(ParserMode.ArbitraryExpression);
            MultiDeclarationParser = create(ParserMode.MultiDeclaration);
        }

        /// <summary>
        /// This method does nothing at all. When called, it implicitly invokes the static constructor (if the cctor has not been invoked before).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Initialize()
        {
        }
    }
}
