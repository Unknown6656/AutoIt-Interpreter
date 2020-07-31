using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Reflection.Emit;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Parser.DLLStructParser;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    using static AST;

    /// <summary>
    /// A module for creating AutoIt/native function delegates and callbacks. These are used for native interop and are <b>not</b> for the feint of heart.
    /// </summary>
    public sealed class DelegateBuilder
    {
        private static readonly ConcurrentDictionary<int, NativeDelegateWrapper> _nativecache = new();

        public static DelegateBuilder Instance { get; } = new DelegateBuilder();


        private readonly AssemblyBuilder _assembly;
        private readonly ModuleBuilder _module;


        private DelegateBuilder()
        {
            _assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(GetRandomName()), AssemblyBuilderAccess.RunAndCollect);
            _module = _assembly.DefineDynamicModule(nameof(DelegateBuilder));
        }

        public UserFunctionCallback? CreateUserFunctionCallback(SIGNATURE signature, Func<(object?, Type)[], Type, object?> callback)
        {
            try
            {
                TypeBuilder type_builder = _module.DefineType(GetRandomName(), TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Public | TypeAttributes.BeforeFieldInit, typeof(object));
                FieldBuilder field_func_builder = type_builder.DefineField("_function", typeof(Func<(object, Type)[], Type, object>), FieldAttributes.Public | FieldAttributes.Static);
                FieldBuilder field_rettype_builder = type_builder.DefineField("_rettype", typeof(Type), FieldAttributes.Public | FieldAttributes.Static);
                FieldBuilder field_ptr_builder = type_builder.DefineField("_pointer", typeof(nint), FieldAttributes.Public | FieldAttributes.Static);

                Type? rettype = ConvertType(signature.ReturnType.Type, false);
                Type?[] @params = signature.ParameterTypes.ToArray(t => ConvertType(t, true));

                if (rettype is null || @params.Contains(null))
                    return null;

                MethodBuilder method_builder = type_builder.DefineMethod(
                    "Invoke",
                    MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Static,
                    CallingConventions.Standard,
                    rettype,
                    @params!
                );
                ILGenerator method_il = method_builder.GetILGenerator();
                LocalBuilder local_builder = method_il.DeclareLocal(typeof((object, Type)[]));

                method_il.Emit(OpCodes.Ldc_I4, @params.Length);
                method_il.Emit(OpCodes.Newarr, typeof(object));
                method_il.Emit(OpCodes.Stloc_0);

                MethodInfo gettypefromhnd = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!;
                ConstructorInfo tuplector = typeof((object, Type)).GetConstructor(new[] { typeof(object), typeof(Type) })!;

                for (int i = 0; i < @params.Length; ++i)
                {
                    method_il.Emit(OpCodes.Ldloc_0);
                    method_il.Emit(OpCodes.Ldc_I4, i);
                    method_il.Emit(OpCodes.Ldarg, i + 1);

                    if (@params[i] is { IsValueType : true } t)
                        method_il.Emit(OpCodes.Box, t);

                    method_il.Emit(OpCodes.Ldtoken, @params[i]!);
                    method_il.Emit(OpCodes.Call, gettypefromhnd);
                    method_il.Emit(OpCodes.Newobj, tuplector);
                    method_il.Emit(OpCodes.Stelem_Ref);
                }

                method_il.Emit(OpCodes.Ldsfld, typeof(object).GetConstructor(Array.Empty<Type>())!);
                method_il.Emit(OpCodes.Ldloc_0);
                method_il.Emit(OpCodes.Callvirt, typeof(Func<(object, Type)[], Type, object>).GetMethod(nameof(callback.Invoke))!);

                if (rettype.IsValueType)
                    method_il.Emit(OpCodes.Unbox_Any, rettype);

                method_il.Emit(OpCodes.Ret);

                ConstructorBuilder cctor_builder = type_builder.DefineConstructor(MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.HideBySig, CallingConventions.Standard, null);
                ILGenerator il_cctor = cctor_builder.GetILGenerator();

                il_cctor.Emit(OpCodes.Ldftn, method_builder);
                il_cctor.Emit(OpCodes.Call, typeof(IntPtr).GetMethod("op_Explicit", new[] { typeof(void*) })!);
                il_cctor.Emit(OpCodes.Stsfld, field_ptr_builder);
                il_cctor.Emit(OpCodes.Ret);

                if (type_builder.CreateType() is Type type &&
                    type.GetField(field_ptr_builder.Name) is FieldInfo field_ptr &&
                    type.GetField(field_func_builder.Name) is FieldInfo field_func &&
                    type.GetField(field_rettype_builder.Name) is FieldInfo field_rettype &&
                    type.GetMethod(method_builder.Name) is MethodInfo method)
                {
                    field_rettype.SetValue(null, rettype);
                    field_func.SetValue(null, callback);

                    nint ptr = (nint)field_ptr.GetValue(null)!;

                    return new UserFunctionCallback(ptr);
                }
            }
            catch (Exception ex)
            {
            }

            return null;
        }

        public NativeDelegateWrapper? CreateNativeDelegateType(SIGNATURE signature)
        {
            int hash = signature.GetHashCode();

            if (_nativecache.TryGetValue(hash, out NativeDelegateWrapper? wrapper))
                return wrapper;

            try
            {
                TypeBuilder delegate_builder = _module.DefineType(GetRandomName(), TypeAttributes.Sealed | TypeAttributes.Public, typeof(MulticastDelegate));
                CallingConvention callconv = signature.ReturnType.CallConvention.IsFastcall ? CallingConvention.FastCall :
                                             signature.ReturnType.CallConvention.IsStdcall ? CallingConvention.StdCall :
                                             signature.ReturnType.CallConvention.IsThiscall ? CallingConvention.ThisCall :
                                             signature.ReturnType.CallConvention.IsWinAPI ? CallingConvention.Winapi : CallingConvention.Cdecl;

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

                Type?[] @params = signature.ParameterTypes.ToArray(t => ConvertType(t, true));

                MethodBuilder invoke = delegate_builder.DefineMethod(
                    "Invoke",
                    MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Public,
                    CallingConventions.Standard,
                    ConvertType(signature.ReturnType.Type, false),
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

                ProcessParameter(0, signature.ReturnType.Type);

                for (int i = 0; i < @params.Length; i++)
                    if (@params[i] is null)
                        return null;
                    else
                        ProcessParameter(i + 1, signature.ParameterTypes[i]);

                if (delegate_builder.CreateType() is Type type &&
                    type.GetMethod(invoke.Name) is MethodInfo inv &&
                    type.GetConstructor(new[] { typeof(object), typeof(nint) }) is ConstructorInfo ctor)
                {
                    wrapper = new NativeDelegateWrapper(type, ctor, inv);

                    _nativecache[hash] = wrapper;

                    return wrapper;
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
                return typeof(IntPtr);
                //return typeof(nint);
                //return typeof(void*);
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
#if DEBUG
        public void SaveAssemly(string path) => new Lokad.ILPack.AssemblyGenerator().GenerateAssembly(_assembly, path);
#endif
        private static string GetRandomName(string prefix = "", string suffix = "") => prefix + Guid.NewGuid() + suffix;
    }

    public unsafe record UserFunctionCallback(nint FunctionPointer)
    {
        /// <summary>
        /// A string prefix reserved only for interpreter errors. Must be a sequence which no one in their right mind would use as a real function return value.
        /// </summary>
        internal static readonly string ErrorPrefix = $"{DateTime.UtcNow}\U0001F1EA\U0001F1F7\U0001F1F7\U0001F1F4\U0001F1F7:";

        public static Func<(object?, Type)[], Type, object?> CreateNativeCallback(ScriptFunction function, Interpreter interpreter) => (arguments, type) =>
        {
            if (arguments.Length < function.ParameterCount.MinimumCount)
                Array.Resize(ref arguments, function.ParameterCount.MinimumCount);
            else if (arguments.Length > function.ParameterCount.MaximumCount)
                Array.Resize(ref arguments, function.ParameterCount.MaximumCount);

            Variant[] args = arguments.ToArray(a => Variant.FromObject(interpreter, a.Item1));
            using AU3Thread thread = interpreter.CreateNewThread();

            Union<InterpreterError, Variant> result = thread.Start(function, args);

            if (result.Is(out Variant value))
                return value.ToCPPObject(type, interpreter);
            else if (result.Is(out InterpreterError? error))
                ; // TODO : ErrorPrefix +  error

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        };
    }

    public unsafe record NativeDelegateWrapper(Type Type, ConstructorInfo Constructor, MethodInfo Invoker)
    {
        internal static readonly FieldInfo _methodPtr = typeof(Delegate).GetField(nameof(_methodPtr), BindingFlags.NonPublic | BindingFlags.Instance)!;
        internal static readonly FieldInfo _methodPtrAux = typeof(Delegate).GetField(nameof(_methodPtrAux), BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly delegate*<void> pdummy = &DummyMethod;


        public object? CallCPP(void* funcptr, params object?[] arguments) => CallCPP((nint)funcptr, arguments);

        //debugging this method will crash the entire application
        [DebuggerNonUserCode, DebuggerHidden, DebuggerStepThrough]
        public object? CallCPP(nint funcptr, params object?[] arguments)
        {
            object @delegate = Constructor.Invoke(new object?[] { null, (nint)pdummy });
            object? result;

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

        public Variant CallCPPfromAutoit(void* funcptr, Interpreter interpreter, Variant[] arguments) => CallCPPfromAutoit((nint)funcptr, interpreter, arguments);

        public Variant CallCPPfromAutoit(nint funcptr, Interpreter interpreter, Variant[] arguments)
        {
            Type[] ptypes = Invoker.GetParameters().ToArray(p => p.ParameterType);
            object?[] cpp_arguments = new object?[ptypes.Length];

            for (int i = 0; i < cpp_arguments.Length; ++i)
                if (i < arguments.Length)
                    cpp_arguments[i] = arguments[i].ToCPPObject(ptypes[i], interpreter);
                else
                    cpp_arguments[i] = ptypes[i].IsValueType ? Activator.CreateInstance(ptypes[i]) : null;

            object? result = CallCPP(funcptr, cpp_arguments);

            return Variant.FromObject(interpreter, result);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void DummyMethod()
        {
        }
    }
}
