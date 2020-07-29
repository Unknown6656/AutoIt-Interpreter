using System.Runtime.InteropServices;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Parser.DLLStructParser;
using Unknown6656.Common;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Unknown6656.AutoIt3.Runtime.Native;

namespace Unknown6656.AutoIt3.Runtime
{
    using static AST;

    public sealed class DelegateBuilder
    {
        public static DelegateBuilder Instance { get; } = new DelegateBuilder();


        private readonly AssemblyBuilder _assembly;
        private readonly ModuleBuilder _module;


        private DelegateBuilder()
        {
            _assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(GetRandomName()), AssemblyBuilderAccess.RunAndCollect);
            _module = _assembly.DefineDynamicModule(nameof(DelegateBuilder));
        }

        public NativeDelegateWrapper? CreateDelegateType(ANNOTATED_TYPE return_type, params TYPE[] parameters)
        {
            try
            {
                TypeBuilder delegate_builder = _module.DefineType(GetRandomName(), TypeAttributes.Sealed | TypeAttributes.Public, typeof(MulticastDelegate));
                CallingConvention callconv = return_type.CallConvention.IsFastcall ? CallingConvention.FastCall :
                                             return_type.CallConvention.IsStdcall ? CallingConvention.StdCall :
                                             return_type.CallConvention.IsThiscall ? CallingConvention.ThisCall :
                                             return_type.CallConvention.IsWinAPI ? CallingConvention.Winapi : CallingConvention.Cdecl;

                delegate_builder.SetCustomAttribute(new CustomAttributeBuilder(
                    typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new[] { typeof(CallingConvention) })!,
                    new object[] { callconv }
                ));

                ConstructorBuilder constructor = delegate_builder.DefineConstructor(
                    MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                    CallingConventions.Standard,
                    new[] { typeof(object), typeof(nint) }
                );
                constructor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);
                // constructor.DefineParameter(1, ParameterAttributes.None, "object");
                // constructor.DefineParameter(2, ParameterAttributes.None, "method");

                Type?[] @params = parameters.ToArray(t => ConvertType(t, true));

                MethodBuilder invoke = delegate_builder.DefineMethod(
                    "Invoke",
                    MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Public,
                    CallingConventions.Standard,
                    ConvertType(return_type.Type, false),
                    null,
                    new[] {
                        callconv switch
                        {
                            CallingConvention.Cdecl => typeof(CallConvCdecl),
                            CallingConvention.StdCall => typeof(CallConvStdcall),
                            CallingConvention.ThisCall => typeof(CallConvThiscall),
                            CallingConvention.FastCall => typeof(CallConvFastcall),
                            CallingConvention.Winapi when NativeInterop.OperatingSystem == OS.Windows => typeof(CallConvStdcall),
                            _ => typeof(CallConvCdecl),
                        }
                    },
                    @params!,
                    null,
                    null
                );
                invoke.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

                ParameterBuilder ProcessParameter(int index, TYPE type)
                {
                    ParameterAttributes attr = index is 0 ? ParameterAttributes.Retval : ParameterAttributes.None;

                    //if (type.IsWSTR || type.IsSTR)
                    //    attr |= ParameterAttributes.HasFieldMarshal;

                    ParameterBuilder parameter = invoke.DefineParameter(index, attr, index is 0 ? null : "item" + index);

                    if (type.IsWSTR || type.IsSTR)
                        parameter.SetCustomAttribute(new CustomAttributeBuilder(
                            typeof(MarshalAsAttribute).GetConstructor(new[] { typeof(UnmanagedType) })!,
                            new object[] { type.IsWSTR ? UnmanagedType.LPWStr : UnmanagedType.LPStr }
                        ));

                    return parameter;
                }

                ProcessParameter(0, return_type.Type);

                for (int i = 0; i < @params.Length; i++)
                    if (@params[i] is null)
                        return null;
                    else
                        ProcessParameter(i + 1, parameters[i]);

