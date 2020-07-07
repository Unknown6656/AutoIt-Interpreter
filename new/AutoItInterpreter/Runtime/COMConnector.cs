using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System;

using Unknown6656.AutoIt3.COM;
using System.Diagnostics.CodeAnalysis;

namespace Unknown6656.AutoIt3.Runtime
{
    using COMData = COMData<Variant>;

    public sealed class COMConnector
        : ICOMConverter<Variant>
        , IDisposable
    {
        private readonly NamedPipeClientStream _client;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly Process _process;

        public Interpreter Interpreter { get; }

        public string PipeName { get; }


        internal COMConnector(Interpreter interpreter)
        {
            Program.PrintDebugMessage("Starting COM connector service ...");

            COMData.Converter = this;

            PipeName = $"__autoit3_com_connector_{Guid.NewGuid():N}";
            Interpreter = interpreter;
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    Arguments = PipeName,
                    FileName = Program.COM_CONNECTOR.FullName,
                }
            };
            _process.Start();
            _client = new NamedPipeClientStream(PipeName);
            _client.Connect();
            _reader = new BinaryReader(_client);
            _writer = new BinaryWriter(_client);
            _writer.WriteNative(COMInteropCommand._none_);

            Program.PrintDebugMessage($"COM connector service '{PipeName}' started.");
        }

        public void Stop()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _writer.WriteNative(COMInteropCommand.Quit);
                    _writer.Flush();
                    _process.WaitForExit(1000);
                    _writer.Close();
                    _reader.Close();
                    _client.Close();
                    _process.Kill();
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Program.PrintDebugMessage($"Disposing COM connector service '{PipeName}' ...");

            Stop();

            _process.Dispose();
            _writer.Dispose();
            _reader.Dispose();
            _client.Dispose();
        }

        public uint? TryCreateCOMObject(string classname, string? server, string? username, string? passwd) => Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, delegate
        {
            _writer.WriteNative(COMInteropCommand.Create);
            _writer.Write(classname);
            _writer.WriteNullable(server);
            _writer.WriteNullable(username);
            _writer.WriteNullable(passwd);
            _writer.Flush();

            return _reader.ReadNullable<uint>();
        });

        public void DeleteObject(uint id) => Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, delegate
        {
            _writer.WriteNative(COMInteropCommand.Delete);
            _writer.Write(id);
            _writer.Flush();
        });

        public string[] GetObjectMembers(uint id) => Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, delegate
        {
            _writer.WriteNative(COMInteropCommand.EnumerateMembers);
            _writer.Write(id);
            _writer.Flush();

            string[] members = new string[_reader.ReadInt32()];

            for (int i = 0; i < members.Length; ++i)
                members[i] = _reader.ReadString();

            return members;
        });

        public bool TrySetIndex(uint id, Variant index, Variant value)
        {
            COMData com_index = Convert(index);
            COMData com_value = Convert(value);

            _writer.Write(id);
            _writer.WriteCOM(com_index);
            _writer.WriteCOM(com_value);
            _writer.Flush();

            return _reader.ReadBoolean();
        }

        public bool TryGetIndex(uint id, Variant index, out Variant value)
        {
            COMData com_index = Convert(index);
            bool success;

            _writer.Write(id);
            _writer.WriteCOM(com_index);
            _writer.Flush();

            success = _reader.ReadBoolean();
            value = Variant.Null;

            if (success)
            {
                COMData com_value = _reader.ReadCOM<Variant>();

                value = Convert(com_value);
            }

            return success;
        }

        public bool TrySetMember(uint id, string name, Variant value)
        {
            COMData com_value = Convert(value);

            _writer.Write(id);
            _writer.Write(name);
            _writer.WriteCOM(com_value);
            _writer.Flush();

            return _reader.ReadBoolean();
        }

        public bool TryGetMember(uint id, string name, out Variant value)
        {
            value = Variant.Null;

            _writer.Write(id);
            _writer.Write(name);
            _writer.Flush();

            bool success = _reader.ReadBoolean();

            if (success)
            {
                COMData com_value = _reader.ReadCOM<Variant>();

                value = Convert(com_value);
            }

            return success;
        }

        public bool TryInvoke(uint id, string name, Variant[] arguments, out Variant value)
        {
            value = Variant.Null;

            _writer.Write(id);
            _writer.Write(name);
            _writer.Write(arguments.Length);

            for (int i = 0; i < arguments.Length; ++i)
            {
                COMData arg = Convert(arguments[i]);

                _writer.WriteCOM(arg);
            }

            _writer.Flush();

            bool success = _reader.ReadBoolean();

            if (success)
            {
                COMData com_value = _reader.ReadCOM<Variant>();

                value = Convert(com_value);
            }

            return success;
        }

        public Variant Read(BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        public void Write(BinaryWriter writer, Variant data)
        {
            throw new NotImplementedException();
        }

        private COMData<Variant> Convert(Variant index)
        {
            throw new NotImplementedException();
        }

        private Variant Convert(COMData<Variant> com_value)
        {
            throw new NotImplementedException();
        }
    }
}
