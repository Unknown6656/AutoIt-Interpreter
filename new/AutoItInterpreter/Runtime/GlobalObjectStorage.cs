using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;

using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    /// <summary>
    /// Represents an object storage unit for global .NET objects.
    /// </summary>
    public sealed class GlobalObjectStorage
        : IDisposable
    {
        private static volatile nint _id = 0;
        private readonly ConcurrentDictionary<nint, object> _objects = new();


        /// <summary>
        /// The AutoIt interpreter associated with the current global object storage.
        /// </summary>
        public Interpreter Interpreter { get; }

        public ReadOnlyIndexer<Variant, Type?> NETType { get; }

        /// <summary>
        /// A list of all currently unique handles in use. Each handle is associated with a global .NET object.
        /// </summary>
        public Variant[] HandlesInUse => _objects.Keys.Select(Variant.FromHandle).ToArray();

        /// <summary>
        /// The count of current active .NET objects (this is equivalent with the count of <see cref="HandlesInUse"/>).
        /// </summary>
        public int ObjectCount => _objects.Count;

        internal IEnumerable<object> Objects => _objects.Values;


        internal GlobalObjectStorage(Interpreter interpreter)
        {
            Interpreter = interpreter;
            NETType = new(h => TryGet(h, out object? item) ? item?.GetType() : null);
        }

        private Variant GetFreeId()
        {
            do
                ++_id;
            while (_objects.Keys.Contains(_id));

            return Variant.FromHandle(_id);
        }

        /// <summary>
        /// Stores the given .NET object into the global object storage and returns the handle associated with the stored object.
        /// </summary>
        /// <typeparam name="T">The generic type of the item (constrained to <see langword="class"/>).</typeparam>
        /// <param name="item">The item to be stored.</param>
        /// <returns>The handle associated with the stored object.</returns>
        public Variant Store<T>(T? item)
            where T : class
        {
            if (item is null)
                return Variant.Zero;

            Variant handle = GetFreeId();

            TryUpdate(handle, item);

            return handle;
        }

        public Variant GetOrStore<T>(T item)
            where T : class
        {
            if (!TryGetHandle(item, out Variant handle))
                handle = Store(item);

            return handle;
        }

        public bool TryGetHandle<T>(T? item, out Variant handle)
            where T : class
        {
            nint key = item is { } ? _objects.FirstOrDefault(kvp => ReferenceEquals(item, kvp.Value)).Key : 0;

            handle = key == 0 ? Variant.Zero : Variant.FromHandle(key);

            return key != 0;
        }

        public bool Contains<T>(T? item) where T : class => item is { } && _objects.Values.Contains(item);

        public bool Contains(Variant handle) => _objects.ContainsKey((nint)handle);

        public bool TryGet(Variant handle, [MaybeNullWhen(false), NotNullWhen(true)] out object? item) => _objects.TryGetValue((nint)handle, out item);

        /// <summary>
        /// Resolves the given handle to the .NET object with the given type.
        /// </summary>
        /// <typeparam name="T">Type of the object to be resolved (constrained to <see langword="class"/>).</typeparam>
        /// <param name="handle">The handle to be resolved.</param>
        /// <param name="item">The resolved object (or <see langword="null"/> if the object could not be resolved and converted to <typeparamref name="T"/>).</param>
        /// <returns>Indicates whether the object resolution <i>and</i> type conversion were successful.</returns>
        public bool TryGet<T>(Variant handle, out T? item)
            where T : class
        {
            bool res = TryGet(handle, out object? value);

            item = value as T;

            return res;
        }

        public bool TryUpdate<T>(Variant handle, T item)
            where T : class
        {
            bool success;

            if (success = (handle.Type is VariantType.Handle))
                _objects[(nint)handle] = item;

            return success;
        }

        public (Variant Handle, T Value)[] GetAllInstancesOfType<T>() => (from kvp in _objects.ToArray()
                                                                          where kvp.Value is T
                                                                          select (Variant.FromHandle(kvp.Key), (T)kvp.Value)).ToArray();

        public void Dispose()
        {
            foreach (Variant handle in HandlesInUse)
                Delete(handle);
        }

        /// <summary>
        /// Deletes the object associated with the given object handle and returns whether the deletion was successful.
        /// </summary>
        /// <param name="handle">The handle associated with the object to be deleted.</param>
        /// <returns>Deletion result. <see langword="true"/> indicates that the associated object has been found and deleted. Otherwise <see langword="false"/>.</returns>
        public bool Delete(Variant handle) => handle.Type is VariantType.Handle && Delete((nint)handle);

        private bool Delete(nint id)
        {
            bool success = _objects.TryRemove(id, out object? obj);

            if (success && obj is IDisposable disp)
                disp.Dispose();

            return success;
        }

        public bool TryCreateNETStaticRefrence(string type, out Variant reference)
        {
            try
            {
                if (Type.GetType(type) is Type t)
                {
                    StaticTypeReference stat_type = new(t);

                    if ((from key in _objects.Keys
                         let val = _objects[key]
                         where stat_type.Equals(val)
                         select key).FirstOrDefault() is nint id and > 0)
                        reference = Variant.FromHandle(id);
                    else
                        reference = Store(stat_type);

                    return true;
                }
            }
            catch
            {
            }

            reference = Variant.Null;

            return false;
        }

        public bool TryCreateNETObject(Variant type, Variant[] arguments, out Variant reference)
        {
            try
            {
                Type? t = type.TryResolveHandle(Interpreter, out StaticTypeReference? tref) ? tref.Type : Type.GetType(type.ToString());

                if (t is { } && (from ctor in t.GetConstructors()
                                 let pars = ctor.GetParameters()
                                 let min_parcount = pars.Count(p => !p.HasDefaultValue)
                                 where min_parcount <= arguments.Length
                                 orderby pars.Length descending
                                 let args = arguments.Take(Math.Min(pars.Length, arguments.Length))
                                 select new
                                 {
                                     Constructor = ctor,
                                     Arguments = args.ToArray((a, i) => a.ToCPPObject(pars[i].ParameterType, Interpreter))
                                 }).FirstOrDefault() is { } best_match)
                {
                    object instance = best_match.Constructor.Invoke(best_match.Arguments);

                    reference = Store(instance);

                    return true;
                }
            }
            catch
            {
            }

            reference = Variant.Null;

            return false;
        }

        public bool TrySetNETIndex(object instance, Variant index, Variant value) => TryInvokeNETMember(instance, "set_Item", new[] { index, value }, out _);

        public bool TryGetNETIndex(object instance, Variant index, out Variant value) => TryInvokeNETMember(instance, "get_Item", new[] { index }, out value);

        public bool TrySetNETMember(object instance, string member, Variant value)
        {
            try
            {
                if (GetMembers(instance).Where(m => m.Name == member).FirstOrDefault() is { } m)
                    if (m is FieldInfo field)
                    {
                        object? netvalue = value.ToCPPObject(field.FieldType, Interpreter);

                        field.SetValue(instance is StaticTypeReference ? null : instance, netvalue);

                        return true;
                    }
                    else if (m is PropertyInfo property)
                    {
                        object? netvalue = value.ToCPPObject(property.PropertyType, Interpreter);

                        property.SetValue(instance is StaticTypeReference ? null : instance, netvalue);

                        return true;
                    }
            }
            catch
            {
            }

            return false;
        }

        public bool TryGetNETMember(object? instance, string member, out Variant value)
        {
            try
            {
                if (GetMembers(instance).Where(m => m.Name == member).FirstOrDefault() is { } m)
                {
                    instance = instance is StaticTypeReference ? null : instance;

                    switch (m)
                    {
                        case FieldInfo field:
                            {
                                object? netvalue = field.GetValue(instance);

                                value = Variant.FromObject(Interpreter, netvalue);

                                return true;
                            }
                        case PropertyInfo property:
                            {
                                object? netvalue = property.GetValue(instance);

                                value = Variant.FromObject(Interpreter, netvalue);

                                return true;
                            }
                        case MethodInfo method:
                            {
                                value = new NETFrameworkFunction(Interpreter, method, instance);

                                return true;
                            }
                    }
                }
            }
            catch
            {
            }

            value = Variant.Null;

            return false;
        }

        internal bool TryInvokeNETMember(object? instance, MethodInfo method, IEnumerable<Variant> arguments, out Variant value)
        {
            try
            {
                ParameterInfo[] pars = method.GetParameters();
                object? result = method.Invoke(instance, arguments.ToArray((a, i) => a.ToCPPObject(pars[i].ParameterType, Interpreter)));

                value = Variant.FromObject(Interpreter, result);

                return true;
            }
            catch
            {
                value = Variant.Null;

                return false;
            }
        }

        public bool TryInvokeNETMember(object instance, string member, Variant[] arguments, out Variant value)
        {
            try
            {
                var methods = from method in GetMembers(instance).OfType<MethodInfo>()
                              where method.Name == member
                              let pars = method.GetParameters()
                              let min_parcount = pars.Count(p => !p.HasDefaultValue)
                              where min_parcount <= arguments.Length
                              orderby pars.Length descending
                              let args = arguments.Take(Math.Min(pars.Length, arguments.Length))
                              select new
                              {
                                  Method = method,
                                  Arguments = args
                              };

                if (methods.FirstOrDefault() is { } meth)
                    return TryInvokeNETMember(instance is StaticTypeReference ? null : instance, meth.Method, meth.Arguments, out value);
            }
            catch
            {
            }

            value = Variant.Null;

            return false;
        }

        public (string Name, bool IsMethod)[] TryListNETMembers(object instance) =>
            GetMembers(instance).ToArray(member => (member.Name, member is MethodBase));

        private MemberInfo[] GetMembers(object? instance) => instance is StaticTypeReference(Type type)
                                                           ? type.GetMembers(BindingFlags.Public | BindingFlags.Static)
                                                           : (instance?.GetType() ?? typeof(void)).GetMembers(BindingFlags.Public | BindingFlags.Instance);
    }

    internal record StaticTypeReference(Type Type)
    {
        public override string ToString() => $"static \"{Type.FullName}\"";
    }
}
