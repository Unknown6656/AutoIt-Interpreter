using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System;

namespace Unknown6656.AutoIt3.COM
{
    public enum COMObjectInfoMode
    {
        OBJ_NAME = 1,
        OBJ_STRING = 2,
        OBJ_PROGID = 3,
        OBJ_FILE = 4,
        OBJ_MODULE = 5,
        OBJ_CLSID = 6,
        OBJ_IID = 7,
    }

    public enum COMInteropCommand
        : byte
    {
        _none_ = 0,
        Create = 1,
        Delete = 2,
        DeleteAll = 3,
        EnumerateMembers = 4,
        GetIndex = 5,
        SetIndex = 6,
        GetMember = 7,
        SetMember = 8,
        Invoke = 9,
        GetInfo = 10,
        GetAllInfos = 11,

        Quit = 255,
    }

    public enum COMDataType
        : byte
    {
        Null,
        Bool,
        Int,
        Byte,
        Short,
        Long,
        Float,
        Double,
        String,
        COM,
        Array,
    }

    public interface ICOMResolver<T>
    {
        uint GetCOMObjectID(T com_object);
        bool TryResolveCOMObject(uint id, out T com_object);
    }

    public delegate bool COMResolver<T>(uint id, out T com);

    [DebuggerDisplay("{" + nameof(Type) + "}: {" + nameof(Data) + "}")]
    public readonly struct COMData
    {
        private static readonly List<(Type expected, Func<object, uint> func)> _converters_from_com = new();
        private static readonly List<COMResolver<object>> _converters_to_com = new();


        //public static ICOMConverter<T> Converter { get; set; } = DefaultCOMConverter<T>.Instance;

        public static COMData Null { get; } = new COMData(COMDataType.Null, null);

        public readonly bool IsNull => Type is COMDataType.Null;

        public readonly COMDataType Type { get; }

        public readonly object? Data { get; }


        private COMData(COMDataType type, object? data)
        {
            Type = type;
            Data = data;
        }

        public readonly bool IsBool(out bool value)
        {
            value = default;

            bool res = Type is COMDataType.Bool;

            if (res && Data is bool v)
                value = v;

            return res;
        }

        public readonly bool IsByte(out byte value)
        {
            value = default;

            bool res = Type is COMDataType.Byte;

            if (res && Data is byte v)
                value = v;

            return res;
        }

        public readonly bool IsShort(out short value)
        {
            value = default;

            bool res = Type is COMDataType.Short;

            if (res && Data is short v)
                value = v;

            return res;
        }

        public readonly bool IsInt(out int value)
        {
            value = default;

            bool res = Type is COMDataType.Int;

            if (res && Data is int v)
                value = v;

            return res;
        }

        public readonly bool IsLong(out long value)
        {
            value = default;

            bool res = Type is COMDataType.Long;

            if (res && Data is long v)
                value = v;

            return res;
        }

        public readonly bool IsFloat(out float value)
        {
            value = default;

            bool res = Type is COMDataType.Float;

            if (res && Data is float v)
                value = v;

            return res;
        }

        public readonly bool IsDouble(out double value)
        {
            value = default;

            bool res = Type is COMDataType.Double;

            if (res && Data is double v)
                value = v;

            return res;
        }

        public readonly bool IsString(out string? value)
        {
            value = default;

            bool res = Type is COMDataType.String;

            if (res && Data is string v)
                value = v;

            return res;
        }

        public readonly bool IsArray(out COMData[]? value)
        {
            value = default;

            bool res = Type is COMDataType.Array;

            if (res && Data is COMData[] v)
                value = v;

            return res;
        }

        public readonly bool IsCOM() => Type is COMDataType.COM;

        public readonly bool IsCOM<T>(out T com_object)
        {
            com_object = default!;

            if (IsCOM() && Data is uint id)
                foreach (COMResolver<object> resolver in _converters_to_com)
                    if (resolver(id, out object com))
                        try
                        {
                            com_object = (T)com;

                            return true;
                        }
                        catch
                        {
                        }

            return false;
        }

        public readonly void Serialize(BinaryWriter writer)
        {
            writer.WriteNative(Type);

            if (IsBool(out bool b))
                writer.Write(b);
            else if (IsByte(out byte by))
                writer.Write(by);
            else if (IsShort(out short s))
                writer.Write(s);
            else if (IsInt(out int i))
                writer.Write(i);
            else if (IsLong(out long l))
                writer.Write(l);
            else if (IsFloat(out float f))
                writer.Write(f);
            else if (IsDouble(out double d))
                writer.Write(d);
            else if (IsString(out string? str))
                writer.Write(str);
            else if (IsArray(out COMData[]? arr))
            {
                writer.Write(arr!.Length);

                for (int idx = 0; idx < arr.Length; ++idx)
                    arr[idx].Serialize(writer);
            }
            else if (Type is COMDataType.COM)
                writer.Write((uint)Data!);
        }

        public static COMData Deserialize(BinaryReader reader)
        {
            COMDataType type = reader.ReadNative<COMDataType>();
            object? data;

            switch (type)
            {
                case COMDataType.Bool:
                    data = reader.ReadBoolean();

                    break;
                case COMDataType.Int:
                    data = reader.ReadInt32();

                    break;
                case COMDataType.Byte:
                    data = reader.ReadByte();

                    break;
                case COMDataType.Short:
                    data = reader.ReadInt16();

                    break;
                case COMDataType.Long:
                    data = reader.ReadInt64();

                    break;
                case COMDataType.Float:
                    data = reader.ReadSingle();

                    break;
                case COMDataType.Double:
                    data = reader.ReadDouble();

                    break;
                case COMDataType.String:
                    data = reader.ReadString();

                    break;
                case COMDataType.COM:
                    data = reader.ReadUInt32();

                    break;
                case COMDataType.Array:
                    COMData[] arr = new COMData[reader.ReadInt32()];

                    for (int i = 0; i < arr.Length; ++i)
                        arr[i] = Deserialize(reader);

                    data = arr;

                    break;
                case COMDataType.Null:
                default:
                    data = null;
                    break;
            }

            return new COMData(type, data);
        }

        public static COMData FromBool(bool value) => new COMData(COMDataType.Bool, value);

        public static COMData FromInt(int value) => new COMData(COMDataType.Int, value);

        public static COMData FromByte(byte value) => new COMData(COMDataType.Byte, value);

        public static COMData FromShort(short value) => new COMData(COMDataType.Short, value);

        public static COMData FromLong(long value) => new COMData(COMDataType.Long, value);

        public static COMData FromFloat(float value) => new COMData(COMDataType.Float, value);

        public static COMData FromDouble(double value) => new COMData(COMDataType.Double, value);

        public static COMData FromString(string value) => new COMData(COMDataType.String, value);

        public static COMData FromArray(IEnumerable value) => new COMData(COMDataType.String, value.Cast<object?>().Select(FromArbitrary).ToArray());

        public static COMData FromCOMObjectID(uint id) => new COMData(COMDataType.COM, id);

        public static COMData FromArbitrary(object? value) => value switch
        {
            null => Null,
            bool b => FromBool(b),
            byte b => FromByte(b),
            sbyte sb => FromByte((byte)sb),
            short s => FromShort(s),
            ushort us => FromShort((short)us),
            char c => FromShort((short)c),
            int i => FromInt(i),
            uint u => FromInt((int)u),
            long l => FromLong(l),
            ulong ul => FromLong((long)ul),
            float f => FromFloat(f),
            double d => FromDouble(d),
            string s => FromString(s),
            IEnumerable a => FromArray(a),
            COMData com => com,
            object obj => new Func<COMData>(delegate
            {
                foreach ((Type expected, Func<object, uint> converter) in _converters_from_com)
                    if (expected.IsAssignableFrom(obj.GetType()))
                        return FromCOMObjectID(converter(obj));

                throw new NotImplementedException();
            })(),
        };

        public static void RegisterCOMResolverMethods<T>(Func<T, uint> from_com, COMResolver<T> to_com)
        {
            _converters_from_com.Add((typeof(T), o => from_com((T)o)));
            _converters_to_com.Add(new COMResolver<object>((uint id, out object o) =>
            {
                o = null!;

                if (to_com(id, out T obj))
                    o = obj;

                return o is { };
            }));
        }

        public static void RegisterCOMResolver<T>(ICOMResolver<T> com_resolver) => RegisterCOMResolverMethods<T>(com_resolver.GetCOMObjectID, com_resolver.TryResolveCOMObject);
    }

