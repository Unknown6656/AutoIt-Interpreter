using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Dynamic;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Unknown6656.AutoIt3.COM.Server
{
    using COMData = COMData<COMWrapper>;

    public static class Program
    {
        private static readonly COMServer _server = new COMServer();
        private static BinaryWriter? _debug_writer = null;
        private static volatile bool _running = true;


        /*
            ARGUMENTS:
                <data-pipe> <debugging-pipe> [...]
         */
        public static int Main(string[] argv)
        {
            int code = -1;

            if (argv.Length > 1)
                try
                {
                    using Task debug_task = Task.Factory.StartNew(async delegate
                    {
                        using NamedPipeClientStream debug = new NamedPipeClientStream(argv[1]);

                        await debug.ConnectAsync();

                        _debug_writer = new BinaryWriter(debug);

                        while (_running)
                            await Task.Delay(100);

                        _debug_writer.Close();
                        _debug_writer.Dispose();
                        _debug_writer = null;
                        debug.Close();
                        debug.Dispose();
                    });
                    using NamedPipeServerStream server = new NamedPipeServerStream(argv[0]);

                    server.WaitForConnection();

                    using BinaryReader reader = new BinaryReader(server);
                    using BinaryWriter writer = new BinaryWriter(server);

                    DebugPrint("COM server is running.");

                    while (_running && server.IsConnected)
                        switch (reader.ReadNative<COMInteropCommand>())
                        {
                            case COMInteropCommand.Quit:
                                DebugPrint("Shutting down COM server.");

                                _running = false;

                                break;
                            case COMInteropCommand.Create:
                                {
                                    string classname = reader.ReadString();
                                    string? servername = reader.ReadNullable();
                                    string? username = reader.ReadNullable();
                                    string? passwd = reader.ReadNullable();
                                    uint? id = _server.CreateCOMObject(classname, servername, username, passwd);

                                    DebugPrint($"Created COM object '{classname}' on '{username}'@'{servername}'. COM object ID: {id}");

                                    writer.WriteNullable(id);
                                }
                                goto default;
                            case COMInteropCommand.EnumerateMembers:
                                {
                                    uint id = reader.ReadUInt32();
                                    string[] members = _server.GetMemberNames(id);

                                    DebugPrint($"Enumerating members of COM object '{id}' ({members.Length} members).");

                                    writer.Write(members.Length);

                                    foreach (string member in members)
                                        writer.Write(member);
                                }
                                goto default;
                            case COMInteropCommand.GetIndex:
                                {
                                    uint id = reader.ReadUInt32();
                                    COMData index = reader.ReadCOM<COMWrapper>();
                                    COMData? value_o = null;
                                    bool success = _server.TryGetCOMObject(id, out COMWrapper com) && com.TryGetIndex(index, out value_o);

                                    DebugPrint($"Fetching index '{index}' of COM object '{id}': {(success ? "success" : "fail")}.");

                                    writer.Write(success);

                                    if (success)
                                        writer.WriteCOM(value_o!.Value);
                                }
                                goto default;
                            case COMInteropCommand.SetIndex:
                                {
                                    uint id = reader.ReadUInt32();
                                    COMData index = reader.ReadCOM<COMWrapper>();
                                    COMData value = reader.ReadCOM<COMWrapper>();
                                    bool success = _server.TryGetCOMObject(id, out COMWrapper com) && com.TrySetIndex(index, value);

                                    DebugPrint($"Setting index '{index}' of COM object '{id}' to '{value}': {(success ? "success" : "fail")}.");

                                    writer.Write(success);
                                }
                                goto default;
                            case COMInteropCommand.GetMember:
                                {
                                    uint id = reader.ReadUInt32();
                                    string name = reader.ReadString();
                                    COMData? value_o = null;
                                    bool success = _server.TryGetCOMObject(id, out COMWrapper com) && com.TryGetMember(name, out value_o);

                                    DebugPrint($"Fetching member '{name}' of COM object '{id}': {(success ? "success" : "fail")}.");

                                    writer.Write(success);

                                    if (success)
                                        writer.WriteCOM(value_o!.Value);
                                }
                                goto default;
                            case COMInteropCommand.SetMember:
                                {
                                    uint id = reader.ReadUInt32();
                                    string name = reader.ReadString();
                                    COMData value = reader.ReadCOM<COMWrapper>();
                                    bool success = _server.TryGetCOMObject(id, out COMWrapper com) && com.TrySetMember(name, value);

                                    DebugPrint($"Setting member '{name}' of COM object '{id}' to '{value}': {(success ? "success" : "fail")}.");

                                    writer.Write(success);
                                }
                                goto default;
                            case COMInteropCommand.Invoke:
                                {
                                    uint id = reader.ReadUInt32();
                                    string name = reader.ReadString();
                                    COMData[] args = new COMData[reader.ReadInt32()];

                                    for (int i = 0; i < args.Length; ++i)
                                        args[i] = reader.ReadCOM<COMWrapper>();

                                    if (_server.TryGetCOMObject(id, out COMWrapper com) && com.TryInvoke(name, args, out COMData? value_o) && value_o is COMData value)
                                    {
                                        writer.Write(true);
                                        writer.WriteCOM(value);
                                    }
                                    else
                                        writer.Write(false);
                                }
                                goto default;
                            case COMInteropCommand.Delete:
                                {
                                    uint id = reader.ReadUInt32();

                                    DebugPrint($"Deleting COM object '{id}'.");

                                    _server.DeleteCOMObject(id);
                                }
                                goto default;
                            case COMInteropCommand.DeleteAll:
                                DebugPrint($"Deleting all COM objects.");

                                _server.DeleteAllCOMObjects();

                                break;
                            case COMInteropCommand command:
                                DebugPrint($"Recieving '{command}'.");

                                break;
                            default:
                                writer.Flush();

                                break;
                        }

                    code = 0;
                }
                catch (Exception ex)
                {
                    code = ex.HResult;

                    StringBuilder sb = new StringBuilder();

                    while (ex is { })
                    {
                        sb.Insert(0, $"[{ex.GetType()}] \"{ex.Message}\":\n{ex.StackTrace}\n");
                        ex = ex.InnerException;
                    }

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(sb.ToString());
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                finally
                {
                    _server.DeleteAllCOMObjects();
                    _debug_writer?.Close();
                    _debug_writer?.Dispose();
                    _debug_writer = null;
                }

            return code;
        }

        public static void DebugPrint(string message)
        {
            if (_debug_writer is { } wr)
            {
                message = message.Trim();

                wr.Write(message);
                wr.Flush();
            }
        }
    }

    public sealed class COMServer
        : ICOMConverter<COMWrapper>
    {
        private readonly ConcurrentDictionary<uint, COMWrapper> _com_objects = new();
        private volatile uint _nextid = 0;



        public COMServer() => COMData.Converter = this;

        public COMWrapper Read(BinaryReader reader) => throw new NotImplementedException();

        public void Write(BinaryWriter writer, COMWrapper data) => throw new NotImplementedException();


        public uint? CreateCOMObject(string classname, string? server = null, string? user = null, string? passwd = null)
        {
            try
            {
                if (Type.GetTypeFromProgID(classname, server, false) is Type t && Activator.CreateInstance(t) is object com)
                {
                    // TODO : username | passwd

                    _com_objects[_nextid++] = new COMWrapper(com);
                }
            }
            catch (Exception ex)
            {
                Program.DebugPrint(ex.Message);
            }

            return null;
        }

        public bool TryGetCOMObject(uint id, out COMWrapper com) => _com_objects.TryGetValue(id, out com);

        public void DeleteCOMObject(uint id)
        {
            if (_com_objects.TryRemove(id, out COMWrapper? com))
                com.Dispose();
        }

        public void DeleteAllCOMObjects()
        {
            foreach (uint id in _com_objects.Keys.ToArray())
                DeleteCOMObject(id);
        }

        public string[] GetMemberNames(uint id)
        {
            if (_com_objects.TryGetValue(id, out COMWrapper? com))
                return com.GetType().FindMembers(MemberTypes.All, BindingFlags.Public | BindingFlags.Instance, null, null).Select(m => m.Name).ToArray();

            return Array.Empty<string>();
        }
    }

    public sealed unsafe class COMWrapper
        : DynamicObject
        , IEquatable<COMWrapper>
        , IDisposable
    {
        #region FIELDS

        private static readonly MethodInfo _get;
        private static readonly MethodInfo _set;
        private static readonly MethodInfo _release;

        #endregion
        #region PROPERTIES

        private readonly HashSet<COMWrapper> _objects = new HashSet<COMWrapper>();

        public bool IsDisposed { get; private set; }

        public object COMObject { get; }

        public nint IUnknownPtr { get; }

        public Type ObjectType { get; }

        #endregion
        #region .CCTOR / .CTOR / .DTOR

#pragma warning disable CA1810 // Initialize reference type static fields inline
        static COMWrapper()
        {
            Type type = Type.GetType("System.__ComObject")!;

            _set = type.GetMethod("SetData", BindingFlags.Instance | BindingFlags.NonPublic)!;
            _get = type.GetMethod("GetData", BindingFlags.Instance | BindingFlags.NonPublic)!;
            _release = type.GetMethod("ReleaseAllData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        }
#pragma warning restore CA1810

        private COMWrapper(Guid guid)
            : this(Activator.CreateInstance(Type.GetTypeFromCLSID(guid, true)!, true)!)
        {
        }

        internal COMWrapper(object com)
        {
            COMObject = com;
            ObjectType = com.GetType();
            IUnknownPtr = Marshal.GetIUnknownForObject(com);
        }

        ~COMWrapper() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                foreach (COMWrapper com in _objects)
                    com.Dispose();

                _objects.Clear();
                _release.Invoke(COMObject, null);

                Marshal.FinalReleaseComObject(COMObject);

                IsDisposed = true;
            }
        }

        #endregion
        #region MEMBER ACCESSORS

        public bool TrySetIndex(COMData index, COMData value)
        {
            try
            {
                SetIndex(new[] { index.Data }, value.Data);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TrySetMember(string name, COMData value)
        {
            try
            {
                SetMember(name, value.Data);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryGetIndex(COMData index, out COMData? value)
        {
            value = null;

            try
            {
                object? data = GetIndex(new[] { index.Data });

                value = COMData.FromObject(data);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryGetMember(string name, out COMData? value)
        {
            value = null;

            try
            {
                object? data = GetMember(name);

                value = COMData.FromObject(data);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryInvoke(string name, COMData[] arguments, out COMData? value)
        {
            try
            {
                object?  data = InvokeMember(name, arguments.Select(arg => arg.Data).ToArray());

                value = COMData.FromObject(data);

                return true;
            }
            catch
            {
                value = null;

                return false;
            }
        }

        public object? GetMember(string name) =>
            TransformValue(ObjectType.InvokeMember(name, BindingFlags.GetProperty, Type.DefaultBinder, COMObject, null, CultureInfo.InvariantCulture));

        public void SetMember(string name, object? value) =>
            ObjectType.InvokeMember(name, BindingFlags.SetProperty, Type.DefaultBinder, COMObject, new[] { value }, CultureInfo.InvariantCulture);

        public object? GetIndex(object?[] indices) =>
            TransformValue(ObjectType.InvokeMember("Item", BindingFlags.InvokeMethod | BindingFlags.GetProperty, Type.DefaultBinder, COMObject, indices, CultureInfo.InvariantCulture));

        public void SetIndex(object?[] indices, object? value) =>
            ObjectType.InvokeMember("Item", BindingFlags.InvokeMethod | BindingFlags.SetProperty, Type.DefaultBinder, COMObject, indices.Append(value).ToArray(), CultureInfo.InvariantCulture);

        public object? InvokeMember(string name, object?[]? args) =>
            TransformValue(ObjectType.InvokeMember(name, BindingFlags.InvokeMethod, Type.DefaultBinder, COMObject, args?.Select(TransformValue).ToArray(), CultureInfo.InvariantCulture));

        #endregion
        #region OVERRIDES

        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            SetMember(binder.Name, value);

            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = GetMember(binder.Name);

            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object?[] indices, out object? result)
        {
            result = GetIndex(indices);

            return true;
        }

        public override bool TrySetIndex(SetIndexBinder binder, object?[] indices, object? value)
        {
            SetIndex(indices, value);

            return true;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
        {
            result = InvokeMember(binder.Name, args);

            return true;
        }

        public override unsafe IEnumerable<string> GetDynamicMemberNames()
        {
            return ObjectType.FindMembers(MemberTypes.Field | MemberTypes.Property | MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance, null, null)
                             .Select(o => o.Name);
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            result = COMObject;

            return true;
        }

        public override int GetHashCode() => COMObject?.GetHashCode() ?? 0;

        public override bool Equals(object? obj) => ReferenceEquals(this, obj) || (obj is COMWrapper cw && Equals(cw));

        public bool Equals(COMWrapper? other) => Equals(COMObject, other?.COMObject);

        #endregion

        private object? TransformValue(object? value) => value switch
        {
            object com when IsComObject(com) => new Func<COMWrapper>(delegate
            {
                COMWrapper wrapper = new COMWrapper(com);

                _objects.Add(wrapper);

                return wrapper;
            })(),
            IEnumerable a when !IsPrimitive(a) => a.Cast<object>().Select(TransformValue).ToArray(),
            COMWrapper o => o.COMObject,
            _ => value,
        };

        public static COMWrapper FromGUID(Guid guid) => new COMWrapper(guid);

        private static bool IsComObject(object o) => o.GetType().Name == "__ComObject";

        private static bool IsPrimitive(object o)
        {
            Type t = o.GetType();

            return t.IsPrimitive || t.IsValueType || t == typeof(string);
        }
    }
}
