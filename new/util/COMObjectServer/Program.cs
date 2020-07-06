using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Unknown6656.AutoIt3.COM.Server
{
    public static class Program
    {
        // [DllImport("ole32.dll", PreserveSig = false)]
        // static extern void CLSIDFromProgIDEx([MarshalAs(UnmanagedType.LPWStr)] string progId, Guid* clsid);
        // [DllImport("ole32.dll", PreserveSig = false)]
        // static extern void CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string progId, Guid* clsid);
        // [DllImport("oleaut32.dll", PreserveSig = false)]
        // static extern void GetActiveObject(Guid* rclsid, void* reserved, [MarshalAs(UnmanagedType.Interface)] out object ppunk);




        private static readonly ConcurrentDictionary<uint, object> _com_objects = new();
        private static volatile uint _nextid = 0;

        /*
            ARGUMENTS:
                <pipe name> [...]
         */
        public static async Task<int> Main(string[] argv)
        {
            if (argv.Length < 1)
                return -1;

            using NamedPipeServerStream server = new NamedPipeServerStream(argv[1]);

            await server.WaitForConnectionAsync();

            using BinaryReader reader = new BinaryReader(server);
            using BinaryWriter writer = new BinaryWriter(server);
            bool running = true;

            while (running)
                switch ((COMInteropCommand)reader.ReadByte())
                {
                    case COMInteropCommand.Quit:
                        running = false;

                        break;
                    case COMInteropCommand.Create:
                        {
                            string classname = reader.ReadString();
                            uint? id = CreateCOMObject(classname);

                            if (id is uint i)
                            {
                                writer.Write(true);
                                writer.Write(i);
                            }
                            else
                                writer.Write(false);
                        }
                        goto default;
                    case COMInteropCommand.GetMembers:
                        {
                            uint id = reader.ReadUInt32();

                            // TODO
                        }
                        goto default;
                    case COMInteropCommand.Delete:
                        {
                            uint id = reader.ReadUInt32();

                            DeleteCOMObject(id);
                        }
                        goto default;
                    case COMInteropCommand.DeleteAll:
                        DeleteAllCOMObjects();

                        break;
                    case COMInteropCommand._none_:
                    default:
                        // TODO : sleep (?)

                        writer.Flush();
                        server.WaitForPipeDrain();

                        await Task.Delay(50);

                        break;
                }

            return 0;
        }

        // var memb = type.FindMembers(MemberTypes.All, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, null);

        public static uint? CreateCOMObject(string classname, string? server = null, string? user = null, string? passwd = null)
        {
            try
            {
                if (System.Type.GetTypeFromProgID(classname, server, false) is Type t && Activator.CreateInstance(t) is object com)
                {
                    // TODO : username | passwd

                    _com_objects[_nextid++] = com;
                }
            }
            catch
            {
            }

            return null;
        }

        public static void DeleteCOMObject(uint id)
        {
            if (_com_objects.TryRemove(id, out object? com))
                Marshal.FinalReleaseComObject(com);
        }

        public static void DeleteAllCOMObjects()
        {
            foreach (uint id in _com_objects.Keys.ToArray())
                DeleteCOMObject(id);
        }

        public static string[] GetMemberNames(uint id)
        {
            if (_com_objects.TryGetValue(id, out object? com))
                return com.GetType().FindMembers(MemberTypes.All, BindingFlags.Public | BindingFlags.Instance, null, null).Select(m => m.Name).ToArray();

            return Array.Empty<string>();
        }
    }
}
