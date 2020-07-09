using System.Diagnostics;
using System.Collections;
using System.Linq;
using System.IO;
using System;

namespace Unknown6656.AutoIt3.COM
{
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
        bool TryResolveCOMObject(uint id, out T com_object);
    }

    [DebuggerDisplay("{" + nameof(Type) + "}: {" + nameof(Data) + "}")]
    public readonly struct COMData
    {
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

        public readonly bool IsCOM<T>(ICOMResolver<T> resolver, out T com_object)
        {
            com_object = default;

            bool res = IsCOM();

            if (res && Data is uint id)
                res &= resolver.TryResolveCOMObject(id, out com_object);

            return res;
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
            _ => throw new NotImplementedException(),
        };
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
}
