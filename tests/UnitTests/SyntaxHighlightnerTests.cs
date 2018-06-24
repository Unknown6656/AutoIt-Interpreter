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
            Assert.IsTrue(ParseMultiLine(code).Select(s => (s.Style, s.StringContent)).SequenceEqual(expected));

        internal static void IsSingleLine(string code, params (HighlightningStyle, string)[] expected) => IsSingleLine(code, false, expected);

        internal static void IsSingleLine(string code, bool IsBlockComment, params (HighlightningStyle, string)[] expected) =>
            Assert.IsTrue(ParseSingleLine(code, IsBlockComment).Sections.Select(s => (s.Style, s.StringContent)).SequenceEqual(expected));


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
            (Code, " - "),
            (Macro, "@b"),
            (Symbol, ")"),
            (Code, " "),
            (Comment, "; comment"),
        });
    }
}
