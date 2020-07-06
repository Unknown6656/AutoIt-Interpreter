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

    public interface ICOMConverter<T>
    {
        T Read(BinaryReader reader);
        void Write(BinaryWriter writer, T data);
    }

    internal sealed class DefaultCOMConverter<T>
        : ICOMConverter<T>
    {
        public static DefaultCOMConverter<T> Instance { get; } = new DefaultCOMConverter<T>();


        private DefaultCOMConverter()
        {
        }

        public T Read(BinaryReader reader) => throw new NotImplementedException();

        public void Write(BinaryWriter writer, T data) => throw new NotImplementedException();
    }

    public readonly struct COMData<T>
    {
        private readonly COMDataType _type;


        public static ICOMConverter<T> Converter { get; set; } = DefaultCOMConverter<T>.Instance;

        public static COMData<T> Null { get; } = new COMData<T>(COMDataType.Null, null);

        public readonly object? Data { get; }


        private COMData(COMDataType type, object? data)
        {
            _type = type;
            Data = data;
        }

        public readonly bool IsBool(out bool value)
        {
            value = default;

            bool res = _type is COMDataType.Bool;

            if (res && Data is bool v)
                value = v;

            return res;
        }

        public readonly bool IsByte(out byte value)
        {
            value = default;

            bool res = _type is COMDataType.Byte;

            if (res && Data is byte v)
                value = v;

            return res;
        }

        public readonly bool IsShort(out short value)
        {
            value = default;

            bool res = _type is COMDataType.Short;

            if (res && Data is short v)
                value = v;

            return res;
        }

        public readonly bool IsInt(out int value)
        {
            value = default;

            bool res = _type is COMDataType.Int;

            if (res && Data is int v)
                value = v;

            return res;
        }

        public readonly bool IsLong(out long value)
        {
            value = default;

            bool res = _type is COMDataType.Long;

            if (res && Data is long v)
                value = v;

            return res;
        }

        public readonly bool IsFloat(out float value)
        {
            value = default;

            bool res = _type is COMDataType.Float;

            if (res && Data is float v)
                value = v;

            return res;
        }

        public readonly bool IsDouble(out double value)
        {
            value = default;

            bool res = _type is COMDataType.Double;

            if (res && Data is double v)
                value = v;

            return res;
        }

        public readonly bool IsString(out string? value)
        {
            value = default;

            bool res = _type is COMDataType.String;

            if (res && Data is string v)
                value = v;

            return res;
        }

        public readonly bool IsArray(out COMData<T>[]? value)
        {
            value = default;

            bool res = _type is COMDataType.Array;

            if (res && Data is COMData<T>[] v)
                value = v;

            return res;
        }

        public readonly void Serialize(BinaryWriter writer)
        {
            writer.WriteNative(_type);

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
            else if (IsArray(out COMData<T>[]? arr))
            {
                writer.Write(arr!.Length);

                for (int idx = 0; idx < arr.Length; ++idx)
                    arr[idx].Serialize(writer);
            }
            else if (_type is COMDataType.COM)
                Converter.Write(writer, (T)Data!);
        }

        public static COMData<T> Deserialize(BinaryReader reader)
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
                    data = Converter.Read(reader);

                    break;
                case COMDataType.Array:
                    COMData<T>[] arr = new COMData<T>[reader.ReadInt32()];

                    for (int i = 0; i < arr.Length; ++i)
                        arr[i] = Deserialize(reader);

                    data = arr;

                    break;
                case COMDataType.Null:
                default:
                    data = null;
                    break;
            }

            return new COMData<T>(type, data);
        }

        public static COMData<T> FromBool(bool value) => new COMData<T>(COMDataType.Bool, value);

        public static COMData<T> FromInt(int value) => new COMData<T>(COMDataType.Int, value);

        public static COMData<T> FromByte(byte value) => new COMData<T>(COMDataType.Byte, value);

        public static COMData<T> FromShort(short value) => new COMData<T>(COMDataType.Short, value);

        public static COMData<T> FromLong(long value) => new COMData<T>(COMDataType.Long, value);

        public static COMData<T> FromFloat(float value) => new COMData<T>(COMDataType.Float, value);

        public static COMData<T> FromDouble(double value) => new COMData<T>(COMDataType.Double, value);

        public static COMData<T> FromString(string value) => new COMData<T>(COMDataType.String, value);

        public static COMData<T> FromArray(IEnumerable value) => new COMData<T>(COMDataType.String, value.Cast<object?>().Select(FromObject).ToArray());

        public static COMData<T> FromObject(object? value) => value switch
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
            object o => new COMData<T>(COMDataType.COM, o)
        };
    }

    public static unsafe class StreamExtensions
    {
        public static void WriteCOM<T>(this BinaryWriter writer, COMData<T> data) => data.Serialize(writer);

        public static COMData<T> ReadCOM<T>(this BinaryReader reader) => COMData<T>.Deserialize(reader);

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
