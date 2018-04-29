using System.Globalization;
using System;

namespace AutoItCoreLibrary
{
    public static class AutoItFunctions
    {
#pragma warning disable RCS1057
#pragma warning disable IDE1006

        [BuiltinFunction]
        public static AutoItVariantType Min(AutoItVariantType v1, AutoItVariantType v2) => v1 <= v2 ? v1 : v2;
        [BuiltinFunction]
        public static AutoItVariantType Max(AutoItVariantType v1, AutoItVariantType v2) => v1 >= v2 ? v1 : v2;

        public static AutoItVariantType __InvalidFunction__(params AutoItVariantType[] _) =>
            throw new InvalidProgramException("The application tried to call an non-existing function ...");

#pragma warning restore RCS1057
#pragma warning restore IDE1006
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class BuiltinFunctionAttribute
        : Attribute
    {
    }
}
