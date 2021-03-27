using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Runtime.ExternalServices;
using Unknown6656.AutoIt3.Common;
using Unknown6656.AutoIt3.COM;
using Unknown6656.AutoIt3.CLI;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class COMConnector
        : ExternalServiceConnector<COMConnector>
        , ICOMResolver<Variant>
    {
        public override string ChannelName { get; } = "COM";

        public Interpreter Interpreter { get; }


        internal COMConnector(Interpreter interpreter)
            : base(MainProgram.COM_CONNECTOR, false, interpreter, interpreter.CurrentUILanguage) => Interpreter = interpreter;

        protected override void BeforeShutdown()
        {
            DataWriter.WriteNative(COMInteropCommand.DeleteAll);
            DataWriter.WriteNative(COMInteropCommand.Quit);
            DataWriter.Flush();
        }

        public uint? TryCreateCOMObject(string classname, string? server, string? username, string? passwd) => Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, delegate
        {
            DataWriter.WriteNative(COMInteropCommand.Create);
            DataWriter.Write(classname);
            DataWriter.WriteNullable(server);
            DataWriter.WriteNullable(username);
            DataWriter.WriteNullable(passwd);
            DataWriter.Flush();

            return DataReader.ReadNullable<uint>();
        });

        public void DeleteObject(uint id) => Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, delegate
        {
            DataWriter.WriteNative(COMInteropCommand.Delete);
            DataWriter.Write(id);
            DataWriter.Flush();
        });

        public string[] GetObjectMembers(uint id) => Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, delegate
        {
            DataWriter.WriteNative(COMInteropCommand.EnumerateMembers);
            DataWriter.Write(id);
            DataWriter.Flush();

            string[] members = new string[DataReader.ReadInt32()];

            for (int i = 0; i < members.Length; ++i)
                members[i] = DataReader.ReadString();

            return members;
        });

        public bool TrySetIndex(uint id, Variant index, Variant value)
        {
            COMData com_index = Convert(index);
            COMData com_value = Convert(value);

            DataWriter.WriteNative(COMInteropCommand.SetIndex);
            DataWriter.Write(id);
            DataWriter.WriteCOM(com_index);
            DataWriter.WriteCOM(com_value);
            DataWriter.Flush();

            return DataReader.ReadBoolean();
        }

        public bool TryGetIndex(uint id, Variant index, out Variant value)
        {
            COMData com_index = Convert(index);
            bool success;

            DataWriter.WriteNative(COMInteropCommand.GetIndex);
            DataWriter.Write(id);
            DataWriter.WriteCOM(com_index);
            DataWriter.Flush();

            success = DataReader.ReadBoolean();
            value = Variant.Null;

            if (success)
            {
                COMData com_value = DataReader.ReadCOM();

                value = Convert(com_value);
            }

            return success;
        }

        public bool TrySetMember(uint id, string name, Variant value)
        {
            COMData com_value = Convert(value);

            DataWriter.WriteNative(COMInteropCommand.SetMember);
            DataWriter.Write(id);
            DataWriter.Write(name);
            DataWriter.WriteCOM(com_value);

            return DataReader.ReadBoolean();
        }

        public bool TryGetMember(uint id, string name, out Variant value)
        {
            value = Variant.Null;

            DataWriter.WriteNative(COMInteropCommand.GetMember);
            DataWriter.Write(id);
            DataWriter.Write(name);
            DataWriter.Flush();

            bool success = DataReader.ReadBoolean();

            if (success)
            {
                COMData com_value = DataReader.ReadCOM();

                value = Convert(com_value);
            }

            return success;
        }

        public bool TryInvoke(uint id, string name, Variant[] arguments, out Variant value)
        {
            value = Variant.Null;

            DataWriter.WriteNative(COMInteropCommand.Invoke);
            DataWriter.Write(id);
            DataWriter.Write(name);
            DataWriter.Write(arguments.Length);

            for (int i = 0; i < arguments.Length; ++i)
            {
                COMData arg = Convert(arguments[i]);

                DataWriter.WriteCOM(arg);
            }

            DataWriter.Flush();

            bool success = DataReader.ReadBoolean();

            if (success)
            {
                COMData com_value = DataReader.ReadCOM();

                value = Convert(com_value);
            }

            return success;
        }

        public bool TryGetCOMObjectInfo(uint id, COMObjectInfoMode mode, [MaybeNullWhen(false), NotNullWhen(true)] out string? info)
        {
            DataWriter.WriteNative(COMInteropCommand.GetInfo);
            DataWriter.Write(id);
            DataWriter.WriteNative(mode);
            DataWriter.Flush();

            info = DataReader.ReadNullable();

            return info is string;
        }

        public (uint id, string type, string clsid, Variant value)[] GetAllCOMObjectInfos()
        {
            DataWriter.WriteNative(COMInteropCommand.GetAllInfos);
            DataWriter.Flush();

            (uint, string, string, Variant)[] objects = new (uint, string, string, Variant)[DataReader.ReadInt32()];

            for (int i = 0; i < objects.Length; ++i)
                objects[i] = (
                    DataReader.ReadUInt32(),
                    DataReader.ReadString(),
                    DataReader.ReadString(),
                    Convert(DataReader.ReadCOM())
                );

            return objects;
        }

        public bool TryResolveCOMObject(uint id, out Variant com_object)
        {
            com_object = Variant.FromCOMObject(id);

            return true;
        }

        uint ICOMResolver<Variant>.GetCOMObjectID(Variant com_object) => (uint)com_object;

        private COMData Convert(Variant com_data) => com_data.Type switch
        {
            VariantType.Null or VariantType.Default => COMData.Null,
            VariantType.Boolean => COMData.FromBool(com_data.ToBoolean()),
            VariantType.Number => FunctionExtensions.Do(delegate
            {
                double d = com_data.ToNumber();

                if ((short)d == d)
                    return COMData.FromInt((short)d);
                else if ((int)d == d)
                    return COMData.FromInt((int)d);
                else if ((long)d == d)
                    return COMData.FromLong((long)d);
                else
                    return COMData.FromDouble(d);
            }),
            VariantType.String => COMData.FromString(com_data.ToString()),
            VariantType.Array => COMData.FromArray(com_data.ToArray(Interpreter).Select(Convert)),
            VariantType.COMObject => COMData.FromCOMObjectID((uint)com_data),

            // TODO
            VariantType.Binary or VariantType.Map or VariantType.Function or VariantType.Handle or VariantType.Reference or _ => throw new NotImplementedException(),
        };

        private Variant Convert(COMData com_data)
        {
            if (com_data.IsBool(out bool b))
                return b;
            else if (com_data.IsByte(out byte by))
                return by;
            else if (com_data.IsShort(out short s))
                return s;
            else if (com_data.IsInt(out int i))
                return i;
            else if (com_data.IsLong(out long l))
                return l;
            else if (com_data.IsFloat(out float f))
                return f;
            else if (com_data.IsDouble(out double d))
                return d;
            else if (com_data.IsString(out string? str))
                return str;
            else if (com_data.IsCOM(out Variant com))
                return com;
            else if (com_data.IsArray(out COMData[]? array))
                return Variant.FromArray(Interpreter, array?.ToArray(Convert));
            else if (com_data.IsNull)
                return Variant.Null;
            else
                throw new NotImplementedException();
        }
    }
}
