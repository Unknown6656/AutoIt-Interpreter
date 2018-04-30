using System.Collections.Generic;
using System;

namespace AutoItCoreLibrary
{
    using var = AutoItVariantType;

    public static class AutoItFunctions
    {
#pragma warning disable RCS1057
#pragma warning disable IDE1006

        private static var __(Action f)
        {
            f?.Invoke();

            return var.Default;
        }

        [BuiltinFunction]
        public static var Min(var v1, var v2) => v1 <= v2 ? v1 : v2;
        [BuiltinFunction]
        public static var Max(var v1, var v2) => v1 >= v2 ? v1 : v2;

        [BuiltinFunction]
        public static var ConsoleRead() => Console.ReadLine();
        [BuiltinFunction]
        public static var ConsoleWrite(var v) => __(() => Console.Write(v.ToString()));
        [BuiltinFunction]
        public static var ConsoleWriteLine(var v) => __(() => Console.WriteLine(v.ToString()));


        // TODO : add all other functions from https://www.autoitscript.com/autoit3/docs/functions/


        public static var __InvalidFunction__(params var[] _) =>
            throw new InvalidProgramException("The application tried to call an non-existing function ...");

        public static var __Debug__(AutoItVariableDictionary vardic) => __(() =>
        {
            Console.WriteLine("globals:");

            foreach (string var in vardic._globals.Keys)
                Console.WriteLine($"    ${var} = \"{vardic._globals[var]}\"");

            if (vardic._locals.Count > 0)
            {
                Dictionary<string, var> topframe = vardic._locals.Peek();

                Console.WriteLine("locals:");

                foreach (string var in topframe.Keys)
                    Console.WriteLine($"    ${var} = \"{topframe[var]}\"");
            }
        });

#pragma warning restore RCS1057
#pragma warning restore IDE1006
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class BuiltinFunctionAttribute
        : Attribute
    {
    }
}
