using Microsoft.VisualStudio.TestTools.UnitTesting;

using AutoItInterpreter;


namespace UnitTests
{
    [TestClass]
    public sealed class CompilerTests
        : TestCommons
    {
        [TestMethod]
        public void Test_01() => TestAutoItCode(@"
func f1()
end func
", (InterpreterState state) =>
        {
            Assert.AreEqual(2, state.Functions.Count);
            Assert.IsTrue(state.Functions.ContainsKey("f1"));
        });
    }
}
