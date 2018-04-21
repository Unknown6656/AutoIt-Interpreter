using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public class ExpressionTests
    {
        [TestMethod]
        public void Test_01()
        {
            string str = "$ulk += 5 << ($lel == $lel) * ($jej = (99 << 3) + (8 / (7 + 2) - 6)) + \"lel\" ^ (14 >>> 3 + 88) nor ($lulz <> $lulz)";
            var parser = new AutoItExpressionParser.ExpressionParser();

            parser.Initialize();

            var expr = (parser.Parse(str)[0] as AutoItExpressionParser.ExpressionAST.MULTI_EXPRESSION.SingleValue).Item;
            var sexpr = AutoItExpressionParser.Refactorings.ProcessConstants(expr);

            string e1 = AutoItExpressionParser.ExpressionAST.Print(expr);
            string e2 = AutoItExpressionParser.ExpressionAST.Print(sexpr);

            // TODO
        }
    }
}
