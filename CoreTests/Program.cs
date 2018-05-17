using System;

using AutoItCoreLibrary;

namespace CoreTests
{
    using v = AutoItVariantType;


    public static class Program
    {
        public static void Main(string[] args)
        {
            v mat = v.NewMatrix(3, 3, 3);

            for (int z = 0; z < 3; ++z)
                for (int y = 0; y < 3; ++y)
                    for (int x = 0; x < 3; ++x)
                        mat[z, y, x] = $"({z}|{y}|{x})";

            v mat2 = mat.Clone();

            mat2[0, 0, 0] = v.NewDelegate(new Action(kek));
            mat2[0, 0, 0].Call();

            var s = mat.ToDebugString();
            var s2 = mat2.ToDebugString();
        }

        public static void kek() => Console.WriteLine("lel");
    }
}
