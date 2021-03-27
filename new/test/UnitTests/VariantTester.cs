using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Testing;

namespace UnitTests
{
    [TestClass]
    public sealed class VariantConversionTester
        : UnitTestRunner
    {
        [TestMethod, TestWith("", "test", "\0test\x1c\U000124F1 \U0001F914")]
        public void Test_01__String(string original)
        {
            Variant variant = original;

            Assert.AreEqual(VariantType.String, variant.Type);
            Assert.AreEqual(original.Length, variant.Length);

            string s1 = variant.ToString();
            string s2 = (string)variant;

            Assert.AreEqual(original, s1);
            Assert.AreEqual(s1, s2);
        }

        [TestMethod, TestWith(0, -420.736, double.MaxValue, double.MinValue, double.Epsilon, 77, Math.PI)]
        public void Test_01__Decimal(double original)
        {
            Variant variant = original;

            Assert.AreEqual(VariantType.Number, variant.Type);

            double d1 = variant.ToNumber();
            double d2 = (double)variant;

            Assert.AreEqual(original, d1);
            Assert.AreEqual(d1, d2);
        }

        // TODO : add test cases
    }

    [TestClass]
    public sealed class VariantOperatorTester
        : UnitTestRunner
    {
        [TestMethod]
        public void Test_01__Concat()
        {
            string str1 = "Hello, ";
            string str2 = "World!";
            Variant variant1 = str1;
            Variant variant2 = str2;

            Variant concat = variant1 & variant2;

            Assert.AreEqual(VariantType.String, concat.Type);
            Assert.AreEqual(str1 + str2, concat.ToString());
        }

        // TODO : add test cases
    }
}
