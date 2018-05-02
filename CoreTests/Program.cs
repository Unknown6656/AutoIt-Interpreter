using System;

using AutoItCoreLibrary;

namespace CoreTests
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var mat = AutoItVariantType.NewMatrix(3, 3, 3);
            
            for (int z = 0; z < 3; ++z)
                for (int y = 0; y < 3; ++y)
                    for (int x = 0; x < 3; ++x)
                        mat[z, y, x] = $"({z}|{y}|{x})";

            var s = mat.ToDebugString();
        }
    }
}
