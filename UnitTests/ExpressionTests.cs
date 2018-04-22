using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public sealed class ExpressionTests
        : TestCommons
    {
        void ___()
        {
            var ex1 = ParseExpression("$ulk += 5 << ($lel == $lel) * ($jej = (99 << 3) + (8 / (7 + 2) - 6)) + \"lel\" ^ (14 >>> 3 + 88) nor ($lulz <> $lulz)");
        }

        [TestMethod]
        public void Test_01() => AssertValidExpression("func($p1, @macro, false and ($test < 5)) + 88");

        [TestMethod]
        public void Test_02() => AssertInvalidExpression("func() += 1");

        [TestMethod]
        public void Test_03() => AssertEqualExpressions("(1 * 2) + 3", "1 * 2 + 3");

        [TestMethod, Skip]
        public void Test_04() => AssertEqualProcessedExpressions("(1 * 2) + 3", "5");
    }
}