                if (delegate_builder.CreateType() is Type type &&
                    type.GetMethod(invoke.Name) is MethodInfo inv &&
                    type.GetConstructor(new[] { typeof(object), typeof(nint) }) is ConstructorInfo ctor)
                {
                    // new Lokad.ILPack.AssemblyGenerator().GenerateAssembly(_assembly, GetRandomName("__test", ".dll"));

                    return new NativeDelegateWrapper(type, ctor, inv);
                }
            }
            catch (Exception ex)
            {
            }

            return null;
        }

        private Type? ConvertType(TYPE type, bool is_parameter)
        {
            if (type.IsU0)
                return is_parameter ? null : typeof(void);
            else if (type.IsU8)
                return typeof(byte);
            else if (type.IsU16)
                return typeof(ushort);
            else if (type.IsU32)
                return typeof(uint);
            else if (type.IsU64)
                return typeof(ulong);
            else if (type.IsI16)
                return typeof(short);
            else if (type.IsI32)
                return typeof(int);
            else if (type.IsI64)
                return typeof(long);
            else if (type.IsR32)
                return typeof(float);
            else if (type.IsR64)
                return typeof(double);
            else if (type.IsR128)
                return typeof(decimal);
            else if (type.IsSTR || type.IsWSTR)
                return typeof(StringBuilder);
            else if (type.IsPTR)
                return typeof(nint);
            else if (type.IsStruct)
                ; // TODO
            else if (type is TYPE.Composite { Item: { } types })
            {
                TYPE[] otypes = types.ToArray();
                Type?[] fields = types.ToArray(t => ConvertType(t, true));
                bool unicode = otypes.Any(t => t.IsWSTR);

                TypeBuilder builder = _module.DefineType(
                    Guid.NewGuid().ToString("N"),
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit | (unicode ? TypeAttributes.UnicodeClass : TypeAttributes.AnsiClass),
                    typeof(ValueType)
                );

                for (int i = 0; i < fields.Length; i++)
                    if (fields[i] is Type ftype)
                    {
                        FieldAttributes attr = FieldAttributes.Public;

                        // if (otypes[i].IsWSTR || otypes[i].IsSTR)
                        //     attr |= FieldAttributes.HasFieldMarshal;

                        FieldBuilder field = builder.DefineField("Item" + i, ftype, new[] { typeof(MarshalAsAttribute) }, null, attr);

                        if (otypes[i].IsWSTR || otypes[i].IsSTR)
                            field.SetCustomAttribute(new CustomAttributeBuilder(
                                typeof(MarshalAsAttribute).GetConstructor(new[] { typeof(UnmanagedType) })!,
                                new object[] { otypes[i].IsWSTR ? UnmanagedType.LPWStr : UnmanagedType.LPStr }
                            ));
                    }
                    else
                        return null;

                return builder.CreateType();
            }

            return null; // TODO
        }

        private static string GetRandomName(string prefix = "", string suffix = "") => prefix + Guid.NewGuid() + suffix;
    }

    public unsafe record NativeDelegateWrapper(Type Type, ConstructorInfo Constructor, MethodInfo Invoker)
    {
        private static readonly FieldInfo _methodPtr = typeof(Delegate).GetField(nameof(_methodPtr), BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo _methodPtrAux = typeof(Delegate).GetField(nameof(_methodPtrAux), BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly delegate*<void> pdummy = &DummyMethod;
        private static readonly object _mutex = new object();


        public object? Invoke(void* funcptr, params object?[] arguments) => Invoke((nint)funcptr, arguments);

        public object? Invoke(nint funcptr, params object?[] arguments)
        {
            object @delegate = Constructor.Invoke(new object?[] { null, (nint)pdummy });
            object? result;

            lock (_mutex)
                try
                {
                    _methodPtr.SetValue(@delegate, funcptr);
                    _methodPtrAux.SetValue(@delegate, funcptr);

                    result = Invoker.Invoke(@delegate, arguments);
                }
                finally
                {
                    _methodPtr.SetValue(@delegate, (nint)pdummy);
                    _methodPtrAux.SetValue(@delegate, (nint)pdummy);
                }

            return result;
        }

        private static void DummyMethod()
        {
        }
    }
}
