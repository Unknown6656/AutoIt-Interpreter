using System.Runtime.CompilerServices;

using Piglet.Parser.Configuration.Generic;

using Unknown6656.AutoIt3.Parser.ExpressionParser;
using Unknown6656.AutoIt3.Parser.DLLStructParser;
using System.Threading.Tasks;

namespace Unknown6656.AutoIt3.Runtime
{
    using exp_parser = ParserConstructor<Parser.ExpressionParser.AST.PARSABLE_EXPRESSION>.ParserWrapper;
    using dll_parser = ParserConstructor<Parser.DLLStructParser.AST.SIGNATURE>.ParserWrapper;


    public sealed class ParserProvider
    {
        public Interpreter Interpreter { get; }
        public exp_parser ExpressionParser { get; private set; }
        public exp_parser ParameterParser { get; private set; }
        public exp_parser MultiDeclarationParser { get; private set; }
        public dll_parser DLLStructParser { get; private set; }

#nullable disable
        internal ParserProvider(Interpreter interpreter)
        {
            Interpreter = interpreter;
            Interpreter.Telemetry.Measure(TelemetryCategory.ParserInitialization, () => Parallel.Invoke(
                () => ParameterParser = new ExpressionParser(ParserMode.FunctionParameters).CreateParser(),
                () => ExpressionParser = new ExpressionParser(ParserMode.ArbitraryExpression).CreateParser(),
                () => MultiDeclarationParser = new ExpressionParser(ParserMode.MultiDeclaration).CreateParser(),
                () => DLLStructParser = new DLLStructParser().CreateParser()
            ));
        }
#nullable enable

        /// <summary>
        /// This method does nothing at all. When called, it implicitly invokes the static constructor (if the cctor has not been invoked before).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Initialize()
        {
        }
    }
}