    public static unsafe class StreamExtensions
    {
        public static void WriteCOM(this BinaryWriter writer, COMData data) => data.Serialize(writer);

        public static COMData ReadCOM(this BinaryReader reader) => COMData.Deserialize(reader);

        public static void WriteNullable<T>(this BinaryWriter writer, T? data) where T : unmanaged
        {
            if (data is T t)
            {
                writer.Write(true);
                writer.WriteNative(t);
            }
            else
                writer.Write(false);
        }

        public static T? ReadNullable<T>(this BinaryReader reader) where T : unmanaged
        {
            if (reader.ReadBoolean())
                return reader.ReadNative<T>();

            return null;
        }

        public static void WriteNullable(this BinaryWriter writer, string? data)
        {
            if (data is string s)
            {
                writer.Write(true);
                writer.Write(s);
            }
            else
                writer.Write(false);
        }

        public static string? ReadNullable(this BinaryReader reader)
        {
            if (reader.ReadBoolean())
                return reader.ReadString();

            return null;
        }

        public static void WriteNative<T>(this BinaryWriter writer, T data) where T : unmanaged
        {
            byte* ptr = (byte*)&data;
            byte[] bytes = new byte[sizeof(T)];

            for (int i = 0; i < bytes.Length; ++i)
                bytes[i] = ptr[i];

            writer.Write(bytes, 0, bytes.Length);
        }

