using System;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AutoItCoreLibrary;

namespace CoreTests
{
    using v = AutoItVariantType;


    public static unsafe class Program
    {
        public delegate int d(int i, int j);


        public static void Main(string[] args)
        {
            v mat = v.NewMatrix(3, 3, 3);

            for (int z = 0; z < 3; ++z)
                for (int y = 0; y < 3; ++y)
                    for (int x = 0; x < 3; ++x)
                        mat[z, y, x] = $"({z}|{y}|{x})";

            mat[1, 1, 1] = v.NewDelegate<AutoItDelegate4Opt1>(TOP_KEK);

            Console.ForegroundColor = ConsoleColor.DarkYellow;

            AutoItFunctions.Debug(mat);

            //v server = AutoItFunctions.TCPListen("[::]", 41488);
            //v client = AutoItFunctions.TCPAccept(server);
            //v txt = AutoItFunctions.TCPRecv(client);

            AutoItFunctions.Debug(v.NewArray(v.NewArray(1,0,0), v.NewArray(0,1,0), v.NewArray(0,0,1)));

            v com = v.CreateCOM("shell.application");
            var tp = com.GetCOM().Type;

            AutoItFunctions.Debug(com);
        }

        public static v TOP_KEK(v v1, v v2, v v3, v? v4 = null) => $"v1={v1}, v2={v2}, v3={v3}, v4={v4 ?? v.Null}";
    }
}
