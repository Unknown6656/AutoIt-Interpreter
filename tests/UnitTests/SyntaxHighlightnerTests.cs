using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Collections.Generic;
using System.Text;
using System.Linq;
using System;

using AutoItExpressionParser.SyntaxHighlightning;

namespace UnitTests
{
    using static HighlightningStyle;


    [TestClass, TestingPriority(1000)]
    public sealed class SyntaxHighlightnerTests
        : TestCommons
    {
        internal static (Section[] Sections, bool IsBlockComment) ParseSingleLine(string code, bool IsBlockComment = false)
        {
            Tuple<Section[], bool> res = SyntaxHighlighter.ParseLine(code, 0, IsBlockComment);

            return (SyntaxHighlighter.Optimize.Invoke(res.Item1), res.Item2);
        }

        internal static Section[] ParseMultiLine(string code) => SyntaxHighlighter.Optimize.Invoke(SyntaxHighlighter.ParseCode(code));

        internal static void IsMultiLine(string code, params (HighlightningStyle, string)[] expected) =>
            AssertSequencialEquals(ParseMultiLine(code).Select(s => (s.Style, s.StringContent)), expected);

        internal static void IsSingleLine(string code, params (HighlightningStyle, string)[] expected) => IsSingleLine(code, false, expected);

        internal static void IsSingleLine(string code, bool IsBlockComment, params (HighlightningStyle, string)[] expected) =>
            AssertSequencialEquals(ParseSingleLine(code, IsBlockComment).Sections.Select(s => (s.Style, s.StringContent)), expected);


        [TestMethod]
        public void Test_01() => Assert.IsTrue(ParseSingleLine("#cs").IsBlockComment);

        [TestMethod]
        public void Test_02()
        {
            Section[] sec = ParseSingleLine("#cs").Sections;

            Assert.AreEqual(1, sec.Length);
            Assert.AreEqual(HighlightningStyle.Comment, sec[0].Style);
        }

        [TestMethod]
        public void Test_03() => Assert.IsTrue(ParseSingleLine("#ce", true).IsBlockComment);

        [TestMethod]
        public void Test_04() => Assert.IsFalse(ParseSingleLine("#ce\nmyfunc()", true).IsBlockComment);

        [TestMethod]
        public void Test_05() => Assert.AreEqual("function() ", string.Concat(ParseSingleLine("function() ; comment").Sections.TakeWhile(s => s.Style != Comment).Select(s => s.StringContent)));

        [TestMethod]
        public void Test_06() => IsSingleLine("$a += (315 - @b) ; comment".Trim(), new[] {
            (Variable, "$a"),
            (Operator, " += "),
            (Symbol, "("),
            (Number, "315"),
            (Operator, " - "),
            (Macro, "@b"),
            (Symbol, ")"),
            (Code, " "),
            (Comment, "; comment"),
        });

        [TestMethod]
        public void Test_07() => Assert.AreEqual("$var", string.Concat(ParseSingleLine("$\"test $var\" text").Sections.Where(s => s.Style == StringEscapeSequence).Select(s => s.StringContent)));

        [TestMethod]
        public void Test_08() => Assert.AreEqual("\"string\"", string.Concat(ParseSingleLine("7 + test(\"string\")").Sections.Where(s => s.Style == String).Select(s => s.StringContent)));

        [TestMethod]
        public void Test_09() => IsSingleLine("func beep as \"int beep(int, int)\" from \"kernel32.dll\"".Trim(), new[] {
            (Keyword, "func"),
            (Code, " "),
            (Function, "beep"),
            (Code, " "),
            (Keyword, "as"),
            (Code, " "),
            (String, "\"int beep(int, int)\""),
            (Code, " "),
            (Keyword, "from"),
            (Code, " "),
            (String, "\"kernel32.dll\""),
        });
    }
}
