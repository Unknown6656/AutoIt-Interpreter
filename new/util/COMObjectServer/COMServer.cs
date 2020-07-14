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
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unknown6656.AutoIt3.COM.Server
{
    public static class Program
    {
        private static BinaryWriter? _debug_writer = null;
        private static volatile bool _running = true;

        public static COMServer Server { get; } = new COMServer();

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

                                Server.DeleteAllCOMObjects();
                                _running = false;

                                break;
                            case COMInteropCommand.Create:
                                {
                                    string classname = reader.ReadString();
                                    string? servername = reader.ReadNullable();
                                    string? username = reader.ReadNullable();
                                    string? passwd = reader.ReadNullable();
                                    uint? id = Server.CreateCOMObject(classname, servername, username, passwd);

                                    DebugPrint($"Created COM object '{classname}' on '{username}'@'{servername}'. COM object ID: {id}");

                                    writer.WriteNullable(id);
                                }
                                goto default;
                            case COMInteropCommand.EnumerateMembers:
                                {
                                    uint id = reader.ReadUInt32();
                                    string[] members = Server.GetMemberNames(id);

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
                                    bool success = Server.TryResolveCOMObject(id, out COMWrapper com) && com.TryGetIndex(index, out value_o);

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
                                    bool success = Server.TryResolveCOMObject(id, out COMWrapper com) && com.TrySetIndex(index, value);

                                    DebugPrint($"Setting index '{index}' of COM object '{id}' to '{value}': {(success ? "success" : "fail")}.");

                                    writer.Write(success);
                                }
                                goto default;
                            case COMInteropCommand.GetMember:
                                {
                                    uint id = reader.ReadUInt32();
                                    string name = reader.ReadString();
                                    COMData? value_o = null;
                                    bool success = Server.TryResolveCOMObject(id, out COMWrapper com) && com.TryGetMember(name, out value_o);

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
                                    bool success = Server.TryResolveCOMObject(id, out COMWrapper com) && com.TrySetMember(name, value);

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

                                    if (Server.TryResolveCOMObject(id, out COMWrapper com) && com.TryInvoke(name, args, out COMData? value_o) && value_o is COMData value)
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

                                    Server.DeleteCOMObject(id);
                                }
                                goto default;
                            case COMInteropCommand.DeleteAll:
                                DebugPrint($"Deleting all COM objects.");

                                Server.DeleteAllCOMObjects();

                                break;
                            case COMInteropCommand.GetInfo:
                                {
                                    uint id = reader.ReadUInt32();
                                    COMObjectInfoMode mode = reader.ReadNative<COMObjectInfoMode>();
                                    string? info = null;
                                    
                                    if (Server.TryResolveCOMObject(id, out COMWrapper com))
                                        com.TryGetInfo(mode, out info);

                                    DebugPrint($"Fetching info '{mode}' of COM object '{id}': {(info is null ? "fail" : "success")}.");

                                    writer.WriteNullable(info);
                                }
                                goto default;
                            case COMInteropCommand.GetAllInfos:
                                {
                                    (uint, string, string, COMData)[] objects = Server.IDsInUse
                                                                                      .Select<uint, (uint, string, string, COMData)?>(id =>
                                                                                      {
                                                                                          if (Server.TryResolveCOMObject(id, out COMWrapper com))
                                                                                          {
                                                                                              com.TryGetInfo(COMObjectInfoMode.OBJ_CLSID, out string? clsid);

                                                                                              return (id, com.ObjectType.FullName, clsid ?? "<void*>", COMData.FromCOMObjectID(id));
                                                                                          }

                                                                                          return null;
                                                                                      })
                                                                                      .Where(n => n.HasValue)
                                                                                      .Select(n => n!.Value)
                                                                                      .ToArray();

                                    writer.Write(objects.Length);

                                    foreach ((uint id, string type, string clsid, COMData com) in objects)
                                    {
                                        writer.Write(id);
                                        writer.Write(type);
                                        writer.Write(clsid);
                                        writer.WriteCOM(com);
                                    }
                                }
                                goto default;
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
                    Server.DeleteAllCOMObjects();
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
        private readonly object _mutex = new object();
        private volatile uint _nextid = 0;

        public uint[] IDsInUse => _com_objects.Keys.ToArray();


        public COMServer() => COMData.RegisterCOMResolver(this);

        public uint AddCOMObject(object raw)
        {
            foreach (uint key in _com_objects.Keys)
                if (ReferenceEquals(_com_objects[key], raw) || ReferenceEquals(_com_objects[key].COMObject, raw))
                    return key;

            COMWrapper cw = raw as COMWrapper ?? new COMWrapper(raw);

            lock(_mutex)
            {
                uint id = ++_nextid;

                _com_objects[id] = cw;

                return id;
            }
        }

        public uint GetCOMObjectID(COMWrapper com_object) => AddCOMObject(com_object);

        public uint? CreateCOMObject(string classname, string? server = null, string? user = null, string? passwd = null)
        {
            try
            {
                object? raw = null;

                if (Guid.TryParse(classname, out Guid uuid))
                    raw = new COMWrapper(uuid);
                else if (Type.GetTypeFromProgID(classname, server, false) is Type t && Activator.CreateInstance(t) is { } com)
                    raw = com; // TODO : username | passwd

                if (raw is { })
                    return AddCOMObject(raw);
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

        internal object? Cast(object? value, Type target_type)
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
            else if ((value?.GetType()?.IsClass ?? false) && target_type == typeof(COMWrapper))
                return _com_objects[AddCOMObject(value)];
            else
                // TODO : array conversion

                return Convert.ChangeType(value, target_type);
        }
    }

    [DebuggerDisplay("{" + nameof(ObjectType) + "}")]
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

        internal object? COMObject { get; private set; }

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
            : this(com, com.GetType())
        {
        }

        internal COMWrapper(object com, Type type)
        {
            COMObject = com;
            ObjectType = type;
            IUnknownPtr = Marshal.GetIUnknownForObject(com);

            List<MemberInfo> members = new List<MemberInfo>();

            do
            {
                members.AddRange(type.FindMembers(
                    MemberTypes.Field | MemberTypes.Property | MemberTypes.Event | MemberTypes.Method,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                    (_, _) => true,
                    null
                ));

                type = type.BaseType;
            }
            while (type is { } && type != typeof(object));

            _cached_members = members.ToArray();

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
                if (FindMembers(INDEX_NAME, MemberFindMode.Getter) is { Count: > 0 } match)
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
                if (FindMembers(INDEX_NAME, MemberFindMode.Setter) is { Count: > 0 } match)
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
                if (FindMembers(name, MemberFindMode.Getter) is { Count: > 0 } match)
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
                if (FindMembers(name, MemberFindMode.Setter) is { Count: > 0 } match)
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
                if (FindMembers(name, MemberFindMode.Regular) is { Count: > 0 } match)
                {
                    object?[] args = arguments.Select(arg => arg.Data).ToArray();

                    // TODO : overload resolution

                    object? data = GetMember(match[0], args);

                    value = COMData.FromArbitrary(data);

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public bool TryGetInfo(COMObjectInfoMode mode, out string? info)
        {
            info = null;

            Guid clsid = default;

            try
            {
                ((IPersist?)COMObject)?.GetClassID(out clsid);
            }
            catch
            {
            }

            switch (mode)
            {
                case COMObjectInfoMode.OBJ_NAME:
                    info = ObjectType.Name;

                    break;
                case COMObjectInfoMode.OBJ_STRING:
                    // TODO : implement

                    break;
                case COMObjectInfoMode.OBJ_PROGID:
                    if (clsid != default)
                        NativeInterop.ProgIDFromCLSID(&clsid, out info);

                    break;
                case COMObjectInfoMode.OBJ_FILE:
                    info = ObjectType.Assembly.Location;

                    break;
                case COMObjectInfoMode.OBJ_MODULE:
                    info = ObjectType.Module.FullyQualifiedName;

                    break;
                case COMObjectInfoMode.OBJ_CLSID:
                case COMObjectInfoMode.OBJ_IID:
                    info = clsid != default ? clsid.ToString() : (ObjectType.GetInterfaceHierarchy().FirstOrDefault()?.GUID.ToString());

                    break;
            }

            return info is { };
        }

        private List<MemberInfo> FindMembers(string name, MemberFindMode mode)
        {
            List<MemberInfo> members = _cached_members.Where(m => m.Name == name).ToList();

            if (members.Count == 0)
                members.AddRange(_cached_members.Where(m => string.Equals(name, m.Name, StringComparison.InvariantCultureIgnoreCase)));

            if (members.Count == 0)
            {
                Regex regex = new Regex("^(" + string.Join("|", new[]
                {
                    (MemberFindMode.EventAdd, "add_"),
                    (MemberFindMode.EventRemove, "remove_"),
                    (MemberFindMode.Setter, "set_"),
                    (MemberFindMode.Getter, "get_"),
                }.Where(t => mode.HasFlag(t.Item1)).Select(t => t.Item2)) + ")?" + name + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

                members.AddRange(_cached_members.Where(m => regex.IsMatch(m.Name)));
            }

            return members;
        }

        private object? GetMember(MemberInfo member, object?[]? args = null)
        {
            object? value;
            Type type;

            switch (member)
            {
                case FieldInfo field:
                    type = field.FieldType;
                    value = field.GetValue(field.IsStatic ? null : COMObject);

                    break;
                case MethodInfo method:
                    {
                        ParameterInfo[] parms = method.GetParameters();

                        args ??= new object[parms.Length];

                        for (int i = 0; i < parms.Length; ++i)
                            args[i] = Unbox(Program.Server.Cast(args[i], parms[i].ParameterType));

                        type = method.ReturnType;
                        value = method.Invoke(method.IsStatic ? null : COMObject, args);
                    }
                    break;
                case PropertyInfo property:
                    {
                        ParameterInfo[] parms = property.GetIndexParameters();

                        args ??= new object[parms.Length];

                        for (int i = 0; i < parms.Length; ++i)
                            args[i] = Unbox(Program.Server.Cast(args[i], parms[i].ParameterType));

                        type = property.PropertyType;

                        if (args.Length == 0)
                            value = property.GetValue(COMObject);
                        else
                            value = property.GetValue(COMObject, args);
                    }
                    break;
                case EventInfo @event:
                default:
                    throw new NotImplementedException();
            }

            return TransformValue(value, type);
        }

        private void SetMember(MemberInfo member, object? value, object?[]? args = null)
        {
            switch (member)
            {
                case FieldInfo field:
                    {
                        value = Unbox(Program.Server.Cast(value, field.FieldType));

                        field.SetValue(field.IsStatic ? null : COMObject, value);
                    }
                    return;
                case MethodInfo method:
                    GetMember(method, (args is null ? new[] { value } : args.Append(value)).ToArray());

                    return;
                case PropertyInfo property:
                    {
                        ParameterInfo[] parms = property.GetIndexParameters();

                        args ??= new object[parms.Length];

                        for (int i = 0; i < parms.Length; ++i)
                            args[i] = Unbox(Program.Server.Cast(args[i], parms[i].ParameterType));

                        value = Unbox(Program.Server.Cast(value, property.PropertyType));

                        if (args.Length == 0)
                            property.SetValue(COMObject, value);
                        else
                            property.SetValue(COMObject, value, args);
                    }
                    return;
                case EventInfo @event:
                default:
                    throw new NotImplementedException();
            }
        }

        #endregion
        #region OVERRIDES

        public override int GetHashCode() => COMObject?.GetHashCode() ?? 0;

        public override bool Equals(object? obj) => ReferenceEquals(this, obj) || (obj is COMWrapper cw && Equals(cw));

        public bool Equals(COMWrapper? other) => Equals(COMObject, other?.COMObject);

        #endregion

        private object? Unbox(object? value) => value switch
        {
            COMWrapper o => o.COMObject,
            IEnumerable a when !IsPrimitive(a) => a.Cast<object>().Select(Unbox).ToArray(),
            //object com when IsComObject(com) => new Func<COMWrapper>(delegate
            //{
            //    if (_objects.FirstOrDefault(w => ReferenceEquals(w.COMObject, com)) is COMWrapper w)
            //        return w;
            //    COMWrapper wrapper = new COMWrapper(com);
            //    _objects.Add(wrapper);
            //    return wrapper;
            //})(),
            _ => value,
        };

        private object? TransformValue(object? value, Type type)
        {
            switch (value)
            {
                case object com when IsComObject(com):
                    if (_objects.FirstOrDefault(w => ReferenceEquals(w.COMObject, com)) is COMWrapper w)
                        return w;

                    // TODO : force typecast for [com]

                    COMWrapper wrapper = new COMWrapper(com, type);

                    _objects.Add(wrapper);

                    return wrapper;
                case IEnumerable enumerable when !IsPrimitive(enumerable):
                    object?[] array = enumerable.Cast<object>().ToArray();
                    Type? ctype = null;

                    foreach (object? obj in array)
                        if (obj?.GetType() is Type t)
                            ctype = (ctype is null ? t : (ctype.GetCommonBaseClass(t) ?? ctype.GetCommonInterface(t))) ?? typeof(object);

                    ctype ??= typeof(object);

                    for (int i = 0; i < array.Length; ++i)
                        array[i] = TransformValue(array[i], ctype);

                    return array;
                default:
                    return value;
            }
        }

        public static COMWrapper FromGUID(Guid guid) => new COMWrapper(guid);

        private static bool IsComObject(object o) => o.GetType().Name == "__ComObject";

        private static bool IsPrimitive(object o)
        {
            Type t = o.GetType();

            return t.IsPrimitive || t.IsValueType || t == typeof(string);
        }
    }

    [Flags]
    public enum MemberFindMode
    {
        Regular = 0,
        Getter = 1,
        Setter = 2,
        EventAdd = 4,
        EventRemove = 8,
    }
}
