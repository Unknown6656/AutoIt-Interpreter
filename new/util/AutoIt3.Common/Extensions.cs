using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace Unknown6656.AutoIt3.Common
{
    public static unsafe partial class StreamExtensions
    {
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

        // iterate on interface hierarchy
        public static IEnumerable<Type> GetInterfaceHierarchy(this Type type)
        {
            if (type.IsInterface)
                yield return type;

            foreach (Type t in type.GetInterfaces().OrderByDescending(current => current.GetInterfaces().Count()))
                yield return t;
        }

        // iterate on class hierarchy
        public static IEnumerable<Type> GetClassHierarchy(this Type type)
        {
            if (type is null)
                yield break;

            Type? typeInHierarchy = type;

            do
                yield return typeInHierarchy;
            while ((typeInHierarchy = typeInHierarchy.BaseType) is { IsInterface: false });
        }
    }
}
