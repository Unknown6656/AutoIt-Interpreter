using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.Reflection;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System;

namespace Unknown6656.AutoIt3.COM.Server
{
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

                                _server.DeleteAllCOMObjects();
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
                                    COMData index = reader.ReadCOM();
                                    COMData? value_o = null;
                                    bool success = _server.TryResolveCOMObject(id, out COMWrapper com) && com.TryGetIndex(index, out value_o);

                                    DebugPrint($"Fetching index '{index}' of COM object '{id}': {(success ? "success" : "fail")}.");

                                    writer.Write(success);

                                    if (success)
                                        writer.WriteCOM(value_o!.Value);
                                }
                                goto default;
                            case COMInteropCommand.SetIndex:
                                {
                                    uint id = reader.ReadUInt32();
                                    COMData index = reader.ReadCOM();
                                    COMData value = reader.ReadCOM();
                                    bool success = _server.TryResolveCOMObject(id, out COMWrapper com) && com.TrySetIndex(index, value);

                                    DebugPrint($"Setting index '{index}' of COM object '{id}' to '{value}': {(success ? "success" : "fail")}.");

                                    writer.Write(success);
                                }
                                goto default;
                            case COMInteropCommand.GetMember:
                                {
                                    uint id = reader.ReadUInt32();
                                    string name = reader.ReadString();
                                    COMData? value_o = null;
                                    bool success = _server.TryResolveCOMObject(id, out COMWrapper com) && com.TryGetMember(name, out value_o);

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
                                    COMData value = reader.ReadCOM();
                                    bool success = _server.TryResolveCOMObject(id, out COMWrapper com) && com.TrySetMember(name, value);

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
                                        args[i] = reader.ReadCOM();

                                    if (_server.TryResolveCOMObject(id, out COMWrapper com) && com.TryInvoke(name, args, out COMData? value_o) && value_o is COMData value)
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
                                server.Flush();

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
                    _debug_writer?.Flush();
                    _debug_writer?.Close();
                    _debug_writer?.Dispose();
                    _debug_writer = null;
                }

            return code;
        }

        public static void DebugPrint(string message)
        {
            if (_debug_writer is { } wr)
                try
                {
                    message = message.Trim();

                    wr.Write(message);
                    wr.Flush();
                }
                catch
                {
                }
        }
    }

    public sealed class COMServer
        : ICOMResolver<COMWrapper>
    {
        private readonly ConcurrentDictionary<uint, COMWrapper> _com_objects = new();
        private volatile uint _nextid = 0;


        public uint? CreateCOMObject(string classname, string? server = null, string? user = null, string? passwd = null)
        {
            try
            {
                COMWrapper? cw = null;

                if (Guid.TryParse(classname, out Guid uuid))
                    cw = new COMWrapper(uuid);
                else if (Type.GetTypeFromProgID(classname, server, false) is Type t && Activator.CreateInstance(t) is { } com)
                    cw = new COMWrapper(com); // TODO : username | passwd

                if (cw is { })
                {
                    uint id = ++_nextid;

                    _com_objects[id] = cw;

                    return id;
                }
            }
            catch (Exception ex)
            {
                Program.DebugPrint(ex.Message);
            }

            return null;
        }

        public bool TryResolveCOMObject(uint id, out COMWrapper com) => _com_objects.TryGetValue(id, out com);

        public void DeleteCOMObject(uint id)
        {
            if (_com_objects.TryRemove(id, out COMWrapper? com))
                com.Dispose();
        }

        public void DeleteAllCOMObjects()
        {
            foreach (COMWrapper obj in _com_objects.Values)
                obj.Dispose();

            _com_objects.Clear();
        }

        public string[] GetMemberNames(uint id)
        {
            if (_com_objects.TryGetValue(id, out COMWrapper? com))
                return com.GetType().FindMembers(MemberTypes.All, BindingFlags.Public | BindingFlags.Instance, null, null).Select(m => m.Name).ToArray();

            return Array.Empty<string>();
        }

        internal static object? Cast(object? value, Type target_type)
        {
            if (target_type == typeof(object))
                return value;
            else if (value?.GetType() == target_type)
                return value;
            else if (target_type == typeof(bool))
                return Convert.ToBoolean(value);
            else if (target_type == typeof(byte))
                return Convert.ToByte(value);
            else if (target_type == typeof(sbyte))
                return Convert.ToSByte(value);
            else if (target_type == typeof(short))
                return Convert.ToInt16(value);
            else if (target_type == typeof(ushort))
                return Convert.ToUInt16(value);
            else if (target_type == typeof(char))
                return Convert.ToChar(value);
            else if (target_type == typeof(int))
                return Convert.ToInt32(value);
            else if (target_type == typeof(uint))
                return Convert.ToUInt32(value);
            else if (target_type == typeof(long))
                return Convert.ToInt64(value);
            else if (target_type == typeof(ulong))
                return Convert.ToUInt64(value);
            else if (target_type == typeof(float))
                return Convert.ToSingle(value);
            else if (target_type == typeof(double))
                return Convert.ToDouble(value);
            else if (target_type == typeof(decimal))
                return Convert.ToDecimal(value);
            else if (target_type == typeof(string))
                return Convert.ToString(value);
            else if (target_type == typeof(DateTime))
                return Convert.ToDateTime(value);
            else if (target_type.IsAssignableFrom(value?.GetType()))
                return value;
            else
                // TODO : array conversion

                return Convert.ChangeType(value, target_type);
        }
    }

    public sealed unsafe class COMWrapper
        // : DynamicObject
        : IEquatable<COMWrapper>
        , IDisposable
    {
        #region FIELDS

        private const string INDEX_NAME = "Item";

        private static readonly MethodInfo _get;
        private static readonly MethodInfo _set;
        private static readonly MethodInfo _release;

        private readonly HashSet<COMWrapper> _objects = new HashSet<COMWrapper>();
        private readonly MemberInfo[] _cached_members;

        #endregion
        #region PROPERTIES

        public bool IsDisposed { get; private set; }

        private object? COMObject { get; set; }

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

        internal COMWrapper(Guid guid)
            : this(Activator.CreateInstance(Type.GetTypeFromCLSID(guid, true)!, true)!)
        {
        }

        internal COMWrapper(object com)
        {
            COMObject = com;
            ObjectType = com.GetType();
            IUnknownPtr = Marshal.GetIUnknownForObject(com);
            _cached_members = ObjectType.FindMembers(
                MemberTypes.Field | MemberTypes.Property | MemberTypes.Event | MemberTypes.Method,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                (_, _) => true,
                null
            );

            Program.DebugPrint($"{(long)IUnknownPtr:x16}h ({ObjectType}) created.");
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

                try
                {
                    _release.Invoke(COMObject, null);
                }
                catch
                {
                }

                if (COMObject is IDisposable disposable)
                    disposable.Dispose();

                try
                {
                    try
                    {
                        Marshal.ReleaseComObject(COMObject);
                    }
                    catch
                    {
                    }

                    while (Marshal.FinalReleaseComObject(COMObject) != 0)
                        ;
                }
                catch
                {
                }

                if (COMObject is { })
                {
                    GC.ReRegisterForFinalize(COMObject);

                    COMObject = null;

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                IsDisposed = true;

                Program.DebugPrint($"{(long)IUnknownPtr:x16}h ({ObjectType}) disposed.");
            }
        }

        #endregion
        #region MEMBER ACCESSORS

        public bool TryGetIndex(COMData index, out COMData? value)
        {
            value = null;

            try
            {
                if (FindMembers(INDEX_NAME) is { Count: > 0 } match)
                {
                    object? data = GetMember(match[0], new[] { index.Data });

                    value = COMData.FromArbitrary(data);

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public bool TrySetIndex(COMData index, COMData value)
        {
            try
            {
                if (FindMembers(INDEX_NAME) is { Count: > 0 } match)
                {
                    SetMember(match[0], value.Data, new[] { index.Data });

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public bool TryGetMember(string name, out COMData? value)
        {
            value = null;

            try
            {
                if (FindMembers(name) is { Count: > 0 } match)
                {
                    object? data = GetMember(match[0]);

                    value = COMData.FromArbitrary(data);

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public bool TrySetMember(string name, COMData value)
        {
            try
            {
                if (FindMembers(name) is { Count: > 0 } match)
                {
                    SetMember(match[0], value.Data);

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public bool TryInvoke(string name, COMData[] arguments, out COMData? value)
        {
            value = null;

            try
            {
                if (FindMembers(name) is { Count: > 0 } match)
                {
                    // TODO : overload resolution

                    object? data = GetMember(match[0], arguments.Select(arg => arg.Data).ToArray());

                    value = COMData.FromArbitrary(data);

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private List<MemberInfo> FindMembers(string name)
        {
            List<MemberInfo> members = _cached_members.Where(m => m.Name == name).ToList();

            if (members.Count == 0)
                members.AddRange(_cached_members.Where(m => string.Equals(name, m.Name, StringComparison.InvariantCultureIgnoreCase)));

            return members;
        }

        private object? GetMember(MemberInfo member, object?[]? args = null) => TransformValue(member switch
        {
            FieldInfo field => field.GetValue(field.IsStatic ? null : COMObject),
            MethodInfo method => new Func<object?>(() =>
            {
                ParameterInfo[] parms = method.GetParameters();

                args = args?.Select(TransformValue).ToArray() ?? Array.Empty<object>();

                for (int i = 0; i < parms.Length; ++i)
                    args[i] = COMServer.Cast(args[i], parms[i].ParameterType);

                return method.Invoke(method.IsStatic ? null : COMObject, args);
            })(),
            PropertyInfo property => new Func<object?>(() =>
            {
                ParameterInfo[] parms = property.GetIndexParameters();

                args ??= new object[parms.Length];

                for (int i = 0; i < parms.Length; ++i)
                    args[i] = COMServer.Cast(args[i], parms[i].ParameterType);

                return property.GetValue(COMObject, args);
            })(),

            EventInfo @event => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        });

        private object? SetMember(MemberInfo member, object? value, object?[]? args = null)
        {
            return TransformValue(member switch
            {
                FieldInfo field => new Func<object?>(() =>
                {
                    value = COMServer.Cast(TransformValue(value), field.FieldType);

                    field.SetValue(field.IsStatic ? null : COMObject, value);

                    return value;
                })(),
                MethodInfo method => new Func<object?>(() =>
                {
                    ParameterInfo[] parms = method.GetParameters();

                    args = (args is null ? new[] { value } : args.Append(value)).Select(TransformValue).ToArray();

                    for (int i = 0; i < parms.Length; ++i)
                        args[i] = COMServer.Cast(args[i], parms[i].ParameterType);

                    return method.Invoke(method.IsStatic ? null : COMObject, args);
                })(),
                PropertyInfo property => new Func<object?>(() =>
                {
                    ParameterInfo[] parms = property.GetIndexParameters();

                    args ??= new object[parms.Length];

                    for (int i = 0; i < parms.Length; ++i)
                        args[i] = COMServer.Cast(args[i], parms[i].ParameterType);

                    value = COMServer.Cast(TransformValue(value), property.PropertyType);

                    property.SetValue(COMObject, value, args);

                    return value;
                })(),

                EventInfo @event => throw new NotImplementedException(),
                _ => throw new NotImplementedException()
            });
        }

        #endregion
        #region OVERRIDES

        // public override bool TrySetMember(SetMemberBinder binder, object? value)
        // {
        //     SetMember(binder.Name, value);
        // 
        //     return true;
        // }
        // 
        // public override bool TryGetMember(GetMemberBinder binder, out object? result)
        // {
        //     bool success = TryGetMember(binder.Name, out COMData? value);
        // 
        //     result = value?.Data;
        // 
        //     return success;
        // }
        // 
        // public override bool TryGetIndex(GetIndexBinder binder, object?[] indices, out object? result)
        // {
        //     result = GetIndex(indices);
        // 
        //     return true;
        // }
        // 
        // public override bool TrySetIndex(SetIndexBinder binder, object?[] indices, object? value)
        // {
        //     SetIndex(indices, value);
        // 
        //     return true;
        // }
        // 
        // public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
        // {
        //     result = InvokeMember(binder.Name, args);
        // 
        //     return true;
        // }
        // 
        // public override unsafe IEnumerable<string> GetDynamicMemberNames()
        // {
        //     return ObjectType.FindMembers(MemberTypes.Field | MemberTypes.Property | MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance, null, null)
        //                      .Select(o => o.Name);
        // }
        // 
        // public override bool TryConvert(ConvertBinder binder, out object result)
        // {
        //     result = COMObject;
        // 
        //     return true;
        // }

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
