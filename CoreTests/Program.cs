using System;

using AutoItCoreLibrary;

namespace CoreTests
{
    using v = AutoItVariantType;


    public static unsafe class Program
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


            v d = v.NewDelegate(new AutoItDelegate4(TOP_KEK));
            v d0 = d.Call();
            v d1 = d0.Call(3m);
            v d2 = d1.Call();
            v d3 = d2.Call("joj kek", -9.99m);
            v d4 = d3.Call();
            v d5 = d4.Call((void*)0x315);
        }

        public static void kek() => Console.WriteLine("lel");

        public static v TOP_KEK(v v1, v v2, v v3, v v4) => $"v1={v1}, v2={v2}, v3={v3}, v4={v4}";
    }
}
