using Microsoft.VisualStudio.TestTools.UnitTesting;

using AutoItInterpreter;


namespace UnitTests
{
    [TestClass]
    public sealed class CompilerTests
        : TestCommons
    {
        private static readonly (int, int)[] NO_ERRORS = new (int, int)[0];


        [TestMethod]
        public void Test_01() => TestAutoItCode(@"
func f1()
end func
", (InterpreterState state) =>
        {
            Assert.AreEqual(2, state.Functions.Count);
            Assert.IsTrue(state.Functions.ContainsKey("f1"));
        });

        [TestMethod]
        public void Test_02() => ExpectErrorsByMarkers(@"
for $x = 0 to 10
    for $y = 0 to 10
        exitloop 1
        exitloop 2
        exitloop 3 ; <--- #2002
        exitloop top/lel ; <--- #2003
        continueloop 1
        continueloop 2
        continueloop 3 ; <--- #2004
        continueloop top/lel ; <--- #2005
    next
next
");

        [TestMethod]
        public void Test_03() => ExpectErrors(@"
switch $x ; <--- #1027
    case 1, 2 to 7 + ""4"", 0x99, -0.5
    case else
    case else
endswitch
", NO_ERRORS);
    }
}
