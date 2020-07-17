using Microsoft.VisualStudio.TestTools.UnitTesting;

using AutoItInterpreter;

namespace UnitTests
{
    [TestClass, TestingPriority(100), Skip]
    public sealed class CompilerTests
        : TestCommons
    {
        private static readonly (int, int)[] NO_ERRORS = new (int, int)[0];


        [TestMethod]
        public void Test_00() => TestAutoItCode(@"
func f1()
end func
", (InterpreterState state) =>
        {
            Assert.AreEqual(2, state.Functions.Count);
            Assert.IsTrue(state.Functions.ContainsKey("f1"));
        });

        [TestMethod]
        public void Test_01() => TestAutoItCode(@"
#cs
    Func f2()
        // this is an invalid comment inside an non-existent function ...
    EndFunc
#ce
", (InterpreterState state) => Assert.AreEqual(1, state.Functions.Count));

        [TestMethod]
        public void Test_02() => ExpectErrorsByMarkers(@"
for $x = 0 to 10
    for $y = 0 to 10
        continueloop 2
        exitloop 3 ; <--- #2002
        exitloop top/lel ; <--- #1202
        continueloop 3 ; <--- #2004
    next
    exitloop 1
next
");

        [TestMethod]
        public void Test_03() => ExpectErrorsByMarkers(@"
switch $x ; <--- #1204
    case 1, 2 to 7 + ""4"", 0x99, -0.5
    case else
    case else
endswitch
");

        [TestMethod]
        public void Test_04() => ExpectErrorsByMarkers(@"
f1(42)  ; <--- #1210
f2() ; <--- #1211

func f1()
endfunc

func f2($a)
endfunc
");
    }
}
