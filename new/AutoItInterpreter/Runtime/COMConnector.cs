using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.COM;
using Unknown6656.Common;

using EnvDTE;

using DTEProcess = EnvDTE.Process;
using Process = System.Diagnostics.Process;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class COMConnector
        : ICOMResolver<Variant>
        , IDisposable
    {
        private readonly NamedPipeClientStream _client;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly Task _debug_monitor;

        public Interpreter Interpreter { get; }

        public Process ServerProcess { get; }

        public string PipeName { get; }


        internal COMConnector(Interpreter interpreter)
        {
            MainProgram.PrintDebugMessage(MainProgram.CurrentLanguage["debug.com_starting"]);

            COMData.RegisterCOMResolver(this);

            PipeName = $"__autoit3_com_connector_{Guid.NewGuid():N}";
            Interpreter = interpreter;
            ServerProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    Arguments = $"{PipeName} {PipeName}D",
                    FileName = Path.GetFullPath(MainProgram.COM_CONNECTOR.FullName),
                }
            };
            ServerProcess.Start();
            _debug_monitor = Task.Factory.StartNew(async () => await DebugMonitorTask(PipeName + 'D'));
            _client = new NamedPipeClientStream(PipeName);
            _client.Connect();
            _reader = new BinaryReader(_client);
            _writer = new BinaryWriter(_client);

            MainProgram.PrintDebugMessage(MainProgram.CurrentLanguage["debug.com_started", PipeName]);
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
                AttachDebuggerToCOMServer();
        }

        private void AttachDebuggerToCOMServer()
        {
            using Process vs_current = Process.GetCurrentProcess();

            try
            {
                if (VisualStudioAttacher.GetAttachedVisualStudio(vs_current) is (Process vs_proc, _DTE vs_inst))
                    using (vs_proc)
                    {
                        VisualStudioAttacher.AttachVisualStudioToProcess(vs_inst, ServerProcess);

                        MainProgram.PrintDebugMessage(MainProgram.CurrentLanguage["debug.com_vsdbg_attached"]);

                        return;
                    }
            }
            catch
            {
                MainProgram.PrintDebugMessage(MainProgram.CurrentLanguage["debug.com_vsdbg_error"]);
            }
#endif
        }

        private async Task DebugMonitorTask(string debug_channel_name)
        {
            using NamedPipeServerStream _debug_server = new NamedPipeServerStream(debug_channel_name);

            await _debug_server.WaitForConnectionAsync();

            using BinaryReader _debug_reader = new BinaryReader(_debug_server);

            while (!ServerProcess.HasExited)
                MainProgram.PrintCOMMessage(Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, _debug_reader.ReadString));
        }

        public void Stop(bool force)
        {
            try
            {
                if (!ServerProcess.HasExited)
                {
                    _writer.WriteNative(COMInteropCommand.DeleteAll);
                    _writer.Flush();
                    _client.Flush();
                    _writer.WriteNative(COMInteropCommand.Quit);
                    _writer.Flush();
                    _client.Flush();

                    if (force)
                        ServerProcess.WaitForExit(1000);
                    else
                        ServerProcess.WaitForExit();

                    _writer.Close();
                    _reader.Close();
                    _client.Close();

                    if (force)
                        ServerProcess.Kill();

                    _debug_monitor.Wait();
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            MainProgram.PrintDebugMessage(MainProgram.CurrentLanguage["debug.com_disposed", PipeName]);

            Stop(false);

            ServerProcess.Dispose();
            _writer.Dispose();
            _reader.Dispose();
            _client.Dispose();
            _debug_monitor.Dispose();
        }

        public uint? TryCreateCOMObject(string classname, string? server, string? username, string? passwd) => Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, delegate
        {
            _writer.WriteNative(COMInteropCommand.Create);
            _writer.Write(classname);
            _writer.WriteNullable(server);
            _writer.WriteNullable(username);
            _writer.WriteNullable(passwd);
            _writer.Flush();
            _client.Flush();

            return _reader.ReadNullable<uint>();
        });

        public void DeleteObject(uint id) => Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, delegate
        {
            _writer.WriteNative(COMInteropCommand.Delete);
            _writer.Write(id);
            _writer.Flush();
            _client.Flush();
        });

        public string[] GetObjectMembers(uint id) => Interpreter.Telemetry.Measure(TelemetryCategory.COMConnection, delegate
        {
            _writer.WriteNative(COMInteropCommand.EnumerateMembers);
            _writer.Write(id);
            _writer.Flush();
            _client.Flush();

            string[] members = new string[_reader.ReadInt32()];

            for (int i = 0; i < members.Length; ++i)
                members[i] = _reader.ReadString();

            return members;
        });

        public bool TrySetIndex(uint id, Variant index, Variant value)
        {
            COMData com_index = Convert(index);
            COMData com_value = Convert(value);

            _writer.WriteNative(COMInteropCommand.SetIndex);
            _writer.Write(id);
            _writer.WriteCOM(com_index);
            _writer.WriteCOM(com_value);
            _writer.Flush();
            _client.Flush();

            return _reader.ReadBoolean();
        }

        public bool TryGetIndex(uint id, Variant index, out Variant value)
        {
            COMData com_index = Convert(index);
            bool success;

            _writer.WriteNative(COMInteropCommand.GetIndex);
            _writer.Write(id);
            _writer.WriteCOM(com_index);
            _writer.Flush();
            _client.Flush();

            success = _reader.ReadBoolean();
            value = Variant.Null;

            if (success)
            {
                COMData com_value = _reader.ReadCOM();

                value = Convert(com_value);
            }

            return success;
        }

        public bool TrySetMember(uint id, string name, Variant value)
        {
            COMData com_value = Convert(value);

            _writer.WriteNative(COMInteropCommand.SetMember);
            _writer.Write(id);
            _writer.Write(name);
            _writer.WriteCOM(com_value);
            _writer.Flush();
            _client.Flush();

            return _reader.ReadBoolean();
        }

        public bool TryGetMember(uint id, string name, out Variant value)
        {
            value = Variant.Null;

            _writer.WriteNative(COMInteropCommand.GetMember);
            _writer.Write(id);
            _writer.Write(name);
            _writer.Flush();
            _client.Flush();

            bool success = _reader.ReadBoolean();

            if (success)
            {
                COMData com_value = _reader.ReadCOM();

                value = Convert(com_value);
            }

            return success;
        }

        public bool TryInvoke(uint id, string name, Variant[] arguments, out Variant value)
        {
            value = Variant.Null;

            _writer.WriteNative(COMInteropCommand.Invoke);
            _writer.Write(id);
            _writer.Write(name);
            _writer.Write(arguments.Length);

            for (int i = 0; i < arguments.Length; ++i)
            {
                COMData arg = Convert(arguments[i]);

                _writer.WriteCOM(arg);
            }

            _writer.Flush();
            _client.Flush();

            bool success = _reader.ReadBoolean();

            if (success)
            {
                COMData com_value = _reader.ReadCOM();

                value = Convert(com_value);
            }

            return success;
        }

        public bool TryGetCOMObjectInfo(uint id, COMObjectInfoMode mode, [MaybeNullWhen(false), NotNullWhen(true)] out string? info)
        {
            _writer.WriteNative(COMInteropCommand.GetInfo);
            _writer.Write(id);
            _writer.WriteNative(mode);
            _writer.Flush();
            _client.Flush();

            info = _reader.ReadNullable();

            return info is string;
        }

        public (uint id, string type, string clsid, Variant value)[] GetAllCOMObjectInfos()
        {
            _writer.WriteNative(COMInteropCommand.GetAllInfos);
            _writer.Flush();
            _client.Flush();

            (uint, string, string, Variant)[] objects = new (uint, string, string, Variant)[_reader.ReadInt32()];

            for (int i = 0; i < objects.Length; ++i)
                objects[i] = (
                    _reader.ReadUInt32(),
                    _reader.ReadString(),
                    _reader.ReadString(),
                    Convert(_reader.ReadCOM())
                );

            return objects;
        }

        public bool TryResolveCOMObject(uint id, out Variant com_object)
        {
            com_object = Variant.FromCOMObject(id);

            return true;
        }

        uint ICOMResolver<Variant>.GetCOMObjectID(Variant com_object) => System.Convert.ToUInt32(com_object.RawData);

        private COMData Convert(Variant com_data) => com_data.Type switch
        {
            VariantType.Null or VariantType.Default => COMData.Null,
            VariantType.Boolean => COMData.FromBool(com_data.ToBoolean()),
            VariantType.Number => Generics.Do(delegate
            {
                decimal d = com_data.ToNumber();

                if ((short)d == d)
                    return COMData.FromInt((short)d);
                else if ((int)d == d)
                    return COMData.FromInt((int)d);
                else if ((long)d == d)
                    return COMData.FromLong((long)d);
                else
                    return COMData.FromDouble((double)d);
            }),
            VariantType.String => COMData.FromString(com_data.ToString()),
            VariantType.Array => COMData.FromArray(com_data.ToArray(Interpreter).Select(Convert)),
            VariantType.COMObject => COMData.FromCOMObjectID((uint)com_data.RawData!),

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

#if DEBUG
    public static class VisualStudioAttacher
    {
        public static string? GetSolutionForVisualStudio(Process vs_process)
        {
            if (TryGetVsInstance(vs_process.Id, out _DTE? instance))
                try
                {
                    return instance.Solution.FullName;
                }
                catch
                {
                }

            return null;
        }

        public static (Process VisualStudioProcess, _DTE VisualStudioInstance)? GetAttachedVisualStudio(Process app_process)
        {
            foreach (Process vs_process in GetVisualStudioProcesses())
                if (TryGetVsInstance(vs_process.Id, out _DTE? instance) && instance?.Debugger.DebuggedProcesses is Processes processes)
                    foreach (DTEProcess? dbg_process in processes)
                        if (dbg_process?.ProcessID == app_process.Id)
                            return (vs_process, instance);

            return null;
        }

        public static void AttachVisualStudioToProcess(_DTE vs_instance, Process target_process)
        {
            DTEProcess? proc = vs_instance.Debugger.LocalProcesses.Cast<DTEProcess>().FirstOrDefault(p => p.ProcessID == target_process.Id);

            if (proc is { })
                proc.Attach();
            else
                throw new InvalidOperationException($"Visual Studio process cannot find specified application '{target_process.Id}'");
        }

        public static Process? GetVisualStudioForSolutions(List<string> solutionNames)
        {
            foreach (string solution in solutionNames)
                if (GetVisualStudioForSolution(solution) is Process proc)
                    return proc;

            return null;
        }

        public static Process? GetVisualStudioForSolution(string solutionName)
        {
            foreach (Process proc in GetVisualStudioProcesses())
                if (TryGetVsInstance(proc.Id, out _DTE? instance))
                    try
                    {
                        string name = Path.GetFileName(instance.Solution.FullName);

                        if (string.Equals(name, solutionName, StringComparison.InvariantCultureIgnoreCase))
                            return proc;
                    }
                    catch
                    {
                    }

            return null;
        }

        private static IEnumerable<Process> GetVisualStudioProcesses() => Process.GetProcesses().Where(o => o.ProcessName.Contains("devenv"));

        private static bool TryGetVsInstance(int processId, [MaybeNullWhen(false), NotNullWhen(true)] out _DTE? instance)
        {
            IMoniker[] monikers = new IMoniker[1];

            NativeInterop.GetRunningObjectTable(0, out IRunningObjectTable runningObjectTable);

            runningObjectTable.EnumRunning(out IEnumMoniker monikerEnumerator);
            monikerEnumerator.Reset();

            while (monikerEnumerator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                NativeInterop.CreateBindCtx(0, out IBindCtx ctx);

                monikers[0].GetDisplayName(ctx, null, out string running_name);
                runningObjectTable.GetObject(monikers[0], out object? running_obj);

                if (running_obj is _DTE && running_name.StartsWith("!VisualStudio"))
                    if (int.Parse(running_name.Split(':')[1]) == processId)
                    {
                        instance = (_DTE)running_obj;

                        return true;
                    }
            }

            instance = null;

            return false;
        }
    }
#endif
}
