using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public sealed class ExpressionTests
        : TestCommons
    {
        [TestMethod]
        public void Test_01() => AssertValidExpression("func($p1, @macro, false and ($test < 5)) + 88", false);

        [TestMethod]
        public void Test_02() => AssertInvalidExpression("func() += 1", true);

        [TestMethod]
        public void Test_03() => AssertEqualExpressions("(1 * 2) + 3", "1 * 2 + 3", false);

        [TestMethod]
        public void Test_04() => AssertEqualProcessedExpressions("(1 * 2) + 3", "5", false);

        [TestMethod]
        public void Test_05() => AssertInvalidExpression("0x80000000000000000", false);

        [TestMethod]
        public void Test_06() => AssertValidExpression("-0x7ffffffffffffff", false);

        [TestMethod]
        public void Test_07() => AssertEqualExpressions(
            "$ulk += 5 << ($lel == $lel) * ($jej = (99 << 3) + (8 / (7 + 2) - 6)) + \"lel\" ^ (14 >>> 3 + 88) nor ($lulz <> $lulz)",
            "$ulk += 0",
            true
        );
    }
}
