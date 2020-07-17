using Microsoft.VisualStudio.TestTools.UnitTesting;

using AutoItInterpreter;

namespace UnitTests
{
    [TestClass]
    public sealed class IntegrityTests
        : TestCommons
    {
        [TestMethod]
        public void Test_01() => Assert.IsTrue(Language.Languages.Count > 0);

        [TestMethod]
        public void Test_02()
        {
            foreach (string code in Language.LanugageCodes)
                if (Language.Languages.TryGetValue(code, out Language lang))
                    Assert.IsNotNull(lang);
                else
                    Assert.Fail($"No language associated with the language code '{code}' could be found.");
        }


        // TODO : verify that all language entries have an error code and that all language entries exist in all languages
    }
}
