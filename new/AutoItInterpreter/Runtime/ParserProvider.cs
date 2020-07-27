using System.Runtime.CompilerServices;

using Piglet.Parser.Configuration.Generic;

using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.AutoIt3.Parser.DLLStructParser;

namespace Unknown6656.AutoIt3.Runtime
{
    using exp_parser = ParserConstructor<Parser.ExpressionParser.AST.PARSABLE_EXPRESSION>.ParserWrapper;
    using dll_parser = ParserConstructor<Parser.DLLStructParser.AST.SIGNATURE>.ParserWrapper;


    public sealed class ParserProvider
    {
        public exp_parser ExpressionParser { get; }
        public exp_parser ParameterParser { get; }
        public exp_parser MultiDeclarationParser { get; }
        public dll_parser DLLStructParser { get; }
        public Interpreter Interpreter { get; }


        internal ParserProvider(Interpreter interpreter)
        {
            Interpreter = interpreter;

            exp_parser create(ParserMode mode) => interpreter.Telemetry.Measure(TelemetryCategory.ParserInitialization, new ExpressionParser(mode).CreateParser);

            ParameterParser = create(ParserMode.FunctionParameters);
            ExpressionParser = create(ParserMode.ArbitraryExpression);
            MultiDeclarationParser = create(ParserMode.MultiDeclaration);
            DLLStructParser = interpreter.Telemetry.Measure(TelemetryCategory.ParserInitialization, new DLLStructParser().CreateParser);
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