        public static T ReadNative<T>(this BinaryReader reader) where T : unmanaged
        {
            T data = default;
            byte[] bytes = new byte[sizeof(T)];

            reader.Read(bytes, 0, bytes.Length);

            for (int i = 0; i < bytes.Length; ++i)
                *((byte*)&data + i) = bytes[i];

            return data;
        }
    }

    public static class TypeExtensions
    {
        public static Type? FindAssignableWith(this Type? typeLeft, Type? typeRight)
        {
            if (typeLeft is null || typeRight is null)
                return null;

            Type commonBaseClass = typeLeft.GetCommonBaseClass(typeRight) ?? typeof(object);

            return commonBaseClass.Equals(typeof(object))
                    ? typeLeft.GetCommonInterface(typeRight)
                    : commonBaseClass;
        }

        // searching for common base class (either concrete or abstract)
        public static Type? GetCommonBaseClass(this Type? typeLeft, Type? typeRight) =>
            typeLeft is { } && typeRight is { } ? typeLeft.GetClassHierarchy()
                                                          .Intersect(typeRight.GetClassHierarchy())
                                                          .FirstOrDefault(type => !type.IsInterface) : null;

        // searching for common implemented interface
        // it's possible for one class to implement multiple interfaces, 
        // in this case return first common based interface
        public static Type? GetCommonInterface(this Type? typeLeft, Type? typeRight) =>
            typeLeft is { } && typeRight is { } ? typeLeft.GetInterfaceHierarchy()
                                                          .Intersect(typeRight.GetInterfaceHierarchy())
                                                          .FirstOrDefault() : null;

        // iterate on interface hierarhy
        public static IEnumerable<Type> GetInterfaceHierarchy(this Type type)
        {
            if (type.IsInterface)
                yield return type;

            foreach (Type t in type.GetInterfaces().OrderByDescending(current => current.GetInterfaces().Count()))
                yield return t;
        }

        // interate on class hierarhy
        public static IEnumerable<Type> GetClassHierarchy(this Type type)
        {
            if (type is null)
                yield break;

            Type? typeInHierarchy = type;

            do
                yield return typeInHierarchy;
            while ((typeInHierarchy = typeInHierarchy.BaseType) is { IsInterface : false });
        }
    }
}

namespace System.Runtime.CompilerServices
{
    /// <summary>For C#9 record compatiblity.</summary>
    [EditorBrowsable(EditorBrowsableState.Never), DebuggerNonUserCode]
    public sealed class IsExternalInit { }
}
