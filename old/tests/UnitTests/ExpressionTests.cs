using Microsoft.VisualStudio.TestTools.UnitTesting;

using AutoItExpressionParser;

namespace UnitTests
{
    using static ExpressionAST;


    [TestClass, TestingPriority(1000)]
    public sealed class ExpressionTests
        : TestCommons
    {
        public ExpressionTests()
        {
            try
            {
                _parser.Initialize();
                _aparser.Initialize();
            }
            catch
            {
            }
        }

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

        [TestMethod]
        public void Test_08() => AssertValidExpression("foo($kek)", false);

        [TestMethod]
        public void Test_09() => AssertValidExpression("$lel[@macro]", false);

        [TestMethod]
        public void Test_10() => AssertValidExpression("$i[0][1][2]", false);

        [TestMethod]
        public void Test_11() => AssertValidExpression("$lambda(3, 1, 5)", false);

        [TestMethod]
        public void Test_12() => AssertValidExpression("foo($jej[0])", false);

        [TestMethod]
        public void Test_13() => AssertValidExpression("$arr[$i]", false);

        [TestMethod]
        public void Test_14() => AssertValidExpression("$f[$g]($h, i($j[99]))", false);

        [TestMethod]
        public void Test_15() => AssertValidExpression("$a @ $b @| $c", false);

        [TestMethod]
        public void Test_16() => AssertValidExpression("9 \\ $i.obj.val(44)", false);

        [TestMethod]
        public void Test_17() => AssertInvalidExpression("((($value()))", false);

        [TestMethod]
        public void Test_18() => AssertInvalidExpression("", false);

        [TestMethod]
        public void Test_19() => AssertValidExpression("$var = \"string\"#", true);

        [TestMethod]
        public void Test_20() => AssertInvalidExpression("$var# = 9", true);

        [TestMethod]
        public void Test_21() => AssertInvalidExpression("($a @ $b) <<<= $c", true);

        [TestMethod]
        public void Test_22()
        {
            var or = AssertIs<EXPRESSION, EXPRESSION.BinaryExpression>(ParseExpression("$a xor $b nand $c or $d", false));
            var xor = AssertIs<EXPRESSION, EXPRESSION.BinaryExpression>(or.Item2);
            var nand = AssertIs<EXPRESSION, EXPRESSION.BinaryExpression>(xor.Item3);

            Assert.IsTrue(or.Item1.IsOr);
            Assert.IsTrue(xor.Item1.IsXor);
            Assert.IsTrue(nand.Item1.IsNand);
            Assert.AreEqual("a", AssertIs<EXPRESSION, EXPRESSION.VariableExpression>(xor.Item2).Item.Name);
            Assert.AreEqual("b", AssertIs<EXPRESSION, EXPRESSION.VariableExpression>(nand.Item2).Item.Name);
            Assert.AreEqual("c", AssertIs<EXPRESSION, EXPRESSION.VariableExpression>(nand.Item3).Item.Name);
            Assert.AreEqual("d", AssertIs<EXPRESSION, EXPRESSION.VariableExpression>(or.Item3).Item.Name);
        }

        [TestMethod]
        public void Test_23() => AssertValidExpression("$a = $b", true);

        [TestMethod]
        public void Test_24() => AssertValidExpression("$a = $b", false);

        [TestMethod]
        public void Test_25() => AssertInvalidExpression("$a += $b", false);

        [TestMethod]
        public void Test_26() => AssertValidExpression("$a = $b = $c == $d <> $e", false);

        [TestMethod]
        public void Test_27() => AssertEqualExpressions("$a ? $b : $c ? $d : $e", "$a ? $b : ($c ? $d : $e)", false);

        [TestMethod]
        public void Test_28() => AssertEqualExpressions("$a impl $b impl $c", "!$a or (!$b or $c)", false);

        [TestMethod]
        public void Test_29() => AssertEqualExpressions("1 impl $a", "$a", false);

        [TestMethod]
        public void Test_30() => AssertEqualExpressions("0 impl $a", "1", false);

        [TestMethod]
        public void Test_31() => AssertValidExpression("$a[$b = $c] = $e", true);

        [TestMethod]
        public void Test_32() => AssertValidExpression("$a[$b = $c] = $e", false);

        [TestMethod]
        public void Test_33() => AssertInvalidExpression("$a[$b = $c]", true);

        [TestMethod]
        public void Test_34() => AssertValidExpression("$a[$b = $c]", false);

        [TestMethod]
        public void Test_35() => AssertValidExpression("°$a", false);

        [TestMethod]
        public void Test_36() => AssertValidExpression("°0", false);

        [TestMethod]
        public void Test_37() => AssertValidExpression("°null", false);

        [TestMethod]
        public void Test_38() => AssertValidExpression("°($a)", false);

        [TestMethod]
        public void Test_39() => AssertValidExpression("°($a + 2 * $b)", false);

        [TestMethod]
        public void Test_40() => AssertValidExpression("func(°$a(33))", false);

        [TestMethod]
        public void Test_41() => AssertValidExpression("func(°$a + 42)", false);

        [TestMethod]
        public void Test_42() => AssertValidExpression("func(°($a + 42))", false);

        [TestMethod]
        public void Test_43() => AssertInvalidExpression("°$a", true);

        [TestMethod]
        public void Test_44() => AssertValidExpression("°$a = 42", true);

        [TestMethod]
        public void Test_45() => AssertValidExpression("°$a += °°func()", true);

        [TestMethod]
        public void Test_46() => AssertValidExpression("°func() = °0", true);

        [TestMethod]
        public void Test_47() => AssertValidExpression("$a @ 5", false);

        [TestMethod]
        public void Test_48() => AssertValidExpression("$a @| $b", false);

        [TestMethod]
        public void Test_49() => AssertValidExpression("$a @ $b .. $c", false);

        [TestMethod]
        public void Test_50() => AssertValidExpression("$a @ -1 .. 7 + 9", false);

        [TestMethod]
        public void Test_51() => AssertInvalidExpression("$a @ $b .. $c .. $d", false);

        [TestMethod]
        public void Test_52() => AssertInvalidExpression("$a .. 7", true);

        [TestMethod]
        public void Test_53() => AssertEqualExpressions("$a @| $b @ $c .. $d", "(($a @| $b) @ $c .. $d)", false);
    }
}
