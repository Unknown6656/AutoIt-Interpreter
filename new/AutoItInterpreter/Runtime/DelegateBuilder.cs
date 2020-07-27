using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class DelegateBuilder
    {
        public static DelegateBuilder Instance { get; } = new DelegateBuilder();


        private readonly AssemblyBuilder _assembly;
        private readonly ModuleBuilder _module;


        private DelegateBuilder()
        {
            _assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(nameof(DelegateBuilder)), AssemblyBuilderAccess.RunAndCollect);
            _module = _assembly.DefineDynamicModule(nameof(DelegateBuilder));
        }

        public Type? CreateDelegateType(Type return_type, params Type[] parameters)
        {
            try
            {
                string delegate_name = Guid.NewGuid().ToString("N");
                TypeBuilder delegate_builder = _module.DefineType(delegate_name, TypeAttributes.Sealed | TypeAttributes.Public, typeof(MulticastDelegate));

                delegate_builder.DefineConstructor(
                    MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                    CallingConventions.Standard,
                    new[] { typeof(object), typeof(nint) }
                ).SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

                MethodBuilder invoke = delegate_builder.DefineMethod(
                    "Invoke",
                    MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public,
                    return_type,
                    parameters
                );
                invoke.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

                for (int i = 0; i < parameters.Length; i++)
                {
                    ParameterBuilder par = invoke.DefineParameter(i + 1, ParameterAttributes.None, "p" + i);

                    // par.SetCustomAttribute(new CustomAttributeBuilder(parameters[i].attributes[0].GetType().GetConstructor()))
                }

                return delegate_builder.CreateType();
            }
            catch
            {
                return null;
            }
        }

        public static Type? TranslateDLLType(string typestring) => typestring.ToUpperInvariant() switch
        {
            "U0" or "NONE" => typeof(void),
            "U1" or "BYTE" or "BOOLEAN" => typeof(byte),
            "I2" or "SHORT" => typeof(short),
            "U2" or "USHORT" or "WORD" => typeof(ushort),
            "I4" or "INT" or "LONG" or "BOOL" or "HANDLE" => typeof(int),
            "U4" or "UINT" or "ULONG" or "DWORD" => typeof(uint),
            "I8" or "INT64" or "LONGLONG" or "LARGE_INTEGER" => typeof(long),
            "U8" or "UINT64" or "ULONGLONG" or "ULARGE_INTEGER" => typeof(ulong),
            "R4" or "FLOAT" => typeof(float),
            "R8" or "DOUBLE" => typeof(double),
            "INT_PTR" or "LONG_PTR" or "LRESULT" or "LPARAM" => typeof(nint),
            "UINT_PTR" or "ULONG_PTR" or "DWORD_PTR" or "WPARAM" or "ULONG_PTR" => typeof(nuint),
            "I2" or "PTR" or "LPVOID" or "HANDLE" or "HWND" or "HINSTANCE" or "STRUCT*" or "LPSTRUCT" => typeof(nint), // void*
            "STR" or "LPCSTR" or "LPSTR" => typeof(string), // ansi
            "WSTR" or "LPCWSTR" or "LPWSTR" => typeof(string), // utf16
            { Length: > 2 } s when s[..2] == "LP" => TranslateDLLType(s + '*'),
            { Length: > 1 } s when s[^1] == '*' => typeof(nint),

            "STRUCT" => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };

    }
}
