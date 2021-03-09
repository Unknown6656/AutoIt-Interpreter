using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Runtime.ExternalServices;
using Unknown6656.AutoIt3.Common;

namespace Unknown6656.AutoIt3.COM.Server
{
    public sealed class COMServer
        : ExternalServiceProvider<COMServer>
        , ICOMResolver<COMWrapper>
    {
        private readonly ConcurrentDictionary<uint, COMWrapper> _com_objects = new();
        private readonly object _mutex = new object();
        private volatile uint _nextid = 0;

        public uint[] IDsInUse => _com_objects.Keys.ToArray();


        public COMServer() => COMData.RegisterCOMResolver(this);

        public static int Main(string[] args) => Run<COMServer>(args);

        protected override void OnStartup(string[] argv)
        {
        }

        protected override void OnShutdown(bool external_request) => DeleteAllCOMObjects();

        protected override void MainLoop(ref bool shutdown)
        {
            switch (DataReader.ReadNative<COMInteropCommand>())
            {
                case COMInteropCommand.Quit:
                    shutdown = true;

                    break;
                case COMInteropCommand.Create:
                    {
                        string classname = DataReader.ReadString();
                        string? servername = DataReader.ReadNullable();
                        string? username = DataReader.ReadNullable();
                        string? passwd = DataReader.ReadNullable();
                        uint? id = CreateCOMObject(classname, servername, username, passwd);

                        DebugPrint("debug.com.created", classname, username, servername, id);

                        DataWriter.WriteNullable(id);
                    }
                    goto default;
                case COMInteropCommand.EnumerateMembers:
                    {
                        uint id = DataReader.ReadUInt32();
                        string[] members = GetMemberNames(id);

                        DebugPrint("debug.com.enum_members", id, members.Length);

                        DataWriter.Write(members.Length);

                        foreach (string member in members)
                            DataWriter.Write(member);
                    }
                    goto default;
                case COMInteropCommand.GetIndex:
                    {
                        uint id = DataReader.ReadUInt32();
                        COMData index = DataReader.ReadCOM();
                        COMData? value_o = null;
                        bool success = TryResolveCOMObject(id, out COMWrapper com) && com.TryGetIndex(index, out value_o);

                        DebugPrint("debug.com.get_index", index, id, success ? "success" : "fail");

                        DataWriter.Write(success);

                        if (success)
                            DataWriter.WriteCOM(value_o!.Value);
                    }
                    goto default;
                case COMInteropCommand.SetIndex:
                    {
                        uint id = DataReader.ReadUInt32();
                        COMData index = DataReader.ReadCOM();
                        COMData value = DataReader.ReadCOM();
                        bool success = TryResolveCOMObject(id, out COMWrapper com) && com.TrySetIndex(index, value);

                        DebugPrint("debug.com.set_index", index, id, value, success ? "success" : "fail");

                        DataWriter.Write(success);
                    }
                    goto default;
                case COMInteropCommand.GetMember:
                    {
                        uint id = DataReader.ReadUInt32();
                        string name = DataReader.ReadString();
                        COMData? value_o = null;
                        bool success = TryResolveCOMObject(id, out COMWrapper com) && com.TryGetMember(name, out value_o);

                        DebugPrint("debug.com.get_member", name, id, success ? "success" : "fail");

                        DataWriter.Write(success);

                        if (success)
                            DataWriter.WriteCOM(value_o!.Value);
                    }
                    goto default;
                case COMInteropCommand.SetMember:
                    {
                        uint id = DataReader.ReadUInt32();
                        string name = DataReader.ReadString();
                        COMData value = DataReader.ReadCOM();
                        bool success = TryResolveCOMObject(id, out COMWrapper com) && com.TrySetMember(name, value);

                        DebugPrint("debug.com.set_member", name, id, value, success ? "success" : "fail");

                        DataWriter.Write(success);
                    }
                    goto default;
                case COMInteropCommand.Invoke:
                    {
                        uint id = DataReader.ReadUInt32();
                        string name = DataReader.ReadString();
                        COMData[] args = new COMData[DataReader.ReadInt32()];

                        for (int i = 0; i < args.Length; ++i)
                            args[i] = DataReader.ReadCOM();

                        if (TryResolveCOMObject(id, out COMWrapper com) && com.TryInvoke(name, args, out COMData? value_o) && value_o is COMData value)
                        {
                            DebugPrint("debug.com.invoke", name, id, string.Join(", ", args), "success");

                            DataWriter.Write(true);
                            DataWriter.WriteCOM(value);
                        }
                        else
                        {
                            DebugPrint("debug.com.invoke", name, id, string.Join(", ", args), "fail");

                            DataWriter.Write(false);
                        }
                    }
                    goto default;
                case COMInteropCommand.Delete:
                    {
                        uint id = DataReader.ReadUInt32();

                        DebugPrint("debug.com.delete", id);

                        DeleteCOMObject(id);
                    }
                    goto default;
                case COMInteropCommand.DeleteAll:
                    DebugPrint("debug.com.delete_all");

                    DeleteAllCOMObjects();

                    break;
                case COMInteropCommand.GetInfo:
                    {
                        uint id = DataReader.ReadUInt32();
                        COMObjectInfoMode mode = DataReader.ReadNative<COMObjectInfoMode>();
                        string? info = null;

                        if (TryResolveCOMObject(id, out COMWrapper com))
                            com.TryGetInfo(mode, out info);

                        DebugPrint("debug.com.get_info", mode, id, info is null ? "fail" : "success");

                        DataWriter.WriteNullable(info);
                    }
                    goto default;
                case COMInteropCommand.GetAllInfos:
                    {
                        (uint, string, string, COMData)[] objects = IDsInUse.Select<uint, (uint, string, string, COMData)?>(id =>
                                                                             {
                                                                                 if (TryResolveCOMObject(id, out COMWrapper com))
                                                                                 {
                                                                                     com.TryGetInfo(COMObjectInfoMode.OBJ_CLSID, out string? clsid);
                                                                             
                                                                                     return (id, com.ObjectType.FullName, clsid ?? "<void*>", COMData.FromCOMObjectID(id));
                                                                                 }
                                                                             
                                                                                 return null;
                                                                             })
                                                                             .Where(n => n.HasValue)
                                                                             .Select(n => n!.Value)
                                                                             .ToArray();

                        DataWriter.Write(objects.Length);

                        foreach ((uint id, string type, string clsid, COMData com) in objects)
                        {
                            DataWriter.Write(id);
                            DataWriter.Write(type);
                            DataWriter.Write(clsid);
                            DataWriter.WriteCOM(com);
                        }
                    }
                    goto default;
                case COMInteropCommand command:
                    DebugPrint("debug.com.cmd_recv", command);

                    break;
                default:
                    DataWriter.Flush();

                    break;
            }
        }

        public uint AddCOMObject(object raw)
        {
            foreach (uint key in _com_objects.Keys)
                if (ReferenceEquals(_com_objects[key], raw) || ReferenceEquals(_com_objects[key].COMObject, raw))
                    return key;

            COMWrapper cw = raw as COMWrapper ?? new COMWrapper(this, raw);

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
                    raw = new COMWrapper(this, uuid);
                else if (Type.GetTypeFromProgID(classname, server, false) is Type t && Activator.CreateInstance(t) is { } com)
                    raw = com; // TODO : username | passwd

                if (raw is { })
                    return AddCOMObject(raw);
            }
            catch (Exception ex)
            {
                DebugPrint("__raw__", ex.Message);
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

        public COMServer Server { get; }

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

        internal COMWrapper(COMServer server, Guid guid)
            : this(server, Activator.CreateInstance(Type.GetTypeFromCLSID(guid, true)!, true)!)
        {
        }

        internal COMWrapper(COMServer server, object com)
            : this(server, com, com.GetType())
        {
        }

        internal COMWrapper(COMServer server, object com, Type type)
        {
            Server = server;
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

            Server.DebugPrint("debug.com.wrapper.created", (long)IUnknownPtr, ObjectType);
        }

        ~COMWrapper() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        private void Dispose(bool _)
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

                Server.DebugPrint("debug.com.wrapper.disposed", (long)IUnknownPtr, ObjectType);
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
                            args[i] = Unbox(Server.Cast(args[i], parms[i].ParameterType));

                        type = method.ReturnType;
                        value = method.Invoke(method.IsStatic ? null : COMObject, args);
                    }
                    break;
                case PropertyInfo property:
                    {
                        ParameterInfo[] parms = property.GetIndexParameters();

                        args ??= new object[parms.Length];

                        for (int i = 0; i < parms.Length; ++i)
                            args[i] = Unbox(Server.Cast(args[i], parms[i].ParameterType));

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
                        value = Unbox(Server.Cast(value, field.FieldType));

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
                            args[i] = Unbox(Server.Cast(args[i], parms[i].ParameterType));

                        value = Unbox(Server.Cast(value, property.PropertyType));

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

                    COMWrapper wrapper = new COMWrapper(Server, com, type);

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

        public static COMWrapper FromGUID(COMServer server, Guid guid) => new COMWrapper(server, guid);

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
